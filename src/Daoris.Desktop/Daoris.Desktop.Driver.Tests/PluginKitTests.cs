using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// PLUG8: the kit a session makes a plugin with. `new` writes a folder a plugins repository can hold —
/// a manifest, a wire script, its self-contained wire test and a README — and `try` starts a plugin as
/// the driver would and checks every answer by the driver's own reader.
/// </summary>
/// <remarks>
/// 🔴 <b>Nothing here pushes or reaches a network.</b> The plugins these tests write answer from their
/// own folder, and the one that runs git runs it where the kit's sample frame says the repository is: an
/// empty scratch folder git is told holds none.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class PluginKitTests : IDisposable
{
    private readonly string _scratch = Path.Combine(
        Path.GetTempPath(), "daoris-kit-" + Guid.NewGuid().ToString("N")[..8]);

    public PluginKitTests() => Directory.CreateDirectory(_scratch);

    /// <summary>The Daoris home a trial keeps its scratch under (D63).</summary>
    private string Home => Path.Combine(_scratch, "home");

    public void Dispose()
    {
        // A plugin's process stands in its folder a moment after it is told to go (FLAKE1).
        for (var attempt = 0; Directory.Exists(_scratch); attempt++)
        {
            try
            {
                Directory.Delete(_scratch, recursive: true);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                if (attempt >= 20) return;
                Thread.Sleep(250);
            }
        }
    }

    // ——— the points, and the frames the samples are

    [Fact]
    public void The_kit_has_every_point_the_driver_has_in_the_driver_s_order()
    {
        Assert.Equal(HookPoints.All, PluginKit.Points.Select(point => point.Name));
        Assert.Equal(["decision", "observation", "act"], PluginKit.Points.Select(point => point.Kind));
    }

    [Fact]
    public void Each_point_waits_as_long_as_the_driver_waits_for_it()
    {
        Assert.Equal(TimeSpan.FromSeconds(10), PluginKit.Find(HookPoints.QuestConsider)!.Patience);
        Assert.Equal(TimeSpan.FromSeconds(10), PluginKit.Find(HookPoints.SessionEnded)!.Patience);
        Assert.Equal(LandingPlugins.DefaultPatience, PluginKit.Find(HookPoints.Land)!.Patience);
    }

    /// <summary>
    /// The sample frame at each point IS the frame the driver sends there, for the kit's sample input: the
    /// loop's waterfall and observation and the landing each hand their payload to a channel, and what they
    /// hand is compared with the kit's table. A point whose frame the driver changes changes here with it.
    /// </summary>
    [Fact]
    public async Task Each_sample_frame_is_what_the_driver_sends_at_that_point()
    {
        var told = new List<object>();
        var channel = new RecordingChannel(HookPoints.All, told);
        var home = Path.Combine(_scratch, "home");
        InstallManifest(home, "acme.every", HookPoints.All);

        await using (var set = new HookSet(home, start: (_, _, _) => Task.FromResult<IHookChannel>(channel)))
        {
            await set.ReconcileAsync(PluginCatalog.Load(home), CancellationToken.None);
            await set.ConsiderAsync(PluginKit.SampleConsideration, CancellationToken.None);
            await set.EndedAsync(PluginKit.SampleEnding, CancellationToken.None);
        }

        await new LandingPlugins(home, start: (_, _, _) => Task.FromResult<IHookChannel>(new RecordingChannel(HookPoints.All, told)))
            .LandAsync("acme.every", PluginKit.SampleLanding);

        Assert.Equal(3, told.Count);
        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(
                JsonSerializer.SerializeToNode(told[i])!.ToJsonString(),
                PluginKit.Points[i].Frame.ToJsonString());
        }

        // The repository's checkout in a sample is the kit's marker, which try and the wire test replace
        // with an empty scratch folder — never a path on anybody's machine.
        Assert.Equal(PluginKit.SampleRoot, PluginKit.Find(HookPoints.QuestConsider)!.Frame["root"]!.GetValue<string>());
        Assert.Equal(PluginKit.SampleRoot, PluginKit.Find(HookPoints.Land)!.Frame["root"]!.GetValue<string>());
    }

    // ——— new

    [Fact]
    public void A_new_plugin_is_a_folder_with_a_manifest_a_wire_script_its_test_and_a_readme()
    {
        var plan = PluginKit.Plan("acme.quiet-hours", [HookPoints.QuestConsider], _scratch);
        var written = PluginKit.Write(plan);

        var folder = Path.Combine(_scratch, "acme.quiet-hours");
        Assert.Equal(folder, plan.Folder);
        Assert.Equal(["plugin.json", "plugin.mjs", "plugin.test.mjs", "README.md"], written);
        Assert.Equal(written.OrderBy(n => n, StringComparer.Ordinal), Directory.GetFiles(folder).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal));

        // The manifest is one the catalogue reads as sound, speaking where it was asked to.
        var (manifest, problem) = PluginCatalog.ReadFolder(folder);
        Assert.Null(problem);
        Assert.Equal("acme.quiet-hours", manifest.Id);
        Assert.Equal("Quiet hours", manifest.Name);
        Assert.Equal([HookPoints.QuestConsider], manifest.Hooks!.Points);
        Assert.Equal(["node", Path.Combine(folder, "plugin.mjs")], manifest.Hooks.Command.Select(Path.GetFullPath).Skip(1).Prepend("node"));
        Assert.Empty(manifest.Harnesses);

        // Every file is BOM-less UTF-8 with LF, whatever the checkout did to the templates.
        foreach (var name in written)
        {
            var bytes = File.ReadAllBytes(Path.Combine(folder, name));
            Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, $"{name} has a BOM");
            Assert.DoesNotContain("\r", Encoding.UTF8.GetString(bytes));
        }

        // The wire script answers the point it was made for, and only that one.
        var script = File.ReadAllText(Path.Combine(folder, "plugin.mjs"));
        Assert.Contains("'quest/consider'", script);
        Assert.DoesNotContain("'work/land'", script);
        // The README carries the frame the point is sent and what it must answer.
        var readme = File.ReadAllText(Path.Combine(folder, "README.md"));
        Assert.Contains("acme.quiet-hours", readme);
        Assert.Contains("\"quest\"", readme);
        Assert.Contains("\"kind\": \"hold\"", readme);
        Assert.Contains("node --test", readme);
        Assert.DoesNotContain("{{", readme + script + File.ReadAllText(Path.Combine(folder, "plugin.test.mjs")));
    }

    [Fact]
    public void A_plan_writes_nothing()
    {
        var plan = PluginKit.Plan("acme.quiet-hours", [HookPoints.QuestConsider, HookPoints.QuestConsider], _scratch);

        Assert.False(Directory.Exists(plan.Folder));
        // A point named twice is one point.
        Assert.Contains("\"points\": [\"quest/consider\"]", plan.Files.Single(f => f.Name == "plugin.json").Content);
    }

    [Fact]
    public void A_landing_plugin_carries_the_helper_that_runs_a_platform_s_tool_and_its_quoting()
    {
        var plan = PluginKit.Plan("acme.lands", [HookPoints.Land], _scratch);
        var script = plan.Files.Single(f => f.Name == "plugin.mjs").Content;

        Assert.Contains("'work/land'", script);
        Assert.Contains("function run(", script);
        // cmd reads a `.cmd` tool's line a second time, so what it would act on inside quotes is refused.
        Assert.Contains("[\"%\\r\\n]", script);
        Assert.DoesNotContain("function run(", PluginKit.Plan("acme.gate", [HookPoints.QuestConsider], _scratch).Files.Single(f => f.Name == "plugin.mjs").Content);
    }

    [Theory]
    [InlineData("Acme")]
    [InlineData("../up")]
    [InlineData(".hidden")]
    [InlineData("a b")]
    [InlineData("")]
    public void A_name_that_is_not_a_plugin_id_is_refused_and_nothing_is_written(string id)
    {
        var refused = Assert.Throws<DriverException>(() => PluginKit.Plan(id, [HookPoints.QuestConsider], _scratch));

        Assert.Contains("is not a plugin id", refused.Message);
        Assert.Empty(Directory.GetFileSystemEntries(_scratch));
    }

    [Fact]
    public void A_point_this_build_lacks_or_none_at_all_is_refused_naming_the_points()
    {
        var unknown = Assert.Throws<DriverException>(() => PluginKit.Plan("acme.gate", ["quest/started"], _scratch));
        Assert.Contains("`quest/started` is not a point", unknown.Message);
        Assert.Contains("quest/consider, session/ended, work/land", unknown.Message);

        var none = Assert.Throws<DriverException>(() => PluginKit.Plan("acme.gate", [], _scratch));
        Assert.Contains("at least one point", none.Message);
        Assert.Empty(Directory.GetFileSystemEntries(_scratch));
    }

    [Fact]
    public void A_folder_that_holds_anything_is_refused_rather_than_written_over()
    {
        var folder = Path.Combine(_scratch, "acme.gate");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "plugin.mjs"), "// somebody's work");

        var refused = Assert.Throws<DriverException>(() => PluginKit.Plan("acme.gate", [HookPoints.QuestConsider], _scratch));

        Assert.Contains("already holds", refused.Message);
        Assert.Equal("// somebody's work", File.ReadAllText(Path.Combine(folder, "plugin.mjs")));
        Assert.Single(Directory.GetFiles(folder));
    }

    [Fact]
    public void A_folder_that_does_not_exist_is_refused_rather_than_made()
    {
        var nowhere = Path.Combine(_scratch, "no-such-folder");

        var refused = Assert.Throws<DriverException>(() => PluginKit.Plan("acme.gate", [HookPoints.QuestConsider], nowhere));

        Assert.Contains("does not exist", refused.Message);
        Assert.False(Directory.Exists(nowhere));
    }

    [Fact]
    public void An_empty_folder_of_the_plugin_s_name_is_written_into()
    {
        Directory.CreateDirectory(Path.Combine(_scratch, "acme.gate"));

        PluginKit.Write(PluginKit.Plan("acme.gate", [HookPoints.QuestConsider], _scratch));

        Assert.True(File.Exists(Path.Combine(_scratch, "acme.gate", "plugin.json")));
    }

    // ——— a fresh plugin passes its own test, and try

    public static TheoryData<string[]> PointSets => new()
    {
        { new[] { HookPoints.QuestConsider } },
        { new[] { HookPoints.SessionEnded } },
        { new[] { HookPoints.Land } },
        { new[] { HookPoints.QuestConsider, HookPoints.SessionEnded, HookPoints.Land } },
    };

    /// <summary>
    /// The gate a session in another repository can run: `node --test` in the folder, with nothing of
    /// Daoris's installed — the test starts the plugin, speaks the handshake, every point's frame and the
    /// shutdown, and checks each answer.
    /// </summary>
    [Theory]
    [MemberData(nameof(PointSets))]
    public async Task A_fresh_plugin_passes_its_own_wire_test(string[] points)
    {
        var folder = New("acme.fresh", points);

        var (exit, output) = await NodeTestAsync(folder);

        Assert.True(exit == 0, output);
        Assert.Contains("# fail 0", output);
        Assert.Matches(@"# pass [1-9]", output);
    }

    [Theory]
    [MemberData(nameof(PointSets))]
    public async Task A_fresh_plugin_passes_try_at_every_point_it_declares(string[] points)
    {
        var folder = New("acme.fresh", points);

        var trial = await PluginKit.TryFolderAsync(Home, folder);

        Assert.True(trial.Passed, Describe(trial));
        Assert.Equal(0, trial.ExitCode);
        Assert.Equal("acme.fresh", trial.Plugin);
        Assert.Equal(["handshake", .. points, "shutdown", "stdout"], trial.Steps.Select(step => step.Name));
        Assert.Contains("listens on", trial.Steps[0].Sentence);
        Assert.Contains("left when told", trial.Steps.Single(step => step.Name == "shutdown").Sentence);
    }

    // ——— try says what went wrong, each in its own sentence

    [Fact]
    public async Task A_wrong_answer_is_the_driver_s_own_sentence_and_exit_1()
    {
        var folder = New("acme.wrong", [HookPoints.QuestConsider]);
        Replace(folder, "({ kind: 'allow' })", "({ kind: 'maybe' })");

        var trial = await PluginKit.TryFolderAsync(Home, folder);

        Assert.False(trial.Passed);
        Assert.Equal(1, trial.ExitCode);
        var step = Failed(trial, HookPoints.QuestConsider);
        Assert.Contains("which is not a decision", step.Sentence);
        Assert.Contains("the driver would hold the quest", step.Sentence);
        // The rest of the conversation still ran: the process was well, only its answer was not.
        Assert.True(trial.Steps.Single(s => s.Name == "shutdown").Ok);
    }

    [Fact]
    public async Task A_silent_plugin_is_still_running_and_said_nothing_within_the_patience()
    {
        var folder = New("acme.silent", [HookPoints.Land]);
        // It takes the frame and never answers it.
        Replace(folder, "      send({ jsonrpc: '2.0', id: frame.id, result: await points[frame.method.slice(5)](frame.params ?? {}) });", "      // silence");

        var trial = await PluginKit.TryFolderAsync(Home, folder, new TrialOptions(Patience: TimeSpan.FromSeconds(1)));

        var step = Failed(trial, HookPoints.Land);
        Assert.Contains("is running and did not answer `hook/work/land` within 1s", step.Sentence);
        Assert.Contains("the landing would say the plugin's step failed", step.Sentence);
        Assert.Equal(1, trial.ExitCode);
    }

    [Fact]
    public async Task A_line_on_stdout_that_is_not_a_frame_fails_the_stdout_check_and_nothing_else()
    {
        var folder = New("acme.noisy", [HookPoints.SessionEnded]);
        Replace(folder, "const send = ", "console.log('hello from stdout');\nconst send = ");

        var trial = await PluginKit.TryFolderAsync(Home, folder);

        var step = Failed(trial, "stdout");
        Assert.Contains("1 line on stdout that is not a frame", step.Sentence);
        Assert.Contains("`hello from stdout`", step.Sentence);
        Assert.Contains("stderr", step.Sentence);
        Assert.All(trial.Steps.Where(s => s.Name != "stdout"), s => Assert.True(s.Ok, s.Sentence));
        Assert.Equal(1, trial.ExitCode);

        // The wire test the plugin carries says the same, with no Daoris.
        var (exit, output) = await NodeTestAsync(folder);
        Assert.NotEqual(0, exit);
        Assert.Contains("on stdout that are not frames, first `hello from stdout`", output);
    }

    [Fact]
    public async Task A_plugin_that_crashes_on_a_frame_is_named_with_its_exit_code_and_its_last_words()
    {
        var folder = New("acme.crashes", [HookPoints.QuestConsider]);
        Replace(folder, "({ kind: 'allow' })", "(() => { console.error('cannot read the calendar'); process.exit(3); })()");

        var trial = await PluginKit.TryFolderAsync(Home, folder);

        var step = Failed(trial, HookPoints.QuestConsider);
        Assert.Contains("exited (code 3) before answering `hook/quest/consider`", step.Sentence);
        Assert.Contains("cannot read the calendar", step.Sentence);
        // A process that is gone is not told to go.
        Assert.DoesNotContain(trial.Steps, s => s.Name == "shutdown");
        Assert.Contains("cannot read the calendar", trial.Said);
        Assert.Equal(1, trial.ExitCode);

        var (exit, output) = await NodeTestAsync(folder);
        Assert.NotEqual(0, exit);
        Assert.Contains("its process exited (code 3) before answering `hook/quest/consider`", output);
    }

    [Fact]
    public async Task A_plugin_that_cannot_start_speaking_fails_the_handshake_with_its_exit_code()
    {
        var folder = New("acme.broken", [HookPoints.QuestConsider]);
        File.WriteAllText(Path.Combine(folder, "plugin.mjs"), "this is not javascript at all\n");

        var trial = await PluginKit.TryFolderAsync(Home, folder);

        var step = Assert.Single(trial.Steps);
        Assert.Equal("handshake", step.Name);
        Assert.False(step.Ok);
        Assert.Contains("exited (code 1) before answering `initialize`", step.Sentence);
        Assert.Contains("the driver would not start it", step.Sentence);
    }

    [Fact]
    public async Task A_plugin_that_stays_after_shutdown_is_ended_and_says_so()
    {
        var folder = New("acme.stays", [HookPoints.SessionEnded]);
        Replace(folder, "    process.exit(0);", "    // stays");

        var trial = await PluginKit.TryFolderAsync(Home, folder);

        var step = Failed(trial, "shutdown");
        Assert.Contains("did not leave within 2s of `shutdown`", step.Sentence);

        var (exit, output) = await NodeTestAsync(folder);
        Assert.NotEqual(0, exit);
        Assert.Contains("did not leave within 2s of `shutdown`", output);
    }

    [Fact]
    public async Task A_point_declared_and_not_listened_on_is_a_promise_the_process_does_not_keep()
    {
        var folder = New("acme.promises", [HookPoints.QuestConsider, HookPoints.SessionEnded]);
        Replace(folder, "  'session/ended'", "  'session/ended-not'");

        var trial = await PluginKit.TryFolderAsync(Home, folder);

        var step = Failed(trial, HookPoints.SessionEnded);
        Assert.Contains("declared but the process does not listen there", step.Sentence);
        Assert.True(trial.Steps.Single(s => s.Name == HookPoints.QuestConsider).Ok);
    }

    // ——— the frame try sends

    [Fact]
    public async Task One_point_with_the_person_s_own_frame_is_sent_that_frame()
    {
        var folder = New("acme.gate", [HookPoints.QuestConsider, HookPoints.SessionEnded]);
        Replace(folder, "({ kind: 'allow' })",
            "(frame.quest.title.includes('[hold]') ? { kind: 'hold', reason: 'the title says so' } : { kind: 'allow' })");
        var frame = JsonNode.Parse("""{ "quest": { "id": "q1", "title": "[hold] rename everything" }, "repository": "engine" }""")!.AsObject();

        var trial = await PluginKit.TryFolderAsync(Home, folder, new TrialOptions(Point: HookPoints.QuestConsider, Frame: frame));

        Assert.True(trial.Passed, Describe(trial));
        Assert.Equal(["handshake", HookPoints.QuestConsider, "shutdown", "stdout"], trial.Steps.Select(s => s.Name));
        Assert.Contains("hold", trial.Steps[1].Sentence);
        Assert.Contains("the title says so", trial.Steps[1].Sentence);
    }

    /// <summary>
    /// 🔴 The sample frame's repository is no repository. git walks UP, so an empty folder under a checkout
    /// would answer for the checkout above it — and a landing plugin that pushes first would push that.
    /// </summary>
    /// <remarks>
    /// Proven where it matters: the scratch folder of both doors sits inside a real repository, and the
    /// plugin answers wrongly if git finds one from the frame's `root`. Under the system's temporary folder
    /// there is usually no repository above, and the test would pass with no guard at all.
    /// </remarks>
    [Fact]
    public async Task The_sample_frame_s_repository_is_one_git_says_is_not_there()
    {
        var repository = Path.Combine(_scratch, "a-repository");
        Directory.CreateDirectory(repository);
        Assert.Equal(0, (await RunAsync("git", repository, [], "init", "--quiet")).Exit);
        var temp = Path.Combine(repository, "temp");
        Directory.CreateDirectory(temp);

        var folder = New("acme.lands", [HookPoints.Land]);
        Replace(folder, """
              'work/land': (frame) => ({
                pushed: false,
            """, """
              'work/land': (frame) => ({
                pushed: run('git', ['rev-parse', '--show-toplevel'], frame.root).ok ? 'git found a repository' : false,
            """);
        Replace(folder, "    message: `${id} does not push yet", "    message: `git: ${run('git', ['rev-parse', '--show-toplevel'], frame.root).why} — ${id} does not push yet");

        var trial = await PluginKit.TryFolderAsync(Home, folder, null, CancellationToken.None, scratchParent: temp);
        var (exit, output) = await NodeTestAsync(folder, new() { ["TEMP"] = temp, ["TMP"] = temp, ["TMPDIR"] = temp });

        var step = trial.Steps.Single(s => s.Name == HookPoints.Land);
        Assert.True(step.Ok, step.Sentence);
        Assert.Contains("not a git repository", step.Sentence);
        Assert.True(exit == 0, output);
        // Both doors took their scratch away with them.
        Assert.Empty(Directory.GetDirectories(temp));
    }

    /// <summary>
    /// A folder's plugin keeps what it keeps in the trial's scratch, `${data}` included: read by the
    /// install layout, `${data}` would be a `.data/` beside the plugins repository's folder, which
    /// nobody named.
    /// </summary>
    [Fact]
    public async Task A_folder_s_plugin_is_told_the_trial_s_data_folder_and_nothing_lands_beside_it()
    {
        var folder = New("acme.keeps", [HookPoints.Land]);
        var manifest = Path.Combine(folder, "plugin.json");
        var text = File.ReadAllText(manifest);
        Assert.Contains("\"${plugin}/plugin.mjs\"]", text);
        File.WriteAllText(manifest, text.Replace("\"${plugin}/plugin.mjs\"]", "\"${plugin}/plugin.mjs\", \"${data}\"]", StringComparison.Ordinal));
        Replace(folder, "    message: `${id} does not push yet", "    message: `data told: ${process.argv[2] === data} — ${id} does not push yet");

        var trial = await PluginKit.TryFolderAsync(Home, folder);

        var step = trial.Steps.Single(s => s.Name == HookPoints.Land);
        Assert.True(step.Ok, step.Sentence);
        Assert.Contains("data told: true", step.Sentence);
        Assert.False(Directory.Exists(Path.Combine(Path.GetDirectoryName(folder)!, ".data")));
    }

    /// <summary>
    /// What a trial keeps goes under the home, never the system's temporary folder, which is under the
    /// user profile where nothing of Daoris's lives (D63, the owner's call): a dot-folder the catalogue
    /// skips, gone once the trial ends. The plugin is told that folder, so it can say where it was.
    /// </summary>
    [Fact]
    public async Task A_trials_scratch_is_under_the_homes_plugins_folder_and_goes_after()
    {
        var folder = New("acme.where", [HookPoints.SessionEnded]);
        Replace(folder, "  'session/ended': (frame) => ({}),", "  'session/ended': (frame) => { console.error('data at ' + process.env.DAORIS_PLUGIN_DATA); return {}; },");

        var trial = await PluginKit.TryFolderAsync(Home, folder);

        Assert.True(trial.Passed, Describe(trial));
        var said = Assert.Single(trial.Said, line => line.StartsWith("data at ", StringComparison.Ordinal));
        Assert.StartsWith(Path.Combine(PluginKit.TrialsOf(Home), "try-"), said["data at ".Length..], StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Directory.Exists(PluginKit.TrialsOf(Home)) ? Directory.GetFileSystemEntries(PluginKit.TrialsOf(Home)) : []);
        Assert.Empty(PluginCatalog.Load(Home).Plugins);
    }

    /// <summary>With no home named, the terminal's folder trial refuses, as every writer does (D63), and says what needs none.</summary>
    [Fact]
    public async Task The_terminal_refuses_a_folder_trial_with_no_home()
    {
        var folder = New("acme.homeless", [HookPoints.SessionEnded]);
        var said = new StringWriter();

        Assert.Equal(2, await PluginKitCommand.RunAsync(["try", folder], said, home: null));
        Assert.Contains("no Daoris home", said.ToString());
        Assert.Contains("`node --test`", said.ToString());
    }

    [Fact]
    public async Task An_installed_plugin_is_tried_by_its_id_with_its_own_data_folder()
    {
        var home = Path.Combine(_scratch, "home");
        Directory.CreateDirectory(Path.Combine(home, "plugins"));
        PluginKit.Write(PluginKit.Plan("acme.installed", [HookPoints.SessionEnded], Path.Combine(home, "plugins")));

        var trial = await PluginKit.TryInstalledAsync(home, "acme.installed");

        Assert.True(trial.Passed, Describe(trial));
        Assert.True(Directory.Exists(Path.Combine(home, "plugins", ".data", "acme.installed")));
        Assert.Contains("no plugin `acme.nobody`",
            (await Assert.ThrowsAsync<DriverException>(() => PluginKit.TryInstalledAsync(home, "acme.nobody"))).Message);
    }

    [Fact]
    public async Task What_try_cannot_check_is_refused_before_anything_starts()
    {
        var folder = New("acme.gate", [HookPoints.QuestConsider]);

        Assert.Contains("no `plugin.json`",
            (await Assert.ThrowsAsync<DriverException>(() => PluginKit.TryFolderAsync(Home, _scratch))).Message);
        Assert.Contains("`work/land` is not a point `acme.gate` declares",
            (await Assert.ThrowsAsync<DriverException>(() => PluginKit.TryFolderAsync(Home, folder, new TrialOptions(Point: HookPoints.Land)))).Message);
        Assert.Contains("a frame needs its point",
            (await Assert.ThrowsAsync<DriverException>(() => PluginKit.TryFolderAsync(Home, folder, new TrialOptions(Frame: new JsonObject())))).Message);

        var declares = Path.Combine(_scratch, "acme.agent");
        Directory.CreateDirectory(declares);
        File.WriteAllText(Path.Combine(declares, "plugin.json"),
            """{ "id": "acme.agent", "harnesses": [ { "name": "acme-agent", "command": ["acme"] } ] }""");
        Assert.Contains("speaks on no point",
            (await Assert.ThrowsAsync<DriverException>(() => PluginKit.TryFolderAsync(Home, declares))).Message);
    }

    [Fact]
    public async Task A_manifest_the_catalogue_refuses_fails_try_with_the_catalogue_s_sentence()
    {
        var folder = Path.Combine(_scratch, "acme.future");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "plugin.json"),
            """{ "id": "acme.future", "apiVersion": 99, "hooks": { "command": ["node", "x.mjs"], "points": ["quest/consider"] } }""");

        var trial = await PluginKit.TryFolderAsync(Home, folder);

        var step = Assert.Single(trial.Steps);
        Assert.Equal("manifest", step.Name);
        Assert.Contains("needs plugin API 99", step.Sentence);
        Assert.Equal(1, trial.ExitCode);
    }

    // ——— the two checkers agree

    /// <summary>
    /// The kit checks an answer at two doors: `try`, through the driver's own reader, and the wire test the
    /// scaffold writes, which a plugins repository runs with no Daoris binary. They share no code, so this
    /// table is their contract: one plugin answering each row, and both doors must give the row's verdict.
    /// </summary>
    public static TheoryData<string, string, bool> Answers => new()
    {
        { "initialize", """{"protocolVersion":1,"points":["{point}"]}""", true },
        { "initialize", """{"protocolVersion":2,"points":["{point}"]}""", false },
        { "initialize", """{"points":["{point}"]}""", false },
        { "initialize", """{"protocolVersion":1,"points":["{point}","work/land"]}""", false },
        { "initialize", """{"protocolVersion":1,"points":[]}""", false },
        { "initialize", "\"yes\"", false },
        { HookPoints.QuestConsider, """{"kind":"allow"}""", true },
        { HookPoints.QuestConsider, """{"kind":"hold","reason":"outside working hours"}""", true },
        { HookPoints.QuestConsider, """{"kind":"hold"}""", true },
        { HookPoints.QuestConsider, """{"kind":"maybe"}""", false },
        { HookPoints.QuestConsider, "{}", false },
        { HookPoints.QuestConsider, "null", false },
        { HookPoints.SessionEnded, "{}", true },
        { HookPoints.SessionEnded, "null", true },
        { HookPoints.Land, """{"pushed":true,"pullRequest":"https://example.test/pull/7","message":"opened"}""", true },
        { HookPoints.Land, """{"pushed":false}""", true },
        { HookPoints.Land, """{"pushed":false,"pullRequest":null,"message":null}""", true },
        { HookPoints.Land, """{"pushed":"yes"}""", false },
        { HookPoints.Land, """{"message":"did not push"}""", false },
        { HookPoints.Land, """{"pushed":true,"pullRequest":"ftp://example.test/7"}""", false },
        { HookPoints.Land, """{"pushed":true,"pullRequest":"pull/7"}""", false },
        { HookPoints.Land, """{"pushed":true,"message":7}""", false },
    };

    [Theory]
    [MemberData(nameof(Answers))]
    public async Task Try_and_the_wire_test_give_every_answer_the_same_verdict(string point, string answer, bool accepted)
    {
        var declared = point == "initialize" ? HookPoints.QuestConsider : point;
        var folder = New("acme.echo", [declared]);
        File.WriteAllText(Path.Combine(folder, "answer.json"),
            JsonSerializer.Serialize(new { point, answer = answer.Replace("{point}", declared, StringComparison.Ordinal) }));
        File.WriteAllText(Path.Combine(folder, "plugin.mjs"), Echo);

        var trial = await PluginKit.TryFolderAsync(Home, folder);
        var (exit, output) = await NodeTestAsync(folder);

        Assert.True(accepted == trial.Passed, $"try: {Describe(trial)}");
        Assert.True(accepted == (exit == 0), $"node --test: {output}");
    }

    /// <summary>A plugin answering every call with what `answer.json` in its folder says, and the rest correctly.</summary>
    private const string Echo = """
        import { readFileSync } from 'node:fs';
        import { createInterface } from 'node:readline';
        const { point, answer } = JSON.parse(readFileSync(new URL('./answer.json', import.meta.url), 'utf8'));
        const said = JSON.parse(answer);
        const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
        for await (const line of createInterface({ input: process.stdin })) {
          const frame = JSON.parse(line);
          if (frame.method === 'shutdown') process.exit(0);
          const mine = frame.method === 'initialize' ? point === 'initialize' : frame.method === 'hook/' + point;
          const result = mine ? said
            : frame.method === 'initialize' ? { protocolVersion: 1, points: frame.params.points }
            : { kind: 'allow' };
          send({ jsonrpc: '2.0', id: frame.id, result });
        }
        """;

    // ——— the terminal door

    [Fact]
    public async Task The_terminal_makes_a_plugin_and_says_what_it_wrote_and_what_comes_next()
    {
        var output = new StringWriter();

        var exit = await PluginKitCommand.RunAsync(
            ["new", "acme.gate", "--point", "quest/consider", "--point", "session/ended", "--in", _scratch], output, home: null);

        Assert.Equal(0, exit);
        var said = output.ToString();
        Assert.Contains("made `acme.gate`", said);
        Assert.Contains("plugin.test.mjs", said);
        Assert.Contains("node --test", said);
        Assert.Contains("daoris-driver plugins try", said);
        Assert.Contains("daoris plugin add", said);
        Assert.True(File.Exists(Path.Combine(_scratch, "acme.gate", "plugin.mjs")));
    }

    [Fact]
    public async Task The_terminal_tries_a_folder_and_its_exit_is_the_verdict()
    {
        var good = New("acme.good", [HookPoints.QuestConsider]);
        var bad = New("acme.bad", [HookPoints.QuestConsider]);
        Replace(bad, "({ kind: 'allow' })", "({ kind: 'maybe' })");

        var passed = new StringWriter();
        Assert.Equal(0, await PluginKitCommand.RunAsync(["try", good], passed, home: Home));
        Assert.Contains("ok    quest/consider", passed.ToString());
        Assert.Contains("answered as the driver reads it", passed.ToString());

        var failed = new StringWriter();
        Assert.Equal(1, await PluginKitCommand.RunAsync(["try", bad], failed, home: Home));
        Assert.Contains("fail  quest/consider", failed.ToString());
        Assert.Contains("failed 1 of 4 checks", failed.ToString());
    }

    [Theory]
    [InlineData(new[] { "new" }, "needs a plugin id")]
    [InlineData(new[] { "new", "acme.gate" }, "at least one point")]
    [InlineData(new[] { "new", "Acme", "--point", "quest/consider" }, "is not a plugin id")]
    [InlineData(new[] { "try" }, "needs a folder")]
    [InlineData(new[] { "try", "acme.nowhere" }, "no folder `acme.nowhere`")]
    [InlineData(new[] { "explode" }, "usage: daoris-driver plugins")]
    public async Task What_the_terminal_cannot_do_is_a_sentence_and_exit_2(string[] args, string sentence)
    {
        var output = new StringWriter();

        var exit = await PluginKitCommand.RunAsync(args.Select(a => a.Replace("{scratch}", _scratch)).ToArray(), output, home: null);

        Assert.Equal(2, exit);
        Assert.Contains(sentence, output.ToString());
    }

    [Fact]
    public async Task The_terminal_sends_a_frame_file_to_one_point()
    {
        var folder = New("acme.gate", [HookPoints.QuestConsider]);
        var frame = Path.Combine(_scratch, "frame.json");
        File.WriteAllText(frame, """{ "quest": { "id": "q1", "title": "anything" } }""");
        var notJson = Path.Combine(_scratch, "frame.txt");
        File.WriteAllText(notJson, "not json");

        var output = new StringWriter();
        Assert.Equal(0, await PluginKitCommand.RunAsync(["try", folder, "--point", "quest/consider", "--frame", frame], output, home: Home));

        var refused = new StringWriter();
        Assert.Equal(2, await PluginKitCommand.RunAsync(["try", folder, "--point", "quest/consider", "--frame", notJson], refused, home: Home));
        Assert.Contains("is not a JSON object", refused.ToString());
    }

    /// <summary>
    /// PLUGUI1d (D119 §4.2): a trial of an installed plugin from a terminal is one <c>plugin.tried</c> line, its
    /// history; a folder's trial is printed and never kept, since its id may name an installed plugin it is not.
    /// </summary>
    [Fact]
    public async Task The_terminals_trial_of_an_installed_plugin_is_one_line_in_the_machine_log_and_a_folders_is_none()
    {
        Directory.CreateDirectory(Path.Combine(Home, "plugins"));
        PluginKit.Write(PluginKit.Plan("acme.logged", [HookPoints.SessionEnded], Path.Combine(Home, "plugins")));
        var folder = New("acme.loose", [HookPoints.SessionEnded]);
        using (var log = new MachineLog(Home, "driver"))
        {
            Assert.Equal(0, await PluginKitCommand.RunAsync(["try", "acme.logged"], new StringWriter(), Home, log: log));
            Assert.Equal(0, await PluginKitCommand.RunAsync(["try", folder], new StringWriter(), Home, log: log));
        }

        var line = Assert.Single(MachineLogReader.Read(Path.Combine(Home, MachineLog.Folder), new LogFilter()).Lines);
        Assert.Equal("plugin.tried", line.Event);
        Assert.Equal("acme.logged", line.Data.GetProperty("plugin").GetString());
        Assert.True(line.Data.GetProperty("passed").GetBoolean());
        Assert.Equal("terminal", line.Data.GetProperty("door").GetString());
    }

    // ——— helpers

    private string New(string id, IEnumerable<string> points)
    {
        var parent = Path.Combine(_scratch, Guid.NewGuid().ToString("N")[..6]);
        Directory.CreateDirectory(parent);
        var plan = PluginKit.Plan(id, points, parent);
        PluginKit.Write(plan);
        return plan.Folder;
    }

    private static void Replace(string folder, string from, string to)
    {
        var path = Path.Combine(folder, "plugin.mjs");
        var script = File.ReadAllText(path);
        // The sabotage must have applied, or the test proves nothing about try.
        Assert.Contains(from, script);
        File.WriteAllText(path, script.Replace(from, to, StringComparison.Ordinal));
    }

    private static TrialStep Failed(PluginTrial trial, string name)
    {
        var step = trial.Steps.SingleOrDefault(s => s.Name == name);
        Assert.True(step is not null, $"no `{name}` step: {Describe(trial)}");
        Assert.False(step!.Ok, step.Sentence);
        return step;
    }

    private static string Describe(PluginTrial trial) =>
        string.Join(" | ", trial.Steps.Select(s => $"{(s.Ok ? "ok" : "FAIL")} {s.Name}: {s.Sentence}"))
        + (trial.Said.Count > 0 ? " | said: " + string.Join(" / ", trial.Said) : "");

    private static void InstallManifest(string home, string id, IReadOnlyList<string> points)
    {
        var folder = Path.Combine(home, "plugins", id);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "plugin.json"), JsonSerializer.Serialize(new
        {
            id,
            hooks = new { command = new[] { "node", "plugin.mjs" }, points },
        }));
    }

    /// <summary>`node --test` in the folder, as a plugins repository's gate runs it — nothing of Daoris's on the way.</summary>
    private static Task<(int Exit, string Output)> NodeTestAsync(string folder, Dictionary<string, string>? environment = null) =>
        // A reporter named, so the counts read the same on every Node and every console.
        RunAsync("node", folder, environment ?? [], "--test", "--test-reporter=tap");

    private static async Task<(int Exit, string Output)> RunAsync(
        string program, string folder, Dictionary<string, string> environment, params string[] arguments)
    {
        var info = new ProcessStartInfo(program)
        {
            WorkingDirectory = folder,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        foreach (var (name, value) in environment) info.Environment[name] = value;
        using var process = Process.Start(info)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        await process.WaitForExitAsync(limit.Token);
        return (process.ExitCode, await stdout + await stderr);
    }

    private sealed class RecordingChannel(IReadOnlyList<string> points, List<object> told) : IHookChannel
    {
        public IReadOnlyList<string> Points => points;
        public bool Alive => true;

        public Task<HookDecision> ConsiderAsync(object payload, CancellationToken ct)
        {
            told.Add(payload);
            return Task.FromResult(HookDecision.Allow);
        }

        public Task EndedAsync(object payload, CancellationToken ct)
        {
            told.Add(payload);
            return Task.CompletedTask;
        }

        public Task<PluginLanding> LandAsync(object payload, CancellationToken ct)
        {
            told.Add(payload);
            return Task.FromResult(new PluginLanding("acme.every", Pushed: false, PullRequest: null, "recorded"));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
