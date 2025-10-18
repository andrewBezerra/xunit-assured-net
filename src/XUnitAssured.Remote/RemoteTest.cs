using System;
using System.Net.Http;

using Flurl.Http;

using Microsoft.Extensions.Options;

using Xunit.Abstractions;
using Xunit.Microsoft.DependencyInjection.Abstracts;

using XUnitAssured.Base;

namespace XUnitAssured.Remote;

/// <summary>
/// Tipo de Teste que faz requisições HTTP para um serviço remoto usando FLURL.
/// </summary>
/// <typeparam name="TFixture">Classe que implementa TestBedFixture</typeparam>
public abstract class RemoteTest<TFixture> : TestBed<TFixture>
	where TFixture : TestFixture
{
	protected BaseSettings Settings => _fixture.GetService<IOptions<BaseSettings>>(_testOutputHelper)!.Value;
	protected FlurlClient WebClient { get; }

	protected readonly ITestOutputHelper Output;
	protected RemoteTest(ITestOutputHelper output, TFixture fixture) : base(output, fixture)
	{
		var http = new HttpClient { BaseAddress = new Uri(Settings.BaseUrl) };
		WebClient = new FlurlClient(http);
		Output = output;
	}
}


