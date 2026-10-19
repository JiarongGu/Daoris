namespace Daoris.Driver;

/// <summary>
/// The facts Ask Daoris's room is written from (PARALLEL1a, D181 points 1-2): their topics on <see cref="MachineFacts"/>, the rules
/// each is judged by, which of the loop's wakes moves each, and the room's reading of them, where what was never judged is said
/// <i>not read yet</i>. A start reads them as last judged and writes its room at once; it never reads the machine itself.
/// </summary>
/// <remarks>
/// <para><b>Per repository, the line.</b> The one fact of the room that asks git of each repository is the checkout's guess at its
/// line (<see cref="LineGuesses"/>), judged per repository on its <c>refs</c> wake, so a ref moved re-judges that repository alone.
/// What the person set is read from the configuration as the room is written, and needs no judgement.</para>
///
/// <para><b>Whole, the rest.</b> The service's answers and the roster, the sessions' look across every tree, each workspace's
/// history and the work inventory are each gathered across the home by their own readers, so each is one subject, judged on its
/// wakes, at its deadline, and at most once each <see cref="FactRules.Rest"/>.</para>
/// </remarks>
public static class HelpFacts
{
    /// <summary>The service's answers and the roster (<see cref="HelpMachineWhole"/>), one subject, <see cref="Whole"/>.</summary>
    public const string Machine = "help.machine";

    /// <summary>A checkout's guess at its line (<see cref="HelpLineGuess"/>), by repository.</summary>
    public const string Lines = "help.lines";

    /// <summary>The sessions as the Sessions list looks at them (<see cref="SessionLook"/>), one subject.</summary>
    public const string Sessions = "help.sessions";

    /// <summary>What each workspace keeps of finished work (a list of <see cref="HistoryReading"/>), one subject.</summary>
    public const string History = "help.history";

    /// <summary>Where each unit of work has got to (<see cref="HelpWork"/>), one subject.</summary>
    public const string Work = "help.work";

    /// <summary>The subject of a topic judged whole.</summary>
    public const string Whole = "machine";

    /// <summary>The service's answers: a minute behind at most, as the asks and the registry move no ledger family.</summary>
    public static readonly FactRules MachineRules = new(Deadline: TimeSpan.FromMinutes(1), Bound: TimeSpan.FromSeconds(30), AtOnce: 1);

    /// <summary>A line per repository: two repositories asked at once, as the ref watch asks (§A3).</summary>
    public static readonly FactRules LinesRules = new(Deadline: TimeSpan.FromMinutes(5), Bound: TimeSpan.FromSeconds(30), AtOnce: 2);

    /// <summary>
    /// A reading across the home: a look at every tree took 24.9 s on a loaded machine (HELPSTART1), so it is bounded at minutes and
    /// rests half a minute between the judgements its wakes ask for.
    /// </summary>
    public static readonly FactRules WholeRules = new(Deadline: TimeSpan.FromMinutes(5), Bound: TimeSpan.FromMinutes(2), AtOnce: 1)
    {
        Rest = TimeSpan.FromSeconds(30),
    };

    private static readonly IReadOnlyList<(string Topic, string Subject)> None = [];

    /// <summary>
    /// The facts a loop's wake moves (D181 point 2): a ref of one repository its line, the trees' sessions and the work; a ledger
    /// family the service's answers, and the sessions, the history and the work it records; the configuration the service's answers
    /// and the sessions' verdicts. A press, a word or the window's focus moves none.
    /// </summary>
    public static IReadOnlyList<(string Topic, string Subject)> Woken(string topic, string subject)
    {
        switch (topic)
        {
            case ChangeTopics.Refs:
                return [(Lines, subject), (Sessions, Whole), (Work, Whole)];
            case ChangeTopics.Config:
                return [(Machine, Whole), (Sessions, Whole)];
            case ChangeTopics.Ledger:
                var moved = subject.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                bool Moved(string family) => moved.Contains(family, StringComparer.Ordinal);
                var woken = new List<(string, string)> { (Machine, Whole) };
                if (Moved("sessions") || Moved("quests")) woken.AddRange([(Sessions, Whole), (History, Whole)]);
                if (Moved("sessions")) woken.Add((Work, Whole));
                return woken;
            default:
                return None;
        }
    }

    /// <summary>Wake the facts <paramref name="topic"/>'s <paramref name="subject"/> moves (<see cref="Woken"/>).</summary>
    public static void Wake(MachineFacts facts, string topic, string subject)
    {
        foreach (var (factTopic, factSubject) in Woken(topic, subject)) facts.Wake(factTopic, factSubject);
    }

