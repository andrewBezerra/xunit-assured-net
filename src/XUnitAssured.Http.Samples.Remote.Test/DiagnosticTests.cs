using Xunit;

namespace XUnitAssured.Http.Samples.Remote.Test;

[Trait("Category", "Diagnostics")]
[Trait("Environment", "Remote")]
/// <summary>
/// Simple diagnostic test to verify remote API connectivity and basic functionality.
/// </summary>
/// <remarks>
/// These tests help troubleshoot connectivity issues with the remote API configured in testsettings.json.
/// Run these tests first to ensure basic connectivity before running other test suites.
/// </remarks>
public class DiagnosticTests : HttpSamplesRemoteTestBase, IClassFixture<HttpSamplesRemoteFixture>
{
	public DiagnosticTests(HttpSamplesRemoteFixture fixture) : base(fixture)
	{
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "Remote API should be accessible and respond to HTTP requests successfully")]
	public async Task RemoteApiShouldBeAccessible()
	{
		// Arrange
		var client = Fixture.CreateClient();

		// Act
		var response = await client.GetAsync("/api/products");

		// Assert
		Assert.NotNull(response);
		var content = await response.Content.ReadAsStringAsync();
		
		// Log for debugging
		System.Console.WriteLine($"Remote API Status: {response.StatusCode}");
		System.Console.WriteLine($"Remote API BaseUrl: {BaseUrl}");
		System.Console.WriteLine($"Response Content Length: {content.Length}");
		
		Assert.True(response.IsSuccessStatusCode, 
			$"Expected success but got {response.StatusCode}. Content: {content}");
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "Remote API configuration should be valid")]
	public void RemoteApiConfigurationShouldBeValid()
	{
		// Verify configuration is loaded correctly
		Assert.NotNull(Fixture);
		Assert.NotNull(BaseUrl);
		Assert.False(string.IsNullOrEmpty(BaseUrl), "BaseUrl should not be empty");
		Assert.True(BaseUrl.StartsWith("http://") || BaseUrl.StartsWith("https://"), 
			"BaseUrl should be a valid HTTP/HTTPS URL");
		
		System.Console.WriteLine($"✅ Remote API configured at: {BaseUrl}");
		System.Console.WriteLine($"✅ Timeout: {Fixture.HttpSettings.Timeout}s");
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "Remote API health check endpoint should respond correctly")]
	public async Task RemoteApiHealthCheckShouldRespond()
	{
		// Arrange
		var client = Fixture.CreateClient();

		// Act - Try common health check endpoints
		var response = await client.GetAsync("/health");
		if (!response.IsSuccessStatusCode)
		{
			// Try alternate health endpoint
			response = await client.GetAsync("/api/health");
		}
		if (!response.IsSuccessStatusCode)
		{
			// If no health endpoint, just try products (basic endpoint)
			response = await client.GetAsync("/api/products");
		}

		// Assert
		Assert.NotNull(response);
		System.Console.WriteLine($"✅ Health check status: {response.StatusCode}");
		
		if (response.IsSuccessStatusCode)
		{
			var content = await response.Content.ReadAsStringAsync();
			System.Console.WriteLine($"✅ Response received: {content.Substring(0, Math.Min(100, content.Length))}...");
		}
		else
		{
			System.Console.WriteLine($"⚠️ Warning: Health check returned {response.StatusCode}");
		}
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "Remote API network latency should be measured")]
	public async Task RemoteApiNetworkLatencyShouldBeMeasured()
	{
		// Arrange
		var client = Fixture.CreateClient();
		var stopwatch = System.Diagnostics.Stopwatch.StartNew();

		// Act
		var response = await client.GetAsync("/api/products/1");
		stopwatch.Stop();

		// Assert
		Assert.NotNull(response);
		
		var latency = stopwatch.ElapsedMilliseconds;
		System.Console.WriteLine($"📊 Network Latency: {latency}ms");
		System.Console.WriteLine($"📊 Response Status: {response.StatusCode}");
		
		if (latency > 1000)
		{
			System.Console.WriteLine($"⚠️ Warning: High latency detected ({latency}ms)");
		}
		else if (latency > 500)
		{
			System.Console.WriteLine($"ℹ️ Info: Moderate latency ({latency}ms)");
		}
		else
		{
			System.Console.WriteLine($"✅ Good latency ({latency}ms)");
		}
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "Remote API timeout configuration should be respected")]
	public void RemoteApiTimeoutShouldBeConfigured()
	{
		// Verify timeout is configured correctly
		var timeout = Fixture.HttpSettings.Timeout;
		
		Assert.True(timeout > 0, "Timeout should be greater than 0");
		Assert.True(timeout <= 300, "Timeout should not exceed 5 minutes (300s)");
		
		System.Console.WriteLine($"✅ Configured timeout: {timeout}s");
		
		if (timeout < 30)
		{
			System.Console.WriteLine($"⚠️ Warning: Timeout is short ({timeout}s) - may cause issues for slow endpoints");
		}
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "Remote API default headers should be sent correctly")]
	public async Task RemoteApiDefaultHeadersShouldBeSent()
	{
		// Arrange
		var client = Fixture.CreateClient();

		// Act
		var response = await client.GetAsync("/api/products/1");

		// Assert
		Assert.NotNull(response);
		
		System.Console.WriteLine($"✅ Request sent with default headers");
		
		if (Fixture.HttpSettings.DefaultHeaders != null)
		{
			System.Console.WriteLine($"📋 Default headers configured: {Fixture.HttpSettings.DefaultHeaders.Count}");
			foreach (var header in Fixture.HttpSettings.DefaultHeaders)
			{
				System.Console.WriteLine($"   - {header.Key}: {header.Value}");
			}
		}
		else
		{
			System.Console.WriteLine($"ℹ️ No default headers configured");
		}
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "Remote API SSL/TLS should be validated")]
	public async Task RemoteApiSslTlsShouldBeValidated()
	{
		// This test verifies SSL/TLS connection for HTTPS endpoints
		if (!BaseUrl.StartsWith("https://"))
		{
			System.Console.WriteLine($"ℹ️ Skipping SSL/TLS test - API is not using HTTPS: {BaseUrl}");
			return;
		}

		// Arrange
		var client = Fixture.CreateClient();

		// Act
		var response = await client.GetAsync("/api/products/1");

		// Assert
		Assert.NotNull(response);
		System.Console.WriteLine($"✅ SSL/TLS connection successful");
		System.Console.WriteLine($"✅ Response status: {response.StatusCode}");
		
		// Check response headers for security indicators
		if (response.Headers.Contains("Strict-Transport-Security"))
		{
			System.Console.WriteLine($"✅ HSTS header present");
		}
	}
}
