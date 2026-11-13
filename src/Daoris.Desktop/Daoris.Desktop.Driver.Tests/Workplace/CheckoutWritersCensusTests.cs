using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The census of every git write the driver, its host and the modules make: each file that starts a git
/// which changes a HEAD, an index, a working tree, a branch or git's record of its trees is named here with what it writes and where, by
/// each call's verb and the folder it runs in. A writer of a repository's checkout (its HEAD, index, working tree or the branch it has out)
/// holds the repository alone through <c>TreeLock.TryWriting</c>, from its fresh judgement through its write and its read after it; every
/// other write is in a tree of Daoris's own, or moves a ref no checkout has out. A new call is a failing line until it is named.
/// </summary>
/// <remarks>
/// A conversation's agent or a driven session's agent writing files itself starts no git of Daoris's and cannot take the lock: it is named
/// in <see cref="Fenced"/> with how it is fenced instead. Read from the sources by name wherever they lie, as
/// <see cref="SettledReadersCensusTests"/> is.
/// </remarks>
[Trait(Category.Name, Category.Census)]
public sealed partial class CheckoutWritersCensusTests
{
    private const string Checkout = "checkout";
    private const string OwnTree = "own tree";
    private const string Refs = "refs";

    /// <summary>Each file's git writes as <c>verb@folder</c>, what kind of write they are, and how it is made safe.</summary>
    private static readonly Dictionary<string, (string Kind, string How, string[] Calls)> Census = new(StringComparer.Ordinal)
    {
        // The checkout's writers: each holds the repository alone from its fresh judgement through its write and its read after it.
        ["LinePulls.cs"] = (Checkout, "a line's pull (D109): `merge --ff-only` in the checkout that has the line out, read clean and on it under the hold; elsewhere the ref from the commit judged", ["merge@root", "update-ref@root"]),
        ["IntegrationPromotes.cs"] = (Checkout, "a promotion's move, background or a merge rule's press: `merge --ff-only` in the checkout that has the line out, read clean under the hold; elsewhere one verified `update-ref`", ["merge@checkout.Path", "update-ref@ask.Root"]),
        ["ActSteps.cs"] = (Checkout, "a git act's effect steps (GITAPP2a, D204): one `update-ref --stdin` transaction, and an index's entries replaced under git's own `index.lock` from a copy `update-index` builds, each git held in the act's job; the act's open records reconciled first under the hold", ["update-index@launch.Root", "update-ref@launch.Root"]),
        ["StashDropSteps.cs"] = (Checkout, "a stash Drop (GITAPP2, D204): one `stash drop` of the shared `refs/stash`'s entry by the commit asked, found as it runs and read back from git's own words, and one `stash store` of the commit git dropped where the place moved under it, each in the act's job with its repository's hooks never skipped", ["stash@launch.Root"]),
        ["ActWorkSteps.cs"] = (Checkout, "a switch, a stash Save or a stash Apply (GITAPP2k, GITAPP2l, D204): one `switch --no-guess` of the checkout to a local branch, never forced, one `stash push` that writes the shared `refs/stash`, or one `stash apply <id>` that writes the tracked files and leaves the stash, each in the act's job with its repository's hooks never skipped", ["switch@launch.Root", "stash@?", "stash@launch.Root"]),
        ["ActCommits.cs"] = (Checkout,"a commit or an amend (GITAPP2i, D204): one `commit` of the act journal, its repository's hooks run and never skipped, in the act's job; the old tip kept first by a ref step, and the index only read on a private copy (`write-tree`)", ["commit@?"]),

        // Trees of Daoris's own, under the trees home: no checkout of the person's.
        ["BacklogMerge.cs"] = (OwnTree, "a backlog's union, staged in the replay's own tree", ["checkout@tree", "update-index@tree"]),
        ["BacklogHeld.cs"] = (OwnTree, "a made backlog's rows the rules take out, staged and, in a replay, committed in its own tree or the line's integration tree", ["checkout@place", "commit@place", "update-index@place"]),
        ["UnionOnce.cs"] = (OwnTree, "a heading union kept twice, kept once and committed in the replay's own tree", ["add@tree", "commit@tree"]),
        ["GeneratedWriters.cs"] = (OwnTree, "generated folders written again and committed in the replay's own tree", ["add@tree", "commit@tree"]),
        ["UnionJoins.cs"] = (OwnTree, "what a union joined, set apart and committed in the replay's own tree", ["add@tree", "commit@tree"]),
        ["VerifyExecution.cs"] =(OwnTree, "a heavy run's own detached tree, made, locked and checked out at the commit sent; git's record of it in the checkout", ["checkout@tree", "worktree@send.Checkout"]),
        ["IntegrationJudging.cs"] = (OwnTree, "the line's integration tree, put at the commit judged", ["checkout@ask.Tree"]),
        ["IntegrationReplays.cs"] = (OwnTree, "the line's integration tree: made and removed through git's record of it, merged and committed in, put back at a commit by reset, clean and checkout", ["checkout@?", "clean@?", "commit@tree", "merge@tree", "reset@?", "worktree@ask.Root"]),
        ["IntegrationResolves.cs"] = (OwnTree, "a conflict resolved in the line's integration tree", ["add@tree", "checkout@tree", "rm@tree"]),
        ["OpinionTree.cs"] = (OwnTree, "a second opinion's own copy, detached at the tip judged", ["checkout@?"]),
        ["CreateHistory.cs"] = (OwnTree, "a department's new folder, initialised and its head pointed at its line before its one empty commit; no checkout of a repository holds it", ["symbolic-ref@?"]),
        ["UntrackedPatches.cs"] = (OwnTree, "Review's read of a session's own tree: its untracked files added as intents to a private index (GIT_INDEX_FILE) beside the tree's own, never that index, removed with its lock after", ["add@_root"]),

        // Refs no checkout has out, each from the tip judged or refused by git itself, and git's record of the trees.
        ["BranchReplays.cs"] = (Refs, "a branch replayed in its own tree or one made for it (D109 §2), moved from the tip judged while no checkout has it out", ["rebase@tree", "switch@tree", "update-ref@root"]),
        ["ReplaySettles.cs"] = (OwnTree, "a work replayed and its stops settled in a tree made for it under the trees home and removed after; it moves no branch", ["add@tree", "checkout@tree", "rebase@tree", "rm@tree", "switch@tree", "worktree@root"]),
        ["ResolveTree.cs"] = (Refs, "a session's tree a hand-off reserves, the replay stopped there on a detached head so its branch never moves, put back on its branch at its tip with the resolver's edits on the node's edits ref", ["add@hand.Tree", "checkout@hand.Tree", "clean@hand.Tree", "rebase@?", "rebase@hand.Tree", "reset@hand.Tree", "switch@hand.Tree", "symbolic-ref@hand.Tree", "update-ref@hand.Tree"]),
        ["TreeUpdates.cs"] = (Refs, "a session's branch brought up to date in its own tree, moved from the tip judged while that tree is detached", ["rebase@tree", "switch@full", "switch@tree", "update-ref@root"]),
        ["TreeLandSyncs.cs"] = (Refs, "a session's branch following what landed, moved from the tip judged while its own tree is detached; a delivery's is TreeDeliveries' transaction instead", ["switch@full", "update-ref@sync.Root"]),
        ["TreeBranches.cs"] = (Refs,"a landing's new branch, and its advance from the recorded tip of a branch checked out in no tree; a delivery's are TreeDeliveries' transaction instead; its merge rule writes the checkout only through IntegrationPromotes' effect and holds nothing itself", ["branch@root", "update-ref@root"]),
        ["TrainBranch.cs"] = (Refs, "an ask's train branch made at the line's commit where the repository has none; an existing one is left where it is", ["branch@root"]),
        ["TreeSquashes.cs"] = (Refs, "a squash's advance, from the recorded tip of a branch checked out in no tree; a delivery's is TreeDeliveries' transaction instead", ["update-ref@root"]),
        ["RecoveryDropping.cs"] = (Refs, "a recovery ref dropped verified at its commit", ["update-ref@root"]),
        ["RefTransaction.cs"] = (Refs, "one typed transaction per ref act (GITTXN1, GITTXN2), each ref's expectation inside it, for refs no checkout has out: the content holds' deletes, drops and put-backs", ["update-ref@?", "update-ref@root"]),
        ["PartnerActRunner.cs"] = (Refs, "a discarded piece's ref dropped verified at the commit it was frozen as, then its copy removed", ["update-ref@root"]),
        ["PieceKeeper.cs"] = (Refs, "a kept piece: read and staged in a private index (GIT_INDEX_FILE), its ref made only where absent; its copy checked out", ["checkout@copy", "read-tree@root", "update-index@root", "update-ref@root"]),
        ["OnlyHereProof.cs"] = (Refs, "git's record of gone trees pruned before its branch is deleted through the journal's act (JournaledDeletes)", ["worktree@root"]),
        ["TreeOpenings.cs"] = (Refs, "an opening undone: its own tree removed and its new branch deleted", ["branch@root", "worktree@root"]),
        ["TreeRemovals.cs"] = (Refs, "a session's tree removed; its branch deleted through the journal's act (JournaledDeletes)", ["worktree@?"]),
        ["TreeReturns.cs"] = (Refs, "a session's tree made again on its branch, through git's record of the trees", ["worktree@root"]),
        ["TreeSetAside.cs"] = (Refs, "a tree set aside, through git's record of the trees", ["worktree@root"]),
        ["TreeFilesStage.cs"] = (Refs, "an unworked tree's files git tracks, which only Daoris's own install or hook changed, put back from its commit before anything is handed it", ["checkout@tree"]),
        ["TreePoolTakes.cs"] = (OwnTree, "a kept slot's take or return: the slot switched in place to the line's branch, never forced and with the repository's hooks held, in the claim's job (R4-F7)", ["switch@slot.Path"]),
        ["PoolUndo.cs"] = (OwnTree, "a take or a return put back where its claim found the kept slot: switched onto the branch it stood on, or detached at the commit, never forced, no ignored file overwritten and the hooks held, in the claim's job; a take's own branch is deleted through the journal's act", ["switch@slot.Path"]),
        ["SlotStanding.cs"] = (OwnTree, "a kept slot settled stuck, detached where it stands, which writes no file, in the claim's job", ["switch@Slot.Path"]),
        ["TreePoolFinishes.cs"] = (OwnTree, "a finished line's kept slot detached at the line's tip in place, never forced and with the repository's hooks held, in the claim's job", ["switch@slot.Path"]),
        ["PoolFinishRecoveries.cs"] = (OwnTree, "a dead run's finish settled: the kept slot detached at the line's tip again, in the takeover's job", ["switch@slot.Path"]),
        ["TreePoolRetires.cs"] = (Refs, "a retired slot's registration unlocked in the claim's job, and git's record of its vanished folder dropped", ["worktree@site.Root"]),
        ["PoolLocks.cs"] = (Refs, "a kept slot's lock changed between the pool's free and claimed reasons, in the claim's job", ["worktree@slot"]),
        ["TreeSweeps.cs"] = (Refs, "a pressed tree removed, through git's record of the trees", ["worktree@root"]),
        ["TreeStrays.cs"] = (Refs, "a stray tree's git lock let go and taken again", ["worktree@root"]),
        ["SessionLocks.cs"] = (Refs, "a session's tree locked and unlocked in git's record of the trees", ["worktree@root", "worktree@tree"]),
        ["TreeOpening.cs"] = (Refs,"a session's tree opened on a new branch, through git's record of the trees", ["worktree@root"]),
        ["StageActs.cs"] = (Refs, "stage and unstage (GITAPP2h, D204): what an act leaves is found by `add`, `reset`, `rm --cached` or `apply --cached` on a private copy of the index (GIT_INDEX_FILE), never the index, which only the act's index step replaces, in `ActRuns`' hold under git's own `index.lock`", ["add@?", "apply@?", "reset@?", "rm@?"]),
        ["ResolveActs.cs"] = (Refs, "mark resolved (GITAPP2j, D204): what the index would hold once the file is added is found by `add` on a private copy of the index (GIT_INDEX_FILE), never the index, which only the act's index step replaces, in `ActRuns`' hold under git's own `index.lock`; a resolve's file step is `ActFiles`, in the same hold", ["add@root"]),
        ["PassLeftovers.cs"] = (Refs,"a pass's tree's git lock let go and taken again", ["worktree@root"]),
    };

