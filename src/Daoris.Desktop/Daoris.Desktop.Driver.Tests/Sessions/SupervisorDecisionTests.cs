using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// SUPERVISOR1a-i (D208; the supervisor design §3.4, §3.6, §6.1, §6.3, §11 row 1a-i): the supervisor's decisions, live in memory. Each
/// refusal; the idempotent retry; <c>answered</c> and <c>overruled</c> sticking; the release in one conditional step owning its word by
/// its receipt, a lost response recovered under it; an admitted decision's files rewritten, and forged files, never handed nor read; a
/// restart closing every decision; a second park and a restart keeping the history, granting nothing; every move not in §3.6 refused.
/// Inert in production: the admission the door will answer refuses every act until 1a-ii.
/// </summary>
public sealed class SupervisorDecisionTests : IDisposable
{
    private const string Session = "a1b2c3d4";
    private const string Repository = "Reports";
    private static readonly DateTimeOffset Kept = DateTimeOffset.Parse("2026-10-24T09:00:00Z");

    private readonly string _home = Path.Combine(AppContext.BaseDirectory, "supervisor", Guid.NewGuid().ToString("N")[..8]);

    public SupervisorDecisionTests() => Directory.CreateDirectory(DaorisHome.Entry(_home, HomeEntry.Sessions));

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    /// <summary>The injected admission: one generation, which a revoke empties.</summary>
    private sealed class Appointed : IDecisionAdmission
    {
        public string? Generation { get; set; } = "g-1";

        public DecisionAdmitted Admit(string repository) =>
            Generation is { } generation && repository == Repository ? new(generation, null) : DecisionAdmitted.Refused("not-appointed");
    }

    private readonly Appointed _appointed = new();

    private QuestionSupervisorDecisions Decisions(IDecisionAdmission? admission = null) => new(_home, admission ?? _appointed);

    private static ParkQuestion Question(string id = "q-1") => new(
        id, Session, "Which report?", [new("The monthly one", "I push to origin now."), new("The weekly one", "I write it.")], 1, null,
        Kept.AddMinutes(-1), "It is asked for.");

    private static SupervisedPark Park(ParkQuestion? filed = null, bool parked = true, bool quest = true, bool owner = false,
        bool open = false, bool unread = false) => new(Repository, parked, quest, filed ?? Question(), owner, open, unread);

    private static SupervisorAnswer Answer(string choice = "The monthly one", string question = "q-1", string generation = "g-1",
        string source = "D201 point 3") =>
        new(Session, question, generation, choice, [new("The decision names the monthly report.", source)], new("terminal", "pwsh"));

    /// <summary>The service's say as the release calls it: one word per key, refused where a word of the decision waits under another.</summary>
    private sealed class Ledger
    {
        public Dictionary<string, (string Word, string Decision)> Kept { get; } = [];
        public List<string> TakenBack { get; } = [];
        public bool LoseNext { get; set; }

        public Task<SupervisorSaid> Say(string session, string decision, string question, string choice, string key, CancellationToken ct)
        {
            if (Kept.TryGetValue(key, out var again))
            {
                return Task.FromResult(new SupervisorSaid(again.Word, null));
            }

            if (Kept.Values.Any(each => each.Decision == decision)) return Task.FromResult(new SupervisorSaid(null, "decision-taken"));
            var word = "w" + Kept.Count;
            Kept[key] = (word, decision);
            if (LoseNext)
            {
                LoseNext = false;
                return Task.FromResult(new SupervisorSaid(null, null, Lost: true));
            }

            return Task.FromResult(new SupervisorSaid(word, null));
        }

        public Task<bool> TakeBack(string session, string word, CancellationToken ct)
        {
            TakenBack.Add(word);
            return Task.FromResult(true);
        }
    }

    private string Folder => DaorisHome.Entry(_home, HomeEntry.Sessions, Session + QuestionSupervisorDecisions.DecisionsSuffix);