    /// <summary>
    /// Keep the room's facts on <paramref name="facts"/>: each topic registered with its judge, and the whole ones known, so they are
    /// judged once the facts start. The service's answers make each repository with a checkout here a subject of the lines, and drop
    /// the line of one no longer registered.
    /// </summary>
    public static void Keep(MachineFacts facts, HelpFactJudges judges)
    {
        facts.Topic(Machine, MachineRules, async (_, ct) => await judges.Machine(ct).ConfigureAwait(false));
        facts.Topic(Lines, LinesRules, (repository, ct) => JudgeLineAsync(facts, repository, judges.Guess, ct));
        facts.Topic(Sessions, WholeRules, async (_, ct) => await judges.Sessions(ct).ConfigureAwait(false));
        facts.Topic(History, WholeRules, async (_, ct) => await judges.History(ct).ConfigureAwait(false));
        facts.Topic(Work, WholeRules, async (_, ct) => await judges.Work(ct).ConfigureAwait(false));

        // Once the service's answers have landed, never before: a line's judge reads the checkout from them, so one started first
        // would find none and keep nothing until its deadline.
        HelpMachineWhole? seen = null;
        facts.Changed += snapshot =>
        {
            if (snapshot.Value<HelpMachineWhole>(Machine, Whole) is { } whole && !ReferenceEquals(Interlocked.Exchange(ref seen, whole), whole))
            {
                Known(facts, whole);
            }
        };
        foreach (var topic in (string[])[Machine, Sessions, History, Work]) facts.Subject(topic, Whole);
    }

    /// <summary>
    /// The repositories the service's answers name with a checkout here, made subjects of the lines: one with no line judged yet is
    /// judged now (a wake before the answers found no checkout to ask), and one no longer named is dropped.
    /// </summary>
    private static void Known(MachineFacts facts, HelpMachineWhole whole)
    {
        var checkouts = whole.Snapshot.Repositories
            .Where(known => !string.IsNullOrWhiteSpace(known.Root))
            .Select(known => known.Repository)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var repository in checkouts)
        {
            if (facts.Current.Of(Lines, repository) is null) facts.Wake(Lines, repository);
            else facts.Subject(Lines, repository);
        }

        foreach (var gone in facts.Current.Subjects(Lines).Where(repository => !checkouts.Contains(repository))) facts.Forget(Lines, gone);
    }

    /// <summary>
    /// One repository's line as its checkout guesses it, through <paramref name="guess"/>: null where the service's answers do not
    /// name it with a checkout here, so it keeps no fact.
    /// </summary>
    private static async Task<object?> JudgeLineAsync(
        MachineFacts facts, string repository, Func<string, CancellationToken, Task<string?>> guess, CancellationToken ct)
    {
        var known = facts.Current.Value<HelpMachineWhole>(Machine, Whole)?.Snapshot.Repositories
            .FirstOrDefault(each => string.Equals(each.Repository, repository, StringComparison.OrdinalIgnoreCase));
        if (known?.Root is not { Length: > 0 } root) return null;
        return new HelpLineGuess(await guess(root, ct).ConfigureAwait(false));
    }

    /// <summary>
    /// The room's reading of the facts as they stand: null where the service's answers were never judged, since without them the
    /// room cannot name a repository; else every repository's line as far as it is judged, the rest of the facts as last judged, and
    /// what was not read yet. Reads nothing outside the facts and <paramref name="config"/>.
    /// </summary>
    public static HelpFactsRead? Read(MachineFactsSnapshot facts, DriverConfig config)
    {
        if (facts.Value<HelpMachineWhole>(Machine, Whole) is not { } whole) return null;

        var lines = new List<RepositoryLine>();
        var unread = new List<string>();
        foreach (var known in whole.Snapshot.Repositories.OrderBy(known => known.Repository, StringComparer.Ordinal))
        {
            // What is set first, the checkout's guess as judged only where nothing is: the lines slice reads it alike (PUSH1b1).
            if (CanonicalLine.Judged(config, known.Repository, known.Workspace, known.Root, facts.Value<HelpLineGuess>(Lines, known.Repository))
                is not { } line)
            {
                unread.Add(known.Repository);
                continue;
            }

            lines.Add(new(known.Repository, RemoteTarget.Workspace(known.Workspace), line.Branch, line.Source));
        }

        var sessions = facts.Value<SessionLook>(Sessions, Whole);
        var history = facts.Value<IReadOnlyList<HistoryReading>>(History, Whole);
        var work = facts.Value<HelpWork>(Work, Whole);
        return new HelpFactsRead(whole, lines, sessions, history, work, new HelpNotReadYet(unread)
        {
            Sessions = sessions is null,
            History = history is null,
            Work = work is null,
        });
    }

    /// <summary>
    /// The room's reading as the facts stand: at once where the service's answers were judged; where they never were, once they
    /// first are (HELPSTUCK1's backstop, which the start bounds).
    /// </summary>
    public static async Task<HelpFactsRead?> ReadAsync(MachineFacts facts, DriverConfig config, CancellationToken ct) =>
        Read(facts.Current, config)
        ?? Read(await facts.FirstAsync(Machine, Whole, ct).ConfigureAwait(false), config);
}

