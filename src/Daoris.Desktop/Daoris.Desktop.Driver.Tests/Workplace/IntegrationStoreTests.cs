using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// INTEGRATE1e part e2 (D215's note; the integration design §5.2, §5.6): the integration's family in the desktop store, called from no
/// production path. A request kept whole and read back as the door made it, one open per repository and branch under two connections; a
/// candidate formed with its members and its form step begun under the claim of the run that forms it, one standing per line under two
/// connections, a request standing in one candidate at a time; one job per candidate; each step's start and end one write each under the
/// claim, one step open at a time, an end written again the first's; a take only from a dead run, the old claim fenced out; an older build's
/// store read on. Recovery by ancestry over real git is <see cref="IntegrationRunGitTests"/>'s; here only the case that asks git nothing.
/// </summary>
/// <remarks>
/// Every case has its own scratch home under <c>_fixtures/</c> and sets no variable of the process's environment. One process is one run to
/// the lease's test, so "another process" here is a second store under a run of its own, writing through its own connection, and a dead run is
/// the liveness the case hands in. Nothing here starts a process.
/// </remarks>
public sealed class IntegrationStoreTests : IDisposable
{
    private const string Line = "0000000000000000000000000000000000000000";
    private const string Tip = "1111111111111111111111111111111111111111";
    private const string Next = "2222222222222222222222222222222222222222";

    private static readonly DateTimeOffset At = new(2026, 10, 30, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "integration-store", Guid.NewGuid().ToString("N")[..8]);

    public IntegrationStoreTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private string Db => DesktopStore.LocationIn(_home);

    /// <summary>The integration store of one run: a shell, by default reading a run's liveness by the lease's own test.</summary>
    private IntegrationStore Store(string run, Func<StoreRun, bool>? alive = null) => new(_home, () => StoreSettled.Yes, alive, run, StoreProcess.Shell);

    /// <summary>A liveness in which the runs named are dead and every other run lives.</summary>
    private static Func<StoreRun, bool> DeadAre(params string[] runs) => run => !runs.Contains(run.Run);

    private static IntegrationRequest Request(string id, string branch = "feature/q1-fix", string repository = "engine", string line = "main") =>
        new(id, repository, "aurora", branch, Tip, IntegrationRoutes.Recorded, line, IntegrationRequester.Card, new LandingIntegrate().Resolved(), At);

    private static IntegrationForming Forming(params string[] requests) => new("engine", "aurora", "main", Line, requests);

    private long Rows(string table)
    {
        using var reader = DesktopStoreReader.Open(Db)!;
        return reader.Query($"SELECT COUNT(*) FROM {table}", row => row.GetInt64(0))[0];
    }

    private long Revision()
    {
        using var reader = DesktopStoreReader.Open(Db);
        return reader?.Revisions().GetValueOrDefault(IntegrationStore.Family) ?? 0;
    }

    /// <summary>A store with requests r1 to r<paramref name="count"/> open, each of its own branch.</summary>
    private IntegrationStore WithRequests(string run, int count, Func<StoreRun, bool>? alive = null)
    {
        var store = Store(run, alive);
        for (var each = 1; each <= count; each++) Assert.Null(store.Add(Request($"r{each}", $"feature/q{each}-fix")));
        return store;
    }

    // ——— The requests (§4.6's store, row e1's door).

    [Fact]
    public void A_request_is_kept_whole_and_read_back_as_the_door_made_it()
    {
        var store = Store("run-a");
        var request = new IntegrationRequest(
            "request-1", "engine", "aurora", "feature/q1-fix", Tip, IntegrationRoutes.Recorded, "main", IntegrationRequester.StewardAct("act-7", 3),
            new LandingIntegrate(Request: LandingIntegrate.Steward, Gates: LandingIntegrate.Declared, Line: "main").Resolved(), At)
        {
            Unit = "unit-a", Session = "s1", Quest = "q1", LandedAt = At.AddHours(-1), RuleSource = LandingSource.Repository,
            Proof = new IntegrationProof(AcceptedBy.Person)
            {
                Review = new LandingReview(ReviewVerdicts.Reviewed, "staging", "q1") { Commit = Tip, At = At.AddMinutes(-30), Words = "it reads well" },
                Opinion = new LandingOpinion(OpinionGateStates.Settled)
                {
                    Reviewer = "codex", Label = "Codex", Base = Line, Tip = Tip, Passes = 2, Disputes = 1, Unread = 0, At = At.AddMinutes(-20),
                    Weights = new Dictionary<string, int>(StringComparer.Ordinal) { ["must"] = 1, ["note"] = 2 },
                    Answers = new Dictionary<string, int>(StringComparer.Ordinal) { ["fixed"] = 1 },
                },
                Workflow = new LandingWorkflow("careful", 2, "review") { Kind = "named" },
            },
        };

        Assert.Null(store.Add(request));

        Assert.Equivalent(request, store.OpenFor("ENGINE", "feature/q1-fix"), strict: true);
        Assert.Equivalent(request, store.Request("request-1"), strict: true);
        Assert.Equivalent(new[] { request }, store.Open(), strict: true);
        Assert.Equal(1, Revision());

        // A records branch's request carries no unit, session, quest or proof, and the card names no act: each reads back as none.
        var records = Request("request-2", "notes/records") with { Route = IntegrationRoutes.Records, RuleSource = LandingSource.Repository };
        Assert.Null(store.Add(records));
        Assert.Equal(records, store.Request("request-2"));
    }

