using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

using Xunit.Abstractions;
using Xunit.Sdk;

using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.DSL;

namespace XUnitAssured.Camunda.TestCases;

/// <summary>
/// Um caso de teste de um arquivo JSON no formato do Camunda Process Test (<c>cpt-test-cases</c>,
/// o mesmo do Test Studio do Web Modeler).
/// </summary>
/// <remarks>
/// <para>
/// Roda num cluster compartilhado, então nem toda instrução do formato cabe: as que mexem no relógio
/// do cluster, fazem broadcast ou implantam algo falso para todos são recusadas antes de o caso
/// começar, dizendo por quê. A lista está no README do pacote.
/// </para>
/// <para>
/// Use com <see cref="CamundaTestCasesAttribute"/>, que faz de cada caso uma linha de um
/// <c>[Theory]</c>.
/// </para>
/// </remarks>
public sealed class CamundaTestCase : IXunitSerializable
{
	/// <summary>Para o xUnit desserializar a linha do <c>[Theory]</c>.</summary>
	[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
	public CamundaTestCase() { }

	internal CamundaTestCase(string file, int index, string name, string? description, JsonElement instructions)
	{
		File = file;
		Index = index;
		Name = name;
		Description = description;
		Instructions = instructions;
	}

	/// <summary>O arquivo de onde o caso veio.</summary>
	public string File { get; private set; } = string.Empty;

	/// <summary>A posição do caso no arquivo, a partir de zero.</summary>
	public int Index { get; private set; }

	/// <summary>O <c>name</c> do caso.</summary>
	public string Name { get; private set; } = string.Empty;

	/// <summary>O <c>description</c> do caso, quando há.</summary>
	public string? Description { get; private set; }

	internal JsonElement Instructions { get; private set; }

	/// <summary>Roda o caso no cluster do <c>testsettings.json</c>, implantando antes estes arquivos ou processos.</summary>
	/// <param name="deploy">O que implantar, como no <c>Deploy</c>: ids de processo ou caminhos. Vazio: nada.</param>
	public Task RunAsync(params string[] deploy) => RunAsync(ScenarioDsl.Given(), deploy);

	/// <summary>Roda o caso na cadeia dada: a do <c>CamundaTestBase.Given()</c>, para usar o cluster do fixture.</summary>
	/// <param name="scenario">A cadeia, de onde vêm o cluster e os prazos.</param>
	/// <param name="deploy">O que implantar, como no <c>Deploy</c>: ids de processo ou caminhos. Vazio: nada.</param>
	public Task RunAsync(ITestScenario scenario, params string[] deploy) =>
		new ExecutorDeCaso(this, scenario ?? throw new ArgumentNullException(nameof(scenario)), deploy ?? Array.Empty<string>()).ExecutarAsync();

	/// <inheritdoc />
	public override string ToString() => Name;

	void IXunitSerializable.Serialize(IXunitSerializationInfo info)
	{
		info.AddValue(nameof(File), File);
		info.AddValue(nameof(Index), Index);
	}

	void IXunitSerializable.Deserialize(IXunitSerializationInfo info)
	{
		var original = CamundaTestCases.Load(info.GetValue<string>(nameof(File)))[info.GetValue<int>(nameof(Index))];
		File = original.File;
		Index = original.Index;
		Name = original.Name;
		Description = original.Description;
		Instructions = original.Instructions;
	}
}

/// <summary>Lê os arquivos de casos de teste JSON.</summary>
public static class CamundaTestCases
{
	/// <summary>
	/// Os casos de um arquivo. Um caminho relativo é procurado na pasta de saída do teste e depois na
	/// pasta atual, como no <c>Deploy</c>.
	/// </summary>
	/// <param name="path">O arquivo JSON, com <c>testCases</c> na raiz.</param>
	public static IReadOnlyList<CamundaTestCase> Load(string path)
	{
		if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path is required.", nameof(path));

		var arquivo = Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path);
		if (!System.IO.File.Exists(arquivo))
			arquivo = Path.GetFullPath(path);
		if (!System.IO.File.Exists(arquivo))
			throw new FileNotFoundException(
				$"Test case file '{path}' not found in the test output folder ({AppContext.BaseDirectory}) or the current folder. " +
				"Copy it to the output with <None Include=\"...\" CopyToOutputDirectory=\"PreserveNewest\" />.", path);

		using var documento = JsonDocument.Parse(System.IO.File.ReadAllText(arquivo));
		if (!documento.RootElement.TryGetProperty("testCases", out var casos) || casos.ValueKind != JsonValueKind.Array)
			throw new FormatException($"'{path}' has no testCases array at its root.");

		return casos.EnumerateArray()
			.Select((caso, i) => new CamundaTestCase(
				path,
				i,
				caso.TryGetProperty("name", out var nome) ? nome.GetString() ?? $"#{i + 1}" : $"#{i + 1}",
				caso.TryGetProperty("description", out var descricao) ? descricao.GetString() : null,
				caso.TryGetProperty("instructions", out var instrucoes) ? instrucoes.Clone() : default))
			.ToList();
	}
}

/// <summary>
/// Faz de cada caso de um arquivo JSON uma linha do <c>[Theory]</c>.
/// </summary>
/// <example>
/// <code>
/// [Theory]
/// [CamundaTestCases("test-cases/order.json")]
/// public Task Order(CamundaTestCase testCase) => testCase.RunAsync("order", "shipping");
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class CamundaTestCasesAttribute : DataAttribute
{
	/// <summary>Os casos deste arquivo.</summary>
	/// <param name="path">O arquivo JSON, relativo à pasta de saída do teste.</param>
	public CamundaTestCasesAttribute(string path) => Path = path;

	/// <summary>O arquivo JSON.</summary>
	public string Path { get; }

	/// <inheritdoc />
	public override IEnumerable<object[]> GetData(MethodInfo testMethod) =>
		CamundaTestCases.Load(Path).Select(caso => new object[] { caso });
}
