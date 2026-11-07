namespace Daoris.Driver;

/// <summary>One repository's census: the recovery refs no journal record names, or why git could not list them.</summary>
public sealed record RecoveryCount(string Repository, IReadOnlyList<string>? Unrecorded, string? Unread);

/// <summary>
/// **The census of recovery refs the journal does not name**: the refs under <c>refs/daoris/discarded/</c> no act of the journal names,
/// which a door kept before every keep went through the journal (<see cref="RecoveryKeeps"/>). <see cref="RecoveryRetention"/>'s dating from a
/// ref's reflog and <see cref="RecoveryDropping"/> stay for those alone; once every repository on the install reads zero, both
/// go, and this with them. It reads and changes nothing, so it needs no lease: <c>daoris work recovery drop --unheld --plan</c> says it.
/// </summary>
public static class RecoveryCensus
{
    /// <summary>One <c>for-each-ref</c> of the prefix, less every ref a journal record of <paramref name="repository"/> names.</summary>
    public static async Task<RecoveryCount> CountAsync(string home, string root, string repository, CancellationToken ct)
    {
        var (code, listed, err) = await WorkingTree.GitAsync(root, ["for-each-ref", "--format=%(refname)", ContentHolds.DiscardedPrefix], ct).ConfigureAwait(false);
        if (code != 0) return new(repository, null, ContentHolds.Said(err));
        var recorded = RecoveryKeeps.Recorded(home, repository);
        return new(repository, [.. listed.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(each => !recorded.ContainsKey(each))], null);
    }

    /// <summary>The census in a line.</summary>
    public static string Words(RecoveryCount count) => count switch
    {
        { Unread: { } unread } => $"census: git could not list `{count.Repository}`'s recovery refs ({unread}), so they are not counted.",
        { Unrecorded: [] } => $"census: every recovery ref in `{count.Repository}` is named by a journal record.",
        { Unrecorded: { } refs } => $"census: {refs.Count} recovery {(refs.Count == 1 ? "ref" : "refs")} in `{count.Repository}` no journal record names, kept "
            + $"before every keep was journaled: {string.Join(", ", refs.Select(each => $"`{each}`"))}.",
        _ => $"census: `{count.Repository}` was not read.",
    };
}
