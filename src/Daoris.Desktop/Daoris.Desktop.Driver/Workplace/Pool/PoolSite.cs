namespace Daoris.Driver;

/// <summary>Where a pool's act runs: the home, the repository's registered checkout and the repository's name.</summary>
internal sealed record PoolSite(string Home, string Root, string Workspace, string Repository)
{
    /// <summary>The folder a repository's trees live under, where its slots are.</summary>
    internal string Trees => Path.Combine(DaorisHome.Entry(Home, HomeEntry.Trees), Workspace, Repository);

    internal string FolderOf(int number) => KeptTree.FolderIn(Trees, number);
}

/// <summary>A take of a free slot by a new line, or a return of a line that let one go.</summary>
/// <param name="Line">The line's path name, <c>s-&lt;8 hex&gt;</c>; its branch is <c>daoris/</c> and it.</param>
/// <param name="Start">The commit a new branch grows at, or a returning line's tip.</param>
/// <param name="Returns">The line has been in the pool: its branch exists and the slot is switched to it, or is on it already.</param>
/// <param name="LineBranch">The canonical line the branch grows from, recorded as an open records it.</param>
/// <param name="GrewFrom">The branch a chain's step grew from, when there is one.</param>
/// <param name="Session">The session that locks the slot once its record is open; empty leaves the pool's free lock.</param>
internal sealed record PoolTake(PoolSite Site, int Number, string Holder, string Line, string Start, bool Returns, string? LineBranch, string? GrewFrom, string Session)
{
    internal string Branch => $"{TreeFollows.SessionPrefix}{Line}";
}

/// <summary>What a pool act came to: the slot as it stands, or why it is not done.</summary>
/// <param name="Refused">Why the claim is not taken or the judgement refuses the slot; nothing changes.</param>
/// <param name="Stuck">Why the slot stays where a refusal finds it; it is out of the pool until a look judges it clean.</param>
/// <param name="Record">The opened record's id.</param>
internal sealed record PoolDone(KeptTree? Slot, string? Refused, string? Stuck, string? Record)
{
    internal bool Ok => Refused is null && Stuck is null;

    internal static PoolDone Not(string why) => new(null, why, null, null);
}

/// <summary>What the pool asked the service about a claim's keyed open.</summary>
internal enum PoolOpens
{
    /// <summary>The open landed.</summary>
    Landed,

    /// <summary>The open never landed.</summary>
    Never,

    /// <summary>The service did not answer, which is not a "never".</summary>
    NoAnswer,
}

/// <summary>The seams of the pool's acts: the keyed open it requests, what it asks of the service, and the moments a test stops it at.</summary>
internal sealed class PoolSeams
{
    /// <summary>The keyed open of the claim's record, under its <c>Opens</c> key; the record's id, or null where the open did not land.</summary>
    internal Func<TreeClaim, CancellationToken, Task<string?>>? Open { get; init; }

    /// <summary>Whether the open a claim requested landed, asked by a takeover.</summary>
    internal Func<TreeClaim, CancellationToken, Task<(PoolOpens Answer, string Record)>>? Opened { get; init; }

    /// <summary>The holds on a slot beside its git facts, read before each judgement; null reads none.</summary>
    internal Func<KeptTree, CancellationToken, Task<SlotJudgementFacts>>? Holds { get; init; }

    /// <summary>The key of the rules' <c>toolchain</c> outputs in a tree; empty where none are read.</summary>
    internal Func<string, CancellationToken, Task<string>>? Toolchain { get; init; }

    internal long? RenewBytes { get; init; }

    /// <summary>Called with the phase an act has reached and its slot's folder; a test stops an act there by throwing.</summary>
    internal Func<string, string, Task>? Reached { get; init; }

    /// <summary>Called after the judgement and before the switch, with the slot's folder; a test plants a write there.</summary>
    internal Func<string, Task>? BeforeSwitch { get; init; }

    internal async Task ReachedAsync(string phase, string folder)
    {
        if (Reached is { } reached) await reached(phase, folder).ConfigureAwait(false);
    }
}
