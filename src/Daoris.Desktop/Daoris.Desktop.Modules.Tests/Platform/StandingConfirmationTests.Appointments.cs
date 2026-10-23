using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// SUPERVISOR1a-ii (D208 points 2 and 3; the supervisor design §2.2, §2.3, §5 and §11 row 1a-ii): the owner's appointments in D201's
/// record. An appointment is read, written and turned off apart from the presses; its key's hash lives in memory alone, so one found at
/// a start is dormant; a role's act is covered only for its repository, holder and key, by a proof over the connection's nonce; every
/// write is one mutation step, and a revoke sweeps its decisions after it, never inside.
/// </summary>
public sealed partial class StandingConfirmationTests
{
    private const string Engine = "engine";

    /// <summary>A judge whose appointments are offered, with <see cref="Engine"/> a repository it drives.</summary>
    private StandingConfirmation Appointing(StandingPlace place = StandingPlace.Outside, MachineLog? log = null)
    {
        var standing = new StandingConfirmation(Home, log, new Placed(place), offered: true, appointments: true)
        {
            Drives = repository => repository is Engine or "other",
        };
        standing.Load();
        return standing;
    }

    /// <summary>Appoint the supervisor for <paramref name="repository"/>, the terminal's start a stand-in that keeps the key it was handed.</summary>
    private static (AppointmentChange Change, string? Key) Appoint(StandingConfirmation standing, string repository = Engine)
    {
        string? handed = null;
        var change = standing.Appoint(Roles.Supervisor, repository, RoleHolders.Terminal, on: true, key =>
        {
            handed = key;
            return true;
        });
        return (change, handed);
    }

    private static string GenerationOf(StandingConfirmation standing, string repository = Engine) =>
        standing.Admit(repository).Generation ?? throw new InvalidOperationException("not admitted");

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

    private static RoleJudgement JudgeRole(StandingConfirmation standing, JsonElement payload, string? nonce) =>
        standing.JudgeRole(Driver, SupervisorRoutes.Answer, payload, null, nonce);

