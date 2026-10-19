using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// PARALLEL1a (D181 points 1-2): Ask Daoris's room is written from the facts the judges keep, never from a read of its own. A
/// repository whose line was not judged yet is said *not read yet*, as are the sessions, the history and the work; a ref moved
/// judges that repository's line again alone, and the next room has it; a room that said something not read yet is written again
/// as the facts land. FACTSPLIT1a: the work is judged per repository, a part each, merged as the room reads it. FACTSPLIT1b: the
/// sessions' trees alike, beside the look without them, which a ref never moves.
/// </summary>
/// <remarks>No process and no git, but where a test counts git's starts: the judges are stand-ins.</remarks>
public sealed class HelpFactsTests : IDisposable
{
    private static readonly TimeSpan Within = TimeSpan.FromSeconds(10);

    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-10-19T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

    private static readonly RepoView Engine = new("engine", Adopted: true, Root: "checkouts/engine");

    private static readonly RepoView Site = new("site", Adopted: true, Root: "checkouts/site");

    private static readonly RepoView Docs = new("docs", Adopted: true, Root: null);

    /// <summary>A part of the trees that judged none.</summary>
    private static readonly HelpTrees NoTrees = new(new Dictionary<string, TreeWork>(StringComparer.OrdinalIgnoreCase));

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-help-facts-" + Guid.NewGuid().ToString("N")[..8]);

    public HelpFactsTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static HelpMachineWhole Whole(params RepoView[] repositories) => new(new Snapshot([], repositories, []), [], []);

    /// <summary>The room as the module writes it from a reading, with nothing else of the machine.</summary>
    internal static HelpMachine Describe(HelpFactsRead read, DriverConfig config) => HelpRoom.Describe(
        config, read.Whole.Snapshot, read.Lines, read.Whole.Roster, _ => null, asks: 0, standing: read.Whole.Standing,
        sessions: read.Sessions, history: read.History, work: read.Work?.Units, workUnread: read.Work?.Unread,
        workRecovery: read.Work?.Recovery, notReadYet: read.NotReadYet);

    [Fact]
    public void A_repository_whose_line_was_not_judged_yet_is_said_not_read_yet_and_no_other()
    {
        var facts = MachineFactsSnapshot.Empty
            .With(HelpFacts.Machine, HelpFacts.Whole, new JudgedFact(Whole(Engine, Site, Docs), At))
            .With(HelpFacts.Lines, "engine", new JudgedFact(new HelpLineGuess("main"), At));

        var read = HelpFacts.Read(facts, DriverConfig.Empty)!;
        var room = HelpRoom.Render(Describe(read, DriverConfig.Empty));

        Assert.Equal(["site"], read.NotReadYet.Repositories);
        Assert.Contains("| `engine` | not driven | `main` (the checkout's own) |", room);
        Assert.Contains("| `site` | not driven | not read yet |", room);
        // No checkout here asks git nothing: what is set, or none, is all there is to say.
        Assert.Contains("| `docs` | not driven, no checkout here | none git can name |", room);
        Assert.Contains("- Not read yet:", room);
    }

    [Fact]
    public void A_line_the_person_set_needs_no_judgement()
    {
        var config = DriverConfig.Empty with { Lines = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["site"] = "trunk" } };
        var facts = MachineFactsSnapshot.Empty.With(HelpFacts.Machine, HelpFacts.Whole, new JudgedFact(Whole(Site), At));

        var read = HelpFacts.Read(facts, config)!;

