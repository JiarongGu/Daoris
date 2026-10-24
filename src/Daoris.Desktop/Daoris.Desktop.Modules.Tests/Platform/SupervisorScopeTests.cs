using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// SUPERVISOR1e (D208; the supervisor design §5, §6.3 and §11 row e): a workspace's appointment and the overrule window. A workspace's
/// appointment admits every repository of the workspace, one registered later included, and a repository's own appointment wins over
/// it; the window is set at the appointment, 1 to 60 minutes, written with the supervisor's record and never the steward's; a holder's
/// change refuses the old holder's late act; a repository's appointment made while a decision under its workspace's is held makes that
/// decision stale and leaves its neighbours; and each revocation drops only the decisions made under it.
/// </summary>
public sealed class SupervisorScopeTests : Bridge
{
    private const string Games = "games";
    private const string Engine = "engine";
    private const string Other = "other";

    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-10-24T09:00:00Z");

    /// <summary>The registry as the loop hands it: <see cref="Engine"/> and <see cref="Other"/> in <see cref="Games"/>, <c>lone</c> in <c>solo</c>.</summary>
    private readonly Dictionary<string, string> _registry = new(StringComparer.OrdinalIgnoreCase)
    {
        [Engine] = Games, [Other] = Games, ["lone"] = "solo",
    };

    private StandingConfirmation Judge(bool steward = false)
    {
        var standing = new StandingConfirmation(
            Home, null, new StandingConfirmationTests.Placed(StandingPlace.Outside), offered: true, supervisor: true, steward: steward,
            walk: new StandingConfirmationTests.Walked())
        {
            Drives = _registry.ContainsKey,
            WorkspaceOf = repository => _registry.GetValueOrDefault(repository),
            HasWorkspace = workspace => _registry.Values.Contains(workspace, StringComparer.OrdinalIgnoreCase),
        };
        standing.Load();
        return standing;
    }

    private static AppointRequest Workspace(string workspace = Games, bool on = true, int? window = null) =>
        new(Roles.Supervisor, RoleScopes.Workspace(workspace), RoleHolders.Terminal, on, "own", Window: window);

    private static AppointRequest Repository(string repository = Engine, bool on = true, int? window = null) =>
        new(Roles.Supervisor, RoleScopes.Repository(repository), RoleHolders.Terminal, on, "own", Window: window);

    private static (AppointmentChange Change, string? Key) Appoint(StandingConfirmation standing, AppointRequest request)
    {
        string? handed = null;
        var change = standing.Appoint(request, open =>
        {
            handed = open.Key;
            return StandingConfirmationTests.Opened(open);
        });
        return (change, handed);
    }

    private static string GenerationOf(StandingConfirmation standing, string repository) =>
        standing.Admit(repository).Generation ?? throw new InvalidOperationException("not admitted");

    [Fact]
    public void A_workspace_s_appointment_admits_every_repository_of_it_one_registered_later_included_and_a_repository_s_own_wins()
    {
        var standing = Judge();
        var (made, _) = Appoint(standing, Workspace(window: 12));
        Assert.True(made.Answer.Done, made.Answer.Message);
        var workspace = GenerationOf(standing, Engine);

        Assert.Equal((workspace, TimeSpan.FromMinutes(12)), (standing.Admit(Other).Generation, standing.Admit(Other).Window));
        Assert.Equal(SupervisorWhy.NotAppointed, standing.Admit("lone").Why);
        // §5: a repository added to the workspace later is covered as it is judged, never by a list kept at the appointment.
        _registry["later"] = Games;
        Assert.Equal(workspace, standing.Admit("later").Generation);

        var (own, _) = Appoint(standing, Repository());
        Assert.True(own.Answer.Done, own.Answer.Message);
        // The workspace's appointment stands for its other repositories; for this one it is narrowed, and swept after the step.
        Assert.Empty(own.Ended);
        var narrowed = Assert.Single(own.Narrowed);
        Assert.Equal((workspace, Engine), (narrowed.Generation, narrowed.Repository));
        var repository = GenerationOf(standing, Engine);
        Assert.NotEqual(workspace, repository);
        Assert.Equal(QuestionSupervisorDecisions.Window, standing.Admit(Engine).Window);
        Assert.Equal(workspace, GenerationOf(standing, Other));

        var rows = standing.Now().Appointments!;
        var shared = Assert.Single(rows, each => each.Workspace == Games);
        Assert.Equal((null, 12, RoleScopes.Workspace(Games)), (shared.Repository, shared.Window, shared.Scope));
        Assert.Equal(5, Assert.Single(rows, each => each.Repository == Engine).Window);

        // Each revocation drops only its own: the repository's ends its generation and the workspace's covers it again.
        var mine = Appoint(standing, Repository(on: false)).Change;
        Assert.Equal([repository], mine.Ended);
        Assert.Equal(workspace, GenerationOf(standing, Engine));
        var theirs = Appoint(standing, Workspace(on: false)).Change;
        Assert.Equal([workspace], theirs.Ended);
        Assert.Equal(SupervisorWhy.NotAppointed, standing.Admit(Other).Why);
    }

