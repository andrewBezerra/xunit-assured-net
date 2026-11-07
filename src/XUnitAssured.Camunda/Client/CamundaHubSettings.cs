using System.Text.Json.Serialization;

namespace XUnitAssured.Camunda.Client;

/// <summary>Which version of the Hub API the client speaks.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CamundaHubApiVersion>))]
public enum CamundaHubApiVersion
{
	/// <summary>
	/// The Web Modeler API (<c>/api/v1</c>), deprecated in Camunda 8.10 and removed in 8.12. Its "project" is the
	/// workspace of the Hub, and its "process application" is the project.
	/// </summary>
	V1,

	/// <summary>The Camunda Hub API (<c>/api/v2</c>), from Camunda 8.10 on.</summary>
	V2
}

/// <summary>
/// What the <see cref="CamundaHubClient"/> needs to reach the Camunda Hub (the Web Modeler): its address and an OAuth
/// client. Unlike <see cref="CamundaSettings"/>, nothing comes from the <c>CAMUNDA_*</c> environment variables.
/// </summary>
public sealed class CamundaHubSettings
{
	/// <summary>
	/// The address of the Hub, without <c>/api/v1</c> or <c>/api/v2</c>, such as <c>http://localhost:8070</c>. A path
	/// after the host, such as <c>http://host/modeler</c>, is kept.
	/// </summary>
	[JsonPropertyName("restAddress")]
	public string? RestAddress { get; set; }

	/// <summary>
	/// The OAuth client (client credentials): the client id, the client secret and the token address are required; the
	/// audience is <see cref="CamundaHubClient.DefaultAudience"/> when empty, and the scope is sent only when given. The
	/// type must be <c>OAuth</c>, or <see langword="null"/>: the Hub takes only a JWT.
	/// </summary>
	[JsonPropertyName("authentication")]
	public CamundaAuthenticationSettings Authentication { get; set; } = new();

	/// <summary>
	/// The version of the Hub API; <see langword="null"/> lets the client choose: the v2 when the Hub has it, otherwise
	/// the v1.
	/// </summary>
	[JsonPropertyName("apiVersion")]
	public CamundaHubApiVersion? ApiVersion { get; set; }
}
