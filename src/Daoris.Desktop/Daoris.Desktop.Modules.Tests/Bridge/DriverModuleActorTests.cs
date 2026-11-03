using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// MODELROLE1c (D205 §8.2, §9.4): <c>ACTORS</c>, each actor's order as <c>daoris driver role</c> says it, and since ONEWAY1a the role
/// door's <c>SET_ROLE</c> the one press that sets one, judged by the driver's one judge and written through the staffing's one write; the
/// retired <c>SET_ACTOR</c> answers nothing. And the row's proof that a Chat started after Build changes keeps its agent: the press's
/// adapters spawn nothing, each refusing in its own words, so the answer names which one the start took.
/// </summary>
public sealed class DriverModuleActorTests : DriverModuleBridge
{
    /// <summary>A seeded role's staffing set over <c>SET_ROLE</c>, its record the work it takes on the machine.</summary>
    private static object Staff(string id, string work, object[]? choices) => new
    {
        change = "set", id, role = new { name = id, takes = new[] { work }, scope = new { kind = "machine" } }, choices,
    };

    /// <summary>The home's staffing wired to its store and imported from <paramref name="file"/>, as a composition root wires it.</summary>
    private RoleStaffing Wired(string file)
    {
        File.WriteAllText(DriverConfigPath, file);
        var staffing = RoleStaffing.Wire(Home, () => StoreSettled.Yes);
        Assert.Equal(StaffingImported.Done, staffing.ImportFile().How);
        return staffing;
    }

    /// <summary>
    /// FOUNDATION1w-s (the design §19.15): on a home whose staffing the store keeps, the press is the staffing's one write, the store's
    /// staffing changed and the file its projection; a hand edit since is read nowhere until the person takes it.
    /// </summary>
    [Fact]
    public async Task On_a_wired_home_the_press_writes_the_store_and_projects_the_file_and_a_hand_edit_waits_found()
    {
        var staffing = Wired("""{ "adapter": "claude-code", "intakeAdapter": "claude-code" }""");
        try
        {
            var module = Module();

            await AnswerAsync(module, "SET_ROLE", Staff("intake", "intake", []));

            Assert.Empty(staffing.Read()!.Actors["intake"]);
            Assert.DoesNotContain("intakeAdapter", File.ReadAllText(DriverConfigPath));
            File.WriteAllText(DriverConfigPath, File.ReadAllText(DriverConfigPath).Replace("\"claude-code\"", "\"codex-acp\""));
            Assert.Equal(StaffingChecks.Found, staffing.Check(StaffingCheckAt.Look).How);
            Assert.Equal("claude-code", DriverConfig.Load(DriverConfigPath).Adapter);
        }
        finally
        {
            RoleStaffingPort.Unwire(DriverConfigPath);
        }
    }

    /// <summary>FOUNDATION1w-s part 5: <c>ACTORS</c> answers what the check kept, and the screen's *Take it* and *Dismiss* press it.</summary>
    [Fact]
    public async Task Actors_answers_each_found_change_and_the_screens_take_it_and_dismiss_answer_it()
    {
        var staffing = Wired("""{ "adapter": "claude-code", "intakeAdapter": "claude-code" }""");
        try
        {
            var module = Module();
            File.WriteAllText(DriverConfigPath, """{ "adapter": "codex-acp" }""");
            staffing.Check(StaffingCheckAt.Look);

            var found = Assert.Single((await AnswerAsync(module, "ACTORS")).GetProperty("found").EnumerateArray().ToList());
            var changes = found.GetProperty("changes").EnumerateArray().ToList();
            Assert.Contains(changes, change => change.GetProperty("role").GetString() == "intake" && change.GetProperty("applied").GetBoolean());
            Assert.Contains(changes, change => change.GetProperty("role").GetString() == "build"
                && change.GetProperty("now").GetProperty("choices")[0].GetProperty("agent").GetString() == "codex-acp");

            await AnswerAsync(module, "STAFFING_FOUND_ANSWER", new { found = found.GetProperty("id").GetInt64(), answer = "take" });

            Assert.Equal("codex-acp", DriverConfig.Load(DriverConfigPath).Adapter);
            Assert.Empty((await AnswerAsync(module, "ACTORS")).GetProperty("found").EnumerateArray());

            File.WriteAllText(DriverConfigPath, """{ "adapter": "other-acp" }""");
            staffing.Check(StaffingCheckAt.Look);
            var second = staffing.Found().Single().Id;
            await AnswerAsync(module, "STAFFING_FOUND_ANSWER", new { found = second, answer = "dismiss" });
            Assert.Equal("codex-acp", DriverConfig.Load(DriverConfigPath).Adapter);
            Assert.Contains("is taken (`take`) or dismissed", await RefusalAsync(module, "STAFFING_FOUND_ANSWER", new { found = second, answer = "keep" }));
        }
        finally
        {
            RoleStaffingPort.Unwire(DriverConfigPath);
        }
    }