    [Fact]
    public void An_appointment_is_written_read_and_turned_off_apart_from_the_presses_and_the_record_holds_no_key()
    {
        var standing = Appointing();
        Assert.True(standing.Set([StandingPresses.Stop]).Done);
        var (change, key) = Appoint(standing);
        Assert.True(change.Answer.Done);
        Assert.Empty(change.Ended);

        var text = File.ReadAllText(standing.RecordPath);
        using (var document = JsonDocument.Parse(text))
        {
            var appointment = Assert.Single(document.RootElement.GetProperty("appointments").EnumerateArray());
            Assert.Equal(
                ["role", "repository", "holder", "generation", "since"],
                appointment.EnumerateObject().Select(each => each.Name));
            Assert.Equal((Roles.Supervisor, Engine, RoleHolders.Terminal), (
                appointment.GetProperty("role").GetString(), appointment.GetProperty("repository").GetString(),
                appointment.GetProperty("holder").GetString()));
            Assert.Equal(["stop"], document.RootElement.GetProperty("presses").EnumerateArray().Select(each => each.GetString()));
        }

        // Neither the key nor its hash in any file of the home.
        var hash = Convert.ToHexStringLower(HolderKey.Hash(key)!);
        foreach (var file in Directory.EnumerateFiles(Home, "*", SearchOption.AllDirectories))
        {
            var written = ReadShared(file);
            Assert.DoesNotContain(key!, written, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(hash, written, StringComparison.OrdinalIgnoreCase);
        }

        // The presses turned off keep the appointment; the appointment turned off keeps the presses.
        Assert.True(standing.Set([]).Done);
        Assert.Single(standing.Now().Appointments!);
        Assert.True(standing.Set([StandingPresses.Hold]).Done);
        var off = standing.Appoint(Roles.Supervisor, Engine, RoleHolders.Terminal, on: false, _ => throw new InvalidOperationException());
        Assert.True(off.Answer.Done);
        Assert.Single(off.Ended);
        Assert.Empty(standing.Now().Appointments!);
        Assert.Equal([StandingPresses.Hold], standing.Now().Presses);
        Assert.Equal(SupervisorWhy.NotAppointed, standing.Admit(Engine).Why);
    }

    /// <summary>
    /// §2.3: an appointment read at a start is dormant, whatever the record carries beside it, a key's hash an agent wrote there included;
    /// the row says it, nothing admits under it, and <i>Appoint again</i> is a new generation with a new key.
    /// </summary>
    [Fact]
    public void An_appointment_found_at_a_start_is_dormant_and_a_hash_written_beside_it_is_never_read()
    {
        var first = Appointing();
        var (_, key) = Appoint(first);
        var generation = GenerationOf(first);

        // An agent's ordinary reach: its own key's hash written into the record.
        var forged = HolderKey.Draw();
        var text = File.ReadAllText(first.RecordPath).Replace(
            "\"holder\":", $"\"hash\":\"{Convert.ToHexStringLower(HolderKey.Hash(forged)!)}\",\"key\":\"{forged}\",\"holder\":", StringComparison.Ordinal);
        File.WriteAllText(first.RecordPath, text);

        var next = Appointing();
        var found = Assert.Single(next.Now().Appointments!);
        Assert.True(found.Dormant);
        Assert.Equal(SupervisorWhy.Dormant, next.Admit(Engine).Why);
        var nonce = SupervisorProof.Nonce();
        Assert.Equal(SupervisorWhy.Dormant, JudgeRole(next, Answer(forged, nonce, generation), nonce).Why);
        Assert.Equal(SupervisorWhy.Dormant, JudgeRole(next, Answer(key!, nonce, generation), SupervisorProof.Nonce()).Why);

        var (again, renewed) = Appoint(next);
        Assert.True(again.Answer.Done);
        Assert.Equal([generation], again.Ended);
        Assert.NotEqual(key, renewed);
        Assert.False(Assert.Single(next.Now().Appointments!).Dormant);
        Assert.NotEqual(generation, GenerationOf(next));
    }

    /// <summary>§2.2, §3.1, §4.6: covered only for its appointment's generation and key, by a proof over this connection's unused nonce.</summary>
    [Fact]
    public void A_role_act_is_covered_only_by_the_holders_proof_over_its_own_nonce_and_form()
    {
        var standing = Appointing();
        var (_, key) = Appoint(standing);
        var generation = GenerationOf(standing);
        var nonce = SupervisorProof.Nonce();

        var covered = JudgeRole(standing, Answer(key!, nonce, generation), nonce);
        Assert.True(covered.Covered);
        Assert.Equal((Engine, generation), (covered.Repository, covered.Generation));
        Assert.Equal(1, Assert.Single(standing.Now().Appointments!).Answers);

        // No proof's nonce (used, or expired), another connection's nonce, another key, an altered form.
        Assert.Equal(SupervisorWhy.NotHolder, JudgeRole(standing, Answer(key!, nonce, generation), null).Why);
        Assert.Equal(SupervisorWhy.NotHolder, JudgeRole(standing, Answer(key!, SupervisorProof.Nonce(), generation), nonce).Why);
        Assert.Equal(SupervisorWhy.NotHolder, JudgeRole(standing, Answer(HolderKey.Draw(), nonce, generation), nonce).Why);
        var altered = Answer(key!, nonce, generation);
        var changed = JsonDocument.Parse(altered.GetRawText().Replace("\"Monthly\"", "\"Weekly\"", StringComparison.Ordinal)).RootElement;
        Assert.Equal(SupervisorWhy.NotHolder, JudgeRole(standing, changed, nonce).Why);

        // Anything more or less than the form; another generation; a route the role names no method for.
        Assert.Equal(SupervisorWhy.Form, JudgeRole(standing, Answer(key!, nonce, generation, extra: "\"key\":\"x\""), nonce).Why);
        Assert.Equal(SupervisorWhy.AppointmentChanged, JudgeRole(standing, Answer(key!, nonce, "older"), nonce).Why);
        Assert.Equal(StandingWhy.NotNamed, standing.JudgeRole(Driver, "SESSION_INPUT", Answer(key!, nonce, generation), null, nonce).Why);

        // A press granted or none changes nothing of it: judged on its own, never open.
        Assert.False(standing.Now().On);
    }

    [Theory]
    [InlineData(StandingPlace.InAgentJob, StandingWhy.InAgentJob)]
    [InlineData(StandingPlace.Untold, StandingWhy.Untold)]
    public void A_contained_or_unplaced_caller_is_refused_before_its_proof_is_read(StandingPlace place, string why)
    {
        var standing = Appointing(place);
        var (_, key) = Appoint(standing);
        var nonce = SupervisorProof.Nonce();
        Assert.Equal(why, JudgeRole(standing, Answer(key!, nonce, GenerationOf(standing)), nonce).Why);
    }

    /// <summary>§2.3: a revoke and a re-appointment of the same holder differ in generation, so an act under the old one is refused.</summary>
    [Fact]
    public void A_revoke_and_a_new_appointment_of_the_same_holder_refuse_the_old_generation()
    {
        var standing = Appointing();
        var (_, key) = Appoint(standing);
        var old = GenerationOf(standing);
        standing.Appoint(Roles.Supervisor, Engine, RoleHolders.Terminal, on: false, _ => true);
        var (_, renewed) = Appoint(standing);
        var nonce = SupervisorProof.Nonce();
        Assert.Equal(SupervisorWhy.AppointmentChanged, JudgeRole(standing, Answer(key!, nonce, old), nonce).Why);
        Assert.True(JudgeRole(standing, Answer(renewed!, nonce, GenerationOf(standing)), nonce).Covered);
    }

    /// <summary>
    /// D208 §2.3: this build does not offer appointments; one that offers them only where D201's grant is offered; an unknown role, an
    /// actor holder, a repository Daoris does not drive, and a terminal that could not start each keep nothing.
    /// </summary>
    [Fact]
    public void An_appointment_this_build_does_not_offer_or_cannot_start_keeps_nothing()
    {
        Assert.False(StandingConfirmation.AppointmentsOffered);
        var dark = new StandingConfirmation(Home, null, new Placed(StandingPlace.Outside), offered: true) { Drives = _ => true };
        dark.Load();
        Assert.Equal(StandingConfirmation.NotOffered, Appoint(dark).Change.Answer.Why);
        Assert.False(new StandingConfirmation(Home, null, new Placed(StandingPlace.Outside), offered: false, appointments: true).AppointsOffered);
        var nonce = SupervisorProof.Nonce();
        Assert.Equal(SupervisorWhy.NotOffered, JudgeRole(dark, Answer(HolderKey.Draw(), nonce, "g"), nonce).Why);
        Assert.Equal(SupervisorWhy.NotOffered, dark.Admit(Engine).Why);

        var standing = Appointing();
        var started = 0;
        bool Start(string _)
        {
            started++;
            return true;
        }

        Assert.Equal(StandingConfirmation.UnknownRole, standing.Appoint("steward", Engine, RoleHolders.Terminal, true, Start).Answer.Why);
        Assert.Equal(StandingConfirmation.NotOffered, standing.Appoint(Roles.Supervisor, Engine, RoleHolders.Actor, true, Start).Answer.Why);
        Assert.Equal(StandingConfirmation.UnknownRepository, standing.Appoint(Roles.Supervisor, "elsewhere", RoleHolders.Terminal, true, Start).Answer.Why);
        Assert.Equal(0, started);
        Assert.Equal(StandingConfirmation.Unstarted, standing.Appoint(Roles.Supervisor, Engine, RoleHolders.Terminal, true, _ => false).Answer.Why);
        Assert.False(File.Exists(standing.RecordPath));
        Assert.Empty(standing.Now().Appointments!);
        Assert.Equal(SupervisorWhy.NotAppointed, standing.Admit(Engine).Why);
    }

    /// <summary>§2.3: the role is never one of the presses, nor taken by a grant.</summary>
    [Fact]
    public void The_role_is_never_a_press()
    {
        Assert.DoesNotContain(Roles.Supervisor, StandingPresses.Built);
        Assert.DoesNotContain("supervise", StandingPresses.Built);
        var standing = Granted();
        Assert.Equal(StandingConfirmation.UnknownPress, standing.Set(["supervise"]).Why);
        Assert.Equal(StandingConfirmation.UnknownPress, standing.Set([Roles.Supervisor]).Why);
    }

    /// <summary>An older shape, presses alone, reads with no appointment; an appointment then written carries the presses through.</summary>
    [Fact]
    public void A_record_of_the_older_shape_reads_and_its_presses_are_carried_through_an_appointment()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(new StandingConfirmation(Home).RecordPath)!);
        var path = new StandingConfirmation(Home).RecordPath;
        File.WriteAllText(path, $$"""{"schemaVersion":1,"presses":["stop","finish"],"since":"2026-10-23T06:05:00Z","machine":"{{Machine}}"}""");
        var standing = Appointing();
        Assert.Empty(standing.Now().Appointments!);
        Assert.True(Appoint(standing).Change.Answer.Done);
        var after = StandingConfirmation.Read(File.ReadAllText(path), Machine);
        Assert.Equal([StandingPresses.Stop, StandingPresses.Finish], after.Presses);
        Assert.Single(after.Appointments);
    }

    /// <summary>
    /// §2.3: a press grant, an appointment change and a revoke raced, many times over: every write is one step, so the record always
    /// says what memory installed and nothing is lost.
    /// </summary>
    [Fact]
    public async Task A_grant_an_appointment_and_a_revoke_raced_lose_nothing()
    {
        var standing = Appointing();
        for (var round = 0; round < 25; round++)
        {
            var presses = round % 2 == 0 ? new[] { StandingPresses.Stop } : [];
            await Task.WhenAll(
                Task.Run(() => standing.Set(presses)),
                Task.Run(() => Appoint(standing, Engine)),
                Task.Run(() => standing.Appoint(Roles.Supervisor, "other", RoleHolders.Terminal, round % 3 != 0, _ => true)),
                Task.Run(() => standing.Set([StandingPresses.Hold])));

            var memory = standing.Now();
            var written = StandingConfirmation.Read(File.ReadAllText(standing.RecordPath), Machine);
            Assert.Equal(memory.Presses, written.Presses);
            Assert.Equal(
                memory.Appointments!.Select(each => each.Repository).Order(),
                written.Appointments.Select(each => each.Repository).Order());
            Assert.Contains(written.Appointments, each => each.Repository == Engine);
        }
    }

    /// <summary>
    /// §5, §3.6: the real revoke raced against a keeping, a release and an admission, which 1a-i could only stub. The mutation step
    /// ends first and the sweep follows under each decision's lock: no lock taken in reverse, nothing deadlocks, and nothing is kept,
    /// released or admitted under the dead generation.
    /// </summary>
    [Fact]
    public async Task The_real_revoke_raced_against_a_keeping_a_release_and_an_admission_hands_nothing_under_a_dead_generation()
    {
        for (var round = 0; round < 20; round++)
        {
            var standing = Appointing();
            Appoint(standing);
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

            var revoke = Task.Run(async () =>
            {
                var change = standing.Appoint(Roles.Supervisor, Engine, RoleHolders.Terminal, on: false, _ => true);
                foreach (var ended in change.Ended) await decisions.RevokedAsync(ended, TakeBack, at.AddMinutes(5));
            });
            var keep = Task.Run(() => decisions.KeepAsync(answer, park, at));
            var release = Task.Run(() => decisions.ReleaseAsync(session, Say, at.AddMinutes(5)));
            var admit = Task.Run(() => decisions.AdmitAsync(session, _ => Task.FromResult(true), "go", at.AddMinutes(6)));
            var all = Task.WhenAll(revoke, keep, release, admit);
            Assert.Same(all, await Task.WhenAny(all, Task.Delay(TimeSpan.FromSeconds(20))));

            // Swept once more after every race, as the next look would read it: nothing live is handable under the dead generation.
            await decisions.RevokedAsync(generation, TakeBack, at.AddMinutes(7));
            Assert.Equal(SupervisorWhy.NotAppointed, standing.Admit(Engine).Why);
            foreach (var view in decisions.LiveOf(session))
            {
                Assert.Equal(generation, view.Generation);
                // An admission made before the revoke is a taken-in act; anything else is stale or never moved past held.
                Assert.True(view.State is SupervisorState.Stale or SupervisorState.Admitted, $"round {round}: {view.State}");
                if (view.State == SupervisorState.Stale && view.Word is { } word) Assert.Contains(word, taken);
                Assert.False(view.Word is { } live && decisions.Handable(session, live));
            }

            Assert.False((await decisions.KeepAsync(answer with { Question = "later" }, park with { Filed = asked with { Id = "later" } }, at)).Kept);
        }
    }

    private static string ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
