using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.Storage;
using XUnitAssured.Kafka.Steps;

namespace XUnitAssured.Tests.KafkaTests;

[Trait("Category", "Kafka")]
[Trait("Component", "BatchConsumeStep")]
/// <summary>
/// A failed batch consume used to report only the exception: the connection
/// settings and broker logs gathered while it ran were thrown away in the
/// <c>catch</c>. The single-consume step had the same defect and was fixed in
/// 5.0.2; this guards the batch step. It runs against an unreachable broker, so
/// the step must fail — what is asserted is that the failure carries its context,
/// not which exception the client happened to raise.
/// </summary>
public class KafkaBatchConsumeDiagnosticsTests
{
	private const string UnreachableBroker = "127.0.0.1:59099";

	[Fact(DisplayName = "A failed batch consume should carry the connection diagnostics it collected")]
	public async Task Failed_Batch_Consume_Should_Carry_Diagnostics()
	{
		var step = new KafkaBatchConsumeStep
		{
			Topic = "batch-diagnostics-topic",
			MessageCount = 3,
			GroupId = $"batch-diag-{Guid.NewGuid():N}",
			BootstrapServers = UnreachableBroker,
			Timeout = TimeSpan.FromSeconds(1)
		};

		var result = await step.ExecuteAsync(new MockKafkaContext());

		result.Success.ShouldBeFalse("nothing can be consumed from an unreachable broker");
		result.GetProperty<string>("BootstrapServers").ShouldBe(UnreachableBroker);
		result.GetProperty<string>("GroupId").ShouldNotBeNullOrWhiteSpace();
		result.Properties.ContainsKey("BrokerLogs").ShouldBeTrue(
			"broker logs are the first thing to read when a consume fails, so they must be on the result");
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
