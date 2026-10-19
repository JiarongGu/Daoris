using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// PARALLEL1a (D181 points 1-2): Ask Daoris's room is written from the facts the judges keep, never from a read of its own. A
/// repository whose line was not judged yet is said *not read yet*, as are the sessions, the history and the work; a ref moved
/// judges that repository's line again alone, and the next room has it; a room that said something not read yet is written again
/// as the facts land.
/// </summary>
/// <remarks>No process and no git: the judges are stand-ins.</remarks>
public sealed class HelpFactsTests : IDisposable
{
    private static readonly TimeSpan Within = TimeSpan.FromSeconds(10);

    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-10-19T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

    private static readonly RepoView Engine = new("engine", Adopted: true, Root: "checkouts/engine");

    private static readonly RepoView Site = new("site", Adopted: true, Root: "checkouts/site");

    private static readonly RepoView Docs = new("docs", Adopted: true, Root: null);

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
            .With(HelpFacts.History, HelpFacts.Whole, new JudgedFact(new List<HistoryReading>(), At))
            .With(HelpFacts.Work, HelpFacts.Whole, new JudgedFact(new HelpWork([], [], []), At));

        var read = HelpFacts.Read(facts, DriverConfig.Empty)!;
        var room = HelpRoom.Render(Describe(read, DriverConfig.Empty));

        Assert.False(read.NotReadYet.Any);
        Assert.DoesNotContain("not read yet", room);
    }

    [Fact]
    public void A_ref_moved_wakes_its_repository_s_line_and_the_trees_and_a_ledger_move_the_machine()
    {
        Assert.Equal(
            [(HelpFacts.Lines, "site"), (HelpFacts.Sessions, HelpFacts.Whole), (HelpFacts.Work, HelpFacts.Whole)],
            HelpFacts.Woken(ChangeTopics.Refs, "site"));
        Assert.Equal(
            [(HelpFacts.Machine, HelpFacts.Whole), (HelpFacts.Sessions, HelpFacts.Whole), (HelpFacts.History, HelpFacts.Whole), (HelpFacts.Work, HelpFacts.Whole)],
            HelpFacts.Woken(ChangeTopics.Ledger, "sessions,quests"));
        Assert.Equal(
            [(HelpFacts.Machine, HelpFacts.Whole), (HelpFacts.Sessions, HelpFacts.Whole), (HelpFacts.History, HelpFacts.Whole)],
            HelpFacts.Woken(ChangeTopics.Ledger, "quests"));
        Assert.Equal([(HelpFacts.Machine, HelpFacts.Whole), (HelpFacts.Sessions, HelpFacts.Whole)], HelpFacts.Woken(ChangeTopics.Config, "driver.json"));
        // A press's words and the window's focus move none of the room's facts.
        Assert.Empty(HelpFacts.Woken(ChangeTopics.Words, "s1"));
        Assert.Empty(HelpFacts.Woken(ChangeTopics.Focus, "window"));
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
            .With(HelpFacts.History, HelpFacts.Whole, new JudgedFact(new List<HistoryReading>(), At))
            .With(HelpFacts.Work, HelpFacts.Whole, new JudgedFact(new HelpWork([], [], []), At));
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

    /// <summary>Stand-in judges: the machine as given, every other topic read as nothing.</summary>
    private static HelpFactJudges Judges(HelpMachineWhole? whole) => new(
        Machine: _ => Task.FromResult(whole),
        Sessions: _ => Task.FromResult<SessionLook?>(null),
        History: _ => Task.FromResult<IReadOnlyList<HistoryReading>?>(null),
        Work: _ => Task.FromResult<HelpWork?>(null));
}
