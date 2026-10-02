using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// WSSETUP6 (D124 §4.1–§4.3): a workspace is set up by a plan of single set-ups, which the driver's tick works. The order
/// is what other work touches first; one is open at a time by default and never the cap's last slot; the plan pauses itself
/// once its pilot has closed; the person pauses, resumes and stops it; a repository the press refuses at its turn is skipped
/// with its reason and never judged again in a loop; a declined, deleted or parked set-up is never asked again, and no
/// repository is ever asked twice. Fast: the world the plan reads is a stand-in.
/// </summary>
public sealed class WorkspaceSetupTests : IDisposable
{
    private static readonly DateOnly Day = new(2026, 10, 2);

    private static readonly DateTimeOffset Now = new(2026, 10, 2, 9, 0, 0, TimeSpan.Zero);

    private static readonly string[] Four = ["atlas", "billing", "cargo", "docs"];

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-wsplan-" + Guid.NewGuid().ToString("N")[..8]);

    public WorkspaceSetupTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    // ---------------------------------------------------------------- the order (§4.2)

    /// <summary>
    /// D124 §4.2: quests addressed to it first, then reads into its checkout by other repositories' sessions, then sessions
    /// run in it, then by name. Its own quests and the set-ups are not work other work asked of it.
    /// </summary>
    [Fact]
    public void The_order_is_what_other_work_touches_first_then_by_name()
    {
        string[] names = ["atlas", "billing", "cargo", "docs", "echo", "fable"];
        var rows = names.Select(name => WorkspaceSetupStandIn.Row(name, root: $"/w/{name}")).ToList();
        List<QuestView> quests =
        [
            new("1", "billing", "cargo", "Say what the tariff is", "", "Done"),
            new("2", "cargo", "cargo", "Tidy its own readme", "", "Open"),
            new("3", "ask #a1", "billing", SetupQuests.Title(SetupQuests.SetUp, Day), "", "Open"),
        ];
        List<SessionRun> runs = [new("s1", "billing"), new("s2", "echo"), new("s3", "echo"), new("s4", "echo"), new("s5", "atlas")];
        var touched = new Dictionary<string, IReadOnlyList<ToolTouch>>
        {
            ["s1"] = [new("c1", "/w/atlas/src/map.cs"), new("c2", "/w/atlas/README.md"), new("c3", "/w/billing/own.cs")],
            ["s5"] = [new("c1", "/w/billing/tariff.cs")],
        };

        var touches = SetupOrder.Count(names, rows, quests, runs, session => touched.GetValueOrDefault(session) ?? []);

        Assert.Equal(new SetupTouches(1, 0, 0), touches["cargo"]);
        Assert.Equal(new SetupTouches(0, 2, 1), touches["atlas"]);
        Assert.Equal(new SetupTouches(0, 1, 1), touches["billing"]);
        Assert.Equal(new SetupTouches(0, 0, 3), touches["echo"]);
        Assert.Equal(SetupTouches.None, touches["docs"]);
        Assert.Equal(["cargo", "atlas", "billing", "echo", "docs", "fable"], SetupOrder.Order(names, touches, [], []));
    }

    /// <summary>
    /// A read is a call another repository's session made into this checkout (D76): each call once however many files it
    /// touched there, separators and case as Windows sees them, the deepest root owning a path, and a path that is not rooted
    /// or only shares a prefix counted nowhere.
    /// </summary>
    [Fact]
    public void A_read_is_another_sessions_call_into_the_checkout_each_call_once_and_by_the_deepest_root()
    {
        List<RegistrationRow> rows =
        [
            WorkspaceSetupStandIn.Row("atlas", root: "/w/atlas"),
            WorkspaceSetupStandIn.Row("tiles", workspace: "home", root: "/w/atlas/vendor/tiles"),
            WorkspaceSetupStandIn.Row("billing", root: "/w/billing"),
        ];
        List<ToolTouch> calls =
        [
            new("c1", "/w/atlas/a.cs"), new("c1", "/w/atlas/b.cs"), new("c2", "\\w\\ATLAS\\c.cs"),
            new("c3", "/w/atlas/vendor/tiles/t.cs"), new("c4", "relative/x.cs"), new("c5", "/w/atlas-old/x.cs"),
        ];

        var touches = SetupOrder.Count(["atlas", "billing"], rows, [], [new("s1", "billing")], _ => calls);

        Assert.Equal(new SetupTouches(0, 2, 0), touches["atlas"]);
        Assert.Equal(new SetupTouches(0, 0, 1), touches["billing"]);
    }

