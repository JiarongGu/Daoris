namespace Daoris.Driver;

/// <summary>An attached file as kept for its session: the person's name for it, and where it lies.</summary>
public sealed record KeptFile(string Name, string Path);
