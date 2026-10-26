namespace Daoris.Driver;

/// <summary>One session's installed lineage hook: the script, the file it appends to, and the node it runs on.</summary>
public sealed record LineageHook(string Script, string File)
{
    /// <summary>The connector's tools, in the harness's own matcher shape.</summary>
    public const string Matcher = "mcp__" + KnowledgeConnector.ServerName + "__.*";

    /// <summary>The node the harness starts the script with: the file Tools resolves (TOOLS5), as the tree guard's.</summary>
    public string Node { get; init; } = "node";
}
