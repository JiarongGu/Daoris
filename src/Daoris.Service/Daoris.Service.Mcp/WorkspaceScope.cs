namespace Daoris.Knowledge.Mcp;

/// <summary>
/// Which circle a tool's call answers from, shared by the tools that answer from one: the search tools and
/// <c>quest_list</c>. The tools' registration adds one; a tool file imports its statics with <c>using static</c>.
/// An agent asking "has anyone solved this" means its own family, not every family the machine can see — and it has no
/// reason to know the wiring, because the wiring is a registry row and not anything in its tree. So the ambient answer is
/// resolved here rather than asked for; naming one explicitly, or <c>all</c>, is how a session looks across, Ask Daoris's included,
/// whose own workspace is its default by its record.
/// </summary>
public sealed class WorkspaceScope(KnowledgeService service, AmbientWorkspace ambient)
{
    /// <summary>The workspace argument's description, the same on every tool that takes one.</summary>
    public const string WorkspaceArgument =
        "Which workspace to answer from. Omit for the one this session belongs to (its repository's, or its "
        + "conversation's), which is almost always right; name another workspace to read it, or `all` to span every workspace on this machine.";

    /// <summary>The default workspace this session's record gives it, or null for one that takes its repository's.</summary>
    public string? Bound => ambient.Bound;

    /// <summary>What the caller named, else the session's default (its record's, or its repository's workspace); null for every workspace.</summary>
    public async Task<string?> ResolveAsync(string? named, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(named))
        {
            // "all" is the deliberate way out of the scope — spelled, so it can never be reached by
            // an empty string or a typo.
            return named.Equals("all", StringComparison.OrdinalIgnoreCase) ? null : named.Trim();
        }

        if (Bound is { Length: > 0 } bound) return bound;
        return await ambient.ResolveAsync(service, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The scope that ran, said on every answer — including when it is every workspace.
    /// </summary>
    /// <remarks>
    /// An unscoped answer looks exactly like one family's answer, and a caller cannot tell the
    /// difference from the results. Naming it is the same discipline as reporting which recall tier
    /// answered: the shape of the answer is part of the answer.
    /// </remarks>
    public static string Scoped(string? scope) =>
        scope is null ? " across every workspace on this machine" : $" in workspace `{scope}`";
}
