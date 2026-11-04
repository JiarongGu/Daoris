namespace Daoris.Driver;

/// <summary>
/// The facts Ask Daoris's room is written from (PARALLEL1a, D181 points 1-2): their topics on <see cref="MachineFacts"/>, the rules
/// each is judged by, which of the loop's wakes moves each, and the room's reading of them, where what was never judged is said
/// <i>not read yet</i>. A start reads them as last judged and writes its room at once; it never reads the machine itself.
/// </summary>
/// <remarks>
/// <para>The trees' parts are <see cref="HelpTreeFacts"/>, the work's and the clean-up's <see cref="HelpWorkFacts"/>; the records they
/// pass are in <c>HelpWorkRecords.cs</c> and <c>HelpTreesRecords.cs</c>. This hub keeps the topics, their rules, the wakes, the
/// registration and the room's reading, and the doors callers reach the parts by.</para>
///
/// <para><b>Per repository, the line.</b> The checkout's guess at a repository's line (<see cref="LineGuesses"/>) is judged per
/// repository on its <c>refs</c> wake, so a ref moved re-judges that repository alone. What the person set is read from the
/// configuration as the room is written, and needs no judgement.</para>
///
/// <para><b>Per repository, the work</b> (FACTSPLIT1a, D181 points 2-3). Each repository with a checkout here has its part of the
/// work inventory, its trees, its records and its checkout's refs alone, judged on its <c>refs</c> wake; one more part holds every
/// repository the trees or the records name with no checkout here (<see cref="NoCheckout"/>). The room reads them merged.</para>
///
/// <para><b>Per repository, the sessions' trees</b> (FACTSPLIT1b). What each ended session's tree holds is judged with git by the
/// same parts, each its own repository's sessions' trees, on its <c>refs</c> wake and as the look lands with its trees moved; the
/// look without them is one subject, which no ref moves. The room reads the look with every part's trees merged in, and since
/// PUSH1c1 the Sessions list's door reads them beside a look of its own (<see cref="WithTreesAsync"/>), so no window's tick walks git.</para>
///
/// <para><b>Per repository, the clean-up's list</b> (PUSH1c4a, D182). Each repository with a checkout here has its part of the list's
/// git half, judged on its <c>refs</c> wake by the pull-request answers the landing record keeps, never by asking a plugin: the
/// person's look still asks, and its kept answer is read at the part's next judgement. The room reads none of it; a slice pushes it.</para>
///
/// <para><b>Whole, the rest.</b> The service's answers and the roster, the sessions' look without its trees and each workspace's
/// history are each gathered across the home by their own readers, so each is one subject, judged on its wakes, at its deadline,
/// and at most once each <see cref="FactRules.Rest"/>.</para>
/// </remarks>
public static class HelpFacts
{
    /// <summary>The service's answers and the roster (<see cref="HelpMachineWhole"/>), one subject, <see cref="Whole"/>.</summary>
    public const string Machine = "help.machine";

    /// <summary>A checkout's guess at its line (<see cref="HelpLineGuess"/>), by repository.</summary>
    public const string Lines = "help.lines";

    /// <summary>
    /// The sessions as the Sessions list looks at them (<see cref="IKnowledgeSessionLook"/>), one subject, without their trees' judgement since
    /// FACTSPLIT1b: the records, the quests, the verdicts and the marks, which hold no git.
    /// </summary>
    public const string Sessions = "help.sessions";

    /// <summary>
    /// What each ended session's tree holds (<see cref="HelpTrees"/>, FACTSPLIT1b), a part by repository with a checkout here, and one
    /// for those with none (<see cref="NoCheckout"/>), as the work's parts.
    /// </summary>
    public const string Trees = "help.trees";

    /// <summary>What each workspace keeps of finished work (a list of <see cref="HistoryReading"/>), one subject.</summary>
    public const string History = "help.history";

    /// <summary>
    /// Where each unit of work has got to (<see cref="HelpWork"/>), a part by repository with a checkout here, and one for those with
    /// none (<see cref="NoCheckout"/>).
    /// </summary>
    public const string Work = "help.work";

