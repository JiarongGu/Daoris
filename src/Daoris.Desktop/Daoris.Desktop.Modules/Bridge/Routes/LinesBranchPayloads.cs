namespace Daoris.Desktop;

/// <summary>
/// A session's or a branch's removal. <c>Named</c> is the one name <c>trees remove</c> takes, with <c>Repository</c> only where it must
/// say which; the page's <c>Repository</c> and <c>Branch</c> are read with no <c>Named</c>.
/// </summary>
public sealed record DiscardBranchPress(string? Named, string? Repository, string? Branch, bool Force = false);

/// <summary>The clean-up's list: one <c>Workspace</c>'s checkouts, or every one's; <c>Said</c> asks for the verb's words beside the list.</summary>
public sealed record SweepPlanRead(string? Workspace, bool Said = false);

/// <summary>The clean-up's press: only the branches the page listed to go (<c>Only</c>, none sent takes every one the proof clears).</summary>
public sealed record SweepPress(string? Workspace, IReadOnlyList<string>? Only);

/// <summary>
/// The sync list: the <c>Repository</c> named, the <c>Also</c> ticked or <c>All</c>, in one <c>Workspace</c>'s checkouts or every one's;
/// <c>Said</c> asks for the verb's words beside the list.
/// </summary>
public sealed record TreesSyncPlanRead(string? Repository, string? Workspace, IReadOnlyList<string>? Also, bool All = false, bool Said = false);

/// <summary>
/// The sync press: its list's scope and the rows it listed (<c>Only</c>). The verb fetches as it presses (<c>Fetch</c>) and has the registry
/// follow the lines it moved (<c>Follow</c>); the page sends neither.
/// </summary>
public sealed record TreesSyncPress(
    string? Repository, string? Workspace, IReadOnlyList<string>? Also, IReadOnlyList<string>? Only, bool All = false, bool Fetch = false, bool Follow = false);

/// <summary>One recovery ref the page showed, at the commit it was shown at.</summary>
public sealed record RecoveryRefAsked(string? Ref, string? Commit);

/// <summary>The recovery refs of a repository deleted, each only at the commit it was shown at.</summary>
public sealed record RecoveryDropPress(string Repository, IReadOnlyList<RecoveryRefAsked>? Refs);

/// <summary>A landing branch withdrawn, its commits kept on a recovery ref; <c>Plan</c> changes nothing.</summary>
public sealed record WithdrawLandingBranchPress(string Repository, string Branch, string? Why, bool Plan = false);

/// <summary>The units' milestones, or with <c>Unit</c> the ones that name picks (its id, session or branch).</summary>
public sealed record WorkStateRead(string? Unit);

/// <summary>A held attempt of a node worded with its <c>Resolution</c>; <c>Said</c> asks for the terminal verb's lines (<c>ask-fix</c> only).</summary>
public sealed record WorkHeldPress(string Unit, string Node, int Attempt, string Resolution, bool Said = false);
