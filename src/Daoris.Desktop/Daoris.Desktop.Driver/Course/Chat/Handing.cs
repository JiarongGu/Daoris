namespace Daoris.Driver;

/// <summary>The earlier conversation's words, kept as this conversation's file, and the line its agent is told beside them.</summary>
internal sealed record Handing(IReadOnlyList<KeptFile> Files, string Preface);
