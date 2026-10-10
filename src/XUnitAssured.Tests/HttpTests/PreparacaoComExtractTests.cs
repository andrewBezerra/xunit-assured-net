using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using XUnitAssured.Core.DSL;
using XUnitAssured.Http.Extensions;

namespace XUnitAssured.Tests.HttpTests;

/// <summary>
/// Preparar dados pela API e receber um valor de volta.
///
/// <para>
/// Criar o recurso pai e guardar o id é o código mais repetido de uma suíte de ponta a ponta, e
/// tirar um valor do ExecuteAsync levava vários passos. Numa suíte real, os helpers de preparação
/// ficaram fora da DSL em quase todo arquivo, 65 deles bloqueando com GetAwaiter().GetResult().
/// </para>
///
/// <para>
/// A regra que importa aqui: uma preparação que falhou não pode seguir em silêncio. Um id vazio
/// ou default levaria o teste seguinte a passar ou falhar pelo motivo errado.
/// </para>
/// </summary>
[Trait("Category", "Http")]
[Trait("Component", "Arrange")]
public class PreparacaoComExtractTests
{
	[Fact(DisplayName = "ExtractAsync returns the value from a successful response")]
	public async Task Extract_Returns_The_Value()
	{
		var servidor = ServidorDeMentira.Sempre(ServidorDeMentira.Responder(HttpStatusCode.Created, """{"id":"abc-123"}"""));

		var id = await ScenarioDsl.Given(new ProvedorDeMentira(servidor))
			.ApiResource("/orders").Post(new { item = "cadeira" })
			.ExtractAsync<string>("$.id");

		id.ShouldBe("abc-123");
		servidor.Recebidos.ShouldHaveSingleItem().Metodo.ShouldBe("POST");
	}

	[Fact(DisplayName = "ExtractAsync returns two values at once")]
	public async Task Extract_Returns_Two_Values()
	{
		var servidor = ServidorDeMentira.Sempre(ServidorDeMentira.Responder(HttpStatusCode.Created,
			"""{"id":"c1","token":"t-9","count":3}"""));

		var (id, token) = await ScenarioDsl.Given(new ProvedorDeMentira(servidor))
			.ApiResource("/invitations").Post(new { })
			.ExtractAsync<string, string>("$.id", "$.token");

		id.ShouldBe("c1");
		token.ShouldBe("t-9");
	}

	[Fact(DisplayName = "ExtractAsync fails on a non-2xx and shows the status and the body")]
	public async Task Extract_Fails_On_An_Error_Response()
	{
		var servidor = ServidorDeMentira.Sempre(ServidorDeMentira.Responder(HttpStatusCode.BadRequest,
			"""{"detail":"Name is required"}"""));

		var erro = await FalhaDe(() =>
			ScenarioDsl.Given(new ProvedorDeMentira(servidor))
				.ApiResource("/orders").Post(new { })
				.ExtractAsync<string>("$.id"));

		erro.Message.ShouldContain("400");
		erro.Message.ShouldContain("Name is required");
	}

	[Fact(DisplayName = "ExtractAsync fails when no response arrived, and says why")]
	public async Task Extract_Fails_Without_A_Response()
	{
		var servidor = new ServidorDeMentira(_ => throw new HttpRequestException("Connection refused"));

		var erro = await FalhaDe(() =>
			ScenarioDsl.Given(new ProvedorDeMentira(servidor))
				.ApiResource("/orders").Post(new { })
				.ExtractAsync<string>("$.id"));

		erro.Message.ShouldContain("no response");
		erro.Message.ShouldContain("Connection refused");
	}

	[Fact(DisplayName = "EnsureSuccessAsync passes on 204 and fails on 409")]
	public async Task Ensure_Success()
	{
		await ScenarioDsl.Given(new ProvedorDeMentira(ServidorDeMentira.Sempre(new HttpResponseMessage(HttpStatusCode.NoContent))))
			.ApiResource("/orders/1").Delete()
			.EnsureSuccessAsync();

		await FalhaDe(() =>
			ScenarioDsl.Given(new ProvedorDeMentira(ServidorDeMentira.Sempre(ServidorDeMentira.Responder(HttpStatusCode.Conflict, "{}"))))
				.ApiResource("/orders/1").Delete()
				.EnsureSuccessAsync());
	}

	/// <summary>
	/// A falha da preparação. O Should.ThrowAsync do Shouldly não captura um ShouldAssertException
	/// vindo de código assíncrono -- ele o repropaga como falha do próprio teste.
	/// </summary>
	private static async Task<ShouldAssertException> FalhaDe(Func<Task> preparacao)
	{
		try
		{
			await preparacao();
		}
		catch (ShouldAssertException erro)
		{
			return erro;
		}

		throw new InvalidOperationException("Expected the arrange to fail, but it succeeded.");
	}

	// O formato que a issue descreve: a preparação numa linha, e o teste segue com o valor.
	[Fact(DisplayName = "An arranged id feeds the request under test")]
	public async Task Arranged_Id_Feeds_The_Test()
	{
		var servidor = new ServidorDeMentira(pedido => pedido.Method == HttpMethod.Post
			? ServidorDeMentira.Responder(HttpStatusCode.Created, """{"id":"p-1"}""")
			: ServidorDeMentira.Responder(HttpStatusCode.OK, """{"id":"p-1","children":[]}"""));
		var api = new ProvedorDeMentira(servidor);

		var paiId = await ScenarioDsl.Given(api).ApiResource("/parents").Post(new { }).ExtractAsync<string>("$.id");

		var leitura = await ScenarioDsl.Given(api).ApiResource($"/parents/{paiId}").Get().ExecuteAsync();

		leitura.Then().AssertStatusCode(200).AssertJsonPathCount("$.children", 0);
		servidor.Recebidos[1].Caminho.ShouldBe("/parents/p-1");
	}
}
