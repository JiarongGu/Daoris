using System.Text.Json;

namespace Daoris.Knowledge.Mcp;

/// <summary>
/// A call that named a place beyond the session's own, as Daoris's door saw it: what it did (<see cref="Search"/>, <see cref="Read"/>,
/// <see cref="Propose"/> or <see cref="Press"/>), the workspace (or <c>all</c>) and the repository its arguments named, and what it
/// named of them: a search's query, a read's path, a proposal's kind, an act's tool. Read from the call's arguments alone; the
/// agent's words are never read.
/// </summary>
public sealed record CallPlace(string Tool, string Act, string? Workspace, string? Repository, string? What)
{
    public const string Search = "search";
    public const string Read = "read";
    public const string Propose = "propose";
    public const string Press = "press";

    private const string ProposalSuffix = "_propose";

    /// <summary>The place a call named, or null where it named no workspace or repository, or its tool is none the table classes.</summary>
    public static CallPlace? Of(string? tool, IDictionary<string, JsonElement>? arguments)
    {
        if (tool is null || ToolClasses.Of(tool) is not { } kind) return null;
        var workspace = Text(arguments, "workspace");
        var repository = Text(arguments, "repository");
        if (workspace is null && repository is null) return null;

        var act = tool switch
        {
            "checkout_read" or "housekeeping_inspect" => Read,
            _ when tool.EndsWith(ProposalSuffix, StringComparison.Ordinal) => Propose,
            _ when kind == ToolClass.Read => Search,
            _ => Press,
        };
        var what = act switch
        {
            Read => Text(arguments, "path") ?? (tool == "housekeeping_inspect" ? "housekeeping" : null),
            Search => Text(arguments, "query"),
            Propose => tool[..^ProposalSuffix.Length],
            _ => tool,
        };
        return new(tool, act, workspace, repository, what);
    }

    private static string? Text(IDictionary<string, JsonElement>? arguments, string name) =>
        arguments is not null && arguments.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.String
        && value.GetString()?.Trim() is { Length: > 0 } text ? text : null;
}
