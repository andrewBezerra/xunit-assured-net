using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.Storage;
using XUnitAssured.Kafka.Steps;

namespace XUnitAssured.Tests.KafkaTests;

[Trait("Category", "Kafka")]
[Trait("Component", "ProduceStep")]
/// <summary>
/// A message without a key is valid Kafka — the broker picks the partition
/// round-robin — and it is how the README's quick start produces. The step used to
/// reject it before ever talking to a broker, so every keyless produce failed with
/// a validation error. These tests run against an unreachable broker: a keyless
/// produce must get as far as the transport, so the only acceptable failure is the
/// transport's, never a rejection of the key.
/// </summary>
public class KafkaProduceKeylessTests
{
	private const string UnreachableBroker = "127.0.0.1:59099";

	private static KafkaProduceStep BuildStep(object? key, object? value) => new()
	{
		Topic = "keyless-topic",
		Key = key,
		Value = value,
		BootstrapServers = UnreachableBroker,
		Timeout = TimeSpan.FromSeconds(1)
	};

	[Fact(DisplayName = "A produce without a key should not be rejected before reaching the broker")]
	public async Task Keyless_Produce_Should_Not_Be_Rejected_By_Validation()
	{
		var result = await BuildStep(key: null, value: "Hello, Kafka!").ExecuteAsync(new MockKafkaContext());

		// The broker is unreachable, so success is impossible; what matters is why
		// it failed. A validation rejection is an InvalidOperationException raised
		// before any I/O; a transport failure is anything else (typically a timeout).
		result.Success.ShouldBeFalse();
		result.GetProperty<string>("ExceptionType")
			.ShouldNotBe(typeof(InvalidOperationException).FullName,
				"a null key must be handed to the broker, not rejected up front");
		result.Errors.ShouldNotContain(e => e.Contains("Message key", StringComparison.OrdinalIgnoreCase));
	}

	[Fact(DisplayName = "A produce without a value should still be rejected before reaching the broker")]
	public async Task Valueless_Produce_Should_Still_Be_Rejected()
	{
		// Guards that relaxing the key check did not also relax the value check.
		var result = await BuildStep(key: "k", value: null).ExecuteAsync(new MockKafkaContext());

		result.Success.ShouldBeFalse();
		result.GetProperty<string>("ExceptionType").ShouldBe(typeof(InvalidOperationException).FullName);
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