    /// <summary>
    /// The clean-up's list, its git half (<see cref="SweepPlan"/>, PUSH1c4a, D182): a part by repository with a checkout here, each judged
    /// by the pull-request answers the landing record keeps and asking no plugin (D148 point 2). None for the repositories with none,
    /// which the list never judges. A second topic beside the work's, so the work's judge stays as it is.
    /// </summary>
    public const string Sweep = "help.sweep";

    /// <summary>
    /// What each line waits on (<see cref="HelpWaits"/>), one subject: the exchanges' one read over the helper, piece and hand-over
    /// files, the quests and the records, which a session's records, a quest or a word moving moves.
    /// </summary>
    public const string Exchanges = "help.exchanges";

    /// <summary>
    /// The exchanges' read: a few files and the service's records and quests, so a minute behind at most, as the machine's answers, and
    /// resting a few seconds between the judgements a burst of ledger moves asks for.
    /// </summary>
    public static readonly FactRules ExchangesRules = new(Deadline: TimeSpan.FromMinutes(1), Bound: TimeSpan.FromSeconds(30), AtOnce: 1)
    {
        Rest = TimeSpan.FromSeconds(5),
    };

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

    /// <summary>
    /// A repository's part of the trees (FACTSPLIT1b): about five git starts for each of its ended sessions' trees, so two repositories
    /// at once and bounded at minutes, as the work's; a burst of one repository's refs while a session commits is one judgement each
    /// half minute, while another repository's are judged at once.
    /// </summary>
    public static readonly FactRules TreesRules = new(Deadline: TimeSpan.FromMinutes(5), Bound: TimeSpan.FromMinutes(2), AtOnce: 2)
    {
        Rest = TimeSpan.FromSeconds(30),
    };

    /// <summary>
    /// A repository's part of the clean-up's list (PUSH1c4a): its refs read once, its trees listed once, and a few git starts for each
    /// session branch and each landing's branch, so the work's rules, which it is modelled on until the install measures it (D182 point
    /// 8): two repositories at once, bounded at minutes, a committing session's burst of refs one judgement each half minute.
    /// </summary>
    public static readonly FactRules SweepRules = new(Deadline: TimeSpan.FromMinutes(5), Bound: TimeSpan.FromMinutes(2), AtOnce: 2)
    {
        Rest = TimeSpan.FromSeconds(30),
    };

    private static readonly IReadOnlyList<(string Topic, string Subject)> None = [];

    /// <summary>
    /// The facts a loop's wake moves (D181 point 2): a ref of one repository its line, its part of the work, its part of the trees and
    /// its part of the clean-up's list, never the sessions' look, which holds no git; a ledger family the service's answers, and the
    /// sessions' look, the history and every part of the work and of the list it records, as <paramref name="facts"/>' answers name
    /// them; the configuration the service's answers, the sessions' verdicts and every part of the trees and of the list, whose line it
    /// may set. A press, a word or the window's focus moves none.
    /// </summary>
    /// <remarks>
    /// The trees' parts are not woken with the ledger (FACTSPLIT1b): which trees a part judges is read from the look, so a part woken
    /// with it would judge the look the wake is about to replace. They are judged as that look lands, each whose trees moved in it
    /// (<see cref="Keep"/>).
    /// </remarks>
    public static IReadOnlyList<(string Topic, string Subject)> Woken(string topic, string subject, MachineFactsSnapshot facts)
    {
        switch (topic)
        {
            case ChangeTopics.Refs:
                return [(Lines, subject), (Trees, subject), (Work, subject), (Sweep, subject)];
            case ChangeTopics.Config:
                // A line set moves what holds a tree's commits by their content, so every part of the trees, as the whole look was,
                // and every part of the clean-up's list, which judges each branch against the line.
                return facts.Value<HelpMachineWhole>(Machine, Whole) is { } named
                    ? [(Machine, Whole), (Sessions, Whole), .. Parts(named).Select(part => (Trees, part)), .. SweepSubjects(named).Select(part => (Sweep, part))]
                    : [(Machine, Whole), (Sessions, Whole)];
            case ChangeTopics.Ledger:
                var moved = subject.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                bool Moved(string family) => moved.Contains(family, StringComparer.Ordinal);
                var woken = new List<(string, string)> { (Machine, Whole) };
                if (Moved("sessions") || Moved("quests")) woken.AddRange([(Sessions, Whole), (History, Whole)]);
                else
                {
                    // PUSH1d1b: a word kept without a note changes what the sessions' look reads, and nothing the history counts.
                    if (Moved("words")) woken.Add((Sessions, Whole));
                    if (Moved("asks")) woken.Add((History, Whole));
                }
                // Which repository's records moved is not said, so every part, as the whole was. Before the answers there is none:
                // each is judged as they land.
                if (Moved("sessions") && facts.Value<HelpMachineWhole>(Machine, Whole) is { } whole)
                {
                    woken.AddRange(Parts(whole).Select(part => (Work, part)));
                    woken.AddRange(SweepSubjects(whole).Select(part => (Sweep, part)));
                }

                // A request's state moves with its asker's or its taker's record, a quest, or the word that answered it.
                if (Moved("sessions") || Moved("quests") || Moved("words")) woken.Add((Exchanges, Whole));
                return woken;
            default:
                return None;
        }
    }