    /// <summary>
    /// The files a session touched are read from its conversation record (D76): each tool call's locations, a call's later
    /// updates the same call, a call with no id its own, a line that does not read skipped, and words that name locations
    /// never taken for a call.
    /// </summary>
    [Fact]
    public void A_conversation_records_tool_calls_name_the_files_they_touched_each_call_and_path_once()
    {
        var record = Path.Combine(_home, "s1.events.jsonl");
        File.WriteAllText(record, string.Join('\n',
            """{"seq":1,"kind":"user","text":"read the map's locations"}""",
            """{"seq":2,"kind":"tool","id":"c1","toolKind":"read","locations":["/w/atlas/a.cs","/w/atlas/b.cs"]}""",
            """{"seq":3,"kind":"tool","id":"c1","status":"completed","locations":["/w/atlas/a.cs"]}""",
            """{"seq":4,"kind":"tool","toolKind":"search","locations":["/w/atlas/c.cs"]}""",
            """{"seq":5,"kind":"message","text":"\"locations\" is a word here"}""",
            """{"seq":6,"kind":"tool","id":"c2","locations":["/w/atlas/d.cs""",
            """{"seq":7,"kind":"tool","id":"c3","locations":["/w/billing/e.cs"]}""") + "\n");

        var touches = WorkspaceSetupWorld.ReadTouches(record);

        Assert.Equal(
            [new("c1", "/w/atlas/a.cs"), new("c1", "/w/atlas/b.cs"), new("#1", "/w/atlas/c.cs"), new ToolTouch("c3", "/w/billing/e.cs")],
            touches);
        Assert.Empty(WorkspaceSetupWorld.ReadTouches(Path.Combine(_home, "nobody.events.jsonl")));
    }

    [Fact]
    public void The_ones_named_first_go_first_and_the_unticked_are_left_out()
    {
        var touches = new Dictionary<string, SetupTouches>(StringComparer.OrdinalIgnoreCase) { ["cargo"] = new(4, 0, 0) };

        Assert.Equal(["docs", "billing", "cargo"], SetupOrder.Order(Four, touches, ["docs", "billing"], ["atlas"]));
    }

    // ---------------------------------------------------------------- the press (§4.1, §4.3)

    /// <summary>
    /// D124 §4.1, §4.3: the press writes the plan under the home, in order, one at a time and a pilot of two by default, and
    /// adds the doctrine tool's verbs once, at the workspace's scope, never a rule per repository. It publishes nothing: the
    /// tick does.
    /// </summary>
    [Fact]
    public async Task A_press_writes_the_plan_and_adds_the_rule_once_at_the_workspaces_scope()
    {
        var world = World(Four);

        var plan = await PressAsync(world, Driven(2));

        Assert.Equal(Four, plan.Order);
        Assert.Equal(1, plan.AtOnce);
        Assert.Equal(2, plan.Pilot);
        Assert.Null(plan.Paused);
        Assert.True(plan.Live);
        Assert.Equal(Path.Combine(_home, "setup", "work.json"), WorkspaceSetupFile.PathOf(_home, "work"));
        Assert.Equal(Four, Plan().Order);
        Assert.Empty(world.Published);

        var rules = PermissionRules.Load(_home);
        Assert.Equal(SetupPress.Rules, rules.Workspaces["work"].Allow);
        Assert.Empty(rules.Repositories);
        var planned = Assert.Single(world.Logged);
        Assert.Equal("setup.planned", planned.Event);
        Assert.Equal([("workspace", "work"), ("repositories", 4), ("atOnce", 1), ("pilot", 2)], planned.Data);
    }

    [Fact]
    public async Task A_second_press_is_refused_while_a_plan_works_and_a_stopped_one_is_replaced()
    {
        var world = World(Four);
        await PressAsync(world, Driven(3));

        var again = await PreviewAsync(world, Driven(3));
        WorkspaceSetup.Stop(world, _home, "work", Now);
        var replaced = await PreviewAsync(world, Driven(3));

        Assert.False(again.Pressable);
        Assert.Contains(again.Refusals, refusal => refusal.Sentence.Contains("already working", StringComparison.Ordinal)
            && refusal.Sentence.Contains("--stop", StringComparison.Ordinal));
        Assert.True(replaced.Pressable);
    }

