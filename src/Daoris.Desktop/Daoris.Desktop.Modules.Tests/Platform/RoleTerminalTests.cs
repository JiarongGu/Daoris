using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// D233 point 2 (the supervisor design §2.2, §2.3, §5; D209 §2.2, §3.4): the supervisor and the steward are catalogue roles, and one the
/// role door staffs with the terminal is held this run once the window starts its terminal. The key's hash lives in memory alone, so a
/// restart starts nothing; a role's act is covered only by its holder's proof over the connection's nonce, against the role as the
/// catalogue says now; a role's change ends its hold and sweeps its decisions after the judge's step, never inside.
/// </summary>
public sealed class RoleTerminalTests : Bridge
{
    private const string Driver = DriverModule.Name;
    private const string Engine = "engine";

    /// <summary>A judge whose catalogue staffs the supervisor with the terminal at the machine, and the steward where asked.</summary>
    private StandingConfirmation Staffed(StandingPlace place = StandingPlace.Outside, MachineLog? log = null, RoleKit.Probe? steward = null)
    {
        var standing = new StandingConfirmation(Home, log, new StandingConfirmationTests.Placed(place), offered: true, walk: new StandingConfirmationTests.Walked());
        standing.Load();
        standing.Staff(RoleKit.Supervisor());
        return steward is null ? standing : standing.Probing(steward);
    }

    /// <summary>The supervisor's terminal started, the tab a stand-in that keeps the key it was handed.</summary>
    private static (HoldChange Change, string? Key) Start(StandingConfirmation standing, string? share = "own") =>
        RoleKit.Start(standing, new HoldRequest(AppointedRoles.Supervisor, share));

    private static string GenerationOf(StandingConfirmation standing, string repository = Engine) =>
        standing.Admit(repository).Generation ?? throw new InvalidOperationException("not admitted");

    /// <summary>The supervisor held by your rules alone: the role door's change, then the door's sweep of the holds it ended.</summary>
    private static IReadOnlyList<string> TurnOff(StandingConfirmation standing, RoleRecord? role = null)
    {
        standing.Staff(role ?? RoleKit.Supervisor(holder: RoleHolders.Rules));
        return standing.RolesChanged();
    }

