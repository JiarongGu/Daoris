namespace Daoris.Driver;

/// <summary>
/// The facts Ask Daoris's room is written from (PARALLEL1a, D181 points 1-2): their topics on <see cref="MachineFacts"/>, the rules
/// each is judged by, which of the loop's wakes moves each, and the room's reading of them, where what was never judged is said
/// <i>not read yet</i>. A start reads them as last judged and writes its room at once; it never reads the machine itself.
/// </summary>
/// <remarks>
/// <para><b>Per repository, the line.</b> The checkout's guess at a repository's line (<see cref="LineGuesses"/>) is judged per
/// repository on its <c>refs</c> wake, so a ref moved re-judges that repository alone. What the person set is read from the
/// configuration as the room is written, and needs no judgement.</para>
///
/// <para><b>Per repository, the work</b> (FACTSPLIT1a, D181 points 2-3). Each repository with a checkout here has its part of the
/// work inventory, its trees, its records and its checkout's refs alone, judged on its <c>refs</c> wake; one more part holds every
/// repository the trees or the records name with no checkout here (<see cref="NoCheckout"/>). The room reads them merged.</para>
///
/// <para><b>Whole, the rest.</b> The service's answers and the roster, the sessions' look across every tree and each workspace's
/// history are each gathered across the home by their own readers, so each is one subject, judged on its wakes, at its deadline,
/// and at most once each <see cref="FactRules.Rest"/>.</para>
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

    /// <summary>
    /// Where each unit of work has got to (<see cref="HelpWork"/>), a part by repository with a checkout here, and one for those with
    /// none (<see cref="NoCheckout"/>).
    /// </summary>
    public const string Work = "help.work";

    /// <summary>
    /// The work's part of the repositories the trees or the records name with no checkout in the registry (FACTSPLIT1a). No
    /// repository is called it: a repository's trees are kept in a folder of its name, which cannot hold <c>&lt;</c>.
    /// </summary>
    public const string NoCheckout = "<no checkout>";

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

    /// <summary>
    /// A repository's part of the work (FACTSPLIT1a): a git start for each of its trees and one for its checkout's refs, so two
    /// repositories at once, as the lines, bounded at minutes as the whole was. It keeps the whole's rest, now one repository's: a
    /// session committing moves its repository's refs at each commit, and a burst of them is one judgement each half minute, while
    /// another repository's refs are judged at once.
    /// </summary>
    public static readonly FactRules WorkRules = new(Deadline: TimeSpan.FromMinutes(5), Bound: TimeSpan.FromMinutes(2), AtOnce: 2)
    {
        Rest = TimeSpan.FromSeconds(30),
    };

    private static readonly IReadOnlyList<(string Topic, string Subject)> None = [];

    /// <summary>
    /// The facts a loop's wake moves (D181 point 2): a ref of one repository its line, its part of the work and the trees' sessions;
    /// a ledger family the service's answers, and the sessions, the history and every part of the work it records, as
    /// <paramref name="facts"/>' answers name them; the configuration the service's answers and the sessions' verdicts. A press, a
    /// word or the window's focus moves none.
    /// </summary>
    public static IReadOnlyList<(string Topic, string Subject)> Woken(string topic, string subject, MachineFactsSnapshot facts)
    {
        switch (topic)
        {
            case ChangeTopics.Refs:
                return [(Lines, subject), (Sessions, Whole), (Work, subject)];
            case ChangeTopics.Config:
                return [(Machine, Whole), (Sessions, Whole)];
            case ChangeTopics.Ledger:
                var moved = subject.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                bool Moved(string family) => moved.Contains(family, StringComparer.Ordinal);
                var woken = new List<(string, string)> { (Machine, Whole) };
                if (Moved("sessions") || Moved("quests")) woken.AddRange([(Sessions, Whole), (History, Whole)]);
                // Which repository's records moved is not said, so every part, as the whole was. Before the answers there is none:
                // each is judged as they land.
                if (Moved("sessions") && facts.Value<HelpMachineWhole>(Machine, Whole) is { } whole)
                {
                    woken.AddRange(WorkParts(whole).Select(part => (Work, part)));
                }

                return woken;
            default:
                return None;
        }
    }

    /// <summary>Wake the facts <paramref name="topic"/>'s <paramref name="subject"/> moves (<see cref="Woken"/>).</summary>
    public static void Wake(MachineFacts facts, string topic, string subject)
    {
        foreach (var (factTopic, factSubject) in Woken(topic, subject, facts.Current)) facts.Wake(factTopic, factSubject);
    }

    /// <summary>
    /// Keep the room's facts on <paramref name="facts"/>: each topic registered with its judge, and the whole ones known, so they are
    /// judged once the facts start. The service's answers make each repository with a checkout here a subject of the lines and of
    /// the work, and the rest one part of the work, and drop the line and the part of one no longer registered.
    /// </summary>
    public static void Keep(MachineFacts facts, HelpFactJudges judges)
    {
        facts.Topic(Machine, MachineRules, async (_, ct) => await judges.Machine(ct).ConfigureAwait(false));
        facts.Topic(Lines, LinesRules, (repository, ct) => JudgeLineAsync(facts, repository, judges.Guess, ct));
        facts.Topic(Sessions, WholeRules, async (_, ct) => await judges.Sessions(ct).ConfigureAwait(false));
        facts.Topic(History, WholeRules, async (_, ct) => await judges.History(ct).ConfigureAwait(false));
        facts.Topic(Work, WorkRules, (part, ct) => JudgeWorkAsync(facts, part, judges.Work, ct));

        // Once the service's answers have landed, never before: a line's and a part's judges read the checkout from them, so one
        // started first would find none and keep nothing until its deadline. The newest answers, read under one gate, so answers
        // told after newer ones passed over never undo what the newer made.
        var gate = new object();
        HelpMachineWhole? seen = null;
        facts.Changed += _ =>
        {
            lock (gate)
            {
                if (facts.Current.Value<HelpMachineWhole>(Machine, Whole) is not { } whole || ReferenceEquals(seen, whole)) return;
                var before = seen;
                seen = whole;
                Known(facts, before, whole);
            }
        };
        foreach (var topic in (string[])[Machine, Sessions, History]) facts.Subject(topic, Whole);
    }

    /// <summary>
    /// The repositories the service's answers name with a checkout here, made subjects of the lines and the work, and the rest a part
    /// of the work: one with nothing judged yet is judged now (a wake before the answers found no checkout to ask), a part whose
    /// reading of the answers moved is judged again (<see cref="Basis"/>), and one no longer named is dropped.
    /// </summary>
    private static void Known(MachineFacts facts, HelpMachineWhole? before, HelpMachineWhole whole)
    {
        var checkouts = Checkouts(whole);
        foreach (var repository in checkouts)
        {
            if (facts.Current.Of(Lines, repository) is null) facts.Wake(Lines, repository);
            else facts.Subject(Lines, repository);
        }

        foreach (var part in WorkParts(whole))
        {
            if (facts.Current.Of(Work, part) is null || (before is not null && Basis(before, part) != Basis(whole, part))) facts.Wake(Work, part);
            else facts.Subject(Work, part);
        }

        foreach (var gone in facts.Current.Subjects(Lines).Where(repository => !checkouts.Contains(repository))) facts.Forget(Lines, gone);
        foreach (var gone in facts.Current.Subjects(Work).Where(part => part != NoCheckout && !checkouts.Contains(part))) facts.Forget(Work, gone);
    }

    /// <summary>The repositories the service's answers name with a checkout here, without case.</summary>
    private static HashSet<string> Checkouts(HelpMachineWhole whole) => whole.Snapshot.Repositories
        .Where(known => !string.IsNullOrWhiteSpace(known.Root))
        .Select(known => known.Repository)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>The work's parts the service's answers name (FACTSPLIT1a): each repository with a checkout here by name, then the rest's.</summary>
    private static IReadOnlyList<string> WorkParts(HelpMachineWhole whole) => [.. Checkouts(whole).Order(StringComparer.Ordinal), NoCheckout];

    /// <summary>
    /// What <paramref name="subject"/>'s part of the work reads from the service's answers (FACTSPLIT1a): a repository's, its own
    /// checkout; the rest's, which repositories have one. Each with the trees the sessions still running or waiting hold, as
    /// <c>/api/sessions</c> answered them, as the module read them afresh before. Null for a repository not named with a checkout.
    /// </summary>
    public static HelpWorkPart? PartOf(HelpMachineWhole whole, string subject)
    {
        var checkouts = Checkouts(whole);
        var inUse = whole.Snapshot.Active.Select(session => session.Tree).OfType<string>().Where(tree => tree.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (subject == NoCheckout) return new HelpWorkPart(null, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), checkouts, inUse);
        if (whole.Snapshot.Repositories.FirstOrDefault(known => string.Equals(known.Repository, subject, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(known.Root)) is not { } checkout)
        {
            return null;
        }

        var roots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [checkout.Repository] = checkout.Root! };
        return new HelpWorkPart(checkout.Repository, roots, checkouts, inUse);
    }

    /// <summary>
    /// What of the service's answers moves <paramref name="part"/>'s judgement: its checkout, or for the rest which repositories have
    /// one, and the trees its repositories' sessions hold. A session letting its tree go lands in the answers after the ledger's wake
    /// woke the work, so the part read the trees in use as they were; their landing judges it again, and no other.
    /// </summary>
    private static string Basis(HelpMachineWhole whole, string part)
    {
        var checkouts = Checkouts(whole);
        bool Ours(string repository) => part == NoCheckout
            ? !checkouts.Contains(repository)
            : string.Equals(repository, part, StringComparison.OrdinalIgnoreCase);
        var reads = part == NoCheckout
            ? checkouts.Order(StringComparer.OrdinalIgnoreCase)
            : whole.Snapshot.Repositories.Where(known => Ours(known.Repository) && !string.IsNullOrWhiteSpace(known.Root)).Select(known => known.Root!).Take(1);
        var held = whole.Snapshot.Active.Where(session => Ours(session.Repository)).Select(session => session.Tree).OfType<string>()
            .Where(tree => tree.Length > 0).Order(StringComparer.OrdinalIgnoreCase);
        return string.Join('\n', reads) + "\n\n" + string.Join('\n', held);
    }

    /// <summary>
    /// One part of the work through <paramref name="work"/>, from what the service's answers say of it (<see cref="PartOf"/>): null
    /// before the answers, or where they no longer name the repository with a checkout here, so it keeps no fact.
    /// </summary>
    private static async Task<object?> JudgeWorkAsync(
        MachineFacts facts, string subject, Func<HelpWorkPart, CancellationToken, Task<HelpWork?>> work, CancellationToken ct)
    {
        if (facts.Current.Value<HelpMachineWhole>(Machine, Whole) is not { } whole || PartOf(whole, subject) is not { } part) return null;
        return await work(part, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// One part of the work read from <paramref name="home"/> (FACTSPLIT1a), as the module's judge reads it: the trees, the records
    /// and the checkout's refs of the part's repositories alone, read-only, as `work state` reads the whole.
    /// </summary>
    public static async Task<HelpWork> ReadWorkAsync(string home, HelpWorkPart part, CancellationToken ct)
    {
        var (units, recovery, unread) = await WorkInventories.ReadPartAsync(home, part.Roots, part.InUse, part.Holds, ct).ConfigureAwait(false);
        return new HelpWork(units, unread, recovery);
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
        var work = WorkOf(facts, whole);
        return new HelpFactsRead(whole, lines, sessions, history, work, new HelpNotReadYet(unread)
        {
            Sessions = sessions is null,
            History = history is null,
            Work = work is null,
        });
    }

    /// <summary>
    /// The work as the room reads it (FACTSPLIT1a), merged from the parts the service's answers name, in their order: by repository,
    /// each part's own order kept, the rest's last. Null while any part was never judged, which the room says not read yet, as it
    /// said the whole: a part alone would read as all the work there is.
    /// </summary>
    private static HelpWork? WorkOf(MachineFactsSnapshot facts, HelpMachineWhole whole)
    {
        var parts = new List<HelpWork>();
        foreach (var subject in WorkParts(whole))
        {
            if (facts.Value<HelpWork>(Work, subject) is not { } part) return null;
            parts.Add(part);
        }

        return new HelpWork(
            [.. parts.SelectMany(part => part.Units)], [.. parts.SelectMany(part => part.Unread)], [.. parts.SelectMany(part => part.Recovery)]);
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

/// <summary>
/// Where each unit of work has got to (FABRIC-MILESTONE1b), what could not be read, each by its repository (FACTSPLIT1a), and
/// Daoris's own recoveries no unit names: one repository's part, or the parts merged.
/// </summary>
public sealed record HelpWork(IReadOnlyList<WorkMilestone> Units, IReadOnlyList<InventoryUnread> Unread, IReadOnlyList<InventoryEntry> Recovery);

/// <summary>What one part of the work reads (FACTSPLIT1a, <see cref="HelpFacts.PartOf"/>), from the service's answers.</summary>
/// <param name="Repository">The repository whose part it is; null for the rest's (<see cref="HelpFacts.NoCheckout"/>).</param>
/// <param name="Roots">The checkout whose refs it reads: the repository's own; none for the rest.</param>
/// <param name="Checkouts">Every repository with a checkout here, which the rest's holds none of.</param>
/// <param name="InUse">The trees sessions still running or waiting hold.</param>
public sealed record HelpWorkPart(
    string? Repository, IReadOnlyDictionary<string, string> Roots, IReadOnlySet<string> Checkouts, IReadOnlySet<string> InUse)
{
    /// <summary>Whether a repository the trees or the records name is this part's.</summary>
    public bool Holds(string repository) => Repository is { } own
        ? string.Equals(own, repository, StringComparison.OrdinalIgnoreCase)
        : !Checkouts.Contains(repository);
}

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

    /// <summary>Whether any part of the work was never judged (FACTSPLIT1a): the work is then read as none, as the whole was.</summary>
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
/// and answers its value, or null where it could not. The work's reads one part (FACTSPLIT1a, <see cref="HelpFacts.ReadWorkAsync"/>).
/// </summary>
public sealed record HelpFactJudges(
    Func<CancellationToken, Task<HelpMachineWhole?>> Machine,
    Func<CancellationToken, Task<SessionLook?>> Sessions,
    Func<CancellationToken, Task<IReadOnlyList<HistoryReading>?>> History,
    Func<HelpWorkPart, CancellationToken, Task<HelpWork?>> Work)
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