    [Fact]
    public async Task ONEWAY1a_an_order_set_by_the_role_door_is_read_back_and_written_through_with_chat_and_brainstorm_kept()
    {
        Wired("""{ "adapter": "claude-code" }""");
        try
        {
            var module = Module();

            await AnswerAsync(module, "SET_ROLE", Staff("build", "build",
                [new { agent = "codex-acp" }, new { agent = "claude-code", model = "opus", effort = "high" }]));

            var config = DriverConfig.Load(DriverConfigPath);
            Assert.Equal("codex-acp", config.Adapter);
            Assert.Equal([new ActorChoice("claude-code")], config.Actors["chat"]);
            Assert.Equal([new ActorChoice("claude-code")], config.Actors["brainstorm"]);
            var actors = (await AnswerAsync(module, "ACTORS")).GetProperty("actors").EnumerateArray().ToList();
            Assert.Equal(ActorChoices.Ids, actors.Select(actor => actor.GetProperty("id").GetString()));
            var build = actors.Single(actor => actor.GetProperty("id").GetString() == "build");
            Assert.Equal("set", build.GetProperty("state").GetString());
            Assert.Equal(["1 codex-acp", "2 claude-code opus high"], build.GetProperty("choices").EnumerateArray().Select(entry =>
                $"{entry.GetProperty("from").GetString()} {entry.GetProperty("agent").GetString()}"
                + (entry.TryGetProperty("model", out var model) && model.GetString() is { } m ? $" {m}" : "")
                + (entry.TryGetProperty("effort", out var effort) && effort.GetString() is { } e ? $" {e}" : "")));
        }
        finally
        {
            RoleStaffingPort.Unwire(DriverConfigPath);
        }
    }

    /// <summary>ONEWAY1a: off for Intake by an empty order, its default by `default` (what *Clear order* became), and a refused order writing nothing.</summary>
    [Fact]
    public async Task ONEWAY1a_off_for_intake_its_default_and_a_refused_order_writing_nothing_through_the_role_door()
    {
        Wired("""{ "adapter": "claude-code", "intakeAdapter": "claude-code" }""");
        try
        {
            var module = Module();

            await AnswerAsync(module, "SET_ROLE", Staff("intake", "intake", []));
            Assert.Empty(ActorChoices.Effective("intake", DriverConfig.Load(DriverConfigPath)));

            // ONEWAY1b: the default is the seed's written order, off for Intake, never as before.
            await AnswerAsync(module, "SET_ROLE", new { change = "default", id = "intake" });
            Assert.Empty(DriverConfig.Load(DriverConfigPath).Actors["intake"]);

            var before = File.ReadAllText(DriverConfigPath);
            var refusal = await RefusalAsync(module, "SET_ROLE", Staff("build", "build", []));
            Assert.Contains("cannot be off", refusal, StringComparison.Ordinal);
            Assert.Equal(before, File.ReadAllText(DriverConfigPath));
        }
        finally
        {
            RoleStaffingPort.Unwire(DriverConfigPath);
        }
    }

