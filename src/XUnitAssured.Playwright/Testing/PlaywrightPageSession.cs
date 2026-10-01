using System;
using System.Threading.Tasks;

using Microsoft.Playwright;

using XUnitAssured.Core.Abstractions;
using XUnitAssured.Playwright.Configuration;

namespace XUnitAssured.Playwright.Testing;

/// <summary>
/// A browser page opened for one test, ready to be handed to a scenario.
/// </summary>
/// <remarks>
/// <para>
/// Browser steps need a page, and a page belongs to a single test — the fixture owns
/// the browser, not the page. <see cref="PlaywrightTestBase{TFixture}"/> manages one
/// for tests that derive from it; this type does the same for tests that cannot, for
/// example a class that derives from an HTTP or Kafka base and still wants a browser
/// leg in its scenario:
/// </para>
/// <code>
/// await using var browser = await UiFixture.OpenPageAsync();
///
/// var assertions = await Given(ApiFixture, KafkaFixture, browser)
///     .ApiResource("/api/orders").Post(order).Validate(...)
///     .And().On().Topic("orders.created").Consume().ValidateMessage&lt;OrderCreated&gt;(...)
///     .And().NavigateTo("/orders/1")
///     .ExecuteAsync();
/// </code>
/// <para>
/// Disposing the session closes the page and its browser context. Tracing and
/// failure screenshots at test scope remain a feature of the base class.
/// </para>
/// </remarks>
public sealed class PlaywrightPageSession : ITestContextSeeder, IAsyncDisposable
{
	private IBrowserContext? _context;
	private IPage? _page;

	internal PlaywrightPageSession(IBrowserContext context, IPage page, PlaywrightSettings settings)
	{
		_context = context ?? throw new ArgumentNullException(nameof(context));
		_page = page ?? throw new ArgumentNullException(nameof(page));
		Settings = settings ?? throw new ArgumentNullException(nameof(settings));
	}

	/// <summary>
	/// The page this session opened.
	/// </summary>
	/// <exception cref="ObjectDisposedException">Thrown after the session is disposed.</exception>
	public IPage Page => _page ?? throw new ObjectDisposedException(nameof(PlaywrightPageSession));

	/// <summary>
	/// The settings the page was opened with.
	/// </summary>
	public PlaywrightSettings Settings { get; }

	/// <summary>
	/// Puts this session's page and settings into a scenario's context, so browser
	/// steps in that scenario drive this page.
	/// </summary>
	/// <param name="context">The context of the scenario being started.</param>
	public void Seed(ITestContext context)
	{
		if (context == null)
			throw new ArgumentNullException(nameof(context));

		context.SetProperty("_PlaywrightPage", Page);
		context.SetProperty("_PlaywrightSettings", Settings);
	}

	/// <summary>
	/// Closes the page and its browser context. Safe to call more than once.
	/// </summary>
	public async ValueTask DisposeAsync()
	{
		var page = _page;
		var context = _context;
		_page = null;
		_context = null;

		if (page != null)
			await page.CloseAsync();

		if (context != null)
			await context.CloseAsync();
	}
}