    [Fact]
    public async Task Two_runs_on_two_connections_requesting_one_branch_at_once_keep_one_open_request_which_the_second_reads()
    {
        var first = Store("run-a");
        var second = Store("run-b");
        // The store made before the race, so each race is the request's alone.
        Assert.Null(first.Add(Request("warm", "feature/warm")));

        for (var each = 0; each < 25; each++)
        {
            var branch = $"feature/q{each}-race";
            using var start = new Barrier(2);
            var answers = await Task.WhenAll(new[] { (first, "a"), (second, "b") }.Select(pair => Task.Run(() =>
            {
                start.SignalAndWait();
                return (Asked: $"r{each}{pair.Item2}", Open: pair.Item1.Add(Request($"r{each}{pair.Item2}", branch)));
            })));

            var kept = Assert.Single(answers, answer => answer.Open is null);
            var refused = Assert.Single(answers, answer => answer.Open is not null);
            Assert.Equal(kept.Asked, refused.Open!.Id);
            Assert.Equal(kept.Asked, first.OpenFor("engine", branch)!.Id);
        }

        Assert.Equal(26L, Rows("integration_requests"));
    }

    [Fact]
    public void A_request_whose_policy_is_not_resolved_or_that_names_nothing_is_refused_and_no_store_is_made()
    {
        var store = Store("run-a");

        Assert.Throws<ArgumentException>(() => store.Add(Request("r1") with { Policy = new LandingIntegrate() }));
        Assert.Throws<ArgumentException>(() => store.Add(Request(" ")));
        Assert.Throws<ArgumentException>(() => store.Add(Request("r1", branch: "")));
        Assert.Throws<ArgumentException>(() => store.Add(Request("r1") with { Tip = " " }));
        Assert.False(File.Exists(Db));
    }

    [Fact]
    public void A_home_with_no_store_or_one_from_before_the_family_reads_no_request_and_a_read_makes_nothing()
    {
        var store = Store("run-a");

        Assert.Null(store.OpenFor("engine", "feature/q1-fix"));
        Assert.Empty(store.Open());
        Assert.Empty(store.Standing());
        Assert.False(File.Exists(Db));

        using (DesktopStore.Open(Db, new DesktopStoreOptions { Steps = [.. DesktopStore.Steps.Take(5)], Settled = () => StoreSettled.Yes }))
        {
        }

        Assert.Null(store.Request("r1"));
        Assert.Empty(store.Queued("engine", "main"));
        using var reader = DesktopStoreReader.Open(Db)!;
        Assert.Equal(5, reader.Version);
    }

    [Fact]
    public void The_queue_of_a_line_is_its_open_requests_in_no_standing_candidate_in_the_order_they_were_admitted()
    {
        var store = Store("run-a");
        Assert.Null(store.Add(Request("r2", "feature/q2-fix") with { At = At.AddMinutes(2) }));
        Assert.Null(store.Add(Request("r1", "feature/q1-fix") with { At = At.AddMinutes(1) }));
        Assert.Null(store.Add(Request("r3", "feature/q3-fix") with { At = At.AddMinutes(3) }));
        Assert.Null(store.Add(Request("r4", "feature/q4-fix", line: "release") with { At = At }));
        Assert.Null(store.Add(Request("r5", "feature/q5-fix", repository: "portal") with { At = At }));
        Assert.True(store.Close("r3", IntegrationRequestCloses.Dropped));

        Assert.Equal(["r1", "r2"], store.Queued("Engine", "main").Select(each => each.Id));

        store.Form(Forming("r1"));

        Assert.Equal(["r2"], store.Queued("engine", "main").Select(each => each.Id));
    }

    // ——— The candidate, its members and its claim (§5.2 steps 2 and 3, §5.6).

    [Fact]
    public void A_candidate_is_formed_with_its_members_and_its_form_step_begun_under_the_claim_of_the_run_that_forms_it()
    {
        var store = WithRequests("run-a", 2);

        var formed = store.Form(Forming("r2", "r1"));

        Assert.True(formed.Claimed);
        var (candidate, claim, step) = (formed.Candidate, formed.Claim!, formed.Step!);
        Assert.StartsWith("candidate-", candidate.Id, StringComparison.Ordinal);
        Assert.Equal((candidate.Id, "run-a", 1L), (claim.Candidate, claim.Run, claim.Generation));
        Assert.Equal(("engine", "aurora", "main", Line, IntegrationJobs.NameOf(candidate.Id)), (candidate.Repository, candidate.Workspace, candidate.Line, candidate.Base, candidate.Job));
        Assert.True(candidate.Stands);
        Assert.Null(candidate.Tip);
        Assert.Equal((IntegrationStepNames.Form, 1, "run-a", true), (step.Step, step.Attempt, step.ClaimRun, step.Open));
        Assert.Equal(
            [("r2", 0, IntegrationMemberStates.Formed), ("r1", 1, IntegrationMemberStates.Formed)],
            store.Members(candidate.Id).Select(each => (each.Request, each.Position, each.State)));
        Assert.Equal(candidate, store.StandingFor("ENGINE", "main"));
        Assert.Equal(step, store.OpenStep(candidate.Id));
    }