    /// <summary>D124 §4.1: never more than <c>cap − 1</c> at once, so other work keeps a slot; at a cap of one, one.</summary>
    [Theory]
    [InlineData(3, 2, true)]
    [InlineData(3, 3, false)]
    [InlineData(2, 1, true)]
    [InlineData(2, 2, false)]
    [InlineData(1, 1, true)]
    [InlineData(1, 2, false)]
    public async Task At_once_is_never_more_than_cap_minus_one_and_one_at_a_cap_of_one(int cap, int atOnce, bool pressable)
    {
        var preview = await PreviewAsync(World(Four), Driven(cap), new(AtOnce: atOnce));

        Assert.Equal(pressable, preview.Pressable);
        if (!pressable) Assert.Contains(preview.Refusals, refusal => refusal.Sentence.Contains("`daoris driver cap", StringComparison.Ordinal));
    }

    /// <summary>A doctrine tool this machine cannot run refuses the press whole: nothing it wrote could be set up.</summary>
    [Fact]
    public async Task A_press_on_a_machine_whose_children_find_no_doctrine_tool_is_refused_whole()
    {
        var world = World(Four);
        world.Tools = new("node", "v22.11.0", null, null, null, null);

        var preview = await PreviewAsync(world, Driven(3));
        var outcome = await WorkspaceSetup.PressAsync(preview, world, _home, Now);

        Assert.Contains(preview.Refusals, refusal => refusal.Code == SetupRefusals.NoTool);
        Assert.False(outcome.Written);
        Assert.False(File.Exists(WorkspaceSetupFile.PathOf(_home, "work")));
        Assert.Empty(PermissionRules.Load(_home).Workspaces);
    }

    [Fact]
    public async Task First_or_skip_naming_a_repository_outside_the_workspace_is_refused_naming_it()
    {
        var world = World(Four).With(WorkspaceSetupStandIn.Row("elsewhere", workspace: "home"));

        var preview = await PreviewAsync(world, Driven(3), new(First: ["elsewhere"], Skip: ["nobody"]));

        Assert.False(preview.Pressable);
        Assert.Contains(preview.Refusals, refusal => refusal.Sentence.Contains("`elsewhere`", StringComparison.Ordinal));
        Assert.Contains(preview.Refusals, refusal => refusal.Sentence.Contains("`nobody`", StringComparison.Ordinal));
    }

    /// <summary>A row with no checkout here is a teammate's: its own machine's driver sets it up, so the plan leaves it out and says so.</summary>
    [Fact]
    public async Task Rows_with_no_checkout_here_are_left_out_and_said_and_a_workspace_with_none_here_is_refused()
    {
        var world = World("atlas").With(WorkspaceSetupStandIn.Row("remote", root: null) with { Root = null });
        var empty = new WorkspaceSetupStandIn().With(WorkspaceSetupStandIn.Row("remote", root: null) with { Root = null });

        var preview = await PreviewAsync(world, Driven(3));
        var none = await PreviewAsync(empty, Driven(3));

        Assert.Equal(["atlas"], preview.Rows.Select(row => row.Repository));
        Assert.Equal(["remote"], preview.NoCheckout);
        Assert.False(none.Pressable);
        Assert.Contains(none.Refusals, refusal => refusal.Sentence.Contains("no checkout here", StringComparison.Ordinal));
    }

    /// <summary>The press's list says where each stands and, for one still to go, what the press would refuse now.</summary>
    [Fact]
    public async Task The_list_says_where_each_stands_and_each_refusal_now()
    {
        var world = World(Four);
        world.SetUp("docs");

        var preview = await PreviewAsync(world, Driven(3).WithDrivable("billing", false));

        Assert.Equal(SetupState.SetUp, preview.Rows.Single(row => row.Repository == "docs").Standing.State);
        var billing = preview.Rows.Single(row => row.Repository == "billing");
        Assert.Equal(SetupState.ToGo, billing.Standing.State);
        Assert.Contains(billing.Refusals, refusal => refusal.Code == SetupRefusals.NotDriven);
        Assert.Empty(preview.Rows.Single(row => row.Repository == "atlas").Refusals);
    }

    // ---------------------------------------------------------------- the tick (§4.1)

    /// <summary>D124 §4.1: while fewer than <c>atOnce</c> are open, the next to go is published, as the single press publishes it.</summary>
    [Fact]
    public async Task The_tick_publishes_the_next_to_go_only_while_fewer_than_at_once_are_open()
    {
        var world = World(Four);
        var config = Driven(3);
        await PressAsync(world, config);

        var first = await TickAsync(world, config);
        await TickAsync(world, config);
        var atlas = world.SetupOf("atlas");
        world.Close(atlas.Id, "Done");
        await TickAsync(world, config);

        Assert.Equal(["atlas", "billing"], Asked(world));
        Assert.Equal("Set up this repository for every agent (2026-10-02)", atlas.Title);
        Assert.Equal(atlas.Id, Plan().Published["atlas"]);
        Assert.Contains(first, line => line.StartsWith("setup  work: asked `atlas` to set itself up", StringComparison.Ordinal));
        var published = world.Logged.First(line => line.Event == "setup.published");
        Assert.Equal([("workspace", "work"), ("repository", "atlas"), ("quest", atlas.Id)], published.Data);
    }

