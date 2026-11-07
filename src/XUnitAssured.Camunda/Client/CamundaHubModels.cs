using System;
using System.Collections.Generic;

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

/// <summary>A workspace of the Hub (a "project" in the API v1).</summary>
/// <param name="Key">The key of the workspace.</param>
/// <param name="Name">The name.</param>
public sealed record CamundaHubWorkspace(string Key, string Name);

/// <summary>A workspace with its projects.</summary>
/// <param name="Workspace">The workspace.</param>
/// <param name="Projects">Its projects.</param>
public sealed record CamundaHubWorkspaceDetail(CamundaHubWorkspace Workspace, IReadOnlyList<CamundaHubProject> Projects);

/// <summary>A project of the Hub, inside a workspace (a "process application" in the API v1).</summary>
/// <param name="Key">The key of the project.</param>
/// <param name="Name">The name.</param>
/// <param name="WorkspaceKey">The key of its workspace.</param>
public sealed record CamundaHubProject(string Key, string Name, string? WorkspaceKey);

/// <summary>A folder of a project of the Hub.</summary>
/// <param name="Key">The key of the folder.</param>
/// <param name="Name">The name.</param>
/// <param name="ProjectKey">The key of its project.</param>
/// <param name="ParentFolderKey">The key of the folder it is in; <see langword="null"/> at the root of the project.</param>
public sealed record CamundaHubFolder(string Key, string Name, string? ProjectKey, string? ParentFolderKey);

/// <summary>The type of a file of the Hub that the client reads and writes.</summary>
public enum CamundaHubFileType
{
	/// <summary>A BPMN diagram.</summary>
	Bpmn,

	/// <summary>A form.</summary>
	Form
}

/// <summary>What the Hub says about a file, without its content.</summary>
/// <param name="Key">The key of the file (the <c>id</c> in the API v1).</param>
/// <param name="Name">The name, without the extension.</param>
/// <param name="ProjectKey">The key of its project.</param>
/// <param name="FolderKey">The key of its folder; <see langword="null"/> at the root of the project.</param>
/// <param name="Type">The type; <see langword="null"/> for one the client does not handle, such as a DMN.</param>
/// <param name="Revision">The revision, which an update must send back.</param>
public sealed record CamundaHubFileMetadata(string Key, string Name, string? ProjectKey, string? FolderKey, CamundaHubFileType? Type, int Revision);

/// <summary>What a project or a folder holds: its folders and its files, without their content.</summary>
/// <param name="Folders">The folders right inside it.</param>
/// <param name="Files">The files right inside it.</param>
public sealed record CamundaHubContent(IReadOnlyList<CamundaHubFolder> Folders, IReadOnlyList<CamundaHubFileMetadata> Files);

/// <summary>A file of the Hub, with its content.</summary>
/// <param name="Metadata">What the Hub says about the file: its key, name, project, folder, type and revision.</param>
/// <param name="Content">The content, as the Hub keeps it: the XML of a BPMN, the JSON of a form.</param>
public sealed record CamundaHubFile(CamundaHubFileMetadata Metadata, string Content);

/// <summary>A version of a file of the Hub: a snapshot of its content, which the Hub can restore.</summary>
/// <param name="Key">The key of the version (the <c>id</c> in the API v1).</param>
/// <param name="Name">The name.</param>
/// <param name="Description">The description; <see langword="null"/> when it has none.</param>
/// <param name="FileKey">The key of its file.</param>
/// <param name="Created">When it was created; <see langword="null"/> when the Hub does not say.</param>
public sealed record CamundaHubVersion(string Key, string Name, string? Description, string? FileKey, DateTimeOffset? Created);
