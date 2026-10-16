#if NET10_0_OR_GREATER
using System;
using System.IO;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Shouldly;
using Xunit;

using XUnitAssured.Mcp.Tools;

namespace XUnitAssured.Tests.CamundaTests;

/// <summary>
/// A ferramenta <c>generate_camunda_test</c> do servidor MCP: do BPMN ao esqueleto do teste.
///
/// <para>
/// Além do que ela escreve, o teste compila o resultado contra o XUnitAssured.Camunda de verdade:
/// se um verbo mudar de nome, a ferramenta quebra aqui, e não na mão de quem a usa.
/// </para>
/// </summary>
[Trait("Category", "Camunda")]
[Trait("Component", "Mcp")]
public class GeradorDeTesteMcpTests
{
	[Fact(DisplayName = "MCP: the order gets a mock per job type, the child deployed with it, and the boundary paths as alternatives")]
	public void Pedido()
	{
		var codigo = Gerar("xa-pedido.bpmn");

		codigo.ShouldContain(".Deploy(\"xa-pedido\", \"xa-envio\")");
		codigo.ShouldContain(".MockJobWorker(\"xa-cobrar\").ThenComplete(");
		codigo.ShouldContain("// .MockJobWorker(\"xa-cobrar\").ThenThrowBpmnError(\"PAGAMENTO_RECUSADO\") // takes 'pagamento-recusado' instead");
		codigo.ShouldContain("// .MockChildProcess(\"chamar-envio\").ThenThrowBpmnError(\"ENVIO_FALHOU\") // takes 'envio-falhou' instead");
		codigo.ShouldContain("// .MockChildProcess(\"chamar-envio\").ThenComplete(");
		codigo.ShouldContain(".StartInstance(\"xa-pedido\"");
		codigo.ShouldContain(".AssertCompletedElements(\"concluido\"); // or end at: recusado, nao-enviado");
		codigo.ShouldNotContain("Warning:");
		Compila(codigo);
	}

	[Fact(DisplayName = "MCP: user tasks and timers are mocked; after an event-based gateway the first path is taken and the others are alternatives")]
	public void Aprovacao()
	{
		var codigo = Gerar("xa-aprovacao.bpmn");

		codigo.ShouldContain(".SkipTimer(\"esperar-analise\") // Esperar 1 hora");
		codigo.ShouldContain(".MockUserTask(\"aprovar\").ThenComplete(");
		codigo.ShouldContain("// .SkipTimer(\"prazo-aprovacao\")");
		codigo.ShouldContain("// .SkipTimer(\"sem-confirmacao\")");
		// O gateway de evento: o primeiro caminho (a mensagem) entra no teste, o timer fica como alternativa
		codigo.ShouldContain("\t.MockMessage(\"xa-confirmacao\").ThenCorrelate(");
		Compila(codigo);
	}

	[Fact(DisplayName = "MCP: messages are mocked by the message name, and signals by the signal name")]
	public void Entrega()
	{
		var codigo = Gerar("xa-entrega.bpmn");

		codigo.ShouldContain(".MockMessage(\"xa-pagamento-confirmado\").ThenCorrelate(");
		codigo.ShouldContain(".MockSignal(\"xa-estoque-liberado\").ThenTrigger()");
		codigo.ShouldContain(".MockUserTask(\"conferir\")");
		codigo.ShouldContain("// .MockMessage(\"xa-cliente-cancelou\")");
		Compila(codigo);
	}