    /// <summary>The loop's memory of a registry read, which the judge reads: each repository's workspace, names compared as the registry's.</summary>
    [Fact]
    public void A_registry_read_places_each_repository_in_its_workspace()
    {
        var read = RepositoryWorkspaces.Of([new RepoView(Engine, true, null, Games), new RepoView("Lone", true, null, "solo"), new RepoView(Engine, true, null, "again")]);

        Assert.Equal((Games, "solo", null), (read.WorkspaceOf("ENGINE"), read.WorkspaceOf("lone"), read.WorkspaceOf("missing")));
        Assert.True(read.Names("GAMES"));
        Assert.False(read.Names("again"));
        Assert.Null(RepositoryWorkspaces.Empty.WorkspaceOf(Engine));
    }

    [Fact]
    public void A_repository_s_appointment_made_first_is_not_narrowed_by_its_workspace_s_and_wins_while_dormant()
    {
        var standing = Judge();
        Appoint(standing, Repository());
        var repository = GenerationOf(standing, Engine);
        var (made, _) = Appoint(standing, Workspace());
        Assert.Empty(made.Narrowed);
        Assert.Equal(repository, GenerationOf(standing, Engine));

        // Found at the next start, both are dormant: the repository's own still answers for it, carrying no authority.
        var next = Judge();
        Assert.Equal(SupervisorWhy.Dormant, next.Admit(Engine).Why);
        Assert.Equal(SupervisorWhy.Dormant, next.Admit(Other).Why);
        Assert.All(next.Now().Appointments!, each => Assert.True(each.Dormant));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(61)]
    [InlineData(-5)]
    public void A_window_outside_one_to_sixty_minutes_keeps_nothing_and_starts_no_terminal(int minutes)
    {
        var standing = Judge();
        var started = 0;
        var change = standing.Appoint(Workspace(window: minutes), open =>
        {
            started++;
            return StandingConfirmationTests.Opened(open);
        });

        Assert.Equal((false, StandingConfirmation.WindowWhy), (change.Answer.Done, change.Answer.Why));
        Assert.Equal(0, started);
        Assert.False(File.Exists(standing.RecordPath));
        Assert.Equal(StandingConfirmation.WindowWhy, Appoint(standing, Repository(window: minutes)).Change.Answer.Why);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(60)]
    [InlineData(null)]
    public void The_window_is_set_at_the_appointment_written_with_its_scope_and_read_back_at_the_next_start(int? minutes)
    {
        var standing = Judge();
        Assert.True(Appoint(standing, Workspace(window: minutes)).Change.Answer.Done);
        var kept = minutes ?? 5;
        Assert.Equal(TimeSpan.FromMinutes(kept), standing.Admit(Engine).Window);

        using (var document = JsonDocument.Parse(File.ReadAllText(standing.RecordPath)))
        {
            var written = Assert.Single(document.RootElement.GetProperty("appointments").EnumerateArray());
            Assert.Equal(
                ["role", "scope", "holder", "generation", "since", "acts", "setup", "window"],
                written.EnumerateObject().Select(each => each.Name));
            Assert.Equal((RoleScopes.Workspace(Games), kept), (written.GetProperty("scope").GetString(), written.GetProperty("window").GetInt32()));
        }

        var found = Assert.Single(Judge().Now().Appointments!);
        Assert.Equal((true, Games, kept), (found.Dormant, found.Workspace, found.Window));
    }

    [Fact]
    public void A_window_a_file_says_outside_one_to_sixty_is_not_read()
    {
        var standing = Judge();
        Appoint(standing, Workspace(window: 30));
        var text = File.ReadAllText(standing.RecordPath);
        var tampered = System.Text.RegularExpressions.Regex.Replace(text, "\"window\":\\s*30", "\"window\": 0");
        Assert.NotEqual(text, tampered);
        File.WriteAllText(standing.RecordPath, tampered);

        var found = Assert.Single(Judge().Now().Appointments!);
        Assert.Null(found.Window);
    }