    /// <summary>Production is inert: the admission the door will answer refuses every act, so nothing is kept outside the tests.</summary>
    [Fact]
    public async Task Production_admission_refuses_every_act_and_keeps_nothing()
    {
        var kept = await Decisions(NoDecisionAdmission.Instance).KeepAsync(Answer(), Park(), Kept);

        Assert.Equal((false, SupervisorWhy.AppointmentChanged), (kept.Kept, kept.Why));
        Assert.Equal(SupervisorWhy.NotOffered, NoDecisionAdmission.Instance.Admit(Repository).Why);
        Assert.False(Directory.Exists(Folder));
    }

    /// <summary>Each §3.1 refusal marked 1a-i, in order, keeping nothing.</summary>
    [Fact]
    public async Task Each_refusal_keeps_nothing()
    {
        var decisions = Decisions();
        var cases = new (SupervisorAnswer Answer, SupervisedPark Park, string Why)[]
        {
            (Answer(source: " "), Park(), SupervisorWhy.NoSource),
            (Answer() with { Reasons = [] }, Park(), SupervisorWhy.Form),
            (Answer(), Park(parked: false), SupervisorWhy.NotParked),
            (Answer(), Park(quest: false), SupervisorWhy.NoQuest),
            (Answer(question: "q-0"), Park(), SupervisorWhy.QuestionChanged),
            (Answer(choice: "the monthly one"), Park(), SupervisorWhy.NotOfferedChoice),
            (Answer(), Park(unread: true), SupervisorWhy.GoAheadUnread),
            (Answer(), Park(open: true), SupervisorWhy.GoAheadOpen),
            (Answer(), Park(owner: true), SupervisorWhy.OwnerAnswered),
            (Answer(generation: "g-0"), Park(), SupervisorWhy.AppointmentChanged),
        };

        foreach (var (answer, park, why) in cases)
        {
            var kept = await decisions.KeepAsync(answer, park, Kept);
            Assert.Equal((false, why), (kept.Kept, kept.Why));
        }

        Assert.Empty(decisions.LiveOf(Session));
    }

    /// <summary>
    /// A kept decision is held for five minutes; a retry of the same form while held answers kept with the same decision; another form
    /// on the question is <c>answered</c>; once overruled, the question is the owner's (<c>overruled</c>) until a new one replaces it.
    /// </summary>
    [Fact]
    public async Task A_retry_is_idempotent_and_answered_and_overruled_stick()
    {
        var decisions = Decisions();
        var kept = await decisions.KeepAsync(Answer(), Park(), Kept);
        Assert.Equal((true, Kept.AddMinutes(5)), (kept.Kept, kept.GoesOnAt));
        Assert.Equal(kept.Decision, (await decisions.KeepAsync(Answer(), Park(), Kept.AddMinutes(1))).Decision);
        Assert.Equal(SupervisorWhy.Answered, (await decisions.KeepAsync(Answer(choice: "The weekly one"), Park(), Kept)).Why);

        var ledger = new Ledger();
        Assert.Equal(SupervisorState.Overruled, await decisions.OverruleAsync(Session, kept.Decision!, ledger.TakeBack, Kept.AddMinutes(2)));
        Assert.Empty(ledger.TakenBack);
        Assert.Equal(SupervisorWhy.Overruled, (await decisions.KeepAsync(Answer(), Park(), Kept)).Why);

        var next = await decisions.KeepAsync(Answer(question: "q-2"), Park(Question("q-2")), Kept.AddMinutes(3));
        Assert.True(next.Kept);
    }

