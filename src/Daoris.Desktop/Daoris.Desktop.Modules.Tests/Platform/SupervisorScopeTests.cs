using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// SUPERVISOR1e under D233 point 2: the supervisor is a catalogue role, and the one that acts for a repository is the most specific
/// scope's that covers it, as a work's role is found: the repository's own, else its workspace's, else the machine's. Its window is the
/// role's, 1 to 60 minutes, five where none is kept; a role's change ends its terminal's hold, and a decision kept under a workspace's
/// supervisor goes stale once a repository's own acts there.
/// </summary>
/// <remarks>
/// The judge alone, as <see cref="StandingConfirmationTests"/> holds it, with a scratch home of its own and no environment set: the
/// registry's placement a dictionary, the catalogue a list (<see cref="RoleKit.Staff"/>), the holder's tab a stand-in that starts nothing.
/// </remarks>
public sealed class SupervisorScopeTests : IDisposable
{
    private const string Workspace = "games";
    private const string Engine = "engine";
    private const string Atlas = "atlas";
    private const string Outside = "outside";
    private const string Games = "supervisor-games";
    private const string Own = "supervisor-engine";

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

    private StandingConfirmation Judge(params RoleRecord[] staffed)
    {
        var standing = new StandingConfirmation(_home, null, new StandingConfirmationTests.Placed(StandingPlace.Outside),
            walk: new StandingConfirmationTests.Walked())
        {
            WorkspaceOf = repository => _placed.GetValueOrDefault(repository),
        };
        return standing.Staff(staffed);
    }

    /// <summary>A role's terminal started, the tab a stand-in that keeps the key it was handed.</summary>
    private static (HoldChange Change, string? Key) Start(StandingConfirmation standing, string role) =>
        RoleKit.Start(standing, new HoldRequest(role, "own"));

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
    public void A_workspace_supervisor_covers_every_repository_the_registry_places_in_it_a_later_one_included_and_nothing_else()
    {
        var standing = Judge(RoleKit.Supervisor(Games, RoleKit.Workspace(Workspace)));
        var (change, _) = Start(standing, Games);
        Assert.True(change.Answer.Done, change.Answer.Message);

        var generation = GenerationOf(standing, Engine);
        Assert.Equal(generation, GenerationOf(standing, Atlas));
        // Outside the workspace the machine's supervisor acts, held by your rules alone.
        Assert.Equal(SupervisorWhy.NotStaffed, standing.Admit(Outside).Why);
        Assert.Equal(SupervisorWhy.NotStaffed, standing.Admit("unplaced").Why);

        // A repository the registry places in the workspace later is covered from the next judgement on, with no new role.
        _placed["later"] = Workspace;
        Assert.Equal(generation, GenerationOf(standing, "later"));

        // The hold reads the role and its kind, never a repository.
        var hold = Assert.Single(standing.Now().Holds!);
        Assert.Equal((Games, AppointedRoles.Supervisor), (hold.Role, hold.Kind));

        // Its holder's answer is judged by the generation alone; the park's repository is checked against the effective supervisor.
        Assert.Null(standing.Covers(generation, Engine));
        Assert.Null(standing.Covers(generation, "later"));
        Assert.Equal(SupervisorWhy.NotStaffed, standing.Covers(generation, Outside));
    }

    /// <summary>The machine's supervisor held by the terminal covers every repository no narrower supervisor covers.</summary>
    [Fact]
    public void The_machines_supervisor_held_by_the_terminal_covers_what_no_narrower_one_does()
    {
        var standing = Judge(RoleKit.Supervisor(), RoleKit.Supervisor(Own, RoleKit.Repository(Engine), RoleHolders.Rules));
        Start(standing, AppointedRoles.Supervisor);

        Assert.NotNull(standing.Admit(Outside).Generation);
        Assert.NotNull(standing.Admit(Atlas).Generation);
        // The repository's own supervisor wins there, and your rules alone hold it.
        Assert.Equal(SupervisorWhy.NotStaffed, standing.Admit(Engine).Why);
    }