    /// <summary>🔴 D124 §4.1: a plan file asking for more is held to <c>cap − 1</c> at every tick, and to one at a cap of one.</summary>
    [Theory]
    [InlineData(4, 3)]
    [InlineData(3, 2)]
    [InlineData(2, 1)]
    [InlineData(1, 1)]
    public async Task A_tick_never_holds_more_than_cap_minus_one_open_whatever_the_plan_says(int cap, int most)
    {
        var world = World(Four);
        WorkspaceSetupFile.Save(_home, new WorkspaceSetupPlan("work", Four, AtOnce: 9, Pilot: 0) { Created = Now });

        await TickAsync(world, Driven(cap));
        await TickAsync(world, Driven(cap));

        Assert.Equal(most, world.Published.Count);
    }

    /// <summary>
    /// D124 §4.1: two go first; nothing more is asked until both have closed, and then the plan pauses itself, says so once,
    /// and waits for the person. Resumed, it carries on and never pauses for the pilot again.
    /// </summary>
    [Fact]
    public async Task The_plan_pauses_itself_once_its_pilot_has_closed_until_the_person_resumes_it()
    {
        var world = World([.. Four, "echo"]);
        var config = Driven(3);
        await PressAsync(world, config, new(AtOnce: 2));

        await TickAsync(world, config);
        world.Close(world.SetupOf("atlas").Id, "Done");
        await TickAsync(world, config);
        var running = Plan();
        world.Close(world.SetupOf("billing").Id, "Declined", "the build needs a key only the owner holds");
        var said = await TickAsync(world, config);
        await TickAsync(world, config);
        var paused = Plan();

        Assert.Null(running.Paused);
        Assert.Equal(["atlas", "billing"], Asked(world));
        Assert.Equal(SetupPausedBy.Pilot, paused.Paused?.By);
        Assert.Contains(said, line => line.StartsWith("setup  work: paused after the pilot", StringComparison.Ordinal)
            && line.Contains("--resume", StringComparison.Ordinal));
        var pause = Assert.Single(world.Logged, line => line.Event == "setup.paused");
        Assert.Equal([("workspace", "work"), ("by", "pilot")], pause.Data);

        Assert.True(WorkspaceSetup.Resume(world, _home, "work").Ok);
        await TickAsync(world, config);
        world.Close(world.SetupOf("cargo").Id, "Done");
        world.Close(world.SetupOf("docs").Id, "Done");
        await TickAsync(world, config);

        Assert.True(Plan().PilotResumed);
        Assert.Equal(["atlas", "billing", "cargo", "docs", "echo"], Asked(world));
        Assert.Null(Plan().Paused);
        Assert.Contains(world.Logged, line => line.Event == "setup.resumed");
    }

    [Fact]
    public async Task A_plan_the_person_paused_publishes_nothing_until_they_resume_it()
    {
        var world = World(Four);
        var config = Driven(3);
        await PressAsync(world, config);

        var paused = WorkspaceSetup.Pause(world, _home, "work", Now);
        var again = WorkspaceSetup.Pause(world, _home, "work", Now);
        await TickAsync(world, config);
        var none = Asked(world);
        WorkspaceSetup.Resume(world, _home, "work");
        await TickAsync(world, config);

        Assert.True(paused.Ok);
        Assert.True(again.Ok);
        Assert.Contains("already paused", again.Message, StringComparison.Ordinal);
        Assert.Empty(none);
        Assert.Equal(["atlas"], Asked(world));
        Assert.Single(world.Logged, line => line.Event == "setup.paused" && line.Data.Contains(("by", "person")));
    }

    /// <summary>D124 §4.5: stop ends the plan. A quest it already published stays.</summary>
    [Fact]
    public async Task A_stopped_plan_publishes_nothing_more_and_what_it_published_stays()
    {
        var world = World(Four);
        var config = Driven(3);
        await PressAsync(world, config);
        await TickAsync(world, config);

        var stopped = WorkspaceSetup.Stop(world, _home, "work", Now);
        world.Close(world.SetupOf("atlas").Id, "Done");
        await TickAsync(world, config);
        var pausedAfter = WorkspaceSetup.Pause(world, _home, "work", Now);

        Assert.True(stopped.Ok);
        Assert.False(Plan().Live);
        Assert.Equal(["atlas"], Asked(world));
        Assert.Single(world.Quests);
        Assert.False(pausedAfter.Ok);
        Assert.Contains("stopped", pausedAfter.Message, StringComparison.Ordinal);
        Assert.Contains(world.Logged, line => line.Event == "setup.stopped");
    }