    [Fact]
    public async Task Two_runs_on_two_connections_forming_a_candidate_of_one_line_at_once_make_one_standing_candidate_which_the_second_reads()
    {
        var first = Store("run-a");
        var second = Store("run-b");

        for (var each = 0; each < 25; each++)
        {
            var line = $"line-{each}";
            Assert.Null(first.Add(Request($"a{each}", $"feature/a{each}", line: line)));
            Assert.Null(first.Add(Request($"b{each}", $"feature/b{each}", line: line)));
            using var start = new Barrier(2);
            var answers = await Task.WhenAll(new[] { (first, $"a{each}"), (second, $"b{each}") }.Select(pair => Task.Run(() =>
            {
                start.SignalAndWait();
                return pair.Item1.Form(new IntegrationForming("engine", "aurora", line, Line, [pair.Item2]));
            })));

            var claimed = Assert.Single(answers, answer => answer.Claimed);
            var stands = Assert.Single(answers, answer => !answer.Claimed);
            Assert.Equal(claimed.Candidate.Id, stands.Candidate.Id);
            Assert.Null(stands.Step);
            Assert.Single(first.Members(claimed.Candidate.Id));
        }

        Assert.Equal(25L, Rows("integration_candidates"));
        Assert.Equal(25L, Rows("integration_steps"));
    }

    [Fact]
    public void A_request_is_formed_only_while_open_into_its_own_line_s_candidate_and_stands_in_one_candidate_at_a_time()
    {
        var store = WithRequests("run-a", 2);
        Assert.Null(store.Add(Request("r3", "feature/q3-fix", line: "release")));
        Assert.Null(store.Add(Request("r4", "feature/q4-fix", line: "release")));
        Assert.True(store.Close("r4", IntegrationRequestCloses.Dropped));
        var formed = store.Form(Forming("r1"));
        var revision = Revision();
        static IntegrationForming Release(params string[] requests) => new("engine", "aurora", "release", Line, requests);

        // Another line's request, a closed one, one the store never held, none and one twice: each refused whole, nothing written.
        Assert.Throws<IntegrationRefusedException>(() => store.Form(Release("r3", "r2")));
        Assert.Throws<IntegrationRefusedException>(() => store.Form(Release("r3", "r4")));
        Assert.Throws<IntegrationRefusedException>(() => store.Form(Release("r9")));
        Assert.Throws<ArgumentException>(() => store.Form(Release()));
        Assert.Throws<ArgumentException>(() => store.Form(Release("r3", "r3")));
        Assert.Equal(revision, Revision());
        Assert.Equal(1L, Rows("integration_candidates"));

        // The store itself holds a request to one standing membership: a second row for r1, in a candidate planted beside it, is refused.
        using (var raw = DesktopStore.Open(Db, new DesktopStoreOptions { Settled = () => StoreSettled.Yes }))
        {
            raw.Write("probe", write => write.Execute(
                """
                INSERT INTO integration_candidates (id, repository, workspace, line, base, job, claim_run, claim_generation, formed_at)
                VALUES ('candidate-planted', 'engine', 'aurora', 'planted', $base, 'job-planted', $run, 1, $at)
                """,
                ("$base", Line), ("$run", raw.Run), ("$at", "2026-10-30T09:00:00.0000000+00:00")));
            var refused = Assert.Throws<SqliteException>(() => raw.Write("probe", write => write.Execute(
                "INSERT INTO integration_members (candidate, request, position, state, joined_at) VALUES ('candidate-planted', 'r1', 0, 'formed', $at)",
                ("$at", "2026-10-30T09:00:00.0000000+00:00"))));
            Assert.Equal(2067, refused.SqliteExtendedErrorCode);
        }

        // A request standing in a candidate closes with its candidate's step, never by itself.
        Assert.Throws<IntegrationRefusedException>(() => store.Close("r1", IntegrationRequestCloses.Dropped));

        // The candidate formed again ends, its member with it: the line and the request are free, and the request's next candidate is its own.
        var claim = formed.Claim!;
        store.End(claim, formed.Step!.Id, new IntegrationStepEnd(IntegrationStepOutcomes.Held) { Ends = IntegrationCandidateEnds.FormedAgain });
        Assert.Null(store.StandingFor("engine", "main"));
        Assert.All(store.Members(formed.Candidate.Id), member => Assert.False(member.Stands));
        var again = store.Form(Forming("r1", "r2"));
        Assert.True(again.Claimed);
        Assert.NotEqual(formed.Candidate.Id, again.Candidate.Id);
        Assert.Equal(IntegrationCandidateEnds.FormedAgain, store.Candidate(formed.Candidate.Id)!.EndedWhy);
    }