    /// <summary>§6.3, §12: the window is the role's, 1 to 60 minutes, five where none is kept, read at each admission.</summary>
    [Fact]
    public void The_overrule_window_is_the_supervisor_roles_and_a_change_of_it_reaches_the_next_admission()
    {
        var standing = Judge(RoleKit.Supervisor(Own, RoleKit.Repository(Engine), window: 12), RoleKit.Supervisor(Games, RoleKit.Workspace(Workspace), window: 60),
            RoleKit.Supervisor("supervisor-outside", RoleKit.Repository(Outside), window: 1));
        Start(standing, Own);
        Assert.True(RoleKit.Start(standing, new HoldRequest(Games, Assert.Single(standing.Now().Holders!).Id)).Change.Answer.Done);
        Assert.True(RoleKit.Start(standing, new HoldRequest("supervisor-outside", Assert.Single(standing.Now().Holders!).Id)).Change.Answer.Done);

        Assert.Equal(TimeSpan.FromMinutes(12), standing.Admit(Engine).Window);
        Assert.Equal(TimeSpan.FromMinutes(60), standing.Admit(Atlas).Window);
        Assert.Equal(TimeSpan.FromMinutes(1), standing.Admit(Outside).Window);

        standing.Staff(RoleKit.Supervisor(Own, RoleKit.Repository(Engine)));
        Assert.Empty(standing.RolesChanged());
        Assert.Equal(TimeSpan.FromMinutes(RoleHold.DefaultWindow), standing.Admit(Engine).Window);

        // A restart starts no terminal: the role is staffed by the terminal and not started.
        var next = Judge(RoleKit.Supervisor(Own, RoleKit.Repository(Engine), window: 12));
        Assert.Equal(SupervisorWhy.NotStarted, next.Admit(Engine).Why);
    }

    /// <summary>§5: the repository's own supervisor wins over its workspace's; the workspace's stands for its other repositories.</summary>
    [Fact]
    public void A_repositorys_own_supervisor_wins_over_its_workspaces_and_turning_either_off_ends_only_its_own()
    {
        var standing = Judge(RoleKit.Supervisor(Games, RoleKit.Workspace(Workspace), window: 20));
        Start(standing, Games);
        var workspace = GenerationOf(standing, Engine);

        standing.Staff(RoleKit.Supervisor(Own, RoleKit.Repository(Engine), window: 3));
        var holder = Assert.Single(standing.Now().Holders!).Id;
        var (own, _) = RoleKit.Start(standing, new HoldRequest(Own, holder));
        Assert.True(own.Answer.Done);
        Assert.Empty(own.Ended);
        var repository = GenerationOf(standing, Engine);
        Assert.NotEqual(workspace, repository);
        Assert.Equal(TimeSpan.FromMinutes(3), standing.Admit(Engine).Window);
        Assert.Equal((workspace, TimeSpan.FromMinutes(20)), (GenerationOf(standing, Atlas), standing.Admit(Atlas).Window));

        // The workspace's holder answering for the repository is told its role changed there; for another it still covers.
        Assert.Equal(SupervisorWhy.RoleChanged, standing.Covers(workspace, Engine));
        Assert.Null(standing.Covers(workspace, Atlas));
        Assert.Null(standing.Covers(repository, Engine));
        Assert.Equal(SupervisorWhy.NotStaffed, standing.Covers(repository, Atlas));

        // The repository's own held by your rules alone ends its hold, and the workspace's still does not act there.
        standing.Staff(RoleKit.Supervisor(Own, RoleKit.Repository(Engine), RoleHolders.Rules));
        Assert.Equal([repository], standing.RolesChanged());
        Assert.Equal(SupervisorWhy.NotStaffed, standing.Admit(Engine).Why);
        Assert.Equal(workspace, GenerationOf(standing, Atlas));

        // The workspace's held by your rules alone ends its generation, every repository of it uncovered.
        standing.Staff(RoleKit.Supervisor(Games, RoleKit.Workspace(Workspace), RoleHolders.Rules));
        Assert.Equal([workspace], standing.RolesChanged());
        Assert.Equal(SupervisorWhy.NotStaffed, standing.Admit(Atlas).Why);
        Assert.Empty(standing.Now().Holders!);
    }