/// <summary>The service's answers and the roster, as the machine's topic judged them (PARALLEL1a).</summary>
/// <param name="Snapshot">The registry, the quests and the active sessions.</param>
/// <param name="Standing">The asks the host answered.</param>
/// <param name="Roster">The harnesses as the roster reports them.</param>
public sealed record HelpMachineWhole(Snapshot Snapshot, IReadOnlyList<AskView> Standing, IReadOnlyList<HarnessReport> Roster);

/// <summary>A checkout's guess at its line as git said it (<see cref="LineGuesses"/>); null where git names none.</summary>
public sealed record HelpLineGuess(string? Branch);

/// <summary>Where each unit of work has got to (FABRIC-MILESTONE1b), what could not be read, and Daoris's own recoveries no unit names.</summary>
public sealed record HelpWork(IReadOnlyList<WorkMilestone> Units, IReadOnlyList<string> Unread, IReadOnlyList<InventoryEntry> Recovery);

/// <summary>
/// What was not read yet when the room was written (PARALLEL1a): the repositories whose line was not judged, and whether the
/// sessions, the history and the work were not. Said <i>not read yet</i> where each is said, never a guess and never a wait.
/// </summary>
public sealed record HelpNotReadYet(IReadOnlyList<string> Repositories)
{
    /// <summary>Everything read.</summary>
    public static HelpNotReadYet None { get; } = new([]);

    public bool Sessions { get; init; }

    public bool History { get; init; }

    public bool Work { get; init; }

    /// <summary>Whether anything was not read yet.</summary>
    public bool Any => Repositories.Count > 0 || Sessions || History || Work;
}

/// <summary>The room's reading of the facts (<see cref="HelpFacts.Read"/>): each part as last judged, null where never.</summary>
public sealed record HelpFactsRead(
    HelpMachineWhole Whole, IReadOnlyList<RepositoryLine> Lines, SessionLook? Sessions, IReadOnlyList<HistoryReading>? History,
    HelpWork? Work, HelpNotReadYet NotReadYet);

/// <summary>
/// What reads each of the room's facts, handed by the module that holds the service and the loop (PARALLEL1a): each reads afresh
/// and answers its value, or null where it could not.
/// </summary>
public sealed record HelpFactJudges(
    Func<CancellationToken, Task<HelpMachineWhole?>> Machine,
    Func<CancellationToken, Task<SessionLook?>> Sessions,
    Func<CancellationToken, Task<IReadOnlyList<HistoryReading>?>> History,
    Func<CancellationToken, Task<HelpWork?>> Work)
{
    /// <summary>A checkout's guess at its line, by its root: git's, asked once while its files stand, unless a test hands its own.</summary>
    public Func<string, CancellationToken, Task<string?>> Guess { get; init; } = LineGuesses.Shared.GuessAsync;
}

/// <summary>
/// Ask Daoris's rooms written again as the facts they said were not read yet land (PARALLEL1a, D181 point 2): only a room that said
/// something not read yet, one writing at a time, and the landings meanwhile written once after. Each workspace's room is written
/// from its own workspace's facts (ASKSCOPE1b, <see cref="HelpMachine.Within"/>), and a room that said nothing is not read yet is
/// never written, whichever workspace's facts landed.
/// </summary>
/// <param name="describe">The whole machine from the facts as they stand now; null where they cannot say it yet. Reads no git.</param>
public sealed class HelpRoomCatchUp(string home, Func<HelpMachine?> describe, MachineLog? log = null)
{
    private int _writing;
    private int _again;

    /// <summary>Whether a writing is under way, which a test waits out.</summary>
    internal bool Writing => Volatile.Read(ref _writing) == 1;

    /// <summary>A fact landed: each room written last saying something not read yet is written again, off the caller's thread.</summary>
    public void Landed()
    {
        if (!HelpRoom.SaidNotReadYet(home)) return;
        Interlocked.Exchange(ref _again, 1);
        if (Interlocked.CompareExchange(ref _writing, 1, 0) != 0) return;
        _ = Task.Run(Write);
    }

    private void Write()
    {
        try
        {
            while (Interlocked.Exchange(ref _again, 0) == 1 && HelpRoom.Unread(home) is { Count: > 0 } rooms)
            {
                if (describe() is not { } machine) continue;
                foreach (var workspace in rooms)
                {
                    try
                    {
                        HelpRoom.Prepare(home, machine.Within(workspace));
                    }
                    catch (Exception error)
                    {
                        // That room as last written stands, and the next landing or its start writes it again; another's is still written.
                        log?.Failed("Ask Daoris's room written again", error);
                    }
                }
            }
        }
        catch (Exception error)
        {
            // The rooms as last written stand, and the next landing or start writes them again.
            log?.Failed("Ask Daoris's room written again", error);
        }
        finally
        {
            Volatile.Write(ref _writing, 0);
        }

        if (Volatile.Read(ref _again) == 1) Landed();
    }
}
