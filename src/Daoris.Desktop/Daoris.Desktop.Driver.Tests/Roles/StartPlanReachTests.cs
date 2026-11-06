using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AGENTPROVIDER1 row u (the agent-provider design §11 row u, D212 §2.4): the plan's walk reads each choice's provider's <c>Reach</c> before
/// any account is asked, so a provider stated <c>Opened</c> is passed <c>not-opened</c> at a place not opened to it and Build's later choice
/// runs; the start holds only where it is the only choice. A brainstorm voice reads its workspace, Ask Daoris's room the machine's, and a
/// meeting, outside the plan, refuses the participant.
/// </summary>
/// <remarks>
/// Nothing here starts a process: each agent is present by a file look (<see cref="HarnessToolchain.ProbeByPresence"/>), as
/// <c>StartPlanTests</c>' are, and no spawn is reached, so a passed choice is passed before anything is sent to it.
/// </remarks>
public sealed class StartPlanReachTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "provider-reach-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly AdapterSet _adapters;

    private readonly HarnessRoster _roster;

    public StartPlanReachTests()
    {
        Directory.CreateDirectory(_home);
        var command = Path.Combine(_home, "agent-here");
        File.WriteAllText(command, "");
        foreach (var agent in new[] { "cool", "fine" }) Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, agent, "account-1"));
        new HarnessSettings().WithDefault("cool", "account-1").WithDefault("fine", "account-1").Save(Settings);
        var present = new HarnessToolchain(Binary: [command], VersionArguments: ["--version"], ProfileVariable: "FAKE_HOME", ProbeByPresence: true);
        _adapters = new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
        {
            ["cool"] = Adapter("cool", present),
            ["fine"] = Adapter("fine", present),
        });
        _roster = new HarnessRoster(_adapters, Settings);
    }

    public void Dispose() => GitSeed.Discard(_home);

    private string Settings => DaorisHome.Entry(_home, HomeEntry.Harnesses);

    private static ISessionAdapter Adapter(string name, HarnessToolchain toolchain) => TestDoors.Door(name, toolchain, transport: Transport.Protocol, spawn: new TestDoors.RefusingSpawn("nothing spawns here"));

    /// <summary>The places a test opens, each to one provider.</summary>
    private sealed class Opened(params (string Provider, ReachPlace Place)[] places) : IReachOpenings
    {
        public bool Opens(ProviderId provider, ReachPlace place) =>
            places.Any(each => Ids.Same(each.Provider, provider.Value) && each.Place == place);
    }

    /// <summary>The registry with <paramref name="opened"/> stated <see cref="Reach.Opened"/>, every other provider as registered.</summary>
    private static ProviderRegistry Stated(ProviderRegistry registry, string opened) =>
        new(registry.Providers.Select(provider => Ids.Same(provider.Id.Value, opened) ? provider with { Reach = Reach.Opened } : provider), registry.Reserved);

    /// <summary>The reach of the machine's agents with <c>cool</c> stated <see cref="Reach.Opened"/>, opened where <paramref name="openings"/> say.</summary>
    private ProviderReach CoolReach(IReachOpenings? openings = null) => new(Stated(_adapters.Registry, "cool"), openings);

    /// <summary>A quest's plan on the repository <c>engine</c>, Build's order <paramref name="choices"/>.</summary>
    private StartPlan Plan(ProviderReach reach, string work = "build", params ActorChoice[] choices)
    {
        var config = ActorChoices.With(StaffedRoles.Config("cool"), StartPlan.RoleOf(work), choices);
        return new StartPlan(work, config, _roster, _adapters, new PassMemory(), place: new RolePlace("default", "engine"))
        {
            Workspace = "default", Subject = "quest:q1", Reaching = reach,
        };
    }

    private static readonly ReachPlace Engine = new("default", "engine");

    /// <summary>
    /// 🔴 An <c>Opened</c> provider at a repository not opened to it is a pass, never a refusal of the whole start: passed <c>not-opened</c>
    /// with its sentence before its accounts are asked, and Build's later choice taken.
    /// </summary>
    [Fact]
    public async Task A_provider_not_opened_to_the_repository_is_passed_not_opened_and_the_later_choice_runs()
    {
        var plan = Plan(CoolReach(), "build", new ActorChoice("cool"), new ActorChoice("fine"));

        var settled = await plan.ResolveAsync(CancellationToken.None);

        Assert.Equal("fine", settled.Chosen?.Agent);
        Assert.True(settled.Selection.Allowed, settled.Selection.Refusal);
        var pass = Assert.Single(settled.Passes);
        Assert.Equal(("cool", StartPasses.NotOpened), (pass.Entry.Agent, pass.Code));
        Assert.Equal("`cool` works only where it is opened, and the repository `engine` is not opened to it", pass.Why);
    }

    /// <summary>Where it is the only choice, the start holds on its sentence, nothing counted, as any walk that finds none.</summary>
    [Fact]
    public async Task A_provider_not_opened_that_is_the_only_choice_holds_the_start_and_counts_nothing()
    {
        var plan = Plan(CoolReach(), "build", new ActorChoice("cool"));

        var settled = await plan.ResolveAsync(CancellationToken.None);

        Assert.False(settled.Selection.Allowed);
        Assert.True(settled.Selection.NotOpened);
        Assert.Equal(0, settled.Reservation);
        Assert.Contains("is not opened to it", settled.Selection.Refusal);
    }

    /// <summary>The repository opened to it admits it: taken first, nothing passed.</summary>
    [Fact]
    public async Task A_provider_on_a_repository_opened_to_it_is_admitted()
    {
        var plan = Plan(CoolReach(new Opened(("cool", Engine))), "build", new ActorChoice("cool"), new ActorChoice("fine"));

        var settled = await plan.ResolveAsync(CancellationToken.None);

        Assert.Equal("cool", settled.Chosen?.Agent);
        Assert.Empty(settled.Passes);
    }

    /// <summary>A brainstorm's voice is on no repository: its workspace is read, the voice dropped where it is not opened and run where it is.</summary>
    [Fact]
    public async Task A_brainstorm_voice_is_dropped_where_its_workspace_is_not_opened_and_runs_where_it_is()
    {
        StartPlan Voice(ProviderReach reach) =>
            new(ParticipantWork.Brainstorm, StaffedRoles.Config("cool"), _roster, _adapters, new PassMemory(), order: [new ActorEffective("cool", null, null, "before")],
                place: new RolePlace("default"))
            {
                Workspace = "default", Subject = "brainstorm:b1", Reaching = reach,
            };

        var dropped = await Voice(CoolReach(new Opened(("cool", Engine)))).AccountsAsync("cool", null, null, CancellationToken.None);
        var run = await Voice(CoolReach(new Opened(("cool", new ReachPlace("default", null))))).AccountsAsync("cool", null, null, CancellationToken.None);

        Assert.True(dropped.NotOpened);
        Assert.Contains("the workspace `default` is not opened to it", dropped.Refusal);
        Assert.True(run.Allowed, run.Refusal);
    }

    /// <summary>Ask Daoris's room works across repositories: refused until the machine's room is opened, a repository opened to it admitting nothing.</summary>
    [Fact]
    public async Task Ask_Daoris_is_refused_an_opened_provider_until_the_machine_s_room_is_opened()
    {
        var closed = Plan(CoolReach(new Opened(("cool", Engine))), ParticipantWork.AskDaoris);
        var open = Plan(CoolReach(new Opened(("cool", ReachPlace.Machine))), ParticipantWork.AskDaoris);

        Assert.Equal(ReachPlace.Machine, closed.Where);
        Assert.True((await closed.AccountsAsync("cool", null, null, CancellationToken.None)).NotOpened);
        Assert.True((await open.AccountsAsync("cool", null, null, CancellationToken.None)).Allowed);
    }

    /// <summary>Every provider today reaches everywhere: the built-in registrations, the machine's registry, and a plugin's made provider.</summary>
    [Fact]
    public void Every_built_in_and_plugin_provider_is_admitted_as_today()
    {
        Assert.All(Providers.Built(), provider => Assert.Equal(Daoris.Driver.Reach.Everywhere, provider.Reach));
        Assert.All(AdapterSet.Built().Registry.Providers, provider => Assert.Equal(Daoris.Driver.Reach.Everywhere, provider.Reach));
        var reach = new ProviderReach(AdapterSet.Built().Registry);
        Assert.All(AdapterSet.Built().Names, door => Assert.Null(reach.Closes(door, Engine)));
        Assert.Null(reach.Closes("claude-code", ReachPlace.Machine));
    }

    /// <summary>A plugin's door joined to an <c>Opened</c> provider is bound by its provider's reach, never wider.</summary>
    [Fact]
    public void A_plugin_door_joined_to_an_opened_provider_is_bound_by_it()
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.Plugins, "p.joined");
        Directory.CreateDirectory(folder);
        File.WriteAllText(
            Path.Combine(folder, PluginCatalog.ManifestName),
            """{ "id": "p.joined", "harnesses": [ { "name": "p-joined", "command": ["agent"], "accountOf": "claude-code" } ] }""");
        var adapters = AdapterSet.Built().WithPlugins(PluginCatalog.Load(_home, AdapterSet.Built().Names));

        var reach = new ProviderReach(Stated(adapters.Registry, "claude-code"));

        Assert.Equal("claude-code", reach.ProviderOf("p-joined")?.Id.Value);
        Assert.Equal("`p-joined` works only where it is opened, and the repository `engine` is not opened to it", reach.Closes("p-joined", Engine));
        Assert.Null(new ProviderReach(adapters.Registry).Closes("p-joined", Engine));
    }

    /// <summary>
    /// A meeting's start is outside the plan, so it has no walk to pass in: a participant whose provider is not opened to its place is refused
    /// before anything goes to it, by its sentence, and the others take their turns.
    /// </summary>
    [Fact]
    public void A_meeting_refuses_a_participant_whose_provider_is_not_opened_to_its_place()
    {
        var request = new MeetingRequest("m1", "Which port?", "engine", ["cool", "fine"], DateTimeOffset.UtcNow);

        var (taking, left, closed) = DriverMeetings.Participants(request, BrainstormRule.Default, _adapters, CoolReach());
        var (opened, _, none) = DriverMeetings.Participants(request, BrainstormRule.Default, _adapters, CoolReach(new Opened(("cool", new ReachPlace(null, "engine")))));

        Assert.Equal(["fine"], taking.Select(each => each.Adapter));
        Assert.Empty(left);
        Assert.Equal(["`cool` works only where it is opened, and the repository `engine` is not opened to it"], closed);
        Assert.Equal(["cool", "fine"], opened.Select(each => each.Adapter));
        Assert.Empty(none);
    }
}
