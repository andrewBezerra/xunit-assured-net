using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Confluent.Kafka;

using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.Storage;
using XUnitAssured.Kafka.Steps;

namespace XUnitAssured.Tests.KafkaTests;

[Trait("Category", "Kafka")]
[Trait("Component", "ConsumeStep")]
/// <summary>
/// Tests how a consume step reports diagnostics.
/// The step used to switch librdkafka's verbose <c>Debug</c> tracing on by default and
/// then funnel the resulting broker chatter into the result's error list — costing CPU
/// on every consume and describing informational log lines as failures.
/// These tests run against an unreachable broker with a short timeout, so no Kafka
/// instance is required.
/// </summary>
public class KafkaConsumeDiagnosticsTests
{
	/// <summary>
	/// A port on the loopback interface with nothing listening on it, so the consume
	/// attempt always ends in a timeout rather than a message.
	/// </summary>
	private const string UnreachableBroker = "127.0.0.1:59099";

	private static KafkaConsumeStep BuildStep(ConsumerConfig config) => new()
	{
		Topic = "diagnostics-topic",
		Timeout = TimeSpan.FromSeconds(1),
		BootstrapServers = UnreachableBroker,
		ConsumerConfig = config
	};

	private static ConsumerConfig BuildConfig() => new()
	{
		BootstrapServers = UnreachableBroker,
		GroupId = $"diag-{Guid.NewGuid():N}",
		AutoOffsetReset = AutoOffsetReset.Earliest,
		EnableAutoCommit = false,
		SessionTimeoutMs = 6000,
		HeartbeatIntervalMs = 2000,
		FetchWaitMaxMs = 100
	};

	[Fact(DisplayName = "Consume step should not enable librdkafka debug tracing by default")]
	public async Task ConsumeStep_Should_Not_Enable_Debug_Tracing()
	{
		// Arrange
		var config = BuildConfig();
		config.Debug.ShouldBeNull();

		// Act
		await BuildStep(config).ExecuteAsync(new MockKafkaContext());

		// Assert
		config.Debug.ShouldBeNull(
			"verbose broker tracing costs CPU on every consume and must stay opt-in");
	}

	[Fact(DisplayName = "Consume step should honour an explicitly configured debug setting")]
	public async Task ConsumeStep_Should_Honour_Explicit_Debug_Setting()
	{
		// Arrange
		var config = BuildConfig();
		config.Debug = "cgrp";

		// Act
		await BuildStep(config).ExecuteAsync(new MockKafkaContext());

		// Assert
		config.Debug.ShouldBe("cgrp", "an explicit troubleshooting setting must be left alone");
	}

	[Fact(DisplayName = "Consume step should not report broker log messages as errors")]
	public async Task ConsumeStep_Should_Not_Report_Log_Messages_As_Errors()
	{
		// Arrange
		var config = BuildConfig();
		// Force the broker to be chatty; without the fix these lines land in Errors.
		config.Debug = "broker,protocol";

		// Act
		var result = await BuildStep(config).ExecuteAsync(new MockKafkaContext());

		// Assert
		result.Success.ShouldBeFalse("nothing can be consumed from an unreachable broker");

		var informationalErrors = result.Errors
			.Where(e => e.StartsWith("Info:", StringComparison.OrdinalIgnoreCase)
				|| e.StartsWith("Debug:", StringComparison.OrdinalIgnoreCase))
			.ToList();

		informationalErrors.ShouldBeEmpty(
			"informational broker logs are diagnostics, not errors");
	}

	[Fact(DisplayName = "Consume step should surface broker logs as diagnostics on timeout")]
	public async Task ConsumeStep_Should_Surface_Broker_Logs_As_Diagnostics()
	{
		// Arrange
		var config = BuildConfig();
		config.Debug = "broker,protocol";

		// Act
		var result = await BuildStep(config).ExecuteAsync(new MockKafkaContext());

		// Assert — the chatter is reported, just in the right place.
		//
		// Only the presence and shape of the property are asserted, never how many
		// entries it holds: how much librdkafka emits within the timeout varies by
		// platform and client version, so asserting a count would make this test
		// fail for reasons unrelated to the behaviour it covers.
		result.Properties.ContainsKey("BrokerLogs").ShouldBeTrue();
		result.GetProperty<List<string>>("BrokerLogs").ShouldNotBeNull();
	}

	private sealed class MockKafkaContext : ITestContext
	{
		public IStepStorage Steps { get; } = new StepStorage();
		public IDictionary<string, object?> Properties { get; } = new Dictionary<string, object?>();

		public T? GetProperty<T>(string key)
			=> Properties.TryGetValue(key, out var value) && value is T typed ? typed : default;

		public void SetProperty<T>(string key, T? value) => Properties[key] = value;
	}
}
