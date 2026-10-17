using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

using Microsoft.Extensions.Configuration.UserSecrets;

using XUnitAssured.Core.Configuration;

namespace XUnitAssured.Tests.CoreTests;

/// <summary>
/// <c>${SECRET:Key}</c> in a settings file: the user secret of the test project, then the
/// environment variable with the same key, then left as written (#212).
/// </summary>
public class SegredosDoUsuarioTests
{
	private static readonly Dictionary<string, string> Segredos = new()
	{
		["Api:Token"] = "tk-local",
		["Senha"] = "a\"b\\c",
	};

	private static string? Segredo(string chave) => Segredos.GetValueOrDefault(chave);

	[Fact(DisplayName = "A user secret replaces ${SECRET:Key}, with nested keys written with ':'")]
	public void A_User_Secret_Is_Expanded()
	{
		ArquivoDeConfiguracao.ExpandirVariaveis("""{"token":"${SECRET:Api:Token}"}""", Segredo)
			.ShouldBe("""{"token":"tk-local"}""");
	}

	[Fact(DisplayName = "Without a user secret, the environment variable with the key's ':' as '__' is used")]
	public void Falls_Back_To_The_Environment_Variable()
	{
		var chave = "XUA_SEGREDO_" + Guid.NewGuid().ToString("N");
		Environment.SetEnvironmentVariable(chave + "__Token", "tk-ci");
		try
		{
			ArquivoDeConfiguracao.ExpandirVariaveis($$"""{"token":"${SECRET:{{chave}}:Token}"}""", Segredo)
				.ShouldBe("""{"token":"tk-ci"}""");
		}
		finally
		{
			Environment.SetEnvironmentVariable(chave + "__Token", null);
		}
	}

	[Fact(DisplayName = "A secret found nowhere stays as written, so the setting shows what is missing")]
	public void A_Missing_Secret_Stays_As_Written()
	{
		ArquivoDeConfiguracao.ExpandirVariaveis("""{"token":"${SECRET:Nao:Existe}"}""", Segredo)
			.ShouldBe("""{"token":"${SECRET:Nao:Existe}"}""");
	}

	[Fact(DisplayName = "A secret with quotes or backslashes still makes valid JSON, and reads back as it was")]
	public void A_Secret_Is_Escaped_For_Json()
	{
		var json = ArquivoDeConfiguracao.ExpandirVariaveis("""{"senha":"${SECRET:Senha}"}""", Segredo);

		JsonDocument.Parse(json).RootElement.GetProperty("senha").GetString().ShouldBe("a\"b\\c");
	}

	[Fact(DisplayName = "The id declared in the file wins over the loaded assemblies")]
	public void The_Declared_Id_Wins()
	{
		SegredosDoUsuario.EscolherId("declarado", ["a", "b"]).ShouldBe("declarado");
	}

	[Fact(DisplayName = "With one loaded assembly carrying a UserSecretsId, that id is used")]
	public void A_Single_Loaded_Id_Is_Used()
	{
		SegredosDoUsuario.EscolherId(null, ["a", "a"]).ShouldBe("a");
	}

	[Fact(DisplayName = "With no UserSecretsId anywhere, there are no user secrets")]
	public void No_Id_Means_No_Secrets()
	{
		SegredosDoUsuario.EscolherId(null, []).ShouldBeNull();
	}

	[Fact(DisplayName = "With two UserSecretsIds and none declared, loading fails naming both")]
	public void Two_Ids_And_None_Declared_Fail()
	{
		var erro = Should.Throw<InvalidOperationException>(() => SegredosDoUsuario.EscolherId(null, ["id-teste", "id-api"]));

		erro.Message.ShouldContain("id-teste");
		erro.Message.ShouldContain("id-api");
		erro.Message.ShouldContain("userSecretsId");
	}

	[Fact(DisplayName = "A real secrets.json, chosen by the file's userSecretsId, is read")]
	public void Reads_A_Real_Secrets_File()
	{
		var id = "xua-teste-" + Guid.NewGuid().ToString("N");
		var caminho = PathHelper.GetSecretsPathFromSecretsId(id);
		Directory.CreateDirectory(Path.GetDirectoryName(caminho)!);
		File.WriteAllText(caminho, """{ "Api": { "Token": "tk-do-arquivo" } }""");
		try
		{
			ArquivoDeConfiguracao.ExpandirVariaveis($$"""{"userSecretsId":"{{id}}","token":"${SECRET:Api:Token}"}""")
				.ShouldBe($$"""{"userSecretsId":"{{id}}","token":"tk-do-arquivo"}""");
		}
		finally
		{
			Directory.Delete(Path.GetDirectoryName(caminho)!, recursive: true);
		}
	}

	[Fact(DisplayName = "A file with no ${SECRET:...} is not touched, even with comments")]
	public void A_File_Without_Secrets_Is_Not_Parsed()
	{
		const string json = """
			{
			  // comentário
			  "token": "${ENV:XUA_NAO_EXISTE}",
			}
			""";

		ArquivoDeConfiguracao.ExpandirVariaveis(json).ShouldBe(json);
	}
}