    /// <summary>
    /// The release waits for <c>goesOnAt</c> in memory, then keeps one word through the say under its receipt; a lost response leaves it
    /// held and the next look recovers the same word under the same receipt, nothing kept twice; an identical canonical word first under
    /// another key refuses it (<c>decision-taken</c>), stale as forged; a revoke before the release keeps nothing.
    /// </summary>
    [Fact]
    public async Task The_release_owns_its_word_by_its_receipt()
    {
        var decisions = Decisions();
        var ledger = new Ledger { LoseNext = true };
        var kept = await decisions.KeepAsync(Answer(), Park(), Kept);

        Assert.Empty(await decisions.ReleaseAsync(Session, ledger.Say, Kept.AddMinutes(4)));
        Assert.Empty(ledger.Kept);
        Assert.Equal(Kept.AddMinutes(5), decisions.HeldUntil(Session));

        Assert.Empty(await decisions.ReleaseAsync(Session, ledger.Say, Kept.AddMinutes(5)));
        Assert.Equal(SupervisorState.Held, decisions.LiveOf(Session).Single().State);
        Assert.Equal([SupervisorState.Released], await decisions.ReleaseAsync(Session, ledger.Say, Kept.AddMinutes(6)));
        Assert.Single(ledger.Kept);
        Assert.True(decisions.Authored(Session, "w0"));
        Assert.True(decisions.Handable(Session, "w0"));
        Assert.False(decisions.Authored(Session, "w9"));
        Assert.Equal(kept.Decision, decisions.LiveOf(Session).Single().Id);
        Assert.Null(decisions.HeldUntil(Session));

        var forged = new Ledger();
        var other = Decisions();
        var second = await other.KeepAsync(Answer(), Park(), Kept);
        forged.Kept["posted-first"] = ("w-forged", second.Decision!);
        Assert.Equal([SupervisorState.Stale], await other.ReleaseAsync(Session, forged.Say, Kept.AddMinutes(5)));
        Assert.Equal(SupervisorWhy.Forged, other.LiveOf(Session).Single().Why);
        Assert.False(other.Authored(Session, "w-forged"));

        var revoked = Decisions();
        await revoked.KeepAsync(Answer(), Park(), Kept);
        _appointed.Generation = "g-2";
        Assert.Equal([SupervisorState.Stale], await revoked.ReleaseAsync(Session, new Ledger().Say, Kept.AddMinutes(5)));
        Assert.Equal(SupervisorWhy.Revoked, revoked.LiveOf(Session).Single().Why);
    }

    /// <summary>A revoke raced against a keeping: the generation rechecked at the keeping keeps nothing under a dead generation.</summary>
    [Fact]
    public async Task A_revoke_before_the_keeping_keeps_nothing_under_a_dead_generation()
    {
        _appointed.Generation = null;
        Assert.Equal(SupervisorWhy.AppointmentChanged, (await Decisions().KeepAsync(Answer(), Park(), Kept)).Why);
        _appointed.Generation = "g-2";
        Assert.Equal(SupervisorWhy.AppointmentChanged, (await Decisions().KeepAsync(Answer(), Park(), Kept)).Why);
    }

