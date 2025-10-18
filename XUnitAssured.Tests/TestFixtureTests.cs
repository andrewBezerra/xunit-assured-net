using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

using Xunit;

using XUnitAssured.Base;

namespace XUnitAssured.Tests;

[Trait("Unit Test", "TestFixture")]
public class TestFixtureTests
{
	// Classe wrapper para testar métodos protegidos sem herdar de TestBedFixture
	private class TestFixtureWrapper
	{
		private readonly TestFixture _fixture;

		public TestFixtureWrapper()
		{
			// Usa reflexão para criar instância sem chamar o construtor da classe base
			_fixture = (TestFixture)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(TestFixture));
		}

		public void CallAddServices(IServiceCollection services, IConfiguration? configuration)
		{
			var method = typeof(TestFixture).GetMethod("AddServices", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
			method!.Invoke(_fixture, new object?[] { services, configuration });
		}

		public IEnumerable<(string Filename, bool IsOptional)> CallGetTestAppSettings()
		{
			var method = typeof(TestFixture).GetMethod("GetTestAppSettings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
			var result = method!.Invoke(_fixture, null);
			
			// Iterar sobre o IEnumerable<TestAppSettings> usando reflexão
			var enumerable = result as System.Collections.IEnumerable;
			foreach (var item in enumerable!)
			{
				var type = item.GetType();
				string filename = (string)type.GetProperty("Filename")!.GetValue(item)!;
				bool isOptional = (bool)type.GetProperty("IsOptional")!.GetValue(item)!;
				yield return (filename, isOptional);
			}
		}

		public ValueTask CallDisposeAsyncCore()
		{
			var method = typeof(TestFixture).GetMethod("DisposeAsyncCore", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
			return (ValueTask)method!.Invoke(_fixture, null)!;
		}
	}

	private sealed class NoopChangeToken : IChangeToken
	{
		public static readonly NoopChangeToken Instance = new();
		public bool HasChanged => false;
		public bool ActiveChangeCallbacks => false;
		public IDisposable RegisterChangeCallback(Action<object?> callback, object? state) => new NoopDisposable();
		private sealed class NoopDisposable : IDisposable { public void Dispose() { } }
	}

	private class ThrowingConfiguration : IConfiguration
	{
		public string? this[string key] { get => null; set { } }
		public IEnumerable<IConfigurationSection> GetChildren() => Enumerable.Empty<IConfigurationSection>();
		public IChangeToken GetReloadToken() => NoopChangeToken.Instance;
		public IConfigurationSection GetSection(string key) => throw new Exception("boom");
	}

	[Fact(DisplayName = "AddServices deve registrar IOptions<BaseSettings> com valores vinculados")]
	public void AddServices_ShouldRegisterOptionsWithBoundValues()
	{
		// Arrange
		var services = new ServiceCollection();
		var inMemory = new Dictionary<string, string?>
		{
			{"TestSettings:BaseUrl", "https://api.example.com"},
			{"TestSettings:OpenApiDocument", "openapi.json"}
		};
		IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();
		var wrapper = new TestFixtureWrapper();

		// Act
		wrapper.CallAddServices(services, config);
		using var provider = services.BuildServiceProvider();
		var options = provider.GetService<IOptions<BaseSettings>>();

		// Assert
		Assert.NotNull(options);
		Assert.Equal("https://api.example.com", options!.Value.BaseUrl);
		Assert.Equal("openapi.json", options.Value.OpenApiDocument);
	}

	[Fact(DisplayName = "AddServices deve lançar InvalidTestSettingsException quando configuração inválida")]
	public void AddServices_ShouldThrowInvalidTestSettingsException_WhenConfigurationInvalid()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = new ThrowingConfiguration();
		var wrapper = new TestFixtureWrapper();

		// Act / Assert
		// Como usamos reflexão, a exceção vem encapsulada em TargetInvocationException
		var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() => 
			wrapper.CallAddServices(services, config));
		
		// Verificar que a exceção interna é InvalidTestSettingsException
		Assert.NotNull(ex.InnerException);
		Assert.IsType<InvalidTestSettingsException>(ex.InnerException);
		Assert.Contains("A sessão TestSettings", ex.InnerException!.Message, StringComparison.OrdinalIgnoreCase);
	}

	[Fact(DisplayName = "GetTestAppSettings deve retornar testsettings.json obrigatório")]
	public void GetTestAppSettings_ShouldReturnRequiredTestSettingsFile()
	{
		// Arrange
		var wrapper = new TestFixtureWrapper();

		// Act
		var list = wrapper.CallGetTestAppSettings().ToList();

		// Assert
		Assert.Single(list);
		var item = list[0];
		Assert.Equal("testsettings.json", item.Filename);
		Assert.False(item.IsOptional);
	}

	[Fact(DisplayName = "DisposeAsyncCore deve completar sem erro")]
	public async Task DisposeAsyncCore_ShouldCompleteSuccessfully()
	{
		// Arrange
		var wrapper = new TestFixtureWrapper();

		// Act
		var vt = wrapper.CallDisposeAsyncCore();
		await vt;

		// Assert
		Assert.True(vt.IsCompletedSuccessfully);
	}
}
