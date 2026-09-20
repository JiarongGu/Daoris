using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The toolchain (D49 §4): which harnesses this machine has, and which ACCOUNT a session runs as.
///
/// <para>The whole of it rests on one sentence from the design: <b>Daoris manages directories and
/// names, never secrets.</b> A profile is a directory Daoris owns the location of; the credential
/// inside it belongs to the harness's own store, under the user's OS account, exactly where it lives
/// today. Nothing here reads, copies, or moves one — and the tests below are written to fail if
/// anything ever starts to.</para>
/// </summary>
public sealed class HarnessSettingsTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-harness-tests", Guid.NewGuid().ToString("N")[..8]);

    private string Path_ => System.IO.Path.Combine(_home, "harnesses.json");

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    /// <summary>A machine that has never named a profile runs exactly as it did before (D21).</summary>
    [Fact]
    public void A_missing_file_is_a_machine_that_named_no_profiles()
    {
        var settings = HarnessSettings.Load(Path_);

        Assert.Empty(settings.Defaults);
        Assert.Empty(settings.Workspaces);
        Assert.Null(settings.Resolve("claude-code", workspace: "aurora", chosen: null));
    }

    /// <summary>
    /// The resolution order the design states: the person's pick for THIS session, then the
    /// workspace's default, then the machine's, then nothing at all.
    /// </summary>
    [Fact]
    public void A_profile_is_chosen_then_the_workspace_s_then_the_machine_s()
    {
        var settings = new HarnessSettings(
            Defaults: new Dictionary<string, string> { ["claude-code"] = "personal" },
            Workspaces: new Dictionary<string, IReadOnlyDictionary<string, string>>
            {
                ["aurora"] = new Dictionary<string, string> { ["claude-code"] = "work" },
            });

        Assert.Equal("picked", settings.Resolve("claude-code", "aurora", "picked"));
        Assert.Equal("work", settings.Resolve("claude-code", "aurora", null));
        Assert.Equal("personal", settings.Resolve("claude-code", "tools", null));
        Assert.Equal("personal", settings.Resolve("claude-code", null, null));
        Assert.Null(settings.Resolve("stub", "aurora", null));
    }

    /// <summary>
    /// <b>Nothing named means the harness's OWN home, untouched.</b> Not an empty profile directory —
    /// pointing a person who never asked for profiles at a fresh configuration home would log them
    /// out of their own tool, which is the loudest possible way to break "Daoris works alone".
    /// </summary>
    [Fact]
    public void No_profile_anywhere_means_the_harness_s_own_home()
    {
        Assert.Null(new HarnessSettings().Resolve("claude-code", "aurora", chosen: null));
    }

    /// <summary>
    /// A profile is a DIRECTORY, and the directory is the contract — the same shape the remotes map
    /// takes, for the same reason: the CLI and the driver share no code, so the layout on disk is what
    /// keeps them agreeing.
    /// </summary>
    [Fact]
    public void A_profile_home_is_a_directory_under_the_harness_s_name()
    {
        var home = HarnessSettings.ProfileHome(_home, "claude-code", "work");

        Assert.Equal(
            System.IO.Path.Combine(_home, "harnesses", "claude-code", "work"),
            home);
    }

    /// <summary>A profile exists because its directory does — there is no second register to disagree.</summary>
    [Fact]
    public void The_profiles_that_exist_are_the_directories_that_exist()
    {
        Assert.Empty(HarnessSettings.Profiles(_home, "claude-code"));

        Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "claude-code", "work"));
        Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "claude-code", "personal"));
        Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "codex", "work"));

        Assert.Equal(["personal", "work"], HarnessSettings.Profiles(_home, "claude-code"));
        Assert.Equal(["work"], HarnessSettings.Profiles(_home, "codex"));
    }

    /// <summary>The file is the API and this is an editor over it (D50) — written, read back, unchanged.</summary>
    [Fact]
    public void The_defaults_round_trip_through_the_file()
    {
        var settings = new HarnessSettings()
            .WithDefault("claude-code", "personal")
            .WithWorkspaceDefault("aurora", "claude-code", "work");
        settings.Save(Path_);

        var read = HarnessSettings.Load(Path_);

        Assert.Equal("personal", read.Resolve("claude-code", null, null));
        Assert.Equal("work", read.Resolve("claude-code", "aurora", null));
    }

    /// <summary>Clearing a default is naming none — and it leaves the other circles alone.</summary>
    [Fact]
    public void A_cleared_default_leaves_every_other_wiring_standing()
    {
        var settings = new HarnessSettings()
            .WithDefault("claude-code", "personal")
            .WithWorkspaceDefault("aurora", "claude-code", "work")
            .WithWorkspaceDefault("aurora", "claude-code", null);

        Assert.Equal("personal", settings.Resolve("claude-code", "aurora", null));
    }

    /// <summary>
    /// A name that would escape the harnesses directory is refused rather than normalized. Daoris owns
    /// the LOCATION of a profile home, and a name carrying a separator is a request to own somewhere
    /// else — the one place in this feature where a bad string becomes a path.
    /// </summary>
    [Theory]
    [InlineData("../escape")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(".")]
    public void A_profile_name_that_is_not_a_name_is_refused(string name)
    {
        var error = Assert.Throws<DriverException>(
            () => HarnessSettings.ProfileHome(_home, "claude-code", name));

        Assert.Contains("profile name", error.Message);
    }

    /// <summary>
    /// A torn or hand-mangled file is a machine with no defaults, never a driver that will not start:
    /// the same judgement the remotes map makes, and for the same reason — this is wiring, and absent
    /// wiring is the documented default.
    /// </summary>
    [Fact]
    public void Unreadable_wiring_is_no_wiring_rather_than_a_dead_driver()
    {
        Directory.CreateDirectory(_home);
        File.WriteAllText(Path_, "{ this is not json");

        Assert.Empty(HarnessSettings.Load(Path_).Defaults);
    }
}