    /// <summary>
    /// INTEGRATE1e part e3a: a member the line holds is contained and settled in one end (its membership ended, its request closed), and a
    /// candidate whose last member left it ends <c>empty</c> in the same write; the four words a candidate ended with before are read as
    /// they were beside the new one.
    /// </summary>
    [Fact]
    public void A_member_the_line_holds_settles_at_once_and_a_candidate_left_with_nothing_ends_empty_in_the_same_write()
    {
        var store = WithRequests("run-a", 1);
        var formed = store.Form(Forming("r1"));
        var revision = Revision();

        var ended = store.End(formed.Claim!, formed.Step!.Id, new IntegrationStepEnd(IntegrationStepOutcomes.Done)
        {
            Members = [new IntegrationMemberChange("r1", IntegrationMemberStates.Contained) { Said = "the line holds its work already.", Ends = true }],
            Closes = [new IntegrationRequestClose("r1", IntegrationRequestCloses.Settled)],
            Ends = IntegrationCandidateEnds.Empty,
        });

        Assert.Equal(IntegrationStepOutcomes.Done, ended.Outcome);
        Assert.Equal(revision + 1, Revision());
        var member = Assert.Single(store.Members(formed.Candidate.Id));
        Assert.Equal((IntegrationMemberStates.Contained, null), (member.State, member.ContainedBy));
        Assert.False(member.Stands);
        Assert.Equal(IntegrationRequestCloses.Settled, store.Request("r1")!.ClosedWhy);
        Assert.Equal(IntegrationCandidateEnds.Empty, store.Candidate(formed.Candidate.Id)!.EndedWhy);
        Assert.Null(store.StandingFor("engine", "main"));
        Assert.Empty(store.Queued("engine", "main"));
        Assert.Equal(["settled", "formed-again", "dropped", "stopped"], IntegrationCandidateEnds.All.Take(4));
        Assert.Equal("empty", IntegrationCandidateEnds.All[4]);
    }

    /// <summary>
    /// INTEGRATE1e part e3a: a member contained by an earlier member stays in its candidate with its request open, and a candidate that ends
    /// any way but settled frees it, to be queued again beside its container's request, in arrival order (§5.2 step 3's give-back).
    /// </summary>
    [Fact]
    public void A_member_contained_by_an_earlier_member_is_given_back_to_the_queue_when_the_candidate_ends_unsettled()
    {
        var store = WithRequests("run-a", 2);
        var formed = store.Form(Forming("r1", "r2"));
        store.End(formed.Claim!, formed.Step!.Id, new IntegrationStepEnd(IntegrationStepOutcomes.Done)
        {
            Tip = Tip,
            Members =
            [
                new IntegrationMemberChange("r1", IntegrationMemberStates.Replayed) { Replayed = Tip },
                new IntegrationMemberChange("r2", IntegrationMemberStates.Contained) { ContainedBy = "r1", Said = "r1's replay holds its work." },
            ],
        });

        var held = store.Members(formed.Candidate.Id).Single(each => each.Request == "r2");
        Assert.Equal(("r1", true), (held.ContainedBy, held.Stands));
        Assert.Empty(store.Queued("engine", "main"));
        Assert.Null(store.Request("r2")!.ClosedAt);

        var step = store.Begin(formed.Claim!, IntegrationStepNames.Settle);
        store.End(formed.Claim!, step.Id, new IntegrationStepEnd(IntegrationStepOutcomes.Held) { Ends = IntegrationCandidateEnds.FormedAgain });

        Assert.Equal(["r1", "r2"], store.Queued("engine", "main").Select(each => each.Id));
    }

    /// <summary>
    /// INTEGRATE1e part e3b (§5.4, <c>conflicts: hold</c>): a member whose conflict is held stands in its candidate in its own state, its request
    /// open and said, and the candidate goes on without it; the request is no longer queued once it is held, even after the candidate ends, since
    /// it waits for the steward's or the owner's act and a queue that took it up again would form it, conflict and hold it for ever.
    /// </summary>
    [Fact]
    public void A_member_held_for_its_conflict_keeps_its_request_open_and_out_of_the_queue_after_its_candidate_ends()
    {
        var store = WithRequests("run-a", 2);
        var formed = store.Form(Forming("r1", "r2"));
        store.End(formed.Claim!, formed.Step!.Id, new IntegrationStepEnd(IntegrationStepOutcomes.Done)
        {
            Tip = Tip,
            Members =
            [
                new IntegrationMemberChange("r1", IntegrationMemberStates.Held) { Said = "it conflicts in a.txt: held for an act." },
                new IntegrationMemberChange("r2", IntegrationMemberStates.Replayed) { Replayed = Tip },
            ],
        });

        var held = store.Members(formed.Candidate.Id).Single(each => each.Request == "r1");
        Assert.Contains(IntegrationMemberStates.Held, IntegrationMemberStates.All);
        Assert.Equal((IntegrationMemberStates.Held, true, "it conflicts in a.txt: held for an act."), (held.State, held.Stands, held.Said));
        Assert.Null(store.Request("r1")!.ClosedAt);

        var step = store.Begin(formed.Claim!, IntegrationStepNames.Settle);
        store.End(formed.Claim!, step.Id, new IntegrationStepEnd(IntegrationStepOutcomes.Held) { Ends = IntegrationCandidateEnds.FormedAgain });

        // The candidate's end ended the member, and the request still stands open: neither queued again nor closed.
        Assert.False(store.Members(formed.Candidate.Id).Single(each => each.Request == "r1").Stands);
        Assert.Null(store.Request("r1")!.ClosedAt);
        Assert.Equal(["r2"], store.Queued("engine", "main").Select(each => each.Id));
    }

