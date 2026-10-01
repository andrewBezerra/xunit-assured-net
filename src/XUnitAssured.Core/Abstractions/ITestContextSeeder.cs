namespace XUnitAssured.Core.Abstractions;

/// <summary>
/// Something that can put what it provides — a broker address, a browser page,
/// credentials — into a scenario's context, so the steps of that scenario find it.
/// </summary>
/// <remarks>
/// <para>
/// Each protocol package reads its needs from <see cref="ITestContext.Properties"/>:
/// the Kafka steps look for the bootstrap servers and authentication a fixture
/// resolved, the Playwright steps for the page the test opened. A fixture or
/// per-test object that implements this interface can hand those over through
/// <c>Given(fixture)</c>, and several of them can be combined in one call, so a
/// scenario that crosses HTTP, Kafka and the browser is configured from one place:
/// </para>
/// <code>
/// var assertions = await Given(api, kafka, browser)
///     .ApiResource("/api/orders").Post(order).Validate(...)
///     .And().On().Topic("orders.created").Consume().ValidateMessage&lt;OrderCreated&gt;(...)
///     .And().NavigateTo("/orders/1")
///     .ExecuteAsync();
/// </code>
/// <para>
/// Core defines only the contract; what gets seeded, and under which keys, belongs
/// to each package, which keeps Core free of any protocol dependency.
/// </para>
/// </remarks>
public interface ITestContextSeeder
{
	/// <summary>
	/// Writes this provider's values into <paramref name="context"/>.
	/// Called once per scenario, before any step runs.
	/// </summary>
	/// <param name="context">The context of the scenario being started.</param>
	void Seed(ITestContext context);
}
