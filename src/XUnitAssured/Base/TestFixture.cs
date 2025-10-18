using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Xunit.Microsoft.DependencyInjection;
using Xunit.Microsoft.DependencyInjection.Abstracts;

namespace XUnitAssured.Base;

public class TestFixture : TestBedFixture
{
	public const string AssemblyName = "XUnitAssured.Tests";
	protected override void AddServices(IServiceCollection services, IConfiguration? configuration)
	{
		try
		{
			// Forçar acesso à seção para capturar exceções de configuração inválida
			var section = configuration!.GetSection("TestSettings");
			// Tenta enumerar os filhos para forçar validação
			_ = section.GetChildren();
			
			services.Configure<BaseSettings>(config => section.Bind(config));
		}
		catch
		{
			throw new InvalidTestSettingsException("A sessão TestSettings no arquivo testsettings.json é inválida. Por favor verifique.");
		}
	}

	protected override ValueTask DisposeAsyncCore()
		=> new();

	protected override IEnumerable<TestAppSettings> GetTestAppSettings()
	{
		yield return new() { Filename = "testsettings.json", IsOptional = false };
	}
}
