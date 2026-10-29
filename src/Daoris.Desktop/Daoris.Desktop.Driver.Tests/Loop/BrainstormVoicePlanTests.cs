using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// 🔴 FOUNDATION1j part ii (the foundation design §2.4's table; D205's round-2 note and §4.4): a brainstorm voice starts on a plan of its own
/// entry alone, asked for its request's workspace, so it runs the version that workspace pins; and a voice that cannot run is answered so,
/// its plan holding no other entry or agent to put in its place, and the brainstorm drops it.
/// </summary>
/// <remarks>
/// Nothing here starts a process: each agent is present by a file look, the pinned one by a file in the home's managed install of that
/// version, as <c>ReviewerChoiceTests</c>' pin is.
/// </remarks>
public sealed class BrainstormVoicePlanTests : IDisposable
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kathmandu");

    private static readonly DateTimeOffset Now = new(DateTime.UtcNow.Date.AddHours(12), TimeSpan.Zero);

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "foundation1j-voice-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly HarnessRoster _roster;

    public BrainstormVoicePlanTests()
    {
        Directory.CreateDirectory(_home);
        File.WriteAllText(Command, "");
        // `voice` runs by a bare name only its pinned install answers; `other` is present wherever it is asked.
        var voice = new HarnessToolchain(Binary: ["voice-pinned"], VersionArguments: ["--version"], ProfileVariable: "FAKE_HOME", ProbeByPresence: true);
        var other = new HarnessToolchain(Binary: [Command], VersionArguments: ["--version"], ProfileVariable: "FAKE_HOME", ProbeByPresence: true);
        var adapters = new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
        {
            ["voice"] = new Adapter("voice", voice),
            ["other"] = new Adapter("other", other),
        });
        foreach (var agent in new[] { "voice", "other" }) Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, agent, "account-1"));
        _roster = new HarnessRoster(adapters, Settings) { Clock = () => Now, Zone = Zone };
    }

    public void Dispose() => GitSeed.Discard(_home);

    private string Settings => DaorisHome.Entry(_home, HomeEntry.Harnesses);

    private string Command => Path.Combine(_home, "agent-here");

    private sealed class Adapter(string name, HarnessToolchain toolchain) : ISessionAdapter
    {
        public string Name => name;

        public HarnessToolchain? Toolchain => toolchain;

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) => throw new InvalidOperationException("nothing spawns here");
    }

    private static readonly ActorEffective Entry = new("voice", "opus", null, "1");

    private static DriverConfig Config => ActorChoices.With(
        DriverConfig.Empty with { Adapter = "other" }, "brainstorm", [new ActorChoice("voice", "opus"), new ActorChoice("other", "sonnet")]);

    private BrainstormTurn Turn(string? workspace) => new("b1", "g1", "voice", _home, "Answer it.", Path.Combine(_home, "g1.json"), 5, Entry)
    {
        Workspace = workspace,
    };

    /// <summary>The managed install of <c>voice</c> at <paramref name="version"/>, present by its file.</summary>
    private string Installed(string version)
    {
        var managed = Path.Combine(
            HarnessSettings.ManagedHome(_home, "voice", version), "bin", OperatingSystem.IsWindows() ? "voice-pinned.exe" : "voice-pinned");
        Directory.CreateDirectory(Path.GetDirectoryName(managed)!);
        File.WriteAllText(managed, "");
        return managed;
    }

    /// <summary>
    /// The voice's plan is its own entry alone, Brainstorm's, for its request's workspace; that workspace's pin is the version it runs, where a
    /// voice asked for no workspace, as every voice was before, runs the machine's.
    /// </summary>
    [Fact]
    public async Task A_voice_runs_its_workspace_s_pinned_version_on_a_plan_of_its_own_entry_alone()
    {
        var work = Installed("2.0.0");
        new HarnessSettings()
            .WithDefault("voice", "account-1").WithDefault("other", "account-1")
            .WithWorkspaceVersion("work", "voice", "2.0.0").Save(Settings);

        var plan = DriverBrainstorms.Voice(Turn("work"), Config, _roster);
        var placed = await DriverBrainstorms.VoiceAsync(Turn("work"), Config, _roster, CancellationToken.None);

        Assert.Equal(("brainstorm", "work"), (plan.Role, plan.Workspace));
        Assert.Equal([Entry], plan.Order);
        // The binary it runs is its pin's managed install: a file look reads no version, so the install is the proof of it. No
        // machine-wide install or pin exists, so the workspace's pin alone makes it present (WSPINONLY1).
        Assert.True(placed.Allowed, placed.Refusal);
        Assert.Equal((work, "account-1"), (placed.Binary, placed.Profile));
    }

    /// <summary>A pin counts for its own workspace's starts alone (WSPINONLY1): another workspace, and a start asking none, find nothing installed.</summary>
    [Fact]
    public async Task A_workspace_s_pin_makes_the_agent_present_for_that_workspace_alone()
    {
        Installed("2.0.0");
        new HarnessSettings().WithDefault("voice", "account-1").WithWorkspaceVersion("work", "voice", "2.0.0").Save(Settings);

        var elsewhere = await DriverBrainstorms.VoiceAsync(Turn("elsewhere"), Config, _roster, CancellationToken.None);
        var none = await DriverBrainstorms.VoiceAsync(Turn(null), Config, _roster, CancellationToken.None);

        Assert.False(elsewhere.Allowed);
        Assert.Contains("not installed", elsewhere.Refusal);
        Assert.False(none.Allowed);
        Assert.Contains("not installed", none.Refusal);
    }

    /// <summary>A machine-wide install and pin count in every workspace and for a start asking none (WSPINONLY1).</summary>
    [Fact]
    public async Task A_machine_wide_install_counts_everywhere()
    {
        var machine = Installed("1.0.0");
        new HarnessSettings().WithDefault("voice", "account-1").WithVersion("voice", "1.0.0").Save(Settings);

        var unplaced = await DriverBrainstorms.VoiceAsync(Turn(null), Config, _roster, CancellationToken.None);
        var placed = await DriverBrainstorms.VoiceAsync(Turn("work"), Config, _roster, CancellationToken.None);

        Assert.True(unplaced.Allowed, unplaced.Refusal);
        Assert.Equal(machine, unplaced.Binary);
        Assert.True(placed.Allowed, placed.Refusal);
        Assert.Equal(machine, placed.Binary);
    }

    /// <summary>
    /// A voice that cannot run, here its one account cooling, is answered held by its own entry's walk alone: the plan asks no other entry and
    /// no other agent, though `other` is ready, so the brainstorm drops the voice and never replaces it.
    /// </summary>
    [Fact]
    public async Task A_voice_that_cannot_run_is_held_on_its_own_entry_and_never_replaced()
    {
        Installed("1.0.0");
        new HarnessSettings().WithDefault("voice", "account-1").WithDefault("other", "account-1").WithVersion("voice", "1.0.0").Save(Settings);
        AccountCooling.Cool(_home, new CoolingEntry("voice", "account-1", Now.AddHours(3), true, "weekly", Now, "s0"), Now);
        var (counted, walks) = (_roster.Mark(), _roster.Selected);

        var held = await DriverBrainstorms.VoiceAsync(Turn("work"), Config, _roster, CancellationToken.None);

        Assert.False(held.Allowed);
        Assert.Equal(("voice", Now.AddHours(3)), (held.Cooling!.Agent, held.Cooling.Until));
        Assert.Equal((counted, walks + 1), (_roster.Mark(), _roster.Selected));
    }
}