    /// <summary>
    /// INTEGRATE1e part e4a: a step ends <c>split</c> where the judge ran the candidate's members one at a time, a word added beside the six, so
    /// an older row is read as it was; the line's last ended candidate is read back by its end, which is how the next form knows to take one
    /// member.
    /// </summary>
    [Fact]
    public void A_split_ending_is_a_word_beside_the_six_and_the_lines_last_ended_candidate_is_read_back()
    {
        var store = WithRequests("run-a", 2);
        Assert.Null(store.LastEnded("engine", "main"));
        var formed = store.Form(Forming("r1", "r2"));
        Assert.Null(store.LastEnded("engine", "main"));
        store.End(formed.Claim!, formed.Step!.Id, new IntegrationStepEnd(IntegrationStepOutcomes.Done) { Tip = Tip });

        var judge = store.Begin(formed.Claim!, IntegrationStepNames.Judge);
        store.End(formed.Claim!, judge.Id, new IntegrationStepEnd(IntegrationStepOutcomes.Split) { Said = "run one at a time.", Ends = IntegrationCandidateEnds.FormedAgain });

        Assert.Equal(["done", "promoted", "held", "handed-back", "stopped", "interrupted"], IntegrationStepOutcomes.All.Take(6));
        Assert.Equal("split", IntegrationStepOutcomes.All[6]);
        var last = store.LastEnded("engine", "main")!;
        Assert.Equal((formed.Candidate.Id, IntegrationCandidateEnds.FormedAgain), (last.Id, last.EndedWhy));
        Assert.Equal(IntegrationStepOutcomes.Split, store.Steps(last.Id).Last().Outcome);
        Assert.Equal(["r1", "r2"], store.Queued("engine", "main").Select(each => each.Id));

        // The next candidate ends later: it is the last ended, and another line's candidate is none of this line's.
        var next = store.Form(Forming("r1"));
        store.End(next.Claim!, next.Step!.Id, new IntegrationStepEnd(IntegrationStepOutcomes.Stopped) { Ends = IntegrationCandidateEnds.Stopped });
        Assert.Equal(next.Candidate.Id, store.LastEnded("engine", "main")!.Id);
        Assert.Null(store.LastEnded("engine", "release"));
    }

    [Fact]
    public void Each_candidate_has_one_job_of_its_own_named_by_it_and_no_two_candidates_share_one()
    {
        var store = WithRequests("run-a", 2);
        var first = store.Form(Forming("r1"));
        store.End(first.Claim!, first.Step!.Id, new IntegrationStepEnd(IntegrationStepOutcomes.Held) { Ends = IntegrationCandidateEnds.FormedAgain });
        var second = store.Form(Forming("r1", "r2"));

        Assert.Equal(IntegrationJobs.Prefix + first.Candidate.Id, first.Candidate.Job);
        Assert.NotEqual(first.Candidate.Job, second.Candidate.Job);
        Assert.Equal(second.Candidate.Job, store.Candidate(second.Candidate.Id)!.Job);

        // The store refuses a second candidate under a job another holds: 2067 is SQLite's unique refusal.
        using (var raw = DesktopStore.Open(Db, new DesktopStoreOptions { Settled = () => StoreSettled.Yes }))
        {
            var refused = Assert.Throws<SqliteException>(() => raw.Write("probe", write => write.Execute(
                """
                INSERT INTO integration_candidates (id, repository, workspace, line, base, job, claim_run, claim_generation, formed_at)
                VALUES ('candidate-copy', 'engine', 'aurora', 'release', $base, $job, $run, 1, $at)
                """,
                ("$base", Line), ("$job", first.Candidate.Job), ("$run", raw.Run), ("$at", "2026-10-30T09:00:00.0000000+00:00"))));
            Assert.Equal(2067, refused.SqliteExtendedErrorCode);
        }

        // On the machine, the name makes one job: a second asked for under it while the first is held is another's, never this one's.
        if (!OperatingSystem.IsWindows()) return;
        var (job, held) = ProcessJob.CreateNamed(second.Candidate.Job);
        using (job)
        {
            Assert.NotNull(job);
            Assert.False(held);
            var (twice, named) = ProcessJob.CreateNamed(second.Candidate.Job);
            Assert.Null(twice);
            Assert.True(named);
        }
    }

