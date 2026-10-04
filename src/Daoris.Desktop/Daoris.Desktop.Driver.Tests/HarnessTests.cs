using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The toolchain (D49 §4): which harnesses this machine has, and which ACCOUNT a session runs as.
///
/// <para>The whole of it rests on one sentence from the design: <b>Daoris manages directories and
/// names, and a sign-in stays the tool's</b> (an API key a person gives is the one secret Daoris
/// keeps, D67 §1 — <c>ApiKeyAccountTests</c>). A profile is a directory Daoris owns the location of; the credential
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

    /// <summary>
    /// An account made by signing in (D66 §3) is made before anyone knows whose it is, so it takes a fresh id (ACCT2) —
    /// twin rule 5, and the CLI's <c>login --new</c> draws the same way. It no longer takes the first free
    /// <c>account-N</c>: removing <c>account-2</c> left <c>account-1</c> and <c>account-3</c>, and the next account made would
    /// have taken a removed one's name, its readings and its usage. Who signed in is offered as its name, never its folder's.
    /// </summary>
    [Fact]
    public void A_new_account_takes_a_fresh_id_and_never_a_removed_one_s_name()
    {
        Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "claude-code", "account-1"));
        Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "claude-code", "account-3"));

        var made = HarnessSettings.NextAccount(_home, "claude-code");

        Assert.Matches("^acct-[0-9a-f]{8}$", made);
        Assert.NotEqual("account-2", made);
        Assert.DoesNotContain(made, HarnessSettings.Profiles(_home, "claude-code"));
    }

    /// <summary>
    /// 🔴 Removing an account deletes its directory, credentials and all (D66 §3) — and a tool that
    /// clones into its home leaves READ-ONLY files there, which a plain recursive delete refuses on
    /// Windows. The account the person removed must not survive on disk because of an attribute.
    /// </summary>
    [Fact]
    public void Removing_an_account_deletes_its_directory_read_only_files_and_all()
    {
        var account = HarnessSettings.ProfileHome(_home, "claude-code", "work");
        var nested = System.IO.Path.Combine(account, "plugins", "cloned", ".git", "objects");
        Directory.CreateDirectory(nested);
        File.WriteAllText(System.IO.Path.Combine(account, ".credentials.json"), "{}");
        var locked = System.IO.Path.Combine(nested, "pack.idx");
        File.WriteAllText(locked, "x");
        File.SetAttributes(locked, FileAttributes.ReadOnly);

        Assert.True(HarnessSettings.RemoveProfile(_home, "claude-code", "work"));

        Assert.False(Directory.Exists(account));
        Assert.Empty(HarnessSettings.Profiles(_home, "claude-code"));
    }

    /// <summary>Removing one that is not there is an answer, not a failure — and says so.</summary>
    [Fact]
    public void Removing_an_account_that_is_not_there_is_an_answer()
    {
        Assert.False(HarnessSettings.RemoveProfile(_home, "claude-code", "nobody"));
    }

    /// <summary>
    /// The name is refused before anything is deleted: a remove is the one verb here where a name
    /// that points somewhere else would destroy something that is not Daoris's.
    /// </summary>
    [Fact]
    public void A_remove_naming_somewhere_else_is_refused_before_it_deletes_anything()
    {
        Directory.CreateDirectory(_home);
        var outside = System.IO.Path.Combine(_home, "keep.txt");
        File.WriteAllText(outside, "mine");

        Assert.Throws<DriverException>(() => HarnessSettings.RemoveProfile(_home, "claude-code", ".."));
        Assert.True(File.Exists(outside));
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

    /// <summary>
    /// Twin rule 4 (TOOL2/D57): the binary is the explicit command, then the managed pin, then
    /// <c>PATH</c> — and the pin resolves by exactly the rule a profile does.
    /// </summary>
    [Fact]
    public void A_pin_is_the_pick_then_the_workspace_then_the_machine_then_none()
    {
        var settings = new HarnessSettings()
            .WithVersion("claude-code", "1.2.3")
            .WithWorkspaceVersion("aurora", "claude-code", "2.0.0");

        Assert.Equal("9.9.9", settings.ResolveVersion("claude-code", "aurora", "9.9.9"));
        Assert.Equal("2.0.0", settings.ResolveVersion("claude-code", "aurora", null));
        Assert.Equal("1.2.3", settings.ResolveVersion("claude-code", "tools", null));
        Assert.Equal("1.2.3", settings.ResolveVersion("claude-code", null, null));
        Assert.Null(settings.ResolveVersion("codex", "aurora", null));
    }

    /// <summary>
    /// 🔴 The rule that must never regress, and the twin of "no profile means the harness's own
    /// home": nothing pinned means whatever the machine has on <c>PATH</c> (D48 §2a).
    /// </summary>
    [Fact]
    public void Nothing_pinned_means_whatever_the_machine_has()
    {
        Assert.Null(new HarnessSettings().ResolveVersion("claude-code", "aurora", null));
        Assert.Null(HarnessSettings.ManagedBinary(_home, "claude-code", null, ["claude"]));
    }

    /// <summary>
    /// 🔴 <b>The pin must survive a write from this side.</b> The CLI writes `versions` and this
    /// artefact writes the same file; a `Save` that knew only about profiles would silently DELETE
    /// somebody's pin, which is the exact shape of counterpart-set rot — it compiles, it passes every
    /// test about profiles, and it loses data.
    /// </summary>
    [Fact]
    public void The_pins_round_trip_through_the_file_beside_the_profiles()
    {
        new HarnessSettings()
            .WithDefault("claude-code", "personal")
            .WithVersion("claude-code", "1.2.3")
            .WithWorkspaceVersion("aurora", "claude-code", "2.0.0")
            .Save(Path_);

        var read = HarnessSettings.Load(Path_);

        Assert.Equal("1.2.3", read.ResolveVersion("claude-code", null, null));
        Assert.Equal("2.0.0", read.ResolveVersion("claude-code", "aurora", null));
        // And the profile beside it is untouched, which is the other half of the same worry.
        Assert.Equal("personal", read.Resolve("claude-code", null, null));
    }

    /// <summary>
    /// A pin naming a version nobody installed answers null, so the caller falls back to PATH — and
    /// the surfaces say so rather than implying the pin is in force.
    /// </summary>
    [Fact]
    public void A_pin_with_nothing_installed_at_it_resolves_to_nothing()
    {
        Assert.Null(HarnessSettings.ManagedBinary(_home, "claude-code", "9.9.9", ["claude"]));

        var bin = System.IO.Path.Combine(
            HarnessSettings.ManagedHome(_home, "claude-code", "1.2.3"), "node_modules", ".bin");
        Directory.CreateDirectory(bin);
        var shim = System.IO.Path.Combine(bin, OperatingSystem.IsWindows() ? "claude.cmd" : "claude");
        File.WriteAllText(shim, "");

        Assert.Equal(shim, HarnessSettings.ManagedBinary(_home, "claude-code", "1.2.3", ["claude"]));
    }

    /// <summary>A version becomes a directory name, so it takes the same refusal a profile does.</summary>
    [Fact]
    public void A_version_that_would_escape_the_toolchain_directory_is_refused()
    {
        foreach (var version in new[] { "../escape", "a/b", "a\\b", "", "  ", ".", ".." })
        {
            Assert.ThrowsAny<Exception>(
                () => HarnessSettings.ManagedHome(_home, "claude-code", version));
        }
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

    /// <summary>
    /// 🔴 REV3 CLEAN1 — the driver's half of CLI F4. Read as empty is right for DRIVING; an EDIT made
    /// over that empty read wrote it back, and every default, workspace choice and pin the person had
    /// in the file was gone. The CLI refuses the same edit, and so does this.
    /// </summary>
    [Fact]
    public void An_edit_over_wiring_that_could_not_be_read_is_refused_and_the_file_is_kept()
    {
        Directory.CreateDirectory(_home);
        const string held = "{ \"defaults\": { \"claude-code\": \"work\" }, torn";
        File.WriteAllText(Path_, held);

        var edited = HarnessSettings.Load(Path_).WithDefault("codex", "home");
        var refused = Assert.Throws<DriverException>(() => edited.Save(Path_));

        Assert.Contains("could not be read", refused.Message);
        Assert.Equal(held, File.ReadAllText(Path_));
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
[Trait(Category.Name, Category.Process)]
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
        // 🔴 The whole sentence, and only it. The deployed application's roster showed this followed
        // by the runtime's own words — "An error occurred trying to start process 'dsh' with working
        // directory '<the probe's cwd>'. The system cannot find the file specified." — a machine
        // path and a second, worse sentence for the one fact the first already states.
        Assert.Equal("`daoris-no-such-binary-anywhere` is not on this machine's PATH", report.Problem);
    }

    /// <summary>
    /// 🔴 The account a person actually has is the tool's OWN configuration home, and the roster
    /// never said a word about it: a machine with no named profile read "No accounts" while its
    /// owner was logged in. The probe asks the harness about its own home exactly as it asks about
    /// each profile — read-only, one boolean — and reports it beside them.
    /// </summary>
    [Fact]
    public async Task The_tool_s_own_home_is_asked_about_logging_in_like_any_profile()
    {
        var report = await HarnessProbe.ProbeAsync(
            "fake", Toolchain(FakeBinary()), command: null, new HarnessSettings(), _home);

        // The fake answers from FAKE_HARNESS_HOME, which the probe leaves unset for the tool's own
        // home — so the tool answers about wherever IT keeps its credential, and here that is "out".
        Assert.Equal(LoginState.Out, report.OwnLogin);
    }

    [Fact]
    public async Task An_absent_harness_has_no_own_login_to_report()
    {
        var toolchain = new HarnessToolchain(
            Binary: ["daoris-no-such-binary-anywhere"], VersionArguments: ["--version"]);

        var report = await HarnessProbe.ProbeAsync(
            "fake", toolchain, command: null, new HarnessSettings(), _home);

        Assert.Equal(LoginState.Unknown, report.OwnLogin);
    }

    /// <summary>
    /// 🔴 A fixture is not a tool. The stub declares a toolchain with no binary of its own — the
    /// "binary" is whatever `driver.json` names, which is what lets the rehearsal gate the whole
    /// roster with no model — and the deployed application, which names nothing, listed it as an
    /// agent tool called <c>stub</c> with an install button and nothing to install. A door with
    /// nothing to run on this machine is off the roster; name a command and it is on it.
    /// </summary>
    [Fact]
    public async Task A_door_with_nothing_to_run_on_this_machine_is_not_on_the_roster()
    {
        var roster = new HarnessRoster(
            new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
            {
                ["stub"] = new StubAdapter(),
            }),
            Path.Combine(_home, "harnesses.json"));

        Assert.Empty(await roster.RosterAsync(DriverConfig.Empty));

        var named = DriverConfig.Empty with
        {
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["stub"] = [FakeBinary()] },
        };
        Assert.Equal(["stub"], (await roster.RosterAsync(named)).Select(r => r.Adapter));
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
    /// A harness that volunteers an email, an organisation and a subscription tier when asked whether
    /// it is logged in — the shape <c>claude auth status</c> answers in. <paramref name="signedIn"/>
    /// flips only the boolean; the rest is printed either way.
    /// </summary>
    private HarnessToolchain Chatty(bool signedIn, string? account)
    {
        Directory.CreateDirectory(_home);
        var script = Path.Combine(_home, $"chatty-{signedIn}.mjs");
        File.WriteAllText(script, $$"""
            if (process.argv[2] === '--version') { console.log('chatty 1.0'); process.exit(0); }
            console.log(JSON.stringify({ loggedIn: {{(signedIn ? "true" : "false")}}, email: 'someone@example.invalid', orgName: 'Secretive', subscriptionType: 'max' }));
            """);
        return new HarnessToolchain(
            Binary: ["node", script], VersionArguments: ["--version"],
            ProfileVariable: "CHATTY_HOME",
            LoginCheck: new LoginQuestion(
                ["auth"], @"""loggedIn""\s*:\s*true", @"""loggedIn""\s*:\s*false", account));
    }

    private static string Everything(HarnessReport report) =>
        string.Join("|", report.Profiles.Select(p => $"{p.Name}:{p.Home}:{p.Login}:{p.Account}"))
        + $"|{report.Version}|{report.Problem}|{report.OwnLogin}|{report.OwnAccount}";

    /// <summary>
    /// 🔴 <b>Who is signed in, and nothing else</b> (D66 §3). The owner asked for an account to be
    /// named by who signed in rather than by a name typed before anyone knew — so the one thing
    /// besides the boolean the probe now takes is the name the tool gives, when its toolchain asks
    /// for it and the answer is yes. The organisation and the tier are still never kept.
    /// </summary>
    [Fact]
    public async Task Who_is_signed_in_is_taken_when_the_toolchain_asks_and_nothing_else_is()
    {
        Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "fake", "account-1"));

        var report = await HarnessProbe.ProbeAsync(
            "fake", Chatty(signedIn: true, account: @"""email""\s*:\s*""([^""]+)"""),
            command: null, new HarnessSettings(), _home);

        Assert.Equal(LoginState.In, report.Profiles.Single().Login);
        Assert.Equal("someone@example.invalid", report.Profiles.Single().Account);
        // The tool's own home is asked the same way, so it is named the same way.
        Assert.Equal("someone@example.invalid", report.OwnAccount);
        Assert.DoesNotContain("Secretive", Everything(report));
        Assert.DoesNotContain("max", Everything(report));
    }

    /// <summary>
    /// The real declaration, against the shape <c>claude auth status</c> answers in — keys measured
    /// on the real binary (2026-09-23), values invented here.
    /// </summary>
    [Fact]
    public void Claude_code_names_who_signed_in_by_the_email_it_reports()
    {
        var question = new ClaudeCodeAdapter().Toolchain!.LoginCheck!;
        const string answer = """
            {
              "loggedIn": true,
              "authMethod": "claude.ai",
              "email": "someone@example.invalid",
              "orgName": "Secretive",
              "subscriptionType": "max"
            }
            """;

        var match = System.Text.RegularExpressions.Regex.Match(answer, question.Account!);

        Assert.True(match.Success);
        Assert.Equal("someone@example.invalid", match.Groups[1].Value);
    }

    /// <summary>A toolchain that does not ask who is signed in keeps no name at all.</summary>
    [Fact]
    public async Task A_toolchain_that_does_not_ask_who_keeps_no_name()
    {
        Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "fake", "work"));

        var report = await HarnessProbe.ProbeAsync(
            "fake", Chatty(signedIn: true, account: null), command: null, new HarnessSettings(), _home);

        Assert.Equal(LoginState.In, report.Profiles.Single().Login);
        Assert.Null(report.Profiles.Single().Account);
        Assert.DoesNotContain("example.invalid", Everything(report));
    }

    /// <summary>
    /// A signed-OUT home names nobody, whatever the tool prints beside its "no": the name is who is
    /// signed in there, and nobody is.
    /// </summary>
    [Fact]
    public async Task A_signed_out_home_names_nobody()
    {
        Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "fake", "stale"));

        var report = await HarnessProbe.ProbeAsync(
            "fake", Chatty(signedIn: false, account: @"""email""\s*:\s*""([^""]+)"""),
            command: null, new HarnessSettings(), _home);

        Assert.Equal(LoginState.Out, report.Profiles.Single().Login);
        Assert.Null(report.Profiles.Single().Account);
        Assert.Null(report.OwnAccount);
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

    /// <summary>
    /// 🔴 <b>The probe asks about the binary a spawn would actually run</b> — rule 4 of the twin
    /// contract, at the presence question rather than only at the spawn.
    /// </summary>
    /// <remarks>
    /// Measured: the selector resolved a pin correctly and then vetoed it on a presence answer
    /// computed from <c>PATH</c>, so a working pinned harness refused to spawn as "not installed on
    /// this machine". The CLI twin had the same defect and reported the machine's own binary as the
    /// pinned one. Both halves of a substitution the pin exists to prevent.
    /// </remarks>
    [Fact]
    public async Task A_pinned_harness_is_probed_on_its_pin_rather_than_on_PATH()
    {
        var script = FakeBinary();
        var bin = Path.Combine(HarnessSettings.ManagedHome(_home, "fake", "1.2.3"), "node_modules", ".bin");
        Directory.CreateDirectory(bin);
        // npm's layout, and a shim that really runs: the managed install stands in for the pin.
        var windows = OperatingSystem.IsWindows();
        var shim = Path.Combine(bin, windows ? "pinned.cmd" : "pinned");
        File.WriteAllText(shim, windows
            ? $"@node \"{script}\" %*\r\n"
            : $"#!/bin/sh\nexec node \"{script}\" \"$@\"\n");
        if (!windows) File.SetUnixFileMode(shim, UnixFileMode.UserRead | UnixFileMode.UserExecute);

        var toolchain = new HarnessToolchain(
            Binary: ["pinned"], VersionArguments: ["--version"], ProfileVariable: "FAKE_HARNESS_HOME");
        var settings = new HarnessSettings(Versions: new Dictionary<string, string> { ["fake"] = "1.2.3" });

        var report = await HarnessProbe.ProbeAsync("fake", toolchain, command: null, settings, _home);

        Assert.True(report.Present, report.Problem);
        Assert.Equal("fake-harness 9.9.9", report.Version);
    }

    /// <summary>
    /// The other side of rule 4: a pin with nothing installed at it is ABSENT, naming the version —
    /// never a silent fall back to whatever <c>PATH</c> happens to hold.
    /// </summary>
    [Fact]
    public async Task A_pin_with_nothing_installed_at_it_is_absent_rather_than_PATH()
    {
        var script = FakeBinary();
        var toolchain = new HarnessToolchain(Binary: ["node", script], VersionArguments: ["--version"]);
        var settings = new HarnessSettings(Versions: new Dictionary<string, string> { ["fake"] = "9.9.9" });

        var report = await HarnessProbe.ProbeAsync("fake", toolchain, command: null, settings, _home);

        Assert.False(report.Present);
        Assert.Null(report.Version);
        Assert.Contains("9.9.9", report.Problem);
    }
}