    /// <summary>
    /// 🔴 D124 §4.1: a repository the press refuses at its turn is skipped, the plan says why, and the next is asked in the same
    /// tick. It is never judged again by a later tick: no loop of refusals.
    /// </summary>
    [Fact]
    public async Task A_repository_the_press_refuses_is_skipped_with_its_reason_and_never_judged_again()
    {
        var world = World(Four);
        var config = Driven(3).WithDrivable("atlas", false);
        await PressAsync(world, config, new(Pilot: 0));

        var said = await TickAsync(world, config);
        var judged = world.LinesRead.Count(read => read == "atlas");
        foreach (var repository in new[] { "billing", "cargo" })
        {
            world.Close(world.SetupOf(repository).Id, "Done");
            await TickAsync(world, config);
        }

        await TickAsync(world, config);

        Assert.Equal(["billing", "cargo", "docs"], Asked(world));
        var skip = Plan().Skipped["atlas"];
        Assert.Equal(SetupRefusals.NotDriven, skip.Code);
        Assert.Contains("`daoris driver drive atlas`", skip.Said, StringComparison.Ordinal);
        Assert.Contains(said, line => line.StartsWith("setup  work: skipped `atlas`", StringComparison.Ordinal));
        Assert.Equal(judged, world.LinesRead.Count(read => read == "atlas"));
        var logged = Assert.Single(world.Logged, line => line.Event == "setup.skipped");
        Assert.Equal([("workspace", "work"), ("repository", "atlas"), ("refusal", "not-driven")], logged.Data);
        Assert.Equal(SetupState.Skipped, (await StandAsync(world, config)).Single(standing => standing.Repository == "atlas").State);
    }

    /// <summary>A service that refuses the ask is a refusal too: skipped, with the service's own words.</summary>
    [Fact]
    public async Task An_ask_the_service_refuses_is_skipped_with_its_words()
    {
        var world = World(Four);
        world.Refuse = new AskAnswer(false, "the workspace is shared and its remote refused the ask.", null, null);
        var config = Driven(2);
        await PressAsync(world, config, new(Pilot: 0));

        await TickAsync(world, config);

        Assert.Equal("the workspace is shared and its remote refused the ask.", Plan().Skipped["atlas"].Said);
        Assert.Empty(Plan().Published);
    }

    /// <summary>D124 §4.1: a declined set-up is never published again by the plan; the person presses for it alone.</summary>
    [Fact]
    public async Task A_declined_set_up_is_never_asked_again()
    {
        var world = World("atlas", "billing");
        var config = Driven(3);
        await PressAsync(world, config, new(Pilot: 0));

        await TickAsync(world, config);
        world.Close(world.SetupOf("atlas").Id, "Declined", "its build needs a key");
        await TickAsync(world, config);
        world.Close(world.SetupOf("billing").Id, "Done");
        await TickAsync(world, config);
        await TickAsync(world, config);

        Assert.Equal(["atlas", "billing"], Asked(world));
        var atlas = (await StandAsync(world, config)).Single(standing => standing.Repository == "atlas");
        Assert.Equal(SetupState.Declined, atlas.State);
        Assert.Contains("its build needs a key", atlas.Said, StringComparison.Ordinal);
    }

    /// <summary>🔴 Never two set-ups for one repository: one already open, from the single press, holds its turn and its slot.</summary>
    [Fact]
    public async Task A_set_up_already_open_is_counted_and_never_asked_twice()
    {
        var world = World("atlas", "billing");
        var single = world.Quest("q9", "atlas", SetupQuests.Title(SetupQuests.SetUp, Day));
        var config = Driven(3);
        await PressAsync(world, config, new(Pilot: 0));

        await TickAsync(world, config);
        var held = Asked(world);
        var standing = (await StandAsync(world, config)).Single(standing => standing.Repository == "atlas");
        world.Close(single.Id, "Done");
        await TickAsync(world, config);

        Assert.Empty(held);
        Assert.Equal((SetupState.Open, "q9"), (standing.State, standing.Quest));
        Assert.Equal(["billing"], Asked(world));
        Assert.Equal(SetupState.Waiting, (await StandAsync(world, config)).Single(standing => standing.Repository == "atlas").State);
    }

