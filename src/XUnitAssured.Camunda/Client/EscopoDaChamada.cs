using System;
using System.Net.Http;
using System.Threading;

namespace XUnitAssured.Camunda.Client;

/// <summary>
/// A operação em andamento, vista pelos handlers embaixo do SDK: a quem avisar cada pedido HTTP, o último caminho da API
/// chamado (para a recusa dizer qual foi) e se o pedido em voo é o do token (para o prazo vencido dizer quem não
/// respondeu).
/// </summary>
/// <remarks>
/// O cliente é um só para chamadas concorrentes, e é o <see cref="AsyncLocal{T}"/> que leva cada pedido à operação que o
/// fez: o SDK faz a chamada HTTP no mesmo fluxo assíncrono de quem pediu.
/// </remarks>
internal sealed class EscopoDaChamada
{
	private static readonly AsyncLocal<EscopoDaChamada?> Corrente = new();

	// As marcas que o handler da autenticação põe no pedido, para o observador saber o que ele é.
	internal static readonly HttpRequestOptionsKey<bool> PedidoDeToken = new("xunitassured.camunda.token");
	internal static readonly HttpRequestOptionsKey<bool> ComTokenNovo = new("xunitassured.camunda.token-novo");

	public EscopoDaChamada(Action<CamundaCall>? observe) => Observe = observe;

	/// <summary>A operação deste fluxo; nula fora de uma operação do cliente (a DSL, que chama o SDK direto).</summary>
	public static EscopoDaChamada? Atual
	{
		get => Corrente.Value;
		set => Corrente.Value = value;
	}

	public Action<CamundaCall>? Observe { get; }

	/// <summary>O caminho do último pedido à API, a partir do endereço REST.</summary>
	public string? UltimoCaminho { get; set; }

	/// <summary>O endereço do último pedido à API, sem a query.</summary>
	public string? UltimoEndereco { get; set; }

	/// <summary>Se o pedido em voo é o do token.</summary>
	public bool PedindoToken { get; set; }
}
