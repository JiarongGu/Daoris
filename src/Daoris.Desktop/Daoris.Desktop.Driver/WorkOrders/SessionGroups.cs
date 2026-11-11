using System.Globalization;
using System.Text.Json;
using static Daoris.Driver.Json;

namespace Daoris.Driver;


/// <summary>
/// The one reader of a session's group: which of the five states each session is in (<see cref="SessionStateReader"/>), or the
/// Archived fold, the word its row shows, and what its second line says. <c>SESSION_GROUPS</c> hands it to the page and the
/// terminal's <c>sessions</c> prints it, so the two cannot disagree.
/// </summary>
/// <remarks>
/// <b>Pure.</b> <see cref="Read"/> decides from a <see cref="SessionLook"/> and touches nothing; what it reads is
/// gathered by the door that asks (<see cref="JudgeAsync"/>, <see cref="VerdictsAsync"/>), so the table of cases holds
/// the rule without a process. Parked and awaiting reply are the planner's verdicts on the session's quest, read for the
/// quest's last session here only; nothing here counts strikes or decides a wait a second way.
/// </remarks>
public static class SessionGroups
{
    public static IReadOnlyList<SessionGrouping> Read(IKnowledgeSessionLook look, IReadOnlyCollection<string>? only = null) =>
        Read((SessionLook)look, only);

    public static IReadOnlyList<string> TreesToJudge(IKnowledgeSessionLook look, Func<string, bool> held, IReadOnlyCollection<string>? only = null) =>
        TreesToJudge((SessionLook)look, held, only);

    /// <summary>The entries over the rows <see cref="Read"/> answers: <see cref="SessionEntries.Read"/>.</summary>
    public static IReadOnlyList<SessionEntry> ReadEntries(SessionLook look, IReadOnlyCollection<string>? only = null) =>
        SessionEntries.Read(look, only);

    /// <summary>
    /// Whether a live record waits on the person's answer, a park that <i>needs you</i>: one nobody answered, since an
    /// answered one goes on at the driver's next look, and not a teammate's, whose park waits on them.
    /// One rule, which Ask Daoris's room lists its waiting sessions by.
    /// </summary>
    /// <param name="answered">Whether the person has said a word to the park (a person's word among its said words).</param>
    public static bool WaitsOnYou(string id, string state, bool answered) =>
        state == "awaiting-person" && !answered && !id.Contains('/');

    /// <summary>Every session's place, in the order a list shows them; with <paramref name="only"/>, those sessions' alone.</summary>
    /// <param name="only">The sessions asked about. The rest still decide theirs: a tree is one session's to land.</param>
    public static IReadOnlyList<SessionGrouping> Read(SessionLook look, IReadOnlyCollection<string>? only = null)
    {
        var facts = new SessionStateReader(look);
        var placed = look.Records
            .Where(record => only is null || only.Contains(record.Id, StringComparer.Ordinal))
            .Select(record => (Record: record, Row: facts.Place(record)))
            .ToList();

        return [.. SessionGroup.Order.SelectMany(group => Ordered(group, placed.Where(each => each.Row.Group == group))).Select(each => each.Row)];
    }

