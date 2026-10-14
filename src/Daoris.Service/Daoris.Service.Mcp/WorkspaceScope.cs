namespace Daoris.Knowledge.Mcp;

/// <summary>
/// Which circle a tool's call answers from (D48 §4), shared by the tools that answer from one: the search tools and
/// <c>quest_list</c>. The host registers one beside the tool classes (PART10a); a tool file imports its statics with
/// <c>using static</c>, so its descriptions and sentences read as they did.
/// </summary>
/// <remarks>
/// An agent asking "has anyone solved this" means its own family, not every family the machine can see — and it has no
/// reason to know the wiring, because the wiring is a registry row and not anything in its tree. So the ambient answer is
/// resolved here rather than asked for; naming one explicitly is how a person looks across.
/// </remarks>
public sealed class WorkspaceScope(KnowledgeService service, AmbientWorkspace ambient)
{
    /// <summary>The workspace argument's description, the same on every tool that takes one.</summary>
    public const string WorkspaceArgument =
        "Which workspace to answer from. Omit for the one this session's repository belongs to, which "
        + "is almost always right; `all` deliberately spans every workspace on this machine.";

    /// <summary>
    /// What the caller named, or the workspace of the repository this session is running in; null for every workspace.
    /// </summary>
    public async Task<string?> ResolveAsync(string? named, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(named))
        {
            // "all" is the deliberate way out of the scope — spelled, so it can never be reached by
            // an empty string or a typo.
            return named.Equals("all", StringComparison.OrdinalIgnoreCase) ? null : named.Trim();
        }

        return await ambient.ResolveAsync(service, ct).ConfigureAwait(false);
    }

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
