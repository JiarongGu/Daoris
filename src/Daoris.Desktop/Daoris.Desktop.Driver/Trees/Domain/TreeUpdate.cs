namespace Daoris.Driver;

/// <summary>
/// What bringing one session's tree up to date did (REVIEWBASE2-i, D164's REVIEWBASE2 note). A refusal is an answer, not an error:
/// <paramref name="Done"/> false with the sentence that says why, as a discarded tree's is, because the person's next move differs
/// for each.
/// </summary>
/// <param name="Moved">Where the tree's HEAD went, where it moved; null where nothing moved.</param>
public sealed record TreeUpdate(bool Done, string Message, TreeMoved? Moved = null);

/// <summary>Where a press moved a tree (REVIEWBASE2-i): its HEAD's commit before and after, onto the line named.</summary>
/// <param name="Branch">The branch moved with it, the session's or one of its own (TREEFOLLOW1); null for a detached tree.</param>
/// <param name="Commits">The tree's own commits beyond the line now.</param>
public sealed record TreeMoved(string Line, string From, string To, string? Branch, int Commits);

/// <summary>Why a tree is not brought up to date, in the order the press asks (REVIEWBASE2-i): the first found is the answer.</summary>
public enum TreeRefusal
{
    /// <summary>Not a tree this home opened: nothing else of it is read.</summary>
    NotOurs,

    /// <summary>Something runs in it: a conversation's turn taking or queued, or a driven session working (point 3).</summary>
    Running,

    /// <summary>It holds uncommitted work, which a replay does not carry.</summary>
    Dirty,

    /// <summary>No line is set for its repository and git names none.</summary>
    NoLine,

    /// <summary>The repository's trees are held: a session starting in one, or another replay (LEFT2).</summary>
    Held,

    /// <summary>The line could not be fetched (point 2): the press refuses rather than judge against the last look's.</summary>
    NotFetched,

    /// <summary>It already holds the line's latest commit.</summary>
    UpToDate,

    /// <summary>Its branch is on its remote: replaying it would need a force push, which is never Daoris's.</summary>
    OnRemote,
}

/// <summary>
/// What a press knows of a tree at each of its steps (REVIEWBASE2-i): each fact is found false until it is asked, so one record
/// is asked again after each step, and <see cref="First"/> is the one place the refusals' order is written.
/// </summary>
public sealed record TreeFacts(string? Repository = null, string? Line = null, string? Branch = null)
{
    public bool NotOurs { get; init; }

    public bool Running { get; init; }

    /// <summary>The paths <c>git status --porcelain</c> lists.</summary>
    public int Uncommitted { get; init; }

    public bool NoLine { get; init; }

    public bool Held { get; init; }

    /// <summary>Why the line was not fetched, in git's words; null where it was, or none was tried yet.</summary>
    public string? NotFetched { get; init; }

    public bool UpToDate { get; init; }

    public bool OnRemote { get; init; }

    /// <summary>
    /// The first refusal found, in the order the press asks: a tree not this home's first, since nothing else of it is read; what
    /// runs in it, its uncommitted work and its line, all read on the machine; the hold and the fetch; then whether it already holds
    /// the line's latest, which only a fetched line can say; a branch on its remote last, as WSR6's judgement has it. Null for none.
    /// </summary>
    public TreeRefusal? First() =>
        NotOurs ? TreeRefusal.NotOurs
        : Running ? TreeRefusal.Running
        : Uncommitted > 0 ? TreeRefusal.Dirty
        : NoLine ? TreeRefusal.NoLine
        : Held ? TreeRefusal.Held
        : NotFetched is not null ? TreeRefusal.NotFetched
        : UpToDate ? TreeRefusal.UpToDate
        : OnRemote ? TreeRefusal.OnRemote
        : null;
}

