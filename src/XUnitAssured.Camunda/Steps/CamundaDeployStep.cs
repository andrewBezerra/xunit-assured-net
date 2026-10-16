using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using XUnitAssured.Camunda.Gateway;
using XUnitAssured.Camunda.Results;
using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.Results;

namespace XUnitAssured.Camunda.Steps;

/// <summary>
/// Implanta arquivos BPMN (e DMN, formulários) num deploy só.
/// </summary>
/// <remarks>
/// <para>
/// <b>Um deploy só, de propósito.</b> Uma <i>call activity</i> com <c>bindingType="deployment"</c>
/// chama o filho implantado junto com o pai, e o Camunda recusa o deploy de um pai sem o filho
/// ao lado. É também o que mantém o teste longe da versão de outra pessoa num cluster
/// compartilhado. Implantar o mesmo conteúdo de novo não cria versão: o cluster devolve a que já
/// existe, então implantar em todo teste custa uma ida e volta e nada mais.
/// </para>
/// <para>
/// Cada argumento é um caminho ou o id de um processo. Com o id, o arquivo é o <c>.bpmn</c> da
/// pasta de saída que declara aquele processo: o teste continua implantando o diagrama do
/// repositório, e não o que já está no cluster.
/// </para>
/// <para>
/// Os processos implantados ficam no contexto, e é por eles que o <c>StartInstance</c> inicia a
/// instância: pela chave desta definição, e não pela última versão do <c>processId</c>.
/// </para>
/// </remarks>
public class CamundaDeployStep : ITestStep
{
	internal const string ChaveDasDefinicoes = "_CamundaDefinicoes";
	internal const string ChaveDaFalha = "_CamundaFalhaDoDeploy";
	internal const string ChaveDosAvisos = "_CamundaAvisos";

	/// <inheritdoc />
	public string? Name { get; internal set; }

	/// <inheritdoc />
	public string StepType => "Camunda";

	/// <inheritdoc />
	public ITestStepResult? Result { get; private set; }

	/// <inheritdoc />
	public bool IsExecuted => Result != null;

	/// <inheritdoc />
	public bool IsValid { get; private set; }

	/// <summary>
	/// Os arquivos, ou os ids dos processos. Um caminho relativo é procurado na pasta de saída do
	/// teste e depois na pasta atual; um id, nos <c>.bpmn</c> da pasta de saída.
	/// </summary>
	public IReadOnlyList<string> Resources { get; init; } = Array.Empty<string>();

	/// <inheritdoc />
	public async Task<ITestStepResult> ExecuteAsync(ITestContext context, CancellationToken cancellationToken = default)
	{
		var cronometro = Stopwatch.StartNew();
		var diagnostico = new Dictionary<string, object?> { ["Resources"] = string.Join(", ", Resources) };

		try
		{
			// Dois ids do mesmo arquivo viram um arquivo só: o Camunda recusa o mesmo recurso duas vezes.
			var recursos = Resources
				.Select(Arquivo)
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.Select(Ler)
				.ToList();
			var gateway = Gateways.De(context, CamundaSettings.De(context));

			var processos = await gateway.DeployAsync(recursos, cancellationToken).ConfigureAwait(false);

			var definicoes = Definicoes(context);
			foreach (var processo in processos)
				definicoes[processo.ProcessId] = processo;

			var avisos = Avisos(context);
			foreach (var aviso in recursos.SelectMany(AvisosDe).Where(a => !avisos.Contains(a)))
				avisos.Add(aviso);

			Result = CamundaStepResult.CreateDeploySuccess(processos, cronometro.Elapsed, avisos.ToArray());
			IsValid = true;
			return Result;
		}
		catch (Exception ex)
		{
			var erro = ex is CamundaRecusouException recusa
				? $"Camunda rejected the deployment of {string.Join(", ", Resources.Select(Path.GetFileName))}: {recusa.Codigo}: {recusa.Message}"
				: $"Could not deploy {string.Join(", ", Resources.Select(Path.GetFileName))}: {ex.Message}";

			context.SetProperty(ChaveDaFalha, erro);
			Result = CamundaStepResult.CreateFailure(erro, ex, diagnostico, cronometro.Elapsed);
			IsValid = false;
			return Result;
		}
	}

	/// <inheritdoc />
	public void Validate(Action<ITestStepResult> validation)
	{
		if (Result == null)
			throw new InvalidOperationException("Step has not been executed. Execute the chain before validating.");

		validation(Result);
		IsValid = true;
	}

	internal static Dictionary<string, DeployedProcess> Definicoes(ITestContext context)
	{
		var definicoes = context.GetProperty<Dictionary<string, DeployedProcess>>(ChaveDasDefinicoes);
		if (definicoes is null)
		{
			definicoes = new Dictionary<string, DeployedProcess>(StringComparer.Ordinal);
			context.SetProperty(ChaveDasDefinicoes, definicoes);
		}

		return definicoes;
	}

	internal static List<string> Avisos(ITestContext context)
	{
		var avisos = context.GetProperty<List<string>>(ChaveDosAvisos);
		if (avisos is null)
		{
			avisos = new List<string>();
			context.SetProperty(ChaveDosAvisos, avisos);
		}

		return avisos;
	}

	// O bindingType padrão é latest: a call activity chama a última versão do filho no momento em que é
	// ativada, e num cluster compartilhado ela pode ser a de outra pessoa.
	internal static IEnumerable<string> AvisosDe(RecursoBpmn recurso)
	{
		if (!recurso.Nome.EndsWith(".bpmn", StringComparison.OrdinalIgnoreCase))
			return Array.Empty<string>();

		try
		{
			return Mocks.ModeloBpmn.Ler(System.Text.Encoding.UTF8.GetString(recurso.Conteudo).TrimStart((char)0xFEFF))
				.ChamadasDaUltimaVersao()
				.Select(c =>
					$"Call activity '{c.Id}' in {recurso.Nome} calls '{c.ProcessoChamado}' with bindingType " +
					$"\"{c.Vinculo ?? "latest"}\"{(c.Vinculo is null ? " (the default)" : "")}: it starts the latest version of the child " +
					"deployed on the cluster, which on a shared cluster may be someone else's. Use bindingType=\"deployment\" " +
					"and deploy the child in the same Deploy(...).")
				.ToList();
		}
		catch (System.Xml.XmlException)
		{
			return Array.Empty<string>();
		}
	}

	internal static string Localizar(string caminho)
	{
		if (Path.IsPathRooted(caminho))
			return caminho;

		var naSaida = Path.Combine(AppContext.BaseDirectory, caminho);
		if (File.Exists(naSaida))
			return naSaida;

		return Path.GetFullPath(caminho);
	}

	private static string Arquivo(string recurso)
	{
		if (!ArquivosBpmn.EhCaminho(recurso))
			return Path.GetFullPath(ArquivosBpmn.Localizar(recurso, AppContext.BaseDirectory));

		var arquivo = Localizar(recurso);
		if (!File.Exists(arquivo))
			throw new FileNotFoundException(
				$"BPMN file '{recurso}' not found in the test output folder ({AppContext.BaseDirectory}) " +
				"or the current folder. Copy it to the output with <None Include=\"...\" CopyToOutputDirectory=\"PreserveNewest\" />.",
				recurso);

		return Path.GetFullPath(arquivo);
	}

	private static RecursoBpmn Ler(string arquivo) =>
		new(Path.GetFileName(arquivo), File.ReadAllBytes(arquivo));
}
