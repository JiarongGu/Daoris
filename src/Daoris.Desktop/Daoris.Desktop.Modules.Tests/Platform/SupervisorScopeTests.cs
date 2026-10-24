using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// SUPERVISOR1e (D208; the supervisor design §5, §6.3, §11 row e): a workspace's appointment under §5's one-effective rule, the
/// repository's own winning, and the overrule window the owner sets at the appointment, 1 to 60 minutes. The record's tests and the
/// row's: holder changes, both revocations, a late result, and a repository's appointment made while a decision under its workspace's
/// is held, which makes that decision <c>stale</c>.
/// </summary>
/// <remarks>
/// The judge alone, as <see cref="StandingConfirmationTests"/> holds it, with a scratch home of its own and no environment set: the
/// registry's placement a dictionary, the holder's tab a stand-in that starts nothing.
/// </remarks>
public sealed class SupervisorScopeTests : IDisposable
{
    private const string Workspace = "games";
    private const string Engine = "engine";
    private const string Atlas = "atlas";
    private const string Outside = "outside";

    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-10-25T09:00:00Z");

    private readonly string _home = Path.Combine(AppContext.BaseDirectory, "supervisor-scope", Guid.NewGuid().ToString("N")[..8]);

    /// <summary>The registry's placement as the loop last read it: a repository added later is placed by adding it here.</summary>
    private readonly Dictionary<string, string> _placed = new(StringComparer.OrdinalIgnoreCase)
    {
        [Engine] = Workspace, [Atlas] = Workspace, [Outside] = "default",
    };

    public SupervisorScopeTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private StandingConfirmation Judge(bool steward = false)
    {
        var standing = new StandingConfirmation(_home, null, new StandingConfirmationTests.Placed(StandingPlace.Outside), offered: true,
            supervisor: true, steward: steward, walk: new StandingConfirmationTests.Walked())
        {
            Drives = repository => _placed.ContainsKey(repository),
            WorkspaceOf = repository => _placed.GetValueOrDefault(repository),
            DrivesIn = workspace => _placed.Values.Contains(workspace, StringComparer.OrdinalIgnoreCase),
        };
        standing.Load();
        return standing;
    }

    private static AppointRequest Supervising(string scope, bool on = true, long? window = null, string? share = "own") =>
        new(Roles.Supervisor, scope, RoleHolders.Terminal, on, share, Window: window);

    /// <summary>Appoint, the tab a stand-in that keeps the key it was handed.</summary>
    private static (AppointmentChange Change, string? Key) Appoint(StandingConfirmation standing, string scope, long? window = null)
    {
        string? handed = null;
        var change = standing.Appoint(Supervising(scope, window: window), open =>
        {
            handed = open.Key;
            return StandingConfirmationTests.Opened(open);
        });
        return (change, handed);
    }

    private static AppointmentChange TurnOff(StandingConfirmation standing, string scope) =>
        standing.Appoint(Supervising(scope, on: false), _ => throw new InvalidOperationException("a dismissal opens no tab"));

    private static string GenerationOf(StandingConfirmation standing, string repository) =>
        standing.Admit(repository).Generation ?? throw new InvalidOperationException("not admitted");

    /// <summary>An answer's payload for <paramref name="session"/>, proved by <paramref name="key"/> over <paramref name="nonce"/>.</summary>
    private static JsonElement Answer(string key, string nonce, string generation, string session = "s1")
    {
        SupervisorReason[] reasons = [new("The ticket says monthly.", "the quest's line 2")];
        var proof = SupervisorProof.Prove(key, nonce, SupervisorProof.Canonical(session, "q-1", generation, "Monthly", reasons))!;
        var form = new SupervisorForm(session, "q-1", generation, proof, "Monthly", reasons);
        return JsonDocument.Parse(JsonSerializer.Serialize(form.Payload())).RootElement.Clone();
    }

    private static RoleJudgement JudgeAnswer(StandingConfirmation standing, string key, string generation)
    {
        var nonce = SupervisorProof.Nonce();
        return standing.JudgeRole(DriverModule.Name, SupervisorRoutes.Answer, Answer(key, nonce, generation), null, nonce);
    }

    private static ParkQuestion Asked(string session) =>
        new("q-1", session, "Which report?", [new("Monthly", "I write it."), new("Weekly", "I write that.")], 1, null, At.AddMinutes(-1));

