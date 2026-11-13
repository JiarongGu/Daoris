namespace Daoris.Knowledge.Mcp;

/// <summary>What a call does, which names the act a call that reached beyond its session's place is recorded as (<see cref="CallPlace"/>).</summary>
public enum ToolClass
{
    /// <summary>Answers from what Daoris knows and changes nothing.</summary>
    Read,

    /// <summary>Changes a record, a mark or another's work, asks or answers someone, or uses a person's grant.</summary>
    Acts,
}

/// <summary>
/// Every tool the connector registers, and its class; <c>ToolClassesTests</c> holds it to the registration. A class refuses no
/// caller: every tool is listed and served to every caller.
/// </summary>
public static class ToolClasses
{
    private const string ProposalSuffix = "_propose";

    /// <summary>Every tool named here, outside the proposals.</summary>
    public static IReadOnlyDictionary<string, ToolClass> Named { get; } = new Dictionary<string, ToolClass>(StringComparer.Ordinal)
    {
        ["knowledge_search"] = ToolClass.Read,
        ["knowledge_get"] = ToolClass.Read,
        ["knowledge_repositories"] = ToolClass.Read,
        ["knowledge_convergence"] = ToolClass.Read,
        ["registry"] = ToolClass.Read,
        ["quest_list"] = ToolClass.Read,
        ["line_read"] = ToolClass.Read,
        ["handback_read"] = ToolClass.Read,
        ["memory_search"] = ToolClass.Read,
        ["memory_get"] = ToolClass.Read,
        ["quest_publish"] = ToolClass.Acts,
        ["quest_respond"] = ToolClass.Acts,
        ["helper_ask"] = ToolClass.Acts,
        ["helper_answer"] = ToolClass.Acts,
        ["check_run"] = ToolClass.Acts,
        ["channel_post"] = ToolClass.Acts,
        ["channel_read"] = ToolClass.Acts,
        ["go_ahead_ask"] = ToolClass.Acts,
        ["question_ask"] = ToolClass.Acts,
        ["opinion_give"] = ToolClass.Acts,
        ["opinion_answer"] = ToolClass.Acts,
        ["review_serve"] = ToolClass.Acts,
        ["review_ready"] = ToolClass.Acts,
        ["partner_answer"] = ToolClass.Acts,
        ["brainstorm_give"] = ToolClass.Acts,
        ["brainstorm_synthesize"] = ToolClass.Acts,
        ["knowledge_refresh"] = ToolClass.Acts,
        ["checkout_read"] = ToolClass.Acts,
        ["memory_write"] = ToolClass.Acts,
        ["memory_end"] = ToolClass.Acts,
        ["system_plan"] = ToolClass.Acts,
    };

    /// <summary>The class of <paramref name="tool"/>, or null for a name this table does not class.</summary>
    public static ToolClass? Of(string? tool)
    {
        if (tool is null) return null;
        if (Named.TryGetValue(tool, out var named)) return named;
        return tool.EndsWith(ProposalSuffix, StringComparison.Ordinal) ? ToolClass.Acts : null;
    }
}
