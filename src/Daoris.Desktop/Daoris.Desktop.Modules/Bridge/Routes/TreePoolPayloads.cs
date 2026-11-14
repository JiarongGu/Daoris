namespace Daoris.Desktop;

/// <summary>The repository whose kept slots the page reads on this machine.</summary>
public sealed record TreePoolRead(string? Workspace, string? Repository);
