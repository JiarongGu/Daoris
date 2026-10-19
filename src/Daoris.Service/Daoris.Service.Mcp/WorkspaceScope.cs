namespace Daoris.Knowledge.Mcp;

/// <summary>
/// Which circle a tool's call answers from (D48 §4), shared by the tools that answer from one: the search tools and
/// <c>quest_list</c>. The tools' registration adds one (PART10a); a tool file imports its statics with <c>using static</c>.
/// </summary>
/// <remarks>
/// <para>An agent asking "has anyone solved this" means its own family, not every family the machine can see — and it has no
/// reason to know the wiring, because the wiring is a registry row and not anything in its tree. So the ambient answer is
/// resolved here rather than asked for; naming one explicitly is how a person looks across.</para>
///
/// <para><b>A session bound to its workspace never looks across</b> (ASKSCOPE1c, D158's ASKSCOPE1 note point 5): Ask Daoris in a
/// workspace's room is bound by its record, not by its arguments. A tool naming another workspace, or <c>all</c>, answers
/// <see cref="Refused"/>'s sentence, and <see cref="ResolveAsync"/> answers the bound workspace whatever is named, so a tool that
/// forgot to ask still never answers wider.</para>
/// </remarks>
public sealed class WorkspaceScope(KnowledgeService service, AmbientWorkspace ambient)
{
    /// <summary>The workspace argument's description, the same on every tool that takes one.</summary>
    public const string WorkspaceArgument =
        "Which workspace to answer from. Omit for the one this session's repository belongs to, which "
        + "is almost always right; `all` deliberately spans every workspace on this machine.";

    /// <summary>The workspace this session is bound to by its record (ASKSCOPE1c), or null for one that may look across.</summary>
    public string? Bound => ambient.Bound;

    /// <summary>
    /// What the caller named, or the workspace of the repository this session is running in; null for every workspace. A bound
    /// session's is its workspace, whatever was named.
    /// </summary>
    public async Task<string?> ResolveAsync(string? named, CancellationToken ct)
    {
        if (Bound is { Length: > 0 } bound) return bound;
        if (!string.IsNullOrWhiteSpace(named))
        {
            // "all" is the deliberate way out of the scope — spelled, so it can never be reached by
            // an empty string or a typo.
            return named.Equals("all", StringComparison.OrdinalIgnoreCase) ? null : named.Trim();
        }

        return await ambient.ResolveAsync(service, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The sentence a bound session's tool answers when its call names another workspace, or <c>all</c> (ASKSCOPE1c); null where
    /// it named none, or its own, or the session is bound to none.
    /// </summary>
    public string? Refused(string? named)
    {
        if (Bound is not { Length: > 0 } bound || string.IsNullOrWhiteSpace(named)) return null;
        var asked = named.Trim();
        if (Workspaces.Same(asked, bound)) return null;
        var reach = asked.Equals("all", StringComparison.OrdinalIgnoreCase) ? "every workspace" : $"workspace `{asked}`";
        return $"Refused: this conversation is workspace `{bound}`'s, so it answers from `{bound}` alone, never from {reach}. "
            + $"Leave `workspace` out, or name `{bound}`; another workspace is read in a conversation started from a window on it.";
    }

    /// <summary>
    /// The sentence a bound session's read answers for an entry of another workspace (ASKSCOPE1c); null where the entry is its
    /// workspace's, or the session is bound to none.
    /// </summary>
    public string? Outside(KnowledgeEntry entry) =>
        Bound is { Length: > 0 } bound && !Workspaces.Same(entry.Workspace, bound)
            ? $"Refused: `{entry.Id}` is not workspace `{bound}`'s, and this conversation reads `{bound}`'s alone. Another "
              + "workspace's knowledge is read in a conversation started from a window on it."
            : null;

    /// <summary>
    /// The scope that ran, said on every answer — including when it was every workspace.
    /// </summary>
    /// <remarks>
    /// An unscoped answer looks exactly like one family's answer, and a caller cannot tell the
    /// difference from the results. Naming it is the same discipline as reporting which recall tier
    /// answered (D24): the shape of the answer is part of the answer.
    /// </remarks>
    public static string Scoped(string? scope) =>
        scope is null ? " across every workspace on this machine" : $" in workspace `{scope}`";
}