    // ——— A step's two writes under the claim (§5.6).

    [Fact]
    public void A_step_s_start_and_end_are_each_one_write_under_the_claim_and_a_candidate_runs_one_step_at_a_time()
    {
        var store = WithRequests("run-a", 1);
        var formed = store.Form(Forming("r1"));
        var claim = formed.Claim!;
        var revision = Revision();

        // The form step is open: no other begins until it ends.
        Assert.Throws<IntegrationRefusedException>(() => store.Begin(claim, IntegrationStepNames.Replay, "r1"));
        Assert.Equal(revision, Revision());

        var formEnd = store.End(claim, formed.Step!.Id, new IntegrationStepEnd(IntegrationStepOutcomes.Done) { Said = "the tree is ready" });
        Assert.Equal((IntegrationStepOutcomes.Done, "the tree is ready", false), (formEnd.Outcome, formEnd.Said, formEnd.Open));
        Assert.Equal(revision + 1, Revision());

        var replay = store.Begin(claim, IntegrationStepNames.Replay, "r1", lineCommit: Line);
        Assert.Equal((IntegrationStepNames.Replay, 1, "r1", (string?)Line, true), (replay.Step, replay.Attempt, replay.Request, replay.LineCommit, replay.Open));
        Assert.Equal(revision + 2, Revision());
        var ended = store.End(claim, replay.Id, new IntegrationStepEnd(IntegrationStepOutcomes.Done)
        {
            Tip = Tip, Members = [new IntegrationMemberChange("r1", IntegrationMemberStates.Replayed) { Replayed = Tip }],
        });

        // An end written again is the first end's, and changes nothing.
        var revisionAfter = Revision();
        var again = store.End(claim, replay.Id, new IntegrationStepEnd(IntegrationStepOutcomes.Held) { Said = "written again" });
        Assert.Equal(ended, again);
        Assert.Equal(revisionAfter, Revision());
        Assert.Equal(Tip, store.Candidate(claim.Candidate)!.Tip);
        Assert.Equal((IntegrationMemberStates.Replayed, (string?)Tip), store.Members(claim.Candidate).Select(each => (each.State, each.Replayed)).Single());

        // The step begun again from its start is its next attempt.
        var second = store.Begin(claim, IntegrationStepNames.Replay, "r1");
        Assert.Equal(2, second.Attempt);
        Assert.Equal(
            [(IntegrationStepNames.Form, 1), (IntegrationStepNames.Replay, 1), (IntegrationStepNames.Replay, 2)],
            store.Steps(claim.Candidate).Select(each => (each.Step, each.Attempt)));
    }

    [Fact]
    public void An_end_that_does_not_follow_from_the_candidate_writes_nothing_its_step_left_open()
    {
        var store = WithRequests("run-a", 2);
        var formed = store.Form(Forming("r1"));
        var claim = formed.Claim!;
        var revision = Revision();

        // A member the candidate does not hold, and a request it does not hold closed with it: refused whole, the step's end with them.
        Assert.Throws<IntegrationRefusedException>(() => store.End(claim, formed.Step!.Id, new IntegrationStepEnd(IntegrationStepOutcomes.Done)
        {
            Members = [new IntegrationMemberChange("r2", IntegrationMemberStates.Replayed)],
        }));
        Assert.Throws<IntegrationRefusedException>(() => store.End(claim, formed.Step!.Id, new IntegrationStepEnd(IntegrationStepOutcomes.Done)
        {
            Closes = [new IntegrationRequestClose("r2", IntegrationRequestCloses.Settled)],
        }));
        Assert.Throws<ArgumentException>(() => store.End(claim, formed.Step!.Id, new IntegrationStepEnd("finished")));
        Assert.Throws<ArgumentException>(() => store.Begin(claim, "lunch"));

        Assert.Equal(revision, Revision());
        Assert.True(store.OpenStep(claim.Candidate)!.Open);
        Assert.NotNull(store.OpenFor("engine", "feature/q2-fix"));
    }

