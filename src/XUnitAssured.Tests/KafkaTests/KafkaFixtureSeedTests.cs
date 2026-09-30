using System.Collections.Generic;

using Confluent.Kafka;

using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.DSL;
using XUnitAssured.Kafka.Testing;

namespace XUnitAssured.Tests.KafkaTests;

[Collection("TestSettings")]
[Trait("Category", "Kafka")]
[Trait("Component", "Fixture")]
/// <summary>
/// <see cref="KafkaClassFixture"/> can now hand what it provides to any scenario
/// through <see cref="ITestContextSeeder"/>, which is what lets it be combined with
/// other providers in <c>Given(api, kafka, browser)</c>. The fixture constructs from
/// <c>testsettings.json</c> (hence the sequential collection) and builds its producer
/// lazily, so no broker is needed to assert what it seeds.
/// </summary>
public class KafkaFixtureSeedTests
{
	[Fact(DisplayName = "The Kafka fixture should seed everything its steps read from the context")]
	public void Fixture_Should_Seed_What_Steps_Read()
	{
		using var fixture = new KafkaClassFixture();

		var scenario = ScenarioDsl.Given(fixture);
		var context = scenario.Context;

		// The keys the Kafka steps resolve: broker, group, credentials, shared producer.
		context.GetProperty<string>("_KafkaBootstrapServers").ShouldBe(fixture.BootstrapServers);
		context.GetProperty<string>("_KafkaGroupId").ShouldBe(fixture.DefaultGroupId);
		context.Properties.ContainsKey("_KafkaAuthConfig").ShouldBeTrue();
		context.GetProperty<IProducer<string, string>>("_KafkaSharedProducer").ShouldBeSameAs(fixture.SharedProducer);
		context.GetProperty<System.Collections.Concurrent.ConcurrentQueue<string>>("_KafkaSharedProducerErrors").ShouldNotBeNull();
	}

	[Fact(DisplayName = "Seeding through Given(fixture) should match what KafkaTestBase.Given() provides")]
	public void Given_Fixture_Should_Match_Base_Class_Given()
	{
		// KafkaTestBase.Given() now delegates to Seed(); a scenario built either way
		// must expose the same Kafka keys, or tests would behave differently depending
		// on which entry point they used.
		using var fixture = new KafkaClassFixture();

		var viaSeeder = ScenarioDsl.Given(fixture).Context;

		var manual = ScenarioDsl.Given().Context;
		fixture.Seed(manual);

		foreach (var key in new[] { "_KafkaBootstrapServers", "_KafkaGroupId", "_KafkaAuthConfig", "_KafkaSharedProducer", "_KafkaSharedProducerErrors" })
		{
			viaSeeder.Properties.ContainsKey(key).ShouldBeTrue(key);
			manual.Properties.ContainsKey(key).ShouldBeTrue(key);
			ReferenceEquals(viaSeeder.Properties[key], manual.Properties[key]).ShouldBeTrue(key);
		}
	}
}