    /// <summary>Wake the facts <paramref name="topic"/>'s <paramref name="subject"/> moves (<see cref="Woken"/>).</summary>
    /// <param name="mine">
    /// A ref this process moved itself, a landing or an <i>Accept</i> (OWNMOVES1): that repository's parts of the trees, of the work and
    /// of the clean-up's list (PUSH1c4a) are judged at once, past the rest a burst of a committing session's refs is held to, so a landed
    /// tree's offer leaves within one judgement. Its line has no rest, and any other topic's wake is as it was.
    /// </param>
    public static void Wake(MachineFacts facts, string topic, string subject, bool mine = false)
    {
        var afresh = mine && topic == ChangeTopics.Refs;
        foreach (var (factTopic, factSubject) in Woken(topic, subject, facts.Current))
        {
            facts.Wake(factTopic, factSubject, afresh && factTopic is Trees or Work or Sweep);
        }
    }

    /// <summary>
    /// Keep the room's facts on <paramref name="facts"/>: each topic registered with its judge, and the whole ones known, so they are
    /// judged once the facts start. The service's answers make each repository with a checkout here a subject of the lines and of
    /// the work, and the rest one part of the work, and drop the line and the part of one no longer registered; with the sessions'
    /// look they make the trees' parts alike (FACTSPLIT1b).
    /// </summary>
    public static void Keep(MachineFacts facts, HelpFactJudges judges)
    {
        facts.Topic(Machine, MachineRules, async (_, ct) => await judges.Machine(ct).ConfigureAwait(false));
        facts.Topic(Lines, LinesRules, (repository, ct) => JudgeLineAsync(facts, repository, judges.Guess, ct));
        facts.Topic(Sessions, WholeRules, async (_, ct) => await judges.Sessions(ct).ConfigureAwait(false));
        facts.Topic(Trees, TreesRules, (part, ct) => HelpTreeFacts.JudgeAsync(facts, part, judges.Trees, ct));
        facts.Topic(History, WholeRules, async (_, ct) => await judges.History(ct).ConfigureAwait(false));
        facts.Topic(Work, WorkRules, (part, ct) => HelpWorkFacts.JudgeAsync(facts, part, judges.Work, ct));
        facts.Topic(Sweep, SweepRules, (part, ct) => HelpWorkFacts.JudgeSweepAsync(facts, part, judges.Sweep, ct));
        facts.Topic(Exchanges, ExchangesRules, async (_, ct) => await judges.Exchanges(ct).ConfigureAwait(false));

        // Once the service's answers have landed, never before: a line's and a part's judges read the checkout from them, so one
        // started first would find none and keep nothing until its deadline. The trees' parts once the look has too, which names
        // their trees. The newest answers and look, read under one gate, so those told after newer ones passed over never undo what
        // the newer made. A look dropped (a judge that could not read it) keeps the last one to compare the next with.
        var gate = new object();
        HelpMachineWhole? seen = null;
        IKnowledgeSessionLook? looked = null;
        facts.Changed += _ =>
        {
            lock (gate)
            {
                var current = facts.Current;
                if (current.Value<HelpMachineWhole>(Machine, Whole) is not { } whole) return;
                var look = current.Value<IKnowledgeSessionLook>(Sessions, Whole) ?? looked;
                if (ReferenceEquals(seen, whole) && ReferenceEquals(looked, look)) return;
                var (before, lookedBefore) = (seen, looked);
                (seen, looked) = (whole, look);
                if (!ReferenceEquals(before, whole)) Known(facts, before, whole);
                if (look is not null) TreesKnown(facts, before ?? whole, lookedBefore, whole, look);
                if (look is not null && !ReferenceEquals(lookedBefore, look)) RecordedKnown(facts, whole, lookedBefore, look);
            }
        };
        foreach (var topic in (string[])[Machine, Sessions, History, Exchanges]) facts.Subject(topic, Whole);
    }