    [Fact]
    public void A_settling_end_closes_its_requests_ends_its_candidate_and_frees_its_line_in_one_transaction()
    {
        var store = WithRequests("run-a", 2);
        var formed = store.Form(Forming("r1", "r2"));
        var claim = formed.Claim!;
        store.End(claim, formed.Step!.Id, new IntegrationStepEnd(IntegrationStepOutcomes.Done) { Tip = Tip });
        var promote = store.Begin(claim, IntegrationStepNames.Promote, lineCommit: Line);
        store.End(claim, promote.Id, new IntegrationStepEnd(IntegrationStepOutcomes.Promoted) { Promoted = true });
        Assert.NotNull(store.Candidate(claim.Candidate)!.PromotedAt);

        var settle = store.Begin(claim, IntegrationStepNames.Settle);
        store.End(claim, settle.Id, new IntegrationStepEnd(IntegrationStepOutcomes.Done)
        {
            Members =
            [
                new IntegrationMemberChange("r1", IntegrationMemberStates.Settled),
                new IntegrationMemberChange("r2", IntegrationMemberStates.Settled) { ContainedBy = "r1", Said = "its work is r1's" },
            ],
            Closes = [new IntegrationRequestClose("r1", IntegrationRequestCloses.Settled), new IntegrationRequestClose("r2", IntegrationRequestCloses.Settled)],
            Ends = IntegrationCandidateEnds.Settled,
        });

        var candidate = store.Candidate(claim.Candidate)!;
        Assert.False(candidate.Stands);
        Assert.Equal(IntegrationCandidateEnds.Settled, candidate.EndedWhy);
        Assert.Null(store.StandingFor("engine", "main"));
        Assert.Empty(store.Open());
        Assert.Equal([IntegrationRequestCloses.Settled, IntegrationRequestCloses.Settled], new[] { "r1", "r2" }.Select(id => store.Request(id)!.ClosedWhy));
        Assert.Equal(
            [("r1", IntegrationMemberStates.Settled, (string?)null, false), ("r2", IntegrationMemberStates.Settled, (string?)"r1", false)],
            store.Members(claim.Candidate).Select(each => (each.Request, each.State, each.ContainedBy, each.Stands)));

        // An ended candidate takes no step.
        Assert.Throws<IntegrationRefusedException>(() => store.Begin(claim, IntegrationStepNames.Tidy));
    }

    [Fact]
    public void A_member_that_left_its_candidate_or_never_joined_it_takes_no_step()
    {
        var store = WithRequests("run-a", 2);
        var formed = store.Form(Forming("r1", "r2"));
        var claim = formed.Claim!;
        store.End(claim, formed.Step!.Id, new IntegrationStepEnd(IntegrationStepOutcomes.Done)
        {
            Members = [new IntegrationMemberChange("r2", IntegrationMemberStates.HandedBack) { Said = "its change conflicts in meaning", Ends = true }],
            Closes = [new IntegrationRequestClose("r2", IntegrationRequestCloses.HandedBack)],
        });
        var revision = Revision();

        Assert.Throws<IntegrationRefusedException>(() => store.Begin(claim, IntegrationStepNames.Replay, "r2"));
        Assert.Throws<IntegrationRefusedException>(() => store.Begin(claim, IntegrationStepNames.Replay, "r9"));
        Assert.Equal(revision, Revision());

        Assert.Equal(1, store.Begin(claim, IntegrationStepNames.Replay, "r1").Attempt);
        var left = store.Members(claim.Candidate).Single(each => each.Request == "r2");
        Assert.Equal((IntegrationMemberStates.HandedBack, "its change conflicts in meaning", false), (left.State, left.Said, left.Stands));
        Assert.Equal(IntegrationRequestCloses.HandedBack, store.Request("r2")!.ClosedWhy);
    }

    // ——— The take, and the claim's fence (§5.6: the run is the lease holder's).

    [Fact]
    public void A_candidate_is_taken_only_from_a_dead_run_and_then_once_and_the_old_claim_writes_nothing_more()
    {
        var holder = WithRequests("run-a", 1);
        var formed = holder.Form(Forming("r1"));
        var old = formed.Claim!;
        var candidate = formed.Candidate.Id;

        // Refused while its run lives, from its own run, at a generation that is not the one read, and for a candidate the store never held.
        Assert.Equal(IntegrationTakeRefusals.Live, Store("run-b").Take(candidate, 1).Refused);
        Assert.Equal(IntegrationTakeRefusals.Own, Store("run-a", DeadAre("run-a")).Take(candidate, 1).Refused);
        Assert.Equal(IntegrationTakeRefusals.Moved, Store("run-b", DeadAre("run-a")).Take(candidate, 7).Refused);
        Assert.Equal(IntegrationTakeRefusals.Gone, Store("run-b", DeadAre("run-a")).Take("candidate-none", 1).Refused);
        Assert.Equal((old.Run, 1L), (holder.Candidate(candidate)!.ClaimRun, holder.Candidate(candidate)!.ClaimGeneration));

        var taker = Store("run-b", DeadAre("run-a"));
        var taken = taker.Take(candidate, 1);
        Assert.Null(taken.Refused);
        Assert.Equal((candidate, "run-b", 2L), (taken.Claim!.Candidate, taken.Claim.Run, taken.Claim.Generation));
        Assert.Equal(IntegrationTakeRefusals.Moved, Store("run-c", DeadAre("run-a")).Take(candidate, 1).Refused);

        // The run that lost it, come back, writes nothing: its step's end, a step's start and its candidate's end are each refused.
        var revision = Revision();
        Assert.Throws<IntegrationClaimLostException>(() => holder.End(old, formed.Step!.Id, new IntegrationStepEnd(IntegrationStepOutcomes.Done)));
        Assert.Throws<IntegrationClaimLostException>(() => holder.Begin(old, IntegrationStepNames.Replay, "r1"));
        // A store writing under one run never writes another's claim.
        Assert.Throws<IntegrationClaimLostException>(() => holder.End(taken.Claim, formed.Step!.Id, new IntegrationStepEnd(IntegrationStepOutcomes.Done)));
        Assert.Equal(revision, Revision());

        // The taker writes the open step's end under its own claim; an ended candidate is never taken.
        taker.End(taken.Claim, formed.Step!.Id, new IntegrationStepEnd(IntegrationStepOutcomes.Interrupted) { Ends = IntegrationCandidateEnds.Dropped });
        Assert.Equal(IntegrationTakeRefusals.Ended, Store("run-c", DeadAre("run-b")).Take(candidate, 2).Refused);
    }

