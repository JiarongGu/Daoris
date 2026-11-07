using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// One record's lineage lines in Claude Code's words: its root is the hook's session, and a connector call is joined to its agent
/// by the hook's line beside it naming the same <c>toolUse</c>; an agent the hook could not read leaves its calls unknown.
/// </summary>
internal sealed class ClaudeChildLines : IChildLines
{
    /// <summary>An agent id the hook sent and Daoris could not read; no id's shape, so no real id equals it.</summary>
    private const string UnreadableAgent = "\0unreadable";

    private readonly Dictionary<string, string?> _agentOf = new(StringComparer.Ordinal);

    public string? Root { get; private set; }

    public IReadOnlyCollection<string> Roots => Root is null ? [] : [Root];

    public void Learn(JsonObject line)
    {
        if (LineageText.Text(line, "by") != "hook") return;
        Root ??= LineageText.Id(line, "session");
        if (LineageText.Text(line, "event") == "call" && LineageText.Id(line, "toolUse") is { } use)
        {
            _agentOf[use] = line["agentUnreadable"] is JsonValue bad && bad.TryGetValue<bool>(out var unread) && unread
                ? UnreadableAgent
                : LineageText.Id(line, "agent");
        }
    }

    public ChildCall Call(JsonObject line)
    {
        if (LineageText.Id(line, "toolUse") is not { } use) return ChildCall.Unknown;
        if (!_agentOf.TryGetValue(use, out var caller)) return ChildCall.Unjoined;
        if (caller == UnreadableAgent) return ChildCall.Unknown;
        return caller is null ? ChildCall.Root : ChildCall.Of(caller);
    }

    /// <summary>A hook's call line names its agent; a connector call is joined by that line, so it moves nothing of its own.</summary>
    public static ChildCall? Wake(JsonObject line) => LineageText.Text(line, "by") switch
    {
        "hook" when LineageText.Text(line, "event") == "call" =>
            line["agentUnreadable"] is not null || LineageText.Id(line, "agent") is not { } agent ? ChildCall.None : ChildCall.Of(agent),
        "call" => ChildCall.None,
        _ => null,
    };
}