    private static SupervisedPark Park(string repository, string session) => new(repository, true, true, Asked(session), false, false, false);

    private static SupervisorAnswer Answering(string session, string generation) =>
        new(session, "q-1", generation, "Monthly", [new("The ticket says monthly.", "line 2")], new("terminal", "pwsh"));

    private sealed class Ledger
    {
        public List<string> TakenBack { get; } = [];

        public Task<bool> TakeBack(string session, string word, CancellationToken ct)
        {
            lock (TakenBack) TakenBack.Add(word);
            return Task.FromResult(true);
        }

        public Task<SupervisorSaid> Say(string session, string decision, string question, string choice, string key, CancellationToken ct) =>
            Task.FromResult(new SupervisorSaid("w-" + decision, null));
    }

    [Fact]
    public void A_workspace_appointment_covers_every_repository_the_registry_places_in_it_a_later_one_included_and_nothing_else()
    {
        var standing = Judge();
        var (change, _) = Appoint(standing, RoleScopes.Workspace(Workspace));
        Assert.True(change.Answer.Done, change.Answer.Message);

        var generation = GenerationOf(standing, Engine);
        Assert.Equal(generation, GenerationOf(standing, Atlas));
        Assert.Equal(SupervisorWhy.NotAppointed, standing.Admit(Outside).Why);
        Assert.Equal(SupervisorWhy.NotAppointed, standing.Admit("unplaced").Why);

        // A repository the registry places in the workspace later is covered from the next judgement on, with no new appointment.
        _placed["later"] = Workspace;
        Assert.Equal(generation, GenerationOf(standing, "later"));

        // The row reads the workspace and the window, never a repository.
        var row = Assert.Single(standing.Now().Appointments!);
        Assert.Equal((RoleScopes.Workspace(Workspace), Workspace, (string?)null, Appointment.DefaultWindow, false),
            (row.Scope, row.Workspace, row.Repository, row.Window, row.Dormant));

        // Its holder's answer is judged by the generation alone; the park's repository is checked against the effective appointment.
        Assert.Null(standing.Covers(generation, Engine));
        Assert.Null(standing.Covers(generation, "later"));
        Assert.Equal(SupervisorWhy.NotAppointed, standing.Covers(generation, Outside));
    }

    [Fact]
    public void A_workspace_nothing_here_drives_is_refused_and_keeps_nothing()
    {
        var standing = Judge();
        var opened = 0;
        var refused = standing.Appoint(Supervising(RoleScopes.Workspace("elsewhere")), open => { opened++; return StandingConfirmationTests.Opened(open); });

        Assert.Equal(StandingConfirmation.UnknownWorkspace, refused.Answer.Why);
        Assert.Equal(0, opened);
        Assert.Empty(standing.Now().Appointments!);
        Assert.False(File.Exists(standing.RecordPath));
    }

    /// <summary>§6.3, §12: the window the owner sets, 1 to 60 minutes, never 0; five where none is set; the steward's has none.</summary>
    [Fact]
    public void The_overrule_window_is_set_at_the_appointment_from_one_to_sixty_minutes_and_written_beside_it()
    {
        var standing = Judge(steward: true);
        foreach (var refused in new long[] { 0, -1, 61, 600 })
        {
            Assert.Equal(StandingConfirmation.WindowRefused, Appoint(standing, RoleScopes.Repository(Engine), refused).Change.Answer.Why);
        }

        Assert.Empty(standing.Now().Appointments!);
        Assert.Equal(StandingConfirmation.WindowRefused,
            standing.Appoint(new AppointRequest(Roles.Steward, RoleScopes.Machine, RoleHolders.Terminal, true, "own", Window: 10), StandingConfirmationTests.Opened).Answer.Why);

        Assert.True(Appoint(standing, RoleScopes.Repository(Engine), 12).Change.Answer.Done);
        Assert.Equal(TimeSpan.FromMinutes(12), standing.Admit(Engine).Window);
        Assert.True(Appoint(standing, RoleScopes.Workspace(Workspace), 60).Change.Answer.Done);
        Assert.Equal(TimeSpan.FromMinutes(60), standing.Admit(Atlas).Window);
        Assert.True(Appoint(standing, RoleScopes.Repository(Outside), 1).Change.Answer.Done);
        Assert.Equal(TimeSpan.FromMinutes(1), standing.Admit(Outside).Window);

        // Written beside each appointment; read back at a start as it was written, and dormant there until Appoint again.
        using (var document = JsonDocument.Parse(File.ReadAllText(standing.RecordPath)))
        {
            Assert.Equal([12, 60, 1], document.RootElement.GetProperty("appointments").EnumerateArray().Select(each => each.GetProperty("window").GetInt32()));
        }

        var next = Judge();
        Assert.Equal([12, 60, 1], next.Now().Appointments!.Select(each => each.Window));
        Assert.All(next.Now().Appointments!, each => Assert.True(each.Dormant));
        Assert.Equal(SupervisorWhy.Dormant, next.Admit(Engine).Why);

        // Appoint again sets its own: none named is five, whatever the found record said.
        Assert.True(Appoint(next, RoleScopes.Repository(Engine)).Change.Answer.Done);
        Assert.Equal(TimeSpan.FromMinutes(Appointment.DefaultWindow), next.Admit(Engine).Window);
    }