    /// <summary>Each writer of a checkout, and the file whose code takes the repository's lock alone around it.</summary>
    private static readonly Dictionary<string, string> HeldIn = new(StringComparer.Ordinal)
    {
        ["LinePulls.cs"] = "TreeSync.cs",
        ["IntegrationPromotes.cs"] = "IntegrationPromotes.cs",
        ["ActSteps.cs"] = "ActRuns.cs",
        ["ActCommits.cs"] = "ActRuns.cs",
        ["ActWorkSteps.cs"] = "ActRuns.cs",
        ["StashDropSteps.cs"] = "ActRuns.cs",
    };

    /// <summary>
    /// Files that hold a checkout alone around a write of refs no checkout has out, through <c>RefTransaction</c>, so no act of the person's on
    /// the same refs runs beside it: what each writes.
    /// </summary>
    private static readonly Dictionary<string, string> RefsHeldAlone = new(StringComparer.Ordinal)
    {
        ["TreeDeliveries.cs"] = "a delivery's landing (LANDNODE1b2): its branch made, moved on from the tip judged, or its tree's own verified at the tip or replayed in place while that tree is detached, one transaction in its phase",
    };

    /// <summary>Files that hold a checkout alone to write no git, only a record every start reads inside its hold: what each writes.</summary>
    private static readonly Dictionary<string, string> ExcludesStarts = new(StringComparer.Ordinal)
    {
        ["TreeReserving.cs"] = "a hand-off's row (`TreeHandOffs`), written once the holders are read; the tree is prepared after the hold is let go",
    };

