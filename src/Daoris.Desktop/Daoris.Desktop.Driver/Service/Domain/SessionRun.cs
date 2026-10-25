namespace Daoris.Driver;

/// <summary>A session record this machine keeps, and the repository it ran in.</summary>
public sealed record SessionRun(string Session, string Repository);