    /// <summary>An answer's payload, proved by <paramref name="key"/> over <paramref name="nonce"/>.</summary>
    private static JsonElement Answer(string key, string nonce, string generation, string choice = "Monthly", string? extra = null)
    {
        SupervisorReason[] reasons = [new("The ticket says monthly.", "the quest's line 2")];
        var proof = SupervisorProof.Prove(key, nonce, SupervisorProof.Canonical("s1", "q-1", generation, choice, reasons))!;
        var form = new SupervisorForm("s1", "q-1", generation, proof, choice, reasons);
        var json = JsonSerializer.Serialize(form.Payload());
        if (extra is not null) json = json[..^1] + "," + extra + "}";
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    /// <summary>A defer's payload, proved by <paramref name="key"/> over <paramref name="nonce"/> and the canonical bytes of <paramref name="route"/>.</summary>
    private static JsonElement Defer(string key, string nonce, string generation, string bound = "sign-in", string route = SupervisorRoutes.Defer)
    {
        SupervisorReason[] reasons = [new("A sign-in is the owner's.", "D191 point 3")];
        var proof = SupervisorProof.Prove(key, nonce, SupervisorProof.Canonical(route, "s1", "q-1", generation, bound, reasons))!;
        return JsonDocument.Parse(JsonSerializer.Serialize(new SupervisorDeferForm("s1", "q-1", generation, proof, bound, reasons).Payload()))
            .RootElement.Clone();
    }

    private static RoleJudgement JudgeRole(StandingConfirmation standing, JsonElement payload, string? nonce) =>
        standing.JudgeRole(Driver, SupervisorRoutes.Answer, payload, null, nonce);

    [Fact]
    public void A_role_staffed_by_the_terminal_is_started_and_neither_its_key_nor_its_hash_reaches_a_file()
    {
        var standing = Staffed();
        Assert.True(standing.Set([StandingPresses.Stop]).Done);
        var (change, key) = Start(standing);
        Assert.True(change.Answer.Done, change.Answer.Message);
        Assert.Empty(change.Ended);

        var hold = Assert.Single(standing.Now().Holds!);
        Assert.Equal((AppointedRoles.Supervisor, AppointedRoles.Supervisor), (hold.Role, hold.Kind));
        Assert.NotNull(GenerationOf(standing));

        // Neither the key nor its hash in any file of the home; the standing confirmation's record keeps its presses alone.
        var hash = Convert.ToHexStringLower(HolderKey.Hash(key)!);
        foreach (var file in Directory.EnumerateFiles(Home, "*", SearchOption.AllDirectories))
        {
            var written = ReadShared(file);
            Assert.DoesNotContain(key!, written, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(hash, written, StringComparison.OrdinalIgnoreCase);
        }

        // The presses turned off keep the hold; the hold ended keeps the presses.
        Assert.True(standing.Set([]).Done);
        Assert.Single(standing.Now().Holds!);
        Assert.True(standing.Set([StandingPresses.Hold]).Done);
        Assert.Single(TurnOff(standing));
        Assert.Empty(standing.Now().Holds!);
        Assert.Equal([StandingPresses.Hold], standing.Now().Presses);
        Assert.Equal(SupervisorWhy.NotStaffed, standing.Admit(Engine).Why);
    }

    /// <summary>A restart holds nothing: the role staffed by the terminal is not started until the window starts it, a new generation and key.</summary>
    [Fact]
    public void After_a_restart_a_role_staffed_by_the_terminal_is_not_started_until_the_window_starts_it_again()
    {
        var first = Staffed();
        var (_, key) = Start(first);
        var generation = GenerationOf(first);

        var next = Staffed();
        Assert.Empty(next.Now().Holds!);
        Assert.Equal(SupervisorWhy.NotStarted, next.Admit(Engine).Why);
        var nonce = SupervisorProof.Nonce();
        Assert.Equal(SupervisorWhy.NotStarted, JudgeRole(next, Answer(key!, nonce, generation), nonce).Why);

        var (again, renewed) = Start(next, share: null);
        Assert.True(again.Answer.Done);
        Assert.NotEqual(key, renewed);
        Assert.NotEqual(generation, GenerationOf(next));
    }

    /// <summary>§2.2, §3.1, §4.6: covered only for its hold's generation and key, by a proof over this connection's unused nonce.</summary>
    [Fact]
    public void A_role_act_is_covered_only_by_the_holders_proof_over_its_own_nonce_and_form()
    {
        var standing = Staffed();
        var (_, key) = Start(standing);
        var generation = GenerationOf(standing);
        var nonce = SupervisorProof.Nonce();

        var covered = JudgeRole(standing, Answer(key!, nonce, generation), nonce);
        Assert.True(covered.Covered);
        Assert.Equal(((string?)null, generation, AppointedRoles.Supervisor), (covered.Repository, covered.Generation, covered.Role));
        Assert.Equal(1, Assert.Single(standing.Now().Holds!).Answers);

        // No proof's nonce (used, or expired), another connection's nonce, another key, an altered form.
        Assert.Equal(SupervisorWhy.NotHolder, JudgeRole(standing, Answer(key!, nonce, generation), null).Why);
        Assert.Equal(SupervisorWhy.NotHolder, JudgeRole(standing, Answer(key!, SupervisorProof.Nonce(), generation), nonce).Why);
        Assert.Equal(SupervisorWhy.NotHolder, JudgeRole(standing, Answer(HolderKey.Draw(), nonce, generation), nonce).Why);
        var altered = Answer(key!, nonce, generation);
        var changed = JsonDocument.Parse(altered.GetRawText().Replace("\"Monthly\"", "\"Weekly\"", StringComparison.Ordinal)).RootElement;
        Assert.Equal(SupervisorWhy.NotHolder, JudgeRole(standing, changed, nonce).Why);

        // Anything more or less than the form; another generation; a route the role names no method for.
        Assert.Equal(SupervisorWhy.Form, JudgeRole(standing, Answer(key!, nonce, generation, extra: "\"key\":\"x\""), nonce).Why);
        Assert.Equal(SupervisorWhy.RoleChanged, JudgeRole(standing, Answer(key!, nonce, "older"), nonce).Why);
        Assert.Equal(StandingWhy.NotNamed, standing.JudgeRole(Driver, "SESSION_INPUT", Answer(key!, nonce, generation), null, nonce).Why);

        // A press granted or none changes nothing of it: judged on its own, never open.
        Assert.False(standing.Now().On);
    }

    /// <summary>SUPERVISOR1b (§3.1 <c>SUPERVISOR_DEFER</c>): the defer is the role's second act, judged as the answer is, its proof over its own route's bytes.</summary>
    [Fact]
    public void A_defer_is_judged_as_the_answer_is_each_proved_over_its_own_route()
    {
        var standing = Staffed();
        var (_, key) = Start(standing);
        var generation = GenerationOf(standing);
        var nonce = SupervisorProof.Nonce();
        RoleJudgement Judge(JsonElement payload, string route = SupervisorRoutes.Defer) => standing.JudgeRole(Driver, route, payload, null, nonce);

        Assert.True(Judge(Defer(key!, nonce, generation)).Covered);
        Assert.Equal(SupervisorWhy.NotHolder, Judge(Defer(key!, nonce, generation, route: SupervisorRoutes.Answer)).Why);
        Assert.Equal(SupervisorWhy.Form, Judge(Answer(key!, nonce, generation)).Why);
        Assert.Equal(SupervisorWhy.Form, Judge(Defer(key!, nonce, generation), SupervisorRoutes.Answer).Why);
        Assert.Equal(SupervisorWhy.Form, Judge(Defer(key!, nonce, generation, bound: "because")).Why);
        Assert.Equal(SupervisorWhy.NotHolder, Judge(Defer(HolderKey.Draw(), nonce, generation)).Why);
        Assert.Equal(["SUPERVISOR_ANSWER", "SUPERVISOR_DEFER", "SUPERVISOR_COURSE"], SupervisorRoutes.Acts);
    }

    [Theory]
    [InlineData(StandingPlace.InAgentJob, StandingWhy.InAgentJob)]
    [InlineData(StandingPlace.Untold, StandingWhy.Untold)]
    public void A_contained_or_unplaced_caller_is_refused_before_its_proof_is_read(StandingPlace place, string why)
    {
        var standing = Staffed(place);
        var (_, key) = Start(standing);
        var nonce = SupervisorProof.Nonce();
        Assert.Equal(why, JudgeRole(standing, Answer(key!, nonce, GenerationOf(standing)), nonce).Why);
    }

    /// <summary>
    /// The catalogue's seed staffs no terminal: the supervisor is your rules' alone and the steward no one's, so nothing starts and every
    /// act and answer is refused; a role that is not appointed, one held otherwise, and a terminal that could not start each keep nothing.
    /// </summary>
    [Fact]
    public void A_role_not_staffed_by_the_terminal_or_one_that_cannot_start_keeps_nothing()
    {
        var seeded = new StandingConfirmation(Home, null, new StandingConfirmationTests.Placed(StandingPlace.Outside), offered: true, walk: new StandingConfirmationTests.Walked());
        seeded.Load();
        Assert.False(seeded.StewardStaffed);
        Assert.Equal(StandingConfirmation.NotTerminal, Start(seeded).Change.Answer.Why);
        Assert.Equal(StandingConfirmation.NotTerminal, RoleKit.Start(seeded, RoleKit.Stewarding()).Change.Answer.Why);
        var nonce = SupervisorProof.Nonce();
        Assert.Equal(SupervisorWhy.NotStaffed, JudgeRole(seeded, Answer(HolderKey.Draw(), nonce, "g"), nonce).Why);
        Assert.Equal(SupervisorWhy.NotStaffed, RoleKit.JudgeAct(seeded, RoleKit.Act(HolderKey.Draw(), nonce, "g"), nonce).Why);
        Assert.Equal(SupervisorWhy.NotStaffed, seeded.Admit(Engine).Why);

        var standing = Staffed();
        var started = 0;
        HolderOpened Open(HolderOpen open)
        {
            started++;
            return StandingConfirmationTests.Opened(open);
        }

        Assert.Equal(StandingConfirmation.UnknownRole, standing.Start(new HoldRequest("build"), Open).Answer.Why);
        Assert.Equal(StandingConfirmation.UnknownRole, standing.Start(new HoldRequest("clerk"), Open).Answer.Why);
        Assert.Equal(0, started);
        Assert.Equal(StandingConfirmation.Unstarted,
            standing.Start(new HoldRequest(AppointedRoles.Supervisor), _ => new HolderOpened(null, null, StandingConfirmation.Unstarted, null)).Answer.Why);
        Assert.Equal(HolderHarness.Refused,
            standing.Start(new HoldRequest(AppointedRoles.Supervisor), _ => new HolderOpened(null, null, HolderHarness.Refused, "no version of claude-code is pinned")).Answer.Why);
        Assert.Empty(standing.Now().Holds!);
        Assert.Equal(SupervisorWhy.NotStarted, standing.Admit(Engine).Why);
    }

    /// <summary>A role's change ends its hold, and a terminal started again of the same role refuses the old generation.</summary>
    [Fact]
    public void A_roles_change_and_a_new_start_refuse_the_old_generation()
    {
        var standing = Staffed();
        var (_, key) = Start(standing);
        var old = GenerationOf(standing);
        Assert.Equal([old], TurnOff(standing));
        Assert.Empty(standing.RolesChanged());

        standing.Staff(RoleKit.Supervisor());
        var (_, renewed) = Start(standing, share: null);
        var live = GenerationOf(standing);
        var nonce = SupervisorProof.Nonce();
        Assert.Equal(SupervisorWhy.RoleChanged, JudgeRole(standing, Answer(key!, nonce, old), nonce).Why);
        Assert.True(JudgeRole(standing, Answer(renewed!, nonce, live), nonce).Covered);

        // A change the door has not swept yet is caught at the act: the role as the catalogue says now.
        standing.Staff(RoleKit.Supervisor(holder: RoleHolders.Rules));
        var again = SupervisorProof.Nonce();
        Assert.Equal(SupervisorWhy.RoleChanged, JudgeRole(standing, Answer(renewed!, again, live), again).Why);
        Assert.Equal([live], standing.RolesChanged());
    }

    /// <summary>
    /// §5, §3.6: a role's change raced against a keeping, a release and an admission. The judge's step ends first and the sweep follows
    /// under each decision's lock: no lock taken in reverse, nothing deadlocks, and nothing is kept, released or admitted under the dead
    /// generation.
    /// </summary>
    [Fact]
    public async Task A_roles_change_raced_against_a_keeping_a_release_and_an_admission_hands_nothing_under_a_dead_generation()
    {
        for (var round = 0; round < 20; round++)
        {
            var standing = Staffed();
            Start(standing);
            var generation = GenerationOf(standing);
            var decisions = new QuestionSupervisorDecisions(Home, standing);
            var at = DateTimeOffset.Parse("2026-10-24T09:00:00Z");
            var session = $"r{round}";
            var asked = new ParkQuestion($"q-{round}", session, "Which report?", [new("Monthly", "I write it."), new("Weekly", "I write that.")], 1, null, at);
            var park = new SupervisedPark(Engine, true, true, asked, false, false, false);
            var answer = new SupervisorAnswer(session, asked.Id, generation, "Monthly", [new("The ticket says monthly.", "line 2")], new("terminal", "pwsh"));
            if (round % 2 == 0) await decisions.KeepAsync(answer, park, at);

            var taken = new List<string>();
            Task<bool> TakeBack(string _, string word, CancellationToken __)
            {
                lock (taken) taken.Add(word);
                return Task.FromResult(true);
            }

            async Task<SupervisorSaid> Say(string s, string d, string q, string c, string k, CancellationToken ct)
            {
                await Task.Yield();
                return new SupervisorSaid("w-" + d, null);
            }

            var change = Task.Run(async () =>
            {
                foreach (var ended in TurnOff(standing)) await decisions.RevokedAsync(ended, TakeBack, at.AddMinutes(5));
            });
            var keep = Task.Run(() => decisions.KeepAsync(answer, park, at));
            var release = Task.Run(() => decisions.ReleaseAsync(session, Say, at.AddMinutes(5)));
            var admit = Task.Run(() => decisions.AdmitAsync(session, _ => Task.FromResult(true), "go", at.AddMinutes(6)));
            var all = Task.WhenAll(change, keep, release, admit);
            Assert.Same(all, await Task.WhenAny(all, Task.Delay(TimeSpan.FromSeconds(20))));

            // Swept once more after every race, as the next look would read it: nothing live is handable under the dead generation.
            await decisions.RevokedAsync(generation, TakeBack, at.AddMinutes(7));
            Assert.Equal(SupervisorWhy.NotStaffed, standing.Admit(Engine).Why);
            foreach (var view in decisions.LiveOf(session))
            {
                Assert.Equal(generation, view.Generation);
                // An admission made before the change is a taken-in act; anything else is stale or never moved past held.
                Assert.True(view.State is SupervisorState.Stale or SupervisorState.Admitted, $"round {round}: {view.State}");
                if (view.State == SupervisorState.Stale && view.Word is { } word) Assert.Contains(word, taken);
                Assert.False(view.Word is { } live && decisions.Handable(session, live));
            }

            Assert.False((await decisions.KeepAsync(answer with { Question = "later" }, park with { Filed = asked with { Id = "later" } }, at)).Kept);
        }
    }

    // STEWARDROLE1a-i (D209 §2.2, §3.4): a holder apart from its holds, shared or its own; a role ended alone, the holder's hash kept to its
    // last; a holder ended by its last role or its tab; and the act judged against the role's authority as the catalogue says now.

    /// <summary>A judge whose catalogue staffs both roles with the terminal, the steward's table holding <paramref name="probe"/>.</summary>
    private StandingConfirmation Both(RoleKit.Probe probe, List<(string Tab, string Line, string? Title)>? told = null, List<IReadOnlyList<string>>? ended = null)
    {
        var standing = Staffed(steward: probe);
        standing.Tell = (tab, line, title) => told?.Add((tab, line, title));
        standing.Ended = generations => ended?.Add(generations);
        return standing;
    }

    [Fact]
    public void A_second_role_is_added_to_the_living_holder_or_given_a_terminal_of_its_own_and_neither_is_preselected()
    {
        var told = new List<(string Tab, string Line, string? Title)>();
        var standing = Both(new RoleKit.Probe(), told);
        var (_, key) = Start(standing);
        var holder = Assert.Single(standing.Now().Holders!);
        Assert.Equal([AppointedRoles.Supervisor], holder.Roles);

        // A holder lives: no choice is refused, and nothing opens.
        var opened = 0;
        Assert.Equal(StandingConfirmation.ChooseTerminal,
            standing.Start(RoleKit.Stewarding(share: null), open => { opened++; return StandingConfirmationTests.Opened(open); }).Answer.Why);
        Assert.Equal(StandingConfirmation.NoHolder, standing.Start(RoleKit.Stewarding(share: "h-gone"), StandingConfirmationTests.Opened).Answer.Why);
        Assert.Equal(0, opened);

        // Added to the terminal Daoris started: the same key, no second tab, a new hold and generation; its title and words told.
        var added = standing.Start(RoleKit.Stewarding(share: holder.Id), open => { opened++; return StandingConfirmationTests.Opened(open); });
        Assert.True(added.Answer.Done, added.Answer.Message);
        Assert.Equal(0, opened);
        var shared = Assert.Single(standing.Now().Holders!);
        Assert.Equal(holder.Id, shared.Id);
        Assert.Equal([AppointedRoles.Steward, AppointedRoles.Supervisor], shared.Roles);
        var line = Assert.Single(told);
        Assert.Equal(($"t-{holder.Id}", "Daoris: the steward's and supervisor's terminal"), (line.Tab, line.Title));
        Assert.Contains("The steward was added to this terminal", line.Line, StringComparison.Ordinal);
        var nonce = SupervisorProof.Nonce();
        Assert.True(RoleKit.JudgeAct(standing, RoleKit.Act(key!, nonce, RoleKit.GenerationOf(standing, AppointedRoles.Steward)), nonce).Covered);

        // A terminal of its own: a second holder with a key of its own, which replaces the steward's hold in the first.
        var (own, second) = RoleKit.Start(standing, RoleKit.Stewarding(share: "own"));
        Assert.True(own.Answer.Done);
        Assert.Single(own.Ended);
        Assert.NotEqual(key, second);
        Assert.Equal(2, standing.Now().Holders!.Count);
    }

    [Fact]
    public void A_role_ended_this_run_is_never_added_back_to_the_holder_that_held_it()
    {
        var probe = new RoleKit.Probe();
        var standing = Both(probe);
        Start(standing);
        var holder = Assert.Single(standing.Now().Holders!).Id;
        Assert.True(standing.Start(RoleKit.Stewarding(share: holder), StandingConfirmationTests.Opened).Answer.Done);
        Assert.Single(TurnOff(standing, RoleKit.Steward([RoleKit.Probe.Named], holder: null)));

        standing.Staff(RoleKit.Steward([RoleKit.Probe.Named]));
        Assert.Equal(StandingConfirmation.RevokedHolder, standing.Start(RoleKit.Stewarding(share: holder), StandingConfirmationTests.Opened).Answer.Why);
        var (own, _) = RoleKit.Start(standing, RoleKit.Stewarding(share: "own"));
        Assert.True(own.Answer.Done);
        Assert.Equal(2, standing.Now().Holders!.Count);
    }

    /// <summary>D209 §2.2, Codex's final read, finding 10: either role of a shared holder ended ends that hold alone, and the other goes on.</summary>
    [Theory]
    [InlineData(AppointedRoles.Steward)]
    [InlineData(AppointedRoles.Supervisor)]
    public void A_shared_holder_s_role_ended_alone_leaves_the_other_answering_and_its_hash_goes_with_the_last(string ended)
    {
        var told = new List<(string Tab, string Line, string? Title)>();
        var standing = Both(new RoleKit.Probe(), told);
        var (_, key) = Start(standing);
        var holder = Assert.Single(standing.Now().Holders!).Id;
        Assert.True(standing.Start(RoleKit.Stewarding(share: holder), StandingConfirmationTests.Opened).Answer.Done);
        var supervisor = GenerationOf(standing);
        var steward = RoleKit.GenerationOf(standing, AppointedRoles.Steward);
        RoleRecord Off(string kind) => kind == AppointedRoles.Steward ? RoleKit.Steward([RoleKit.Probe.Named], holder: null) : RoleKit.Supervisor(holder: RoleHolders.Rules);

        Assert.Equal([ended == AppointedRoles.Steward ? steward : supervisor], TurnOff(standing, Off(ended)));
        var nonce = SupervisorProof.Nonce();
        var answer = JudgeRole(standing, Answer(key!, nonce, supervisor), nonce);
        var act = RoleKit.JudgeAct(standing, RoleKit.Act(key!, nonce, steward), nonce);
        if (ended == AppointedRoles.Steward)
        {
            Assert.True(answer.Covered, answer.Why);
            Assert.Equal(SupervisorWhy.NotStaffed, act.Why);
            Assert.Equal([AppointedRoles.Supervisor], answer.Roles);
        }
        else
        {
            Assert.True(act.Covered, act.Why);
            Assert.Equal(SupervisorWhy.NotStaffed, standing.Admit(Engine).Why);
            Assert.Equal([AppointedRoles.Steward], act.Roles);
        }

        Assert.Equal(holder, Assert.Single(standing.Now().Holders!).Id);
        Assert.DoesNotContain(told, each => each.Line == HolderTerminal.TurnedOff);

        // The last role off: the holder's hash goes, and its tab is told.
        Assert.Single(TurnOff(standing, Off(ended == AppointedRoles.Steward ? AppointedRoles.Supervisor : AppointedRoles.Steward)));
        Assert.Empty(standing.Now().Holders!);
        Assert.Contains(told, each => each.Tab == $"t-{holder}" && each.Line == HolderTerminal.TurnedOff);
        var again = SupervisorProof.Nonce();
        Assert.False(RoleKit.JudgeAct(standing, RoleKit.Act(key!, again, steward), again).Covered);
    }

    /// <summary>
    /// D209 §2.2: a holder's tab ending, by whichever of its paths (the module tells each once, <c>HolderTerminalTests</c>), drops its hash
    /// and every hold it holds in one step, swept after it; a tab that ends while its start opens it leaves nothing kept.
    /// </summary>
    [Fact]
    public void A_holder_ends_with_its_tab_and_every_hold_it_holds_goes_with_it()
    {
        var ended = new List<IReadOnlyList<string>>();
        var standing = Both(new RoleKit.Probe(), ended: ended);
        var (_, key) = Start(standing);
        var holder = Assert.Single(standing.Now().Holders!).Id;
        Assert.True(standing.Start(RoleKit.Stewarding(share: holder), StandingConfirmationTests.Opened).Answer.Done);
        var generations = new[] { GenerationOf(standing), RoleKit.GenerationOf(standing, AppointedRoles.Steward) };

        Assert.Equal(generations.Order(), standing.HolderEnded($"t-{holder}").Order());

        Assert.Equal(generations.Order(), Assert.Single(ended).Order());
        Assert.Empty(standing.Now().Holders!);
        Assert.Empty(standing.Now().Holds!);
        var nonce = SupervisorProof.Nonce();
        Assert.False(JudgeRole(standing, Answer(key!, nonce, generations[0]), nonce).Covered);
        Assert.Empty(standing.HolderEnded($"t-{holder}"));

        // The next start draws a new key and a new tab, asked for no choice since no holder lives.
        var (renewed, fresh) = Start(standing, share: null);
        Assert.True(renewed.Answer.Done);
        Assert.NotEqual(key, fresh);

        // A tab that ends while its start still opens it: the hold is never kept.
        var exiting = standing.Start(RoleKit.Stewarding(share: "own"), open =>
        {
            standing.HolderEnded($"t-{open.Holder}");
            return StandingConfirmationTests.Opened(open);
        });
        Assert.Equal(StandingConfirmation.Unstarted, exiting.Answer.Why);
        Assert.DoesNotContain(standing.Now().Holds!, each => each.Kind == AppointedRoles.Steward);
    }

    /// <summary>D209 §3.4: an act in the role's table but not its authority, an act no table names, and another form, each refused.</summary>
    [Fact]
    public void A_role_act_is_judged_against_the_roles_authority_and_admitted_once_with_an_id_daoris_draws()
    {
        var probe = new RoleKit.Probe();
        var standing = Both(probe);
        var (_, key) = RoleKit.Start(standing, RoleKit.Stewarding(share: null));
        var generation = RoleKit.GenerationOf(standing, AppointedRoles.Steward);
        RoleJudgement Judge(JsonElement payload, string nonce) => RoleKit.JudgeAct(standing, payload, nonce);
        string Nonce() => SupervisorProof.Nonce();

        var n = Nonce();
        var admitted = Judge(RoleKit.Act(key!, n, generation), n);
        Assert.True(admitted.Covered, admitted.Why);
        Assert.Equal((AppointedRoles.Steward, RoleKit.Probe.Named, (string?)null), (admitted.Role, admitted.Act, admitted.Repository));
        Assert.Equal(RoleActStates.Admitted, standing.ActState(admitted.ActId!));

        n = Nonce();
        Assert.Equal(RoleWhy.NotRole, Judge(RoleKit.Act(key!, n, generation, act: "drive"), n).Why);
        n = Nonce();
        Assert.Equal(SupervisorWhy.Form, Judge(RoleKit.Act(key!, n, generation, form: """{"repository":"engine","extra":1}"""), n).Why);
        n = Nonce();
        Assert.Equal(SupervisorWhy.RoleChanged, Judge(RoleKit.Act(key!, n, "older"), n).Why);
        n = Nonce();
        Assert.Equal(SupervisorWhy.NotHolder, Judge(RoleKit.Act(HolderKey.Draw(), n, generation), n).Why);

        // An act the role's authority does not name: the table grew after it, or the person narrowed the role.
        standing.TableOf = role => new Dictionary<string, IRoleAct>(StringComparer.Ordinal) { [RoleKit.Probe.Named] = probe, ["later"] = probe };
        n = Nonce();
        Assert.Equal(RoleWhy.NotGranted, Judge(RoleKit.Act(key!, n, generation, act: "later"), n).Why);
        standing.Staff(RoleKit.Steward(["later"]));
        n = Nonce();
        Assert.Equal(RoleWhy.NotGranted, Judge(RoleKit.Act(key!, n, generation), n).Why);

        // The steward's table is the catalogue's vocabulary of its acts, the set-up act apart: §3.1 rows 1 to 8.
        Assert.Equal(AppointedRoles.StewardActs.Where(act => act != StewardActNames.Setup).Order(StringComparer.Ordinal), StewardActs.Table.Keys.Order(StringComparer.Ordinal));
    }

    private static string ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