    /// <summary>A window written outside the bounds, by a hand or an agent, is read as five: it only shows, and never reaches a decision.</summary>
    [Fact]
    public void A_window_written_outside_the_bounds_is_read_as_five()
    {
        var standing = Judge();
        Appoint(standing, RoleScopes.Workspace(Workspace), 30);
        var written = File.ReadAllText(standing.RecordPath);
        var text = System.Text.RegularExpressions.Regex.Replace(written, "\"window\"\\s*:\\s*30", "\"window\": 0");
        Assert.NotEqual(written, text);
        File.WriteAllText(standing.RecordPath, text);

        var found = Assert.Single(Judge().Now().Appointments!);
        Assert.Equal(Appointment.DefaultWindow, found.Window);
    }

    /// <summary>§5: the repository's own appointment wins over its workspace's; the workspace's stands for its other repositories.</summary>
    [Fact]
    public void A_repositorys_own_appointment_wins_over_its_workspaces_and_turning_either_off_ends_only_its_own()
    {
        var standing = Judge();
        Appoint(standing, RoleScopes.Workspace(Workspace), 20);
        var workspace = GenerationOf(standing, Engine);

        var (own, _) = Appoint(standing, RoleScopes.Repository(Engine), 3);
        Assert.True(own.Answer.Done);
        Assert.Empty(own.Ended);
        Assert.Equal([(workspace, Engine)], own.Narrowed);
        var repository = GenerationOf(standing, Engine);
        Assert.NotEqual(workspace, repository);
        Assert.Equal(TimeSpan.FromMinutes(3), standing.Admit(Engine).Window);
        Assert.Equal((workspace, TimeSpan.FromMinutes(20)), (GenerationOf(standing, Atlas), standing.Admit(Atlas).Window));

        // The workspace's holder answering for the repository is told its appointment changed there; for another it still covers.
        Assert.Equal(SupervisorWhy.AppointmentChanged, standing.Covers(workspace, Engine));
        Assert.Null(standing.Covers(workspace, Atlas));
        Assert.Null(standing.Covers(repository, Engine));
        Assert.Equal(SupervisorWhy.NotAppointed, standing.Covers(repository, Atlas));

        // A workspace's appointment made over a repository's own narrows nothing: the repository's still wins.
        var (again, _) = Appoint(standing, RoleScopes.Workspace(Workspace), 20);
        Assert.Equal([workspace], again.Ended);
        Assert.Empty(again.Narrowed);
        Assert.Equal(repository, GenerationOf(standing, Engine));
        var renewed = GenerationOf(standing, Atlas);

        // Turning the repository's own off ends its generation alone, and the workspace's covers the repository again.
        var off = TurnOff(standing, RoleScopes.Repository(Engine));
        Assert.Equal([repository], off.Ended);
        Assert.Equal(renewed, GenerationOf(standing, Engine));

        // Turning the workspace's off ends its generation, every repository of it uncovered; a repository's own elsewhere stands.
        Appoint(standing, RoleScopes.Repository(Outside));
        var outside = GenerationOf(standing, Outside);
        var gone = TurnOff(standing, RoleScopes.Workspace(Workspace));
        Assert.Equal([renewed], gone.Ended);
        Assert.Equal(SupervisorWhy.NotAppointed, standing.Admit(Engine).Why);
        Assert.Equal(SupervisorWhy.NotAppointed, standing.Admit(Atlas).Why);
        Assert.Equal(outside, GenerationOf(standing, Outside));
    }

