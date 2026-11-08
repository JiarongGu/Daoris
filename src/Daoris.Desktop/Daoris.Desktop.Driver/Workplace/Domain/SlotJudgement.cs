namespace Daoris.Driver;

/// <summary>How a slot's lock reads to the pool.</summary>
public enum SlotLocks
{
    /// <summary>The pool's own free reason.</summary>
    Free,

    /// <summary>A session's lock whose record no longer holds the slot.</summary>
    SessionNoRecord,

    /// <summary>No lock at all.</summary>
    None,

    /// <summary>Any other reason: a person's, or an opening's whose process lives.</summary>
    Other,
}

/// <summary>
/// What the caller reads of a slot before it is taken for another line. The defaults are those of a clean, free slot, so a case names only what
/// differs.
/// </summary>
public sealed record SlotJudgementFacts
{
    // Step 1: what holds it.
    public bool HeldByLine { get; init; }

    public bool Claimed { get; init; }

    public bool Visited { get; init; }

    public bool LandingUnderWay { get; init; }

    public bool StandingMove { get; init; }

    public bool LandDue { get; init; }

    public bool StartDue { get; init; }

    public bool HelperWait { get; init; }

    // Step 2: git's view of it.
    public bool Listed { get; init; } = true;

    public bool BoundElsewhere { get; init; }

    public SlotLocks Lock { get; init; } = SlotLocks.Free;

    // Step 3: its links and repositories.
    public bool LinkUnderTrees { get; init; }

    /// <summary>A path marked assume-unchanged or skip-worktree.</summary>
    public bool PathHidden { get; init; }

    public bool Unread { get; init; }

    /// <summary>Links no placement record holds.</summary>
    public int LinksNotRecorded { get; init; }

    /// <summary>A <c>.git</c> file or folder below its top, a linked worktree or a bare repository included.</summary>
    public bool RepositoryInside { get; init; }

    // Step 4.
    public bool OperationHalfDone { get; init; }

    // Step 5: what status shows.
    public bool TrackedChange { get; init; }

    public bool UntrackedPath { get; init; }

    /// <summary>An ignored path that is neither the slot's kept kinds nor Daoris's own copy, a dependency whose record fails among them.</summary>
    public bool LineOwnIgnored { get; init; }

    // Step 6: where the line let it go.
    public string Head { get; init; } = "tip";

    /// <summary>The commit the last let-go recorded, or the slot's tip while it is parked; empty where none is recorded.</summary>
    public string Expected { get; init; } = "tip";

    /// <summary>Whether a ref holds <see cref="Head"/>.</summary>
    public bool RefHolds { get; init; } = true;
}

/// <summary>The first step that failed, or none (<see cref="Step"/> 0).</summary>
public sealed record SlotVerdict(int Step, string Code, string Why)
{
    public bool Clean => Step == 0;

    public static SlotVerdict Passed { get; } = new(0, "", "");
}

/// <summary>
/// The judgement of a slot's safety to be taken for another line, pure: each step reads what the caller found and the first that fails
/// answers. Nothing is reset to make a slot pass, so a slot holding a change, an untracked file, a half-done operation or a commit no ref
/// holds is never taken.
/// </summary>
public static class SlotJudgement
{
    public const string Held = "held";

    public const string Unlisted = "unlisted";

    public const string Bound = "bound";

    public const string Locked = "locked";

    public const string Linked = "linked";

    public const string Unread = "unread";

    public const string RepositoryInside = "repository-inside";

    public const string HalfDone = "half-done";

    public const string Changed = "changed";

    public const string LinesOwn = "lines-own";

    public const string HeadMoved = "head-moved";

    public const string HeadUnheld = "head-unheld";

    public static SlotVerdict Judge(SlotJudgementFacts facts)
    {
        if (Holding(facts) is { } holding) return new(1, Held, $"{holding} holds it");
        if (!facts.Listed) return new(2, Unlisted, "git does not list it at its folder");
        if (facts.BoundElsewhere) return new(2, Bound, "git binds it to another checkout");
        if (facts.Lock == SlotLocks.Other) return new(2, Locked, "it is locked for another reason");
        if (facts.LinkUnderTrees) return new(3, Linked, "a link lies under the trees on the way to it");
        if (facts.PathHidden) return new(3, Linked, "a path is hidden from git's status");
        if (facts.Unread) return new(3, Unread, "something in it could not be read");
        if (facts.LinksNotRecorded > 0) return new(3, Linked, "a link no placement record holds");
        if (facts.RepositoryInside) return new(3, RepositoryInside, "a repository lies inside it");
        if (facts.OperationHalfDone) return new(4, HalfDone, "a git operation is half done in it");
        if (facts.TrackedChange) return new(5, Changed, "it holds a tracked change");
        if (facts.UntrackedPath) return new(5, Changed, "it holds an untracked path");
        if (facts.LineOwnIgnored) return new(5, LinesOwn, "it holds an ignored path that is its line's own");
        if (facts.Expected.Length == 0 || !string.Equals(facts.Head, facts.Expected, StringComparison.Ordinal))
        {
            return new(6, HeadMoved, "its HEAD changed since its line let it go");
        }

        return facts.RefHolds ? SlotVerdict.Passed : new(6, HeadUnheld, "no ref holds its HEAD");
    }

    private static string? Holding(SlotJudgementFacts facts) =>
        facts.HeldByLine ? "a line it serves"
        : facts.Claimed ? "a claim in flight"
        : facts.Visited ? "a visit"
        : facts.LandingUnderWay ? "a landing under way"
        : facts.StandingMove ? "a standing move"
        : facts.LandDue ? "a land node due"
        : facts.StartDue ? "a start due"
        : facts.HelperWait ? "a helper wait"
        : null;
}
