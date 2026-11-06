using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// Each seeded role's order as its <c>ROLES</c> row carries it, as <c>daoris role</c> says it, and the role
/// door's <c>SET_ROLE</c> the one press that sets one, judged by the driver's one judge and written through the staffing's one write.
/// And the row's proof that a Chat started after Build changes keeps its agent: the press's
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
    /// FOUNDATION1w-s (the design §19.15): on a home whose staffing the store keeps, the press is the staffing's one write, in the store alone;
    /// a hand edit of the file is read nowhere (D232).
    /// </summary>
    [Fact]
    public async Task On_a_wired_home_the_press_writes_the_store_alone_and_a_hand_edit_is_read_nowhere()
    {
        var staffing = Wired("""{ "actors": { "build": [{ "agent": "claude-code" }], "chat": [{ "agent": "claude-code" }], "brainstorm": [{ "agent": "claude-code" }], "intake": [{ "agent": "claude-code" }] } }""");
        try
        {
            var module = Module();

            await AnswerAsync(module, "SET_ROLE", Staff("intake", "intake", []));

            Assert.Empty(staffing.Read()!.Actors["intake"]);
            File.WriteAllText(DriverConfigPath, File.ReadAllText(DriverConfigPath).Replace("\"claude-code\"", "\"codex-acp\""));
            Assert.Equal(StaffingChecks.Same, staffing.Check(StaffingCheckAt.Look).How);
            Assert.Equal("claude-code", ActorChoices.FirstAgent("build", DriverConfig.Load(DriverConfigPath)));
        }
        finally
        {
            RoleStaffingPort.Unwire(DriverConfigPath);
        }
    }

    [Fact]
    public async Task ONEWAY1a_an_order_set_by_the_role_door_is_read_back_and_written_through_with_chat_and_brainstorm_kept()
    {
        Wired("""{ "actors": { "build": [{ "agent": "claude-code" }], "chat": [{ "agent": "claude-code" }], "brainstorm": [{ "agent": "claude-code" }] } }""");
        try
        {
            var module = Module();

            await AnswerAsync(module, "SET_ROLE", Staff("build", "build",
                [new { agent = "codex-acp" }, new { agent = "claude-code", model = "opus", effort = "high" }]));

            var config = DriverConfig.Load(DriverConfigPath);
            Assert.Equal("codex-acp", ActorChoices.FirstAgent("build", config));
            Assert.Equal([new ActorChoice("claude-code")], config.Actors["chat"]);
            Assert.Equal([new ActorChoice("claude-code")], config.Actors["brainstorm"]);
            var rows = (await AnswerAsync(module, "ROLES")).GetProperty("roles").EnumerateArray().ToList();
            var actors = rows.Where(row => row.TryGetProperty("staffing", out var staffing) && staffing.ValueKind == System.Text.Json.JsonValueKind.Object)
                .Select(row => row.GetProperty("staffing")).ToList();
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
        Wired("""{ "actors": { "build": [{ "agent": "claude-code" }], "chat": [{ "agent": "claude-code" }], "brainstorm": [{ "agent": "claude-code" }], "intake": [{ "agent": "claude-code" }] } }""");
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

    /// <summary>The staffing's own read is folded into the role read: no route answers it apart.</summary>
    [Fact]
    public async Task The_staffing_read_is_the_role_reads_and_actors_is_no_route()
    {
        File.WriteAllText(DriverConfigPath, """{ "actors": { "build": [{ "agent": "claude-code" }] } }""");
        var module = Module();

        await RefusalAsync(module, "ACTORS");

        var rows = (await AnswerAsync(module, "ROLES")).GetProperty("roles").EnumerateArray().ToList();
        Assert.All(rows.Where(row => row.GetProperty("seeded").GetBoolean()), row => Assert.Equal(row.GetProperty("id").GetString(),
            row.GetProperty("staffing").GetProperty("id").GetString()));
    }

    /// <summary>ONEWAY1b: the door's wire is Build's first agent's, read from its order rather than the old key; the pipe where it does not resolve.</summary>
    [Fact]
    public void ONEWAY1b_the_doors_wire_reads_builds_first_agent_and_refuses_one_this_build_does_not_know()
    {
        var loop = Loop();
        var build = new Dictionary<string, IReadOnlyList<ActorChoice>>(StringComparer.Ordinal) { ["build"] = [new ActorChoice("claude-code-acp")] };

        Assert.Equal(true, DriverContext.UnadoptedOf(loop, DriverConfig.Empty with { Actors = build }));
        // A door this build does not know is not answered as the pipe: the refusal names the agents the machine has.
        var unknown = Assert.Throws<UnknownDoorException>(() => DriverContext.UnadoptedOf(loop, DriverConfig.Empty with
        {
            Actors = new Dictionary<string, IReadOnlyList<ActorChoice>>(StringComparer.Ordinal) { ["build"] = [new ActorChoice("nobody-knows")] },
        }));
        Assert.Contains("`nobody-knows` is not a door this build knows", unknown.Message);
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
        Wired("""{ "actors": { "build": [{ "agent": "talk" }], "chat": [{ "agent": "talk" }] } }""");
        try
        {
            var service = new DriverModuleStartFromTests.StandIn(root);
            var loop = Loop();
            var adapters = new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
            {
                ["talk"] = Refusing("talk"), ["other"] = Refusing("other"),
            });
            var client = new ServiceClient("http://stand-in", null, new HttpClient(service));
            var chat = new ChatRunner(
                client, adapters, loop.Home, loop.Processes, loop.Output, new HarnessRoster(adapters, HarnessSettingsPath), loop.Events);
            await loop.ComeUpAsync(client, chat);
            var module = new DriverModule(Bus, loop);

            await AnswerAsync(module, "SET_ROLE", Staff("build", "build", [new { agent = "other" }]));
            Assert.Equal("other", ActorChoices.FirstAgent("build", DriverConfig.Load(DriverConfigPath)));

            var started = await AnswerAsync(module, "START_CHAT", new { repository = "engine", text = "Fix the build.", ownTree = false });

            Assert.Contains("talk spawns nothing", started.GetProperty("message").GetString(), StringComparison.Ordinal);
        }
        finally
        {
            RoleStaffingPort.Unwire(DriverConfigPath);
        }
    }

    /// <summary>An adapter on the protocol door that refuses to spawn, saying its own name, so a start's answer tells which it took.</summary>
    private static ISessionAdapter Refusing(string name) => TestDoors.Door(name, transport: Transport.Protocol, interactive: true, spawn: new TestDoors.RefusingSpawn($"{name} spawns nothing"));
}
