using Flurl.Http;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Xunit.Abstractions;
using Xunit.Microsoft.DependencyInjection.Abstracts;

namespace XUnitAssured.Remote;

/// <summary>
/// Base class for remote integration tests using HTTP/REST APIs.
/// Provides access to configured FlurlClient and RemoteSettings.
/// Use with xUnit's IClassFixture pattern.
/// </summary>
/// <typeparam name="TFixture">Test fixture that extends RemoteTestFixture</typeparam>
public abstract class RemoteTest<TFixture>
	where TFixture : RemoteTestFixture
{
	protected readonly TFixture _fixture;
	protected readonly ITestOutputHelper _output;

	/// <summary>
	/// Gets the remote settings configured for the test.
	/// </summary>
	protected RemoteSettings Settings { get; }
	
	/// <summary>
	/// Gets the configured FlurlClient for making HTTP requests.
	/// </summary>
	protected FlurlClient WebClient { get; }

	/// <summary>
	/// Gets the test output helper for logging.
	/// </summary>
	protected ITestOutputHelper Output => _output;

	/// <summary>
	/// Initializes a new instance of the RemoteTest class.
	/// </summary>
	/// <param name="output">xUnit test output helper</param>
	/// <param name="fixture">Test fixture providing configuration and dependencies</param>
	protected RemoteTest(ITestOutputHelper output, TFixture fixture)
	{
		_fixture = fixture;
		_output = output;
		
		// Get services from fixture
		Settings = _fixture.GetService<IOptions<RemoteSettings>>(output)!.Value;
		WebClient = _fixture.GetService<FlurlClient>(output)!;
	}
}