    /// <summary>Writers of a checkout that start no git of Daoris's and so take no lock: how each is fenced instead.</summary>
    private static readonly Dictionary<string, string> Fenced = new(StringComparer.Ordinal)
    {
        ["ChatStarts.cs"] = "a conversation's agent in the checkout writes its files itself: its start holds the repository shared until its record is open and the ledger names the checkout, so no writer moves the checkout under the commit it begins from; after that each writer reads the checkout clean under its own hold, immediately before, and moves it only by `merge --ff-only`, which git refuses over the agent's change, and the pull and the transport's promotion wait while the ledger holds the session there",
        ["GoingOnRun.cs"] = "a conversation going on in the checkout: its start holds the repository shared until its record holds the checkout again, then as a new conversation's",
        ["GrowTree.cs"] = "a driven session in the checkout: its start holds the repository shared until its record is open, then as a conversation's",
        ["OpenRecord.cs"] = "a carry-on's open, which takes the hold again after its resume let it go, until its record is open",
    };

    /// <summary>
    /// The doors that start a checkout's writer with a service to ask, each handing it the ledger's sessions, so the write waits for a
    /// session working in the checkout. A merge rule's press reaches the promotion through the run's <c>land</c>, whose performer hands
    /// it the same; the Accept doors land nothing themselves.</summary>
    private static readonly Dictionary<string, string> Beside = new(StringComparer.Ordinal)
    {
        ["TreesSyncVerb.cs"] = "`daoris tree sync --yes`'s pull where no Daoris runs",
        ["ReviewLandingWorld.cs"] = "a run's `land` performed, a merge rule's move",
        ["TreesTidyDesk.cs"] = "the window's Bring up to date, and the terminal's and Ask Daoris's press of it",
        ["WatchNodes.cs"] = "the integration transport's promotions",
        ["GitRoutes.cs"] = "the Changes tab's stage, unstage and Undo",
    };

