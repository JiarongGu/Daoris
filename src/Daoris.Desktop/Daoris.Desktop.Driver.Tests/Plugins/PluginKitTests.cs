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
        Assert.Equal(["decision", "observation", "act", "query", "observation", "observation", "event", "act"], PluginKit.Points.Select(point => point.Kind));
    }

    [Fact]
    public void Each_point_waits_as_long_as_the_driver_waits_for_it()
    {
        Assert.Equal(TimeSpan.FromSeconds(10), PluginKit.Find(HookPoints.QuestConsider)!.Patience);
        Assert.Equal(TimeSpan.FromSeconds(10), PluginKit.Find(HookPoints.SessionEnded)!.Patience);
        Assert.Equal(LandingPlugins.DefaultPatience, PluginKit.Find(HookPoints.Land)!.Patience);
        Assert.Equal(TimeSpan.FromSeconds(30), PluginKit.Find(HookPoints.State)!.Patience);
        Assert.Equal(TimeSpan.FromSeconds(10), PluginKit.Find(HookPoints.StateChanged)!.Patience);
        // The work wire's peer waits its default for the awaited set's answer and an attempt's receipt (FABRIC-PERFORM1a).
        Assert.Equal(HookPeer.DefaultPatience, PluginKit.Find(HookPoints.Watch)!.Patience);
        Assert.Equal(HookPeer.DefaultPatience, PluginKit.Find(HookPoints.Perform)!.Patience);
    }

    /// <summary>
    /// FABRIC-PERFORM1e: the work points' samples are the work wire's own messages. The awaited set and an attempt are written by the
    /// writer the hook set sends with and read back whole; the event a performer sends, made a plugin's own, is one the hook set's
    /// reader takes, bound to its invocation, with its receipt and what it made.
    /// </summary>
    [Fact]
    public void The_work_points_samples_are_the_work_wire_s_own_messages()
    {
        static JsonElement Element(JsonNode node) => JsonDocument.Parse(node.ToJsonString()).RootElement.Clone();

        var watch = PluginKit.Find(HookPoints.Watch)!.Frame;
        Assert.Equal(WorkWire.Watch(PluginKit.SampleWatch).ToJsonString(), watch.ToJsonString());
        Assert.Equal(PluginKit.SampleWatch.Units[0].Awaiting, WorkWire.ReadWatch(Element(watch))!.Units[0].Awaiting);

        var perform = PluginKit.Find(HookPoints.Perform)!.Frame;
        Assert.Equal(WorkWire.Perform(PluginKit.SamplePerform).ToJsonString(), perform.ToJsonString());
        Assert.Equal(PluginKit.SamplePerform with { Inputs = [] }, WorkWire.ReadPerform(Element(perform))! with { Inputs = [] });

        var step = PluginKit.Find(HookPoints.Step)!;
        Assert.Equal("event", step.Kind);
        var own = step.Frame.ToJsonString()
            .Replace(PluginKit.SampleStep, "acme.deploy/perform", StringComparison.Ordinal)
            .Replace(PluginKit.SamplePlugin, "acme.deploy", StringComparison.Ordinal);
        var heard = WorkWire.ReadStep(JsonDocument.Parse(own).RootElement, out var field);
        Assert.True(heard is not null, $"the sample event does not read at `{field}`");
        Assert.Equal(PluginKit.SamplePerform.Invocation, heard!.Invocation);
        Assert.Equal("acme.deploy/receipt@1:i-0fda18a2b3c4", heard.Outcome.Evidence!.Ref);
        Assert.Equal([new WorkResource("acme.deploy/resource@1", "r-i-0fda18a2b3c4")], heard.Outcome.Resources);
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

        // The query's frame, through an occasion's asks (PLUGHOOK1a).
        var landed = new LandedBranch("engine", "default", PluginKit.SampleState.Branch, "main", PluginKit.SampleState.PushedTip!, "s1a2b3c4",
            "0fda18", "Expose a streaming budget", DateTimeOffset.UnixEpoch);
        await new LandingPlugins(home, start: (_, _, _) => Task.FromResult<IHookChannel>(new RecordingChannel(HookPoints.All, told)))
            .AskStatesAsync([new StateAsk(landed, "acme.every", PluginKit.SampleState)]);

        // The change's frame, through the loop's set as the shell's bus tells it (STATEHUB1g).
        await using (var set = new HookSet(home, start: (_, _, _) => Task.FromResult<IHookChannel>(channel)))
        {
            await set.ReconcileAsync(PluginCatalog.Load(home), CancellationToken.None);
            await set.ChangedAsync(PluginKit.SampleChange, CancellationToken.None);
        }

        Assert.Equal(5, told.Count);
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(
                JsonSerializer.SerializeToNode(told[i])!.ToJsonString(),
                PluginKit.Points[i].Frame.ToJsonString());
        }

        // The repository's checkout in a sample is the kit's marker, which try and the wire test replace
        // with an empty scratch folder — never a path on anybody's machine.
        Assert.Equal(PluginKit.SampleRoot, PluginKit.Find(HookPoints.QuestConsider)!.Frame["root"]!.GetValue<string>());
        Assert.Equal(PluginKit.SampleRoot, PluginKit.Find(HookPoints.Land)!.Frame["root"]!.GetValue<string>());
        Assert.Equal(PluginKit.SampleRoot, PluginKit.Find(HookPoints.State)!.Frame["root"]!.GetValue<string>());
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
        // It declares no tools until it runs one (PLUGTOOL1a): `tools: []`, read as none.
        Assert.Empty(manifest.Tools);
        Assert.Null(manifest.ToolsProblem);

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
        // A query asks its platform's tool too (PLUGHOOK1a), so it carries the same helper.
        var asks = PluginKit.Plan("acme.asks", [HookPoints.State], _scratch).Files.Single(f => f.Name == "plugin.mjs").Content;
        Assert.Contains("'work/state'", asks);
        Assert.Contains("function run(", asks);
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
        Assert.Contains("quest/consider, session/ended, work/land, work/state, state/changed, work/watch, work/step, work/perform", unknown.Message);

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
        { new[] { HookPoints.State } },
        { new[] { HookPoints.StateChanged } },
        { new[] { HookPoints.QuestConsider, HookPoints.SessionEnded, HookPoints.Land, HookPoints.State, HookPoints.StateChanged } },
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

    /// <summary>
    /// 🔴 PROC1g: the wire test ends once its plugin leaves at `shutdown`. The grace it races is a bound on a plugin that stays,
    /// never a wait on one that went: its timer left running held every run open for the whole grace after the plugin had gone.
    /// Here the grace is ten minutes, past the run's own limit (<see cref="RunAsync"/>'s ninety seconds), so a run that waits it out never
    /// ends at all (TESTCLOCK2), where the machine's clock timed a minute's grace against thirty seconds.
    /// </summary>
    [Fact]
    public async Task A_wire_test_ends_once_its_plugin_leaves_not_when_the_grace_runs_out()
    {
        var folder = New("acme.fresh", [HookPoints.SessionEnded]);
        var test = Path.Combine(folder, "plugin.test.mjs");
        var grace = $"const GRACE = {(long)HookProcess.Grace.TotalMilliseconds};";
        var text = File.ReadAllText(test);
        Assert.Contains(grace, text);
        File.WriteAllText(test, text.Replace(grace, "const GRACE = 600000;", StringComparison.Ordinal));

        var (exit, output) = await NodeTestAsync(folder);

        Assert.True(exit == 0, output);
        Assert.Contains("# fail 0", output);
    }

    [Theory]
    [MemberData(nameof(PointSets))]
    public async Task A_fresh_plugin_passes_try_at_every_point_it_declares(string[] points)
    {
        var folder = New("acme.fresh", points);

        var trial = await PluginTrials.TryFolderAsync(Home, folder);

        Assert.True(trial.Passed, Describe(trial));
        Assert.Equal(0, trial.ExitCode);
        Assert.Equal("acme.fresh", trial.Plugin);
        Assert.Equal(["handshake", .. points, "shutdown", "stdout"], trial.Steps.Select(step => step.Name));
        Assert.Contains("listens on", trial.Steps[0].Sentence);
        Assert.Contains("left when told", trial.Steps.Single(step => step.Name == "shutdown").Sentence);
    }

    /// <summary>
    /// FABRIC-PERFORM1a, round 2: try reads a plugin's stdout under the bound the driver reads it under. A landing's answer over 64 KB from a
    /// plugin that speaks no work point passes, as the driver takes it; one past the answers' bound fails at once with the reason, as the
    /// driver fails it — where try had read it whole and passed a plugin the driver then never heard.
    /// </summary>
    [Theory]
    [InlineData(70_000, true)]
    [InlineData(HookPeer.AnswerBytes + 1, false)]
    public async Task Try_reads_a_long_answer_as_the_driver_does(int length, bool passes)
    {
        var folder = New("acme.long", [HookPoints.Land]);
        Replace(folder, "pushed: false,", $"pushed: false, diagnostic: 'd'.repeat({length}),");

        var trial = await PluginTrials.TryFolderAsync(Home, folder);

        if (passes)
        {
            Assert.True(trial.Passed, Describe(trial));
            return;
        }

        var step = Failed(trial, HookPoints.Land);
        Assert.Contains("4 MB", step.Sentence);
        Assert.DoesNotContain("did not answer", step.Sentence);
    }

    // ——— try says what went wrong, each in its own sentence

    [Fact]
    public async Task A_wrong_answer_is_the_driver_s_own_sentence_and_exit_1()
    {
        var folder = New("acme.wrong", [HookPoints.QuestConsider]);
        Replace(folder, "({ kind: 'allow' })", "({ kind: 'maybe' })");

        var trial = await PluginTrials.TryFolderAsync(Home, folder);

        Assert.False(trial.Passed);
        Assert.Equal(1, trial.ExitCode);
        var step = Failed(trial, HookPoints.QuestConsider);
        Assert.Contains("which is not a decision", step.Sentence);
        Assert.Contains("the driver would hold the quest", step.Sentence);
        // The rest of the conversation still ran: the process was well, only its answer was not.
        Assert.True(trial.Steps.Single(s => s.Name == "shutdown").Ok);
    }

    /// <remarks>
    /// 🔴 PLUGKIT2: the one second was the whole trial's, so node's start and the handshake outlasted it under load and
    /// `work/land` was never asked. The second is the point's call's now, the handshake waits the point's own patience, and
    /// this plugin's handshake comes later than the second, as a loaded machine's did.
    /// </remarks>
    [Fact]
    public async Task A_silent_plugin_is_still_running_and_said_nothing_within_the_patience()
    {
        var folder = New("acme.silent", [HookPoints.Land]);
        // It takes the frame and never answers it.
        Replace(folder, "      send({ jsonrpc: '2.0', id: frame.id, result: await answers[frame.method.slice(5)](frame.params ?? {}) });", "      // silence");
        Replace(folder, "    const declared = frame.params?.points ?? [];",
            "    await new Promise((resolve) => setTimeout(resolve, 1500));\n    const declared = frame.params?.points ?? [];");

        var trial = await PluginTrials.TryFolderAsync(Home, folder, new TrialOptions(Patience: TimeSpan.FromSeconds(1)));

        Assert.True(trial.Steps.Single(s => s.Name == "handshake").Ok, trial.Steps.Single(s => s.Name == "handshake").Sentence);
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

        var trial = await PluginTrials.TryFolderAsync(Home, folder);

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

        var trial = await PluginTrials.TryFolderAsync(Home, folder);

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

        var trial = await PluginTrials.TryFolderAsync(Home, folder);

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

        var trial = await PluginTrials.TryFolderAsync(Home, folder);

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

        var trial = await PluginTrials.TryFolderAsync(Home, folder);

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

        var trial = await PluginTrials.TryFolderAsync(Home, folder, new TrialOptions(Point: HookPoints.QuestConsider, Frame: frame));

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

        var trial = await PluginTrials.TryFolderAsync(Home, folder, null, CancellationToken.None, scratchParent: temp);
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

        var trial = await PluginTrials.TryFolderAsync(Home, folder);

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

        var trial = await PluginTrials.TryFolderAsync(Home, folder);

        Assert.True(trial.Passed, Describe(trial));
        var said = Assert.Single(trial.Said, line => line.StartsWith("data at ", StringComparison.Ordinal));
        Assert.StartsWith(Path.Combine(PluginTrials.TrialsOf(Home), "try-"), said["data at ".Length..], StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Directory.Exists(PluginTrials.TrialsOf(Home)) ? Directory.GetFileSystemEntries(PluginTrials.TrialsOf(Home)) : []);
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
        Directory.CreateDirectory(DaorisHome.Entry(home, HomeEntry.Plugins));
        PluginKit.Write(PluginKit.Plan("acme.installed", [HookPoints.SessionEnded], DaorisHome.Entry(home, HomeEntry.Plugins)));

        var trial = await PluginTrials.TryInstalledAsync(home, "acme.installed");

        Assert.True(trial.Passed, Describe(trial));
        Assert.True(Directory.Exists(DaorisHome.Entry(home, HomeEntry.Plugins,".data", "acme.installed")));
        Assert.Contains("no plugin `acme.nobody`",
            (await Assert.ThrowsAsync<DriverException>(() => PluginTrials.TryInstalledAsync(home, "acme.nobody"))).Message);
    }

    [Fact]
    public async Task What_try_cannot_check_is_refused_before_anything_starts()
    {
        var folder = New("acme.gate", [HookPoints.QuestConsider]);

        Assert.Contains("no `plugin.json`",
            (await Assert.ThrowsAsync<DriverException>(() => PluginTrials.TryFolderAsync(Home, _scratch))).Message);
        Assert.Contains("`work/land` is not a point `acme.gate` declares",
            (await Assert.ThrowsAsync<DriverException>(() => PluginTrials.TryFolderAsync(Home, folder, new TrialOptions(Point: HookPoints.Land)))).Message);
        Assert.Contains("a frame needs its point",
            (await Assert.ThrowsAsync<DriverException>(() => PluginTrials.TryFolderAsync(Home, folder, new TrialOptions(Frame: new JsonObject())))).Message);

        var declares = Path.Combine(_scratch, "acme.agent");
        Directory.CreateDirectory(declares);
        File.WriteAllText(Path.Combine(declares, "plugin.json"),
            """{ "id": "acme.agent", "harnesses": [ { "name": "acme-agent", "command": ["acme"] } ] }""");
        Assert.Contains("speaks on no point",
            (await Assert.ThrowsAsync<DriverException>(() => PluginTrials.TryFolderAsync(Home, declares))).Message);
    }

    [Fact]
    public async Task A_manifest_the_catalogue_refuses_fails_try_with_the_catalogue_s_sentence()
    {
        var folder = Path.Combine(_scratch, "acme.future");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "plugin.json"),
            """{ "id": "acme.future", "apiVersion": 99, "hooks": { "command": ["node", "x.mjs"], "points": ["quest/consider"] } }""");

        var trial = await PluginTrials.TryFolderAsync(Home, folder);

        var step = Assert.Single(trial.Steps);
        Assert.Equal("manifest", step.Name);
        Assert.Contains("needs plugin API 99", step.Sentence);
        Assert.Equal(1, trial.ExitCode);
    }

    // ——— the tools it declares (PLUGTOOL1a)

    /// <summary>
    /// `try` finds each tool a manifest declares as the driver would, asks its version against its range, and runs its
    /// checks, each a step in its own sentence, before it starts the plugin (D150 point 7, the UX6 design §7.2).
    /// </summary>
    [Fact]
    public async Task Try_finds_each_declared_tool_asks_its_version_and_runs_its_checks_each_in_its_own_sentence()
    {
        var folder = New("acme.tools", [HookPoints.SessionEnded]);
        DeclareTools(folder, """
            [{ "id": "node", "versions": ">=18", "for": "Runs the wire script.",
               "ready": [{ "run": ["node", "-e", "process.exit(0)"], "says": "It runs a script" }] }]
            """);

        var trial = await PluginTrials.TryFolderAsync(Home, folder);

        Assert.True(trial.Passed, Describe(trial));
        Assert.Equal(["tool node", "node check 1", "handshake", HookPoints.SessionEnded, "shutdown", "stdout"], trial.Steps.Select(s => s.Name));
        Assert.Matches(@"^Node\.js \d+\.\d+\.\d+, the system's, in its range \(18 or newer\)\.$", trial.Steps[0].Sentence);
        Assert.Equal("ready: It runs a script.", trial.Steps[1].Sentence);
    }

    /// <summary>
    /// A bad manifest's tools fail their own steps, each in its own sentence, and the rest of the trial still runs: a problem
    /// in `tools` is shown, never a refusal. A check that does not pass names its fix, which Daoris never runs.
    /// </summary>
    [Fact]
    public async Task A_bad_manifests_tools_fail_their_own_steps_and_the_plugin_is_still_tried()
    {
        var folder = New("acme.tools", [HookPoints.SessionEnded]);
        var marker = Path.Combine(_scratch, "fixed");
        DeclareTools(folder, $$"""
            [{ "id": "node", "versions": ">=999",
               "ready": [{ "run": ["node", "-e", "process.exit(3)"], "says": "It exits clean",
                           "fix": "node -e \"require('fs').writeFileSync('{{marker.Replace("\\", "/", StringComparison.Ordinal)}}', '')\"" }] },
             { "id": "Az" },
             { "id": "acme-missing", "command": "daoris-no-such-program" }]
            """);

        var trial = await PluginTrials.TryFolderAsync(Home, folder);

        Assert.False(trial.Passed);
        Assert.Equal(1, trial.ExitCode);
        Assert.Equal(
            ["tool node", "node check 1", "tool 2", "tool acme-missing", "handshake", HookPoints.SessionEnded, "shutdown", "stdout"],
            trial.Steps.Select(s => s.Name));
        Assert.Matches(@"needs 999 or newer; this is \d+\.\d+\.\d+\.$", Failed(trial, "tool node").Sentence);
        Assert.StartsWith("not ready: It exits clean. `node -e process.exit(3)` exited 3. Run `node -e", Failed(trial, "node check 1").Sentence);
        Assert.Contains("has the `id` `Az`, which is not one", Failed(trial, "tool 2").Sentence);
        Assert.Contains("`daoris-no-such-program` is not on this machine's PATH", Failed(trial, "tool acme-missing").Sentence);
        // The plugin itself was tried, and answered well.
        Assert.All(trial.Steps.Skip(4), s => Assert.True(s.Ok, s.Sentence));
        // 🔴 The fix is shown, never run.
        Assert.False(File.Exists(marker));
    }

    [Fact]
    public async Task The_terminal_tries_a_plugins_tools_and_its_exit_is_the_verdict()
    {
        var good = New("acme.good", [HookPoints.SessionEnded]);
        DeclareTools(good, """[{ "id": "node", "versions": ">=18" }]""");
        var bad = New("acme.bad", [HookPoints.SessionEnded]);
        DeclareTools(bad, """[{ "id": "node", "versions": "latest" }]""");

        var passed = new StringWriter();
        Assert.Equal(0, await PluginKitCommand.RunAsync(["try", good], passed, home: Home));
        Assert.Contains("ok    tool node", passed.ToString());

        var failed = new StringWriter();
        Assert.Equal(1, await PluginKitCommand.RunAsync(["try", bad], failed, home: Home));
        Assert.Contains("fail  tool node", failed.ToString());
        Assert.Contains("tool `node`'s `versions` must be a range", failed.ToString());
        Assert.Contains("failed 1 of 5 checks", failed.ToString());
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
        // PLUGHOOK1a: the query's answer, as HookStateTests holds the driver's reader to the same rows.
        { HookPoints.State, """{"state":"open"}""", true },
        { HookPoints.State, """{"state":"abandoned","pullRequest":null,"at":"2026-10-04T14:02:11Z","message":"abandoned"}""", true },
        { HookPoints.State, """{"state":"unknown","message":"no pull request from that branch"}""", true },
        { HookPoints.State, $$"""{"state":"completed","mergeCommit":"{{FullA}}","sourceCommit":"{{FullB}}","target":"main","how":"squash"}""", true },
        { HookPoints.State, $$"""{"state":"completed","mergeCommit":"{{FullA}}","sourceCommit":"{{FullB}}","how":"fast-forward"}""", true },
        { HookPoints.State, $$"""{"state":"completed","mergeCommit":"{{FullA}}"}""", false },
        { HookPoints.State, $$"""{"state":"completed","mergeCommit":"1111111","sourceCommit":"{{FullB}}"}""", false },
        { HookPoints.State, """{"state":"merged"}""", false },
        { HookPoints.State, """{"pullRequest":"https://example.test/pr/7"}""", false },
        { HookPoints.State, """{"state":"open","pullRequest":"pull/7"}""", false },
        { HookPoints.State, """{"state":"open","target":7}""", false },
        { HookPoints.State, """{"state":"open","at":"yesterday"}""", false },
        { HookPoints.State, """{"state":"open","message":7}""", false },
        { HookPoints.State, "null", false },
        // STATEHUB1g: an observation's answer is not read, as session/ended's is not.
        { HookPoints.StateChanged, "{}", true },
        { HookPoints.StateChanged, "null", true },
    };

    private const string FullA = "1111111111111111111111111111111111111111";
    private const string FullB = "2222222222222222222222222222222222222222";

    [Theory]
    [MemberData(nameof(Answers))]
    public async Task Try_and_the_wire_test_give_every_answer_the_same_verdict(string point, string answer, bool accepted)
    {
        var declared = point == "initialize" ? HookPoints.QuestConsider : point;
        var folder = New("acme.echo", [declared]);
        File.WriteAllText(Path.Combine(folder, "answer.json"),
            JsonSerializer.Serialize(new { point, answer = answer.Replace("{point}", declared, StringComparison.Ordinal) }));
        File.WriteAllText(Path.Combine(folder, "plugin.mjs"), Echo);

        var trial = await PluginTrials.TryFolderAsync(Home, folder);
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
        Directory.CreateDirectory(DaorisHome.Entry(Home, HomeEntry.Plugins));
        PluginKit.Write(PluginKit.Plan("acme.logged", [HookPoints.SessionEnded], DaorisHome.Entry(Home, HomeEntry.Plugins)));
        var folder = New("acme.loose", [HookPoints.SessionEnded]);
        using (var log = new MachineLog(Home, "driver"))
        {
            Assert.Equal(0, await PluginKitCommand.RunAsync(["try", "acme.logged"], new StringWriter(), Home, log: log));
            Assert.Equal(0, await PluginKitCommand.RunAsync(["try", folder], new StringWriter(), Home, log: log));
        }

        // Each trial's start says its containment too (TERMTRUST1a, agent.contained): codes only, never the plugin, so no history of it.
        var line = Assert.Single(MachineLogReader.Read(DaorisHome.Entry(Home, HomeEntry.Logs), new LogFilter()).Lines,
            each => each.Event != "agent.contained");
        Assert.Equal("plugin.tried", line.Event);
        Assert.Equal("acme.logged", line.Data.GetProperty("plugin").GetString());
        Assert.True(line.Data.GetProperty("passed").GetBoolean());
        Assert.Equal("terminal", line.Data.GetProperty("door").GetString());
    }

    // ——— a plugin that works (FABRIC-PERFORM1e)

    [Fact]
    public void A_new_performer_declares_its_step_and_what_it_makes_and_carries_the_kit_s_work_files()
    {
        var plan = PluginKit.Plan("acme.deploy", [HookPoints.Perform], _scratch);
        var written = PluginKit.Write(plan);

        // It reports at `work/step` too, where its outcome is heard, and is told the awaited set at `work/watch`, as every plugin that
        // works is: the kit adds both, in the driver's order.
        Assert.Equal([HookPoints.Watch, HookPoints.Step, HookPoints.Perform], plan.Points);
        Assert.Equal(["plugin.json", "plugin.mjs", "plugin.test.mjs", "README.md", "work.mjs", "plugin.contract.test.mjs", "cycle.json"], written);

        var (manifest, problem) = PluginCatalog.ReadFolder(plan.Folder);
        Assert.Null(problem);
        var step = Assert.Single(manifest.Steps);
        Assert.Equal(("acme.deploy/perform", NodeReach.Performed, "acme.deploy/receipt@1"), (step.Kind, step.Reach, step.Evidence));
        Assert.Equal(new NodePerform(Idempotent: true, Reconcile: true, ["unavailable", "rate-limited", "timeout"]), step.Perform);
        Assert.Equal(3, step.Retry!.MaxAttempts);
        Assert.Equal(new PluginResource("acme.deploy/resource@1", PluginResourceRoles.Temporary, PluginSteps.CleanupPoint), Assert.Single(manifest.Resources));

        var script = File.ReadAllText(Path.Combine(plan.Folder, "plugin.mjs"));
        Assert.Contains("import { work } from './work.mjs';", script);
        Assert.Contains("act: async (call) =>", script);
        Assert.Contains("capabilities: kit.capabilities", script);
        Assert.Contains("...kit.calls", script);
        Assert.Contains(WorkWire.Capability, File.ReadAllText(Path.Combine(plan.Folder, "work.mjs")));
        Assert.Equal(CycleFixture.Default with { Step = "perform" }, PluginCycles.Read(Path.Combine(plan.Folder, "cycle.json")) with
        {
            Revisions = CycleFixture.Default.Revisions,
            Inputs = CycleFixture.Default.Inputs,
        });
        // The README says the plugin's own step and receipt, never the samples' markers.
        var readme = File.ReadAllText(Path.Combine(plan.Folder, "README.md"));
        Assert.Contains("\"step\": \"acme.deploy/perform\"", readme);
        Assert.Contains("acme.deploy/receipt@1:i-0fda18a2b3c4", readme);
        Assert.Contains("## Its contract test", readme);
        foreach (var name in written)
        {
            var text = File.ReadAllText(Path.Combine(plan.Folder, name));
            Assert.DoesNotContain("{{", text);
            Assert.DoesNotContain("\r", text);
        }

        Assert.DoesNotContain(PluginKit.SamplePlugin, readme);
        Assert.DoesNotContain(PluginKit.SampleStep, readme);
    }

    [Fact]
    public void A_new_observer_declares_an_observed_step_and_no_performer_s_files()
    {
        var plan = PluginKit.Plan("acme.checks", [HookPoints.Watch], _scratch);

        Assert.Equal([HookPoints.Watch, HookPoints.Step], plan.Points);
        Assert.Equal(["plugin.json", "plugin.mjs", "plugin.test.mjs", "README.md", "work.mjs"], plan.Files.Select(file => file.Name));
        PluginKit.Write(plan);
        var (manifest, problem) = PluginCatalog.ReadFolder(plan.Folder);
        Assert.Null(problem);
        Assert.Equal(("acme.checks/observe", NodeReach.Observed), (Assert.Single(manifest.Steps).Kind, manifest.Steps[0].Reach));
        Assert.Empty(manifest.Resources);
        Assert.Contains("observe: async (unit, step) => null", File.ReadAllText(Path.Combine(plan.Folder, "plugin.mjs")));
    }

    /// <summary>
    /// §D9's gate in a plugins repository: `node --test` runs the wire test and the contract test, with nothing of Daoris's installed, and
    /// a fresh performer passes both, in the stand-in world.
    /// </summary>
    [Fact]
    public async Task A_fresh_performer_passes_its_wire_test_and_its_contract_test()
    {
        var folder = New("acme.deploy", [HookPoints.Perform, HookPoints.Watch]);

        var (exit, output) = await NodeTestAsync(folder);

        Assert.True(exit == 0, output);
        Assert.Contains("# fail 0", output);
        Assert.Contains("# pass 8", output);
        foreach (var shown in new[] { "one external effect under duplicate delivery", "each work/reconcile answer", "already-gone on a repeated cleanup",
                     "an event sent again keeps the id it first had", "no frame claims what a plugin may not" })
        {
            Assert.Contains(shown, output);
        }
    }

    [Fact]
    public async Task A_fresh_observer_passes_its_wire_test()
    {
        var folder = New("acme.checks", [HookPoints.Watch]);

        var (exit, output) = await NodeTestAsync(folder);

        Assert.True(exit == 0, output);
        Assert.Contains("# fail 0", output);
    }

    /// <summary>A performer that forgets a key it has seen does the work twice: its contract test says so, and so does the cycle.</summary>
    [Fact]
    public async Task A_performer_that_does_the_work_twice_fails_its_contract_test_and_its_cycle()
    {
        var folder = New("acme.twice", [HookPoints.Perform]);
        Sabotage(folder, "work.mjs", "      if (!known) {", "      if (!known || true) {");

        var (exit, output) = await NodeTestAsync(folder);
        var cycle = await PluginCycles.TryAsync(Home, folder, CycleFixture.Default);

        Assert.NotEqual(0, exit);
        Assert.Contains("the same key reached the platform more than once", output);
        Assert.Contains("work was done 2 times", Failed(cycle, "duplicate").Sentence);
        Assert.Equal(1, cycle.ExitCode);
    }

    /// <summary>
    /// 🔴 Nothing reaches a platform: a plugin that does not take the stand-in world is sent no attempt, by `try`, the cycle, the wire
    /// test or the contract test, and its work is never done.
    /// </summary>
    [Fact]
    public async Task A_plugin_that_does_not_take_the_stand_in_world_is_sent_no_attempt()
    {
        var folder = New("acme.real", [HookPoints.Perform]);
        Sabotage(folder, "work.mjs", "process.env.DAORIS_PLUGIN_WORLD ? standIn(process.env.DAORIS_PLUGIN_WORLD) : null", "null");
        // Were an attempt sent, its own act() would leave this mark beside it: the platform reached.
        Sabotage(folder, "plugin.mjs", "act: async (call) => ({",
            "act: async (call) => (writeFileSync(`${process.env.DAORIS_PLUGIN_FOLDER}/reached`, '1'), {");
        Sabotage(folder, "plugin.mjs", "import { work } from './work.mjs';", "import { work } from './work.mjs';\nimport { writeFileSync } from 'node:fs';");

        var trial = await PluginTrials.TryFolderAsync(Home, folder);
        var cycle = await PluginCycles.TryAsync(Home, folder, CycleFixture.Default);
        var (exit, output) = await NodeTestAsync(folder);

        Assert.Contains("none was sent", Failed(trial, HookPoints.Perform).Sentence);
        Assert.Contains("nothing was sent", Failed(cycle, "world").Sentence);
        Assert.NotEqual(0, exit);
        Assert.Contains("did not take the kit's stand-in world", output);
        Assert.False(File.Exists(Path.Combine(folder, "reached")));
    }

    [Fact]
    public async Task A_fresh_performer_passes_try_and_its_cycle_through_the_driver()
    {
        var folder = New("acme.deploy", [HookPoints.Perform]);

        var trial = await PluginTrials.TryFolderAsync(Home, folder);
        var cycle = await PluginCycles.TryAsync(Home, folder, CycleFixture.Default with { Deadline = TimeSpan.FromSeconds(1) });

        Assert.True(trial.Passed, Describe(trial));
        Assert.Equal(["handshake", HookPoints.Watch, HookPoints.Step, HookPoints.Perform, "shutdown", "stdout"], trial.Steps.Select(step => step.Name));
        Assert.Contains("acknowledged `i-0fda18a2b3c4`", trial.Steps.Single(step => step.Name == HookPoints.Perform).Sentence);
        Assert.True(cycle.Passed, Describe(cycle));
        Assert.Equal(["handshake", "eligible", "duplicate", "failure", "timeout", "handshake again", "restart", "stale", "cleanup", "shutdown", "stdout"],
            cycle.Steps.Select(step => step.Name));
        Assert.Contains("on `acme.deploy/receipt@1:i-1`", cycle.Steps.Single(step => step.Name == "eligible").Sentence);
        Assert.Contains("which its step tries again", cycle.Steps.Single(step => step.Name == "failure").Sentence);
        Assert.Contains("removed, then already-gone, removed once", cycle.Steps.Single(step => step.Name == "cleanup").Sentence);
        // The cycle's scratch goes with it, under the home's trials (D63).
        Assert.Empty(Directory.EnumerateFileSystemEntries(PluginTrials.TrialsOf(Home)));
    }

    [Fact]
    public async Task The_terminal_drives_a_folder_s_cycle_and_refuses_what_a_cycle_does_not_take()
    {
        var folder = New("acme.deploy", [HookPoints.Perform]);
        var fixture = Path.Combine(folder, "cycle.json");
        File.WriteAllText(fixture, File.ReadAllText(fixture).Replace("\"deadlineSeconds\": 3", "\"deadlineSeconds\": 1", StringComparison.Ordinal));

        var output = new StringWriter();
        Assert.Equal(0, await PluginKitCommand.RunAsync(["try", folder, "--cycle", fixture], output, home: Home));
        Assert.Contains("ok    restart", output.ToString());
        Assert.Contains("answered as the driver reads it", output.ToString());

        var refused = new StringWriter();
        Assert.Equal(2, await PluginKitCommand.RunAsync(["try", folder, "--cycle", fixture, "--point", HookPoints.Perform], refused, home: Home));
        Assert.Contains("takes no --point or --frame", refused.ToString());
        refused = new StringWriter();
        Assert.Equal(2, await PluginKitCommand.RunAsync(["try", "acme.deploy", "--cycle", fixture], refused, home: Home));
        Assert.Contains("never an installed plugin's data", refused.ToString());
    }

    /// <summary>
    /// The example performed plugin (<c>examples/plugins/local-preview</c>) is the kit's own output filled in: its kit-owned files are what
    /// the kit writes now, so a change to the kit changes the example in the same commit, and it passes its cycle through the driver.
    /// </summary>
    [Fact]
    public async Task The_example_performer_is_the_kit_s_scaffold_filled_in_and_passes_its_cycle()
    {
        var example = Path.Combine(GitSeed.RepoRoot(), "examples", "plugins", "local-preview");
        var made = PluginKit.Plan("local-preview", [HookPoints.Perform], _scratch);

        foreach (var name in new[] { PluginKit.WorkScript, PluginKit.Test, PluginKit.ContractTest, PluginKit.CycleFixture })
        {
            Assert.Equal(made.Files.Single(file => file.Name == name).Content, File.ReadAllText(Path.Combine(example, name)).Replace("\r\n", "\n"));
        }

        var (manifest, problem) = PluginCatalog.ReadFolder(example);
        Assert.Null(problem);
        Assert.Equal("local-preview/perform", Assert.Single(manifest.Steps).Kind);

        // Copied out, so the cycle's journal and previews never land in the repository's folder.
        var copy = Path.Combine(_scratch, "local-preview");
        Directory.CreateDirectory(copy);
        foreach (var file in Directory.GetFiles(example)) File.Copy(file, Path.Combine(copy, Path.GetFileName(file)));
        var cycle = await PluginCycles.TryAsync(Home, copy, PluginCycles.Read(Path.Combine(copy, PluginKit.CycleFixture)) with { Deadline = TimeSpan.FromSeconds(1) });
        Assert.True(cycle.Passed, Describe(cycle));
    }

    // ——— helpers

    private static void Sabotage(string folder, string file, string from, string to)
    {
        var path = Path.Combine(folder, file);
        var text = File.ReadAllText(path);
        // The sabotage must have applied, or the test proves nothing.
        Assert.Contains(from, text);
        File.WriteAllText(path, text.Replace(from, to, StringComparison.Ordinal));
    }

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

    /// <summary>Put a manifest's tools where the scaffold wrote none (PLUGTOOL1a).</summary>
    private static void DeclareTools(string folder, string tools)
    {
        var path = Path.Combine(folder, "plugin.json");
        var text = File.ReadAllText(path);
        Assert.Contains("\"tools\": []", text);
        File.WriteAllText(path, text.Replace("\"tools\": []", $"\"tools\": {tools}", StringComparison.Ordinal));
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
        var folder = DaorisHome.Entry(home, HomeEntry.Plugins,id);
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
        try
        {
            await process.WaitForExitAsync(limit.Token);
        }
        catch (OperationCanceledException)
        {
            // A run past its limit is ended, so one that waits out a long grace leaves nothing behind.
            process.Kill(entireProcessTree: true);
            throw;
        }

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

        public Task<PullRequestState> StateAsync(object payload, CancellationToken ct)
        {
            told.Add(payload);
            return Task.FromResult(new PullRequestState(PullRequestStates.Unknown));
        }

        public Task ChangedAsync(object payload, CancellationToken ct)
        {
            told.Add(payload);
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
