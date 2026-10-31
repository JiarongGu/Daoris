using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

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

    /// <summary>SQLite's refusal of a row a unique index already holds.</summary>
    private const int UniqueRefused = 2067;

    private const string RequestColumns =
        "id, repository, workspace, branch, tip, route, line, requester, door, act, generation, policy_request, policy_commit, policy_batch, "
        + "policy_order, policy_gates, policy_unreached, policy_reached, policy_conflicts, policy_overlap, policy_line, rule_source, unit, session, "
        + "quest, landed_at, proof, at, closed_at, closed_why";

    private const string CandidateColumns =
        "id, repository, workspace, line, base, job, claim_run, claim_generation, formed_at, tip, promoted_at, ended_at, ended_why";

    private const string MemberColumns = "candidate, request, position, state, joined_at, replayed, contained_by, said, ended_at";

    private const string StepColumns = "id, candidate, step, attempt, claim_run, claim_generation, began_at, request, line_commit, ended_at, outcome, said";

    private readonly Func<StoreSettled> _settled = settled ?? StartConfirmation.Read;

    private readonly Func<StoreRun, bool> _alive = alive ?? Lives;

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
                $"SELECT {RequestColumns} FROM integration_requests WHERE repository = $repository COLLATE NOCASE AND branch = $branch AND closed_at IS NULL",
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

    /// <inheritdoc/>
    public IntegrationRequest? FindByKey(string unit, string tip) =>
        Reading(reader => reader.Query(
                $"SELECT {RequestColumns} FROM integration_requests WHERE unit = $unit AND tip = $tip COLLATE NOCASE ORDER BY at DESC, id DESC",
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
        Reading(reader => reader.Query($"SELECT {RequestColumns} FROM integration_requests WHERE id = $id", MapRequest, ("$id", id)).FirstOrDefault(), null);

    /// <summary>Every request of a repository's branch, open or closed, in the order admitted.</summary>
    public IReadOnlyList<IntegrationRequest> Of(string repository, string branch) =>
        Reading(reader => reader.Query(
            $"SELECT {RequestColumns} FROM integration_requests WHERE repository = $repository COLLATE NOCASE AND branch = $branch ORDER BY at, id",
            MapRequest, ("$repository", repository), ("$branch", branch)), []);

    /// <summary>Every open request, in the order admitted.</summary>
    public IReadOnlyList<IntegrationRequest> Open() =>
        Reading(reader => reader.Query($"SELECT {RequestColumns} FROM integration_requests WHERE closed_at IS NULL ORDER BY at, id", MapRequest), []);

    /// <summary>A repository's most recent closed requests, newest first (the Integration view's history).</summary>
    public IReadOnlyList<IntegrationRequest> Closed(string repository, int most) =>
        Reading(reader => reader.Query(
            $"SELECT {RequestColumns} FROM integration_requests WHERE repository = $repository COLLATE NOCASE AND closed_at IS NOT NULL "
            + "ORDER BY closed_at DESC, id DESC LIMIT $most",
            MapRequest, ("$repository", repository), ("$most", most)), []);

    /// <summary>The newest request a unit made, open or closed; null where it made none.</summary>
    public IntegrationRequest? OfUnit(string unit) =>
        Reading(reader => reader.Query(
                $"SELECT {RequestColumns} FROM integration_requests WHERE unit = $unit ORDER BY at DESC, id DESC LIMIT 1", MapRequest, ("$unit", unit))
            .FirstOrDefault(), null);

    /// <summary>
    /// A line's queue (§5.2 step 2's input): its repository's open requests for it that stand in no candidate, in the order admitted. A request
    /// whose member was held for its conflict (<see cref="IntegrationMemberStates.Held"/>, §5.4) is not queued: it waits for the steward's or the
    /// owner's act, and queued again it would be formed, conflict and be held in every candidate for ever.
    /// </summary>
    public IReadOnlyList<IntegrationRequest> Queued(string repository, string line) =>
        Reading(reader => reader.Query(
            $"""
            SELECT {RequestColumns} FROM integration_requests r
            WHERE r.repository = $repository COLLATE NOCASE AND r.line = $line AND r.closed_at IS NULL
              AND NOT EXISTS (SELECT 1 FROM integration_members m WHERE m.request = r.id AND m.ended_at IS NULL)
              AND NOT EXISTS (SELECT 1 FROM integration_members h WHERE h.request = r.id AND h.state = $held)
            ORDER BY r.at, r.id
            """,
            MapRequest, ("$repository", repository), ("$line", line), ("$held", IntegrationMemberStates.Held)), []);

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

                for (var position = 0; position < forming.Requests.Count; position++)
                {
                    var request = forming.Requests[position];
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

    /// <summary>
    /// Take up a candidate whose claim's run is dead by the lease's test (§5.6, as the move record's take), in one guarded update that writes
    /// this store's run and raises the generation: refused, nothing written, where the candidate is gone or ended, where its claim is this
    /// run's own, where the generation moved since <paramref name="generation"/> was read, or where its run lives or cannot be told dead.
    /// </summary>
    public IntegrationTaken Take(string candidate, long generation)
    {
        try
        {
            using var store = Writer();
            return store.Write(Family, write =>
            {
                var found = CandidateIn(write, candidate) ?? throw Refused(null, IntegrationTakeRefusals.Gone);
                if (!found.Stands) throw Refused(found, IntegrationTakeRefusals.Ended);
                if (string.Equals(found.ClaimRun, Run, StringComparison.Ordinal)) throw Refused(found, IntegrationTakeRefusals.Own);
                if (found.ClaimGeneration != generation) throw Refused(found, IntegrationTakeRefusals.Moved);

                // A run the store holds no row of cannot be told dead, so it holds, as a start that cannot be read does.
                if (RunIn(write, found.ClaimRun) is not { } holder || _alive(holder)) throw Refused(found, IntegrationTakeRefusals.Live);
                var taken = write.Execute(
                    """
                    UPDATE integration_candidates SET claim_run = $mine, claim_generation = claim_generation + 1
                    WHERE id = $id AND claim_run = $run AND claim_generation = $generation AND ended_at IS NULL
                    """,
                    ("$mine", Run), ("$id", candidate), ("$run", found.ClaimRun), ("$generation", generation));
                if (taken != 1) throw Refused(found, IntegrationTakeRefusals.Moved);
                var now = CandidateIn(write, candidate)!;
                return new IntegrationTaken(now, new IntegrationClaim(candidate, Run, now.ClaimGeneration), null);
            });
        }
        catch (Unwritten unwritten)
        {
            return (IntegrationTaken)unwritten.Answer!;
        }

        static Unwritten Refused(IntegrationCandidate? found, string why) => new(new IntegrationTaken(found, null, why));
    }

    // ——— A superseded member's pending dispositions (§5.7).

    /// <summary>Every disposition a request has, pending and answered, in the order they were kept; none where the home has no store or one from before them.</summary>
    public IReadOnlyList<IntegrationDisposition> Dispositions(string request) =>
        Reading(reader => reader.Query(
            "SELECT request, commit_id, candidate, reference, by_commit, files, subject, at, answered_at FROM integration_dispositions "
            + "WHERE request = $request ORDER BY at, rowid", MapDisposition, ("$request", request)), [], DispositionsSince);

    /// <summary>
    /// Every disposition no one has answered yet, oldest first: visible on the Integration view and the session's head until the session, the
    /// steward or the owner answers it, even once the review row of its request has gone (§5.7).
    /// </summary>
    public IReadOnlyList<IntegrationDisposition> Pending() =>
        Reading(reader => reader.Query(
            "SELECT request, commit_id, candidate, reference, by_commit, files, subject, at, answered_at FROM integration_dispositions "
            + "WHERE answered_at IS NULL ORDER BY at, rowid", MapDisposition), [], DispositionsSince);

    /// <summary>
    /// A disposition answered (by <i>superseded</i> or <i>rebase it</i>, which later rows make): it leaves the pending list and its row stays. False
    /// where none is pending for that request and commit. The answerer is a person's or a session's act, not the run's, so no claim is checked.
    /// </summary>
    public bool Answer(string request, string commit)
    {
        if (Reading(reader => reader.Version, 0) < DispositionsSince) return false;
        using var store = Writer();
        return store.Write(Family, write => write.Execute(
            "UPDATE integration_dispositions SET answered_at = $at WHERE request = $request AND commit_id = $commit AND answered_at IS NULL",
            ("$at", Now()), ("$request", request), ("$commit", commit))) == 1;
    }

    // ——— A step's two writes (§5.6).

    /// <summary>
    /// A step's start, written before its effect (§5.6), in one transaction under the claim's check: its next attempt, the member it is of and
    /// the line's commit it read. Refused while another step of the candidate is open, which must end first (a resume ends a cut one), and
    /// for an ended candidate.
    /// </summary>
    /// <exception cref="ArgumentException">A step no candidate has.</exception>
    /// <exception cref="IntegrationClaimLostException">The claim no longer holds the candidate.</exception>
    /// <exception cref="IntegrationRefusedException">A step is open, the candidate ended, or the member is not its own.</exception>
    public IntegrationStep Begin(IntegrationClaim claim, string step, string? request = null, string? lineCommit = null)
    {
        Known(step, IntegrationStepNames.All, "step");
        using var store = Writer();
        return store.Write(Family, write =>
        {
            var candidate = Claimed(write, claim);
            if (!candidate.Stands) throw new IntegrationRefusedException(claim.Candidate, $"it ended ({candidate.EndedWhy}) and takes no step");
            if (request is not null && MemberIn(write, claim.Candidate, request) is not { Stands: true })
            {
                throw new IntegrationRefusedException(claim.Candidate, $"request {request} is no standing member of it");
            }

            try
            {
                return InsertStep(write, claim, step, request, lineCommit, Now());
            }
            catch (SqliteException error) when (error.SqliteExtendedErrorCode == UniqueRefused)
            {
                var open = OpenStepIn(write, claim.Candidate);
                throw new IntegrationRefusedException(
                    claim.Candidate,
                    $"its {open?.Step} begun at {Moments.Write(open?.BeganAt)} has no end, and a candidate runs one step at a time: a step a crash cut is the resume's to judge");
            }
        });
    }

    /// <summary>
    /// A step's end, written after its effect (§5.6), in one transaction under the claim's check: its outcome and words, the candidate's tip,
    /// its promotion, its members' changes, the requests it closes and the candidate's end, all or none. An end written again is the first
    /// end's, and changes nothing.
    /// </summary>
    /// <exception cref="ArgumentException">An outcome, a member's state, a close or an end no step writes.</exception>
    /// <exception cref="IntegrationClaimLostException">The claim no longer holds the candidate.</exception>
    /// <exception cref="IntegrationRefusedException">The step is not the candidate's, or a change names what the candidate does not hold: nothing is written.</exception>
    public IntegrationStep End(IntegrationClaim claim, long step, IntegrationStepEnd end)
    {
        Known(end.Outcome, IntegrationStepOutcomes.All, "outcome");
        foreach (var change in end.Members) Known(change.State, IntegrationMemberStates.All, "member's state");
        foreach (var close in end.Closes) Known(close.Why, IntegrationRequestCloses.All, "request's close");
        if (end.Ends is { } ends) Known(ends, IntegrationCandidateEnds.All, "candidate's end");
        try
        {
            using var store = Writer();
            return store.Write(Family, write =>
            {
                Claimed(write, claim);
                var found = StepIn(write, step);
                if (found is null || !string.Equals(found.Candidate, claim.Candidate, StringComparison.Ordinal))
                {
                    throw new IntegrationRefusedException(claim.Candidate, $"it has no step {step}");
                }

                if (!found.Open) throw new Unwritten(found);
                var at = Now();
                Fenced(write, claim,
                    "UPDATE integration_steps SET ended_at = $at, outcome = $outcome, said = $said WHERE id = $step AND ended_at IS NULL",
                    ("$at", at), ("$outcome", end.Outcome), ("$said", end.Said), ("$step", step));
                if (end.Tip is { } tip) Fenced(write, claim, "UPDATE integration_candidates SET tip = $tip WHERE id = $candidate", ("$tip", tip));
                if (end.Promoted)
                {
                    Fenced(write, claim, "UPDATE integration_candidates SET promoted_at = COALESCE(promoted_at, $at) WHERE id = $candidate", ("$at", at));
                }

                foreach (var change in end.Members)
                {
                    if (MemberIn(write, claim.Candidate, change.Request) is not { Stands: true })
                    {
                        throw new IntegrationRefusedException(claim.Candidate, $"request {change.Request} is no standing member of it");
                    }

                    Fenced(write, claim,
                        """
                        UPDATE integration_members
                        SET state = $state, replayed = COALESCE($replayed, replayed), contained_by = COALESCE($containedBy, contained_by),
                            said = COALESCE($said, said), ended_at = CASE WHEN $ends THEN $at ELSE ended_at END
                        WHERE candidate = $candidate AND request = $request AND ended_at IS NULL
                        """,
                        ("$state", change.State), ("$replayed", change.Replayed), ("$containedBy", change.ContainedBy), ("$said", change.Said),
                        ("$ends", change.Ends), ("$at", at), ("$request", change.Request));
                }

                foreach (var close in end.Closes)
                {
                    if (MemberIn(write, claim.Candidate, close.Request) is null || RequestIn(write, close.Request) is not { ClosedAt: null })
                    {
                        throw new IntegrationRefusedException(claim.Candidate, $"request {close.Request} is no open request of its members");
                    }

                    Fenced(write, claim,
                        "UPDATE integration_requests SET closed_at = $at, closed_why = $why WHERE id = $request AND closed_at IS NULL",
                        ("$at", at), ("$why", close.Why), ("$request", close.Request));
                }

                // A superseded commit's disposition is visible from the moment its member settles, in the same transaction (§5.7).
                foreach (var disposition in end.Dispositions)
                {
                    if (MemberIn(write, claim.Candidate, disposition.Request) is null)
                    {
                        throw new IntegrationRefusedException(claim.Candidate, $"request {disposition.Request} is no member of it, so it has no disposition to keep");
                    }

                    write.Execute(
                        """
                        INSERT INTO integration_dispositions (request, commit_id, candidate, reference, by_commit, files, subject, at)
                        VALUES ($request, $commit, $candidate, $reference, $by, $files, $subject, $at)
                        ON CONFLICT (request, commit_id) DO NOTHING
                        """,
                        ("$request", Required(disposition.Request, "its request")), ("$commit", Required(disposition.Commit, "its commit")), ("$candidate", claim.Candidate),
                        ("$reference", Required(disposition.Reference, "its recovery ref")), ("$by", Required(disposition.By, "the commit that superseded it")),
                        ("$files", FilesText(disposition.Files)), ("$subject", disposition.Subject), ("$at", at));
                }

                if (end.Ends is { } why)
                {
                    Fenced(write, claim, "UPDATE integration_candidates SET ended_at = $at, ended_why = $why WHERE id = $candidate AND ended_at IS NULL",
                        ("$at", at), ("$why", why));
                    write.Execute(
                        "UPDATE integration_members SET ended_at = $at WHERE candidate = $candidate AND ended_at IS NULL",
                        ("$at", at), ("$candidate", claim.Candidate));
                }

                return StepIn(write, step)!;
            });
        }
        catch (Unwritten unwritten)
        {
            return (IntegrationStep)unwritten.Answer!;
        }
    }

    // ——— The writes' own parts.

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

    /// <summary>One attempt's start: its attempt is one more than the step's attempts for that member so far.</summary>
    private static IntegrationStep InsertStep(DesktopWrite write, IntegrationClaim claim, string step, string? request, string? lineCommit, string at)
    {
        var attempts = write.Scalar<long>(
            "SELECT COUNT(*) FROM integration_steps WHERE candidate = $candidate AND step = $step AND request IS $request",
            ("$candidate", claim.Candidate), ("$step", step), ("$request", request));
        write.Execute(
            """
            INSERT INTO integration_steps (candidate, step, attempt, claim_run, claim_generation, began_at, request, line_commit)
            VALUES ($candidate, $step, $attempt, $run, $generation, $at, $request, $lineCommit)
            """,
            ("$candidate", claim.Candidate), ("$step", step), ("$attempt", attempts + 1), ("$run", claim.Run), ("$generation", claim.Generation),
            ("$at", at), ("$request", request), ("$lineCommit", lineCommit));
        return StepIn(write, write.Scalar<long>("SELECT last_insert_rowid()"))!;
    }

    /// <summary>The claim's check, inside the write's transaction: the candidate is there and its run and generation are the claim's, which is this store's run.</summary>
    private IntegrationCandidate Claimed(DesktopWrite write, IntegrationClaim claim)
    {
        if (!string.Equals(claim.Run, Run, StringComparison.Ordinal)) throw new IntegrationClaimLostException(claim, $"this store writes under run {Run}");
        var candidate = CandidateIn(write, claim.Candidate) ?? throw new IntegrationClaimLostException(claim, "the store holds no such candidate");
        if (!string.Equals(candidate.ClaimRun, claim.Run, StringComparison.Ordinal) || candidate.ClaimGeneration != claim.Generation)
        {
            throw new IntegrationClaimLostException(claim, $"run {candidate.ClaimRun} holds it at generation {candidate.ClaimGeneration}");
        }

        return candidate;
    }

    /// <summary>An update of the candidate's rows under its claim, named in the update's own condition, which must change exactly one row.</summary>
    private static void Fenced(DesktopWrite write, IntegrationClaim claim, string update, params (string Name, object? Value)[] values)
    {
        var changed = write.Execute(
            update + " AND EXISTS (SELECT 1 FROM integration_candidates WHERE id = $candidate AND claim_run = $claimRun AND claim_generation = $claimGeneration)",
            [.. values, ("$candidate", claim.Candidate), ("$claimRun", claim.Run), ("$claimGeneration", claim.Generation)]);
        if (changed != 1) throw new IntegrationClaimLostException(claim, "its row changed under the write");
    }

    private static IntegrationRequest? OpenIn(DesktopWrite write, string repository, string branch) =>
        write.Query(
                $"SELECT {RequestColumns} FROM integration_requests WHERE repository = $repository COLLATE NOCASE AND branch = $branch AND closed_at IS NULL",
                MapRequest, ("$repository", repository), ("$branch", branch))
            .FirstOrDefault();

    private static IntegrationRequest? RequestIn(DesktopWrite write, string id) =>
        write.Query($"SELECT {RequestColumns} FROM integration_requests WHERE id = $id", MapRequest, ("$id", id)).FirstOrDefault();

    private static IntegrationCandidate? CandidateIn(DesktopWrite write, string id) =>
        write.Query($"SELECT {CandidateColumns} FROM integration_candidates WHERE id = $id", MapCandidate, ("$id", id)).FirstOrDefault();

    private static IntegrationCandidate? StandingIn(DesktopWrite write, string repository, string line) =>
        write.Query(
                $"SELECT {CandidateColumns} FROM integration_candidates WHERE repository = $repository COLLATE NOCASE AND line = $line AND ended_at IS NULL",
                MapCandidate, ("$repository", repository), ("$line", line))
            .FirstOrDefault();

    private static IntegrationMember? MemberIn(DesktopWrite write, string candidate, string request) =>
        write.Query(
                $"SELECT {MemberColumns} FROM integration_members WHERE candidate = $candidate AND request = $request", MapMember,
                ("$candidate", candidate), ("$request", request))
            .FirstOrDefault();

    private static IntegrationStep? StepIn(DesktopWrite write, long id) =>
        write.Query($"SELECT {StepColumns} FROM integration_steps WHERE id = $id", MapStep, ("$id", id)).FirstOrDefault();

    private static IntegrationStep? OpenStepIn(DesktopWrite write, string candidate) =>
        write.Query($"SELECT {StepColumns} FROM integration_steps WHERE candidate = $candidate AND ended_at IS NULL", MapStep, ("$candidate", candidate))
            .FirstOrDefault();

    private static StoreRun? RunIn(DesktopWrite write, string run) =>
        write.Query(
            "SELECT run, kind, pid, started, began FROM store_runs WHERE run = $run",
            row => new StoreRun(row.GetString(0), row.GetString(1), row.GetInt32(2), row.IsDBNull(3) ? null : row.GetInt64(3), row.GetString(4)),
            ("$run", run)).FirstOrDefault();

    // ——— The rows read.

    private static string? Text(SqliteDataReader row, string column)
    {
        var at = row.GetOrdinal(column);
        return row.IsDBNull(at) ? null : row.GetString(at);
    }

    private static long? Number(SqliteDataReader row, string column)
    {
        var at = row.GetOrdinal(column);
        return row.IsDBNull(at) ? null : row.GetInt64(at);
    }

    private static DateTimeOffset Moment(SqliteDataReader row, string column) =>
        Moments.Read(Text(row, column)) ?? throw new DesktopStoreException($"the integration's `{column}` does not read as a moment");

    private static IntegrationRequest MapRequest(SqliteDataReader row)
    {
        var generation = Number(row, "generation");
        var requester = new IntegrationRequester(Text(row, "requester")!, Text(row, "door")!) { Act = Text(row, "act"), Generation = generation };
        var policy = new LandingIntegrate(
            Text(row, "policy_request"), Text(row, "policy_commit"), (int?)Number(row, "policy_batch"), Text(row, "policy_order"), Text(row, "policy_gates"),
            Text(row, "policy_unreached"), Text(row, "policy_reached"), Text(row, "policy_conflicts"), Text(row, "policy_overlap"), Text(row, "policy_line"));
        return new IntegrationRequest(
            Text(row, "id")!, Text(row, "repository")!, Text(row, "workspace")!, Text(row, "branch")!, Text(row, "tip")!, Text(row, "route")!,
            Text(row, "line")!, requester, policy, Moment(row, "at"))
        {
            RuleSource = Text(row, "rule_source"),
            Unit = Text(row, "unit"),
            Session = Text(row, "session"),
            Quest = Text(row, "quest"),
            LandedAt = Moments.Read(Text(row, "landed_at")),
            Proof = ProofOf(Text(row, "proof")),
            ClosedAt = Moments.Read(Text(row, "closed_at")),
            ClosedWhy = Text(row, "closed_why"),
        };
    }

    private static IntegrationCandidate MapCandidate(SqliteDataReader row) =>
        new(Text(row, "id")!, Text(row, "repository")!, Text(row, "workspace")!, Text(row, "line")!, Text(row, "base")!, Text(row, "job")!,
            Text(row, "claim_run")!, Number(row, "claim_generation")!.Value, Moment(row, "formed_at"))
        {
            Tip = Text(row, "tip"),
            PromotedAt = Moments.Read(Text(row, "promoted_at")),
            EndedAt = Moments.Read(Text(row, "ended_at")),
            EndedWhy = Text(row, "ended_why"),
        };

    private static IntegrationMember MapMember(SqliteDataReader row) =>
        new(Text(row, "candidate")!, Text(row, "request")!, (int)Number(row, "position")!.Value, Text(row, "state")!, Moment(row, "joined_at"))
        {
            Replayed = Text(row, "replayed"),
            ContainedBy = Text(row, "contained_by"),
            Said = Text(row, "said"),
            EndedAt = Moments.Read(Text(row, "ended_at")),
        };

    private static IntegrationStep MapStep(SqliteDataReader row) =>
        new(Number(row, "id")!.Value, Text(row, "candidate")!, Text(row, "step")!, (int)Number(row, "attempt")!.Value, Text(row, "claim_run")!,
            Number(row, "claim_generation")!.Value, Moment(row, "began_at"))
        {
            Request = Text(row, "request"),
            LineCommit = Text(row, "line_commit"),
            EndedAt = Moments.Read(Text(row, "ended_at")),
            Outcome = Text(row, "outcome"),
            Said = Text(row, "said"),
        };

    /// <summary>
    /// The gate proof in the landing record's own shape (<c>acceptedBy</c>, <c>review</c>, <c>opinion</c>, <c>workflow</c>), written by that
    /// record's own writers so a request keeps it as its landing kept it; null for a records branch's, which carries none.
    /// </summary>
    private static string? ProofText(IntegrationProof? proof)
    {
        if (proof is null) return null;
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("acceptedBy", proof.AcceptedBy);
            if (proof.Review is { } review) LandedBranches.WriteReview(writer, review);
            if (proof.Opinion is { } opinion) LandedBranches.WriteOpinion(writer, opinion);
            if (proof.Workflow is { } workflow) LandedBranches.WriteWorkflow(writer, workflow);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>The gate proof as <see cref="ProofText"/> wrote it, read by the landing record's own readers; null where none was kept or it does not read.</summary>
    private static IntegrationProof? ProofOf(string? text)
    {
        if (text is null) return null;
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("acceptedBy", out var by) || by.ValueKind != JsonValueKind.String) return null;
            return new IntegrationProof(by.GetString()!)
            {
                Review = LandedBranches.ReviewOf(root),
                Opinion = LandedBranches.OpinionOf(root),
                Workflow = LandedBranches.WorkflowOf(root),
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>A read of a store that holds the family (or <paramref name="since"/>, a later step's table), or <paramref name="none"/> where the home has no store or one from before it.</summary>
    private T Reading<T>(Func<DesktopStoreReader, T> read, T none, int since = Since)
    {
        using var reader = DesktopStoreReader.Open(Location);
        return reader is not null && reader.Version >= since ? read(reader) : none;
    }

    private static IntegrationDisposition MapDisposition(SqliteDataReader row) =>
        new(Text(row, "request")!, Text(row, "commit_id")!, Text(row, "reference")!, Text(row, "by_commit")!, FilesOf(Text(row, "files")), Text(row, "subject")!)
        {
            Candidate = Text(row, "candidate")!,
            At = Moment(row, "at"),
            AnsweredAt = Moments.Read(Text(row, "answered_at")),
        };

    private static string FilesText(IReadOnlyList<string> files) => JsonSerializer.Serialize(files);

    private static IReadOnlyList<string> FilesOf(string? text)
    {
        try
        {
            return text is null ? [] : JsonSerializer.Deserialize<string[]>(text) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>The store opened for one write, made and migrated where it must be, under this store's run.</summary>
    private DesktopStore Writer() => DesktopStore.Open(Location, StoreProcess.Options(_settled) with { Run = Run, Kind = kind ?? StoreProcess.Kind, Clock = _clock });

    private string Now() => Moments.Write(_clock.GetUtcNow());

    private static string Required(string? value, string what) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"an integration's write needs {what}") : value;

    private static string Known(string? value, IReadOnlyList<string> words, string what) =>
        value is not null && words.Contains(value, StringComparer.Ordinal)
            ? value
            : throw new ArgumentException($"`{value}` is no integration {what}: one of {string.Join(", ", words)}");

    /// <summary>A write's answer that writes nothing: thrown out of the store's transaction, so it is rolled back, no revision moved and no wake touched.</summary>
    private sealed class Unwritten(object? answer) : Exception
    {
        public object? Answer { get; } = answer;
    }
}
