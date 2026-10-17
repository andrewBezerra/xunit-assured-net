using System;

using XUnitAssured.Core.Abstractions;

namespace XUnitAssured.Camunda.Testing;

/// <summary>
/// O cluster que as cadeias de uma classe de teste usam.
/// </summary>
/// <remarks>
/// <para>
/// Sozinho, lê a seção <c>camunda</c> do <c>testsettings.json</c> (e as variáveis <c>CAMUNDA_*</c>),
/// como uma cadeia sem fixture já faria; serve de base. O <c>CamundaContainerFixture</c>, do pacote
/// <c>XUnitAssured.Camunda.Testcontainers</c>, troca as configurações pelas do Camunda que ele sobe.
/// </para>
/// <para>
/// Use com <see cref="CamundaTestBase{TFixture}"/>, que entrega as configurações a cada cadeia. Para
/// a suíte inteira compartilhar um cluster, use-o como <c>ICollectionFixture</c>.
/// </para>
/// </remarks>
public class CamundaClusterFixture : ITestContextSeeder
{
	/// <summary>Lê as configurações do <c>testsettings.json</c>.</summary>
	public CamundaClusterFixture() : this(CamundaSettings.Load()) { }

	/// <summary>Usa estas configurações.</summary>
	/// <param name="settings">O cluster, a autenticação e os prazos.</param>
	public CamundaClusterFixture(CamundaSettings settings) =>
		Settings = settings ?? throw new ArgumentNullException(nameof(settings));

	/// <summary>As configurações que cada cadeia recebe.</summary>
	public CamundaSettings Settings { get; protected set; }

	/// <inheritdoc />
	public virtual void Seed(ITestContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		context.SetProperty(CamundaSettings.ChaveNoContexto, Settings);
	}
}