    /// <summary>The Accept doors, which land nothing themselves: no checkout's writer starts from them.</summary>
    private static readonly string[] AcceptDoors = ["TreesLandDesk.cs", "ChatAccepts.cs", "EntriesRoutes.cs"];

    /// <summary>
    /// Writers of a checkout outside Daoris's desktop, which no lock of its reaches, by the source that writes where Daoris has one: what
    /// covers each. Each writer above reads the checkout again under its hold, immediately before it moves it, and moves it only by
    /// <c>merge --ff-only</c> or a ref from the commit judged, which git refuses over a change of theirs.
    /// </summary>
    private static readonly Dictionary<string, (string? Source, string How)> Outside = new(StringComparer.Ordinal)
    {
        ["the person's editor, terminal and git"] = (null, "their own writes, at any moment: covered only by each writer's fresh reads under its hold"),
        ["the CLI's doctrine sync"] = ("src/Daoris.Cli/src/cli/sync.ts", "`daoris sync` writes doctrine files into a checkout, offline and with no service: covered as the person's own writes are"),
        ["the CLI's set-up"] = ("src/Daoris.Cli/src/cli/init.ts", "`daoris init` writes a repository's first doctrine and records: covered as the person's own writes are"),
    };

    /// <summary>A git argument list opening with a writing verb, after any <c>-c key=value</c> pairs or a spread of them.</summary>
    [GeneratedRegex(
        """(?:(?<![\w\])])\[|new\s*(?:string)?\[\]\s*\{)\s*(?:"-c"\s*,\s*"[^"]*"\s*,\s*|\.\.\s*\w+\s*,\s*)*"(?<verb>merge|pull|checkout|switch|reset|rebase|cherry-pick|revert|commit|stash|am|apply|restore|rm|mv|add|read-tree|checkout-index|update-index|clean|update-ref|branch|worktree|symbolic-ref)"(?!\]\s*=)\s*(?:,\s*(?<next>\.\.|"[^"]*"))?""")]
    private static partial Regex Write();

