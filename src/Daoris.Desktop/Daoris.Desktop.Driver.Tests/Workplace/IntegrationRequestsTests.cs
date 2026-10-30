using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// INTEGRATE1e part e1 (D215's note; the integration design §4.6): the one door, <see cref="IntegrationRequests"/>, its recorded-landing
/// path and its records-branch path, over a stand-in of the records and the desktop store's request family (part e2,
/// <see cref="IntegrationStore"/>, on a scratch home of each case's own), with git's answers scripted through
/// <see cref="WorkingTree.Answered"/> so no process starts. A recorded landing is admitted only on its record, its exact tip, its gate
/// proof as the record keeps it and no double, each refused in its own sentence; a session's branch and a landing's are refused at the
/// records-branch door; the come-up sweep requests each standing landing once, under <c>request: done</c> alone. The same over real git is
/// <see cref="IntegrationRequestsGitTests"/>.
/// </summary>
public sealed class IntegrationRequestsTests : IDisposable
{
    private readonly string _homes = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "integration-requests", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_homes);
    }

    /// <summary>The request store of a home of its own, as each admission the case does not hand one gets: nothing open in it.</summary>
    private IntegrationStore Fresh() => new(Path.Combine(_homes, Guid.NewGuid().ToString("N")[..8]), () => StoreSettled.Yes);

    private const string Line = "0000000000000000000000000000000000000000";
    private const string Tip = "1111111111111111111111111111111111111111";
    private const string Next = "2222222222222222222222222222222222222222";
    private const string Other = "3333333333333333333333333333333333333333";
    private const string Branch = "feature/q1-fix-the-gap";

    private static readonly DateTimeOffset At = new(2026, 10, 30, 9, 0, 0, TimeSpan.Zero);

    /// <summary>No git ever runs here: every ask is answered by <see cref="Git"/>, and the folder is never made.</summary>
    private static readonly string Root = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "integration-requests-unmade");

    private readonly List<string> _asked = [];
    private readonly Dictionary<string, string> _refs = new(StringComparer.Ordinal) { ["refs/heads/main"] = Line, [$"refs/heads/{Branch}"] = Tip };
    private readonly HashSet<string> _held = new(StringComparer.Ordinal);
    private bool _refUnread;
    private bool _ancestryUnread;

    private static readonly LandingRule BranchRule = new(LandingForm.Branch, "feature/{quest}-{slug}");

    private static DriverConfig Config(LandingIntegrate? integrate, bool autoAccept = false) =>
        DriverConfig.Empty.WithLine("engine", "main")
            .WithLanding("engine", BranchRule with { AutoAccept = autoAccept })
            .WithLandingIntegrate("engine", integrate);

    private static readonly DriverConfig Owner = Config(new LandingIntegrate(Request: LandingIntegrate.Owner));
    private static readonly DriverConfig Done = Config(new LandingIntegrate(Request: LandingIntegrate.Done, Gates: LandingIntegrate.Declared), autoAccept: true);
    private static readonly DriverConfig Steward = Config(new LandingIntegrate(Request: LandingIntegrate.Steward, Gates: LandingIntegrate.Declared));

    /// <summary>A landing the person accepted, its review skipped in the person's words, its unit and the quest it served.</summary>
    private static LandedBranch Landed(string branch = Branch, string tip = Tip, string session = "s1") =>
        new("engine", "aurora", branch, "main", tip, session, "q1", "Fix the gap", At)
        {
            AcceptedBy = AcceptedBy.Person, Units = ["unit-a"], Served = "q1",
            Review = new LandingReview("skipped", null, null) { Words = "a records change" },
            Opinion = new LandingOpinion("none") { Words = "no second opinion was asked" },
        };

    private sealed class Records(IReadOnlyList<LandedBranch> landings, IReadOnlyList<GrownBranch>? sessions = null) : IIntegrationRecords
    {
        public IReadOnlyList<LandedBranch> Landings() => landings;

        public IReadOnlyList<GrownBranch> SessionBranches() => sessions ?? [];
    }

    /// <summary>git as this repository answers it: a ref by its commit, git's own <i>missing</i>, and ancestry by the tips the line holds.</summary>
    private (int, string, string)? Git(IReadOnlyList<string> args)
    {
        lock (_asked) _asked.Add(string.Join(' ', args));
        return args[0] switch
        {
            "rev-parse" when _refUnread && args[^1].Contains(Branch, StringComparison.Ordinal) => (1, "", "warning: ignoring broken ref"),
            "rev-parse" => _refs.TryGetValue(args[^1].Replace("^{commit}", "", StringComparison.Ordinal), out var commit) ? (0, commit + "\n", "") : (1, "", ""),
            "show-ref" => _refUnread ? (1, "", "error: cannot read the ref\n") : (2, "", ""),
            "merge-base" when _ancestryUnread => (128, "", "fatal: not a valid commit\n"),
            "merge-base" => _held.Contains(args[2]) ? (0, "", "") : (1, "", ""),
            _ => (129, "", $"no such git here: {string.Join(' ', args)}"),
        };
    }

    private async Task<IntegrationAdmission> AdmitAsync(
        DriverConfig config, IIntegrationRecords records, IntegrationAsk ask, IntegrationStore? store = null)
    {
        WorkingTree.Answered.Value = Git;
        try
        {
            return await new IntegrationRequests(records, store ?? Fresh(), () => config).AdmitAsync(ask);
        }
        finally
        {
            WorkingTree.Answered.Value = null;
        }
    }

    private static IntegrationAsk Card(string branch = Branch, string route = IntegrationRoutes.Recorded) =>
        new("engine", "aurora", Root, branch, route, IntegrationRequester.Card);

    private static void Refused(IntegrationAdmission admission, string refusal, string words)
    {
        Assert.False(admission.Admitted, admission.Message);
        Assert.Null(admission.Request);
        Assert.Equal(refusal, admission.Refusal);
        Assert.Contains(words, admission.Message, StringComparison.Ordinal);
    }

    /// <summary>A repository whose rule carries no <c>integrate</c> requests nothing, and asks git nothing.</summary>
    [Fact]
    public async Task A_repository_whose_rule_carries_no_integrate_requests_nothing()
    {
        var refused = await AdmitAsync(Config(null), new Records([Landed()]), Card());

        Refused(refused, IntegrationRefusals.NoIntegrate, "`engine`'s landing rule carries no `integrate`, so nothing was requested");
        Assert.Empty(_asked);
    }

    /// <summary>
    /// The owner's card opens under every <c>request</c>; the steward's act only under <c>steward</c>, naming its act and generation; the
    /// come-up sweep only under <c>done</c>, and never for a records branch. Each closed door says what the rule says, and asks git nothing.
    /// </summary>
    [Fact]
    public async Task Each_requester_comes_through_only_the_door_its_rule_opens()
    {
        var records = new Records([Landed()]);

        Refused(await AdmitAsync(Owner, records, Card() with { Requester = IntegrationRequester.StewardAct("act-7", 3) }), IntegrationRefusals.Door,
            "the steward requests `engine`'s work only where its rule says `request: steward`, and it says `request: owner`");
        Refused(await AdmitAsync(Owner, records, Card() with { Requester = IntegrationRequester.Sweep }), IntegrationRefusals.Door,
            "the come-up sweep requests `engine`'s landings only where its rule says `request: done`, and it says `request: owner`");
        Refused(await AdmitAsync(Done, records, Card("notes/records", IntegrationRoutes.Records) with { Requester = IntegrationRequester.Sweep }),
            IntegrationRefusals.Door, "Daoris requests no records branch");
        Refused(await AdmitAsync(Steward, records, Card() with { Requester = new IntegrationRequester(IntegrationRequester.Steward, IntegrationDoors.Act) }),
            IntegrationRefusals.Door, "a steward's request names its act and its appointment's generation");
        Assert.Empty(_asked);

        foreach (var (config, requester) in new[]
                 {
                     (Owner, IntegrationRequester.Card), (Done, IntegrationRequester.Card), (Steward, IntegrationRequester.Card),
                     (Steward, IntegrationRequester.StewardAct("act-7", 3)), (Done, IntegrationRequester.Sweep),
                 })
        {
            var admitted = await AdmitAsync(config, records, Card() with { Requester = requester });
            Assert.True(admitted.Admitted, admitted.Message);
            Assert.Equal(requester, admitted.Request!.Requester);
        }
    }

    /// <summary>
    /// The recorded landing is admitted bound to what its record holds: its exact tip, its unit, its session and quest, the rule's policy as
    /// it stands, and its gate proof as the record keeps it, a skipped review and no second opinion among it, never judged again.
    /// </summary>
    [Fact]
    public async Task A_recorded_landing_is_requested_bound_to_its_record_and_its_gate_proof_read_as_recorded()
    {
        var entry = Landed();
        var store = Fresh();

        var admitted = await AdmitAsync(Owner, new Records([entry]), Card() with { Unit = "unit-a", Tip = Tip }, store);

        Assert.True(admitted.Admitted, admitted.Message);
        Assert.Null(admitted.Refusal);
        Assert.Equal($"`{Branch}` at 11111111 is requested for `main` from its landing's record, by the person's card.", admitted.Message);
        var request = admitted.Request!;
        Assert.Equal(("engine", "aurora", Branch, Tip, IntegrationRoutes.Recorded, "main"),
            (request.Repository, request.Workspace, request.Branch, request.Tip, request.Route, request.Line));
        Assert.Equal(("unit-a", "s1", "q1", (DateTimeOffset?)At), (request.Unit, request.Session, request.Quest, request.LandedAt));
        Assert.Equal(new LandingIntegrate(Request: LandingIntegrate.Owner).Resolved(), request.Policy);
        Assert.Equal(LandingSource.Repository, request.RuleSource);
        Assert.Equal(new IntegrationProof(AcceptedBy.Person) { Review = entry.Review, Opinion = entry.Opinion }, request.Proof);
        // Read back from the store's file, so the opinion's counts are maps of their own: equivalent, field by field, to what the door made.
        Assert.Equivalent(request, Assert.Single(store.Open()), strict: true);
    }

    /// <summary>An advanced landing's tip is its newest advance's, and the proof read is that advance's own, with its own acceptance.</summary>
    [Fact]
    public async Task An_advanced_landing_is_bound_to_its_newest_advance_and_that_advances_proof()
    {
        _refs[$"refs/heads/{Branch}"] = Next;
        var advance = new LandedAdvance(Tip, Next, At.AddHours(1), "s2")
        {
            AcceptedBy = AcceptedBy.Auto, Quest = "q2", Unit = "unit-b", Review = new LandingReview("reviewed", "staging", "q2"),
        };
        var entry = Landed() with { Tip = Next, Advances = [advance], Units = ["unit-a", "unit-b"] };

        var admitted = await AdmitAsync(Done, new Records([entry]), Card());

        Assert.True(admitted.Admitted, admitted.Message);
        var request = admitted.Request!;
        Assert.Equal((Next, "unit-b", "s2", "q2"), (request.Tip, request.Unit, request.Session, request.Quest));
        Assert.Equal(new IntegrationProof(AcceptedBy.Auto) { Review = advance.Review }, request.Proof);
    }

    /// <summary>
    /// Its record: no landing recorded of that name, a trace of one, and a unit the landing does not carry are each refused in their own
    /// words before git is asked.
    /// </summary>
    [Fact]
    public async Task A_branch_with_no_standing_record_or_not_that_units_is_refused_before_git_is_asked()
    {
        Refused(await AdmitAsync(Owner, new Records([]), Card("notes/records")), IntegrationRefusals.NoRecord,
            "`notes/records` is no landing this machine recorded in `engine`, so it is not requested as one");
        Refused(await AdmitAsync(Owner, new Records([Landed() with { GoneAt = At.AddDays(1) }]), Card()), IntegrationRefusals.NoRecord,
            $"`{Branch}`'s landing on this machine is a trace");
        Refused(await AdmitAsync(Owner, new Records([Landed()]), Card() with { Unit = "unit-z" }), IntegrationRefusals.NotUnit,
            $"`{Branch}`'s landing carries no unit `unit-z`");
        Assert.Empty(_asked);
    }

    /// <summary>
    /// Its gate proof: a record that does not say who accepted the tip it holds (one recorded before that was kept) has no proof to read,
    /// so it is refused; a review and an opinion it never asked are no part of the judgement.
    /// </summary>
    [Fact]
    public async Task A_record_that_keeps_no_acceptance_of_its_tip_holds_no_gate_proof_and_is_refused()
    {
        Refused(await AdmitAsync(Owner, new Records([Landed() with { AcceptedBy = null }]), Card()), IntegrationRefusals.NoProof,
            $"`{Branch}`'s record does not say who accepted it at 11111111");
        var unkept = Landed() with { Tip = Next, Advances = [new LandedAdvance(Tip, Next, At, "s2")] };
        Refused(await AdmitAsync(Owner, new Records([unkept]), Card()), IntegrationRefusals.NoProof,
            $"`{Branch}`'s record does not say who accepted it at 22222222");
        Assert.Empty(_asked);

        var bare = await AdmitAsync(Owner, new Records([Landed() with { Review = null, Opinion = null }]), Card());
        Assert.True(bare.Admitted, bare.Message);
        Assert.Equal(new IntegrationProof(AcceptedBy.Person), bare.Request!.Proof);
    }

    /// <summary>
    /// Its exact tip: a branch that moved since its landing is refused as moved, a revision being its unit's next landing; a tip the caller
    /// froze that the record does not hold is refused too; git's own <i>missing</i> is gone, and any other failed read unread.
    /// </summary>
    [Fact]
    public async Task A_recorded_landing_not_at_its_records_exact_tip_is_refused()
    {
        _refs[$"refs/heads/{Branch}"] = Other;
        Refused(await AdmitAsync(Owner, new Records([Landed()]), Card()), IntegrationRefusals.Moved,
            $"`{Branch}` moved since its landing: it stands at 33333333, and its record holds 11111111. Nothing was requested: a commit "
            + "made after its landing is its unit's own next landing");

        _refs[$"refs/heads/{Branch}"] = Tip;
        Refused(await AdmitAsync(Owner, new Records([Landed()]), Card() with { Tip = Next }), IntegrationRefusals.Moved,
            $"the landing record holds `{Branch}` at 11111111, not 22222222");

        _refs.Remove($"refs/heads/{Branch}");
        Refused(await AdmitAsync(Owner, new Records([Landed()]), Card()), IntegrationRefusals.Gone,
            $"`{Branch}` is no branch in `engine` now, though its landing's record stands");

        _refUnread = true;
        Refused(await AdmitAsync(Owner, new Records([Landed()]), Card()), IntegrationRefusals.Unread,
            $"git could not read `{Branch}` in `engine` (error: cannot read the ref), so nothing was requested");
    }

    /// <summary>
    /// No double: a request open for the branch, at its tip or an earlier one, refuses before git is asked; the line holding the tip
    /// refuses, and a line git could not judge is unread, never admitted.
    /// </summary>
    [Fact]
    public async Task A_landing_already_requested_or_already_on_the_line_is_never_requested_twice()
    {
        var store = Fresh();
        Assert.True((await AdmitAsync(Owner, new Records([Landed()]), Card(), store)).Admitted);
        _asked.Clear();

        Refused(await AdmitAsync(Owner, new Records([Landed()]), Card(), store), IntegrationRefusals.Open,
            $"a request is already open for `{Branch}` at 11111111, made by the person's card: nothing more was requested.");
        Assert.Empty(_asked);
        _refs[$"refs/heads/{Branch}"] = Next;
        var advanced = Landed() with { Tip = Next, Advances = [new LandedAdvance(Tip, Next, At, "s2") { AcceptedBy = AcceptedBy.Person }] };
        Refused(await AdmitAsync(Owner, new Records([advanced]), Card(), store), IntegrationRefusals.Open,
            "This revision waits for its outcome.");
        Assert.Single(store.Open());

        _refs[$"refs/heads/{Branch}"] = Tip;
        _held.Add(Tip);
        Refused(await AdmitAsync(Owner, new Records([Landed()]), Card()), IntegrationRefusals.Held,
            $"the line `main` already holds `{Branch}` at 11111111: nothing was requested.");

        _ancestryUnread = true;
        Refused(await AdmitAsync(Owner, new Records([Landed()]), Card()), IntegrationRefusals.Unread,
            $"git could not say whether the line `main` holds `{Branch}` (fatal: not a valid commit)");

        _ancestryUnread = false;
        _refs.Remove("refs/heads/main");
        Refused(await AdmitAsync(Owner, new Records([Landed()]), Card()), IntegrationRefusals.NoLine,
            "the line `main` has no branch in `engine`'s checkout");
    }

    /// <summary>
    /// The records-branch door: a session's branch, by its name or its record, a landing's branch standing or a trace, and the line itself
    /// are each refused; a records branch is requested at the commit it stands at, with no workflow gate, and never at another.
    /// </summary>
    [Fact]
    public async Task The_records_branch_door_refuses_a_sessions_branch_and_a_landings_and_requests_a_records_branch_with_no_gate()
    {
        var grown = new GrownBranch("engine", "aurora", "chore/someone-else", "main", Line, null, At);
        var records = new Records([Landed(), Landed("feature/q0-gone", Other, "s0") with { GoneAt = At }], [grown]);

        Refused(await AdmitAsync(Owner, records, Card("daoris/s1a2b3c4", IntegrationRoutes.Records)), IntegrationRefusals.SessionBranch,
            "`daoris/s1a2b3c4` is a session's branch: a session's work is requested only through its own landing or its recorded landing");
        Refused(await AdmitAsync(Owner, records, Card("chore/someone-else", IntegrationRoutes.Records)), IntegrationRefusals.SessionBranch,
            "`chore/someone-else` is a session's branch");
        Refused(await AdmitAsync(Owner, records, Card(Branch, IntegrationRoutes.Records)), IntegrationRefusals.LandingBranch,
            $"`{Branch}` is a landing this machine recorded: it is requested as that landing, from its record, never as a records branch");
        Refused(await AdmitAsync(Owner, records, Card("feature/q0-gone", IntegrationRoutes.Records)), IntegrationRefusals.LandingBranch,
            "`feature/q0-gone` was a landing this machine recorded, and its record no longer holds it");
        Refused(await AdmitAsync(Owner, records, Card("main", IntegrationRoutes.Records)), IntegrationRefusals.IsLine,
            "`main` is the line itself");
        Assert.Empty(_asked);

        _refs["refs/heads/notes/records"] = Other;
        Refused(await AdmitAsync(Owner, records, Card("notes/records", IntegrationRoutes.Records) with { Tip = Next }), IntegrationRefusals.Moved,
            "`notes/records` stands at 33333333, not 22222222");
        var admitted = await AdmitAsync(Steward, records,
            Card("notes/records", IntegrationRoutes.Records) with { Requester = IntegrationRequester.StewardAct("act-7", 3) });
        Assert.True(admitted.Admitted, admitted.Message);
        Assert.Equal("`notes/records` at 33333333 is requested for `main` as a records branch, by the steward's act `act-7`: it carries no workflow gate.",
            admitted.Message);
        Assert.True(admitted.Request is { Tip: Other, Route: IntegrationRoutes.Records, Proof: null, Unit: null, Session: null, LandedAt: null });
    }

    /// <summary>
    /// The come-up sweep, under <c>request: done</c>: every standing landing of the repository is requested once, a trace and another
    /// repository's never, and a second sweep requests nothing more; under <c>owner</c> it requests nothing and asks git nothing.
    /// </summary>
    [Fact]
    public async Task The_sweep_requests_each_standing_landing_once_under_done_alone()
    {
        _refs["refs/heads/feature/q2-more"] = Next;
        var records = new Records(
        [
            Landed(), Landed("feature/q2-more", Next, "s2"), Landed("feature/q0-gone", Other, "s0") with { GoneAt = At },
            Landed("feature/q3-elsewhere", Other, "s3") with { Repository = "portal" },
        ]);
        var store = Fresh();

        async Task<IReadOnlyList<IntegrationAdmission>> SweepAsync(DriverConfig config)
        {
            WorkingTree.Answered.Value = Git;
            try
            {
                return await new IntegrationRequests(records, store, () => config).SweepAsync("engine", "aurora", Root);
            }
            finally
            {
                WorkingTree.Answered.Value = null;
            }
        }

        Assert.Empty(await SweepAsync(Owner));
        Assert.Empty(_asked);

        var swept = await SweepAsync(Done);
        Assert.Equal([Branch, "feature/q2-more"], swept.Select(each => each.Request!.Branch));
        Assert.All(swept, each => Assert.Equal(IntegrationRequester.Sweep, each.Request!.Requester));
        Assert.Equal(2, store.Open().Count);

        var again = await SweepAsync(Done);
        Assert.All(again, each => Assert.Equal(IntegrationRefusals.Open, each.Refusal));
        Assert.Equal(2, store.Open().Count);
    }

    /// <summary>
    /// The store keeps one open request per repository and branch, the repository named in any case, and lets a closed one go, keeping its
    /// row with when and why it closed.
    /// </summary>
    [Fact]
    public void The_store_keeps_one_open_request_per_branch_and_a_closed_one_makes_room()
    {
        var store = Fresh();
        var first = new IntegrationRequest("r1", "engine", "aurora", Branch, Tip, IntegrationRoutes.Recorded, "main", IntegrationRequester.Card,
            new LandingIntegrate().Resolved(), At);

        Assert.Null(store.Add(first));
        Assert.Equal(first, store.Add(first with { Id = "r2", Repository = "ENGINE", Tip = Next }));
        Assert.Null(store.Add(first with { Id = "r3", Branch = "feature/q2-more" }));
        Assert.Equal(first, store.OpenFor("Engine", Branch));

        Assert.True(store.Close("r1", IntegrationRequestCloses.Dropped));
        Assert.False(store.Close("r1", IntegrationRequestCloses.Dropped));
        Assert.Null(store.OpenFor("engine", Branch));
        Assert.Equal(IntegrationRequestCloses.Dropped, store.Request("r1")!.ClosedWhy);
        Assert.Null(store.Add(first with { Id = "r4", Tip = Next }));
        Assert.Equal(["r3", "r4"], store.Open().Select(each => each.Id).Order(StringComparer.Ordinal));
    }

    // ——— The unit's own landing: the `integrate` node's route (INTEGRATE1n part n2; the design §17.4, F40, C30).

    private static IntegrationAsk Node(string unit = "unit-a", string? tip = Tip) =>
        new("engine", "aurora", Root, Branch, IntegrationRoutes.Node, IntegrationRequester.NodeStart) { Unit = unit, Tip = tip };

    /// <summary>A landing record as the acceptance wrote it: who accepted, and the provenance of <c>integrate</c> there; none where null.</summary>
    private static LandedBranch Written(string by, string? request, string source = LandedIntegrate.Rule, string tip = Tip) =>
        Landed(tip: tip) with { AcceptedBy = by, Integrate = request is null ? null : new LandedIntegrate(request, source) };

    /// <summary>
    /// The start reads the landing's acceptance provenance: a person's Accept where <c>integrate</c> stood under <c>owner</c> or <c>done</c> is
    /// the request, and an automatic landing under <c>done</c> is (the done is the request); the request is the record's, kept by key.
    /// </summary>
    [Fact]
    public async Task A_persons_accept_or_an_automatic_done_under_integrate_is_the_request_and_is_found_by_its_key()
    {
        var store = Fresh();
        var admitted = await AdmitAsync(Owner, new Records([Written(AcceptedBy.Person, LandingIntegrate.Owner)]), Node(), store);

        Assert.True(admitted.Admitted, admitted.Message);
        Assert.Equal($"`{Branch}` at 11111111 is requested for `main` from its landing's record, by the unit's `integrate` step.", admitted.Message);
        var request = admitted.Request!;
        Assert.Equal((IntegrationRoutes.Node, IntegrationRequester.NodeStart, "unit-a", Tip), (request.Route, request.Requester, request.Unit, request.Tip));
        Assert.Equal(new LandingIntegrate(Request: LandingIntegrate.Owner).Resolved(), request.Policy);
        Assert.Equal(request.Id, store.FindByKey("unit-a", Tip)!.Id);

        foreach (var (config, by, asked) in new[]
                 {
                     (Done, AcceptedBy.Person, LandingIntegrate.Done), (Done, AcceptedBy.Auto, LandingIntegrate.Done),
                 })
        {
            var each = await AdmitAsync(config, new Records([Written(by, asked)]), Node());
            Assert.True(each.Admitted, each.Message);
            Assert.Equal(asked, each.Request!.Policy.Request);
        }
    }

    /// <summary>
    /// An automatic landing under <c>owner</c> is never the request: the composition <c>accept: automatic</c>, <c>request: owner</c>,
    /// <c>gates: none</c> makes the branch and waits for the owner's card. Under <c>steward</c> no landing is the request either.
    /// </summary>
    [Fact]
    public async Task An_automatic_landing_under_owner_waits_for_the_card_and_steward_waits_for_its_act()
    {
        var ownerAuto = Config(new LandingIntegrate(Request: LandingIntegrate.Owner, Gates: LandingIntegrate.NoGates), autoAccept: true);
        Refused(await AdmitAsync(ownerAuto, new Records([Written(AcceptedBy.Auto, LandingIntegrate.Owner)]), Node()), IntegrationRefusals.AwaitsOwner,
            $"`{Branch}` was landed automatically under `request: owner`, so its landing is no request: it waits for the owner's card");

        foreach (var by in new[] { AcceptedBy.Person, AcceptedBy.Auto })
        {
            Refused(await AdmitAsync(Steward, new Records([Written(by, LandingIntegrate.Steward)]), Node()), IntegrationRefusals.AwaitsSteward,
                "under `request: steward` no landing is the request: it waits for the steward's `request` act, or the owner's card");
        }

        Assert.Empty(_asked);
    }

    /// <summary>
    /// A record that says neither (a landing made before the rule carried <c>integrate</c>, before the field, or by a build that rewrote the
    /// file without it) is no request: a rule gaining <c>integrate</c> over standing landings requests none of them.
    /// </summary>
    [Fact]
    public async Task A_rule_gaining_integrate_over_standing_landings_requests_none_of_them()
    {
        var store = Fresh();

        Refused(await AdmitAsync(Done, new Records([Written(AcceptedBy.Person, null)]), Node(), store), IntegrationRefusals.NoRequest,
            "landing record does not say it was accepted where `integrate` stood");
        Refused(await AdmitAsync(Done, new Records([Landed() with { AcceptedBy = null, Integrate = new LandedIntegrate(LandingIntegrate.Done, LandedIntegrate.Rule) }]), Node(), store),
            IntegrationRefusals.NoRequest, "landing record does not say it was accepted where `integrate` stood");
        Refused(await AdmitAsync(Done, new Records([Written(AcceptedBy.Person, "whenever")]), Node(), store), IntegrationRefusals.NoRequest,
            "landing record does not say it was accepted where `integrate` stood");
        Assert.Empty(store.Open());
        Assert.Empty(_asked);
    }

    /// <summary>An advance is judged by its own provenance and acceptance, not the landing's first: its tip is the one the node froze.</summary>
    [Fact]
    public async Task An_advance_is_judged_by_its_own_provenance()
    {
        _refs[$"refs/heads/{Branch}"] = Next;
        var first = Written(AcceptedBy.Person, LandingIntegrate.Owner);
        var advance = new LandedAdvance(Tip, Next, At.AddHours(1), "s2") { AcceptedBy = AcceptedBy.Auto, Unit = "unit-b", Integrate = new LandedIntegrate(LandingIntegrate.Owner, LandedIntegrate.Rule) };
        var moved = first with { Tip = Next, Advances = [advance], Units = ["unit-a", "unit-b"] };

        Refused(await AdmitAsync(Owner, new Records([moved]), Node("unit-b", Next)), IntegrationRefusals.AwaitsOwner, "landed automatically under `request: owner`");

        var accepted = moved with { Advances = [advance with { AcceptedBy = AcceptedBy.Person }] };
        var admitted = await AdmitAsync(Owner, new Records([accepted]), Node("unit-b", Next));
        Assert.True(admitted.Admitted, admitted.Message);
        Assert.Equal((Next, "unit-b"), (admitted.Request!.Tip, admitted.Request.Unit));

        // The tip the unit froze that the record no longer holds is the unit's next landing's, never this one.
        Refused(await AdmitAsync(Owner, new Records([accepted]), Node("unit-b", Tip)), IntegrationRefusals.Moved, "not 11111111");
    }

    /// <summary>
    /// A bound version's own <c>integrate</c> step (<c>source: version</c>) needs no rule carrying one, and asks with its node's <c>request</c>;
    /// a rule's provenance whose rule has dropped <c>integrate</c> since is refused as the door refuses a rule with none.
    /// </summary>
    [Fact]
    public async Task A_versions_own_step_asks_with_its_nodes_request_and_a_dropped_rule_refuses()
    {
        var versioned = new Records([Written(AcceptedBy.Auto, LandingIntegrate.Done, LandedIntegrate.Version)]);
        var admitted = await AdmitAsync(Config(null), versioned, Node() with { Request = LandingIntegrate.Done, Conflicts = LandingIntegrate.Hold });
        Assert.True(admitted.Admitted, admitted.Message);
        Assert.Equal((LandingIntegrate.Done, LandingIntegrate.Hold), (admitted.Request!.Policy.Request, admitted.Request.Policy.Conflicts));

        Refused(await AdmitAsync(Config(null), new Records([Written(AcceptedBy.Person, LandingIntegrate.Owner)]), Node()), IntegrationRefusals.NoIntegrate,
            "`engine`'s landing rule carries no `integrate`");
    }

    /// <summary>The node's route is Daoris's own, and only for a unit and the tip it froze; no other requester or route uses its door.</summary>
    [Fact]
    public async Task The_nodes_door_opens_only_to_the_node_for_a_unit_and_its_tip()
    {
        var records = new Records([Written(AcceptedBy.Person, LandingIntegrate.Owner)]);

        Refused(await AdmitAsync(Owner, records, Node() with { Requester = IntegrationRequester.Card }), IntegrationRefusals.Door,
            "the `integrate` node's route is Daoris's own");
        Refused(await AdmitAsync(Owner, records, Card() with { Requester = IntegrationRequester.NodeStart }), IntegrationRefusals.Door,
            "the `integrate` node asks only through its own route");
        Refused(await AdmitAsync(Owner, records, Node() with { Unit = null }), IntegrationRefusals.Door, "a node's ask names its unit and the tip it froze");
        Refused(await AdmitAsync(Owner, records, Node(tip: null)), IntegrationRefusals.Door, "a node's ask names its unit and the tip it froze");
        Assert.Empty(_asked);
    }
}