/// <summary>
/// A tree's press in words (REVIEWBASE2-i), beside <c>SyncWords</c>'s rows: its refusals, and a detached tree's replay. A tree on
/// a branch is replayed by WSR6's own replay, which says what it did in its own words.
/// </summary>
public static class TreeUpdateWords
{
    /// <summary>One refusal, naming what it is about from <paramref name="facts"/>.</summary>
    public static string Refused(TreeRefusal why, TreeFacts facts) => why switch
    {
        TreeRefusal.NotOurs => "this is no tree Daoris opened on this machine, so bringing it up to date is not Daoris's to do",
        TreeRefusal.Running => "something runs in its tree right now, a turn taking or one queued, so it was left as it was: bring it up "
                               + "to date once that is done",
        TreeRefusal.Dirty => $"its tree has uncommitted work ({facts.Uncommitted} path(s)), which a replay does not carry, so it was left "
                             + "as it was: commit it, then bring it up to date",
        TreeRefusal.NoLine => $"no line is set for `{facts.Repository}` and git names none, so there is no line to bring it up to",
        TreeRefusal.Held => $"`{facts.Repository}`'s trees are held right now, by a session starting in one or by another replay, so "
                            + "it was left as it was: try again in a moment",
        TreeRefusal.NotFetched => $"could not fetch `{facts.Line}`: {facts.NotFetched?.Trim().TrimEnd('.')}. Nothing was moved.",
        TreeRefusal.UpToDate => $"already based on `{facts.Line}`",
        _ => $"`{facts.Branch}` is on its remote: replaying it would need a force push, which is never Daoris's, so it was left as it was",
    };

    /// <summary>
    /// What WSR6's judgement left of a tree on a branch, where it is none of the refusals above: a chain's step whose parent's work
    /// has not reached the line, or git unable to say.
    /// </summary>
    public static string Left(TreeFacts facts, string kind, string? grewFrom, string? detail) => kind == RebaseKind.Waits
        ? $"`{facts.Branch}` " + (grewFrom is { } parent ? $"grew from `{parent}`" : "grew from a commit beyond the line")
          + ", whose work has not reached the line yet: replaying only its own would lose the work it builds on, so it was left as it was"
        : $"git could not tell: {detail}";

    /// <summary>A detached tree replayed in place (point 4): <paramref name="kept"/> of its <paramref name="had"/> commits of its own.</summary>
    public static string Replayed(string line, int kept, int had) => had == 0
        ? $"moved the tree to `{line}`: it held nothing of its own beyond the line."
        : $"replayed the tree onto `{line}`: {kept} commit(s) of its own"
          + (kept < had ? $" ({had - kept} became empty, their changes already on the line)." : ".");

    /// <summary>A detached tree's replay git would not make: a conflict names its files. <paramref name="back"/>: HEAD is where it was.</summary>
    public static string NotReplayed(string line, string words, bool back) => $"the tree would not replay onto `{line}`: {words} " + (back
        ? "The replay was aborted, and the tree is as it was."
        : "Git could not put the tree back where it was: look at the tree before anything else (`git rebase --abort`, then `git status`).");

    /// <summary>A detached tree's replay the content proof refused: the files it changed beyond what the line brought.</summary>
    public static string NotKept(string beyond, bool back) => $"the tree's replay changed what the line did not bring ({beyond}), so it "
        + (back
            ? "was not kept: the tree is as it was."
            : "was not kept, and git could not put the tree back where it was: look at the tree before anything else (`git status`).");

    /// <summary>A detached tree that moved between the press's look and its replay.</summary>
    public const string MovedSince = "the tree moved while the press looked at it, so it was left as it was: press again";

    /// <summary>
    /// Said after a replay in a repository with no <c>origin</c>: nothing to fetch is no failed fetch, since local operation needs no
    /// remote, so the line this checkout has is the latest there is.
    /// </summary>
    public const string NoOrigin = " There is no `origin` here to fetch from, so the line is this checkout's own.";
}