	[Fact(DisplayName = "MCP: a call activity with bindingType latest is flagged in the generated test")]
	public void Latest_Is_Flagged()
	{
		var xml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "CamundaTests", "bpmn", "xa-pedido.bpmn"))
			.Replace("bindingType=\"deployment\"", "");

		CamundaTestGeneratorTools.GenerateCamundaTest(xml)
			.ShouldContain("// Warning: Call activity 'chamar-envio' calls 'xa-envio' with bindingType \"latest\"");
	}

	[Fact(DisplayName = "MCP: a file that is not XML, or a process that is not in it, gets a comment instead of code")]
	public void Bad_Input()
	{
		CamundaTestGeneratorTools.GenerateCamundaTest("not xml").ShouldStartWith("// The BPMN could not be read as XML");
		CamundaTestGeneratorTools.GenerateCamundaTest(File.ReadAllText(Caminho("xa-pedido.bpmn")), "outro")
			.ShouldContain("// The BPMN has no process 'outro'. Processes in it: xa-pedido.");
	}

	[Fact(DisplayName = "MCP: what the BPMN brings stays inside literals and comments: a hostile diagram cannot inject code")]
	public void Hostile_Bpmn_Cannot_Inject_Code()
	{
		// Aspas, barra e quebras de linha (inclusive a U+2028, que o C# também trata como fim de linha).
		var quebra = ((char)0x2028).ToString();
		var xml = $$"""
			<?xml version="1.0" encoding="UTF-8"?>
			<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:zeebe="http://camunda.org/schema/zeebe/1.0" id="d">
			  <bpmn:process id="x" isExecutable="true">
			    <bpmn:startEvent id="inicio" />
			    <bpmn:serviceTask id="t" name="ok&#10;System.Environment.Exit(2);{{quebra}}System.Environment.Exit(3);">
			      <bpmn:extensionElements><zeebe:taskDefinition type="a&quot;); System.Environment.Exit(1); //\ */ System.Environment.Exit(4); /*" /></bpmn:extensionElements>
			    </bpmn:serviceTask>
			    <bpmn:userTask id="u&#10;System.Environment.Exit(5);" />
			    <bpmn:endEvent id="fim" />
			    <bpmn:sequenceFlow id="f1" sourceRef="inicio" targetRef="t" />
			    <bpmn:sequenceFlow id="f2" sourceRef="t" targetRef="fim" />
			  </bpmn:process>
			</bpmn:definitions>
			""";

		var codigo = CamundaTestGeneratorTools.GenerateCamundaTest(xml);

		// Fora de literais e comentários, nenhum identificador do BPMN pode virar código.
		var identificadores = CSharpSyntaxTree.ParseText($"class C {{ {codigo} }}").GetRoot().DescendantNodes()
			.OfType<Microsoft.CodeAnalysis.CSharp.Syntax.IdentifierNameSyntax>()
			.Select(i => i.Identifier.Text)
			.ToList();
		identificadores.ShouldNotContain("Exit", codigo);
		identificadores.ShouldNotContain("Environment", codigo);
		Compila(codigo);

		// O processo pedido e os ids da mensagem de "não encontrado" também ficam numa linha só de comentário.
		var quebraDeLinha = ((char)10).ToString();
		var naoEncontrado = CamundaTestGeneratorTools.GenerateCamundaTest(xml, "y" + quebraDeLinha + "System.Environment.Exit(6);");
		naoEncontrado.ShouldStartWith("//");
		naoEncontrado.ShouldNotContain(quebraDeLinha);
	}

	private static string Gerar(string arquivo) =>
		CamundaTestGeneratorTools.GenerateCamundaTest(File.ReadAllText(Caminho(arquivo)));

	private static string Caminho(string arquivo) => Path.Combine(AppContext.BaseDirectory, "CamundaTests", "bpmn", arquivo);

	// O método gerado, dentro de uma classe como a do usuário, compilado com as referências deste processo.
	private static void Compila(string metodo)
	{
		var fonte = $$"""
			using System.Threading.Tasks;
			using Xunit;
			using XUnitAssured.Camunda.Extensions;
			using static XUnitAssured.Core.DSL.ScenarioDsl;

			public class TesteGerado
			{
			{{metodo}}
			}
			""";

		var referencias = AppDomain.CurrentDomain.GetAssemblies()
			.Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
			.Select(a => MetadataReference.CreateFromFile(a.Location));

		var compilacao = CSharpCompilation.Create(
			"TesteGerado",
			new[] { CSharpSyntaxTree.ParseText(fonte) },
			referencias,
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

		var erros = compilacao.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
		erros.ShouldBeEmpty("The generated test does not compile:" + Environment.NewLine + fonte);
	}
}
#endif
