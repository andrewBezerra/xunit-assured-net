using System;
using System.IO;

using XUnitAssured.Core.Configuration;
using XUnitAssured.Http.Configuration;

namespace XUnitAssured.Tests.CoreTests;

/// <summary>
/// Onde os arquivos de configuração são procurados, e a sobreposição por ambiente do
/// httpsettings.json (#146).
///
/// <para>
/// Cada pacote tinha a sua cópia da busca, e nenhuma era testada: os testes apontavam o arquivo
/// por TESTSETTINGS_PATH. Agora há uma só, e ela recebe as pastas como argumento, então estes
/// testes não mexem no diretório atual nem em variável de ambiente do processo.
/// </para>
/// </summary>
[Trait("Category", "Core")]
[Trait("Component", "Configuration")]
public sealed class BuscaDoArquivoDeConfiguracaoTests : IDisposable
{
	private const string Nome = "testsettings.json";

	// raiz/a/b/c/d/e — fundo o bastante para passar dos três níveis acima da pasta atual
	private readonly string _raiz = Path.Combine(Path.GetTempPath(), "busca-" + Guid.NewGuid().ToString("N"));
	private readonly string _fundo;
	private readonly string _outra;

	public BuscaDoArquivoDeConfiguracaoTests()
	{
		_fundo = Directory.CreateDirectory(Path.Combine(_raiz, "a", "b", "c", "d", "e")).FullName;
		_outra = Directory.CreateDirectory(Path.Combine(_raiz, "outra")).FullName;
	}

	public void Dispose() => Directory.Delete(_raiz, recursive: true);

	[Theory(DisplayName = "The file is found in the working directory and up to three parents")]
	[InlineData("a/b/c/d/e")]
	[InlineData("a/b/c/d")]
	[InlineData("a/b/c")]
	[InlineData("a/b")]
	public void Found_Up_To_Three_Parents(string ondeEsta)
	{
		var arquivo = Escrever(ondeEsta);

		ArquivoDeConfiguracao.Localizar(Nome, _fundo, _outra).ShouldBe(arquivo);
	}

	[Fact(DisplayName = "Four parents up from the working directory is too far")]
	public void Not_Found_Four_Parents_Up()
	{
		Escrever("a");

		ArquivoDeConfiguracao.Localizar(Nome, _fundo, _outra).ShouldBeNull();
	}

	// É o que faz o arquivo ao lado do projeto valer quando a execução começa em outra pasta.
	[Fact(DisplayName = "Walking up from the output directory finds the file at any depth")]
	public void Found_Walking_Up_From_The_Output_Directory()
	{
		var arquivo = Escrever("a");

		ArquivoDeConfiguracao.Localizar(Nome, _outra, _fundo).ShouldBe(arquivo);
	}

	[Fact(DisplayName = "The working directory comes before the output directory")]
	public void Working_Directory_First()
	{
		var daPastaAtual = Escrever("outra");
		Escrever("a/b/c/d/e");

		ArquivoDeConfiguracao.Localizar(Nome, _outra, _fundo).ShouldBe(daPastaAtual);
	}

	[Fact(DisplayName = "A variable that is set is expanded; one that is not stays as written")]
	public void Expands_Only_Variables_That_Are_Set()
	{
		var nome = "XUA_BUSCA_" + Guid.NewGuid().ToString("N");
		Environment.SetEnvironmentVariable(nome, "valor");
		try
		{
			ArquivoDeConfiguracao.ExpandirVariaveis($$"""{"a":"${ENV:{{nome}}}","b":"${env:{{nome}}}","c":"${ENV:{{nome}}_NAO}"}""")
				.ShouldBe($$"""{"a":"valor","b":"valor","c":"${ENV:{{nome}}_NAO}"}""");
		}
		finally
		{
			Environment.SetEnvironmentVariable(nome, null);
		}
	}

	// ---------- httpsettings.json por ambiente ----------

	private const string Base = """
		{
		  "http": {
		    "baseUrl": "https://base.teste",
		    "timeout": 60,
		    "defaultHeaders": { "X-Base": "1" },
		    "authentication": { "type": "Bearer", "bearer": { "token": "tk-base" } }
		  },
		  "environments": {
		    "staging": { "baseUrl": "https://staging.teste" },
		    "lento": { "timeout": 120, "defaultHeaders": { "X-Ambiente": "lento" } },
		    "anonimo": { "authentication": { "type": "None" } }
		  }
		}
		""";

	[Fact(DisplayName = "An environment section overrides only what it writes")]
	public void Environment_Section_Keeps_What_It_Leaves_Out()
	{
		var arquivo = EscreverHttp(Base);

		var staging = HttpSettingsLoader.LoadFromFile(arquivo, "staging");
		staging.BaseUrl.ShouldBe("https://staging.teste");
		staging.Timeout.ShouldBe(60);
		staging.DefaultHeaders!["X-Base"].ShouldBe("1");
		staging.Authentication!.Type.ShouldBe(AuthenticationType.Bearer);
		staging.Authentication.Bearer!.Token.ShouldBe("tk-base");

		var lento = HttpSettingsLoader.LoadFromFile(arquivo, "lento");
		lento.Timeout.ShouldBe(120);
		lento.DefaultHeaders!["X-Base"].ShouldBe("1");
		lento.DefaultHeaders["X-Ambiente"].ShouldBe("lento");
		lento.Authentication!.Bearer!.Token.ShouldBe("tk-base");

		HttpSettingsLoader.LoadFromFile(arquivo, "anonimo").Authentication!.Type.ShouldBe(AuthenticationType.None);
	}

	[Fact(DisplayName = "An environment file overrides only what it writes")]
	public void Environment_File_Keeps_What_It_Leaves_Out()
	{
		var arquivo = EscreverHttp(Base);
		File.WriteAllText(Path.Combine(_raiz, "httpsettings.local.json"), """{ "http": { "baseUrl": "http://localhost:5000" } }""");
		File.WriteAllText(Path.Combine(_raiz, "httpsettings.lento.json"), """{ "http": { "timeout": 90 } }""");
		File.WriteAllText(Path.Combine(_raiz, "httpsettings.vazio.json"), "{}");

		var local = HttpSettingsLoader.LoadFromFile(arquivo, "local");
		local.BaseUrl.ShouldBe("http://localhost:5000");
		local.Timeout.ShouldBe(60);
		local.Authentication!.Bearer!.Token.ShouldBe("tk-base");

		var lento = HttpSettingsLoader.LoadFromFile(arquivo, "lento");
		lento.Timeout.ShouldBe(90);
		lento.BaseUrl.ShouldBe("https://base.teste");

		var vazio = HttpSettingsLoader.LoadFromFile(arquivo, "vazio");
		vazio.BaseUrl.ShouldBe("https://base.teste");
		vazio.Authentication!.Bearer!.Token.ShouldBe("tk-base");
	}

	// ---------- Apoio ----------

	private string Escrever(string pasta)
	{
		var caminho = Path.Combine(_raiz, pasta, Nome);
		File.WriteAllText(caminho, "{}");
		return Path.GetFullPath(caminho);
	}

	private string EscreverHttp(string json)
	{
		var caminho = Path.Combine(_raiz, "httpsettings.json");
		File.WriteAllText(caminho, json);
		return caminho;
	}
}
