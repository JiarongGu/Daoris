namespace Daoris.Knowledge.Mcp;

/// <summary>Who may call a tool, for a connector that names a session (FABRIC-C2a, D173's FABRIC-C2a note, point 3).</summary>
public enum ToolClass
{
    /// <summary>Answers from what Daoris knows and changes nothing: served to every caller.</summary>
    Read,

    /// <summary>Changes a record, a mark or another's work, asks or answers someone, or uses a person's grant: a proven root's alone.</summary>
    Restricted,

    /// <summary>A quest to another repository, which a child may publish in its plain shape (the plan's §4; canary 8).</summary>
    Publish,
}

/// <summary>
/// Every tool the connector registers, and its class (FABRIC-C2a, D173's FABRIC-C2a note, point 3). A census
/// (<c>ToolClassesTests</c>) holds it to the registration, so a tool added later has no class, and is refused to a child, until
/// it is given one here.
/// </summary>
/// <remarks>
/// The note's reads, and <c>line_read</c> and <c>handback_read</c>, added since, which read records and move nothing, and
/// <c>memory_search</c> and <c>memory_get</c>, which open every engine with no reinforcement (KNOWCENTRAL2b).
/// <c>channel_read</c> is restricted because it moves the parent's read mark, <c>knowledge_refresh</c> because it rebuilds the
/// index every session shares, and <c>checkout_read</c> and <c>housekeeping_inspect</c> because they read under a person's grant
/// to the session. Every <c>*_propose</c> is restricted by its suffix.
/// </remarks>
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
        ["quest_publish"] = ToolClass.Publish,
        ["quest_respond"] = ToolClass.Restricted,
        ["helper_ask"] = ToolClass.Restricted,
        ["helper_answer"] = ToolClass.Restricted,
        ["channel_post"] = ToolClass.Restricted,
        ["channel_read"] = ToolClass.Restricted,
        ["go_ahead_ask"] = ToolClass.Restricted,
        ["question_ask"] = ToolClass.Restricted,
        ["opinion_give"] = ToolClass.Restricted,
        ["opinion_answer"] = ToolClass.Restricted,
        ["review_serve"] = ToolClass.Restricted,
        ["review_ready"] = ToolClass.Restricted,
        ["partner_answer"] = ToolClass.Restricted,
        ["brainstorm_give"] = ToolClass.Restricted,
        ["brainstorm_synthesize"] = ToolClass.Restricted,
        ["knowledge_refresh"] = ToolClass.Restricted,
        ["checkout_read"] = ToolClass.Restricted,
        ["housekeeping_inspect"] = ToolClass.Restricted,
        ["housekeeping_clean_up"] = ToolClass.Restricted,
        ["housekeeping_discard_branch"] = ToolClass.Restricted,
        ["settings_cap"] = ToolClass.Restricted,
        ["settings_cooloff"] = ToolClass.Restricted,
        ["settings_language"] = ToolClass.Restricted,
        ["settings_notify"] = ToolClass.Restricted,
        ["settings_timeout"] = ToolClass.Restricted,
        ["memory_write"] = ToolClass.Restricted,
        ["memory_end"] = ToolClass.Restricted,
    };

    /// <summary>The reads, which a refusal names as what remains.</summary>
    public static IReadOnlyList<string> Reads { get; } = [.. Named.Where(pair => pair.Value == ToolClass.Read).Select(pair => pair.Key)];

    /// <summary>The class of <paramref name="tool"/>, or null for a name this table does not class.</summary>
    public static ToolClass? Of(string? tool)
    {
        if (tool is null) return null;
        if (Named.TryGetValue(tool, out var named)) return named;
        return tool.EndsWith(ProposalSuffix, StringComparison.Ordinal) ? ToolClass.Restricted : null;
    }
}
