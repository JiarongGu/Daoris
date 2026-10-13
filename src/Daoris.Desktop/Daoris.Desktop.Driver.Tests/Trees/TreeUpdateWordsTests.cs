using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// REVIEWBASE2-i (D164's REVIEWBASE2 note): one session's tree brought up to date at the person's press. Pure: the order its
/// refusals are asked in, and what each answer says, from the facts the press read (<see cref="TreeFacts"/>).
/// </summary>
public sealed class TreeUpdateWordsTests
{
    private static readonly TreeFacts Known = new("engine", "main", "daoris/aurora-1a2b");

    /// <summary>A fact not yet asked is found false, so a press with nothing found refuses nothing.</summary>
    [Fact]
    public void Nothing_found_is_no_refusal()
    {
        Assert.Null(Known.First());
        Assert.Null(new TreeFacts().First());
    }

    /// <summary>
    /// The order a press asks in (point 3): a tree not this home's first, since nothing else of it is read; then what runs in it, its
    /// uncommitted work and its line, all on the machine; then the hold and the fetch; and only then the line's latest, so "already
    /// based" is never said of a line that was not fetched; a branch on its remote last, as WSR6's judgement has it.
    /// </summary>
    [Fact]
    public void Each_refusal_is_asked_in_its_order()
    {
        var all = Known with
        {
            NotOurs = true, Running = true, Uncommitted = 2, NoLine = true, Held = true, NotFetched = "fatal: gone", UpToDate = true,
            OnRemote = true,
        };
        TreeRefusal[] order =
        [
            TreeRefusal.NotOurs, TreeRefusal.Running, TreeRefusal.Dirty, TreeRefusal.NoLine, TreeRefusal.Held, TreeRefusal.NotFetched,
            TreeRefusal.UpToDate, TreeRefusal.OnRemote,
        ];

        var facts = all;
        foreach (var expected in order)
        {
            Assert.Equal(expected, facts.First());
            facts = expected switch
            {
                TreeRefusal.NotOurs => facts with { NotOurs = false },
                TreeRefusal.Running => facts with { Running = false },
                TreeRefusal.Dirty => facts with { Uncommitted = 0 },
                TreeRefusal.NoLine => facts with { NoLine = false },
                TreeRefusal.Held => facts with { Held = false },
                TreeRefusal.NotFetched => facts with { NotFetched = null },
                TreeRefusal.UpToDate => facts with { UpToDate = false },
                _ => facts with { OnRemote = false },
            };
        }

        Assert.Null(facts.First());
    }

    /// <summary>Each refusal in Daoris's English, naming what it is about; the two the parent decided word for word.</summary>
    [Fact]
    public void Each_refusal_is_said_in_words()
    {
        Assert.Equal("already based on `main`", TreeUpdateWords.Refused(TreeRefusal.UpToDate, Known));
        Assert.Equal(
            "could not fetch `main`: fatal: 'origin' does not appear to be a git repository. Nothing was moved.",
            TreeUpdateWords.Refused(TreeRefusal.NotFetched, Known with { NotFetched = "fatal: 'origin' does not appear to be a git repository" }));
        Assert.Contains("uncommitted work (3 path(s))", TreeUpdateWords.Refused(TreeRefusal.Dirty, Known with { Uncommitted = 3 }));
        Assert.Contains("`daoris/aurora-1a2b` is on its remote", TreeUpdateWords.Refused(TreeRefusal.OnRemote, Known));
        Assert.Contains("force push", TreeUpdateWords.Refused(TreeRefusal.OnRemote, Known));
        Assert.Contains("`engine`'s trees are held", TreeUpdateWords.Refused(TreeRefusal.Held, Known));
        Assert.Contains("no line is set for `engine`", TreeUpdateWords.Refused(TreeRefusal.NoLine, Known with { Line = null }));
        Assert.Contains("runs in its tree", TreeUpdateWords.Refused(TreeRefusal.Running, Known));
        Assert.Contains("no tree Daoris opened", TreeUpdateWords.Refused(TreeRefusal.NotOurs, new TreeFacts()));
        Assert.All(Enum.GetValues<TreeRefusal>(), why => Assert.False(string.IsNullOrWhiteSpace(TreeUpdateWords.Refused(why, Known))));
    }

    /// <summary>
    /// A repository with no <c>origin</c> has nothing to fetch, which is no failed fetch: local operation needs no remote, so the
    /// press goes on with the line this checkout has, and its answer says so.
    /// </summary>
    [Fact]
    public void A_repository_with_no_origin_is_said_to_be_brought_up_to_its_own_line()
    {
        Assert.Equal(
            "replayed the tree onto `main`: 1 commit(s) of its own. There is no `origin` here to fetch from, so the line is this checkout's own.",
            TreeUpdateWords.Replayed("main", 1, 1) + TreeUpdateWords.NoOrigin);
    }

    /// <summary>A detached tree's replay (point 4): what it took, and what a conflict or a failed proof left, naming the files.</summary>
    [Fact]
    public void A_detached_tree_s_replay_is_said_with_what_it_kept_or_why_not()
    {
        Assert.Equal("replayed the tree onto `main`: 2 commit(s) of its own.", TreeUpdateWords.Replayed("main", 2, 2));
        Assert.Equal(
            "replayed the tree onto `main`: 1 commit(s) of its own (1 became empty, their changes already on the line).",
            TreeUpdateWords.Replayed("main", 1, 2));
        Assert.Equal("moved the tree to `main`: it held nothing of its own beyond the line.", TreeUpdateWords.Replayed("main", 0, 0));

        var conflict = TreeUpdateWords.NotReplayed("main", "CONFLICT (content): Merge conflict in shared.txt.", back: true);
        Assert.Equal(
            "the tree would not replay onto `main`: CONFLICT (content): Merge conflict in shared.txt. The replay was aborted, and the tree is as it was.",
            conflict);
        Assert.Contains("look at the tree before anything else", TreeUpdateWords.NotReplayed("main", "error: boom.", back: false));

        Assert.Equal(
            "the tree's replay changed what the line did not bring (shared.txt), so it was not kept: the tree is as it was.",
            TreeUpdateWords.NotKept("shared.txt", back: true));
        Assert.Contains("look at the tree before anything else", TreeUpdateWords.NotKept("shared.txt", back: false));
    }

    /// <summary>What WSR6's judgement leaves of a tree on a branch, where it is not one of the refusals above.</summary>
    [Fact]
    public void A_branch_left_by_its_judgement_is_said()
    {
        Assert.Contains("grew from `daoris/parent-0001`, whose work has not reached the line yet",
            TreeUpdateWords.Left(Known, RebaseKind.Waits, "daoris/parent-0001", null));
        Assert.Equal("git could not tell: the branch is gone", TreeUpdateWords.Left(Known, RebaseKind.Unknown, null, "the branch is gone"));
    }
}