    /// <summary>A call that starts git: a file holding none holds word lists, not git's arguments.</summary>
    [GeneratedRegex(@"\b\w*Git\w*Async\(|\bAskAsync\(")]
    private static partial Regex StartsGit();

    /// <summary>The runner call a list is handed to: its first argument is the folder git runs in.</summary>
    [GeneratedRegex(@"Async\(\s*(?<cwd>[^,()]+?)\s*,")]
    private static partial Regex Runner();

    /// <summary>The verbs that read when their next word says so: a list of branches, of trees, or the name HEAD stands on.</summary>
    private static readonly HashSet<(string Verb, string Next)> Reads =
    [
        ("branch", "\"--list\""), ("worktree", "\"list\""), ("symbolic-ref", "\"--short\""), ("symbolic-ref", "\"-q\""),
    ];

    private static IEnumerable<(string File, string Source)> Sources() => SayDoorSources.All.Select(each => (each.File, each.Text));

    /// <summary>The source with its comments blanked, so a list in a comment or a doc comment is not read as a call.</summary>
    private static string Code(string source) => Regex.Replace(source, @"//[^\n]*", match => new string(' ', match.Length));

    /// <summary>Each file's git writes, as <c>verb@folder</c>: the folder is the runner's first argument, or <c>?</c> where none is in the statement.</summary>
    internal static SortedDictionary<string, SortedSet<string>> Found(IEnumerable<(string File, string Source)> sources)
    {
        var found = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        foreach (var (file, source) in sources)
        {
            if (!StartsGit().IsMatch(source)) continue;
            var code = Code(source);
            if (!StartsGit().IsMatch(code)) continue;
            foreach (Match call in Write().Matches(code))
            {
                var verb = call.Groups["verb"].Value;
                var next = call.Groups["next"].Value;
                if (next == ".." || Reads.Contains((verb, next))) continue;
                var start = Math.Max(code.LastIndexOfAny([';', '{', '}'], call.Index) + 1, 0);
                var statement = code[start..call.Index];
                var runner = Runner().Matches(statement).LastOrDefault();
                var cwd = runner is null ? "?" : runner.Groups["cwd"].Value.Trim();
                if (!found.TryGetValue(file, out var calls)) found[file] = calls = new SortedSet<string>(StringComparer.Ordinal);
                calls.Add($"{verb}@{cwd}");
            }
        }

        return found;
    }