    /// <summary>
    /// The trees' parts the service's answers name (FACTSPLIT1b), as the work's: one with nothing judged yet is judged now, one whose
    /// trees moved in the look or the answers is judged again at once, and one no longer named is dropped.
    /// </summary>
    /// <remarks>
    /// A part whose trees moved is asked afresh, not held by its rest: a session's last commit judged its repository's part a moment
    /// before its ending put its tree among those to judge, and the room would say its work nothing to review until the rest ended.
    /// It happens once a look at most, which rests in turn.
    /// </remarks>
    private static void TreesKnown(MachineFacts facts, HelpMachineWhole before, IKnowledgeSessionLook? lookedBefore, HelpMachineWhole whole, IKnowledgeSessionLook look)
    {
        var checkouts = Checkouts(whole);
        foreach (var part in Parts(whole))
        {
            if (facts.Current.Of(Trees, part) is null) facts.Wake(Trees, part);
            else if (lookedBefore is not null && HelpTreeFacts.Basis(before, lookedBefore, part) != HelpTreeFacts.Basis(whole, look, part)) _ = facts.JudgedAsync(Trees, part, CancellationToken.None);
            else facts.Subject(Trees, part);
        }

        foreach (var gone in facts.Current.Subjects(Trees).Where(part => part != NoCheckout && !checkouts.Contains(part))) facts.Forget(Trees, gone);
    }

    /// <summary>
    /// The parts of the work whose sessions' recorded trees moved in the look (OVERVIEWNOISE1b), each judged again, so a tree joins the
    /// session that recorded it once the look names it: the ledger's wake may judge a part before the look it woke lands.
    /// </summary>
    private static void RecordedKnown(MachineFacts facts, HelpMachineWhole whole, IKnowledgeSessionLook? lookedBefore, IKnowledgeSessionLook look)
    {
        foreach (var part in Parts(whole))
        {
            if (facts.Current.Of(Work, part) is not null && HelpWorkFacts.RecordedBasis(whole, lookedBefore, part) != HelpWorkFacts.RecordedBasis(whole, look, part)) facts.Wake(Work, part);
        }
    }

    /// <summary>
    /// The repositories the service's answers name with a checkout here, made subjects of the lines, the work and the clean-up's list,
    /// and the rest a part of the work: one with nothing judged yet is judged now (a wake before the answers found no checkout to ask), a
    /// part whose reading of the answers moved is judged again (<see cref="HelpWorkFacts.Basis"/>, <see cref="HelpWorkFacts.SweepBasis"/>),
    /// and one no longer named is dropped.
    /// </summary>
    private static void Known(MachineFacts facts, HelpMachineWhole? before, HelpMachineWhole whole)
    {
        var checkouts = Checkouts(whole);
        foreach (var repository in checkouts)
        {
            if (facts.Current.Of(Lines, repository) is null) facts.Wake(Lines, repository);
            else facts.Subject(Lines, repository);
        }

        foreach (var part in Parts(whole))
        {
            if (facts.Current.Of(Work, part) is null || (before is not null && HelpWorkFacts.Basis(before, part) != HelpWorkFacts.Basis(whole, part))) facts.Wake(Work, part);
            else facts.Subject(Work, part);
        }

        foreach (var repository in SweepSubjects(whole))
        {
            if (facts.Current.Of(Sweep, repository) is null || (before is not null && HelpWorkFacts.SweepBasis(before, repository) != HelpWorkFacts.SweepBasis(whole, repository)))
            {
                facts.Wake(Sweep, repository);
            }
            else
            {
                facts.Subject(Sweep, repository);
            }
        }

        foreach (var gone in facts.Current.Subjects(Lines).Where(repository => !checkouts.Contains(repository))) facts.Forget(Lines, gone);
        foreach (var gone in facts.Current.Subjects(Work).Where(part => part != NoCheckout && !checkouts.Contains(part))) facts.Forget(Work, gone);
        foreach (var gone in facts.Current.Subjects(Sweep).Where(repository => !checkouts.Contains(repository))) facts.Forget(Sweep, gone);
    }

