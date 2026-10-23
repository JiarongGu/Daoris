using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The supervisor's decisions, live in this instance's memory (SUPERVISOR1a-i, D208; the supervisor design §3.4, §3.6, §6.1, §6.3):
/// one per instance, shared by the modules' handler and the driver's look, since both run in the window's process.
/// </summary>
/// <remarks>
/// <para><b>Every check reads memory alone.</b> Each live decision is kept whole here: its question's snapshot, choice, reasons, holder,
/// generation, when it was kept and goes on, its state, the release's receipt, the id of the word its release wrote, and an admission's
/// ticket. The release, the hold, the admission, the hand, the attribution and the overrule read nothing else.</para>
///
/// <para><b>The files are written and never read back to decide</b>: <c>&lt;session&gt;.decisions/&lt;decision&gt;.json</c>, written once,
/// and its <c>.states</c> lines appended, for the session's history, the card after a restart and the start's sweep; and the
/// current-question pointer <c>&lt;session&gt;.supervisor.json</c>. They are in an agent's ordinary reach, so a file that differs from
/// what memory wrote moves its decision to <c>stale</c> (<see cref="SupervisorWhy.Tampered"/>), compared under the decision's lock so a
/// state line being appended is never read as a difference. A decision file an agent writes is never in memory, so never released nor
/// handed.</para>
///
/// <para><b>One lock per session</b>, async, which may be held across a call to the service; every move is made under it, and the lock
/// order is the decision's, then the judge's briefly (inside <see cref="IDecisionAdmission"/>, never across an await), then the ledger's
/// step inside the service.</para>
///
/// <para><b>A restart empties memory</b>: the start's sweep (<see cref="SweepAtStart"/>) reads the files only to close them, <c>held</c>
/// and <c>released</c> to <c>stale</c> and <c>admitted</c> to <c>interrupted</c>, and remembers each closed question as the owner's.</para>
/// </remarks>
public sealed class QuestionSupervisorDecisions(string home, IDecisionAdmission admission, MachineLog? log = null)
{
    /// <summary>Row 1's overrule window (§6.3): five minutes from the keeping. SUPERVISOR1e lets the appointment set 1 to 60.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(5);

    public const string DecisionsSuffix = ".decisions";
    public const string PointerSuffix = ".supervisor.json";

    /// <summary>A reason's bounds, and how many (§3.1): 1 to 5, each <c>says</c> 1-300 and <c>source</c> 1-200.</summary>
    public const int MostReasons = 5, MaxSays = 300, MaxSource = 200;

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Live> _live = new(StringComparer.Ordinal);

    /// <summary>Questions a decision closed as the owner's, by session then question (§3.1 <c>overruled</c>), across a restart too.</summary>
    private readonly ConcurrentDictionary<(string Session, string Question), SupervisorState> _closed = new();

    /// <summary>One live decision, whole; only ever read or changed under its session's lock.</summary>
    private sealed class Live
    {
        public required string Id { get; init; }
        public required string Session { get; init; }
        public required string Repository { get; init; }
        public required ParkQuestion Question { get; init; }
        public required string Choice { get; init; }
        public required IReadOnlyList<SupervisorReason> Reasons { get; init; }
        public required SupervisorHolder Holder { get; init; }
        public required string Generation { get; init; }
        public required DateTimeOffset KeptAt { get; init; }
        public required DateTimeOffset GoesOnAt { get; init; }
        public required string Written { get; init; }
        public List<string> Lines { get; } = [];
        public SupervisorState State { get; set; } = SupervisorState.Held;
        public DateTimeOffset StateAt { get; set; }
        public string? Why { get; set; }
        public string? Receipt { get; set; }
        public string? Word { get; set; }
        public SupervisorTicket? Ticket { get; set; }
    }

    // ——— The lock.

