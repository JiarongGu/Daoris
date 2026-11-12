namespace Daoris.Knowledge;

/// <summary>
/// The records' half of clearing finished history from this machine:
/// one judgement for both of a local host's doors, read by the driver's <c>HistoryClearing</c>,
/// which judges the machine's half and removes the home's files.
/// </summary>
/// <remarks>
/// <para><b>Listed first, then pressed</b>: <see cref="PlanAsync"/> and <see cref="PlanWorkspaceAsync"/> list each unit
/// with what it would take or why it stays, and change nothing. <see cref="ClearAsync"/> clears exactly the units named,
/// judging each again inside the one transaction that clears it, so a unit that changed since the list is kept with its word
/// and is never half cleared.</para>
///
/// <para><b>A record that never left the machine simply goes</b> (design §3.1): the quest's row and its log together (H3),
/// its sessions' rows, its ask's row. <b>A quest a remote numbered is forgotten</b> (§3.2): it goes the same way, and its id
/// is kept, so the fetches pass over it. No operation is written and nothing is pushed: the team's copy is untouched.</para>
///
/// <para><b>Nothing here removes a home file but what the service keeps</b>: a quest's and an ask's kept files and settled
/// rule proposals, after the records. A session's transcript, conversation and files are the driver's (§2.2).</para>
/// </remarks>
public sealed class HistoryDesk(
    QuestStore quests, SessionStore sessions, AskStore? asks, KnowledgeService service,
    IRemotes? remotes = null, QuestFiles? files = null, RuleProposalBox? proposals = null, ChannelStore? channel = null)
{
    /// <summary>One unit: what it holds, what a clear would take, and why it would stay. Changes nothing.</summary>
    public async Task<HistoryUnit> PlanAsync(HistoryUnitRef unit, CancellationToken ct = default)
    {
        var wiring = await WiringAsync(ct).ConfigureAwait(false);
        return await JudgeAsync(unit, await LookAsync(within: false, ct).ConfigureAwait(false), wiring, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// A workspace's finished history, unit by unit: each ask whose work closed or that is closed, then each
    /// closed quest no ask here asked, oldest first, each once. A question rides with the work that asked it, and a quest
    /// with its ask. Changes nothing. Any name is read, so a workspace no page shows any more is still listed by its records.
    /// </summary>
    public async Task<IReadOnlyList<HistoryUnit>> PlanWorkspaceAsync(string workspace, CancellationToken ct = default)
    {
        var wiring = await WiringAsync(ct).ConfigureAwait(false);
        var look = await LookAsync(within: false, ct).ConfigureAwait(false);
        var circle = Workspaces.Normalize(workspace);
        var held = look.Asks.Select(ask => ask.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var covered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var units = new List<HistoryUnit>();

        foreach (var ask in look.Asks
                     .Where(ask => Workspaces.Same(ask.Workspace, circle))
                     .OrderBy(ask => ask.Asked).ThenBy(ask => ask.Id, StringComparer.Ordinal))
        {
            if (AskDesk.Standing(ask, AskedBy(look, ask)).State is not (AskState.Closed or AskState.Done)) continue;
            var unit = await JudgeAsync(new HistoryUnitRef(HistoryUnitKind.Ask, ask.Id), look, wiring, ct).ConfigureAwait(false);
            covered.UnionWith(unit.Quests);
            units.Add(unit);
        }

        var candidates = new List<HistoryUnit>();
        foreach (var quest in look.Quests
                     .Where(quest => Workspaces.Same(quest.Workspace, circle) && IsClosed(quest))
                     .OrderBy(quest => quest.Filed).ThenBy(quest => quest.Id, StringComparer.Ordinal))
        {
            if (covered.Contains(quest.Id)) continue;
            if (AskDesk.AskOf(quest.From) is { } asker && held.Contains(asker)) continue;
            candidates.Add(await JudgeAsync(new HistoryUnitRef(HistoryUnitKind.Quest, quest.Id), look, wiring, ct).ConfigureAwait(false));
        }

        // A question rides with the work that asked it, whatever order they were filed in (two can share an instant): a
        // candidate any other candidate's work joined is not a unit of its own. A unit's own quest is its first.
        var joined = candidates.SelectMany(unit => unit.Quests.Skip(1)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        units.AddRange(candidates.Where(unit => !joined.Contains(unit.Id)));
        return units;
    }

    /// <summary>
    /// The second press: clear exactly <paramref name="units"/>, in order, each judged again and cleared in one transaction,
    /// or kept with its word. The service's kept files and the settled rule proposals of a cleared unit go after its records.
    /// </summary>
    public async Task<IReadOnlyList<HistoryOutcome>> ClearAsync(
        IReadOnlyList<HistoryUnitRef> units, DateTimeOffset now, CancellationToken ct = default)
    {
        var outcomes = new List<HistoryOutcome>();
        foreach (var named in units)
        {
            // Read before the transaction: the registry may index on first use, and the remotes and proposals are files.
            var wiring = await WiringAsync(ct).ConfigureAwait(false);
            var (unit, cleared) = await sessions.ExclusiveAsync(async inside =>
            {
                var judged = await JudgeAsync(
                    named, await LookAsync(within: true, inside).ConfigureAwait(false), wiring, inside).ConfigureAwait(false);
                if (judged.Refusal is not null) return (judged, false);

                // The clear's range ends at its sessions' last move, read before they go.
                DateTimeOffset? through = null;
                foreach (var session in judged.Sessions)
                {
                    if (await sessions.FindAsync(session, inside).ConfigureAwait(false) is { } found && (through is null || found.Updated > through))
                    {
                        through = found.Updated;
                    }
                }

                foreach (var quest in judged.Quests) await quests.ForgetAsync(quest, now, inside).ConfigureAwait(false);
                foreach (var session in judged.Sessions.Concat(judged.Teammates))
                {
                    await sessions.DeleteAsync(session, inside).ConfigureAwait(false);
                }

                // A finished tree's record its sessions' clear left with none, or one posted after they went, goes by its own `at`.
                if (judged.Workspace is { } circle && through is { } end)
                {
                    await sessions.ForgetOrphanRecordsAsync(circle, end, inside).ConfigureAwait(false);
                }

                foreach (var ask in judged.Asks)
                {
                    if (asks is not null) await asks.DeleteAsync(ask, inside).ConfigureAwait(false);
                    // The ask's channel goes with it, messages and marks, as deleting the ask forgets it.
                    if (channel is not null) await channel.ForgetAsync(ask, inside).ConfigureAwait(false);
                }

                return (judged, true);
            }, ct).ConfigureAwait(false);

            var failed = cleared ? Tidy(unit) : HistoryFailed.None;
            outcomes.Add(new HistoryOutcome(unit, cleared, cleared ? Said(unit) : unit.Refusal!.Message) { Failed = failed });
        }

        return outcomes;
    }

    // ——— What a judgement reads.

    /// <summary>Every record a judgement reads, closed ones included, and how it reads a quest's history.</summary>
    private sealed record Look(
        IReadOnlyList<Quest> Quests, IReadOnlyList<Session> Sessions, IReadOnlyList<Ask> Asks,
        Func<string, CancellationToken, Task<IReadOnlyList<QuestOperation>>> History);

    /// <summary>
    /// The records, read whole: a clear is rare, and a unit's work is read across every quest and record. Within the
    /// press's transaction a history is read inside the connection's gate that transaction already holds.
    /// </summary>
    private async Task<Look> LookAsync(bool within, CancellationToken ct) => new(
        await quests.ListAsync(includeClosed: true, ct: ct).ConfigureAwait(false),
        await sessions.ListAsync(includeClosed: true, ct: ct).ConfigureAwait(false),
        asks is null ? [] : await asks.ListAsync(includeClosed: true, ct: ct).ConfigureAwait(false),
        within ? (id, inside) => quests.HistoryWithinAsync(id, inside) : (id, outside) => quests.HistoryAsync(id, outside));

    /// <summary>This machine's wiring and its rule proposals, which a judgement reads beside the records.</summary>
    private sealed class Wiring(IReadOnlyList<Registration> registry, IRemotes? remotes, IReadOnlyList<RuleProposalNote> proposals)
    {
        private readonly Dictionary<string, bool> _wired = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<RuleProposalNote> Proposals => proposals;

        /// <summary>Whether the workspace has a remote here: what makes a pending move the team's copy still to come.</summary>
        public bool Wired(string workspace)
        {
            var circle = Workspaces.Normalize(workspace);
            if (!_wired.TryGetValue(circle, out var wired)) _wired[circle] = wired = remotes?.For(circle) is not null;
            return wired;
        }

        /// <summary>Whether a repository joined the workspace: only then does what is filed for it leave the machine (design §8).</summary>
        public bool Joined(string workspace, string repository) =>
            registry.Any(row => row.Joined && Workspaces.Same(row.InWorkspace, workspace)
                                && string.Equals(row.Repository, repository, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<Wiring> WiringAsync(CancellationToken ct) =>
        new(await service.RegistryAsync(ct: ct).ConfigureAwait(false), remotes, proposals?.Notes() ?? []);

    // ——— The judgement.

    private async Task<HistoryUnit> JudgeAsync(HistoryUnitRef named, Look look, Wiring wiring, CancellationToken ct)
    {
        var id = named.Id.Trim().TrimStart('#').Trim();
        switch (named.Kind)
        {
            case HistoryUnitKind.Ask:
            {
                var ask = look.Asks.FirstOrDefault(each => string.Equals(each.Id, id, StringComparison.OrdinalIgnoreCase));
                if (ask is null)
                {
                    return new HistoryUnit(named.Kind, id, Workspace: null)
                    {
                        Refusal = new HistoryKept(HistoryRefusal.Unknown, $"No ask `#{id}` on this machine.") { Ask = id },
                    };
                }

                var asked = AskedBy(look, ask);
                var intake = look.Sessions.Where(session =>
                    string.Equals(session.Ask, ask.Id, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(session.Id, ask.Intake, StringComparison.Ordinal));
                return await JudgeWorkAsync(
                    named.Kind, ask.Id, ask.Workspace, Read(asked, intake, look), AskDesk.Standing(ask, asked), look, wiring, ct)
                    .ConfigureAwait(false);
            }

            case HistoryUnitKind.Failed:
                return await JudgeFailedAsync(id, look, wiring, ct).ConfigureAwait(false);

            default:
            {
                if (Find(look, id) is not { } quest)
                {
                    return new HistoryUnit(named.Kind, id, Workspace: null)
                    {
                        Refusal = new HistoryKept(HistoryRefusal.Unknown, $"No quest `#{id}` on this machine.") { Quest = id },
                    };
                }

                // An ask this machine does not hold (a teammate's, whose asks never leave their machine) asks nothing here.
                if (AskDesk.AskOf(quest.From) is { } asker
                    && look.Asks.Any(ask => string.Equals(ask.Id, asker, StringComparison.OrdinalIgnoreCase)))
                {
                    return new HistoryUnit(named.Kind, quest.Id, quest.Workspace)
                    {
                        Refusal = new HistoryKept(
                            HistoryRefusal.Asked,
                            $"Ask `#{asker}` asked `#{quest.Id}`; clear the ask, which takes every quest it became.")
                        {
                            Ask = asker, Quest = quest.Id,
                        },
                    };
                }

                return await JudgeWorkAsync(named.Kind, quest.Id, quest.Workspace, Read([quest], [], look), ask: null, look, wiring, ct)
                    .ConfigureAwait(false);
            }
        }
    }

    /// <summary>A quest of the work, how it joined, and for a question the session of the work that published it.</summary>
    private sealed record WorkQuest(Quest Quest, bool Question, string? By);

    private sealed record Work(IReadOnlyList<WorkQuest> Quests, IReadOnlyList<Session> Sessions);

    /// <summary>
    /// The work read from <paramref name="roots"/> (design §1.1): the session records of each quest in it, and each quest a
    /// session of it published, applied again to what that adds until nothing more joins. The driver's <c>AskWork.Read</c>
    /// reads a work the same way, so a pause, an abandon and a clear reach the same pieces.
    /// </summary>
    private static Work Read(IEnumerable<Quest> roots, IEnumerable<Session> intake, Look look)
    {
        var quests = new List<WorkQuest>();
        var inWork = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Join(WorkQuest quest)
        {
            if (inWork.Add(quest.Quest.Id)) quests.Add(quest);
        }

        foreach (var root in roots) Join(new WorkQuest(root, Question: false, By: null));

        var records = new List<Session>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var unread = new Queue<Session>();
        void Admit(Session record)
        {
            if (!seen.Add(record.Id)) return;
            records.Add(record);
            unread.Enqueue(record);
        }

        foreach (var record in intake) Admit(record);

        var next = 0;
        while (next < quests.Count || unread.Count > 0)
        {
            for (; next < quests.Count; next++)
            {
                var quest = quests[next].Quest.Id;
                foreach (var record in look.Sessions.Where(record => string.Equals(record.Quest, quest, StringComparison.OrdinalIgnoreCase)))
                {
                    Admit(record);
                }
            }

            while (unread.TryDequeue(out var record))
            {
                foreach (var quest in look.Quests.Where(quest => quest.PublishedBy is { } by && Spells(record, by)))
                {
                    Join(new WorkQuest(quest, Question: true, By: record.Id));
                }
            }
        }

        return new Work(quests, records);
    }

    private async Task<HistoryUnit> JudgeWorkAsync(
        HistoryUnitKind kind, string id, string workspace, Work work, Ask? ask, Look look, Wiring wiring, CancellationToken ct)
    {
        var histories = new Dictionary<string, IReadOnlyList<QuestOperation>>(StringComparer.OrdinalIgnoreCase);
        foreach (var each in work.Quests)
        {
            histories[each.Quest.Id] = await look.History(each.Quest.Id, ct).ConfigureAwait(false);
        }

        return new HistoryUnit(kind, id, workspace)
        {
            Quests = [.. work.Quests.Select(each => each.Quest.Id)],
            Forgotten = [.. work.Quests.Where(each => histories[each.Quest.Id].Any(operation => operation.Number is not null)).Select(each => each.Quest.Id)],
            Asks = ask is null ? [] : [ask.Id],
            Sessions = [.. work.Sessions.Where(session => session.Origin is null).Select(session => session.Id)],
            Teammates = [.. work.Sessions.Where(session => session.Origin is not null).Select(session => session.Id)],
            Refusal = await RefusalAsync(work, ask, histories, look, wiring, ct).ConfigureAwait(false),
        };
    }

    /// <summary>
    /// The first piece of the work that keeps it, in the order a person meets them (design §1.2): work in progress, a session
    /// still running, what waits on the person, open work naming it, and moves the remote has not taken. Null when it may go.
    /// </summary>
    private async Task<HistoryKept?> RefusalAsync(
        Work work, Ask? ask, IReadOnlyDictionary<string, IReadOnlyList<QuestOperation>> histories, Look look, Wiring wiring,
        CancellationToken ct)
    {
        // Work in progress: its record is what the driver plans from (§4).
        foreach (var (quest, _, _) in work.Quests.Where(each => !each.Question && !IsClosed(each.Quest)))
        {
            return InProgress(
                quest,
                quest.Status == QuestStatus.Taken
                    ? $"Quest `#{quest.Id}` is taken: its record is work in progress."
                    : $"Quest `#{quest.Id}` is still open: its record is work in progress.");
        }

        // A session that runs writes into its record; a teammate's that still reads as running has not ended yet.
        foreach (var session in work.Sessions.Where(session => session.Active))
        {
            if (session.Origin is { } origin)
            {
                return new HistoryKept(HistoryRefusal.Live, $"Session `{session.Id}` still reads as running on `{origin}`.")
                {
                    Session = session.Id, Origin = origin,
                };
            }

            if (session.State != SessionState.AwaitingPerson)
            {
                return new HistoryKept(HistoryRefusal.Live, $"Session `{session.Id}` is still running; stop it first.")
                {
                    Session = session.Id,
                };
            }
        }

        if (NeedsYou(work, ask, wiring) is { } waits) return waits;

        // Open work naming it.
        foreach (var (quest, _, by) in work.Quests.Where(each => each.Question && !IsClosed(each.Quest)))
        {
            return Published(quest, by!);
        }

        var inWork = work.Quests.Select(each => each.Quest.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var quest in look.Quests.Where(quest => !inWork.Contains(quest.Id) && !IsClosed(quest)))
        {
            if (quest.Status == QuestStatus.Taken && quest.Awaits is { } awaits && inWork.Contains(awaits))
            {
                return Awaited(HistoryAwaitedBy.Asker, $"Quest `#{quest.Id}` waits on its answer from `#{awaits}`.") with
                {
                    Quest = quest.Id,
                };
            }

            if (quest.Parent is { } parent && inWork.Contains(parent))
            {
                return Awaited(HistoryAwaitedBy.Step, $"Quest `#{quest.Id}`, its chain's next step, is still open and builds on its work.") with
                {
                    Quest = quest.Id,
                };
            }
        }

        // The team's copy still to come: a move or a record the remote has not taken yet.
        foreach (var (quest, _, _) in work.Quests)
        {
            if (wiring.Wired(quest.Workspace) && wiring.Joined(quest.Workspace, quest.To)
                && histories[quest.Id].Any(operation => operation.Number is null))
            {
                return Unpushed(quest.Workspace) with { Quest = quest.Id };
            }
        }

        return await UnpushedRecordAsync(work.Sessions, wiring, ct).ConfigureAwait(false);
    }

    /// <summary>What of the work waits on the person, if anything: it is never cleared under them (design §1.2).</summary>
    private static HistoryKept? NeedsYou(Work work, Ask? ask, Wiring wiring)
    {
        foreach (var session in work.Sessions.Where(session => session.Origin is null && session.State == SessionState.AwaitingPerson))
        {
            return Waiting(HistoryWaiting.Parked, $"Session `{session.Id}` waits on you.") with { Session = session.Id };
        }

        foreach (var (quest, _, _) in work.Quests)
        {
            if (quest.Held)
            {
                return Waiting(HistoryWaiting.Held, $"Quest `#{quest.Id}` is done and waits for you to accept it.") with
                {
                    Quest = quest.Id,
                };
            }

            if (quest.Conflicts.Count > 0)
            {
                return Waiting(HistoryWaiting.Conflict, $"A conflict on `#{quest.Id}` waits on you.") with { Quest = quest.Id };
            }
        }

        if (ask is { State: AskState.Open or AskState.Proposed })
        {
            return Waiting(HistoryWaiting.Ask, $"Ask `#{ask.Id}` waits for you to publish or close it.") with { Ask = ask.Id };
        }

        var own = work.Sessions.Where(session => session.Origin is null).Select(session => session.Id).ToHashSet(StringComparer.Ordinal);
        return Proposed(wiring, note => (note.Session is { } by && own.Contains(by))
                                        || (ask is not null && string.Equals(note.Ask, ask.Id, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>A rule proposal still waiting that <paramref name="made"/> says the work made, if any.</summary>
    private static HistoryKept? Proposed(Wiring wiring, Func<RuleProposalNote, bool> made) =>
        wiring.Proposals.FirstOrDefault(note => note.Pending && made(note)) is { } pending
            ? Waiting(
                HistoryWaiting.Proposal,
                pending.Session is { } session
                    ? $"A rule proposal from session `{session}` waits on you."
                    : $"A rule proposal for ask `#{pending.Ask}` waits on you.") with
            {
                Session = pending.Session, Ask = pending.Ask,
            }
            : null;

    /// <summary>
    /// A <see cref="HistoryRefusal.NeedsYou"/> that names what waits: the one way the desk says the word, so none of
    /// its sentences reaches the wire without the variant the driver reads in place of the records.
    /// </summary>
    private static HistoryKept Waiting(HistoryWaiting waits, string message) => new(HistoryRefusal.NeedsYou, message) { Waits = waits };

    /// <summary>
    /// A <see cref="HistoryRefusal.Open"/> that names how <paramref name="quest"/> stands, as <see cref="Waiting"/> names
    /// what waits: the one way the desk says the word, so the driver reads *taken* from the refusal and not from the quest again.
    /// </summary>
    private static HistoryKept InProgress(Quest quest, string message) => new(HistoryRefusal.Open, message)
    {
        Quest = quest.Id, Stands = quest.Status == QuestStatus.Taken ? HistoryStands.Taken : HistoryStands.Open,
    };

    /// <summary>A <see cref="HistoryRefusal.Awaited"/> that names the open work naming the unit: the one way the desk says the word.</summary>
    private static HistoryKept Awaited(HistoryAwaitedBy by, string message) => new(HistoryRefusal.Awaited, message) { By = by };

    /// <summary>An open <paramref name="question"/> the work's session <paramref name="session"/> published: the work awaits its answer.</summary>
    private static HistoryKept Published(Quest question, string session) =>
        Awaited(HistoryAwaitedBy.Question, $"Quest `#{question.Id}` was published by its session `{session}` and is still open.") with
        {
            Quest = question.Id, Session = session,
        };

    /// <summary>
    /// A record of this machine's, of a repository joined in its wired workspace, written after the last push: the team's
    /// copy still to come. A record of a repository that never joined stays home, so it is never waited for.
    /// </summary>
    private async Task<HistoryKept?> UnpushedRecordAsync(IEnumerable<Session> records, Wiring wiring, CancellationToken ct)
    {
        var unpushed = new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var session in records.Where(session => session.Origin is null))
        {
            if (!wiring.Wired(session.Workspace) || !wiring.Joined(session.Workspace, session.Repository)) continue;
            if (!unpushed.TryGetValue(session.Workspace, out var changed))
            {
                var (pushed, _) = await sessions.CursorAsync(session.Workspace, ct).ConfigureAwait(false);
                unpushed[session.Workspace] = changed = (await sessions.OwnChangedSinceAsync(pushed, session.Workspace, ct).ConfigureAwait(false))
                    .Select(each => each.Session.Id)
                    .ToHashSet(StringComparer.Ordinal);
            }

            if (changed.Contains(session.Id)) return Unpushed(session.Workspace) with { Session = session.Id };
        }

        return null;
    }

    private static HistoryKept Unpushed(string workspace) =>
        new(HistoryRefusal.Unpushed, $"Its last moves have not reached the remote for `{workspace}`; sync, then clear it.")
        {
            Workspace = workspace,
        };

    /// <summary>
    /// A closed quest's failed sessions: this machine's go, a teammate's are listed and kept. Never an open
    /// or taken quest's, whose strikes are counted from them.
    /// </summary>
    private async Task<HistoryUnit> JudgeFailedAsync(string id, Look look, Wiring wiring, CancellationToken ct)
    {
        if (Find(look, id) is not { } quest)
        {
            return new HistoryUnit(HistoryUnitKind.Failed, id, Workspace: null)
            {
                Refusal = new HistoryKept(HistoryRefusal.Unknown, $"No quest `#{id}` on this machine.") { Quest = id },
            };
        }

        var failed = look.Sessions
            .Where(session => string.Equals(session.Quest, quest.Id, StringComparison.OrdinalIgnoreCase) && session.State == SessionState.Failed)
            .ToList();
        // A conversation is cleared whole or not at all: a failed part whose conversation holds a part that did not fail stays, said.
        var notFailed = look.Sessions
            .Where(session => session.Origin is null && session.State != SessionState.Failed)
            .Select(session => session.Line ?? session.Id).ToHashSet(StringComparer.Ordinal);
        var split = failed.Where(session => session.Origin is null && notFailed.Contains(session.Line ?? session.Id)).ToList();
        var own = failed.Where(session => session.Origin is null && !split.Contains(session)).ToList();
        var unit = new HistoryUnit(HistoryUnitKind.Failed, quest.Id, quest.Workspace)
        {
            Sessions = [.. own.Select(session => session.Id)],
            Kept =
            [
                .. split.Select(session => new HistoryKept(
                    HistoryRefusal.Open,
                    $"Session `{session.Id}` stays: its conversation holds a part that did not fail, and a clear takes a conversation whole.")
                {
                    Session = session.Id,
                }),
                .. failed.Where(session => session.Origin is not null).Select(session =>
                new HistoryKept(HistoryRefusal.NotOurs, $"Session `{session.Id}` ran on `{session.Origin}`; its record is theirs.")
                {
                    Session = session.Id, Origin = session.Origin,
                })],
        };

        if (!IsClosed(quest))
        {
            return unit with
            {
                Refusal = InProgress(
                    quest,
                    // A person reads this; strike is the code's word, never on the window (the glossary's).
                    $"Quest `#{quest.Id}` is still open or taken, and these failed sessions count against it; archive them instead."),
            };
        }

        var ids = own.Select(session => session.Id).ToHashSet(StringComparer.Ordinal);
        var refusal = Proposed(wiring, note => note.Session is { } by && ids.Contains(by));
        foreach (var session in own)
        {
            if (refusal is not null) break;
            if (look.Quests.FirstOrDefault(asked => asked.PublishedBy is { } by && Spells(session, by) && !IsClosed(asked)) is { } open)
            {
                refusal = Published(open, session.Id);
            }
        }

        return unit with { Refusal = refusal ?? await UnpushedRecordAsync(own, wiring, ct).ConfigureAwait(false) };
    }

    // ——— After the records.

    /// <summary>
    /// What the service keeps for a cleared unit, removed once its records are gone: each quest's and
    /// the ask's kept files, and each settled rule proposal naming a cleared session or the ask. Best effort, as an ask's delete
    /// removes kept files: a file the disk will not let go of is left over, and nothing names it any more. The quests and asks
    /// whose kept files stayed come back, so the driver counts them as failed rather than freed; a rule proposal that
    /// stays is not among them, since no clear of a workspace takes one as left over.
    /// </summary>
    private HistoryFailed Tidy(HistoryUnit unit)
    {
        var questsKept = new List<string>();
        foreach (var quest in unit.Quests)
        {
            if (files?.Forget(quest) == false) questsKept.Add(quest);
        }

        var askFiles = files?.For(HomeEntry.AskFiles);
        var asksKept = new List<string>();
        foreach (var ask in unit.Asks)
        {
            if (askFiles?.Forget(ask) == false) asksKept.Add(ask);
        }

        var failed = questsKept.Count + asksKept.Count == 0 ? HistoryFailed.None : new HistoryFailed(questsKept, asksKept);

        if (proposals is null) return failed;
        var sessionsGone = unit.Sessions.ToHashSet(StringComparer.Ordinal);
        var asksGone = unit.Asks.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var note in proposals.Notes().Where(note => !note.Pending
                     && ((note.Session is { } session && sessionsGone.Contains(session)) || (note.Ask is { } ask && asksGone.Contains(ask)))))
        {
            proposals.Forget(note.Id);
        }

        return failed;
    }

    /// <summary>What a cleared unit's answer says: what went, and, where a remote numbered it, that the team keeps its copy.</summary>
    private static string Said(HistoryUnit unit)
    {
        if (unit.Kind == HistoryUnitKind.Failed)
        {
            var kept = unit.Kept.Count == 0 ? "" : " " + string.Join(" ", unit.Kept.Select(piece => piece.Message));
            return unit.Sessions.Count == 0
                ? $"Nothing to clear: `#{unit.Id}` has no failed session of this machine's.{kept}"
                : $"Cleared {Count(unit.Sessions.Count, "failed session")} of `#{unit.Id}` from this machine; the quest and its "
                  + $"other sessions stay.{kept}";
        }

        var what = $"{Count(unit.Quests.Count, "quest")} and {Count(unit.Sessions.Count + unit.Teammates.Count, "session record")}";
        var forgotten = unit.Forgotten.Count == 0
            ? ""
            : $" The remote for `{unit.Workspace}` keeps the team's copy; this machine will not fetch it again.";
        return unit.Kind == HistoryUnitKind.Ask
            ? $"Cleared ask `#{unit.Id}` from this machine, with {what}.{forgotten}"
            : $"Cleared `#{unit.Id}` from this machine: {what}.{forgotten}";
    }

    private static string Count(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";

    private static bool IsClosed(Quest quest) => quest.Status is QuestStatus.Done or QuestStatus.Declined;

    private static Quest? Find(Look look, string id) =>
        look.Quests.FirstOrDefault(quest => string.Equals(quest.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>Every quest an ask asked, chain steps included, closed ones included: each names the ask as its sender.</summary>
    private static List<Quest> AskedBy(Look look, Ask ask)
    {
        var sender = AskDesk.SenderOf(ask.Id);
        return [.. look.Quests.Where(quest => string.Equals(quest.From, sender, StringComparison.Ordinal))];
    }

    /// <summary>
    /// Whether a quest's <c>publishedBy</c> names this record's session: as the record is keyed, or, for a teammate's record
    /// (keyed <c>origin/id</c>), as their machine spells it. The driver's <c>AskWork</c> reads it the same way.
    /// </summary>
    private static bool Spells(Session record, string by) =>
        string.Equals(record.Id, by, StringComparison.Ordinal)
        || (record.Origin is not null && record.Id.IndexOf('/') is > 0 and var slash
            && string.Equals(record.Id[(slash + 1)..], by, StringComparison.Ordinal));
}
