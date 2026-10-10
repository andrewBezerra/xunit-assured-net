using System;

using Microsoft.Extensions.Logging;

using Shouldly;
using Xunit;

using XUnitAssured.Core.Logging;

namespace XUnitAssured.Tests.CoreTests;

/// <summary>
/// A captura de log.
///
/// <para>
/// Numa suíte real, três classes de teste escreveram cada uma o próprio ILoggerProvider para
/// afirmar o que o sistema registrou: que um 500 deixa um único registro de erro, que recusar
/// acesso deixa um evento de segurança, e que nenhuma linha carrega um dado sensível. Este último
/// é o motivo de a captura guardar também escopos e valores estruturados: um dado que fica fora
/// da mensagem ainda pode sair num escopo.
/// </para>
///
/// A captura é exercitada pelo LoggerFactory de verdade, como um host a usaria.
/// </summary>
[Trait("Category", "Core")]
[Trait("Component", "Logging")]
public class LogCaptureTests
{
	private static (LogCapture Logs, ILoggerFactory Fabrica) Montar()
	{
		var logs = new LogCapture();
		var fabrica = LoggerFactory.Create(b => b.SetMinimumLevel(LogLevel.Trace).AddProvider(logs));
		return (logs, fabrica);
	}

	[Fact(DisplayName = "An entry is captured with its category, level, event id, message and structured values")]
	public void Captures_The_Entry()
	{
		var (logs, fabrica) = Montar();
		var log = fabrica.CreateLogger("Pedidos.Checkout");

		log.LogWarning(new EventId(42, "Recusado"), "Pedido {PedidoId} recusado pelo {Motivo}", 7, "limite");

		var entrada = logs.Entries.ShouldHaveSingleItem();
		entrada.Category.ShouldBe("Pedidos.Checkout");
		entrada.Level.ShouldBe(LogLevel.Warning);
		entrada.EventId.Id.ShouldBe(42);
		entrada.Message.ShouldBe("Pedido 7 recusado pelo limite");
		entrada.Properties["PedidoId"].ShouldBe(7);
		entrada.Properties["Motivo"].ShouldBe("limite");
	}

	[Fact(DisplayName = "AssertLogged filters by category prefix and minimum level, and lists near misses")]
	public void Assert_Logged_Filters_And_Explains()
	{
		var (logs, fabrica) = Montar();
		fabrica.CreateLogger("App.Seguranca.Acesso").LogWarning(new EventId(9001), "Acesso a outra COLIH recusado");
		fabrica.CreateLogger("App.Pedidos").LogError("Falha ao gravar");

		logs.AssertLogged("App.Seguranca", LogLevel.Warning, e => e.EventId.Id == 9001);

		var erro = Should.Throw<ShouldAssertException>(() => logs.AssertLogged("App.Seguranca", LogLevel.Error));
		erro.Message.ShouldContain("Nothing was logged in App.Seguranca*");
	}

	// O caso que originou a captura: um 500 abria três alertas, e o teste exige um só.
	[Fact(DisplayName = "AssertLoggedOnce fails when the same thing is logged twice, and shows both")]
	public void Assert_Logged_Once_Counts()
	{
		var (logs, fabrica) = Montar();
		var log = fabrica.CreateLogger("App.Erros");
		log.LogError("Excecao nao tratada");

		logs.AssertLoggedOnce("App.Erros", LogLevel.Error);

		log.LogError("Excecao nao tratada");
		var erro = Should.Throw<ShouldAssertException>(() => logs.AssertLoggedOnce("App.Erros", LogLevel.Error));
		erro.Message.ShouldContain("found 2");
	}

	[Fact(DisplayName = "AssertNothingContains finds a value that rode in a scope, not in the message")]
	public void Nothing_Contains_Looks_At_Scopes()
	{
		var (logs, fabrica) = Montar();
		var log = fabrica.CreateLogger("App.Casos");

		using (log.BeginScope("Paciente {Nome}", "Maria da Silva"))
			log.LogInformation("Caso salvo");

		logs.Entries.ShouldHaveSingleItem().Message.ShouldBe("Caso salvo");
		Should.Throw<ShouldAssertException>(() => logs.AssertNothingContains("Maria da Silva"));
	}

	[Fact(DisplayName = "AssertNothingContains finds a value in the exception, not in the message")]
	public void Nothing_Contains_Looks_At_The_Exception()
	{
		var (logs, fabrica) = Montar();
		fabrica.CreateLogger("App.Banco").LogError(
			new InvalidOperationException("SELECT * FROM Pacientes WHERE Cpf = '123'"), "Falha no comando");

		logs.AssertNothingContains("Maria");
		Should.Throw<ShouldAssertException>(() => logs.AssertNothingContains("Cpf = '123'"));
	}

	[Fact(DisplayName = "Clear forgets what came before, so a test sees only its own action")]
	public void Clear_Forgets()
	{
		var (logs, fabrica) = Montar();
		var log = fabrica.CreateLogger("App");
		log.LogError("do arranjo");

		logs.Clear();
		log.LogInformation("da acao");

		logs.Entries.ShouldHaveSingleItem().Message.ShouldBe("da acao");
		logs.AssertNotLogged(e => e.Level >= LogLevel.Error);
	}
}
