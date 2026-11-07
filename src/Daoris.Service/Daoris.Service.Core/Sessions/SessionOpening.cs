namespace Daoris.Knowledge;

/// <summary>
/// When a session may open: a driven session for a quest, a chat in a repository, Ask Daoris's conversation and an
/// intake for an ask, each judged and written as one step across hosts, and the keyed open that moves the earlier part's words with it.
/// </summary>
/// <param name="opinions">
/// The second opinions, where findings an open moved to a new part are followed: the part that holds them answers them.
/// </param>
internal sealed class SessionOpening(
    QuestStore quests, SessionStore sessions, KnowledgeService? registry, AskStore? asks, OpinionStore? opinions)
{
    /// <summary>
    /// The driver's code for a session the person's reopen of the work its step grew from stopped (FABRIC-CLOSE1c2; its
    /// <c>EndNoteCodes.EndedSourceReopened</c>, a twin read here by its spelling): the one ending the ledger reads by a code, since
    /// the record's state alone, <c>completed</c>, says the work finished (FABRIC-CLOSE1c26).
    /// </summary>
    internal const string EndedSourceReopened = "ended.source-reopened";

    private readonly SessionHolders holders = new(quests, sessions, registry);

    private static string Spell(SessionState state) => Session.Spell(state);

    /// <summary>
    /// Open an INTAKE for an ask — a conversation Daoris opens in a room it owns, which
    /// decides from the circle's declarations, publishes the quests itself, and asks the person where
    /// the declarations do not settle it.
    /// </summary>
    /// <remarks>
    /// <para><b>Its own open, not a chat's.</b> A chat names a registered repository and takes its
    /// circle from the row; the room is no repository, so the circle comes from the ASK — derived,
    /// never passed beside it, for the same reason a driven session's comes from its quest.</para>
    /// <para><b>Recorded as a chat</b>, serving no quest, in "repository" <c>ask #id</c> — the sender its
    /// quests are published by. Every build already reads a chat as a session nothing plans from.</para>
    /// <para><b>The room's lock is the process, not the record</b>: one intake RUNNING per room, and a
    /// parked one — which has asked the person and ended — leaves it to the next ask
    /// (<see cref="SessionStore.RunningInTreeAsync"/>).</para>
    /// </remarks>
    public async Task<SessionOpenOutcome> OpenIntakeAsync(
        string askId, string adapter, string room, DateTimeOffset now,
        string? doorVersion = null, string? profile = null, CancellationToken ct = default, string? actor = null)
    {
        var stored = asks is null ? null : await asks.FindAsync(askId, ct).ConfigureAwait(false);
        if (stored is null)
        {
            return new(SessionOpenRefusal.AskNotFound, $"No ask `#{askId.TrimStart('#')}`.", Session: null);
        }

        // As it stands, the way the desk reads it: an ask whose every quest is deleted is a
        // proposal again — an intake's to answer, but never a named ask's.
        var ask = AskDesk.Standing(
            stored, await quests.FromAsync(AskDesk.SenderOf(stored.Id), ct: ct).ConfigureAwait(false));

        var served = ask.Intake is { } earlier
            ? $"intake session `{earlier}` already served it"
            : ask.State switch
            {
                AskState.Published or AskState.Done => $"it already became {string.Join(", ", ask.Quests.Select(q => $"`#{q}`"))}",
                AskState.Closed => $"it is closed ({ask.Note})",
                // The person named its receiver and that quest is deleted — an intake would route it by its proposal.
                AskState.Proposed when ask.Tier == AskDesk.ByName =>
                    "the person named its receiver, and the quest it became was deleted: no intake routes an ask by its proposal after that",
                _ => null,
            };
        if (served is not null)
        {
            return new(
                SessionOpenRefusal.AskAnswered,
                $"Ask `#{ask.Id}` is not an intake's to answer — {served}. Publish it with a receiver you "
                + "name, or close it.",
                Session: null);
        }

        var holding = Trees.Normalize(room)
            ?? throw new ArgumentException("an intake runs in a room — name it", nameof(room));

        // The check and the write as one step, across hosts: see SessionStore.ExclusiveAsync.
        return await sessions.ExclusiveAsync(async inside =>
        {
            var running = await sessions.RunningInTreeAsync(holding, inside).ConfigureAwait(false);
            if (running is not null)
            {
                return new SessionOpenOutcome(
                    SessionOpenRefusal.RepositoryBusy,
                    $"The intake room for `{ask.Workspace}` already has a running session — `{running.Id}` "
                    + $"({Spell(running.State)}{(running.Ask is { } other ? $", ask `#{other}`" : "")}). One intake runs "
                    + "per workspace at a time; the next ask is taken when it ends.",
                    Session: null);
            }

            // No base commit, deliberately: the room is no repository, and git asked about it would walk
            // UP and answer for whatever checkout the home sits in.
            var session = await sessions
                .CreateAsync(
                    null, AskDesk.SenderOf(ask.Id), adapter, now, ask.Workspace, SessionKind.Chat,
                    doorVersion, profile, holding, baseCommit: null, inside, ask: ask.Id,
                    entryOrigin: SessionEntries.AskOrigin(ask.Id), entryPart: "intake", actor: actor)
                .ConfigureAwait(false);
            await asks!.RecordIntakeAsync(ask.Id, session.Id, now, inside).ConfigureAwait(false);

            return new SessionOpenOutcome(
                SessionOpenRefusal.None,
                $"Intake `{session.Id}` opened for ask `#{ask.Id}` in `{ask.Workspace}`, via {adapter}.",
                session);
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Open Ask Daoris's conversation — a chat about Daoris itself, in the room the
    /// driver keeps for it under its home.
    /// </summary>
    /// <remarks>
    /// <para><b>The intake's open, for a conversation.</b> It serves no quest and no ask, so nothing
    /// plans from it. It belongs to the workspace its window is on when it starts, recorded here and never moved;
    /// none named is <c>default</c>, as a record from before reads.</para>
    ///
    /// <para><b>One running per workspace</b> (point 3): a second in the same workspace is refused naming the first, and the
    /// driver hands the page the running one rather than asking for another; one in another workspace runs beside it, in the
    /// same room folder.</para>
    ///
    /// <para><b>A workspace this machine does not hold is refused</b>, naming those it does: <c>default</c> and each registered
    /// repository's. With no registry to ask, the name is taken as told.</para>
    /// </remarks>
    /// <param name="workspace">The workspace the person's window is on at the press; null is <c>default</c>.</param>
    /// <param name="mode">
    /// The access mode its start gets, the driver's to say, since it is the half that spawns it; null is
    /// <see cref="SessionLedger.HelpRestricted"/>, as a driver from before sends it.
    /// </param>
    /// <exception cref="ArgumentException">No room, or a mode the ledger does not know.</exception>
    public async Task<SessionOpenOutcome> OpenHelpAsync(
        string adapter, string room, DateTimeOffset now,
        string? doorVersion = null, string? profile = null, string? workspace = null, string? mode = null,
        CancellationToken ct = default, string? actor = null)
    {
        var holding = Trees.Normalize(room)
            ?? throw new ArgumentException("Ask Daoris runs in a room — name it", nameof(room));
        var running = SessionLedger.HelpModeOf(mode);
        var circle = Workspaces.Normalize(workspace);

        if (registry is not null)
        {
            var held = (await registry.RegistryAsync(ct: ct).ConfigureAwait(false))
                .Select(row => Workspaces.Normalize(row.InWorkspace)).Append(Workspaces.Default)
                .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToList();
            if (!held.Any(each => Workspaces.Same(each, circle)))
            {
                return new SessionOpenOutcome(
                    SessionOpenRefusal.WorkspaceUnknown,
                    $"`{circle}` is not a workspace this machine holds — Ask Daoris's conversation is started in the workspace "
                    + $"its window is on. Held here: {string.Join(", ", held)}.",
                    Session: null);
            }

            // Spelled as the machine holds it, so one workspace is never two by its letters' case.
            circle = held.First(each => Workspaces.Same(each, circle));
        }

        return await sessions.ExclusiveAsync(async inside =>
        {
            var holder = await sessions.RunningInWorkspaceAsync(SessionLedger.HelpRepository, circle, parked: false, inside).ConfigureAwait(false);
            if (holder is not null)
            {
                return new SessionOpenOutcome(
                    SessionOpenRefusal.RepositoryBusy,
                    $"Ask Daoris already has a conversation running in `{holder.Workspace}` — `{holder.Id}` "
                    + $"({Spell(holder.State)}). One runs at a time in a workspace; carry that one on, or finish it first.",
                    Session: null);
            }

            // No base commit, for the intake's reason: the room is no repository.
            var session = await sessions
                .CreateAsync(
                    null, SessionLedger.HelpRepository, adapter, now, circle, SessionKind.Chat,
                    doorVersion, profile, holding, baseCommit: null, inside, ownEntry: true, helpMode: running, actor: actor)
                .ConfigureAwait(false);

            return new SessionOpenOutcome(
                SessionOpenRefusal.None,
                $"Ask Daoris `{session.Id}` opened in `{session.Workspace}`, via {adapter}.",
                session);
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Open a chat in a repository — a person-initiated session serving no quest yet.
    /// </summary>
    /// <remarks>
    /// <para><b>The same lock, for the same reason.</b> One active session per repository holds for
    /// chats exactly as for driven work: two agents in one working tree corrupt each other's git
    /// state regardless of who is typing. The refusal names the session that holds it.</para>
    /// <para><b>No quest, and no quest required.</b> The point of a chat is starting work that is not
    /// yet shaped as an ask; a chat may take a quest mid-conversation through its own connector, or
    /// end by publishing several. The quest system is where the work lands, not the toll to begin.</para>
    /// <para><b>The circle comes from the repository</b>, because there is no quest to take it from —
    /// the registry row is the machine's own wiring, and it is the only honest source.</para>
    /// </remarks>
    /// <param name="from">The conversation this one continues, handed over, with <paramref name="fromWhy"/> and <paramref name="fromBy"/>; recorded as told.</param>
    /// <param name="moves">The words a keyed open moves onto the record it opens, in its own step; none moves without it.</param>
    /// <exception cref="ArgumentException"><paramref name="moves"/> without <paramref name="opens"/>, or moves that do not read; nothing is opened.</exception>
    public async Task<SessionOpenOutcome> OpenChatAsync(
        string repository, string adapter, DateTimeOffset now,
        string? doorVersion = null, string? profile = null, string? tree = null,
        string? baseCommit = null, CancellationToken ct = default,
        string? from = null, string? fromWhy = null, string? fromBy = null, string? actor = null, string? opens = null,
        OpenMoves? moves = null)
    {
        if (opens is not null)
            return await KeyedOpenAsync(opens, from, moves, inside => OpenChatAsync(repository, adapter, now,
                doorVersion, profile, tree, baseCommit, inside, from, fromWhy, fromBy, actor), ct).ConfigureAwait(false);
        if (moves is not null) throw Unkeyed();

        if (string.IsNullOrWhiteSpace(repository)) throw new ArgumentException("repository is required");

        var known = registry is null
            ? null
            : (await registry.RegistryAsync(ct: ct).ConfigureAwait(false))
                .Named(repository);

        if (known is null)
        {
            return new(
                SessionOpenRefusal.RepositoryUnknown,
                $"`{repository}` is not registered on this machine, so there is no working tree to talk "
                + "in. `daoris connect` from inside it, or add it from Repositories.",
                Session: null);
        }

        // Unstated means the registered root — this repository's main tree. Resolved here rather
        // than taken on trust, so a caller that names the root and one that says nothing land on the
        // same key instead of holding one tree twice.
        var holding = Trees.Normalize(tree) ?? Trees.Normalize(known.Root);

        // A conversation is its own line, and one handed over or started from another's words takes that one's.
        var continuedOrigin = from is not null && await sessions.FindOrMirrorAsync(from, ct).ConfigureAwait(false) is { } continued
            ? await SessionEntries.OriginOfAsync(continued, new StoreEntryFacts(quests, sessions), ct).ConfigureAwait(false)
            : null;

        return await sessions.ExclusiveAsync(async inside =>
        {
            var active = await holders.HolderAsync(known.Repository, holding, inside).ConfigureAwait(false);
            if (active is not null)
            {
                return new SessionOpenOutcome(
                    SessionOpenRefusal.RepositoryBusy,
                    SessionHolders.Busy(known.Repository, active) + " That is true of a conversation exactly as it is of "
                    + "driven work.",
                    Session: null);
            }

            var session = await sessions
                .CreateAsync(
                    null, known.Repository, adapter, now, known.InWorkspace, SessionKind.Chat,
                    doorVersion, profile, holding, baseCommit, inside, from: from, fromWhy: fromWhy, fromBy: fromBy,
                    entryOrigin: continuedOrigin, ownEntry: continuedOrigin is null, actor: actor)
                .ConfigureAwait(false);

            return new SessionOpenOutcome(
                SessionOpenRefusal.None,
                $"Chat `{session.Id}` opened in `{known.Repository}`, via {adapter}.",
                session);
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// A keyed open (row d-i), and from row d-ii part 3 its move of the old part's words (the design §3.4 rule 3), as one exclusive step: the
    /// key's receipt answered first, whatever the request says now; then the record the words leave, the record left or, where the open names no
    /// <c>from</c>, its moves' own source (a start from another's words), which must stand, be its line's head and not be active; then the open,
    /// the move and the receipt with every word moved. A receipt that cannot be written throws, and the step rolls the open and the move back.
    /// </summary>
    /// <remarks>
    /// <b>What moves</b> is .NET's selection, never invented here: each word still waiting there that the course named, whoever said it, and
    /// each of the person's words kept since the course read (no id it read or named), but one a dispatcher attempted, which may have reached
    /// its agent and is never sent again by itself. Daoris's own words, a helper's answer and another agent's claim kept since are not
    /// carried (rule 6): the course composes Daoris's again under their keys. Each keeps its id and its producer key, and its transfer id is
    /// the open's key and that id, so a word with no key has one too.
    /// </remarks>
    private Task<SessionOpenOutcome> KeyedOpenAsync(
        string key, string? from, OpenMoves? moves, Func<CancellationToken, Task<SessionOpenOutcome>> open, CancellationToken ct) =>
        sessions.ExclusiveAsync(async inside =>
        {
            // Replay precedes even the record-left and request checks: the committed result owns the key, its transfer the receipt's, never
            // read again from any record's words.
            if (await sessions.Opens.FindAsync(key, inside).ConfigureAwait(false) is { } kept)
            {
                var record = kept.Gone ? null : await sessions.FindAsync(kept.Opened, inside).ConfigureAwait(false);
                return record is null
                    ? new SessionOpenOutcome(SessionOpenRefusal.Gone, $"The destination `{kept.Opened}` is gone.", null) { Opens = key }
                    : new SessionOpenOutcome(SessionOpenRefusal.None, $"Session `{record.Id}` was opened.", record)
                        { Opens = key, Transfer = kept.Transfer };
            }

            moves?.Judge(from);
            var left = from ?? moves?.From;
            var before = left is null ? null : await sessions.FindAsync(left, inside).ConfigureAwait(false);
            if (before is null)
                return new SessionOpenOutcome(SessionOpenRefusal.Gone, $"The record left `{left}` is gone.", null) { Opens = key };
            var head = await Lines.HeadOf(sessions, before, inside).ConfigureAwait(false);
            if (before.Origin is not null || before.Active || head?.Id != before.Id)
                return new SessionOpenOutcome(SessionOpenRefusal.Superseded,
                    $"Session `{before.Id}` cannot be left; its head is `{head?.Id ?? "unplaced"}` and its state is {before.StateName}.", null)
                    { Opens = key, Head = head?.Id };

            var result = await open(inside).ConfigureAwait(false);
            if (result.Session is not { } opened || result.Refusal != SessionOpenRefusal.None) return result with { Opens = key };

            IReadOnlyList<OpenTransfer> transfer = [];
            var message = result.Message;
            if (moves is not null)
            {
                var named = moves.Words.ToHashSet(StringComparer.Ordinal);
                var read = moves.Read.ToHashSet(StringComparer.Ordinal);
                var moved = await sessions.MoveSaidAsync(before.Id, opened.Id,
                    word => named.Contains(word.Id) || (word.Persons && word.Attempt is null && !read.Contains(word.Id)), inside).ConfigureAwait(false);
                transfer = [.. moved.Select((word, order) => new OpenTransfer(word.Id, word.Key, $"{key}/{word.Id}", order, word.Id))];
                // The part that now holds a waiting findings word answers its opinion (an authority change): handed_to follows the word.
                if (opinions is not null)
                {
                    foreach (var word in moved.Where(each => each.By is not null))
                    {
                        await opinions.KeepFollowedAsync(word.By!, word.Id, before.Id, opened.Id, inside).ConfigureAwait(false);
                    }
                }

                if (moved.Count > 0) opened = await sessions.FindAsync(opened.Id, inside).ConfigureAwait(false) ?? opened;
                message += moved.Count switch
                {
                    0 => $" No words waited on session `{before.Id}` to move.",
                    1 => $" One word moved from session `{before.Id}`.",
                    _ => $" {moved.Count} words moved from session `{before.Id}`.",
                };
            }

            await sessions.Opens.KeepAsync(key, before.Id, opened.Id, transfer, inside).ConfigureAwait(false);
            return result with { Opens = key, Transfer = transfer, Session = opened, Message = message };
        }, ct);

    /// <summary>Moves asked of an open with no key: nothing would answer them again, so a lost answer could move twice.</summary>
    private static ArgumentException Unkeyed() => new(
        "Words move only under an open's key (`opens`), whose receipt answers a retry with what moved, so nothing was opened.");

    /// <summary>
    /// Queue a session for an open quest. Refuses an unknown or non-open quest, and a repository that
    /// already has an active session.
    /// </summary>
    /// <param name="from">
    /// The session this one carries on from another agent's or another account's, with <paramref name="fromWhy"/>
    /// and <paramref name="fromBy"/>; recorded as told, since the driver is the half that knows.
    /// </param>
    /// <param name="access">
    /// The access setup the start resolved, each pause with its value; recorded as told, since the driver is
    /// the half that resolves it, and judged only as a short map of words (<see cref="SessionAccess.Judged"/>).
    /// </param>
    /// <param name="moves">The words a keyed open moves onto the record it opens (row d-ii part 3), in its own step; none moves without it.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="access"/> is not a short map of words, or <paramref name="moves"/> come without <paramref name="opens"/> or do not read;
    /// nothing is opened.
    /// </exception>
    public async Task<SessionOpenOutcome> OpenAsync(
        string questId, string adapter, DateTimeOffset now,
        string? doorVersion = null, string? profile = null, string? tree = null,
        string? baseCommit = null, CancellationToken ct = default,
        string? from = null, string? fromWhy = null, string? fromBy = null,
        IReadOnlyDictionary<string, string>? access = null, string? actor = null, string? opens = null, OpenMoves? moves = null)
    {
        if (opens is not null)
            return await KeyedOpenAsync(opens, from, moves, inside => OpenAsync(questId, adapter, now,
                doorVersion, profile, tree, baseCommit, inside, from, fromWhy, fromBy, access, actor), ct).ConfigureAwait(false);
        if (moves is not null) throw Unkeyed();

        if (string.IsNullOrWhiteSpace(questId) || string.IsNullOrWhiteSpace(adapter))
            throw new ArgumentException("quest and door are required");

        // The caller's mistake is said before anything is read or held.
        var setup = SessionAccess.Judged(access);
        var quest = await quests.FindAsync(questId.TrimStart('#'), ct).ConfigureAwait(false);
        if (quest is null)
        {
            return new(
                SessionOpenRefusal.QuestNotFound,
                $"No quest `#{questId.TrimStart('#')}`. Ids come from `quest_list`.",
                Session: null);
        }

        // A resume: a taken quest whose taker waited on a question, now answered, is its taker's
        // to carry on — the one taken quest a session may start on.
        var question = quest is { Status: QuestStatus.Taken, Awaits: { } awaits }
            ? await quests.FindAsync(awaits, ct).ConfigureAwait(false)
            : null;
        var resumes = question is { Status: QuestStatus.Done or QuestStatus.Declined };

        // A carry-on: a taken quest that is not waiting, whose last session on THIS machine
        // failed — timed out, refused, crashed — before closing it. The take is this machine's and its
        // tree holds the work. The driver's strikes bound how often; a stand-down or a teammate's
        // record never counts, because either means the take is somebody else's.
        // And the person's answer to one that parked to ask them, where the driver could not
        // go on with that record itself: it ended the park `completed` with their words kept, saying why,
        // and the quest it held is carried on the same way. A park still parked holds its quest.
        // And a stop that is not the person's: the sweep found nothing running it, or the driver
        // shut down under it.
        // And the person's own stop, of a take a session here made: the driver holds the quest
        // until Try again releases it, so an open reaching here is the release. A stop before
        // the take leaves none here, and the quest taken since is somebody else's, as after a stand-down.
        var last = quest is { Status: QuestStatus.Taken } && question is null
            ? await sessions.LastOwnForQuestAsync(quest.Id, ct).ConfigureAwait(false)
            : null;
        var tookHere = last is not null && await sessions.TookHereAsync(quest.Id, ct).ConfigureAwait(false);
        // And a stand-down whose take lost, once the person brought its work forward: the quest stands
        // taken by the fresh take that settled its conflict, made after the record began. Once, since the session carried on is the
        // newest record from then, and a later one is never older than the take. Read from the log; no record marks it.
        var forward = last is { State: SessionState.StoodDown }
            ? (await quests.ReadTakesAsync(quest.Id, ct).ConfigureAwait(false)).BroughtForwardBy(quests.Machine)
            : null;
        var brought = forward is not null && forward.At > last!.Created;
        // And a session the person's reopen of the work its step grew from stopped: it ended
        // completed with its quest still taken, said by the driver's code alone, and their go-on carries it on in its tree.
        var carriesOn = last is { State: SessionState.Failed }
                or { State: SessionState.Completed, PersonSaid: true }
                or { State: SessionState.Stopped, Interrupted: true }
            || (last is { State: SessionState.Stopped } && tookHere)
            || (last is { State: SessionState.Completed } && NoteParts.Carries(last.NoteParts, EndedSourceReopened))
            || brought;

        if (quest.Status != QuestStatus.Open && !resumes && !carriesOn)
        {
            // Taken means someone — a session, a person, another machine — already has it, and Done or
            // Declined means the work is over. Either way there is nothing here for a new session to do.
            return new(
                SessionOpenRefusal.QuestNotOpen,
                question is not null
                    ? $"Quest `#{quest.Id}` waits on `#{question.Id}`, which is still {question.Status} — it resumes once that is answered."
                    : $"Quest `#{quest.Id}` is {quest.Status} — a session starts only on an open quest.",
                Session: null);
        }

        // A carry-on goes on with this machine's take, so it asks whose the take is: a
        // session that failed before its take does not carry on over whoever took the quest since.
        if (carriesOn && await TakenElsewhereAsync(quest, last!, tookHere, brought, ct).ConfigureAwait(false) is { } elsewhere)
        {
            return new(SessionOpenRefusal.TakenElsewhere, elsewhere, Session: null);
        }

        // The same resolution as a chat's, through the same registry, for the same reason: two doors
        // onto one tree must agree about which tree that is.
        var holding = Trees.Normalize(tree) ?? Trees.Normalize(await holders.RootOfAsync(quest.To, ct).ConfigureAwait(false));

        // The line of work the quest belongs to, read before the lock is taken so it holds nothing while it asks.
        var origin = await SessionEntries.OriginOfQuestAsync(quest, new StoreEntryFacts(quests, sessions), ct).ConfigureAwait(false);

        return await sessions.ExclusiveAsync(async inside =>
        {
            var active = await holders.HolderAsync(quest.To, holding, inside).ConfigureAwait(false);
            if (active is not null)
            {
                return new SessionOpenOutcome(SessionOpenRefusal.RepositoryBusy, SessionHolders.Busy(quest.To, active), Session: null);
            }

            // The record's circle is the quest's circle — derived, never passed beside it, so a record can
            // never be filed under a workspace its quest does not belong to.
            var session = await sessions
                .CreateAsync(
                    quest.Id, quest.To, adapter, now, quest.Workspace, SessionKind.Driven,
                    doorVersion, profile, holding, baseCommit, inside, from: from, fromWhy: fromWhy, fromBy: fromBy,
                    entryOrigin: origin, access: setup, actor: actor)
                .ConfigureAwait(false);

            return new SessionOpenOutcome(
                SessionOpenRefusal.None,
                $"Session `{session.Id}` queued for quest `#{quest.Id}` in `{quest.To}`, via {adapter}.",
                session);
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Whose a carry-on's take is: the refusal's sentence when it is not this machine's, else null.
    /// </summary>
    /// <remarks>
    /// <para><b>The quest's log names the machine that took it</b>: a take another machine made is theirs,
    /// whichever session here failed or stopped, and so is one that beat this machine's own take to the remote.
    /// Never by the clock, which is another machine's there. Named by a teammate's record on the quest where one came,
    /// since the log's machine is an id no person reads, else as another machine.</para>
    ///
    /// <para><b>A take made here after this machine's last session on the quest ended</b> is not that session's, and no
    /// session here marked it: a chat's connector took it, whose mark names no quest, or work
    /// outside Daoris did. Both times are this machine's own clock. A take with no mark made before the session ended
    /// is still carried on: the HTTP door marks none, and a take from before the mark has none.</para>
    ///
    /// <para><b>A take the person's bring-forward made</b> (SYNCMERGE1b0, <paramref name="broughtForward"/>) is made after the session
    /// ended and marked by no session, and is this machine's own for the session it carries on: the person made it for that work.</para>
    /// </remarks>
    private async Task<string?> TakenElsewhereAsync(Quest quest, Session last, bool tookHere, bool broughtForward, CancellationToken ct)
    {
        var take = (await quests.HistoryAsync(quest.Id, ct).ConfigureAwait(false))
            .LastOrDefault(operation => operation.Kind == QuestOperationKind.Taken);
        if (take is null) return null;

        var over = $"the take is theirs, so session `{last.Id}` is not carried on over it.";
        if (!string.Equals(take.Machine, quests.Machine, StringComparison.Ordinal))
        {
            return await sessions.LastTeammateForQuestAsync(quest.Id, ct).ConfigureAwait(false) is { } teammate
                ? $"Quest `#{quest.Id}` is taken on `{teammate.Origin}`, by session `{teammate.Id}`: {over}"
                : $"Quest `#{quest.Id}` is taken on another machine: {over}";
        }

        return !tookHere && !broughtForward && take.At > last.Updated
            ? $"Quest `#{quest.Id}` was taken here after session `{last.Id}` ended, by a chat or by work outside Daoris: {over}"
            : null;
    }
}