/// <summary>
/// The probe (D49 §4): locate, version, and per-profile login state — <b>run, never assumed.</b>
/// </summary>
/// <remarks>
/// Driven against a fake binary, which is the only way this is gate-testable: install, update and
/// login are person-actions on real installers and stay out of gates by the design's own words. What
/// a gate CAN hold is that the probe asks, parses what it is given, and refuses to guess when the
/// answer is a shape it does not recognise.
/// </remarks>
public sealed class HarnessProbeTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-probe-tests", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    /// <summary>
    /// A script standing in for a harness binary: it answers `--version`, and answers the login
    /// question out of whatever its profile home contains. Real mechanics, no model, no network.
    /// </summary>
    private string FakeBinary()
    {
        Directory.CreateDirectory(_home);
        var script = Path.Combine(_home, "fake-harness.mjs");
        File.WriteAllText(script, """
            const home = process.env.FAKE_HARNESS_HOME;
            if (process.argv[2] === '--version') { console.log('fake-harness 9.9.9'); process.exit(0); }
            if (process.argv[2] === 'auth') {
              const fs = await import('node:fs');
              const inside = home && fs.existsSync(home + '/credentials.json');
              console.log(JSON.stringify({ loggedIn: Boolean(inside) }));
              process.exit(0);
            }
            console.log('fake-harness: ' + process.argv.slice(2).join(' '));
            """);
        return script;
    }

    private HarnessToolchain Toolchain(string script) => new(
        Binary: ["node", script],
        VersionArguments: ["--version"],
        ProfileVariable: "FAKE_HARNESS_HOME",
        LoginCheck: new LoginQuestion(["auth", "status"], @"""loggedIn""\s*:\s*true", @"""loggedIn""\s*:\s*false"));

    [Fact]
    public async Task A_present_harness_reports_the_version_it_answered_with()
    {
        var report = await HarnessProbe.ProbeAsync(
            "fake", Toolchain(FakeBinary()), command: null, new HarnessSettings(), _home);

        Assert.True(report.Present);
        Assert.Equal("fake-harness 9.9.9", report.Version);
        Assert.Null(report.Problem);
    }

    /// <summary>
    /// An absent harness is an ANSWER, not a crash — and it names the action that fixes it, which is
    /// the whole point of the roster existing.
    /// </summary>
    [Fact]
    public async Task An_absent_harness_is_reported_rather_than_thrown()
    {
        var toolchain = new HarnessToolchain(
            Binary: ["daoris-no-such-binary-anywhere"], VersionArguments: ["--version"]);

        var report = await HarnessProbe.ProbeAsync(
            "fake", toolchain, command: null, new HarnessSettings(), _home);

        Assert.False(report.Present);
        Assert.Null(report.Version);
        Assert.NotNull(report.Problem);
    }

    /// <summary>
    /// Login state is asked of the harness IN a profile home, and the two answers are distinguished by
    /// what the harness prints. A fresh profile is logged OUT; one the harness has written into is in.
    /// </summary>
    [Fact]
    public async Task Each_profile_s_login_state_is_asked_of_the_harness_inside_it()
    {
        var script = FakeBinary();
        Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "fake", "fresh"));
        var used = HarnessSettings.ProfileHome(_home, "fake", "used");
        Directory.CreateDirectory(used);
        File.WriteAllText(Path.Combine(used, "credentials.json"), "{}");

        var report = await HarnessProbe.ProbeAsync(
            "fake", Toolchain(script), command: null, new HarnessSettings(), _home);

        Assert.Equal(LoginState.Out, report.Profiles.Single(p => p.Name == "fresh").Login);
        Assert.Equal(LoginState.In, report.Profiles.Single(p => p.Name == "used").Login);
    }

    /// <summary>
    /// A harness whose answer this build does not recognise leaves the state UNKNOWN — and unknown is
    /// permissive at spawn. The same judgement WSP4 made about an undeclared canonical line: refusing
    /// on an answer nobody can read would silence a working tool over a reworded sentence.
    /// </summary>
    [Fact]
    public async Task An_unrecognised_answer_is_unknown_rather_than_logged_out()
    {
        var script = FakeBinary();
        Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "fake", "opaque"));
        var toolchain = Toolchain(script) with
        {
            LoginCheck = new LoginQuestion(["auth", "status"], "NEVER MATCHES", "ALSO NEVER MATCHES"),
        };

        var report = await HarnessProbe.ProbeAsync(
            "fake", toolchain, command: null, new HarnessSettings(), _home);

        Assert.Equal(LoginState.Unknown, report.Profiles.Single().Login);
    }

    /// <summary>
    /// A harness that cannot be asked about logins is not a harness whose profiles are logged out.
    /// </summary>
    [Fact]
    public async Task A_harness_with_no_login_question_leaves_every_profile_unknown()
    {
        var script = FakeBinary();
        Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "fake", "whatever"));

        var report = await HarnessProbe.ProbeAsync(
            "fake", Toolchain(script) with { LoginCheck = null }, command: null, new HarnessSettings(), _home);

        Assert.Equal(LoginState.Unknown, report.Profiles.Single().Login);
    }

    /// <summary>
    /// <b>The probe takes one fact and keeps nothing else.</b> A real harness volunteers an email, an
    /// organisation and a subscription tier when asked whether it is logged in; Daoris reads the
    /// boolean. This is the test that fails the day somebody decides the roster would look nicer with
    /// the account name on it.
    /// </summary>
    [Fact]
    public async Task Nothing_the_harness_volunteers_beyond_the_answer_is_kept()
    {
        Directory.CreateDirectory(_home);
        var script = Path.Combine(_home, "chatty-harness.mjs");
        File.WriteAllText(script, """
            if (process.argv[2] === '--version') { console.log('chatty 1.0'); process.exit(0); }
            console.log(JSON.stringify({ loggedIn: true, email: 'someone@example.invalid', orgName: 'Secretive' }));
            """);
        Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "fake", "work"));

        var report = await HarnessProbe.ProbeAsync(
            "fake",
            new HarnessToolchain(
                Binary: ["node", script], VersionArguments: ["--version"],
                ProfileVariable: "CHATTY_HOME",
                LoginCheck: new LoginQuestion(["auth"], @"""loggedIn""\s*:\s*true", @"""loggedIn""\s*:\s*false")),
            command: null, new HarnessSettings(), _home);

        var serialised = string.Join("|", report.Profiles.Select(p => $"{p.Name}:{p.Home}:{p.Login}"))
            + $"|{report.Version}|{report.Problem}";
        Assert.DoesNotContain("example.invalid", serialised);
        Assert.DoesNotContain("Secretive", serialised);
        Assert.Equal(LoginState.In, report.Profiles.Single().Login);
    }

    /// <summary>The configured command wins over the declared one — a machine's shim, as ever.</summary>
    [Fact]
    public async Task The_configured_command_is_what_gets_probed()
    {
        var script = FakeBinary();
        var toolchain = new HarnessToolchain(
            Binary: ["daoris-no-such-binary-anywhere"], VersionArguments: ["--version"]);

        var report = await HarnessProbe.ProbeAsync(
            "fake", toolchain, command: ["node", script], new HarnessSettings(), _home);

        Assert.True(report.Present);
        Assert.Equal("fake-harness 9.9.9", report.Version);
    }
}