    [Fact]
    public void Every_git_write_is_named_with_what_it_writes_and_where()
    {
        var found = Found(Sources());
        var unnamed = found.Where(each => !Census.TryGetValue(each.Key, out var named) || !named.Calls.ToHashSet(StringComparer.Ordinal).SetEquals(each.Value))
            .Select(each => $"[\"{each.Key}\"] = ({string.Join(", ", each.Value.Select(call => $"\"{call}\""))})")
            .ToList();
        Assert.True(unnamed.Count == 0, "git writes not named as found:\n" + string.Join("\n", unnamed));
        Assert.Empty(Census.Keys.Except(found.Keys));
        Assert.All(Census, each => Assert.False(string.IsNullOrWhiteSpace(each.Value.How), each.Key));
    }

    /// <summary>🔴 Each writer of a checkout holds its repository alone, taken in the file named (its effect needing the hold where another takes it), and no other file takes that hold, so no door reaching a writer through another takes it twice (the legacy merge, through the promotion). Each start in the checkout holds it shared, whether or not the session has a tree of its own.</summary>
    [Fact]
    public void Each_writer_of_a_checkout_holds_its_repository_alone_once_and_each_start_holds_it_shared()
    {
        var sources = Sources().GroupBy(each => each.File).ToDictionary(group => group.Key, group => Code(string.Join("\n", group.Select(each => each.Source))));
        var writers = Census.Where(each => each.Value.Kind == Checkout).Select(each => each.Key).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(writers.Order(StringComparer.Ordinal), HeldIn.Keys.Order(StringComparer.Ordinal));
        foreach (var (writer, holder) in HeldIn)
        {
            Assert.Contains("TreeLock.TryWriting(", sources[holder], StringComparison.Ordinal);
            if (holder != writer) Assert.Matches(@"\bTreeLock\s+\w+\s*,", sources[writer]);
        }

        var taking = sources.Where(each => each.Value.Contains("TreeLock.TryWriting(", StringComparison.Ordinal)).Select(each => each.Key);
        Assert.Equal(
            HeldIn.Values.Concat(RefsHeldAlone.Keys).Concat(ExcludesStarts.Keys).Distinct().Order(StringComparer.Ordinal), taking.Order(StringComparer.Ordinal));
        Assert.All(ExcludesStarts.Keys, holder => Assert.DoesNotContain("GitAsync(", sources[holder], StringComparison.Ordinal));
        foreach (var (holder, what) in RefsHeldAlone)
        {
            Assert.False(string.IsNullOrWhiteSpace(what), holder);
            Assert.Contains("new RefTransaction()", sources[holder], StringComparison.Ordinal);
            Assert.DoesNotContain(holder, Census.Keys);
        }

        foreach (var (start, how) in Fenced)
        {
            Assert.False(string.IsNullOrWhiteSpace(how));
            Assert.Contains("TreeLock.TryStarting(", sources[start], StringComparison.Ordinal);
            Assert.DoesNotMatch(@"\b(isolated|own)\b[^;?]*\?\s*TreeLock\.TryStarting\(", sources[start]);
        }

        foreach (var (door, what) in Beside)
        {
            Assert.True(sources[door].Contains("BranchTidying.BesideAsync(", StringComparison.Ordinal), $"{door} ({what}) hands the writer no ledger");
        }

        foreach (var door in AcceptDoors)
        {
            Assert.False(sources[door].Contains("BranchTidying.BesideAsync(", StringComparison.Ordinal), $"{door} starts a writer: an Accept lands by its run's `land`");
        }

        var pressing = sources.Where(each => Regex.IsMatch(each.Value, @"\.SyncAsync\([^;]*inUseNow:")).Select(each => each.Key);
        Assert.Empty(pressing.Except(Beside.Keys));

        foreach (var (writer, (source, how)) in Outside)
        {
            Assert.False(string.IsNullOrWhiteSpace(how), writer);
            if (source is not null) Assert.True(File.Exists(Path.Combine(WorkspaceRoot.Folder, source)), $"{writer}: {source} is no source");
        }
    }

    /// <summary>What the scan reads as a git write, and what it passes over: a comment, an initializer's key, a list pattern, a read.</summary>
    [Theory]
    [InlineData("""await WorkingTree.GitAsync(root, ["merge", "--ff-only", tip], ct);""", "merge@root")]
    [InlineData("""await WorkingTree.GitAsync(\n    tree, ["-c", "rebase.autoStash=false", "rebase", "--onto", onto], ct);""", "rebase@tree")]
    [InlineData("""var arguments = new[] { "worktree", "remove", full }; await WorkingTree.GitAsync(root, arguments, ct);""", "worktree@?")]
    [InlineData("""// await WorkingTree.GitAsync(root, ["reset", "--hard"], ct);""", "")]
    [InlineData("""var said = new JsonObject { ["commit"] = tip }; await WorkingTree.GitAsync(root, ["status"], ct);""", "")]
    [InlineData("""switch (args) { case ["clean", ..]: break; } await WorkingTree.GitAsync(root, ["status"], ct);""", "")]
    [InlineData("""await WorkingTree.GitAsync(root, ["branch", "--list"], ct);""", "")]
    public void The_scan_reads_a_git_write_by_its_verb_and_folder_and_passes_over_what_is_not_one(string source, string call)
    {
        var found = Found([("Some.cs", source.Replace("\\n", "\n", StringComparison.Ordinal))]);
        Assert.Equal(call, found.TryGetValue("Some.cs", out var calls) ? string.Join(" ", calls) : "");
    }
}