    // ——— The runner's two writes, and a resume that asks git nothing.

    [Fact]
    public async Task A_step_s_start_is_written_before_its_effect_and_its_end_after_and_an_effect_that_never_returns_leaves_its_start()
    {
        var store = WithRequests("run-a", 1);
        var formed = store.Form(Forming("r1"));
        var claim = formed.Claim!;
        store.End(claim, formed.Step!.Id, new IntegrationStepEnd(IntegrationStepOutcomes.Done));
        var steps = new IntegrationSteps(store);

        IntegrationStep? seen = null;
        var ran = await steps.RunAsync(claim, IntegrationStepNames.Replay, (begun, _) =>
        {
            seen = store.OpenStep(claim.Candidate);
            Assert.Equal(begun, seen);
            return Task.FromResult(new IntegrationStepEnd(IntegrationStepOutcomes.Done) { Tip = Tip });
        }, request: "r1");

        Assert.Equal((IntegrationStepNames.Replay, "r1", IntegrationStepOutcomes.Done, false), (ran.Step, ran.Request, ran.Outcome, ran.Open));
        Assert.Equal(seen!.Id, ran.Id);

        await Assert.ThrowsAsync<IOException>(() => steps.RunAsync(claim, IntegrationStepNames.Gates, (_, _) => throw new IOException("the process was killed")));
        var open = store.OpenStep(claim.Candidate)!;
        Assert.Equal((IntegrationStepNames.Gates, 1, true), (open.Step, open.Attempt, open.Open));
    }

    [Fact]
    public async Task A_resumed_step_of_a_candidate_with_no_tip_runs_again_from_its_start_and_asks_git_nothing()
    {
        var store = WithRequests("run-a", 1);
        var formed = store.Form(Forming("r1"));
        var claim = formed.Claim!;
        store.End(claim, formed.Step!.Id, new IntegrationStepEnd(IntegrationStepOutcomes.Done));
        var begun = store.Begin(claim, IntegrationStepNames.Replay, "r1");
        var asked = new List<string>();
        WorkingTree.Answered.Value = args =>
        {
            lock (asked) asked.Add(string.Join(' ', args));
            return (129, "", "no git here");
        };

        IntegrationResumed resumed;
        try
        {
            resumed = await new IntegrationSteps(store).ResumeAsync(claim, Path.Combine(_home, "unmade"));
        }
        finally
        {
            WorkingTree.Answered.Value = null;
        }

        Assert.Empty(asked);
        Assert.Equal(IntegrationResumes.Again, resumed.Kind);
        Assert.Equal((begun.Id, IntegrationStepOutcomes.Interrupted), (resumed.Step!.Id, resumed.Step.Outcome));
        Assert.Null(store.OpenStep(claim.Candidate));
        Assert.Equal(2, store.Begin(claim, IntegrationStepNames.Replay, "r1").Attempt);
    }

    // ——— An older build (§13.5: a build rolled back before it confirmed still writes the store).

    [Fact]
    public void A_build_of_five_steps_writes_the_store_as_it_did_and_the_newer_build_back_reads_its_integration_as_it_left_it()
    {
        var store = WithRequests("run-a", 1);
        var formed = store.Form(Forming("r1"));

        using (var older = DesktopStore.Open(Db, new DesktopStoreOptions { Steps = [.. DesktopStore.Steps.Take(5)], Settled = () => StoreSettled.Yes }))
        {
            Assert.False(older.Newer);
            older.Write("moves", write => write.Execute(
                """
                INSERT INTO moves (record_left, executor, how, from_why, from_by, phase, claim_run, claim_generation, decided_at)
                VALUES ('s1', 'conversation', 'brief', 'limit', 'daoris', 'decided', $run, 1, '2026-10-30T09:00:00.0000000+00:00')
                """,
                ("$run", older.Run)));
        }

        Assert.Equal(formed.Candidate, store.Candidate(formed.Candidate.Id));
        Assert.Equal(formed.Step, store.OpenStep(formed.Candidate.Id));
        Assert.Equal("r1", Assert.Single(store.Members(formed.Candidate.Id)).Request);
        Assert.NotNull(store.OpenFor("engine", "feature/q1-fix"));
        store.End(formed.Claim!, formed.Step!.Id, new IntegrationStepEnd(IntegrationStepOutcomes.Done) { Tip = Next });
        Assert.Equal(Next, store.Candidate(formed.Candidate.Id)!.Tip);
    }
}