    /// <summary>Run <paramref name="body"/> under <paramref name="session"/>'s decision lock.</summary>
    public async Task<T> UnderAsync<T>(string session, Func<Task<T>> body, CancellationToken ct = default)
    {
        var gate = _locks.GetOrAdd(session, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await body().ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    // ——— Keeping (§3.1, §3.6 `held`).

    /// <summary>
    /// Keep a decision for <paramref name="answer"/> on the park the handler read (§3.1's checks marked 1a-i, then the generation rechecked
    /// under the decision's lock), held for <see cref="Window"/>. A retry of the same form while it is held answers kept with the same
    /// decision; nothing is kept on any refusal.
    /// </summary>
    public Task<SupervisorKept> KeepAsync(SupervisorAnswer answer, SupervisedPark park, DateTimeOffset now, CancellationToken ct = default) =>
        UnderAsync(answer.Session, () =>
        {
            var refused = Judge(answer, park);
            if (refused is not null)
            {
                log?.Info("supervisor.refused", ("why", refused));
                return Task.FromResult(new SupervisorKept(false, null, null, refused));
            }

            // Memory first: a decision already on this question answers for it.
            if (_live.Values.FirstOrDefault(each => each.Session == answer.Session && each.Question.Id == answer.Question) is { } held)
            {
                if (held.State == SupervisorState.Held && Same(held, answer)) return Task.FromResult(new SupervisorKept(true, held.Id, held.GoesOnAt, null));
                var why = SupervisorStates.OwnersAgain(held.State) ? SupervisorWhy.Overruled : SupervisorWhy.Answered;
                log?.Info("supervisor.refused", ("why", why));
                return Task.FromResult(new SupervisorKept(false, null, null, why));
            }

            if (_closed.ContainsKey((answer.Session, answer.Question)))
            {
                log?.Info("supervisor.refused", ("why", SupervisorWhy.Overruled));
                return Task.FromResult(new SupervisorKept(false, null, null, SupervisorWhy.Overruled));
            }

            // The generation rechecked at the keeping, under the decision's lock: a revoke between the judgement and here keeps nothing.
            if (admission.Admit(park.Repository) is not { Why: null, Generation: { } generation } || generation != answer.Generation)
            {
                log?.Info("supervisor.refused", ("why", SupervisorWhy.AppointmentChanged));
                return Task.FromResult(new SupervisorKept(false, null, null, SupervisorWhy.AppointmentChanged));
            }

            var id = Guid.NewGuid().ToString("N")[..12];
            var decision = new Live
            {
                Id = id, Session = answer.Session, Repository = park.Repository, Question = park.Filed!, Choice = answer.Choice,
                Reasons = answer.Reasons, Holder = answer.Holder, Generation = generation, KeptAt = now, GoesOnAt = now + Window,
                Written = DecisionJson(id, answer, park.Filed!, generation, now, now + Window), StateAt = now,
            };
            Directory.CreateDirectory(FolderOf(answer.Session));
            AtomicFile.WriteText(FileOf(answer.Session, id), decision.Written);
            Append(decision, SupervisorState.Held, now, why: null);
            AtomicFile.WriteText(PointerOf(answer.Session), PointerJson(answer.Question, id));
            _live[id] = decision;
            log?.Info("supervisor.answered", ("session", answer.Session), ("question", answer.Question), ("holder", answer.Holder.Kind),
                ("reasons", answer.Reasons.Count));
            return Task.FromResult(new SupervisorKept(true, id, decision.GoesOnAt, null));
        }, ct);

    /// <summary>§3.1's refusals the park and the form answer, before memory is read; null where none applies.</summary>
    private static string? Judge(SupervisorAnswer answer, SupervisedPark park)
    {
        // D135 point 4: a reason with no source is refused by name; Daoris checks that one is named, never what it says.
        if (answer.Reasons.Any(reason => string.IsNullOrWhiteSpace(reason.Source))) return SupervisorWhy.NoSource;
        if (answer.Reasons.Count is 0 or > MostReasons || answer.Choice.Length == 0
            || answer.Reasons.Any(reason => reason.Says.Trim().Length is 0 or > MaxSays || reason.Source.Length > MaxSource))
        {
            return SupervisorWhy.Form;
        }

        if (!park.Parked) return SupervisorWhy.NotParked;
        if (!park.HoldsQuest) return SupervisorWhy.NoQuest;
        if (park.Filed is not { } filed || filed.Id != answer.Question) return SupervisorWhy.QuestionChanged;
        if (!filed.Choices.Any(choice => string.Equals(choice.Label, answer.Choice, StringComparison.Ordinal))) return SupervisorWhy.NotOfferedChoice;
        if (park.GoAheadUnread) return SupervisorWhy.GoAheadUnread;
        if (park.GoAheadOpen) return SupervisorWhy.GoAheadOpen;
        return park.OwnerWaits ? SupervisorWhy.OwnerAnswered : null;
    }

    private static bool Same(Live held, SupervisorAnswer answer) =>
        held.Choice == answer.Choice && held.Generation == answer.Generation && held.Holder == answer.Holder
        && held.Reasons.SequenceEqual(answer.Reasons);

    // ——— Release (§3.4).

    /// <summary>
    /// Release each held decision of <paramref name="session"/> whose <c>goesOnAt</c> has come, in one conditional step: under the
    /// decision's lock, live and held in memory and its generation still effective, the service's say is called with no text and the
    /// receipt as its key; only its kept word's id moves it to <c>released</c>. A lost response keeps it held with its receipt, which the
    /// next look repeats; a refusal moves it to <c>stale</c>, or <c>superseded</c> where the owner's words came first.
    /// </summary>
    /// <returns>The states the session's decisions moved to; a word a refusal leaves claiming the decision is the quarantine's.</returns>
    public async Task<IReadOnlyList<SupervisorState>> ReleaseAsync(
        string session, SupervisorSay say, DateTimeOffset now, CancellationToken ct = default)
    {
        var moved = new List<SupervisorState>();
        foreach (var id in LiveIds(session))
        {
            var state = await UnderAsync(session, async () =>
            {
                if (!_live.TryGetValue(id, out var decision) || decision.State != SupervisorState.Held) return (SupervisorState?)null;
                if (decision.Receipt is null && now < decision.GoesOnAt) return null;
                if (admission.Admit(decision.Repository) is not { Why: null, Generation: { } generation } || generation != decision.Generation)
                {
                    return Move(decision, SupervisorState.Stale, now, SupervisorWhy.Revoked);
                }

                decision.Receipt ??= Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
                SupervisorSaid said;
                try
                {
                    said = await say(session, decision.Id, decision.Question.Id, decision.Choice, decision.Receipt, ct).ConfigureAwait(false);
                }
                catch (Exception error) when (error is HttpRequestException or IOException or TaskCanceledException)
                {
                    said = new SupervisorSaid(null, null, Lost: true);
                }

                if (said.Lost) return null;
                if (said.Word is { } word)
                {
                    decision.Word = word;
                    return Move(decision, SupervisorState.Released, now, null);
                }

                if (said.Refusal == "decision-taken")
                {
                    // A word claiming this decision under another key was never the release's: it is taken back by the look's quarantine.
                    log?.Warn("supervisor.unattributed", ("session", session));
                    return Move(decision, SupervisorState.Stale, now, SupervisorWhy.Forged);
                }

                return said.Refusal == SupervisorWhy.OwnerAnswered
                    ? Move(decision, SupervisorState.Superseded, now, said.Refusal)
                    : Move(decision, SupervisorState.Stale, now, said.Refusal ?? SupervisorWhy.NotParked);
            }, ct).ConfigureAwait(false);
            if (state is { } went) moved.Add(went);
        }

        return moved;
    }

    // ——— The look's other moves (§3.6, §6.3).

    /// <summary>
    /// The look's reading of <paramref name="session"/>'s record, before it plans (§6.3, §3.6): a held decision with an owner's word
    /// waiting is <c>superseded</c>; a released one whose word is gone is <c>superseded</c>, and one on a record no longer parked is
    /// <c>stale</c> with its word taken back.
    /// </summary>
    public async Task SettleAsync(
        string session, bool parked, bool ownerWaits, IReadOnlyCollection<string> waiting, SupervisorTakeBack takeBack, DateTimeOffset now,
        CancellationToken ct = default)
    {
        foreach (var id in LiveIds(session))
        {
            await UnderAsync(session, async () =>
            {
                if (!_live.TryGetValue(id, out var decision)) return 0;
                if (decision.State == SupervisorState.Held && ownerWaits) Move(decision, SupervisorState.Superseded, now, null);
                else if (decision.State == SupervisorState.Released && !waiting.Contains(decision.Word!)) Move(decision, SupervisorState.Superseded, now, null);
                else if (decision.State == SupervisorState.Released && !parked)
                {
                    await takeBack(session, decision.Word!, ct).ConfigureAwait(false);
                    Move(decision, SupervisorState.Stale, now, SupervisorWhy.NotParked);
                }

                return 0;
            }, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// <c>CannotGoOn</c> for a park whose own conversation cannot resume (§6.1): its released decision goes <c>stale</c>, its word taken
    /// back, and the park is a wait on the owner again.
    /// </summary>
    public Task CannotGoOnAsync(string session, SupervisorTakeBack takeBack, DateTimeOffset now, CancellationToken ct = default) =>
        StaleAsync(session, SupervisorWhy.CannotGoOn, takeBack, now, ct);

    /// <summary>A revoke's or a replacing appointment's sweep (§5): every held or released decision of the session goes stale.</summary>
    public Task StaleAsync(string session, string why, SupervisorTakeBack takeBack, DateTimeOffset now, CancellationToken ct = default) =>
        UnderAsync(session, async () =>
        {
            foreach (var decision in _live.Values.Where(each => each.Session == session).ToList())
            {
                if (decision.State is not (SupervisorState.Held or SupervisorState.Released)) continue;
                if (decision.Word is { } word) await takeBack(session, word, ct).ConfigureAwait(false);
                Move(decision, SupervisorState.Stale, now, why);
            }

            return 0;
        }, ct);

    /// <summary>
    /// The owner's <b>Overrule</b> (§6.3, <c>SUPERVISOR_OVERRULE</c>), under the decision's lock: while held it moves to <c>overruled</c>
    /// with nothing to take back; while released its word is taken back first. From <c>admitted</c> on it changes nothing and answers
    /// the state. Null where no decision of this session is live by that id.
    /// </summary>
    public Task<SupervisorState?> OverruleAsync(
        string session, string decision, SupervisorTakeBack takeBack, DateTimeOffset now, CancellationToken ct = default) =>
        UnderAsync(session, async () =>
        {
            if (!_live.TryGetValue(decision, out var live) || live.Session != session) return (SupervisorState?)null;
            if (live.State == SupervisorState.Released && !await takeBack(session, live.Word!, ct).ConfigureAwait(false))
            {
                // The take-back refused: the record runs, so the word is the run's; nothing moved.
                return live.State;
            }

            return live.State is SupervisorState.Held or SupervisorState.Released
                ? Move(live, SupervisorState.Overruled, now, null)
                : live.State;
        }, ct);

    /// <summary>
    /// The owner's words named <paramref name="question"/> (§6.3, <c>SESSION_INPUT</c> with its question): every held or released
    /// decision on it moves to <c>superseded</c> at once, since the service's one owner-word operation already took a released word back.
    /// </summary>
    /// <returns>How many moved.</returns>
    public Task<int> SupersedeAsync(string session, string question, DateTimeOffset now, CancellationToken ct = default) =>
        UnderAsync(session, () => Task.FromResult(_live.Values
            .Where(each => each.Session == session && each.Question.Id == question
                           && each.State is SupervisorState.Held or SupervisorState.Released)
            .ToList()
            .Count(each => Move(each, SupervisorState.Superseded, now, null) == SupervisorState.Superseded)), ct);

    // ——— Admission by ticket (§6.3).

    /// <summary>
    /// The go-on's move to working, under the decision's lock: a held decision moves nothing (null, and the next look plans again); a
    /// released one rechecks the generation (stale where a revoke came first) and calls <paramref name="move"/> with <c>expect</c>, the
    /// exact ids of the handable supervisor's words; the move succeeding makes it <c>admitted</c> with a single-use ticket. A session with
    /// no live decision moves with <c>expect</c> empty.
    /// </summary>
    /// <returns>Whether the record moved, and the ticket where a supervisor's word was admitted.</returns>
    public Task<(bool Moved, SupervisorTicket? Ticket)> AdmitAsync(
        string session, Func<IReadOnlyList<string>, Task<bool>> move, string goOn, DateTimeOffset now, CancellationToken ct = default) =>
        UnderAsync(session, async () =>
        {
            var live = _live.Values.Where(each => each.Session == session && !SupervisorStates.Final(each.State)).ToList();
            if (live.Any(each => each.State == SupervisorState.Held)) return (false, (SupervisorTicket?)null);
            if (live.FirstOrDefault(each => each.State == SupervisorState.Released) is not { } released)
            {
                return (await move([]).ConfigureAwait(false), (SupervisorTicket?)null);
            }

            if (admission.Admit(released.Repository) is not { Why: null, Generation: { } generation } || generation != released.Generation)
            {
                Move(released, SupervisorState.Stale, now, SupervisorWhy.Revoked);
                return (false, null);
            }

            if (!await move([released.Word!]).ConfigureAwait(false)) return (false, null);
            released.Ticket = new SupervisorTicket(session, released.Id, released.Word!, goOn);
            Move(released, SupervisorState.Admitted, now, null);
            return (true, released.Ticket);
        }, ct);

    /// <summary>
    /// The paragraph the ticket's prompt hands (§4.5), composed from the decision in memory, never the word's stored text; consuming the
    /// ticket, so a second build with it hands nothing. Null for a ticket already used or not this decision's.
    /// </summary>
    public string? Hand(SupervisorTicket ticket)
    {
        if (!_live.TryGetValue(ticket.Decision, out var decision) || decision.State != SupervisorState.Admitted) return null;
        lock (decision)
        {
            if (decision.Ticket is null || decision.Ticket != ticket) return null;
            decision.Ticket = null;
        }

        return SupervisorHanded.Text(decision.Choice);
    }

    /// <summary>The run built with the ticket's word was spawned (§6.3): <c>went-on</c>, at that moment.</summary>
    public Task<bool> WentOnAsync(SupervisorTicket ticket, DateTimeOffset at, CancellationToken ct = default) =>
        AfterAdmissionAsync(ticket, SupervisorState.WentOn, null, at, ct);

    /// <summary>A failure between the admission and the wire (§6.3): <c>not-handed</c>, its caller restoring the park less the word.</summary>
    public Task<bool> NotHandedAsync(SupervisorTicket ticket, string why, DateTimeOffset at, CancellationToken ct = default) =>
        AfterAdmissionAsync(ticket, SupervisorState.NotHanded, why, at, ct);

    private Task<bool> AfterAdmissionAsync(SupervisorTicket ticket, SupervisorState to, string? why, DateTimeOffset at, CancellationToken ct) =>
        UnderAsync(ticket.Session, () => Task.FromResult(
            _live.TryGetValue(ticket.Decision, out var decision) && decision.State == SupervisorState.Admitted
            && Move(decision, to, at, why) == to), ct);

    // ——— Attribution (§6.1).

    /// <summary>
    /// Whether <paramref name="word"/> is the supervisor's: its id is the one memory recorded from a live decision's own release, from
    /// <c>released</c> through <c>admitted</c> and <c>went-on</c>. Ids, never text: an identical word posted by another is not.
    /// </summary>
    public bool Authored(string session, string word) =>
        _live.Values.Any(each => each.Session == session && each.Word == word
            && each.State is SupervisorState.Released or SupervisorState.Admitted or SupervisorState.WentOn);

    /// <summary>
    /// Whether <paramref name="word"/> may be planned or handed: authored, and its decision <c>released</c> (eligible to plan), or
    /// <c>admitted</c> under <paramref name="ticket"/> (handed).
    /// </summary>
    public bool Handable(string session, string word, SupervisorTicket? ticket = null) =>
        _live.Values.Any(each => each.Session == session && each.Word == word
            && (each.State == SupervisorState.Released
                || (each.State == SupervisorState.Admitted && ticket is not null && ticket.Decision == each.Id && ticket.Word == word)));

    /// <summary>A word claiming the supervisor that Daoris did not attribute: quarantined, never handed, taken back by the next look.</summary>
    public bool Unattributed(string session, string word) => !Authored(session, word);

    /// <summary>
    /// The hold (§6.3): the moment a held decision of <paramref name="session"/> goes on, or null where none is held. While it is held
    /// and no word of the owner's waits, the record is held whatever else waits.
    /// </summary>
    public DateTimeOffset? HeldUntil(string session) =>
        _live.Values.Where(each => each.Session == session && each.State == SupervisorState.Held)
            .Select(each => (DateTimeOffset?)each.GoesOnAt).Min();

    /// <summary>The soonest moment a held decision goes on, across every session, for the loop's nudge (§3.4); null where none is held.</summary>
    public DateTimeOffset? NextGoesOn() =>
        _live.Values.Where(each => each.State == SupervisorState.Held).Select(each => (DateTimeOffset?)each.GoesOnAt).Min();

    /// <summary>The sessions a held decision holds now (§6.3), for a look that reads no snapshot of the driver's.</summary>
    public IReadOnlySet<string> HeldSessions() =>
        new HashSet<string>(_live.Values.Where(each => each.State == SupervisorState.Held).Select(each => each.Session), StringComparer.Ordinal);

    /// <summary>The sessions a held or released decision answers for now (§4.2): their parks are not a wait on the owner.</summary>
    public IReadOnlySet<string> Answering() => new HashSet<string>(
        _live.Values.Where(each => each.State is SupervisorState.Held or SupervisorState.Released).Select(each => each.Session),
        StringComparer.Ordinal);

    /// <summary>Whether <paramref name="session"/> has a decision that is not final, which the look settles, compares and releases.</summary>
    public bool Moving(string session) => _live.Values.Any(each => each.Session == session && !SupervisorStates.Final(each.State));

    /// <summary>The decisions live in memory for <paramref name="session"/>, as they stand.</summary>
    public IReadOnlyList<SupervisorDecisionView> LiveOf(string session) =>
        [.. _live.Values.Where(each => each.Session == session).OrderBy(each => each.KeptAt).Select(View)];

    /// <summary>
    /// <paramref name="session"/>'s decisions for its history (§6.1): those live here from memory, every other read from its files as
    /// history, marked <see cref="SupervisorDecisionView.Earlier"/>, granting nothing.
    /// </summary>
    public IReadOnlyList<SupervisorDecisionView> History(string session)
    {
        var views = LiveOf(session).ToList();
        if (!SessionEventText.IsLocalId(session) || !Directory.Exists(FolderOf(session))) return views;
        foreach (var file in Directory.EnumerateFiles(FolderOf(session), "*.json"))
        {
            var id = Path.GetFileNameWithoutExtension(file);
            if (_live.ContainsKey(id) || Earlier(session, id) is not { } earlier) continue;
            views.Add(earlier);
        }

        return [.. views.OrderBy(view => view.KeptAt)];
    }

    // ——— The files compared (§3.4).

    /// <summary>
    /// Compare every live decision's files of <paramref name="session"/> with what memory wrote, each under the decision's lock, the
    /// whole JSON and every state line: a file that differs moves its decision to <c>stale</c> as tampered, its word taken back.
    /// </summary>
    /// <returns>How many went stale.</returns>
    public async Task<int> CompareAsync(string session, SupervisorTakeBack takeBack, DateTimeOffset now, CancellationToken ct = default)
    {
        var tampered = 0;
        foreach (var id in LiveIds(session))
        {
            tampered += await UnderAsync(session, async () =>
            {
                if (!_live.TryGetValue(id, out var decision) || SupervisorStates.Final(decision.State)) return 0;
                if (Read(FileOf(session, id)) == decision.Written && Read(StatesOf(session, id)) == string.Concat(decision.Lines)) return 0;
                if (decision.State == SupervisorState.Admitted) return 0;
                if (decision.Word is { } word) await takeBack(session, word, ct).ConfigureAwait(false);
                Move(decision, SupervisorState.Stale, now, SupervisorWhy.Tampered);
                return 1;
            }, ct).ConfigureAwait(false);
        }

        return tampered;
    }

    // ——— The start (§3.4, §3.6).

    /// <summary>
    /// The start's sweep, before the first look's carry-ons: every decision file on disk read only to close it, <c>held</c> and
    /// <c>released</c> to <c>stale</c> (<see cref="SupervisorWhy.Restarted"/>), <c>admitted</c> to <c>interrupted</c>; each closed
    /// question remembered as the owner's. Memory is empty, so nothing is live and nothing is handable.
    /// </summary>
    /// <returns>The sessions with a decision the sweep closed, whose waiting supervisor's words the look takes back.</returns>
    public IReadOnlyList<string> SweepAtStart(DateTimeOffset now)
    {
        var root = DaorisHome.Entry(home, HomeEntry.Sessions);
        if (!Directory.Exists(root)) return [];
        var swept = new List<string>();
        foreach (var folder in Directory.EnumerateDirectories(root, "*" + DecisionsSuffix))
        {
            var session = Path.GetFileName(folder)[..^DecisionsSuffix.Length];
            if (!SessionEventText.IsLocalId(session)) continue;
            foreach (var file in Directory.EnumerateFiles(folder, "*.json"))
            {
                var id = Path.GetFileNameWithoutExtension(file);
                if (_live.ContainsKey(id) || Earlier(session, id) is not { } earlier) continue;
                var closed = earlier.State switch
                {
                    SupervisorState.Held or SupervisorState.Released => SupervisorState.Stale,
                    SupervisorState.Admitted => SupervisorState.Interrupted,
                    _ => (SupervisorState?)null,
                };
                if (closed is { } to)
                {
                    TryAppend(StatesOf(session, id), StateLine(to, now, to == SupervisorState.Stale ? SupervisorWhy.Restarted : null, null));
                    log?.Info(to == SupervisorState.Stale ? "supervisor.stale" : "supervisor.interrupted", ("why", SupervisorWhy.Restarted));
                    if (!swept.Contains(session)) swept.Add(session);
                }

                var state = closed ?? earlier.State;
                if (SupervisorStates.OwnersAgain(state)) _closed[(session, earlier.Question.Id)] = state;
            }
        }

        return swept;
    }

    // ——— Moves and files.

    private IReadOnlyList<string> LiveIds(string session) =>
        [.. _live.Values.Where(each => each.Session == session).OrderBy(each => each.KeptAt).Select(each => each.Id)];

    /// <summary>Move <paramref name="decision"/> under its lock, by §3.6's table alone; a move not in it is refused and changes nothing.</summary>
    private SupervisorState Move(Live decision, SupervisorState to, DateTimeOffset at, string? why)
    {
        if (!SupervisorStates.Moves(decision.State, to)) return decision.State;
        Append(decision, to, at, why);
        if (SupervisorStates.OwnersAgain(to)) _closed[(decision.Session, decision.Question.Id)] = to;
        if (to != SupervisorState.Admitted) decision.Ticket = null;
        var code = $"supervisor.{SupervisorStates.Spell(to)}";
        if (why is null) log?.Info(code, ("session", decision.Session));
        else log?.Info(code, ("session", decision.Session), ("why", why));
        return to;
    }

    /// <summary>Exposed for the tests of §3.6's table: try any move on a live decision; true where it moved.</summary>
    internal Task<bool> TryMoveAsync(string session, string decision, SupervisorState to, DateTimeOffset at) =>
        UnderAsync(session, () => Task.FromResult(
            _live.TryGetValue(decision, out var live) && SupervisorStates.Moves(live.State, to) && Move(live, to, at, null) == to));

    private void Append(Live decision, SupervisorState to, DateTimeOffset at, string? why)
    {
        var line = StateLine(to, at, why, to == SupervisorState.Released ? decision.Word : null);
        decision.State = to;
        decision.StateAt = at;
        decision.Why = why;
        decision.Lines.Add(line);
        TryAppend(StatesOf(decision.Session, decision.Id), line);
    }

    private static void TryAppend(string path, string line)
    {
        try
        {
            File.AppendAllText(path, line, new UTF8Encoding(false));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // The files are history; memory decides. A line that could not be written reads as a difference at the next comparison.
        }
    }

    private static string? Read(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, new UTF8Encoding(false));
            return reader.ReadToEnd();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private SupervisorDecisionView View(Live each) => new(
        each.Id, each.Session, each.Question, each.Choice, each.Reasons, each.Holder, each.Generation, each.KeptAt, each.GoesOnAt,
        each.State, each.StateAt, each.Why, each.Word, Earlier: false);

    /// <summary>A decision read from its files as history: never live, never handable.</summary>
    private SupervisorDecisionView? Earlier(string session, string id)
    {
        try
        {
            if (Read(FileOf(session, id)) is not { } text) return null;
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("question", out var asked)) return null;
            var question = QuestionFiles.Parsed(asked.GetRawText(), session, () => DateTimeOffset.MinValue);
            if (question is null || Json.Text(root, "choice") is not { } choice) return null;
            var reasons = Json.Items(root, "reasons")
                .Select(reason => new SupervisorReason(Json.Text(reason, "says") ?? "", Json.Text(reason, "source") ?? ""))
                .ToList();
            var holder = root.TryGetProperty("holder", out var held) && held.ValueKind == JsonValueKind.Object
                ? new SupervisorHolder(Json.Text(held, "kind") ?? "", Json.Text(held, "parent"))
                : new SupervisorHolder("", null);
            var kept = Moments.Read(root, "keptAt") ?? DateTimeOffset.MinValue;
            var (state, at, why, word) = (SupervisorState.Held, kept, (string?)null, (string?)null);
            foreach (var line in (Read(StatesOf(session, id)) ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                using var each = JsonDocument.Parse(line);
                if (SupervisorStates.Parse(Json.Text(each.RootElement, "state")) is not { } said) continue;
                (state, at, why) = (said, Moments.Read(each.RootElement, "at") ?? at, Json.Text(each.RootElement, "why"));
                word = Json.Text(each.RootElement, "word") ?? word;
            }

            return new SupervisorDecisionView(
                id, session, question, choice, reasons, holder, Json.Text(root, "appointment") ?? "", kept,
                Moments.Read(root, "goesOnAt") ?? kept, state, at, why, word, Earlier: true);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private string FolderOf(string session) => DaorisHome.Entry(home, HomeEntry.Sessions, session + DecisionsSuffix);

    private string FileOf(string session, string id) => Path.Combine(FolderOf(session), id + ".json");

    private string StatesOf(string session, string id) => Path.Combine(FolderOf(session), id + ".states");

    private string PointerOf(string session) => DaorisHome.Entry(home, HomeEntry.Sessions, session + PointerSuffix);

    private static string StateLine(SupervisorState state, DateTimeOffset at, string? why, string? word) => Written(writer =>
    {
        writer.WriteString("state", SupervisorStates.Spell(state));
        writer.WriteString("at", Moments.Write(at));
        if (why is not null) writer.WriteString("why", why);
        if (word is not null) writer.WriteString("word", word);
    }, indented: false) + "\n";

    private static string PointerJson(string question, string decision) => Written(writer =>
    {
        writer.WriteString("question", question);
        writer.WriteString("decision", decision);
    }) + "\n";

    private static string DecisionJson(
        string id, SupervisorAnswer answer, ParkQuestion question, string generation, DateTimeOffset kept, DateTimeOffset goesOn) => Written(writer =>
    {
        writer.WriteString("decision", id);
        writer.WriteString("session", answer.Session);
        writer.WriteStartObject("question");
        writer.WriteString("id", question.Id);
        writer.WriteString("question", question.Question);
        writer.WriteStartArray("choices");
        foreach (var choice in question.Choices)
        {
            writer.WriteStartObject();
            writer.WriteString("label", choice.Label);
            writer.WriteString("does", choice.Does);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        if (question.Recommend is { } recommend) writer.WriteNumber("recommend", recommend);
        if (question.By is not null) writer.WriteString("by", question.By);
        if (question.Why is not null) writer.WriteString("why", question.Why);
        writer.WriteString("at", Moments.Write(question.At));
        writer.WriteEndObject();
        writer.WriteString("choice", answer.Choice);
        writer.WriteStartArray("reasons");
        foreach (var reason in answer.Reasons)
        {
            writer.WriteStartObject();
            writer.WriteString("says", reason.Says);
            writer.WriteString("source", reason.Source);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteStartObject("holder");
        writer.WriteString("kind", answer.Holder.Kind);
        if (answer.Holder.Parent is not null) writer.WriteString("parent", answer.Holder.Parent);
        writer.WriteEndObject();
        writer.WriteString("appointment", generation);
        writer.WriteString("keptAt", Moments.Write(kept));
        writer.WriteString("goesOnAt", Moments.Write(goesOn));
    }) + "\n";

    private static string Written(Action<Utf8JsonWriter> body, bool indented = true)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = indented }))
        {
            writer.WriteStartObject();
            body(writer);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n");
    }
}