    /// <summary>A set-up its failed sessions parked (DRV6) holds no session and needs the person: it frees the slot and is not asked again.</summary>
    [Fact]
    public async Task A_parked_set_up_frees_its_slot_and_is_never_asked_again()
    {
        var world = World("atlas", "billing");
        var config = Driven(3);
        await PressAsync(world, config, new(Pilot: 0));

        await TickAsync(world, config);
        world.Strikes[world.SetupOf("atlas").Id] = config.Strikes;
        await TickAsync(world, config);
        await TickAsync(world, config);

        Assert.Equal(["atlas", "billing"], Asked(world));
        Assert.Equal(SetupState.Parked, (await StandAsync(world, config)).Single(standing => standing.Repository == "atlas").State);
    }

    /// <summary>A set-up the person deleted (D95) is not asked again by the plan.</summary>
    [Fact]
    public async Task A_deleted_set_up_is_never_asked_again()
    {
        var world = World("atlas", "billing");
        var config = Driven(3);
        await PressAsync(world, config, new(Pilot: 0));

        await TickAsync(world, config);
        world.Delete(world.SetupOf("atlas").Id);
        await TickAsync(world, config);
        await TickAsync(world, config);

        Assert.Equal(["atlas", "billing"], Asked(world));
        Assert.Equal(SetupState.Deleted, (await StandAsync(world, config)).Single(standing => standing.Repository == "atlas").State);
    }

    /// <summary>
    /// <i>Set up</i> is the registry's own word, <c>registered</c> (adopted, and declaring), read and never recomputed: a row
    /// that holds a declaration the service has not called registered is still to go.
    /// </summary>
    [Fact]
    public async Task A_repository_the_registry_calls_registered_is_set_up_and_never_asked()
    {
        var world = World("atlas", "billing", "cargo");
        world.SetUp("atlas");
        world.Rows[1] = world.Rows[1] with { Adopted = true, Summary = "Declared, and not called registered." };
        var config = Driven(3);
        await PressAsync(world, config, new(Pilot: 0, AtOnce: 2));

        await TickAsync(world, config);

        Assert.Equal(["billing", "cargo"], Asked(world));
        var standing = (await StandAsync(world, config)).Single(standing => standing.Repository == "atlas");
        Assert.Equal(SetupState.SetUp, standing.State);
        Assert.Equal("set up: What atlas owns.", standing.Said);
    }

    /// <summary>
    /// A doctrine tool the machine's children no longer find refuses every repository alike: the plan pauses and says why,
    /// rather than skip each in turn. Resumed once it is back, it carries on.
    /// </summary>
    [Fact]
    public async Task A_doctrine_tool_gone_pauses_the_plan_rather_than_skip_every_repository()
    {
        var world = World(Four);
        var config = Driven(3);
        await PressAsync(world, config, new(Pilot: 0));
        var tools = world.Tools;
        world.Tools = new("node", "v22.11.0", null, null, null, null);

        await TickAsync(world, config);
        var paused = Plan();
        world.Tools = tools;
        WorkspaceSetup.Resume(world, _home, "work");
        await TickAsync(world, config);

        Assert.Equal(SetupPausedBy.Tool, paused.Paused?.By);
        Assert.Contains("the doctrine tool cannot run here", paused.Paused!.Said, StringComparison.Ordinal);
        Assert.Empty(paused.Skipped);
        Assert.Equal(["atlas"], Asked(world));
    }

    /// <summary>A resume judges the skipped again, so a refusal the person has since answered is asked in its turn.</summary>
    [Fact]
    public async Task A_resume_judges_the_skipped_again()
    {
        var world = World("atlas", "billing");
        var config = Driven(3).WithDrivable("atlas", false);
        await PressAsync(world, config, new(Pilot: 0));
        await TickAsync(world, config);

        var resumed = WorkspaceSetup.Resume(world, _home, "work");
        world.Close(world.SetupOf("billing").Id, "Done");
        await TickAsync(world, config.WithDrivable("atlas", true));

        Assert.True(resumed.Ok);
        Assert.Contains("judged again", resumed.Message, StringComparison.Ordinal);
        Assert.Empty(Plan().Skipped);
        Assert.Equal(["billing", "atlas"], Asked(world));
    }