    /// <summary>
    /// An admitted decision's files rewritten (another label, an earlier <c>goesOnAt</c>, a <c>released</c> line appended): nothing reads
    /// them, so none is handed early or as another label; a held or released one is <c>stale</c> as tampered at the next comparison, its
    /// word taken back. A comparison under the lock beside a state's append finds no difference.
    /// </summary>
    [Fact]
    public async Task Rewritten_files_are_never_read_and_make_their_decision_stale()
    {
        var decisions = Decisions();
        var ledger = new Ledger();
        var kept = await decisions.KeepAsync(Answer(), Park(), Kept);
        var file = Path.Combine(Folder, kept.Decision + ".json");
        var states = Path.Combine(Folder, kept.Decision + ".states");

        Assert.Equal(0, await decisions.CompareAsync(Session, ledger.TakeBack, Kept.AddMinutes(1)));

        var earlier = File.ReadAllText(file).Replace("2026-10-24T09:05:00", "2026-10-24T08:00:00");
        Assert.NotEqual(File.ReadAllText(file), earlier);
        File.WriteAllText(file, earlier);
        Assert.Empty(await decisions.ReleaseAsync(Session, ledger.Say, Kept.AddMinutes(1)));
        Assert.Equal(1, await decisions.CompareAsync(Session, ledger.TakeBack, Kept.AddMinutes(1)));
        Assert.Equal((SupervisorState.Stale, SupervisorWhy.Tampered), (decisions.LiveOf(Session).Single().State, decisions.LiveOf(Session).Single().Why));

        var released = Decisions();
        var again = await released.KeepAsync(Answer(question: "q-2"), Park(Question("q-2")), Kept);
        await released.ReleaseAsync(Session, ledger.Say, Kept.AddMinutes(5));
        File.AppendAllText(Path.Combine(Folder, again.Decision + ".states"), "{\"state\":\"released\",\"at\":\"2026-10-24T09:00:00Z\",\"word\":\"w-other\"}\n");
        Assert.False(released.Handable(Session, "w-other"));
        Assert.Equal(1, await released.CompareAsync(Session, ledger.TakeBack, Kept.AddMinutes(6)));
        Assert.Equal(["w0"], ledger.TakenBack);

        var labelled = Decisions();
        var third = await labelled.KeepAsync(Answer(question: "q-3"), Park(Question("q-3")), Kept);
        await labelled.ReleaseAsync(Session, ledger.Say, Kept.AddMinutes(5));
        var (moved, ticket) = await labelled.AdmitAsync(Session, _ => Task.FromResult(true), "go-1", Kept.AddMinutes(6));
        Assert.True(moved);
        var relabelled = Path.Combine(Folder, third.Decision + ".json");
        var weekly = File.ReadAllText(relabelled).Replace("\"choice\": \"The monthly one\"", "\"choice\": \"The weekly one\"");
        Assert.NotEqual(File.ReadAllText(relabelled), weekly);
        File.WriteAllText(relabelled, weekly);
        Assert.Equal(0, await labelled.CompareAsync(Session, ledger.TakeBack, Kept.AddMinutes(6)));
        Assert.Contains("\"The monthly one\"", labelled.Hand(ticket!));
        Assert.Null(labelled.Hand(ticket!));
    }