    /// <summary>ONEWAY1a: the retired route answers nothing and writes nothing.</summary>
    [Fact]
    public async Task ONEWAY1a_set_actor_is_no_route()
    {
        File.WriteAllText(DriverConfigPath, """{ "adapter": "claude-code" }""");
        var module = Module();

        await RefusalAsync(module, "SET_ACTOR", new { actor = "build", choices = new object[] { new { agent = "codex-acp" } } });

        Assert.Equal("claude-code", DriverConfig.Load(DriverConfigPath).Adapter);
    }

    /// <summary>ONEWAY1b: the door's wire is Build's first agent's, read from its order rather than the old key; the pipe where it does not resolve.</summary>
    [Fact]
    public void ONEWAY1b_the_doors_wire_reads_builds_first_agent_and_is_the_pipe_where_it_does_not_resolve()
    {
        var loop = Loop();
        var build = new Dictionary<string, IReadOnlyList<ActorChoice>>(StringComparer.Ordinal) { ["build"] = [new ActorChoice("claude-code-acp")] };

        Assert.Equal(SessionWire.Acp, DriverContext.DoorOf(loop, DriverConfig.Empty with { Adapter = "claude-code", Actors = build }));
        Assert.Equal(SessionWire.Pipe, DriverContext.DoorOf(loop, DriverConfig.Empty with
        {
            Adapter = "claude-code-acp", Actors = new Dictionary<string, IReadOnlyList<ActorChoice>>(StringComparer.Ordinal) { ["build"] = [new ActorChoice("nobody-knows")] },
        }));
    }

    /// <summary>
    /// 🔴 The row's proof (design §14 row c, round 3's Codex 8): Chat's agent is read through its actor, so a chat started after Build
    /// changed agent still opens on the agent it ran before, never on Build's new one.
    /// </summary>
    [Fact]
    public async Task A_chat_started_after_build_changes_agent_keeps_its_own()
    {
        var root = Path.Combine(Home, "engine");
        Directory.CreateDirectory(root);
        Wired("""{ "adapter": "talk" }""");
        try
        {
            var service = new DriverModuleStartFromTests.StandIn(root);
            var loop = Loop();
            var adapters = new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
            {
                ["talk"] = new Refusing("talk"), ["other"] = new Refusing("other"),
            });
            var client = new ServiceClient("http://stand-in", null, new HttpClient(service));
            var chat = new ChatRunner(
                client, adapters, loop.Home, loop.Processes, loop.Output, new HarnessRoster(adapters, HarnessSettingsPath), loop.Events);
            await loop.ComeUpAsync(client, chat);
            var module = new DriverModule(Bus, loop);

            await AnswerAsync(module, "SET_ROLE", Staff("build", "build", [new { agent = "other" }]));
            Assert.Equal("other", DriverConfig.Load(DriverConfigPath).Adapter);

            var started = await AnswerAsync(module, "START_CHAT", new { repository = "engine", text = "Fix the build.", ownTree = false });

            Assert.Contains("talk spawns nothing", started.GetProperty("message").GetString(), StringComparison.Ordinal);
        }
        finally
        {
            RoleStaffingPort.Unwire(DriverConfigPath);
        }
    }

    /// <summary>An adapter on the protocol door that refuses to spawn, saying its own name, so a start's answer tells which it took.</summary>
    private sealed class Refusing(string name) : ISessionAdapter
    {
        public string Name => name;

        public SessionWire Wire => SessionWire.Acp;

        public bool Interactive => true;

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) =>
            throw new InvalidOperationException($"{name} spawns nothing");

        public ProcessStartInfo PrepareChat(ChatTarget target, IReadOnlyList<string>? command) =>
            throw new InvalidOperationException($"{name} spawns nothing");
    }
}
