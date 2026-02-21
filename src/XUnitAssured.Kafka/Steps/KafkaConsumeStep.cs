using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Confluent.Kafka;

using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.Results;
using XUnitAssured.Kafka.Configuration;
using XUnitAssured.Kafka.Handlers;
using XUnitAssured.Kafka.Results;

namespace XUnitAssured.Kafka.Steps;

/// <summary>
/// Represents a Kafka message consumption step in a test scenario.
/// Executes Kafka consume operations and returns KafkaStepResult.
/// </summary>
public class KafkaConsumeStep : ITestStep
{
	/// <inheritdoc />
	public string? Name { get; internal set; }

	/// <inheritdoc />
	public string StepType => "Kafka";

	/// <inheritdoc />
	public ITestStepResult? Result { get; private set; }

	/// <inheritdoc />
	public bool IsExecuted => Result != null;

	/// <inheritdoc />
	public bool IsValid { get; private set; }

	/// <summary>
	/// The Kafka topic to consume from.
	/// </summary>
	public string Topic { get; init; } = string.Empty;

	/// <summary>
	/// Expected schema type for the consumed message.
	/// </summary>
	public Type? SchemaType { get; init; }

	/// <summary>
	/// Timeout for consuming a message.
	/// Default is 30 seconds.
	/// </summary>
	public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Kafka consumer configuration.
	/// If not provided, will use default configuration.
	/// </summary>
	public ConsumerConfig? ConsumerConfig { get; init; }

	/// <summary>
	/// Consumer group ID.
	/// Default is "xunitassured-consumer".
	/// </summary>
	public string GroupId { get; init; } = "xunitassured-consumer";

	/// <summary>
	/// Bootstrap servers for Kafka.
	/// Default is "localhost:9092".
	/// </summary>
	public string BootstrapServers { get; init; } = "localhost:9092";

	/// <summary>
	/// Authentication configuration for this Kafka connection.
	/// If null, will try to load from kafkasettings.json.
	/// </summary>
	public KafkaAuthConfig? AuthConfig { get; init; }

	/// <inheritdoc />
	public async Task<ITestStepResult> ExecuteAsync(ITestContext context)
	{
		var startTime = DateTimeOffset.UtcNow;
		var diagnosticProperties = new Dictionary<string, object?>();
		var errorDetails = new List<string>();

		try
		{
			// Resolve bootstrap servers: explicit > context > default
			var resolvedBootstrapServers = BootstrapServers != "localhost:9092"
				? BootstrapServers
				: context.GetProperty<string>("_KafkaBootstrapServers") ?? BootstrapServers;

			var resolvedGroupId = GroupId;
			if (string.Equals(GroupId, "xunitassured-consumer", StringComparison.OrdinalIgnoreCase))
			{
				resolvedGroupId = context.GetProperty<string>("_KafkaGroupId") ?? GroupId;
			}

			// Build consumer config
			var config = ConsumerConfig ?? new ConsumerConfig
			{
				BootstrapServers = resolvedBootstrapServers,
				GroupId = resolvedGroupId,
				AutoOffsetReset = AutoOffsetReset.Earliest,
				EnableAutoCommit = false,
				SessionTimeoutMs = 6000,
				HeartbeatIntervalMs = 2000,
				FetchWaitMaxMs = 100
			};

			// Apply authentication
			ApplyAuthentication(config, context);

			if (string.IsNullOrWhiteSpace(config.Debug))
			{
				config.Debug = "cgrp,protocol,security,broker,fetch";
			}

			diagnosticProperties["BootstrapServers"] = config.BootstrapServers;
			diagnosticProperties["GroupId"] = config.GroupId;
			diagnosticProperties["SecurityProtocol"] = config.SecurityProtocol.ToString();
			diagnosticProperties["SaslMechanism"] = config.SaslMechanism?.ToString();
			diagnosticProperties["SaslUsername"] = config.SaslUsername;

			// Create consumer
			using var consumer = new ConsumerBuilder<string, string>(config)
				.SetErrorHandler((_, error) => errorDetails.Add(error.ToString()))
				.SetLogHandler((_, log) =>
				{
					if (errorDetails.Count < 200)
						errorDetails.Add($"{log.Level}: {log.Message}");
				})
				.SetPartitionsAssignedHandler((_, partitions) =>
				{
					diagnosticProperties["AssignedPartitions"] = string.Join(",", partitions);
				})
				.Build();

			// Subscribe to topic
			consumer.Subscribe(Topic);

			var deadline = DateTime.UtcNow.Add(Timeout);

			while (DateTime.UtcNow <= deadline)
			{
				var consumeResult = consumer.Consume(TimeSpan.FromMilliseconds(250));

				if (consumeResult?.Message != null)
				{
					// Create success result
					Result = KafkaStepResult.CreateKafkaConsumeSuccess(consumeResult);
					return Result;
				}
			}

			if (errorDetails.Any(detail => detail.Contains("COORDINATOR_NOT_AVAILABLE", StringComparison.OrdinalIgnoreCase)))
			{
				diagnosticProperties["Fallback"] = "DirectPartitionAssign";

				consumer.Unsubscribe();

				var adminConfig = new AdminClientConfig
				{
					BootstrapServers = config.BootstrapServers,
					SecurityProtocol = config.SecurityProtocol,
					SaslMechanism = config.SaslMechanism,
					SaslUsername = config.SaslUsername,
					SaslPassword = config.SaslPassword,
					SslCaLocation = config.SslCaLocation,
					EnableSslCertificateVerification = config.EnableSslCertificateVerification
				};

				using var adminClient = new AdminClientBuilder(adminConfig).Build();
				var metadata = adminClient.GetMetadata(Topic, TimeSpan.FromSeconds(5));
				var topicMetadata = metadata.Topics.Find(t => t.Topic == Topic);
				if (topicMetadata != null)
				{
					var partitions = topicMetadata.Partitions
						.Select(p => new TopicPartition(Topic, new Partition(p.PartitionId)))
						.ToList();

					if (partitions.Count > 0)
					{
						try
						{
							var partitionOffsets = partitions
								.Select(partition => new TopicPartitionOffset(partition, Offset.Beginning))
								.ToList();

							consumer.Assign(partitionOffsets);

							var fallbackDeadline = DateTime.UtcNow.Add(Timeout);
							while (DateTime.UtcNow <= fallbackDeadline)
							{
								var consumeResult = consumer.Consume(TimeSpan.FromMilliseconds(250));

								if (consumeResult?.Message != null)
								{
									Result = KafkaStepResult.CreateKafkaConsumeSuccess(consumeResult);
									return Result;
								}
							}
						}
						catch (Exception ex)
						{
							errorDetails.Add(ex.ToString());
						}
					}
				}
			}

			// No message received
			Result = KafkaStepResult.CreateTimeout(Topic, Timeout, errorDetails, diagnosticProperties);
			return Result;
		}
		catch (Exception ex)
		{
			// Network error, Kafka error, etc.
			errorDetails.Add(ex.ToString());
			diagnosticProperties["ExceptionMessage"] = ex.Message;
			Result = KafkaStepResult.CreateFailure(ex);
			return Result;
		}
	}

