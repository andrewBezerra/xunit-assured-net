namespace XUnitAssured.Camunda.Client;

/// <summary>What the Hub says about the API and the credentials.</summary>
/// <param name="ApiVersion">The version of the API the client speaks.</param>
/// <param name="Organization">The organization the credentials are authorized for.</param>
/// <param name="CanCreate">Whether the client may create; <see langword="null"/> on the v2, which does not say.</param>
/// <param name="CanRead">Whether the client may read; <see langword="null"/> on the v2, which does not say.</param>
/// <param name="CanUpdate">Whether the client may update; <see langword="null"/> on the v2, which does not say.</param>
/// <param name="CanDelete">Whether the client may delete; <see langword="null"/> on the v2, which does not say.</param>
public sealed record CamundaHubInfo(
	CamundaHubApiVersion ApiVersion, string? Organization, bool? CanCreate, bool? CanRead, bool? CanUpdate, bool? CanDelete);
