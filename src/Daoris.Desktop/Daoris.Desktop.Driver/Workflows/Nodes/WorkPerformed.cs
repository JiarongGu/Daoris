namespace Daoris.Driver;

/// <summary>One thing the performer did, on one unit's node.</summary>
public sealed record WorkPerformed(string Unit, string Node, string What, string? Code = null);
