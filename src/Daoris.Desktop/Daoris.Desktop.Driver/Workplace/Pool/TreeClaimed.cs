namespace Daoris.Driver;

/// <summary>The claim a run holds on a slot, as its writes check it: the run and the generation it took the claim at.</summary>
public sealed record ClaimFence(string Workspace, string Repository, int Number, string Run, long Generation);

/// <summary>A claim's whole intent, written before its first effect.</summary>
/// <param name="InPlace">The claim goes on in the slot without switching it (a line on its own branch), so a visit does not hold it back.</param>
public sealed record TreeClaimOpening(
    string Workspace, string Repository, int Number, string Kind, string Holder, string Line, string Branch, string Start, string Expect, string Job)
{
    /// <summary>The phase the claim begins at.</summary>
    public string Phase { get; init; } = TreeClaimPhases.Judged;

    public bool InPlace { get; init; }
}

/// <summary>The answer to a claim or a takeover: the claim and its fence, or why the slot is not claimed.</summary>
public sealed record TreeClaimed(TreeClaim? Claim, ClaimFence? Fence, string? Refused);

/// <summary>What a finished claim leaves the slot's row holding.</summary>
public sealed record SlotSettled(string Registration, string Path, string Line, string Tip, string State, string Said, long Bytes, string Toolchain);

/// <summary>A write of the pool the store's rules refuse.</summary>
public sealed class TreePoolRefusedException(string why) : Exception(why);