    /// <summary>
    /// §5: a holder's change is a new appointment with a new key, so the old holder's late answer is refused <c>appointment-changed</c>,
    /// and so is a workspace holder's answer made after a repository's own appointment replaced its workspace's there.
    /// </summary>
    [Fact]
    public void A_late_answer_of_an_earlier_holder_or_generation_is_refused_appointment_changed()
    {
        var standing = Judge();
        var (_, first) = Appoint(standing, RoleScopes.Workspace(Workspace));
        var old = GenerationOf(standing, Atlas);
        Assert.True(JudgeAnswer(standing, first!, old).Covered);

        // Appointed again: a new holder with a new key and a new generation.
        var (_, second) = Appoint(standing, RoleScopes.Workspace(Workspace));
        var renewed = GenerationOf(standing, Atlas);
        Assert.NotEqual(old, renewed);
        Assert.Equal(SupervisorWhy.AppointmentChanged, JudgeAnswer(standing, first!, old).Why);
        Assert.Equal(SupervisorWhy.NotHolder, JudgeAnswer(standing, first!, renewed).Why);
        var covered = JudgeAnswer(standing, second!, renewed);
        Assert.True(covered.Covered);
        Assert.Null(covered.Repository);

        // The workspace's holder is still the holder of its generation, but the repository's own now answers for that repository.
        Appoint(standing, RoleScopes.Repository(Engine));
        Assert.True(JudgeAnswer(standing, second!, renewed).Covered);
        Assert.Equal(SupervisorWhy.AppointmentChanged, standing.Covers(renewed, Engine));
    }

    /// <summary>
    /// §5, §2.3: a repository's appointment made while a decision under its workspace's is held makes that decision <c>stale</c>, by the
    /// sweep after the judge's step; a held decision of the workspace's other repository stays held, and a released one's word is taken
    /// back. A keeping, a release or an admission under the workspace's generation for the repository is refused after it.
    /// </summary>
    [Fact]
    public async Task A_repositorys_appointment_made_while_a_workspace_decision_is_held_makes_it_stale()
    {
        var standing = Judge();
        Appoint(standing, RoleScopes.Workspace(Workspace), 10);
        var workspace = GenerationOf(standing, Engine);
        var decisions = new QuestionSupervisorDecisions(_home, standing);
        var ledger = new Ledger();

        var held = await decisions.KeepAsync(Answering("e-held", workspace), Park(Engine, "e-held"), At);
        Assert.Equal((true, At.AddMinutes(10)), (held.Kept, held.GoesOnAt));
        Assert.True((await decisions.KeepAsync(Answering("e-released", workspace), Park(Engine, "e-released"), At.AddMinutes(-20))).Kept);
        Assert.Equal([SupervisorState.Released], await decisions.ReleaseAsync("e-released", ledger.Say, At));
        var word = decisions.LiveOf("e-released").Single().Word!;
        Assert.True((await decisions.KeepAsync(Answering("a-held", workspace), Park(Atlas, "a-held"), At)).Kept);

        var (own, _) = Appoint(standing, RoleScopes.Repository(Engine));
        foreach (var (generation, repository) in own.Narrowed)
        {
            await decisions.RevokedAsync(generation, ledger.TakeBack, At.AddMinutes(1), repository: repository);
        }

        Assert.Equal((SupervisorState.Stale, SupervisorWhy.AppointmentChanged),
            (decisions.LiveOf("e-held").Single().State, decisions.LiveOf("e-held").Single().Why));
        Assert.Equal(SupervisorState.Stale, decisions.LiveOf("e-released").Single().State);
        Assert.Equal([word], ledger.TakenBack);
        Assert.Equal(SupervisorState.Held, decisions.LiveOf("a-held").Single().State);

        // A late keeping under the workspace's generation for the repository is refused; for the other repository it is kept.
        var late = await decisions.KeepAsync(Answering("e-late", workspace), Park(Engine, "e-late"), At.AddMinutes(2));
        Assert.Equal((false, SupervisorWhy.AppointmentChanged), (late.Kept, late.Why));
        Assert.True((await decisions.KeepAsync(Answering("a-late", workspace), Park(Atlas, "a-late"), At.AddMinutes(2))).Kept);

        // The other repository's decision goes on at its window, under the workspace's still effective generation.
        Assert.Equal([SupervisorState.Released], await decisions.ReleaseAsync("a-held", ledger.Say, At.AddMinutes(10)));
    }
}
