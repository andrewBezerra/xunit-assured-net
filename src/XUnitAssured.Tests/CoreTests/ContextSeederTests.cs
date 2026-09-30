using System;
using System.Net.Http;

using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.DSL;

namespace XUnitAssured.Tests.CoreTests;

[Trait("Category", "Core")]
[Trait("Component", "DSL")]
/// <summary>
/// <c>Given(params ITestContextSeeder[])</c> lets several providers configure one
/// scenario — the way a cross-boundary test gets its HttpClient, broker and page
/// from one call. These tests use stub providers so the behaviour is asserted
/// without any protocol package.
/// </summary>
public class ContextSeederTests
{
	private sealed class Seeder : ITestContextSeeder
	{
		private readonly string _key;
		private readonly object _value;
		public int Calls { get; private set; }

		public Seeder(string key, object value) { _key = key; _value = value; }

		public void Seed(ITestContext context)
		{
			Calls++;
			context.SetProperty(_key, _value);
		}
	}

	private sealed class HttpSeeder : ITestContextSeeder, IHttpClientProvider
	{
		public HttpClient Client { get; } = new();
		public HttpClient CreateClient() => Client;
		public void Seed(ITestContext context) => context.SetProperty("seeded-by-http", true);
	}

	[Fact(DisplayName = "Given(seeders) should put every provider's values into the context")]
	public void Given_Should_Seed_Every_Provider()
	{
		var kafka = new Seeder("_KafkaBootstrapServers", "broker:9092");
		var browser = new Seeder("_PlaywrightPage", "page-object");

		var scenario = ScenarioDsl.Given(kafka, browser);

		scenario.Context.GetProperty<string>("_KafkaBootstrapServers").ShouldBe("broker:9092");
		scenario.Context.GetProperty<string>("_PlaywrightPage").ShouldBe("page-object");
	}

	[Fact(DisplayName = "Given(seeders) should call each provider exactly once")]
	public void Given_Should_Call_Each_Provider_Once()
	{
		var a = new Seeder("a", 1);
		var b = new Seeder("b", 2);

		ScenarioDsl.Given(a, b);

		a.Calls.ShouldBe(1);
		b.Calls.ShouldBe(1);
	}

	[Fact(DisplayName = "An HttpClient provider among the seeders should become the scenario's HttpClient source")]
	public void Http_Provider_Among_Seeders_Should_Be_Registered()
	{
		var http = new HttpSeeder();

		var scenario = ScenarioDsl.Given(new Seeder("x", 1), http);

		scenario.Context.GetProperty<IHttpClientProvider>("HttpClientProvider").ShouldBeSameAs(http);
		scenario.Context.GetProperty<bool>("seeded-by-http").ShouldBeTrue("the provider must also seed its own values");
	}

	[Fact(DisplayName = "The first HttpClient provider should win, matching the single-argument overload")]
	public void First_Http_Provider_Should_Win()
	{
		var first = new HttpSeeder();
		var second = new HttpSeeder();

		var scenario = ScenarioDsl.Given(first, second);

		scenario.Context.GetProperty<IHttpClientProvider>("HttpClientProvider").ShouldBeSameAs(first);
	}

	[Fact(DisplayName = "Given(IHttpClientProvider) should also seed when the provider is a seeder")]
	public void Single_Http_Provider_Overload_Should_Also_Seed()
	{
		var http = new HttpSeeder();

		// The single-argument overload is chosen for one argument; a fixture that
		// provides more than an HttpClient must still get to seed the rest.
		var scenario = ScenarioDsl.Given(http);

		scenario.Context.GetProperty<IHttpClientProvider>("HttpClientProvider").ShouldBeSameAs(http);
		scenario.Context.GetProperty<bool>("seeded-by-http").ShouldBeTrue();
	}

	[Fact(DisplayName = "A later provider should be able to override an earlier one's value")]
	public void Later_Provider_Should_Override_Earlier_Value()
	{
		// Order is the caller's tool for precedence: the last word wins, as with
		// any sequence of SetProperty calls.
		var scenario = ScenarioDsl.Given(new Seeder("k", "first"), new Seeder("k", "second"));

		scenario.Context.GetProperty<string>("k").ShouldBe("second");
	}

	[Fact(DisplayName = "Given(seeders) should reject a null array and null elements")]
	public void Given_Should_Reject_Nulls()
	{
		Should.Throw<ArgumentNullException>(() => ScenarioDsl.Given((ITestContextSeeder[])null!));
		Should.Throw<ArgumentNullException>(() => ScenarioDsl.Given(new Seeder("a", 1), null!));
	}

	[Fact(DisplayName = "Given() with no seeders should start an empty scenario")]
	public void Given_With_No_Seeders_Should_Start_Empty_Scenario()
	{
		var scenario = ScenarioDsl.Given(Array.Empty<ITestContextSeeder>());

		scenario.ShouldNotBeNull();
		scenario.Context.Properties.ShouldBeEmpty();
	}
}