    /// <summary>The repositories the service's answers name with a checkout here, without case.</summary>
    internal static HashSet<string> Checkouts(HelpMachineWhole whole) => whole.Snapshot.Repositories
        .Where(known => !string.IsNullOrWhiteSpace(known.Root))
        .Select(known => known.Repository)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The parts the service's answers name, of the work (FACTSPLIT1a) and of the trees (FACTSPLIT1b): each repository with a checkout
    /// here by name, then the rest's.
    /// </summary>
    internal static IReadOnlyList<string> Parts(HelpMachineWhole whole) => [.. Checkouts(whole).Order(StringComparer.Ordinal), NoCheckout];

    /// <summary>
    /// The parts of the clean-up's list the service's answers name (PUSH1c4a): each repository with a checkout here by name, and no
    /// rest's, since the list judges no repository without one.
    /// </summary>
    internal static IReadOnlyList<string> SweepSubjects(HelpMachineWhole whole) => [.. Checkouts(whole).Order(StringComparer.Ordinal)];

    /// <summary>What <paramref name="subject"/>'s part of the trees reads (<see cref="HelpTreeFacts.PartOf"/>).</summary>
    public static HelpTreesPart? TreesPartOf(HelpMachineWhole whole, IKnowledgeSessionLook look, string subject) =>
        HelpTreeFacts.PartOf(whole, look, subject);

    /// <summary>One part of the trees read from <paramref name="home"/> (<see cref="HelpTreeFacts"/>).</summary>
    public static async Task<HelpTrees> ReadTreesAsync(string home, HelpTreesPart part, CancellationToken ct, LookTimes? times = null) =>
        await HelpTreeFacts.ReadAsync(home, part, ct, times).ConfigureAwait(false);

    /// <summary>One part of the trees through a stand-in judge (<see cref="HelpTreeFacts"/>).</summary>
    public static Task<HelpTrees> ReadTreesAsync(
        HelpTreesPart part, Func<string, bool> held, Func<string, CancellationToken, Task<TreeWork?>> judge, CancellationToken ct,
        LookTimes? times = null, int atOnce = 1, Func<string, bool>? walkable = null) =>
        HelpTreeFacts.ReadAsync(part, held, judge, ct, times, atOnce, walkable);

    /// <summary>How many of a part's trees are read at once (<see cref="HelpTreeFacts.AtOnce"/>).</summary>
    public const int TreesAtOnce = HelpTreeFacts.AtOnce;

    /// <summary>The longest a door reading the trees waits for the parts' first judgement (<see cref="HelpTreeFacts.First"/>).</summary>
    public static readonly TimeSpan TreesFirst = HelpTreeFacts.First;

    /// <summary>A look read by a door, with its trees as the parts judged them (<see cref="HelpTreeFacts.WithTreesAsync"/>).</summary>
    public static Task<IKnowledgeSessionLook> WithTreesAsync(
        MachineFacts facts, IKnowledgeSessionLook look, Func<string, bool> held, Func<string, CancellationToken, Task<TreeWork?>> walk,
        IReadOnlyCollection<string>? only = null, TimeSpan? first = null, CancellationToken ct = default) =>
        HelpTreeFacts.WithTreesAsync(facts, look, held, walk, only, first, ct);

    /// <summary>Each of <paramref name="asked"/> as the parts judged it (<see cref="HelpTreeFacts.Judged"/>).</summary>
    public static HelpTreesJudged TreesJudged(MachineFactsSnapshot facts, IReadOnlyList<string> asked) => HelpTreeFacts.Judged(facts, asked);