        Assert.Empty(read.NotReadYet.Repositories);
        Assert.Equal(("trunk", LineSource.Repository), (read.Lines.Single().Branch, read.Lines.Single().Source));
    }

    /// <summary>
    /// PUSH1b1: the line as far as the checkout's guess was judged, which the room and the lines slice both read: what is set first,
    /// with no checkout what is set alone, else the judged guess, and none where it was not judged yet. It asks no git.
    /// </summary>
    [Fact]
    public void The_line_as_far_as_it_was_judged_is_what_is_set_then_the_judged_guess_and_none_where_none_was_judged()
    {
        var config = DriverConfig.Empty.WithLine("alpha", "trunk").WithWorkspaceLine("w2", "develop");

        Assert.Equal(new Line("trunk", LineSource.Repository), CanonicalLine.Judged(config, "alpha", "w1", "checkouts/alpha", guessed: null));
        Assert.Equal(new Line("develop", LineSource.Workspace), CanonicalLine.Judged(config, "beta", "w2", "checkouts/beta", guessed: null));
        Assert.Equal(new Line(null, LineSource.None), CanonicalLine.Judged(config, "gamma", "w1", root: null, guessed: null));
        Assert.Equal(new Line("main", LineSource.Checkout), CanonicalLine.Judged(config, "gamma", "w1", "checkouts/gamma", new HelpLineGuess("main")));
        Assert.Equal(new Line(null, LineSource.None), CanonicalLine.Judged(config, "gamma", "w1", "checkouts/gamma", new HelpLineGuess(null)));
        Assert.Null(CanonicalLine.Judged(config, "gamma", "w1", "checkouts/gamma", guessed: null));
    }

    [Fact]
    public void Nothing_judged_of_the_machine_is_no_reading_and_the_sessions_history_and_work_not_judged_are_said_not_read_yet()
    {
        Assert.Null(HelpFacts.Read(MachineFactsSnapshot.Empty, DriverConfig.Empty));

        var facts = MachineFactsSnapshot.Empty.With(HelpFacts.Machine, HelpFacts.Whole, new JudgedFact(Whole(), At));
        var room = HelpRoom.Render(Describe(HelpFacts.Read(facts, DriverConfig.Empty)!, DriverConfig.Empty));

        Assert.Contains("- This machine's sessions by the Sessions list's groups: not read yet.\n", room);
        Assert.Contains("- Finished history this machine keeps, by workspace: not read yet.\n", room);
        Assert.Contains("The milestones are not read yet, so do not say where any work stands", room);
    }

    [Fact]
    public void A_room_with_everything_judged_says_nothing_is_not_read_yet()
    {
        var facts = MachineFactsSnapshot.Empty
            .With(HelpFacts.Machine, HelpFacts.Whole, new JudgedFact(Whole(Engine), At))
            .With(HelpFacts.Lines, "engine", new JudgedFact(new HelpLineGuess("main"), At))
            .With(HelpFacts.Sessions, HelpFacts.Whole, new JudgedFact(new SessionLook([], [], []), At))
            .With(HelpFacts.Trees, "engine", new JudgedFact(NoTrees, At))
            .With(HelpFacts.Trees, HelpFacts.NoCheckout, new JudgedFact(NoTrees, At))
            .With(HelpFacts.History, HelpFacts.Whole, new JudgedFact(new List<HistoryReading>(), At))
            .With(HelpFacts.Work, "engine", new JudgedFact(new HelpWork([], [], []), At))
            .With(HelpFacts.Work, HelpFacts.NoCheckout, new JudgedFact(new HelpWork([], [], []), At));

        var read = HelpFacts.Read(facts, DriverConfig.Empty)!;
        var room = HelpRoom.Render(Describe(read, DriverConfig.Empty));

        Assert.False(read.NotReadYet.Any);
        Assert.DoesNotContain("not read yet", room);
    }

    /// <summary>
    /// A ref moves its repository's line, its part of the work (FACTSPLIT1a) and its part of the trees (FACTSPLIT1b), never the
    /// sessions' look, which holds no git; a session's records moving move the look and every part of the work, as they moved the
    /// whole, and the trees' parts as the look lands (<see cref="A_session_ended_judges_its_repository_s_trees_again_as_the_look_lands_and_no_other"/>);
    /// the configuration the look and every part of the trees, whose commits a line set holds. A part is named only once the
    /// service's answers name it.
    /// </summary>
    [Fact]
    public void A_ref_moved_wakes_its_repository_s_line_and_work_and_the_trees_and_a_ledger_move_the_machine()
    {
        var facts = MachineFactsSnapshot.Empty.With(HelpFacts.Machine, HelpFacts.Whole, new JudgedFact(Whole(Site, Engine, Docs), At));

        Assert.Equal(
            [(HelpFacts.Lines, "site"), (HelpFacts.Trees, "site"), (HelpFacts.Work, "site")],
            HelpFacts.Woken(ChangeTopics.Refs, "site", facts));
        Assert.Equal(
            [
                (HelpFacts.Machine, HelpFacts.Whole), (HelpFacts.Sessions, HelpFacts.Whole), (HelpFacts.History, HelpFacts.Whole),
                (HelpFacts.Work, "engine"), (HelpFacts.Work, "site"), (HelpFacts.Work, HelpFacts.NoCheckout),
            ],
            HelpFacts.Woken(ChangeTopics.Ledger, "sessions,quests", facts));
        // Before the service's answers there is no part to wake: each is judged as they land.
        Assert.Equal(
            [(HelpFacts.Machine, HelpFacts.Whole), (HelpFacts.Sessions, HelpFacts.Whole), (HelpFacts.History, HelpFacts.Whole)],
            HelpFacts.Woken(ChangeTopics.Ledger, "sessions", MachineFactsSnapshot.Empty));
        Assert.Equal(
            [(HelpFacts.Machine, HelpFacts.Whole), (HelpFacts.Sessions, HelpFacts.Whole), (HelpFacts.History, HelpFacts.Whole)],
            HelpFacts.Woken(ChangeTopics.Ledger, "quests", facts));
        Assert.Equal(
            [
                (HelpFacts.Machine, HelpFacts.Whole), (HelpFacts.Sessions, HelpFacts.Whole),
                (HelpFacts.Trees, "engine"), (HelpFacts.Trees, "site"), (HelpFacts.Trees, HelpFacts.NoCheckout),
            ],
            HelpFacts.Woken(ChangeTopics.Config, "driver.json", facts));
        Assert.Equal(
            [(HelpFacts.Machine, HelpFacts.Whole), (HelpFacts.Sessions, HelpFacts.Whole)],
            HelpFacts.Woken(ChangeTopics.Config, "driver.json", MachineFactsSnapshot.Empty));
        // A press's words and the window's focus move none of the room's facts.
        Assert.Empty(HelpFacts.Woken(ChangeTopics.Words, "s1", facts));
        Assert.Empty(HelpFacts.Woken(ChangeTopics.Focus, "window", facts));
    }

    [Fact]
    public async Task A_refs_wake_judges_that_repository_s_line_again_alone_and_the_next_room_has_it()
    {
        var guessed = new Dictionary<string, int>(StringComparer.Ordinal);
        var siteLine = "main";
        using var facts = new MachineFacts();
        HelpFacts.Keep(facts, Judges(Whole(Engine, Site)) with
        {
            Guess = (root, _) =>
            {
                lock (guessed) guessed[root] = guessed.GetValueOrDefault(root) + 1;
                return Task.FromResult<string?>(root == Site.Root ? Volatile.Read(ref siteLine) : "main");
            },
        });
        facts.Start();
        await facts.FirstAsync(HelpFacts.Lines, "engine", CancellationToken.None).WaitAsync(Within);
        await facts.FirstAsync(HelpFacts.Lines, "site", CancellationToken.None).WaitAsync(Within);

        Volatile.Write(ref siteLine, "trunk");
        HelpFacts.Wake(facts, ChangeTopics.Refs, "site");
        await Poll.Until(() => HelpFacts.Read(facts.Current, DriverConfig.Empty)!.Lines.Single(line => line.Repository == "site").Branch == "trunk", within: Within);

        lock (guessed) Assert.Equal((1, 2), (guessed[Engine.Root!], guessed[Site.Root!]));
        var room = HelpRoom.Render(Describe(HelpFacts.Read(facts.Current, DriverConfig.Empty)!, DriverConfig.Empty));
        Assert.Contains("| `site` | not driven | `trunk` (the checkout's own) |", room);
    }

    /// <summary>
    /// 🔴 FACTSPLIT1a (D181 points 2-3): one repository's refs judge its part of the work again alone. The other repository's part
    /// and the part of the repositories with no checkout here stand as last judged.
    /// </summary>
    [Fact]
    public async Task A_refs_wake_judges_that_repository_s_work_again_alone()
    {
        var judged = new Dictionary<string, int>(StringComparer.Ordinal);
        var clock = new Clock();
        using var facts = new MachineFacts(clock.Now);
        HelpFacts.Keep(facts, Judges(Whole(Engine, Site, Docs)) with
        {
            Work = (part, _) =>
            {
                var subject = part.Repository ?? HelpFacts.NoCheckout;
                lock (judged) judged[subject] = judged.GetValueOrDefault(subject) + 1;
                return Task.FromResult<HelpWork?>(new HelpWork([], [], []));
            },
        });
        facts.Start();
        foreach (var part in (string[])["engine", "site", HelpFacts.NoCheckout])
        {
            await facts.FirstAsync(HelpFacts.Work, part, CancellationToken.None).WaitAsync(Within);
        }

        // Past the rest a burst of one repository's refs is held to.
        clock.Pass(HelpFacts.WorkRules.Rest + TimeSpan.FromSeconds(1));
        HelpFacts.Wake(facts, ChangeTopics.Refs, "site");
        await Poll.Until(() => Judged(judged, "site") == 2, within: Within);
        await facts.Settled().WaitAsync(Within);

        Assert.Equal((1, 2, 1), (Judged(judged, "engine"), Judged(judged, "site"), Judged(judged, HelpFacts.NoCheckout)));
    }

    /// <summary>
    /// 🔴 FACTSPLIT1a: the room reads the work as it read the whole, merged from the parts: by repository as the lines are, the
    /// repositories with no checkout here last. While any part was never judged the work is not read yet, as the whole was.
    /// </summary>
    [Fact]
    public void The_room_reads_the_work_merged_from_its_parts_and_none_while_a_part_is_not_judged_yet()
    {
        var engine = new HelpWork([Unit("u-engine", "engine")], [], [Recovery("engine")]);
        var site = new HelpWork([Unit("u-site", "site")], [new InventoryUnread("site", "git could not list `site`'s recovery refs: fatal: bad object")], []);
        var rest = new HelpWork([Unit("u-docs", "docs")], [new InventoryUnread("docs", "`docs` has no checkout here, so its recovery refs were not read")], []);
        var partly = MachineFactsSnapshot.Empty
            .With(HelpFacts.Machine, HelpFacts.Whole, new JudgedFact(Whole(Site, Engine, Docs), At))
            .With(HelpFacts.Work, "site", new JudgedFact(site, At))
            .With(HelpFacts.Work, HelpFacts.NoCheckout, new JudgedFact(rest, At));

        var unread = HelpFacts.Read(partly, DriverConfig.Empty)!;
        Assert.Null(unread.Work);
        Assert.True(unread.NotReadYet.Work);
        Assert.Contains("The milestones are not read yet", HelpRoom.Render(Describe(unread, DriverConfig.Empty)));

        var read = HelpFacts.Read(partly.With(HelpFacts.Work, "engine", new JudgedFact(engine, At)), DriverConfig.Empty)!;

        Assert.False(read.NotReadYet.Work);
        Assert.Equal(["u-engine", "u-site", "u-docs"], read.Work!.Units.Select(unit => unit.Unit));
        Assert.Equal(["site", "docs"], read.Work.Unread.Select(said => said.Repository));
        Assert.Equal(["engine"], read.Work.Recovery.Select(recovery => recovery.Repository));
        var room = HelpRoom.Render(Describe(read, DriverConfig.Empty));
        Assert.Contains("- not read: git could not list `site`'s recovery refs: fatal: bad object\n", room);
        Assert.Contains("- not read: `docs` has no checkout here, so its recovery refs were not read\n", room);
    }

    /// <summary>
    /// 🔴 FACTSPLIT1b (D181 points 2-3): one repository's refs judge its part of the sessions' trees again alone, and never the look
    /// without them. The other repository's part and the part of the repositories with no checkout here stand as last judged.
    /// </summary>
    [Fact]
    public async Task A_refs_wake_judges_that_repository_s_trees_again_alone_and_never_the_look()
    {
        var judged = new Dictionary<string, int>(StringComparer.Ordinal);
        var looked = 0;
        var clock = new Clock();
        using var facts = new MachineFacts(clock.Now);
        HelpFacts.Keep(facts, Judges(Whole(Engine, Site, Docs)) with
        {
            Sessions = _ =>
            {
                Interlocked.Increment(ref looked);
                return Task.FromResult<SessionLook?>(new SessionLook([], [], []));
            },
            Trees = (part, _) =>
            {
                var subject = part.Repository ?? HelpFacts.NoCheckout;
                lock (judged) judged[subject] = judged.GetValueOrDefault(subject) + 1;
                return Task.FromResult<HelpTrees?>(NoTrees);
            },
        });
        facts.Start();
        foreach (var part in (string[])["engine", "site", HelpFacts.NoCheckout])
        {
            await facts.FirstAsync(HelpFacts.Trees, part, CancellationToken.None).WaitAsync(Within);
        }

        // Past the rest a burst of one repository's refs is held to.
        clock.Pass(HelpFacts.TreesRules.Rest + TimeSpan.FromSeconds(1));
        HelpFacts.Wake(facts, ChangeTopics.Refs, "site");
        await Poll.Until(() => Judged(judged, "site") == 2, within: Within);
        await facts.Settled().WaitAsync(Within);

        Assert.Equal((1, 2, 1), (Judged(judged, "engine"), Judged(judged, "site"), Judged(judged, HelpFacts.NoCheckout)));
        Assert.Equal(1, Volatile.Read(ref looked));
    }

    /// <summary>
    /// 🔴 FACTSPLIT1b: the room reads the sessions as it read the whole look, the look without trees with every part's trees merged
    /// in by their path; while the look or any part was never judged the sessions are not read yet, as the whole was.
    /// </summary>
    [Fact]
    public void The_room_reads_the_sessions_merged_from_the_look_and_its_trees_and_none_while_one_is_not_judged_yet()
    {
        var engineTree = Path.Combine("trees", "default", "engine", "s-1");
        var siteTree = Path.Combine("trees", "alpha", "site", "s-2");
        var docsTree = Path.Combine("trees", "default", "docs", "s-3");
        var look = new SessionLook([Ended("e1", "engine", engineTree), Ended("s2", "site", siteTree), Ended("d3", "docs", docsTree)], [], []);
        var partly = MachineFactsSnapshot.Empty
            .With(HelpFacts.Machine, HelpFacts.Whole, new JudgedFact(Whole(Site, Engine, Docs), At))
            .With(HelpFacts.Trees, "engine", new JudgedFact(TreesOf((engineTree, new TreeWork(2, 0))), At))
            .With(HelpFacts.Trees, HelpFacts.NoCheckout, new JudgedFact(TreesOf((docsTree, new TreeWork(0, 1))), At));

        Assert.Null(HelpFacts.Read(partly, DriverConfig.Empty)!.Sessions);
        var unread = HelpFacts.Read(partly.With(HelpFacts.Sessions, HelpFacts.Whole, new JudgedFact(look, At)), DriverConfig.Empty)!;
        Assert.Null(unread.Sessions);
        Assert.True(unread.NotReadYet.Sessions);
        Assert.Contains("- This machine's sessions by the Sessions list's groups: not read yet.\n", HelpRoom.Render(Describe(unread, DriverConfig.Empty)));

        var whole = partly
            .With(HelpFacts.Sessions, HelpFacts.Whole, new JudgedFact(look, At))
            .With(HelpFacts.Trees, "site", new JudgedFact(TreesOf((siteTree, new TreeWork(0, 0))), At));
        var read = HelpFacts.Read(whole, DriverConfig.Empty)!;

        Assert.False(read.NotReadYet.Sessions);
        Assert.Equal(look.Records, read.Sessions!.Records);
        Assert.Equal(
            [SessionGroups.Normal(siteTree), SessionGroups.Normal(docsTree), SessionGroups.Normal(engineTree)],
            read.Sessions.Trees.Keys.Order(StringComparer.Ordinal));
        // Keyed as the Sessions list keys them, whatever the case a path is asked in.
        Assert.Equal(2, read.Sessions.Trees[SessionGroups.Normal(engineTree).ToUpperInvariant()].Commits);
        Assert.Contains(", 2 to review, 0 working, 0 resuming later, 1 ended.\n", HelpRoom.Render(Describe(read, DriverConfig.Empty)));
    }

    /// <summary>🔴 FACTSPLIT1b: a repository the registry no longer names drops its part of the trees; what is left is read whole.</summary>
    [Fact]
    public async Task A_repository_gone_from_the_registry_loses_its_part_of_the_trees()
    {
        var whole = Whole(Engine, Site);
        using var facts = new MachineFacts();
        HelpFacts.Keep(facts, Judges(null) with
        {
            Machine = _ => Task.FromResult<HelpMachineWhole?>(Volatile.Read(ref whole)),
            Sessions = _ => Task.FromResult<SessionLook?>(new SessionLook([], [], [])),
        });
        facts.Start();
        foreach (var part in (string[])["engine", "site", HelpFacts.NoCheckout])
        {
            await facts.FirstAsync(HelpFacts.Trees, part, CancellationToken.None).WaitAsync(Within);
        }

        Volatile.Write(ref whole, Whole(Engine));
        await facts.JudgedAsync(HelpFacts.Machine, HelpFacts.Whole, CancellationToken.None).WaitAsync(Within);

        Assert.Null(facts.Current.Of(HelpFacts.Trees, "site"));
        Assert.False(HelpFacts.Read(facts.Current, DriverConfig.Empty)!.NotReadYet.Sessions);
    }

    /// <summary>
    /// 🔴 FACTSPLIT1b: the trees a part judges are read from the look, so a session ending, which moves its records and no ref, judges
    /// its repository's part again as the look lands: at once, not held by the rest its refs keep, and no other part.
    /// </summary>
    [Fact]
    public async Task A_session_ended_judges_its_repository_s_trees_again_as_the_look_lands_and_no_other()
    {
        var engineTree = Path.Combine("trees", "default", "engine", "s-1");
        var running = new SessionLook([Ended("e1", "engine", engineTree) with { State = "working" }, Ended("s2", "site", Path.Combine("trees", "default", "site", "s-2"))], [], []);
        var look = running;
        var judged = new Dictionary<string, int>(StringComparer.Ordinal);
        var asked = new List<IReadOnlyList<string>>();
        using var facts = new MachineFacts(new Clock().Now);
        HelpFacts.Keep(facts, Judges(Whole(Engine, Site)) with
        {
            Sessions = _ => Task.FromResult<SessionLook?>(Volatile.Read(ref look)),
            Trees = (part, _) =>
            {
                var subject = part.Repository ?? HelpFacts.NoCheckout;
                lock (judged)
                {
                    judged[subject] = judged.GetValueOrDefault(subject) + 1;
                    if (subject == "engine") asked.Add(SessionGroups.TreesToJudge(part.Look, _ => true, part.Sessions));
                }

                return Task.FromResult<HelpTrees?>(NoTrees);
            },
        });
        facts.Start();
        foreach (var part in (string[])["engine", "site", HelpFacts.NoCheckout])
        {
            await facts.FirstAsync(HelpFacts.Trees, part, CancellationToken.None).WaitAsync(Within);
        }

        // The clock stands still, so a judgement the rest held would wait half a minute.
        Volatile.Write(ref look, running with { Records = [running.Records[0] with { State = "completed" }, running.Records[1]] });
        await facts.JudgedAsync(HelpFacts.Sessions, HelpFacts.Whole, CancellationToken.None).WaitAsync(Within);
        await Poll.Until(() => Judged(judged, "engine") == 2, within: Within);
        await facts.Settled().WaitAsync(Within);

        Assert.Equal((2, 1, 1), (Judged(judged, "engine"), Judged(judged, "site"), Judged(judged, HelpFacts.NoCheckout)));
        lock (judged)
        {
            Assert.Empty(asked[0]);
            Assert.Equal([engineTree], asked[1]);
        }
    }

    /// <summary>
    /// 🔴 FACTSPLIT1b: one repository's judgement of its trees starts git in its own trees alone, in every workspace's folder of it;
    /// another repository's trees start none, nor does the look landing again with no tree of a part moved.
    /// </summary>
    [Fact]
    public async Task One_repository_s_trees_judgement_starts_git_in_its_own_trees_alone()
    {
        var trees = new SessionTrees(_home).TreesRoot;
        string Tree(string workspace, string repository, string name)
        {
            var tree = Path.Combine(trees, workspace, repository, name);
            Directory.CreateDirectory(tree);
            // Linked as git links a worktree, so git is asked of it.
            File.WriteAllText(Path.Combine(tree, ".git"), "gitdir: elsewhere\n");
            return tree;
        }

        string[] engineTrees = [Tree("default", "engine", "s-1"), Tree("alpha", "engine", "s-2")];
        var look = new SessionLook(
            [
                Ended("e1", "engine", engineTrees[0]), Ended("e2", "engine", engineTrees[1]) with { Workspace = "alpha" },
                Ended("s3", "site", Tree("default", "site", "s-3")), Ended("d4", "docs", Tree("default", "docs", "s-4")),
            ],
            [], []);
        var starts = new List<string>();
        using var facts = new MachineFacts();
        HelpFacts.Keep(facts, Judges(Whole(Engine, Site, Docs)) with
        {
            Sessions = _ => Task.FromResult<SessionLook?>(look),
            Trees = async (part, ct) => await HelpFacts.ReadTreesAsync(_home, part, ct),
        });
        using (ProcessStarts.Within(info =>
               {
                   lock (starts) starts.Add(Path.GetFullPath(info.WorkingDirectory));
                   return null;
               }))
        {
            facts.Start();
        }

        foreach (var part in (string[])["engine", "site", HelpFacts.NoCheckout])
        {
            await facts.FirstAsync(HelpFacts.Trees, part, CancellationToken.None).WaitAsync(Within);
        }

        await facts.Settled().WaitAsync(Within);
        lock (starts) starts.Clear();

        await facts.JudgedAsync(HelpFacts.Trees, "engine", CancellationToken.None).WaitAsync(Within);
        await facts.JudgedAsync(HelpFacts.Sessions, HelpFacts.Whole, CancellationToken.None).WaitAsync(Within);
        await facts.Settled().WaitAsync(Within);

        lock (starts)
        {
            Assert.Equal(engineTrees.Select(Path.GetFullPath).Order(StringComparer.Ordinal), starts.Order(StringComparer.Ordinal));
        }
    }

    [Fact]
    public async Task A_repository_gone_from_the_registry_loses_its_line()
    {
        var whole = Whole(Engine, Site);
        using var facts = new MachineFacts();
        HelpFacts.Keep(facts, Judges(null) with
        {
            Machine = _ => Task.FromResult<HelpMachineWhole?>(Volatile.Read(ref whole)),
        });
        facts.Start();
        await facts.FirstAsync(HelpFacts.Lines, "site", CancellationToken.None).WaitAsync(Within);

        Volatile.Write(ref whole, Whole(Engine));
        await facts.JudgedAsync(HelpFacts.Machine, HelpFacts.Whole, CancellationToken.None).WaitAsync(Within);

        Assert.Null(facts.Current.Of(HelpFacts.Lines, "site"));
    }

    /// <summary>🔴 FACTSPLIT1a: a repository the registry no longer names drops its part of the work; what is left is read whole.</summary>
    [Fact]
    public async Task A_repository_gone_from_the_registry_loses_its_part_of_the_work()
    {
        var whole = Whole(Engine, Site);
        using var facts = new MachineFacts();
        HelpFacts.Keep(facts, Judges(null) with
        {
            Machine = _ => Task.FromResult<HelpMachineWhole?>(Volatile.Read(ref whole)),
            Work = (_, _) => Task.FromResult<HelpWork?>(new HelpWork([], [], [])),
        });
        facts.Start();
        foreach (var part in (string[])["engine", "site", HelpFacts.NoCheckout])
        {
            await facts.FirstAsync(HelpFacts.Work, part, CancellationToken.None).WaitAsync(Within);
        }

        Volatile.Write(ref whole, Whole(Engine));
        await facts.JudgedAsync(HelpFacts.Machine, HelpFacts.Whole, CancellationToken.None).WaitAsync(Within);

        Assert.Null(facts.Current.Of(HelpFacts.Work, "site"));
        Assert.False(HelpFacts.Read(facts.Current, DriverConfig.Empty)!.NotReadYet.Work);
    }

    /// <summary>
    /// 🔴 FACTSPLIT1a: the work's trees in use are the service's answers its part was made from, so a session letting its tree go
    /// judges that repository's part again as the answers land, and no other.
    /// </summary>
    [Fact]
    public async Task A_tree_a_session_let_go_judges_its_repository_s_work_again_as_the_service_s_answers_land()
    {
        var holding = new SessionView("s1", "engine", "working") { Tree = "trees/default/engine/s-1" };
        var whole = new HelpMachineWhole(new Snapshot([], [Engine, Site], [holding]), [], []);
        var judged = new Dictionary<string, int>(StringComparer.Ordinal);
        var inUse = new List<IReadOnlySet<string>>();
        var clock = new Clock();
        using var facts = new MachineFacts(clock.Now);
        HelpFacts.Keep(facts, Judges(null) with
        {
            Machine = _ => Task.FromResult<HelpMachineWhole?>(Volatile.Read(ref whole)),
            Work = (part, _) =>
            {
                var subject = part.Repository ?? HelpFacts.NoCheckout;
                lock (judged)
                {
                    judged[subject] = judged.GetValueOrDefault(subject) + 1;
                    if (subject == "engine") inUse.Add(part.InUse);
                }

                return Task.FromResult<HelpWork?>(new HelpWork([], [], []));
            },
        });
        facts.Start();
        foreach (var part in (string[])["engine", "site", HelpFacts.NoCheckout])
        {
            await facts.FirstAsync(HelpFacts.Work, part, CancellationToken.None).WaitAsync(Within);
        }

        clock.Pass(HelpFacts.WorkRules.Rest + TimeSpan.FromSeconds(1));
        Volatile.Write(ref whole, whole with { Snapshot = whole.Snapshot with { Active = [] } });
        await facts.JudgedAsync(HelpFacts.Machine, HelpFacts.Whole, CancellationToken.None).WaitAsync(Within);
        await Poll.Until(() => Judged(judged, "engine") == 2, within: Within);
        await facts.Settled().WaitAsync(Within);

        Assert.Equal((2, 1, 1), (Judged(judged, "engine"), Judged(judged, "site"), Judged(judged, HelpFacts.NoCheckout)));
        lock (judged)
        {
            Assert.Equal(["trees/default/engine/s-1"], inUse[0]);
            Assert.Empty(inUse[1]);
        }
    }

    /// <summary>
    /// 🔴 FACTSPLIT1a: a repository the trees name with no checkout here keeps its line in the part of its own, read from the home as
    /// the module reads it, and the room says it; the part of the repository with a checkout reads none of it.
    /// </summary>
    [Fact]
    public async Task A_repository_with_no_checkout_here_keeps_its_unread_line_in_the_part_of_its_own()
    {
        // A folder with no link to a repository: no git is asked of it.
        Directory.CreateDirectory(Path.Combine(new SessionTrees(_home).TreesRoot, "default", "docs", "s-1"));
        using var facts = new MachineFacts();
        HelpFacts.Keep(facts, Judges(Whole(Engine, Docs)) with { Work = async (part, ct) => await HelpFacts.ReadWorkAsync(_home, part, ct) });
        facts.Start();
        await facts.FirstAsync(HelpFacts.Work, "engine", CancellationToken.None).WaitAsync(Within);
        await facts.FirstAsync(HelpFacts.Work, HelpFacts.NoCheckout, CancellationToken.None).WaitAsync(Within);

        var read = HelpFacts.Read(facts.Current, DriverConfig.Empty)!;

        Assert.Equal([new InventoryUnread("docs", "`docs` has no checkout here, so its recovery refs were not read")], read.Work!.Unread);
        Assert.Equal(["docs"], read.Work.Units.Select(unit => unit.Repository));
        Assert.Contains("- not read: `docs` has no checkout here, so its recovery refs were not read\n", HelpRoom.Render(Describe(read, DriverConfig.Empty)));
        Assert.Empty(facts.Current.Value<HelpWork>(HelpFacts.Work, "engine")!.Units);
    }

    /// <summary>
    /// 🔴 FACTSPLIT1a: what the inventory could not read names its repository as data, so a workspace's room keeps its own
    /// repository's line though its words name another workspace's, and the other room never has it.
    /// </summary>
    [Fact]
    public void A_workspace_s_room_keeps_its_own_repository_s_unread_line_whatever_its_words_name()
    {
        var engine = new RepoView("engine", Adopted: true, Root: "checkouts/engine", Workspace: "alpha");
        var site = new RepoView("site", Adopted: true, Root: "checkouts/site", Workspace: "beta");
        const string said = "git could not list `engine`'s recovery refs: its checkout is a worktree of `site`";
        var facts = MachineFactsSnapshot.Empty
            .With(HelpFacts.Machine, HelpFacts.Whole, new JudgedFact(Whole(engine, site), At))
            .With(HelpFacts.Work, "engine", new JudgedFact(new HelpWork([Unit("u-engine", "engine")], [new InventoryUnread("engine", said)], []), At))
            .With(HelpFacts.Work, "site", new JudgedFact(new HelpWork([Unit("u-site", "site")], [], []), At))
            .With(HelpFacts.Work, HelpFacts.NoCheckout, new JudgedFact(new HelpWork([], [], []), At));

        var machine = Describe(HelpFacts.Read(facts, DriverConfig.Empty)!, DriverConfig.Empty);

        Assert.Equal([said], machine.Within("alpha").WorkUnread);
        Assert.Empty(machine.Within("beta").WorkUnread);
        Assert.Contains($"- not read: {said}\n", HelpRoom.Render(machine.Within("alpha")));
    }

    /// <summary>
    /// 🔴 FACTSPLIT1a: one repository's judgement of its work starts git in its own trees, in every workspace's folder of it, and in
    /// its own checkout alone; another repository's trees are never listed.
    /// </summary>
    [Fact]
    public async Task One_repository_s_work_judgement_starts_git_in_its_own_trees_and_checkout_alone()
    {
        var trees = new SessionTrees(_home).TreesRoot;
        string Tree(string workspace, string repository, string name)
        {
            var tree = Path.Combine(trees, workspace, repository, name);
            Directory.CreateDirectory(tree);
            // Linked as git links a worktree, so its branch is asked of git.
            File.WriteAllText(Path.Combine(tree, ".git"), "gitdir: elsewhere\n");
            return tree;
        }

        string[] engineTrees = [Tree("default", "engine", "s-1"), Tree("alpha", "engine", "s-2")];
        Tree("default", "site", "s-3");
        Tree("default", "docs", "s-4");
        var engine = new RepoView("engine", Adopted: true, Root: Path.Combine(_home, "checkouts", "engine"));
        var site = new RepoView("site", Adopted: true, Root: Path.Combine(_home, "checkouts", "site"));
        Directory.CreateDirectory(engine.Root!);
        Directory.CreateDirectory(site.Root!);
        var starts = new List<string>();
        using var facts = new MachineFacts();
        HelpFacts.Keep(facts, Judges(Whole(engine, site, Docs)) with { Work = async (part, ct) => await HelpFacts.ReadWorkAsync(_home, part, ct) });
        using (ProcessStarts.Within(info =>
               {
                   lock (starts) starts.Add(Path.GetFullPath(info.WorkingDirectory));
                   return null;
               }))
        {
            facts.Start();
        }

        foreach (var part in (string[])["engine", "site", HelpFacts.NoCheckout])
        {
            await facts.FirstAsync(HelpFacts.Work, part, CancellationToken.None).WaitAsync(Within);
        }

        await facts.Settled().WaitAsync(Within);
        lock (starts) starts.Clear();

        await facts.JudgedAsync(HelpFacts.Work, "engine", CancellationToken.None).WaitAsync(Within);

        lock (starts)
        {
            Assert.Equal(
                engineTrees.Append(engine.Root!).Select(Path.GetFullPath).Order(StringComparer.Ordinal),
                starts.Order(StringComparer.Ordinal));
        }
    }

    [Fact]
    public async Task A_room_that_said_something_not_read_yet_is_written_again_as_the_facts_land()
    {
        var agents = Path.Combine(HelpRoom.PathOf(_home), "AGENTS.md");
        var unread = MachineFactsSnapshot.Empty.With(HelpFacts.Machine, HelpFacts.Whole, new JudgedFact(Whole(Site), At));
        HelpRoom.Prepare(_home, Describe(HelpFacts.Read(unread, DriverConfig.Empty)!, DriverConfig.Empty));
        Assert.Contains("| `site` | not driven | not read yet |", File.ReadAllText(agents));
        var landed = unread
            .With(HelpFacts.Lines, "site", new JudgedFact(new HelpLineGuess("main"), At))
            .With(HelpFacts.Sessions, HelpFacts.Whole, new JudgedFact(new SessionLook([], [], []), At))
            .With(HelpFacts.Trees, "site", new JudgedFact(NoTrees, At))
            .With(HelpFacts.Trees, HelpFacts.NoCheckout, new JudgedFact(NoTrees, At))
            .With(HelpFacts.History, HelpFacts.Whole, new JudgedFact(new List<HistoryReading>(), At))
            .With(HelpFacts.Work, "site", new JudgedFact(new HelpWork([], [], []), At))
            .With(HelpFacts.Work, HelpFacts.NoCheckout, new JudgedFact(new HelpWork([], [], []), At));
        var described = 0;
        var catchUp = new HelpRoomCatchUp(_home, () =>
        {
            Interlocked.Increment(ref described);
            return Describe(HelpFacts.Read(landed, DriverConfig.Empty)!, DriverConfig.Empty);
        });

        catchUp.Landed();
        await Poll.Until(() => File.ReadAllText(agents).Contains("| `site` | not driven | `main` (the checkout's own) |", StringComparison.Ordinal), within: Within);

        // Written from the facts that leave nothing to catch up on, a landing writes nothing more.
        await Poll.Until(() => !catchUp.Writing, within: Within);
        var before = Volatile.Read(ref described);
        catchUp.Landed();
        await Task.Delay(100);
        Assert.Equal(before, Volatile.Read(ref described));
    }

    /// <summary>Stand-in judges: the machine as given, each checkout's line `main`, every other topic read as nothing.</summary>
    private static HelpFactJudges Judges(HelpMachineWhole? whole) => new(
        Machine: _ => Task.FromResult(whole),
        Sessions: _ => Task.FromResult<SessionLook?>(null),
        History: _ => Task.FromResult<IReadOnlyList<HistoryReading>?>(null),
        Work: (_, _) => Task.FromResult<HelpWork?>(null))
    {
        Guess = (_, _) => Task.FromResult<string?>("main"),
    };

    private static int Judged(Dictionary<string, int> judged, string subject)
    {
        lock (judged) return judged.GetValueOrDefault(subject);
    }

    /// <summary>A part of the trees, each tree's work keyed by its path as the Sessions list keys it.</summary>
    private static HelpTrees TreesOf(params (string Tree, TreeWork Work)[] trees) =>
        new(trees.ToDictionary(each => SessionGroups.Normal(each.Tree), each => each.Work, StringComparer.OrdinalIgnoreCase));

    /// <summary>A session of this machine's on <paramref name="repository"/> that ended in <paramref name="tree"/>, the newest on it.</summary>
    private static SessionRecord Ended(string id, string repository, string tree) =>
        new(id, repository, "completed") { Tree = tree, Created = At.AddHours(-1), Updated = At.AddMinutes(-30) };

    /// <summary>A unit of <paramref name="repository"/> whose cleanup waits on a tree a session holds, so the room lists it.</summary>
    private static WorkMilestone Unit(string unit, string repository) => new(
        unit, repository, $"daoris/s-{unit}", $"s-{unit}",
        new MilestoneEnd("ended", EndBy.Merged, "main", "a1b2c3d4e5f6", At, "every commit of it is on `main`"),
        [new(unit, InventoryKind.Line, "main", InventoryRole.Result, InventoryDisposition.Kept, "the line holds it")],
        new MilestoneCleanup("pending", [new(unit, InventoryKind.Tree, $"s-{unit}", InventoryRole.Temporary, InventoryDisposition.Blocked, "a session still holds this tree")], []),
        [], []);

    private static InventoryEntry Recovery(string repository) => new(
        "", InventoryKind.MovedAside, $"trees/default/{repository}/s0-20261018", InventoryRole.Recovery, InventoryDisposition.Retained,
        "kept until 2026-10-25 09:00 UTC") { Repository = repository };

    /// <summary>The facts' time, moved on by the test alone, so a rest is passed without waiting it out.</summary>
    private sealed class Clock
    {
        private long _ticks = At.UtcTicks;

        public DateTimeOffset Now() => new(Interlocked.Read(ref _ticks), TimeSpan.Zero);

        public void Pass(TimeSpan span) => Interlocked.Add(ref _ticks, span.Ticks);
    }
}