	/// <inheritdoc />
	public void Validate(Action<ITestStepResult> validation)
	{
		if (Result == null)
			throw new InvalidOperationException("Step must be executed before validation. Call ExecuteAsync first.");

		try
		{
			validation(Result);
			IsValid = true;
		}
		catch
		{
			IsValid = false;
			throw;
		}
	}

	/// <summary>
	/// Applies authentication to the consumer configuration.
	/// Uses AuthConfig if provided, otherwise resolves from context or loads from kafkasettings.json.
	/// </summary>
	private void ApplyAuthentication(ConsumerConfig config, ITestContext context)
	{
		// Get authentication config
		var authConfig = AuthConfig;

		// If no config provided, try to resolve from context (fixture settings)
		authConfig ??= context.GetProperty<KafkaAuthConfig>("_KafkaAuthConfig");

		// If still no config, try to load from settings
		if (authConfig == null)
		{
			var settings = KafkaSettings.Load();
			authConfig = settings.Authentication;
		}

		// Skip if no authentication configured
		if (authConfig == null || authConfig.Type == KafkaAuthenticationType.None)
			return;

		// Create appropriate handler and apply authentication
		IKafkaAuthenticationHandler? handler = authConfig.Type switch
		{
			KafkaAuthenticationType.SaslPlain when authConfig.SaslPlain != null => new SaslPlainHandler(authConfig.SaslPlain),
			KafkaAuthenticationType.SaslSsl when authConfig.SaslPlain != null => new SaslPlainHandler(authConfig.SaslPlain),
			KafkaAuthenticationType.SaslScram256 when authConfig.SaslScram != null => new SaslScramHandler(authConfig.SaslScram),
			KafkaAuthenticationType.SaslScram512 when authConfig.SaslScram != null => new SaslScramHandler(authConfig.SaslScram),
			KafkaAuthenticationType.Ssl when authConfig.Ssl != null => new SslHandler(authConfig.Ssl),
			KafkaAuthenticationType.MutualTls when authConfig.Ssl != null => new SslHandler(authConfig.Ssl),
			_ => null
		};

		handler?.ApplyAuthentication(config);
	}
}
