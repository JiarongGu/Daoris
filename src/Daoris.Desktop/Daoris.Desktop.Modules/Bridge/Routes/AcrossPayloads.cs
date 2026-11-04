namespace Daoris.Desktop;

/// <summary>A repository's reading across, or a workspace's, or either cleared with no <c>Read</c>.</summary>
public sealed record ReadAcrossPress(string? Repository, string? Workspace, bool? Read);

/// <summary>A relationship declared, or taken back with no <c>Allow</c>.</summary>
public sealed record WriteAcrossPress(string Repository, string? To, bool Allow = false);