    [Fact]
    public void The_steward_s_record_keeps_no_window_and_a_workspace_is_the_supervisor_s_scope_alone()
    {
        var standing = Judge(steward: true);
        var windowed = Appoint(standing, RoleKit.Stewarding() with { Window = 10 }).Change;
        Assert.Equal((false, StandingConfirmation.WindowWhy), (windowed.Answer.Done, windowed.Answer.Why));
        var scoped = Appoint(standing, RoleKit.Stewarding() with { Scope = RoleScopes.Workspace(Games) }).Change;
        Assert.Equal((false, StandingConfirmation.Scope), (scoped.Answer.Done, scoped.Answer.Why));

        Assert.True(Appoint(standing, RoleKit.Stewarding()).Change.Answer.Done);
        using var document = JsonDocument.Parse(File.ReadAllText(standing.RecordPath));
        var steward = Assert.Single(document.RootElement.GetProperty("appointments").EnumerateArray());
        Assert.False(steward.TryGetProperty("window", out _));
        Assert.Null(Assert.Single(standing.Now().Appointments!).Window);
    }

    [Fact]
    public void A_workspace_the_registry_does_not_name_is_refused_and_nothing_starts()
    {
        var standing = Judge();
        var started = 0;
        var change = standing.Appoint(Workspace("elsewhere"), open =>
        {
            started++;
            return StandingConfirmationTests.Opened(open);
        });
        Assert.Equal((false, StandingConfirmation.UnknownWorkspace), (change.Answer.Done, change.Answer.Why));
        Assert.Equal(0, started);
    }

    /// <summary>§5: a holder's change is a new appointment with a new key, so the old holder's late act is refused.</summary>
    [Fact]
    public void A_holder_change_of_the_workspace_s_appointment_refuses_the_old_holder_s_late_act()
    {
        var standing = Judge();
        var (_, first) = Appoint(standing, Workspace());
        var old = GenerationOf(standing, Engine);
        var (renewed, second) = Appoint(standing, Workspace());
        Assert.Equal([old], renewed.Ended);
        var generation = GenerationOf(standing, Other);
        var nonce = SupervisorProof.Nonce();

        Assert.Equal(SupervisorWhy.AppointmentChanged, standing.JudgeRole(DriverModule.Name, SupervisorRoutes.Answer, Answer(first!, nonce, old), null, nonce).Why);
        var covered = standing.JudgeRole(DriverModule.Name, SupervisorRoutes.Answer, Answer(second!, nonce, generation), null, nonce);
        Assert.True(covered.Covered);
        // A workspace's act names no repository: the door reads the session's and asks the admission which appointment is effective there.
        Assert.Equal((null, generation), (covered.Repository, covered.Generation));
    }

    private static JsonElement Answer(string key, string nonce, string generation)
    {
        SupervisorReason[] reasons = [new("The ticket says monthly.", "the quest's line 2")];
        var proof = SupervisorProof.Prove(key, nonce, SupervisorProof.Canonical("s1", "q-1", generation, "Monthly", reasons))!;
        return JsonDocument.Parse(JsonSerializer.Serialize(new SupervisorForm("s1", "q-1", generation, proof, "Monthly", reasons).Payload())).RootElement.Clone();
    }

    // The decisions under the real judge (§5, §6.3): the window from the appointment, the narrowing's sweep, a late keeping, and both revocations.

    private static ParkQuestion Asked(string session) =>
        new($"q-{session}", session, "Which report?", [new("Monthly", "I write it."), new("Weekly", "I write that.")], 1, null, At);

    private static SupervisedPark Park(string session, string repository) => new(repository, true, true, Asked(session), false, false, false);

    private static SupervisorAnswer Answering(string session, string generation) =>
        new(session, $"q-{session}", generation, "Monthly", [new("The ticket says monthly.", "line 2")], new("terminal", "pwsh"));

    private readonly List<string> _taken = [];

    private Task<bool> TakeBack(string _, string word, CancellationToken __)
    {
        lock (_taken) _taken.Add(word);
        return Task.FromResult(true);
    }

    private static Task<SupervisorSaid> Say(string s, string d, string q, string c, string k, CancellationToken ct) =>
        Task.FromResult(new SupervisorSaid("w-" + d, null));

