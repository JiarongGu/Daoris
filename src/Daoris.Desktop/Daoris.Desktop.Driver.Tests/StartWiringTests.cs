using System.Diagnostics;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <b>What a start would take, and where each part came from</b> (MAP1b, D67 §3) — the wiring panel's
/// answer, read through the driver's own judgement so the picture cannot disagree with the loop.
/// </summary>
/// <remarks>
/// The design's rule (<c>docs/2026-09-23-map-design.md</c> §2): the wiring is resolved "by the same
/// functions the driver uses". So the account is <see cref="HarnessSettings.Resolve"/>'s own answer,
/// with its source beside it, and whether the start would happen is <see cref="HarnessRoster.SelectAsync"/>
/// itself — its refusal verbatim.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class StartWiringTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-wiring-tests", Guid.NewGuid().ToString("N")[..8]);

    private string Settings => Path.Combine(_home, "harnesses.json");

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private sealed class Adapter(string name, HarnessToolchain toolchain, SessionWire wire) : ISessionAdapter
    {
        public string Name => name;

        public SessionWire Wire => wire;

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) => new();

        public HarnessToolchain? Toolchain => toolchain;
    }

    /// <summary>An agent that is there, signed in when its home holds `credentials.json` or a key is set.</summary>
    private HarnessRoster Roster()
    {
        Directory.CreateDirectory(_home);
        var script = Path.Combine(_home, "agent.mjs");
        File.WriteAllText(script, """
            const fs = await import('node:fs');
            if (process.argv[2] === '--version') { console.log('agent 1.4.2'); process.exit(0); }
            const home = process.env.AGENT_HOME;
            const signed = process.env.AGENT_KEY || (home && fs.existsSync(home + '/credentials.json'));
            console.log(signed ? 'logged-in' : 'logged-out');
            """);

        var toolchain = new HarnessToolchain(
            Binary: ["node", script], VersionArguments: ["--version"], ProfileVariable: "AGENT_HOME",
            LoginCheck: new LoginQuestion(["auth"], "logged-in", "logged-out"), KeyVariable: "AGENT_KEY",
            Package: "agent-cli");
        var adapters = new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
        {
            ["agent"] = new Adapter("agent", toolchain, SessionWire.Pipe),
            ["agent-acp"] = new Adapter("agent-acp", new HarnessToolchain(
                Binary: ["node", script], VersionArguments: ["--version"],
                ProfileVariable: "AGENT_HOME", AccountOf: "agent"), SessionWire.Acp),
        };
        return new HarnessRoster(new AdapterSet(adapters), Settings);
    }

    private static DriverConfig Config(string adapter = "agent") => DriverConfig.Empty with { Adapter = adapter };

    private void SignedIn(string owner, string profile)
    {
        var home = HarnessSettings.ProfileHome(_home, owner, profile);
        Directory.CreateDirectory(home);
        File.WriteAllText(Path.Combine(home, "credentials.json"), "{}");
    }

    [Fact]
    public async Task The_machine_s_default_account_and_the_version_on_PATH()
    {
        SignedIn("agent", "work");
        new HarnessSettings().WithDefault("agent", "work").Save(Settings);

        var wiring = await Roster().WiringAsync("agent", Config(), "aurora");

        Assert.Null(wiring.Refusal);
        Assert.Equal("work", wiring.Profile);
        Assert.Equal(ChoiceFrom.Machine, wiring.ProfileFrom);
        Assert.Equal(ChoiceFrom.Unset, wiring.VersionFrom);
        Assert.False(wiring.Commanded);
        Assert.Contains("1.4.2", wiring.Version);
    }

    /// <summary>The order a start asks in (D49 §4): the workspace's default before the machine's.</summary>
    [Fact]
    public async Task A_workspace_s_default_account_outranks_the_machine_s()
    {
        SignedIn("agent", "work");
        SignedIn("agent", "office");
        new HarnessSettings().WithDefault("agent", "work").WithWorkspaceDefault("aurora", "agent", "office").Save(Settings);
        var roster = Roster();

        var aurora = await roster.WiringAsync("agent", Config(), "aurora");
        var other = await roster.WiringAsync("agent", Config(), "elsewhere");

        Assert.Equal(("office", ChoiceFrom.Workspace), (aurora.Profile, aurora.ProfileFrom));
        Assert.Equal(("work", ChoiceFrom.Machine), (other.Profile, other.ProfileFrom));
    }

    /// <summary>No default anywhere is the agent's own sign-in — a real answer, not a missing one.</summary>
    [Fact]
    public async Task No_default_is_the_agent_s_own_sign_in()
    {
        var wiring = await Roster().WiringAsync("agent", Config(), "aurora");

        Assert.Null(wiring.Profile);
        Assert.Equal(ChoiceFrom.Unset, wiring.ProfileFrom);
    }

    /// <summary>
    /// 🔴 The panel's account IS the one a start takes: for every shape of the wiring file, the answer
    /// and <see cref="HarnessRoster.SelectAsync"/> agree — the whole point of reading one judgement.
    /// </summary>
    [Fact]
    public async Task The_account_shown_is_the_account_a_start_takes()
    {
        SignedIn("agent", "work");
        SignedIn("agent", "office");
        var shapes = new[]
        {
            new HarnessSettings(),
            new HarnessSettings().WithDefault("agent", "work"),
            new HarnessSettings().WithWorkspaceDefault("aurora", "agent", "office"),
            new HarnessSettings().WithDefault("agent", "work").WithWorkspaceDefault("aurora", "agent", "office"),
        };
        var roster = Roster();

        foreach (var shape in shapes)
        {
            shape.Save(Settings);
            foreach (var workspace in new[] { "aurora", "elsewhere" })
            {
                var wiring = await roster.WiringAsync("agent", Config(), workspace);
                var selection = await roster.SelectAsync("agent", Config(), workspace, chosen: null);
                Assert.Equal(selection.Profile, wiring.Profile);
                Assert.Equal(selection.Version, wiring.Version);
            }
        }
    }

    /// <summary>A start that would be refused says so in the driver's own words, and still names the account.</summary>
    [Fact]
    public async Task A_refused_start_carries_the_driver_s_sentence_and_the_account()
    {
        Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "agent", "stale"));
        new HarnessSettings().WithDefault("agent", "stale").Save(Settings);
        var roster = Roster();

        var wiring = await roster.WiringAsync("agent", Config(), "aurora");
        var selection = await roster.SelectAsync("agent", Config(), "aurora", chosen: null);

        Assert.Equal(selection.Refusal, wiring.Refusal);
        Assert.Contains("daoris agent login agent --profile stale", wiring.Refusal);
        Assert.Equal("stale", wiring.Profile);
    }

    [Fact]
    public async Task A_pin_says_where_it_was_set_and_a_missing_pin_is_the_refusal()
    {
        new HarnessSettings().WithVersion("agent", "9.9.9").Save(Settings);

        var wiring = await Roster().WiringAsync("agent", Config(), "aurora");

        Assert.Equal(ChoiceFrom.Machine, wiring.VersionFrom);
        Assert.Equal("9.9.9", wiring.Version);
        Assert.Contains("pinned to 9.9.9", wiring.Refusal);
    }

    /// <summary>A command `driver.json` names has the last word over any pin (TOOL2), and the panel says so.</summary>
    [Fact]
    public async Task A_command_driver_json_names_outranks_the_pin()
    {
        new HarnessSettings().WithVersion("agent", "9.9.9").Save(Settings);
        var script = Path.Combine(_home, "agent.mjs");
        var config = Config() with
        {
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["agent"] = ["node", script] },
        };

        var wiring = await Roster().WiringAsync("agent", config, "aurora");

        Assert.True(wiring.Commanded);
        Assert.Null(wiring.Refusal);
    }

    /// <summary>A door runs as its owner's accounts (AGT7): the panel names the owner, and the owner's default.</summary>
    [Fact]
    public async Task A_door_is_wired_to_its_owner_s_account()
    {
        SignedIn("agent", "work");
        new HarnessSettings().WithDefault("agent", "work").Save(Settings);

        var wiring = await Roster().WiringAsync("agent-acp", Config("agent-acp"), "aurora");

        Assert.Equal("agent-acp", wiring.Adapter);
        Assert.Equal("agent", wiring.Owner);
        Assert.Equal(("work", ChoiceFrom.Machine), (wiring.Profile, wiring.ProfileFrom));
    }

    /// <summary>🔴 A key account's answer names the account and never carries the key (AGT3).</summary>
    [Fact]
    public async Task A_key_account_is_named_and_its_key_never_travels()
    {
        var account = HarnessKeys.Add(_home, "agent", "sk-wiring-secret-4242");
        new HarnessSettings().WithDefault("agent", account).Save(Settings);

        var wiring = await Roster().WiringAsync("agent", Config(), "aurora");

        Assert.Equal(account, wiring.Profile);
        Assert.DoesNotContain("sk-wiring-secret", JsonSerializer.Serialize(wiring));
    }
}