    /// <summary>A forged <c>held</c> file and a forged <c>released</c> one with a matching word: never in memory, so never handed.</summary>
    [Fact]
    public async Task Forged_decision_files_are_never_handed()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "forged.json"), """{ "decision": "forged", "session": "a1b2c3d4", "question": { "id": "q-1", "question": "Which?", "choices": [ { "label": "A", "does": "x" } ] }, "choice": "A", "reasons": [], "keptAt": "2026-10-24T09:00:00Z" }""");
        File.WriteAllText(Path.Combine(Folder, "forged.states"), "{\"state\":\"released\",\"at\":\"2026-10-24T09:00:00Z\",\"word\":\"w-forged\"}\n");
        var decisions = Decisions();

        Assert.False(decisions.Handable(Session, "w-forged"));
        Assert.False(decisions.Authored(Session, "w-forged"));
        Assert.Empty(await decisions.ReleaseAsync(Session, new Ledger().Say, Kept.AddHours(1)));
        var (moved, ticket) = await decisions.AdmitAsync(Session, expect => Task.FromResult(expect.Count == 0), "go", Kept);
        Assert.Equal((true, (SupervisorTicket?)null), (moved, ticket));
        var history = Assert.Single(decisions.History(Session));
        Assert.True(history.Earlier);
    }

    /// <summary>
    /// A restart: <c>held</c> and <c>released</c> stale, <c>admitted</c> interrupted, each question the owner's, nothing handable; a second
    /// park and the restart keep every earlier decision's holder and reasons for the history, an earlier <c>went-on</c> among them.
    /// </summary>
    [Fact]
    public async Task A_restart_closes_every_decision_and_keeps_its_history_granting_nothing()
    {
        var before = Decisions();
        var ledger = new Ledger();
        var went = await before.KeepAsync(Answer(), Park(), Kept);
        await before.ReleaseAsync(Session, ledger.Say, Kept.AddMinutes(5));
        var (_, ticket) = await before.AdmitAsync(Session, _ => Task.FromResult(true), "go-1", Kept.AddMinutes(6));
        Assert.True(await before.WentOnAsync(ticket!, Kept.AddMinutes(6)));
        var admitted = await before.KeepAsync(Answer(question: "q-2"), Park(Question("q-2")), Kept.AddMinutes(10));
        await before.ReleaseAsync(Session, ledger.Say, Kept.AddMinutes(15));
        await before.AdmitAsync(Session, _ => Task.FromResult(true), "go-2", Kept.AddMinutes(16));
        await before.KeepAsync(Answer(question: "q-3"), Park(Question("q-3")), Kept.AddMinutes(20));

        var after = Decisions();
        Assert.Equal([Session], after.SweepAtStart(Kept.AddHours(1)));

        var history = after.History(Session);
        Assert.Equal(
            [(went.Decision, SupervisorState.WentOn), (admitted.Decision, SupervisorState.Interrupted), (history[2].Id, SupervisorState.Stale)],
            history.Select(view => ((string?)view.Id, view.State)));
        Assert.All(history, view => Assert.True(view.Earlier));
        Assert.Equal(("terminal", "pwsh", "D201 point 3"), (history[0].Holder.Kind, history[0].Holder.Parent, history[0].Reasons[0].Source));
        Assert.False(after.Handable(Session, "w0"));
        Assert.False(after.Authored(Session, "w1"));
        Assert.Equal(SupervisorWhy.Overruled, (await after.KeepAsync(Answer(question: "q-3"), Park(Question("q-3")), Kept.AddHours(1))).Why);
        Assert.Empty(after.SweepAtStart(Kept.AddHours(2)));
    }

    /// <summary>
    /// Admission by ticket: held moves nothing; released moves with <c>expect</c> naming its word and is admitted; the ticket hands the
    /// composed paragraph once; <c>went-on</c> only after; a failure before the wire is <c>not-handed</c>, and overrule from admitted on
    /// changes nothing.
    /// </summary>
    [Fact]
    public async Task Admission_is_by_a_single_use_ticket()
    {
        var decisions = Decisions();
        var ledger = new Ledger();
        var kept = await decisions.KeepAsync(Answer(), Park(), Kept);
        IReadOnlyList<string>? expected = null;

        Assert.Equal((false, (SupervisorTicket?)null), await decisions.AdmitAsync(Session, expect => { expected = expect; return Task.FromResult(true); }, "go", Kept));
        Assert.Null(expected);

        await decisions.ReleaseAsync(Session, ledger.Say, Kept.AddMinutes(5));
        var (moved, ticket) = await decisions.AdmitAsync(Session, expect => { expected = expect; return Task.FromResult(true); }, "go", Kept.AddMinutes(6));
        Assert.True(moved);
        Assert.Equal(["w0"], expected);
        Assert.True(decisions.Handable(Session, "w0", ticket));
        Assert.False(decisions.Handable(Session, "w0"));
        Assert.Equal(SupervisorState.Admitted, await decisions.OverruleAsync(Session, kept.Decision!, ledger.TakeBack, Kept.AddMinutes(6)));
        Assert.Equal(SupervisorHanded.Text("The monthly one"), decisions.Hand(ticket!));
        Assert.Null(decisions.Hand(ticket!));
        Assert.True(await decisions.NotHandedAsync(ticket!, "spawn", Kept.AddMinutes(7)));
        Assert.False(await decisions.WentOnAsync(ticket!, Kept.AddMinutes(7)));
        Assert.Equal(SupervisorState.NotHanded, decisions.LiveOf(Session).Single().State);
        Assert.Equal(SupervisorWhy.Overruled, (await decisions.KeepAsync(Answer(), Park(), Kept)).Why);
    }

    /// <summary>The look's own moves: the owner's word supersedes a held decision; a released one whose record stopped is stale, its word taken back.</summary>
    [Fact]
    public async Task The_owners_word_supersedes_and_a_record_no_longer_parked_makes_it_stale()
    {
        var ledger = new Ledger();
        var held = Decisions();
        await held.KeepAsync(Answer(), Park(), Kept);
        await held.SettleAsync(Session, parked: true, ownerWaits: true, [], ledger.TakeBack, Kept.AddMinutes(1));
        Assert.Equal(SupervisorState.Superseded, held.LiveOf(Session).Single().State);

        var stopped = Decisions();
        await stopped.KeepAsync(Answer(), Park(), Kept);
        await stopped.ReleaseAsync(Session, ledger.Say, Kept.AddMinutes(5));
        await stopped.SettleAsync(Session, parked: false, ownerWaits: false, ["w0"], ledger.TakeBack, Kept.AddMinutes(6));
        Assert.Equal((SupervisorState.Stale, SupervisorWhy.NotParked), (stopped.LiveOf(Session).Single().State, stopped.LiveOf(Session).Single().Why));
        Assert.Equal(["w0"], ledger.TakenBack);
    }

    /// <summary>Every transition not in §3.6 is refused and changes nothing.</summary>
    [Fact]
    public async Task Every_transition_not_in_the_lifecycle_is_refused()
    {
        var allowed = new HashSet<(SupervisorState, SupervisorState)>
        {
            (SupervisorState.Held, SupervisorState.Released), (SupervisorState.Held, SupervisorState.Overruled),
            (SupervisorState.Held, SupervisorState.Superseded), (SupervisorState.Held, SupervisorState.Stale),
            (SupervisorState.Released, SupervisorState.Admitted), (SupervisorState.Released, SupervisorState.Overruled),
            (SupervisorState.Released, SupervisorState.Superseded), (SupervisorState.Released, SupervisorState.Stale),
            (SupervisorState.Admitted, SupervisorState.WentOn), (SupervisorState.Admitted, SupervisorState.NotHanded),
            (SupervisorState.Admitted, SupervisorState.Interrupted),
        };
        foreach (var from in Enum.GetValues<SupervisorState>())
        {
            foreach (var to in Enum.GetValues<SupervisorState>())
            {
                Assert.Equal(allowed.Contains((from, to)), SupervisorStates.Moves(from, to));
            }
        }

        var decisions = Decisions();
        var kept = await decisions.KeepAsync(Answer(), Park(), Kept);
        Assert.False(await decisions.TryMoveAsync(Session, kept.Decision!, SupervisorState.WentOn, Kept));
        Assert.False(await decisions.TryMoveAsync(Session, kept.Decision!, SupervisorState.Admitted, Kept));
        Assert.Equal(SupervisorState.Held, decisions.LiveOf(Session).Single().State);
    }

    /// <summary>The handed text's twin: the driver hands exactly the service's paragraph, which grants nothing.</summary>
    [Fact]
    public void The_handed_text_is_the_services_twin_and_grants_nothing()
    {
        var text = SupervisorHanded.Text("The monthly one");
        var service = File.ReadAllText(Path.Combine(
            WorkspaceRoot.Folder, "src", "Daoris.Service", "Daoris.Service.Core", "Sessions", "SupervisorWord.cs"));
        Assert.Contains("chose \"The monthly one\" among the answers you offered, on the owner's behalf.", text);
        Assert.Contains("chose \\\"{label}\\\" among the answers you offered, on the owner's behalf.", service);

        foreach (var part in new[] { "These are not the owner's own words.", "it grants nothing: no ", "go-ahead, sign-in, access, spending, push, publication, deletion or history rewrite", "their words win." })
        {
            Assert.Contains(part, text);
            Assert.Contains(part, service);
        }

        Assert.DoesNotContain("I push to origin now", text);
    }
}