    /// <summary>A tick with no plan to work, or only a paused or stopped one, reads nothing from the service.</summary>
    [Fact]
    public async Task A_tick_with_no_plan_to_work_reads_nothing()
    {
        var world = World(Four);
        var config = Driven(3);

        var none = await TickAsync(world, config);
        WorkspaceSetupFile.Save(_home, new WorkspaceSetupPlan("work", Four, 1, 2) { Created = Now, Paused = new(SetupPausedBy.Person, "paused by you", Now) });
        WorkspaceSetupFile.Save(_home, new WorkspaceSetupPlan("home", Four, 1, 2) { Created = Now, Stopped = Now });
        await TickAsync(world, config);

        Assert.Empty(none);
        Assert.Equal(0, world.RegistrationsRead);
        Assert.Empty(world.Published);
    }

    /// <summary>A taken set-up is one at work, as an open one is: <i>setting up</i>.</summary>
    [Fact]
    public void A_taken_set_up_stands_as_setting_up()
    {
        var plan = new WorkspaceSetupPlan("work", ["atlas"], 1, 2);
        QuestView taken = new("q1", "ask #a1", "atlas", SetupQuests.Title(SetupQuests.SetUp, Day), "", "Taken");

        var standing = Assert.Single(WorkspaceSetup.Stand(plan, [WorkspaceSetupStandIn.Row("atlas")], [taken], new Dictionary<string, int>(), Driven(2)));

        Assert.Equal(SetupState.Open, standing.State);
        Assert.Equal("q1", standing.Quest);
        Assert.Equal("setting up", WorkspaceSetup.Word(SetupState.Open));
    }

    /// <summary>The head line, in the design's own words (D124 §4.4).</summary>
    [Fact]
    public void The_head_line_says_each_state_in_the_designs_words()
    {
        var plan = new WorkspaceSetupPlan("work", [], 1, 2) { Paused = new(SetupPausedBy.Pilot, "paused after the pilot", Now) };
        List<SetupStanding> standings =
        [
            new("a", SetupState.SetUp, null, ""), new("b", SetupState.SetUp, null, ""), new("c", SetupState.Waiting, "q1", ""),
            new("d", SetupState.Open, "q2", ""), .. Enumerable.Range(0, 22).Select(n => new SetupStanding($"r{n}", SetupState.ToGo, null, "")),
            new("e", SetupState.Declined, "q3", ""),
        ];

        Assert.Equal(
            "Setting up — 2 set up · 1 waiting for your review · 1 setting up · 22 to go · 1 declined — paused after the pilot",
            WorkspaceSetup.Summary(plan, standings));
        Assert.Equal("Setting up — 1 to go — paused by you",
            WorkspaceSetup.Summary(plan with { Paused = new(SetupPausedBy.Person, "", Now) }, [new("a", SetupState.ToGo, null, "")]));
        Assert.Equal("Setting up — 1 set up — stopped",
            WorkspaceSetup.Summary(plan with { Paused = null, Stopped = Now }, [new("a", SetupState.SetUp, null, "")]));
    }

    // ---------------------------------------------------------------- the file (§4.1)

    [Fact]
    public void The_plan_file_reads_back_what_was_written_in_utf8_with_lf_and_a_file_that_does_not_read_is_none()
    {
        var plan = new WorkspaceSetupPlan("Work Things", ["atlas", "billing"], 1, 2)
        {
            Created = Now,
            Paused = new(SetupPausedBy.Person, "paused by you", Now),
            PilotResumed = true,
            Published = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["atlas"] = "q1" },
            Skipped = new Dictionary<string, SetupSkip>(StringComparer.OrdinalIgnoreCase)
            {
                ["billing"] = new(SetupRefusals.NoOwnTree, "its sessions run in its checkout.", Now),
            },
        };

        WorkspaceSetupFile.Save(_home, plan);
        var path = WorkspaceSetupFile.PathOf(_home, "Work Things");
        var bytes = File.ReadAllBytes(path);
        var (read, problem) = WorkspaceSetupFile.Load(_home, "work things");

        Assert.StartsWith(Path.Combine(_home, "setup"), path, StringComparison.Ordinal);
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
        Assert.DoesNotContain("\r", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
        Assert.Null(problem);
        Assert.Equal("Work Things", read!.Workspace);
        Assert.Equal(plan.Order, read.Order);
        Assert.Equal((1, 2, true, (DateTimeOffset?)null), (read.AtOnce, read.Pilot, read.PilotResumed, read.Stopped));
        Assert.Equal(plan.Paused, read.Paused);
        Assert.Equal(Now, read.Created);
        Assert.Equal("q1", read.Published["ATLAS"]);
        Assert.Equal(plan.Skipped["billing"], read.Skipped["billing"]);

        File.WriteAllText(path, "{ not json");
        var (broken, why) = WorkspaceSetupFile.Load(_home, "Work Things");
        Assert.Null(broken);
        Assert.NotNull(why);
        Assert.Empty(WorkspaceSetupFile.All(_home));
    }

