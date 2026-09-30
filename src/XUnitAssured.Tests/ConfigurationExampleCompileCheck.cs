using System;
using System.Net.Http;

using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.Configuration;
using XUnitAssured.Http.Configuration;

namespace XUnitAssured.Tests;

/// <summary>
/// The HTTP fixture shown in the README's Configuration section, kept here so it
/// is compiled against the real API on every build. Not a test: its only job is
/// to fail the build if the snippet stops matching the library.
/// </summary>
internal sealed class ApiFixture : IHttpClientProvider, IHttpClientAuthProvider
{
	private readonly HttpSettings _http = TestSettings.Load().GetHttpSettings()!;
	public HttpClient CreateClient() => new() { BaseAddress = new Uri(_http.BaseUrl!) };
	public HttpAuthConfig? GetAuthenticationConfig() => _http.Authentication;
}