    /// <summary>
    /// The quests the planner parked on their failed sessions, each with its last session here, its number, that session's
    /// note and when it ended, in the planner's order.
    /// </summary>
    /// <remarks>
    /// Read from the facts a *parked* row is placed by (the planner's verdict, the last run, the strikes), so Overview's row,
    /// the park's notice and the list's row name one session and count one number. A person's stop is no park: it holds
    /// its quest by <see cref="StartVerdict.Stopped"/>, which the person caused.
    /// </remarks>
    public static IReadOnlyList<QuestPark> Parks(SessionLook look)
    {
        var records = look.Records
            .GroupBy(record => record.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        return [.. look.Considered
            .Where(consideration => consideration.Verdict == StartVerdict.Exhausted)
            .DistinctBy(consideration => consideration.Quest.Id, StringComparer.OrdinalIgnoreCase)
            .Select(consideration =>
            {
                var quest = consideration.Quest.Id;
                var last = look.LastRun.TryGetValue(quest, out var run) ? run : null;
                var ended = last is not null && records.TryGetValue(last.Session, out var record) && record.Updated != DateTimeOffset.MinValue
                    ? record.Updated
                    : (DateTimeOffset?)null;
                return new QuestPark(quest, consideration.Quest.To)
                {
                    Session = last?.Session,
                    Strikes = look.Strikes.TryGetValue(quest, out var strikes) ? strikes : null,
                    Note = string.IsNullOrWhiteSpace(last?.Note) ? null : last.Note.Trim(),
                    NoteParts = string.IsNullOrWhiteSpace(last?.Note) ? null : last.NoteParts,
                    Since = ended,
                };
            })];
    }

    /// <summary>
    /// The trees <see cref="Read"/> would offer to land, or show beside a run's wait, if they held work: an ended session's own
    /// tree, this home's, the newest session on it and held by nothing live. A tree its quest goes back into is asked too (a
    /// cut-off's, a parked quest's, an asker's), since its commits are offered to land whatever its ending. Only these are worth a git walk.
    /// </summary>
    /// <param name="held">Whether a path is a tree this home opened (<see cref="SessionTrees.Holds"/>): the only kind looked at.</param>
    public static IReadOnlyList<string> TreesToJudge(
        SessionLook look, Func<string, bool> held, IReadOnlyCollection<string>? only = null)
    {
        var facts = new SessionStateReader(look);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var trees = new List<string>();
        foreach (var record in look.Records)
        {
            if (only is not null && !only.Contains(record.Id, StringComparer.Ordinal)) continue;
            if (facts.LandableTree(record) is not { } tree || !seen.Add(Normal(tree)) || !Held(held, tree)) continue;
            trees.Add(tree);
        }

        return trees;
    }

    /// <summary>The look with each tree <see cref="TreesToJudge"/> names judged once: a tree the judge cannot speak for holds nothing to review.</summary>
    /// <param name="judge"><see cref="SessionTrees.WorkAsync"/>, the git walk; a test hands in its own.</param>
    /// <param name="atOnce">How many trees are judged at once; one after another unless a caller says more.</param>
    public static async Task<SessionLook> JudgeAsync(
        SessionLook look, Func<string, bool> held, Func<string, CancellationToken, Task<TreeWork?>> judge,
        IReadOnlyCollection<string>? only = null, CancellationToken ct = default, int atOnce = 1)
    {
        var trees = TreesToJudge(look, held, only);
        var works = new TreeWork?[trees.Count];
        if (atOnce <= 1)
        {
            for (var at = 0; at < trees.Count; at++) works[at] = await judge(trees[at], ct).ConfigureAwait(false);
        }
        else
        {
            // Each tree's judgement is its own git, so a few at once overlap their runs; the starts still pass the one gate.
            await Parallel.ForAsync(0, trees.Count, new ParallelOptions { MaxDegreeOfParallelism = atOnce, CancellationToken = ct },
                async (at, token) => works[at] = await judge(trees[at], token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        var judged = new Dictionary<string, TreeWork>(look.Trees, StringComparer.OrdinalIgnoreCase);
        for (var at = 0; at < trees.Count; at++)
        {
            if (works[at] is { } work) judged[Normal(trees[at])] = work;
        }

        return look with { Trees = judged };
    }

    /// <summary>
    /// The planner's verdict on each quest, never a second copy of its rules: the loop's last look where a loop has
    /// looked, and the planner over a fresh snapshot where none has (a terminal, or a desktop waiting on another driver's
    /// lock).
    /// </summary>
    /// <param name="unadopted">Whether the configured adapter may drive a repository that never adopted (<see cref="Adoption"/>).</param>
    public static async Task<IReadOnlyList<Consideration>> VerdictsAsync(
        ServiceClient service, DriverConfig config, bool unadopted, IReadOnlyList<Consideration>? lastLook, CancellationToken ct = default,
        string? home = null)
    {
        if (lastLook is not null) return lastLook;
        // A fresh plan reads the pauses as the loop's look does, so the list and a tick say one verdict.
        var snapshot = await PausedWork.LookAsync(service, config, await service.SnapshotAsync(ct).ConfigureAwait(false), ct).ConfigureAwait(false);
        // And the typed holds a role's policy leaves, read from the home's store, so a plan with no look says the hold the look says.
        if (home is not null) snapshot = StaffSnapshotCourse.Held(new StaffLedger(home), snapshot, config, DateTimeOffset.UtcNow);
        return Planner.Plan(snapshot, config, unadopted);
    }

    /// <summary>
    /// One look at the sessions, gathered for the reader: the records, every quest, the planner's verdicts, the
    /// trees judged, the archive marks and what this machine still holds of each. The screen's
    /// <c>SESSION_GROUPS</c> and the terminal's <c>sessions</c> both gather here, so they cannot disagree.
    /// </summary>
    /// <param name="lastLook">The loop's last look, where a loop has looked; null plans over a fresh snapshot.</param>
    /// <param name="coolingOf">
    /// The cool-off a start on an adapter would read for an account: the home's, by <see cref="CoolingOn"/>, where
    /// none is handed.
    /// </param>
    /// <param name="turnEnded">
    /// The word of the runner in this process on an open conversation's turns (<see cref="ChatRunner.TurnEnded"/>):
    /// null where it holds none, and none handed where this process runs no conversation, as the terminal's listing.
    /// </param>
    public static async Task<SessionLook> LookAsync(
        ServiceClient service, DriverConfig config, bool unadopted, IReadOnlyList<Consideration>? lastLook, string home,
        IReadOnlyCollection<string>? only = null, CancellationToken ct = default, Func<string?, string?, string?, CoolingEntry?>? coolingOf = null,
        LookTimes? times = null, Func<string, bool?>? turnEnded = null)
    {
        // Each step timed where the caller asked; a look nobody times runs its steps untimed.
        var asked = times is not null;
        times ??= new LookTimes();
        var records = await times.TimeAsync("records", () => service.SessionRecordsJsonAsync(ct)).ConfigureAwait(false);
        var quests = await times.TimeAsync("quests", () => service.EveryQuestAsync(ct)).ConfigureAwait(false);
        var considered = await times.TimeAsync("verdicts", () => VerdictsAsync(service, config, unadopted, lastLook, ct, home)).ConfigureAwait(false);
        var trees = new SessionTrees(home);
        var look = times.Time("marks", () =>
        {
            var gathered = SessionLook.From(records, quests, considered, config.ForgivenAt) with
            {
                Archived = new SessionArchive(home).Marks(),
            };
            // What each quest's last session here, and the one its reopen names, could not go on with,
            // read as the planner's snapshot reads it.
            gathered = gathered with { Unable = new GoOnMarks(home).For(gathered.LastRun.Values.Concat(gathered.NamedByReopen.Values)) };
            // The open conversations between turns, whose trees offer their work to land.
            gathered = gathered with
            {
                Between = Between(gathered.Records, turnEnded ?? (_ => null), new SessionEvents(DaorisHome.Entry(home, HomeEntry.Sessions))),
            };
            return gathered with { Kept = new SessionDeletion(home).Kept(gathered.Records) };
        });
        // What holds words the planner cannot see, a chat's runner and a cool-off a fresh plan missed. The home's roster
        // is built only where a record asks it, since most looks hold no words waiting.
        var roster = new Lazy<Func<string?, string?, string?, CoolingEntry?>>(() => CoolingOn(home));
        look = times.Time("waiting", () => Waiting(
            look, records, home, config, fresh: lastLook is null,
            coolingOf ?? ((adapter, profile, model) => roster.Value(adapter, profile, model)), TimeZoneInfo.Local));
        if (asked) times.Trees = TreesToJudge(look, trees.Holds, only).Count;
        return await times.TimeAsync("trees", () => JudgeAsync(look, trees.Holds, trees.WorkAsync, only, ct)).ConfigureAwait(false);
    }

    /// <summary>
    /// The open conversations of this machine's between turns: live, their last turn ended
    /// and nothing said since. A conversation's record stays <c>working</c> between turns, so the record cannot say it.
    /// </summary>
    /// <remarks>
    /// <b>The runner's word first, else its record of its turns.</b> The page's foot offers <i>Accept…</i> by the runner's word on a
    /// conversation's turns, which lives in the process that runs it; the screen's look is taken in that process and reads it
    /// (<paramref name="runner"/>). A terminal's listing, or a conversation another process runs, reads what that runner wrote as each
    /// turn ended and each word came in (<see cref="SessionEvents.TurnEnded"/>), so every door says the same once the turn has ended.
    /// </remarks>
    /// <param name="runner">Whether the runner here says the conversation's turn ended with nothing waiting; null where it holds none.</param>
    public static IReadOnlySet<string> Between(IReadOnlyList<SessionRecord> records, Func<string, bool?> runner, SessionEvents events)
    {
        var between = new HashSet<string>(StringComparer.Ordinal);
        foreach (var record in records)
        {
            if (record is not { Kind: "chat", State: "working", Teammate: false }) continue;
            if (runner(record.Id) ?? events.TurnEnded(record.Id)) between.Add(record.Id);
        }

        return between;
    }

    /// <summary>
    /// What the listing reads from this home where the planner cannot say what holds the person's words:
    /// the ended chats they wait on (<see cref="SessionLook.ChatsWaiting"/>), and what holds each word that waits
    /// (<see cref="SessionLook.Held"/>).
    /// </summary>
    /// <remarks>
    /// <para><b>A chat</b> is its runner's, which takes words up at once and, where something holds them, says so as its
    /// conversation's last line, *it does not go on yet* (<see cref="ResumeWords.NotYet"/>), leaving them for the next word.
    /// That line, still the last, is what holds them, in its words, with the reset where the account it ran on cools, since the
    /// runner asks that account first. Words every one of which it could not go on with (its marks) wait for nothing.</para>
    ///
    /// <para><b>A driven record</b> resumed on its own account waits for that account's reset, which the loop's look
    /// holds the start on and a fresh plan cannot see. Where no loop has looked (<paramref name="fresh"/>), the cool-off is read
    /// as the resume would read it: the planner goes on with the record in its own conversation on the agent its start goes on with
    /// (a hand-over's, else the record's own asked choice's), the person chose no new session, and its account cools. The line is the
    /// one the loop's look would hold it with.</para>
    /// </remarks>
    /// <param name="config">The machine's choices, whose roles a record's words go on with.</param>
    /// <param name="fresh">Whether the verdicts are a fresh plan's, which saw no cool-off; a loop's look holds its own.</param>
    /// <param name="coolingOf">The cool-off a start on an adapter would read for an account and the model its line asks, or null where it is ready.</param>
    public static SessionLook Waiting(
        SessionLook look, string recordsJson, string home, DriverConfig config, bool fresh, Func<string?, string?, string?, CoolingEntry?> coolingOf,
        TimeZoneInfo zone)
    {
        // A record's words go on with its own agent, the one its
        // choice names, as the loop's start asks it, never the machine's `adapter` key; a role that does not read holds nothing here.
        string? GoesOnWith(PriorSession record, string work)
        {
            try
            {
                return StartPlan.ContinuedOn(record, config, work);
            }
            catch (DriverException)
            {
                return null;
            }
        }

        var chats = new HashSet<string>(StringComparer.Ordinal);
        var held = new Dictionary<string, WordsHold>(StringComparer.Ordinal);
        var marks = new GoOnMarks(home);
        var events = new SessionEvents(DaorisHome.Entry(home, HomeEntry.Sessions));
        foreach (var record in look.Records)
        {
            // Ask Daoris's own conversation among them: its runner takes its words up as a chat's.
            if (record.Kind != "chat" || record.Live || record.Teammate || record.State == "stood-down"
                || ServiceClient.ReadRecord(recordsJson, record.Id) is not { WordsWaiting: true } prior
                || GoOnMarks.Judged(prior, marks.Read(record.Id)))
            {
                continue;
            }

            chats.Add(record.Id);
            if (NotYet(events, record.Id) is not { } reason) continue;
            // A conversation's words wait for the earliest reset of any account of its agent, which
            // the hold names, never its own account's alone; Ask Daoris's room alone keeps its own.
            held[record.Id] = Cooling(coolingOf, prior) is { } cooling
                ? CoolingHold(
                    reason,
                    prior.Repository is { } repository && string.Equals(repository, KnowledgeRoomIdentity.Repository, StringComparison.OrdinalIgnoreCase)
                        ? cooling
                        : (prior.Adapter ?? GoesOnWith(prior, ParticipantWork.Chat)) is { } agent ? Soonest(coolingOf, home, agent, cooling, prior.ModelOn(agent)) ?? cooling : cooling,
                    home)
                : new WordsHold(WordsHold.Waits, reason);
        }

        if (fresh)
        {
            var choices = new NewSessionChoices(home);
            var conversations = new HarnessConversations(home);
            foreach (var verdict in look.Considered)
            {
                // The resume's own account, as the loop's start asks for it: words going on in the record they came in on, in its own
                // conversation, by the continuation's one rule (OwnResume), on the agent the start goes on with: a hand-over's, else its record's
                // own.
                if (verdict is not { Verdict: StartVerdict.Start, GoesOn: true, Resumes: { WordsWaiting: true } resumes }
                    || (new HandedQuests(home).Read(verdict.Quest.Id)?.Agent ?? GoesOnWith(resumes, ParticipantWork.Build)) is not { } adapter
                    || OwnResume.Of(verdict, adapter, session => conversations.Read(session)?.Adapter) is not { } words
                    || choices.Covers(words)
                    || Cooling(coolingOf, words) is not { } cooling)
                {
                    continue;
                }

                // The words of an open or taken quest's record never wait for their own account
                // while another is ready, so the loop's look carries them on there and nothing is held; where every account cools, they wait
                // for the earliest reset of any, as the roster's resume says. A closed quest's session's words the same, the
                // shell's chat runner going on in a new part of it; only the door out of the wait differs.
                // A closed quest's session goes on in a new part only with a word that part carries (the person's, or Daoris's own
                // that states a fact); with none, as Daoris's resume words only, its words wait for the own account, as the driver's look says.
                var carried = verdict.Quest.Status is "Open" or "Taken";
                var names = AccountNames.Of(home, cooling.Agent);
                if (!carried && !words.Waiting.Any(DaorisWords.NewPartCarries))
                {
                    held[words.Session] = CoolingHold(
                        $"{ResumeWords.Waits(cooling, zone, names)} {ResumeWords.ChatDoor(words.Session)}", cooling, home);
                    continue;
                }

                if (Soonest(coolingOf, home, adapter, cooling, words.ModelOn(adapter)) is not { } soonest) continue;

                // Where the own account is not the earliest, the sentence is the roster's: whose reset comes first.
                var sentence = ReferenceEquals(LineAccount.Earliest(ResetSet.Switch, own: cooling, walked: soonest), cooling)
                    ? ResumeWords.Waits(cooling, zone, names)
                    : ResumeWords.LimitWaits(soonest, zone, names).Note;
                cooling = soonest;

                var door = carried ? ResumeWords.NewSessionDoor(words.Session) : ResumeWords.ChatDoor(words.Session);
                held[words.Session] = CoolingHold($"{sentence} {door}", cooling, home);
            }
        }

        return look with { ChatsWaiting = chats, Held = held };
    }

    /// <summary>The home's cool-offs as a start reads them (see <see cref="SessionCooling.On"/>).</summary>
    public static Func<string?, string?, string?, CoolingEntry?> CoolingOn(string home) => SessionCooling.On(home);

    private static WordsHold CoolingHold(string reason, CoolingEntry cooling, string home) => SessionCooling.Hold(reason, cooling, home);

    private static CoolingEntry? Soonest(Func<string?, string?, string?, CoolingEntry?> coolingOf, string home, string adapter, CoolingEntry own, string? model) =>
        SessionCooling.Soonest(coolingOf, home, adapter, own, model);

    private static CoolingEntry? Cooling(Func<string?, string?, string?, CoolingEntry?> coolingOf, PriorSession record) => SessionCooling.Of(coolingOf, record);

    private static string? NotYet(SessionEvents events, string session) => SessionCooling.NotYet(events, session);

    /// <summary>A tree path as one tree: separators and a trailing one aside, compared without case, as Windows sees it.</summary>
    public static string Normal(string tree) => TreeNames.Normal(tree);

    private static bool Held(Func<string, bool> held, string tree)
    {
        try
        {
            return held(tree);
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // A path no folder could have is no tree of this home's.
            return false;
        }
    }

    /// <summary>
    /// A group's rows in its order: needing you and pending oldest first, the longest wait first; working by repository then
    /// start, so rows do not reshuffle as their states move; failed, done and archived newest first.
    /// </summary>
    private static IEnumerable<(SessionRecord Record, SessionGrouping Row)> Ordered(
        string group, IEnumerable<(SessionRecord Record, SessionGrouping Row)> rows) => group switch
        {
            SessionGroup.NeedsYou or SessionGroup.Pending => rows
                .OrderBy(each => each.Record.Updated).ThenBy(each => each.Record.Id, StringComparer.Ordinal),
            SessionGroup.Working => rows
                .OrderBy(each => each.Record.Repository, StringComparer.OrdinalIgnoreCase)
                .ThenBy(each => each.Record.Created).ThenBy(each => each.Record.Id, StringComparer.Ordinal),
            _ => rows.OrderByDescending(each => each.Record.Updated).ThenBy(each => each.Record.Id, StringComparer.Ordinal),
        };

}