    [Fact]
    public async Task A_decision_is_held_for_the_window_its_appointment_set()
    {
        var standing = Judge();
        Appoint(standing, Workspace(window: 12));
        var decisions = new QuestionSupervisorDecisions(Home, standing);

        var kept = await decisions.KeepAsync(Answering("s-engine", GenerationOf(standing, Engine)), Park("s-engine", Engine), At);

        Assert.True(kept.Kept, kept.Why);
        Assert.Equal(At.AddMinutes(12), kept.GoesOnAt);
        Assert.Empty(await decisions.ReleaseAsync("s-engine", Say, At.AddMinutes(11)));
        Assert.Equal([SupervisorState.Released], await decisions.ReleaseAsync("s-engine", Say, At.AddMinutes(12)));
    }

    [Fact]
    public async Task A_repository_s_appointment_made_while_a_decision_under_its_workspace_s_is_held_makes_it_stale_and_leaves_its_neighbours()
    {
        var standing = Judge();
        Appoint(standing, Workspace());
        var workspace = GenerationOf(standing, Engine);
        var decisions = new QuestionSupervisorDecisions(Home, standing);
        Assert.True((await decisions.KeepAsync(Answering("s-engine", workspace), Park("s-engine", Engine), At)).Kept);
        Assert.True((await decisions.KeepAsync(Answering("s-other", workspace), Park("s-other", Other), At)).Kept);

        var (made, _) = Appoint(standing, Repository());
        foreach (var each in made.Narrowed) await decisions.NarrowedAsync(each.Generation, each.Repository, TakeBack, At.AddMinutes(1));

        var swept = Assert.Single(decisions.LiveOf("s-engine"));
        Assert.Equal((SupervisorState.Stale, SupervisorWhy.AppointmentChanged), (swept.State, swept.Why));
        Assert.Equal(SupervisorState.Held, Assert.Single(decisions.LiveOf("s-other")).State);

        // A late result of the workspace's holder for this repository is refused at the keeping; its neighbour is released as held.
        var late = await decisions.KeepAsync(Answering("s-engine", workspace) with { Question = "q-late" }, Park("s-engine", Engine) with { Filed = Asked("s-engine") with { Id = "q-late" } }, At.AddMinutes(2));
        Assert.Equal((false, SupervisorWhy.AppointmentChanged), (late.Kept, late.Why));
        Assert.Equal([SupervisorState.Released], await decisions.ReleaseAsync("s-other", Say, At.AddMinutes(5)));
    }

    [Fact]
    public async Task A_held_workspace_decision_whose_repository_was_appointed_since_is_stale_at_its_release_without_the_sweep()
    {
        var standing = Judge();
        Appoint(standing, Workspace());
        var decisions = new QuestionSupervisorDecisions(Home, standing);
        Assert.True((await decisions.KeepAsync(Answering("s-engine", GenerationOf(standing, Engine)), Park("s-engine", Engine), At)).Kept);

        Appoint(standing, Repository());

        Assert.Equal([SupervisorState.Stale], await decisions.ReleaseAsync("s-engine", Say, At.AddMinutes(5)));
    }

    [Fact]
    public async Task Each_revocation_drops_only_the_decisions_made_under_it()
    {
        var standing = Judge();
        Appoint(standing, Workspace());
        Appoint(standing, Repository());
        var decisions = new QuestionSupervisorDecisions(Home, standing);
        Assert.True((await decisions.KeepAsync(Answering("s-engine", GenerationOf(standing, Engine)), Park("s-engine", Engine), At)).Kept);
        Assert.True((await decisions.KeepAsync(Answering("s-other", GenerationOf(standing, Other)), Park("s-other", Other), At)).Kept);

        foreach (var ended in Appoint(standing, Workspace(on: false)).Change.Ended) await decisions.RevokedAsync(ended, TakeBack, At.AddMinutes(1));
        Assert.Equal(SupervisorState.Stale, Assert.Single(decisions.LiveOf("s-other")).State);
        Assert.Equal(SupervisorState.Held, Assert.Single(decisions.LiveOf("s-engine")).State);

        foreach (var ended in Appoint(standing, Repository(on: false)).Change.Ended) await decisions.RevokedAsync(ended, TakeBack, At.AddMinutes(2));
        Assert.Equal(SupervisorState.Stale, Assert.Single(decisions.LiveOf("s-engine")).State);
    }
}