    // ---------------------------------------------------------------- the service's doors

    /// <summary>
    /// What the plan reads through the client, as the service spells it: the registry's own word that a row is registered
    /// (absent is no), the strikes as the snapshot derives them, and this machine's session records with where each ran.
    /// </summary>
    [Fact]
    public async Task The_client_reads_the_registrys_own_word_the_strikes_and_this_machines_runs()
    {
        using var client = new ServiceClient("http://service.example", null, new HttpClient(new Canned(new()
        {
            ["/api/registry"] = """
                [{ "repository": "atlas", "adopted": true, "registered": true, "summary": "Maps.", "owns": [], "accepts": [] },
                 { "repository": "billing", "adopted": true, "summary": "Declared, and the host said nothing.", "owns": [], "accepts": [] },
                 { "repository": "cargo", "adopted": false, "registered": false, "owns": [], "accepts": [] }]
                """,
            ["/api/sessions"] = """
                [{ "id": "s1", "repository": "atlas", "quest": "q1", "state": "failed" },
                 { "id": "s2", "repository": "billing", "quest": "q1", "state": "failed" },
                 { "id": "origin/s3", "repository": "atlas", "quest": "q1", "state": "failed" },
                 { "id": "s4", "repository": "atlas", "quest": "q2", "state": "completed" }]
                """,
        })));

        var rows = await client.RegistrationsAsync();
        var strikes = await client.StrikesAsync();
        var runs = await client.SessionRunsAsync();

        Assert.Equal([true, false, false], rows.Select(row => row.Registered));
        Assert.Equal(2, strikes["q1"]);
        Assert.False(strikes.ContainsKey("q2"));
        Assert.Equal([new("s1", "atlas"), new("s2", "billing"), new SessionRun("s4", "atlas")], runs);
    }

    /// <summary>Answers each path with what it was given, whatever the query — the doors' own JSON.</summary>
    private sealed class Canned(Dictionary<string, string> answers) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(answers.TryGetValue(request.RequestUri!.AbsolutePath, out var body)
                ? new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") }
                : new HttpResponseMessage(System.Net.HttpStatusCode.NotFound) { Content = new StringContent("""{ "error": "no" }""") });
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>Drivable here, each in trees of its own, on an agent that rides the protocol door, at <paramref name="cap"/>.</summary>
    private static DriverConfig Driven(int cap) =>
        new[] { "atlas", "billing", "cargo", "docs", "echo" }.Aggregate(
            DriverConfig.Empty with { Adapter = "claude-code-acp", Cap = cap },
            (config, repository) => config.WithDrivable(repository, true).WithTrees(repository, true));

    private static WorkspaceSetupStandIn World(params string[] repositories) =>
        new WorkspaceSetupStandIn().With([.. repositories.Select(repository => WorkspaceSetupStandIn.Row(repository))]);

    private Task<WorkspaceSetupPreview> PreviewAsync(WorkspaceSetupStandIn world, DriverConfig config, WorkspaceSetupOptions? options = null) =>
        WorkspaceSetup.PreviewAsync(world, config, SessionWire.Acp, _home, "work", options ?? new(), Day);

    private async Task<WorkspaceSetupPlan> PressAsync(WorkspaceSetupStandIn world, DriverConfig config, WorkspaceSetupOptions? options = null)
    {
        var outcome = await WorkspaceSetup.PressAsync(await PreviewAsync(world, config, options), world, _home, Now);
        Assert.True(outcome.Written, outcome.Message);
        return outcome.Plan!;
    }

    private Task<IReadOnlyList<string>> TickAsync(WorkspaceSetupStandIn world, DriverConfig config) =>
        WorkspaceSetup.TickAsync(world, config, SessionWire.Acp, _home, Day, Now);

    private Task<IReadOnlyList<SetupStanding>> StandAsync(WorkspaceSetupStandIn world, DriverConfig config) =>
        WorkspaceSetup.StandAsync(world, config, Plan());

    private WorkspaceSetupPlan Plan() => WorkspaceSetupFile.Load(_home, "work").Plan ?? throw new Xunit.Sdk.XunitException("no plan for `work`");

    private static List<string> Asked(WorkspaceSetupStandIn world) => [.. world.Published.Select(published => published.To)];
}