    /// <summary>
    /// §5: a terminal started again is a new hold with a new key, so the old holder's late answer is refused <c>role-changed</c>, and so is
    /// a workspace holder's answer for a repository whose own supervisor acts there now.
    /// </summary>
    [Fact]
    public void A_late_answer_of_an_earlier_holder_or_generation_is_refused_role_changed()
    {
        var standing = Judge(RoleKit.Supervisor(Games, RoleKit.Workspace(Workspace)));
        var (_, first) = Start(standing, Games);
        var old = GenerationOf(standing, Atlas);
        Assert.True(JudgeAnswer(standing, first!, old).Covered);

        // Started again in a terminal of its own: a new holder with a new key and a new generation.
        var (again, second) = Start(standing, Games);
        Assert.Equal([old], again.Ended);
        var renewed = GenerationOf(standing, Atlas);
        Assert.NotEqual(old, renewed);
        Assert.Equal(SupervisorWhy.RoleChanged, JudgeAnswer(standing, first!, old).Why);
        Assert.Equal(SupervisorWhy.NotHolder, JudgeAnswer(standing, first!, renewed).Why);
        var covered = JudgeAnswer(standing, second!, renewed);
        Assert.True(covered.Covered);
        Assert.Null(covered.Repository);

        // The workspace's holder is still the holder of its generation, but the repository's own now answers for that repository.
        standing.Staff(RoleKit.Supervisor(Own, RoleKit.Repository(Engine)));
        Start(standing, Own);
        Assert.True(JudgeAnswer(standing, second!, renewed).Covered);
        Assert.Equal(SupervisorWhy.RoleChanged, standing.Covers(renewed, Engine));
    }

    /// <summary>
    /// §5: a repository's own supervisor started while a decision under its workspace's is held makes that decision <c>stale</c> at its
    /// release, which asks the admission again; a held decision of the workspace's other repository goes on at its window.
    /// </summary>
    [Fact]
    public async Task A_repositorys_supervisor_started_while_a_workspace_decision_is_held_makes_it_stale()
    {
        var standing = Judge(RoleKit.Supervisor(Games, RoleKit.Workspace(Workspace), window: 10));
        Start(standing, Games);
        var workspace = GenerationOf(standing, Engine);
        var decisions = new QuestionSupervisorDecisions(_home, standing);
        var ledger = new Ledger();

        var held = await decisions.KeepAsync(Answering("e-held", workspace), Park(Engine, "e-held"), At);
        Assert.Equal((true, At.AddMinutes(10)), (held.Kept, held.GoesOnAt));
        Assert.True((await decisions.KeepAsync(Answering("a-held", workspace), Park(Atlas, "a-held"), At)).Kept);

        standing.Staff(RoleKit.Supervisor(Own, RoleKit.Repository(Engine)));
        Start(standing, Own);

        Assert.DoesNotContain(SupervisorState.Released, await decisions.ReleaseAsync("e-held", ledger.Say, At.AddMinutes(10)));
        Assert.Equal(SupervisorState.Stale, decisions.LiveOf("e-held").Single().State);

        // A late keeping under the workspace's generation for the repository is refused; for the other repository it is kept.
        var late = await decisions.KeepAsync(Answering("e-late", workspace), Park(Engine, "e-late"), At.AddMinutes(2));
        Assert.Equal((false, SupervisorWhy.RoleChanged), (late.Kept, late.Why));
        Assert.True((await decisions.KeepAsync(Answering("a-late", workspace), Park(Atlas, "a-late"), At.AddMinutes(2))).Kept);

        // The other repository's decision goes on at its window, under the workspace's still effective generation.
        Assert.Equal([SupervisorState.Released], await decisions.ReleaseAsync("a-held", ledger.Say, At.AddMinutes(10)));
    }
}
