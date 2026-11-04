using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using static Daoris.Driver.IntegrationRows;
using static Daoris.Driver.IntegrationStepKeeping;

namespace Daoris.Driver;

/// <summary>
/// The integration's family in the desktop store (INTEGRATE1e part e2, D215; the integration design §5.2, §5.6, D212 §13): Workplace's
/// requests, candidates, members and steps, in <c>integration_requests</c>, <c>integration_candidates</c>, <c>integration_members</c> and
/// <c>integration_steps</c>. Written by the one door (<see cref="IntegrationRequests"/>) and by the line's run (<see cref="IntegrationLines"/>),
/// whose steps run over it.
/// </summary>
/// <remarks>
/// <para><b>One open request per repository and branch, one standing candidate per line, one standing membership per request, one open step
/// per candidate.</b> Each is a partial unique index the write's single insert meets inside its <c>BEGIN IMMEDIATE</c>: where the index refuses
/// it, the row that stands is read under the same lock and answered, and the transaction rolled back, so nothing is written and no revision
/// moves. No read before the insert decides it.</para>
/// <para><b>The claim is the process run that forms the candidate</b> (<c>claim_run</c>, a row of <c>store_runs</c>, and
/// <c>claim_generation</c>), as the move record's is: every step's start and end, every member's change and the candidate's end is one store
/// transaction that checks the claim first, inside the transaction, and names it in its own update's condition. A candidate is taken over only
/// from a run dead by the lease's own test, never from the taker's own and never from a live one, in one update that raises the generation, so
/// a run that lost it and comes back writes nothing more.</para>
/// <para><b>Each step's start is written before its effect and its end after</b>, each in its own transaction (<see cref="IntegrationSteps"/>
/// runs them). A start with no end is a step a crash cut, which a resume judges by asking git, never the store alone.</para>
/// <para>Opened for each use and closed, under the process's one run (<see cref="StoreProcess"/>), as the bindings' and the moves' stores are; a
/// read never creates the store, and a store from before the family reads as holding nothing. Repositories are named in any case, as
/// <see cref="SessionBranches.SameBranch"/> names them; branches and lines exactly. No text a person typed is kept beyond a request's gate
/// proof as its landing's record keeps it, and no path or credential.</para>
/// </remarks>
/// <param name="home">The home whose store this is.</param>
/// <param name="settled">The reading of the start's confirmation; null reads this install's swap journal.</param>
/// <param name="alive">Whether a run that wrote the store still lives; null reads <see cref="Lives"/>, the lease's own test. A test hands one, since one process is one run.</param>
/// <param name="run">The run this store writes and claims under; null for the process's own.</param>
/// <param name="kind">The run's kind; null for the process's own.</param>
/// <param name="clock">The clock of every moment written.</param>
public sealed class IntegrationStore(
    string home, Func<StoreSettled>? settled = null, Func<StoreRun, bool>? alive = null, string? run = null, string? kind = null, TimeProvider? clock = null)
    : IIntegrationRequestStore
{
    /// <summary>The family's name in <c>store_revisions</c>.</summary>
    public const string Family = "integration";

    /// <summary>The store's step that made the family: a store before it holds no integration.</summary>
    private const int Since = 6;

    /// <summary>The store's step that made the pending dispositions' table (INTEGRATE1e part e4b): a store before it holds none.</summary>
    private const int DispositionsSince = 7;

    /// <summary>The store's step that made the decides, the queue's order and a disposition's answer (INTEGRATE1h): a store before it holds none.</summary>
    private const int ActsSince = 12;

    /// <summary>The store's step that made the epochs' table (LANDNODE1g): a store before it holds none, and every record reads as its own epoch.</summary>
    private const int EpochsSince = 14;

    private const string RequestSubject = "request";

    private const string CandidateSubject = "candidate";

    /// <summary>SQLite's refusal of a row a unique index already holds.</summary>
    internal const int UniqueRefused = 2067;

    private const string RequestColumns =
        "id, repository, workspace, branch, tip, route, line, requester, door, act, generation, policy_request, policy_commit, policy_batch, "
        + "policy_order, policy_gates, policy_unreached, policy_reached, policy_conflicts, policy_overlap, policy_line, rule_source, unit, session, "
        + "quest, landed_at, proof, at, closed_at, closed_why";

    /// <summary>A request read: its columns and, from the epochs' step, the epoch kept beside it (a store before it reads none).</summary>
    private static string Read(int version, string table = "integration_requests") =>
        version < EpochsSince
            ? RequestColumns
            : RequestColumns + $", (SELECT e.epoch FROM integration_epochs e WHERE e.subject = '{RequestSubject}' AND e.id = {table}.id) AS epoch";

    private const string CandidateColumns =
        "id, repository, workspace, line, base, job, claim_run, claim_generation, formed_at, tip, promoted_at, ended_at, ended_why";

    private const string MemberColumns = "candidate, request, position, state, joined_at, replayed, contained_by, said, ended_at";

    private const string StepColumns = "id, candidate, step, attempt, claim_run, claim_generation, began_at, request, line_commit, ended_at, outcome, said";

    private readonly Func<StoreSettled> _settled = settled ?? StartConfirmation.Read;

    internal readonly Func<StoreRun, bool> _alive = alive ?? Lives;

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    /// <summary>The store's file in this home.</summary>
    public string Location => DesktopStore.LocationIn(home);

    /// <summary>The run this store writes and claims under.</summary>
    public string Run { get; } = run ?? StoreProcess.Run;

    /// <summary>
    /// The lease's own test of a run (§13.4): alive while its process runs under the start time it recorded; a start it could not read when it
    /// began counts as alive, as the lease's <i>cannot tell</i> holds.
    /// </summary>
    public static bool Lives(StoreRun run) => run.Started is not { } started || ProcessLiveness.Alive(run.Pid, started);

    // ——— The requests (§4.6).

    /// <inheritdoc/>
    public IntegrationRequest? OpenFor(string repository, string branch) =>
        Reading(reader => reader.Query(
                $"SELECT {Read(reader.Version)} FROM integration_requests WHERE repository = $repository COLLATE NOCASE AND branch = $branch AND closed_at IS NULL",
                MapRequest, ("$repository", repository), ("$branch", branch))
            .FirstOrDefault(), null);

    /// <summary>
    /// Keep <paramref name="request"/> where no request is open for its repository and branch, in one write the open index judges; else keep
    /// nothing and return the one open, read under the same lock.
    /// </summary>
    /// <exception cref="ArgumentException">The request names no id, repository, branch, tip, route or line, or its policy is not resolved.</exception>
    public IntegrationRequest? Add(IntegrationRequest request)
    {
        Validate(request);
        try
        {
            using var store = Writer();
            return store.Write(Family, write =>
            {
                try
                {
                    InsertRequest(write, request);
                    KeepEpoch(write, RequestSubject, request.Id, request.Epoch ?? IntegrationEpoch.Of(request.Policy, null));
                }
                catch (SqliteException error) when (error.SqliteExtendedErrorCode == UniqueRefused)
                {
                    var open = OpenIn(write, request.Repository, request.Branch);
                    if (open is null) throw;
                    throw new Unwritten(open);
                }

                return (IntegrationRequest?)null;
            });
        }
        catch (Unwritten unwritten)
        {
            return (IntegrationRequest)unwritten.Answer!;
        }
    }

    /// <summary>The epoch a request was admitted under; null for one admitted before epochs, which reads as its own.</summary>
    public string? RequestEpoch(string request) => EpochOf(RequestSubject, request);

    /// <summary>The epoch a candidate was formed under, its first member's; null for one formed before epochs.</summary>
    public string? CandidateEpoch(string candidate) => EpochOf(CandidateSubject, candidate);

    private string? EpochOf(string subject, string id) =>
        Reading(reader => reader.Query(
            "SELECT epoch FROM integration_epochs WHERE subject = $subject AND id = $id", row => row.GetString(0), ("$subject", subject), ("$id", id))
            .FirstOrDefault(), null, EpochsSince);

    private static string? EpochIn(DesktopWrite write, string subject, string id) =>
        write.Query("SELECT epoch FROM integration_epochs WHERE subject = $subject AND id = $id", row => row.GetString(0), ("$subject", subject), ("$id", id))
            .FirstOrDefault();

    private static void KeepEpoch(DesktopWrite write, string subject, string id, string epoch) =>
        write.Execute("INSERT INTO integration_epochs (subject, id, epoch) VALUES ($subject, $id, $epoch)", ("$subject", subject), ("$id", id), ("$epoch", epoch));

    /// <inheritdoc/>
    public IntegrationRequest? FindByKey(string unit, string tip) =>
        Reading(reader => reader.Query(
                $"SELECT {Read(reader.Version)} FROM integration_requests WHERE unit = $unit AND tip = $tip COLLATE NOCASE ORDER BY at DESC, id DESC",
                MapRequest, ("$unit", unit), ("$tip", tip))
            .FirstOrDefault(), null);

    /// <summary>
    /// The newest membership of a request in any candidate, standing or ended; null where it stands in none. What a node's stop reads to
    /// refuse itself once the member promoted (§17.4, F50).
    /// </summary>
    public IntegrationMember? MemberOf(string request) =>
        Reading(reader => reader.Query(
                $"SELECT {MemberColumns} FROM integration_members WHERE request = $request ORDER BY joined_at DESC, position DESC", MapMember, ("$request", request))
            .FirstOrDefault(), null);

    /// <summary>A request by its id, open or closed; null where the store holds none.</summary>
    public IntegrationRequest? Request(string id) =>
        Reading(reader => reader.Query($"SELECT {Read(reader.Version)} FROM integration_requests WHERE id = $id", MapRequest, ("$id", id)).FirstOrDefault(), null);

    /// <summary>Every request of a repository's branch, open or closed, in the order admitted.</summary>
    public IReadOnlyList<IntegrationRequest> Of(string repository, string branch) =>
        Reading(reader => reader.Query(
            $"SELECT {Read(reader.Version)} FROM integration_requests WHERE repository = $repository COLLATE NOCASE AND branch = $branch ORDER BY at, id",
            MapRequest, ("$repository", repository), ("$branch", branch)), []);

    /// <summary>Every open request, in the order admitted.</summary>
    public IReadOnlyList<IntegrationRequest> Open() =>
        Reading(reader => reader.Query($"SELECT {Read(reader.Version)} FROM integration_requests WHERE closed_at IS NULL ORDER BY at, id", MapRequest), []);

    /// <summary>A repository's most recent closed requests, newest first (the Integration view's history).</summary>
    public IReadOnlyList<IntegrationRequest> Closed(string repository, int most) =>
        Reading(reader => reader.Query(
            $"SELECT {Read(reader.Version)} FROM integration_requests WHERE repository = $repository COLLATE NOCASE AND closed_at IS NOT NULL "
            + "ORDER BY closed_at DESC, id DESC LIMIT $most",
            MapRequest, ("$repository", repository), ("$most", most)), []);

    /// <summary>The newest request a unit made, open or closed; null where it made none.</summary>
    public IntegrationRequest? OfUnit(string unit) =>
        Reading(reader => reader.Query(
                $"SELECT {Read(reader.Version)} FROM integration_requests WHERE unit = $unit ORDER BY at DESC, id DESC LIMIT 1", MapRequest, ("$unit", unit))
            .FirstOrDefault(), null);

    /// <summary>
    /// A line's queue (§5.2 step 2's input): its repository's open requests for it that stand in no candidate, in their order (INTEGRATE1h:
    /// those an <c>order</c> moved first, then the rest as admitted, then those it moved last). A request whose member was held for its conflict
    /// (<see cref="IntegrationMemberStates.Held"/>, §5.4) is not queued: it waits for the steward's or the owner's act, and queued again it would
    /// be formed, conflict and be held in every candidate for ever.
    /// </summary>
    public IReadOnlyList<IntegrationRequest> Queued(string repository, string line) =>
        Reading(reader =>
        {
            // A store before the order's column is read by arrival, as the build that wrote it ordered it.
            var ordered = reader.Version >= ActsSince;
            return reader.Query(
                $"""
                SELECT {Read(reader.Version, "r")}{(ordered ? ", queue_order" : "")} FROM integration_requests r
                WHERE r.repository = $repository COLLATE NOCASE AND r.line = $line AND r.closed_at IS NULL
                  AND NOT EXISTS (SELECT 1 FROM integration_members m WHERE m.request = r.id AND m.ended_at IS NULL)
                  AND NOT EXISTS (SELECT 1 FROM integration_members h WHERE h.request = r.id AND h.state = $held)
                ORDER BY {(ordered ? "COALESCE(r.queue_order, 0), " : "")}r.at, r.id
                """,
                MapRequest, ("$repository", repository), ("$line", line), ("$held", IntegrationMemberStates.Held));
        }, []);

    /// <summary>
    /// Move a queued request first or last in its line's queue (§8's <c>order</c>, INTEGRATE1h): before, or after, every other open request of
    /// the line, in one write. Null where it moved; else why not (<see cref="IntegrationOrderRefusals"/>), nothing written.
    /// </summary>
    /// <exception cref="ArgumentException">A place that is neither first nor last.</exception>
    public string? Order(string request, string where)
    {
        Known(where, IntegrationOrders.All, "order");
        if (Request(request) is not { ClosedAt: null }) return IntegrationOrderRefusals.Gone;
        try
        {
            using var store = Writer();
            return store.Write(Family, write =>
            {
                if (RequestIn(write, request) is not { ClosedAt: null } open) throw new Unwritten(IntegrationOrderRefusals.Gone);
                if (write.Scalar<long>("SELECT COUNT(*) FROM integration_members WHERE request = $id AND (ended_at IS NULL OR state = $held)",
                        ("$id", request), ("$held", IntegrationMemberStates.Held)) > 0)
                {
                    throw new Unwritten(IntegrationOrderRefusals.Stands);
                }

                var bound = write.Scalar<long>(
                    $"SELECT {(where == IntegrationOrders.First ? "MIN" : "MAX")}(COALESCE(queue_order, 0)) FROM integration_requests "
                    + "WHERE repository = $repository COLLATE NOCASE AND line = $line AND closed_at IS NULL",
                    ("$repository", open.Repository), ("$line", open.Line));
                write.Execute(
                    "UPDATE integration_requests SET queue_order = $order WHERE id = $id AND closed_at IS NULL",
                    ("$order", where == IntegrationOrders.First ? Math.Min(bound, 0) - 1 : Math.Max(bound, 0) + 1), ("$id", request));
                return (string?)null;
            });
        }
        catch (Unwritten unwritten)
        {
            return (string?)unwritten.Answer;
        }
    }

    /// <summary>
    /// Close an open request that stands in no candidate (its requester's drop, before it is formed); whether it was open. A request standing
    /// in a candidate closes with that candidate's step, under its claim (<see cref="IntegrationStepEnd.Closes"/>).
    /// </summary>
    /// <exception cref="IntegrationRefusedException">The request stands in a candidate: nothing is written.</exception>
    public bool Close(string id, string why)
    {
        Required(why, "the reason it closes");
        if (Request(id) is not { ClosedAt: null }) return false;
        try
        {
            using var store = Writer();
            return store.Write(Family, write =>
            {
                if (write.Query("SELECT candidate FROM integration_members WHERE request = $id AND ended_at IS NULL", row => row.GetString(0), ("$id", id))
                    is [var candidate, ..])
                {
                    throw new IntegrationRefusedException(candidate, $"request {id} stands in it, and closes with its step, under its claim");
                }

                var closed = write.Execute(
                    "UPDATE integration_requests SET closed_at = $at, closed_why = $why WHERE id = $id AND closed_at IS NULL",
                    ("$at", Now()), ("$why", why), ("$id", id));
                return closed == 1 ? true : throw new Unwritten(false);
            });
        }
        catch (Unwritten unwritten)
        {
            return (bool)unwritten.Answer!;
        }
    }

    // ——— The candidates (§5.2 step 2).

    /// <summary>
    /// Form a candidate (§5.2 step 2) in one transaction: the candidate on its line's commit, claimed by this store's run at generation 1, its
    /// job named by it, each request a member in the order given, and its form step's start; or, where a candidate already stands for the
    /// line, answer that one and write nothing.
    /// </summary>
    /// <exception cref="ArgumentException">The forming names no line or commit, no request, or one twice.</exception>
    /// <exception cref="IntegrationRefusedException">A request is not open, is another repository's or line's, or stands in another candidate: nothing is written.</exception>
    public IntegrationFormed Form(IntegrationForming forming)
    {
        Required(forming.Repository, "its repository");
        Required(forming.Workspace, "its workspace");
        Required(forming.Line, "its line");
        Required(forming.Base, "the line's commit it is formed on");
        if (forming.Requests.Count == 0) throw new ArgumentException("a candidate takes one request or more");
        if (forming.Requests.Distinct(StringComparer.Ordinal).Count() != forming.Requests.Count) throw new ArgumentException("a candidate takes each request once");
        var id = $"candidate-{Guid.NewGuid():N}";
        try
        {
            using var store = Writer();
            return store.Write(Family, write =>
            {
                var at = Now();
                try
                {
                    write.Execute(
                        """
                        INSERT INTO integration_candidates (id, repository, workspace, line, base, job, claim_run, claim_generation, formed_at)
                        VALUES ($id, $repository, $workspace, $line, $base, $job, $run, 1, $at)
                        """,
                        ("$id", id), ("$repository", forming.Repository), ("$workspace", forming.Workspace), ("$line", forming.Line),
                        ("$base", forming.Base), ("$job", IntegrationJobs.NameOf(id)), ("$run", Run), ("$at", at));
                }
                catch (SqliteException error) when (error.SqliteExtendedErrorCode == UniqueRefused)
                {
                    // The partial index refused a second standing candidate of the line: the standing one is the answer, read under the same
                    // lock, and the transaction is rolled back so nothing is written and no revision moves.
                    var standing = StandingIn(write, forming.Repository, forming.Line);
                    if (standing is null) throw;
                    throw new Unwritten(new IntegrationFormed(standing, null, null));
                }

                // The epoch is the first member's; a member of another epoch, or any member of an older record's (its own epoch), is refused.
                var epoch = EpochIn(write, RequestSubject, forming.Requests[0]);
                if (epoch is not null) KeepEpoch(write, CandidateSubject, id, epoch);
                for (var position = 0; position < forming.Requests.Count; position++)
                {
                    var request = forming.Requests[position];
                    if (forming.Requests.Count > 1 && !IntegrationEpoch.Shared(epoch, EpochIn(write, RequestSubject, request)))
                    {
                        throw new IntegrationRefusedException(id, $"request {request} was not admitted under the epoch of request {forming.Requests[0]}");
                    }

                    var held = RequestIn(write, request) ?? throw new IntegrationRefusedException(id, $"the store holds no request {request}");
                    if (held.ClosedAt is not null) throw new IntegrationRefusedException(id, $"request {request} is closed ({held.ClosedWhy})");
                    if (!string.Equals(held.Repository, forming.Repository, StringComparison.OrdinalIgnoreCase)
                        || !string.Equals(held.Line, forming.Line, StringComparison.Ordinal))
                    {
                        throw new IntegrationRefusedException(id, $"request {request} integrates `{held.Repository}`'s `{held.Line}`, not `{forming.Repository}`'s `{forming.Line}`");
                    }

                    try
                    {
                        write.Execute(
                            "INSERT INTO integration_members (candidate, request, position, state, joined_at) VALUES ($candidate, $request, $position, $state, $at)",
                            ("$candidate", id), ("$request", request), ("$position", position), ("$state", IntegrationMemberStates.Formed), ("$at", at));
                    }
                    catch (SqliteException error) when (error.SqliteExtendedErrorCode == UniqueRefused)
                    {
                        throw new IntegrationRefusedException(id, $"request {request} stands in another candidate");
                    }
                }

                var step = InsertStep(write, new IntegrationClaim(id, Run, 1), IntegrationStepNames.Form, null, forming.Base, at);
                return new IntegrationFormed(CandidateIn(write, id)!, new IntegrationClaim(id, Run, 1), step);
            });
        }
        catch (Unwritten unwritten)
        {
            return (IntegrationFormed)unwritten.Answer!;
        }
    }

    /// <summary>A candidate by its id, standing or ended; null where the store holds none.</summary>
    public IntegrationCandidate? Candidate(string id) =>
        Reading(reader => reader.Query($"SELECT {CandidateColumns} FROM integration_candidates WHERE id = $id", MapCandidate, ("$id", id)).FirstOrDefault(), null);

    /// <summary>The candidate standing for a repository's line, or null.</summary>
    public IntegrationCandidate? StandingFor(string repository, string line) =>
        Reading(reader => reader.Query(
                $"SELECT {CandidateColumns} FROM integration_candidates WHERE repository = $repository COLLATE NOCASE AND line = $line AND ended_at IS NULL",
                MapCandidate, ("$repository", repository), ("$line", line))
            .FirstOrDefault(), null);

    /// <summary>
    /// The candidate of a repository's line that ended last, or null where none has (INTEGRATE1e part e4a): what the next form reads to know the
    /// last one was split, so it takes one member. Read by the end's own moment; the later formed of two that ended together.
    /// </summary>
    public IntegrationCandidate? LastEnded(string repository, string line) =>
        Reading(reader => reader.Query(
                $"SELECT {CandidateColumns} FROM integration_candidates WHERE repository = $repository COLLATE NOCASE AND line = $line AND ended_at IS NOT NULL "
                + "ORDER BY ended_at DESC, formed_at DESC, rowid DESC LIMIT 1",
                MapCandidate, ("$repository", repository), ("$line", line))
            .FirstOrDefault(), null);

    /// <summary>Every standing candidate, oldest first: what a come-up reads to resume.</summary>
    public IReadOnlyList<IntegrationCandidate> Standing() =>
        Reading(reader => reader.Query($"SELECT {CandidateColumns} FROM integration_candidates WHERE ended_at IS NULL ORDER BY formed_at, id", MapCandidate), []);

    /// <summary>A candidate's members, in their order, standing and ended.</summary>
    public IReadOnlyList<IntegrationMember> Members(string candidate) =>
        Reading(reader => reader.Query(
            $"SELECT {MemberColumns} FROM integration_members WHERE candidate = $candidate ORDER BY position", MapMember, ("$candidate", candidate)), []);

    /// <summary>A candidate's steps' attempts, in the order begun.</summary>
    public IReadOnlyList<IntegrationStep> Steps(string candidate) =>
        Reading(reader => reader.Query($"SELECT {StepColumns} FROM integration_steps WHERE candidate = $candidate ORDER BY id", MapStep, ("$candidate", candidate)), []);

    /// <summary>The candidate's one open step, begun and not ended; null where none is.</summary>
    public IntegrationStep? OpenStep(string candidate) =>
        Reading(reader => reader.Query(
                $"SELECT {StepColumns} FROM integration_steps WHERE candidate = $candidate AND ended_at IS NULL", MapStep, ("$candidate", candidate))
            .FirstOrDefault(), null);

    private IntegrationStepKeeping? _steps;

    private IntegrationStepKeeping Keeper => _steps ??= new(this);

    /// <inheritdoc cref="IntegrationStepKeeping.Take"/>
    public IntegrationTaken Take(string candidate, long generation) => Keeper.Take(candidate, generation);

    /// <inheritdoc cref="IntegrationStepKeeping.Begin"/>
    public IntegrationStep Begin(IntegrationClaim claim, string step, string? request = null, string? lineCommit = null) => Keeper.Begin(claim, step, request, lineCommit);

    /// <inheritdoc cref="IntegrationStepKeeping.End"/>
    public IntegrationStep End(IntegrationClaim claim, long step, IntegrationStepEnd end) => Keeper.End(claim, step, end);

    /// <summary>Every disposition a request has, pending and answered, in the order they were kept; none where the home has no store or one from before them.</summary>
    public IReadOnlyList<IntegrationDisposition> Dispositions(string request) =>
        Reading(reader => reader.Query(
            $"SELECT request, commit_id, candidate, reference, by_commit, files, subject, at, answered_at{(reader.Version >= ActsSince ? ", answer" : "")} "
            + "FROM integration_dispositions WHERE request = $request ORDER BY at, rowid", MapDisposition, ("$request", request)), [], DispositionsSince);

    /// <summary>
    /// Every disposition no one has answered yet, oldest first: visible on the Integration view and the session's head until the session, the
    /// steward or the owner answers it, even once the review row of its request has gone (§5.7).
    /// </summary>
    public IReadOnlyList<IntegrationDisposition> Pending() =>
        Reading(reader => reader.Query(
            "SELECT request, commit_id, candidate, reference, by_commit, files, subject, at, answered_at FROM integration_dispositions "
            + "WHERE answered_at IS NULL ORDER BY at, rowid", MapDisposition), [], DispositionsSince);

    /// <summary>
    /// A disposition answered, by its word where one is given (<see cref="IntegrationAnswers"/>): it leaves the pending list and its row stays. False
    /// where none is pending for that request and commit. The answerer is a person's or a session's act, not the run's, so no claim is checked.
    /// </summary>
    /// <exception cref="ArgumentException">A word that is no answer.</exception>
    public bool Answer(string request, string commit, string? answer = null)
    {
        if (answer is not null) Known(answer, IntegrationAnswers.All, "answer");
        if (Reading(reader => reader.Version, 0) < DispositionsSince) return false;
        using var store = Writer();
        return store.Write(Family, write => write.Execute(
            "UPDATE integration_dispositions SET answered_at = $at, answer = $answer WHERE request = $request AND commit_id = $commit AND answered_at IS NULL",
            ("$at", Now()), ("$answer", answer), ("$request", request), ("$commit", commit))) == 1;
    }

    // ——— A held candidate's decide (INTEGRATE1h; §4.5, §8).

    /// <summary>The decide recorded on a candidate; null where none is, or the store is from before the decides.</summary>
    public IntegrationDecide? DecideOf(string candidate) =>
        Reading(reader => reader.Query(
                "SELECT candidate, decide, requester, at FROM integration_decides WHERE candidate = $candidate",
                row => new IntegrationDecide(row.GetString(0), row.GetString(1), row.GetString(2), Moment(row, "at")), ("$candidate", candidate))
            .FirstOrDefault(), null, ActsSince);

    /// <summary>
    /// Record a decide on a candidate held for one (§4.5's <c>hold</c>: its last step a judge ended <c>held</c>), once, in one write that judges
    /// it under the lock. The person's or the steward's act, not the run's, so no claim is checked: the run consumes it at its next look. Refused,
    /// nothing written, for a candidate that does not stand, one not held for a decide, one decided already (whose decide is answered), and a
    /// <c>land</c> by anyone but the person.
    /// </summary>
    /// <exception cref="ArgumentException">A word that is no decide, or no requester.</exception>
    public IntegrationDecided Decide(string candidate, string decide, string requester)
    {
        Known(decide, IntegrationDecides.All, "decide");
        Known(requester, [IntegrationRequester.Person, IntegrationRequester.Steward], "decider");
        if (decide == IntegrationDecides.Land && requester != IntegrationRequester.Person) return new IntegrationDecided(null, IntegrationDecideRefusals.Land);
        if (Candidate(candidate) is not { Stands: true }) return new IntegrationDecided(null, IntegrationDecideRefusals.Gone);
        try
        {
            using var store = Writer();
            return store.Write(Family, write =>
            {
                if (CandidateIn(write, candidate) is not { Stands: true }) throw new Unwritten(new IntegrationDecided(null, IntegrationDecideRefusals.Gone));
                if (DecideIn(write, candidate) is { } first) throw new Unwritten(new IntegrationDecided(first, IntegrationDecideRefusals.Decided));
                var last = write.Query($"SELECT {StepColumns} FROM integration_steps WHERE candidate = $candidate ORDER BY id DESC LIMIT 1", MapStep, ("$candidate", candidate))
                    .FirstOrDefault();
                if (last is not { Step: IntegrationStepNames.Judge, Outcome: IntegrationStepOutcomes.Held })
                {
                    throw new Unwritten(new IntegrationDecided(null, IntegrationDecideRefusals.NotHeld));
                }

                write.Execute(
                    "INSERT INTO integration_decides (candidate, decide, requester, at) VALUES ($candidate, $decide, $requester, $at)",
                    ("$candidate", candidate), ("$decide", decide), ("$requester", requester), ("$at", Now()));
                return new IntegrationDecided(DecideIn(write, candidate), null);
            });
        }
        catch (Unwritten unwritten)
        {
            return (IntegrationDecided)unwritten.Answer!;
        }
    }

    private static IntegrationDecide? DecideIn(DesktopWrite write, string candidate) =>
        write.Query(
                "SELECT candidate, decide, requester, at FROM integration_decides WHERE candidate = $candidate",
                row => new IntegrationDecide(row.GetString(0), row.GetString(1), row.GetString(2), Moment(row, "at")), ("$candidate", candidate))
            .FirstOrDefault();

    // ——— A step's two writes (§5.6).

    private static void Validate(IntegrationRequest request)
    {
        Required(request.Id, "its id");
        Required(request.Repository, "its repository");
        Required(request.Workspace, "its workspace");
        Required(request.Branch, "its branch");
        Required(request.Tip, "its frozen tip");
        Required(request.Route, "its route");
        Required(request.Line, "its line");
        Required(request.Requester.Kind, "who asked");
        Required(request.Requester.Door, "the door it came through");
        if (request.Policy is not { Request: not null, Commit: not null, Batch: not null, Order: not null, Gates: not null, Unreached: not null, Reached: not null,
                Conflicts: not null, Overlap: not null })
        {
            throw new ArgumentException("a request keeps its rule's `integrate` resolved, every absent key read through the Absent column");
        }
    }

    private void InsertRequest(DesktopWrite write, IntegrationRequest request)
    {
        var policy = request.Policy;
        write.Execute(
            $"""
            INSERT INTO integration_requests ({RequestColumns})
            VALUES ($id, $repository, $workspace, $branch, $tip, $route, $line, $requester, $door, $act, $generation, $request, $commit, $batch, $order,
                    $gates, $unreached, $reached, $conflicts, $overlap, $policyLine, $ruleSource, $unit, $session, $quest, $landedAt, $proof, $at, NULL, NULL)
            """,
            ("$id", request.Id), ("$repository", request.Repository), ("$workspace", request.Workspace), ("$branch", request.Branch), ("$tip", request.Tip),
            ("$route", request.Route), ("$line", request.Line), ("$requester", request.Requester.Kind), ("$door", request.Requester.Door),
            ("$act", request.Requester.Act), ("$generation", request.Requester.Generation), ("$request", policy.Request), ("$commit", policy.Commit),
            ("$batch", policy.Batch), ("$order", policy.Order), ("$gates", policy.Gates), ("$unreached", policy.Unreached), ("$reached", policy.Reached),
            ("$conflicts", policy.Conflicts), ("$overlap", policy.Overlap), ("$policyLine", policy.Line), ("$ruleSource", request.RuleSource),
            ("$unit", request.Unit), ("$session", request.Session), ("$quest", request.Quest), ("$landedAt", Moments.Write(request.LandedAt)),
            ("$proof", ProofText(request.Proof)), ("$at", Moments.Write(request.At)));
    }

    private static IntegrationRequest? OpenIn(DesktopWrite write, string repository, string branch) =>
        write.Query(
                $"SELECT {Read(int.MaxValue)} FROM integration_requests WHERE repository = $repository COLLATE NOCASE AND branch = $branch AND closed_at IS NULL",
                MapRequest, ("$repository", repository), ("$branch", branch))
            .FirstOrDefault();

    internal static IntegrationRequest? RequestIn(DesktopWrite write, string id) =>
        write.Query($"SELECT {Read(int.MaxValue)} FROM integration_requests WHERE id = $id", MapRequest, ("$id", id)).FirstOrDefault();

    internal static IntegrationCandidate? CandidateIn(DesktopWrite write, string id) =>
        write.Query($"SELECT {CandidateColumns} FROM integration_candidates WHERE id = $id", MapCandidate, ("$id", id)).FirstOrDefault();

    private static IntegrationCandidate? StandingIn(DesktopWrite write, string repository, string line) =>
        write.Query(
                $"SELECT {CandidateColumns} FROM integration_candidates WHERE repository = $repository COLLATE NOCASE AND line = $line AND ended_at IS NULL",
                MapCandidate, ("$repository", repository), ("$line", line))
            .FirstOrDefault();

    internal static IntegrationMember? MemberIn(DesktopWrite write, string candidate, string request) =>
        write.Query(
                $"SELECT {MemberColumns} FROM integration_members WHERE candidate = $candidate AND request = $request", MapMember,
                ("$candidate", candidate), ("$request", request))
            .FirstOrDefault();

    internal static IntegrationStep? StepIn(DesktopWrite write, long id) =>
        write.Query($"SELECT {StepColumns} FROM integration_steps WHERE id = $id", MapStep, ("$id", id)).FirstOrDefault();

    internal static IntegrationStep? OpenStepIn(DesktopWrite write, string candidate) =>
        write.Query($"SELECT {StepColumns} FROM integration_steps WHERE candidate = $candidate AND ended_at IS NULL", MapStep, ("$candidate", candidate))
            .FirstOrDefault();

    internal static StoreRun? RunIn(DesktopWrite write, string run) =>
        write.Query(
            "SELECT run, kind, pid, started, began FROM store_runs WHERE run = $run",
            row => new StoreRun(row.GetString(0), row.GetString(1), row.GetInt32(2), row.IsDBNull(3) ? null : row.GetInt64(3), row.GetString(4)),
            ("$run", run)).FirstOrDefault();

    // ——— The rows read.

    /// <summary>A read of a store that holds the family (or <paramref name="since"/>, a later step's table), or <paramref name="none"/> where the home has no store or one from before it.</summary>
    private T Reading<T>(Func<DesktopStoreReader, T> read, T none, int since = Since)
    {
        using var reader = DesktopStoreReader.Open(Location);
        return reader is not null && reader.Version >= since ? read(reader) : none;
    }

    /// <summary>The store opened for one write, made and migrated where it must be, under this store's run.</summary>
    internal DesktopStore Writer() => DesktopStore.Open(Location, StoreProcess.Options(_settled) with { Run = Run, Kind = kind ?? StoreProcess.Kind, Clock = _clock });

    internal string Now() => Moments.Write(_clock.GetUtcNow());

    internal static string Required(string? value, string what) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"an integration's write needs {what}") : value;

    internal static string Known(string? value, IReadOnlyList<string> words, string what) =>
        value is not null && words.Contains(value, StringComparer.Ordinal)
            ? value
            : throw new ArgumentException($"`{value}` is no integration {what}: one of {string.Join(", ", words)}");

    /// <summary>A write's answer that writes nothing: thrown out of the store's transaction, so it is rolled back, no revision moved and no wake touched.</summary>
    internal sealed class Unwritten(object? answer) : Exception
    {
        public object? Answer { get; } = answer;
    }
}