/// <summary>
/// The judgement half (D49 §4): may this spawn happen, and as which account? <b>One implementation
/// for both doors</b> — the driven loop and a conversation ask the same question of the same object,
/// because two copies of "may this start" drift and the drift is invisible.
/// </summary>
[Trait(Category.Name, Category.Process)]
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
    private sealed class FakeAdapter(HarnessToolchain? toolchain, SessionWire wire = SessionWire.Pipe)
        : ISessionAdapter
    {
        public string Name => "fake";

        /// <summary>Which door — the protocol one asks for the Agent SDK's executable seam (ACP2).</summary>
        public SessionWire Wire => wire;

        public System.Diagnostics.ProcessStartInfo Prepare(
            SessionTarget target, IReadOnlyList<string>? command) => new();

        public HarnessToolchain? Toolchain => toolchain;
    }

    private static AdapterSet Set(HarnessToolchain? toolchain, SessionWire wire = SessionWire.Pipe) =>
        new(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
        {
            ["fake"] = new FakeAdapter(toolchain, wire),
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

    /// <summary>
    /// 🔴 <b>The additive case for the toolchain</b> (TOOL2/D57), and the twin of the one above.
    /// Nothing pinned means the spawn runs whatever is on <c>PATH</c> — the selection names no
    /// binary at all, so nothing overrides what the adapter built.
    /// </summary>
    [Fact]
    public async Task With_no_version_pinned_the_spawn_runs_what_the_machine_has()
    {
        var roster = new HarnessRoster(Set(Toolchain(Present())), Settings);

        var selection = await roster.SelectAsync("fake", Config(), workspace: "aurora", chosen: null);

        Assert.True(selection.Allowed);
        Assert.Null(selection.Binary);
    }

    /// <summary>A pin with an install behind it is what the spawn runs, rather than `PATH`.</summary>
    [Fact]
    public async Task A_pinned_version_is_the_binary_the_spawn_runs()
    {
        new HarnessSettings().WithVersion("fake", "1.2.3").Save(Settings);
        var shim = Install("1.2.3");

        var selection = await new HarnessRoster(Set(Toolchain(Present())), Settings)
            .SelectAsync("fake", Config(), workspace: null, chosen: null);

        Assert.True(selection.Allowed);
        Assert.Equal(shim, selection.Binary);
    }

    /// <summary>
    /// 🔴 A pin nobody installed <b>refuses the spawn</b> rather than quietly running `PATH`. Falling
    /// back would run a different tool than the one the person asked for and report success — and
    /// the version on the record would then be a claim about the wrong binary.
    /// </summary>
    [Fact]
    public async Task A_pin_with_nothing_installed_at_it_refuses_and_says_how_to_fix_it()
    {
        new HarnessSettings().WithVersion("fake", "9.9.9").Save(Settings);

        var selection = await new HarnessRoster(Set(Toolchain(Present())), Settings)
            .SelectAsync("fake", Config(), workspace: null, chosen: null);

        Assert.False(selection.Allowed);
        Assert.Contains("9.9.9", selection.Refusal);
        // Every refusal in this class names the action that fixes it.
        Assert.Contains("pin", selection.Refusal);
    }

    /// <summary>
    /// The explicit command outranks the pin: a person naming exactly what to run in `driver.json`
    /// has said the last word, and a pin quietly replacing it would be the surface overruling them.
    /// </summary>
    [Fact]
    public async Task An_explicit_command_outranks_the_pin()
    {
        new HarnessSettings().WithVersion("fake", "1.2.3").Save(Settings);
        Install("1.2.3");

        var config = Config() with
        {
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["fake"] = ["node", "elsewhere.mjs"] },
        };
        var selection = await new HarnessRoster(Set(Toolchain(Present())), Settings)
            .SelectAsync("fake", config, workspace: null, chosen: null);

        Assert.True(selection.Allowed);
        Assert.Null(selection.Binary);
    }

    /// <summary>A circle's pin outranks the machine's, exactly as its profile does.</summary>
    [Fact]
    public async Task A_workspace_pin_outranks_the_machine_s()
    {
        new HarnessSettings()
            .WithVersion("fake", "1.2.3")
            .WithWorkspaceVersion("aurora", "fake", "2.0.0")
            .Save(Settings);
        Install("1.2.3");
        var theirs = Install("2.0.0");

        var selection = await new HarnessRoster(Set(Toolchain(Present())), Settings)
            .SelectAsync("fake", Config(), workspace: "aurora", chosen: null);

        Assert.Equal(theirs, selection.Binary);
    }

    /// <summary>
    /// 🔴 The ACP adapter's <b>executable seam</b> (ACP2, §1a): a protocol-door spawn is told which
    /// `claude` to run, read from the <b>pipe door's</b> pin — because the CLI and the account belong
    /// to `claude-code`, and the ACP adapter is a separate package that merely runs it.
    /// </summary>
    [Fact]
    public async Task A_protocol_door_spawn_is_told_which_claude_to_run()
    {
        var claudeBin = Path.Combine(
            HarnessSettings.ManagedHome(_home, "claude-code", "2.1.278"), "node_modules", ".bin");
        Directory.CreateDirectory(claudeBin);
        var claude = Path.Combine(claudeBin, OperatingSystem.IsWindows() ? "claude.cmd" : "claude");
        File.WriteAllText(claude, "");
        new HarnessSettings().WithVersion("claude-code", "2.1.278").Save(Settings);

        var selection = await new HarnessRoster(Set(Toolchain(Present()), SessionWire.Acp), Settings)
            .SelectAsync("fake", Config(), workspace: null, chosen: null);

        Assert.True(selection.Allowed);
        Assert.Equal(claude, selection.ClaudeExecutable);
    }

    /// <summary>
    /// The pipe door is never told, because it does not run the Agent SDK — it IS the CLI. A machine
    /// driving the old way must not acquire an environment variable because a new door exists.
    /// </summary>
    [Fact]
    public async Task A_pipe_door_spawn_is_told_nothing_about_an_executable()
    {
        new HarnessSettings().WithVersion("claude-code", "2.1.278").Save(Settings);

        var selection = await new HarnessRoster(Set(Toolchain(Present())), Settings)
            .SelectAsync("fake", Config(), workspace: null, chosen: null);

        Assert.Null(selection.ClaudeExecutable);
    }

    /// <summary>
    /// With no `claude` pinned the seam is null, so the SDK finds its own — the additive rule again
    /// (D48 §2a). A protocol-door session on a machine that pinned nothing works exactly as it would.
    /// </summary>
    [Fact]
    public async Task With_no_claude_pinned_the_protocol_door_lets_the_sdk_find_its_own()
    {
        var selection = await new HarnessRoster(Set(Toolchain(Present()), SessionWire.Acp), Settings)
            .SelectAsync("fake", Config(), workspace: null, chosen: null);

        Assert.True(selection.Allowed);
        Assert.Null(selection.ClaudeExecutable);
    }

    /// <summary>A managed install, as npm would leave it.</summary>
    /// <summary>
    /// A managed install of the fake harness at one version.
    /// </summary>
    /// <remarks>
    /// 🔴 The shim has to RUN. It used to be an empty file, which was enough while only the spawn
    /// resolved a pin — but the probe now asks the pinned binary its version (rule 4 at the presence
    /// question), and a file that cannot execute makes a pinned harness report absent. An empty
    /// fixture standing in for "an install exists" stopped being true the moment anything ran it.
    /// </remarks>
    private string Install(string version)
    {
        var bin = Path.Combine(
            HarnessSettings.ManagedHome(_home, "fake", version), "node_modules", ".bin");
        Directory.CreateDirectory(bin);
        var windows = OperatingSystem.IsWindows();
        var shim = Path.Combine(bin, windows ? "node.cmd" : "node");
        File.WriteAllText(shim, windows ? "@node %*\r\n" : "#!/bin/sh\nexec node \"$@\"\n");
        if (!windows) File.SetUnixFileMode(shim, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        return shim;
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
        Assert.Contains("daoris agent install fake", selection.Refusal);
    }

    /// <summary>
    /// The logged-out refusal mirrors the missing-harness one deliberately: same shape, same
    /// before-the-record timing, same naming of the action that fixes it. Once a reading said so: the person's press here,
    /// since a look reads no account (ROSTER1).
    /// </summary>
    [Fact]
    public async Task A_logged_out_profile_refuses_naming_the_login_action()
    {
        Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "fake", "fresh"));
        new HarnessSettings().WithDefault("fake", "fresh").Save(Settings);

        var roster = new HarnessRoster(Set(Toolchain(Present())), Settings);
        await roster.ReportAsync("fake", Config(), refresh: true);
        var selection = await roster.SelectAsync("fake", Config(), null, null);

        Assert.False(selection.Allowed);
        Assert.Contains("not signed in", selection.Refusal);
        Assert.Contains("daoris agent login fake --profile fresh", selection.Refusal);
        // And it says where the credential lives, because the obvious worry is that Daoris took it.
        Assert.Contains("agent's own store", selection.Refusal);
    }

    /// <summary>
    /// <b>A cached "no" is asked again before it is given.</b> Otherwise the person does exactly what
    /// the refusal told them to and the driver keeps refusing until somebody restarts it — which is
    /// the worst possible reading of a message that named an action. Asked once per sign-out (TOOL6g):
    /// a sign-in through Daoris's doors marks the account, and a start then asks it again.
    /// </summary>
    [Fact]
    public async Task A_refusal_is_re_checked_so_logging_in_takes_effect_without_a_restart()
    {
        var home = HarnessSettings.ProfileHome(_home, "fake", "fresh");
        Directory.CreateDirectory(home);
        new HarnessSettings().WithDefault("fake", "fresh").Save(Settings);
        var now = DateTimeOffset.UtcNow;

        var roster = new HarnessRoster(Set(Toolchain(Present())), Settings) { Clock = () => now };
        await roster.ReportAsync("fake", Config(), refresh: true);
        Assert.False((await roster.SelectAsync("fake", Config(), null, null)).Allowed);

        // The person logs in at their terminal, which marks the sign-in. Nothing restarts.
        File.WriteAllText(Path.Combine(home, "credentials.json"), "{}");
        ProbeLock.MarkSignedIn(home, now);
        File.SetLastWriteTimeUtc(ProbeLock.SignedInPathOf(_home, "fake", "fresh"), now.AddSeconds(1).UtcDateTime);

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