    /// <summary>What <paramref name="subject"/>'s part of the work reads (<see cref="HelpWorkFacts.PartOf"/>).</summary>
    public static HelpWorkPart? PartOf(HelpMachineWhole whole, string subject, IKnowledgeSessionLook? look = null) =>
        HelpWorkFacts.PartOf(whole, subject, look);

    /// <summary>One part of the work read from <paramref name="home"/> (<see cref="HelpWorkFacts.ReadAsync"/>).</summary>
    public static Task<HelpWork> ReadWorkAsync(string home, HelpWorkPart part, CancellationToken ct) => HelpWorkFacts.ReadAsync(home, part, ct);

    /// <summary>What <paramref name="subject"/>'s part of the clean-up's list reads (<see cref="HelpWorkFacts.SweepPartOf"/>).</summary>
    public static HelpSweepPart? SweepPartOf(HelpMachineWhole whole, string subject) => HelpWorkFacts.SweepPartOf(whole, subject);

    /// <summary>One part of the clean-up's list read from <paramref name="home"/> (<see cref="HelpWorkFacts.ReadSweepAsync"/>).</summary>
    public static Task<IKnowledgeSweep> ReadSweepAsync(string home, HelpSweepPart part, CancellationToken ct) => HelpWorkFacts.ReadSweepAsync(home, part, ct);

    /// <summary>The work's parts as a slice pushes them (<see cref="HelpWorkFacts"/>).</summary>
    public static IReadOnlyList<(string Part, HelpWork? Work)>? WorkParts(MachineFactsSnapshot facts) => HelpWorkFacts.Parts(facts);

    /// <summary>The clean-up's list's parts as a slice pushes them (<see cref="HelpWorkFacts.SweepParts"/>).</summary>
    public static IReadOnlyList<(string Part, IKnowledgeSweep? Plan)>? SweepParts(MachineFactsSnapshot facts) => HelpWorkFacts.SweepParts(facts);

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
            if (KnowledgeWorkplace.Current.JudgedLine(config, known.Repository, known.Workspace, known.Root, facts.Value<HelpLineGuess>(Lines, known.Repository))
                is not { } line)
            {
                unread.Add(known.Repository);
                continue;
            }

            lines.Add(new(known.Repository, RemoteTarget.Workspace(known.Workspace), line.Branch, line.Source));
        }

        var sessions = SessionsOf(facts, whole);
        var history = facts.Value<IReadOnlyList<HistoryReading>>(History, Whole);
        var work = HelpWorkFacts.Merged(facts, whole);
        var waits = facts.Value<HelpWaits>(Exchanges, Whole);
        return new HelpFactsRead(whole, lines, sessions, history, work, new HelpNotReadYet(unread)
        {
            Sessions = sessions is null,
            History = history is null,
            Work = work is null,
            Waits = waits is null,
        })
        {
            Waits = waits,
        };
    }

    /// <summary>
    /// The sessions as the room reads them (FACTSPLIT1b), the look as the Sessions list takes it: the look without trees, with each
    /// part's trees merged in by their path (<see cref="KnowledgeWorkOrders.Current.Normal"/>). Null while the look or any part the service's
    /// answers name was never judged, which the room says not read yet, as it said the whole: a tree not judged would read as
    /// holding nothing to review.
    /// </summary>
    internal static IKnowledgeSessionLook? SessionsOf(MachineFactsSnapshot facts, HelpMachineWhole whole)
    {
        if (facts.Value<IKnowledgeSessionLook>(Sessions, Whole) is not { } look) return null;
        var trees = new Dictionary<string, TreeWork>(StringComparer.OrdinalIgnoreCase);
        foreach (var subject in Parts(whole))
        {
            if (facts.Value<HelpTrees>(Trees, subject) is not { } part) return null;
            foreach (var (tree, work) in part.Trees) trees[tree] = work;
        }

        return look.WithTrees(trees);
    }

    /// <summary>
    /// The room's reading as the facts stand: at once where the service's answers were judged; where they never were, once they
    /// first are (HELPSTUCK1's backstop, which the start bounds).
    /// </summary>
    public static async Task<HelpFactsRead?> ReadAsync(MachineFacts facts, DriverConfig config, CancellationToken ct) =>
        Read(facts.Current, config)
        ?? Read(await facts.FirstAsync(Machine, Whole, ct).ConfigureAwait(false), config);
}