/// <summary>
/// The judgement half (D49 §4): may this spawn happen, and as which account? <b>One implementation
/// for both doors</b> — the driven loop and a conversation ask the same question of the same object,
/// because two copies of "may this start" drift and the drift is invisible.
/// </summary>
public sealed class HarnessSelectionTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-selection-tests", Guid.NewGuid().ToString("N")[..8]);

    private string Settings => Path.Combine(_home, "harnesses.json");

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    /// <summary>A harness that is there, with a profile question it answers from its own home.</summary>
    private sealed class FakeAdapter(HarnessToolchain? toolchain) : ISessionAdapter
    {
        public string Name => "fake";

        public System.Diagnostics.ProcessStartInfo Prepare(
            SessionTarget target, IReadOnlyList<string>? command) => new();

        public HarnessToolchain? Toolchain => toolchain;
    }

    private static AdapterSet Set(HarnessToolchain? toolchain) =>
        new(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
        {
            ["fake"] = new FakeAdapter(toolchain),
        });

    private string Present()
    {
        Directory.CreateDirectory(_home);
        var script = Path.Combine(_home, "present.mjs");
        File.WriteAllText(script, """
            const fs = await import('node:fs');
            if (process.argv[2] === '--version') { console.log('present 1.2.3'); process.exit(0); }
            const home = process.env.FAKE_HOME;
            console.log(home && fs.existsSync(home + '/credentials.json') ? 'logged-in' : 'logged-out');
            """);
        return script;
    }

    private HarnessToolchain Toolchain(string script) => new(
        Binary: ["node", script],
        VersionArguments: ["--version"],
        ProfileVariable: "FAKE_HOME",
        Install: ["npm", "install", "-g", "fake"],
        LoginCheck: new LoginQuestion(["auth"], "logged-in", "logged-out"));

    private static DriverConfig Config() => DriverConfig.Empty with { Adapter = "fake" };

    /// <summary>
    /// <b>The additive case, and the one that must never regress.</b> No profile named anywhere: the
    /// spawn proceeds, the environment seam is not touched, and the harness uses the configuration
    /// home it always has. This is what keeps "Daoris works alone" true through a feature about
    /// accounts (D48 §2a) — a person who never asked for profiles must not be logged out by one.
    /// </summary>
    [Fact]
    public async Task With_no_profile_named_the_spawn_proceeds_on_the_harness_s_own_home()
    {
        var roster = new HarnessRoster(Set(Toolchain(Present())), Settings);

        var selection = await roster.SelectAsync("fake", Config(), workspace: "aurora", chosen: null);

        Assert.True(selection.Allowed);
        Assert.Null(selection.Profile);
        Assert.Null(selection.ProfileHome);
        Assert.Equal("present 1.2.3", selection.Version);
    }

    /// <summary>The version observed at spawn is what the record will carry — asked, not assumed.</summary>
    [Fact]
    public async Task The_version_the_record_carries_is_the_one_the_harness_answered_with()
    {
        var roster = new HarnessRoster(Set(Toolchain(Present())), Settings);

        Assert.Equal(
            "present 1.2.3",
            (await roster.SelectAsync("fake", Config(), null, null)).Version);
    }

    /// <summary>A workspace's default is the natural cut: a work account for the work circle.</summary>
    [Fact]
    public async Task A_workspace_s_default_profile_is_what_a_spawn_in_that_circle_runs_as()
    {
        var used = HarnessSettings.ProfileHome(_home, "fake", "work");
        Directory.CreateDirectory(used);
        File.WriteAllText(Path.Combine(used, "credentials.json"), "{}");
        new HarnessSettings().WithWorkspaceDefault("aurora", "fake", "work").Save(Settings);

        var roster = new HarnessRoster(Set(Toolchain(Present())), Settings);
        var selection = await roster.SelectAsync("fake", Config(), workspace: "aurora", chosen: null);

        Assert.True(selection.Allowed);
        Assert.Equal("work", selection.Profile);
        Assert.Equal(used, selection.ProfileHome);
    }

    /// <summary>
    /// A missing harness refuses BEFORE anything is recorded, and the refusal names the action —
    /// never a bare not-found. Daoris installs nothing unasked: a tool that changed under a running
    /// loop is the moving-target problem one layer down.
    /// </summary>
    [Fact]
    public async Task A_missing_harness_refuses_naming_the_install_action()
    {
        var toolchain = new HarnessToolchain(
            Binary: ["daoris-no-such-binary-anywhere"], VersionArguments: ["--version"],
            Install: ["npm", "install", "-g", "fake"]);
        var roster = new HarnessRoster(Set(toolchain), Settings);

        var selection = await roster.SelectAsync("fake", Config(), null, null);

        Assert.False(selection.Allowed);
        Assert.Contains("not installed", selection.Refusal);
        Assert.Contains("daoris harness install fake", selection.Refusal);
    }

    /// <summary>
    /// The logged-out refusal mirrors the missing-harness one deliberately: same shape, same
    /// before-the-record timing, same naming of the action that fixes it.
    /// </summary>
    [Fact]
    public async Task A_logged_out_profile_refuses_naming_the_login_action()
    {
        Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "fake", "fresh"));
        new HarnessSettings().WithDefault("fake", "fresh").Save(Settings);

        var roster = new HarnessRoster(Set(Toolchain(Present())), Settings);
        var selection = await roster.SelectAsync("fake", Config(), null, null);

        Assert.False(selection.Allowed);
        Assert.Contains("not logged in", selection.Refusal);
        Assert.Contains("daoris harness login fake --profile fresh", selection.Refusal);
        // And it says where the credential lives, because the obvious worry is that Daoris took it.
        Assert.Contains("harness's own store", selection.Refusal);
    }

    /// <summary>
    /// <b>A cached "no" is asked again before it is given.</b> Otherwise the person does exactly what
    /// the refusal told them to and the driver keeps refusing until somebody restarts it — which is
    /// the worst possible reading of a message that named an action.
    /// </summary>
    [Fact]
    public async Task A_refusal_is_re_checked_so_logging_in_takes_effect_without_a_restart()
    {
        var home = HarnessSettings.ProfileHome(_home, "fake", "fresh");
        Directory.CreateDirectory(home);
        new HarnessSettings().WithDefault("fake", "fresh").Save(Settings);

        var roster = new HarnessRoster(Set(Toolchain(Present())), Settings);
        Assert.False((await roster.SelectAsync("fake", Config(), null, null)).Allowed);

        // The person logs in. Nothing restarts.
        File.WriteAllText(Path.Combine(home, "credentials.json"), "{}");

        var second = await roster.SelectAsync("fake", Config(), null, null);
        Assert.True(second.Allowed);
        Assert.Equal("fresh", second.Profile);
    }

    /// <summary>
    /// The wiring file is re-read, never held — same rule as `driver.json` (D50): a default changed
    /// from a terminal takes effect on the next spawn, because the FILE is the truth.
    /// </summary>
    [Fact]
    public async Task A_default_changed_on_disk_takes_effect_without_a_restart()
    {
        var work = HarnessSettings.ProfileHome(_home, "fake", "work");
        Directory.CreateDirectory(work);
        File.WriteAllText(Path.Combine(work, "credentials.json"), "{}");

        var roster = new HarnessRoster(Set(Toolchain(Present())), Settings);
        Assert.Null((await roster.SelectAsync("fake", Config(), null, null)).Profile);

        new HarnessSettings().WithDefault("fake", "work").Save(Settings);

        Assert.Equal("work", (await roster.SelectAsync("fake", Config(), null, null)).Profile);
    }

    /// <summary>
    /// An adapter that declares no toolchain spawns exactly as it did before this existed. Purely
    /// additive: a new adapter arrives without first answering questions about an installer it may
    /// not have.
    /// </summary>
    [Fact]
    public async Task An_adapter_with_no_toolchain_is_checked_for_nothing()
    {
        var roster = new HarnessRoster(Set(toolchain: null), Settings);

        var selection = await roster.SelectAsync("fake", Config(), "aurora", chosen: "ignored");

        Assert.True(selection.Allowed);
        Assert.Null(selection.Profile);
    }

    /// <summary>
    /// The environment seam, applied. This is the whole mechanism of a credential profile: one
    /// variable, pointing at a directory Daoris owns the location of.
    /// </summary>
    [Fact]
    public void Selecting_a_profile_puts_its_home_into_the_environment_through_the_harness_s_own_seam()
    {
        var info = new System.Diagnostics.ProcessStartInfo();
        var home = Path.Combine(_home, "somewhere");

        HarnessProbe.Apply(info, Toolchain("x"), home);

        Assert.Equal(home, info.Environment["FAKE_HOME"]);
        // Created as part of selecting it: at least one supported harness refuses to start when its
        // home variable names a path that does not exist.
        Assert.True(Directory.Exists(home));
    }

    /// <summary>A harness with no configuration-home variable cannot be run as a named profile.</summary>
    [Fact]
    public void A_harness_with_no_seam_refuses_a_profile_rather_than_ignoring_it()
    {
        var toolchain = new HarnessToolchain(Binary: ["x"], VersionArguments: ["--version"]);

        var error = Assert.Throws<DriverException>(
            () => HarnessProbe.Apply(new System.Diagnostics.ProcessStartInfo(), toolchain, "somewhere"));

        Assert.Contains("configuration-home variable", error.Message);
    }
}
