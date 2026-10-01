using XUnitAssured.Core.Abstractions;

namespace XUnitAssured.Core.DSL;

/// <summary>
/// Static entry point for the fluent test DSL.
/// Provides the Given() method to start test scenarios.
/// </summary>
public static class ScenarioDsl
{
	/// <summary>
	/// Starts a new test scenario.
	/// This is the entry point for the fluent DSL.
	/// Usage: Given().ApiResource(...).Post(...).Validate(...)
	/// </summary>
	/// <returns>A new test scenario</returns>
	public static ITestScenario Given()
	{
		return new TestScenario();
	}

	/// <summary>
	/// Starts a new test scenario with a custom context.
	/// </summary>
	/// <param name="context">Custom test context</param>
	/// <returns>A new test scenario with the specified context</returns>
	public static ITestScenario Given(ITestContext context)
	{
		return new TestScenario(context);
	}

	/// <summary>
	/// Starts a new test scenario with an HttpClient provider (e.g., test fixture).
	/// Automatically configures the scenario to use the HttpClient from the provider.
	/// </summary>
	/// <param name="httpClientProvider">Provider that supplies HttpClient instances (e.g., WebApplicationFactory fixture)</param>
	/// <returns>A new test scenario pre-configured with the HttpClient</returns>
	/// <example>
	/// <code>
	/// // Traditional way:
	/// Given()
	///     .WithHttpClient(_fixture.CreateClient())
	///     .ApiResource("/api/products")
	///     .Get()
	/// 
	/// // Simplified way with this overload:
	/// Given(_fixture)
	///     .ApiResource("/api/products")
	///     .Get()
	/// </code>
	/// </example>
	public static ITestScenario Given(IHttpClientProvider httpClientProvider)
	{
		if (httpClientProvider == null)
			throw new System.ArgumentNullException(nameof(httpClientProvider));

		var scenario = new TestScenario();

		// Store the HttpClient provider in the context for later use
		scenario.Context.SetProperty("HttpClientProvider", httpClientProvider);

		// A fixture that also provides other things (a broker, a page) seeds them too,
		// so a single object can configure a scenario that crosses boundaries.
		if (httpClientProvider is ITestContextSeeder seeder)
			seeder.Seed(scenario.Context);

		return scenario;
	}

	/// <summary>
	/// Starts a new test scenario whose context is populated by every provider given,
	/// so one call configures a scenario that spans several boundaries.
	/// </summary>
	/// <param name="seeders">
	/// Fixtures or per-test objects that put what they provide into the context —
	/// for example a Kafka fixture, a browser page session and an HTTP fixture.
	/// An <see cref="IHttpClientProvider"/> among them is also registered as the
	/// scenario's HttpClient source, exactly as <see cref="Given(IHttpClientProvider)"/> does.
	/// </param>
	/// <returns>A new test scenario with every provider's values in its context.</returns>
	/// <example>
	/// <code>
	/// var assertions = await Given(api, kafka, browser)
	///     .ApiResource("/api/orders").Post(order).Validate(r =&gt; r.StatusCode.ShouldBe(201))
	///     .And().On().Topic("orders.created").Consume().ValidateMessage&lt;OrderCreated&gt;(m =&gt; ...)
	///     .And().NavigateTo("/orders/1")
	///     // Nothing above has run yet: the chain describes, and this call carries it out.
	///     // A chain without it compiles and does nothing, which passes a test that tested nothing.
	///     .ExecuteAsync();
	/// </code>
	/// </example>
	/// <exception cref="System.ArgumentNullException">Thrown when the array or any element is null.</exception>
	public static ITestScenario Given(params ITestContextSeeder[] seeders)
	{
		if (seeders == null)
			throw new System.ArgumentNullException(nameof(seeders));

		var scenario = new TestScenario();

		foreach (var seeder in seeders)
		{
			if (seeder == null)
				throw new System.ArgumentNullException(nameof(seeders), "One of the providers is null.");

			// The first HttpClient provider wins, matching the single-argument overload.
			if (seeder is IHttpClientProvider httpProvider
				&& scenario.Context.GetProperty<IHttpClientProvider>("HttpClientProvider") == null)
			{
				scenario.Context.SetProperty("HttpClientProvider", httpProvider);
			}

			seeder.Seed(scenario.Context);
		}

		return scenario;
	}
}
