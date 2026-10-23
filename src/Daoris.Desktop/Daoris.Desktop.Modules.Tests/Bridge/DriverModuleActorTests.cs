using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// MODELROLE1c (D205 §8.2, §9.4): <c>ACTORS</c>, each actor's order as <c>daoris driver actor</c> says it, and <c>SET_ACTOR</c>, the
/// person's press that sets one, judged by the driver's one judge and written through the legacy keys. And the row's proof that a Chat
/// started after Build changes keeps its agent: the press's adapters spawn nothing, each refusing in its own words, so the answer
/// names which one the start took.
/// </summary>
public sealed class DriverModuleActorTests : DriverModuleBridge
{
    [Fact]
    public async Task An_order_set_is_read_back_and_written_through_with_chat_and_brainstorm_kept()
    {
        File.WriteAllText(DriverConfigPath, """{ "adapter": "claude-code" }""");
        var module = Module();

        await AnswerAsync(module, "SET_ACTOR", new
        {
            actor = "build",
            choices = new object[] { new { agent = "codex-acp" }, new { agent = "claude-code", model = "opus", effort = "high" } },
        });

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

    [Fact]
    public async Task Off_for_intake_null_back_to_as_before_and_a_refused_order_writes_nothing()
    {
        File.WriteAllText(DriverConfigPath, """{ "adapter": "claude-code", "intakeAdapter": "claude-code" }""");
        var module = Module();

        await AnswerAsync(module, "SET_ACTOR", new { actor = "intake", choices = Array.Empty<object>() });
        Assert.Null(DriverConfig.Load(DriverConfigPath).IntakeAdapter);
        Assert.Empty(ActorChoices.Effective("intake", DriverConfig.Load(DriverConfigPath)));

        await AnswerAsync(module, "SET_ACTOR", new { actor = "intake", choices = (object[]?)null });
        Assert.False(DriverConfig.Load(DriverConfigPath).Actors.ContainsKey("intake"));

        var before = File.ReadAllText(DriverConfigPath);
        var refusal = await RefusalAsync(module, "SET_ACTOR", new { actor = "build", choices = Array.Empty<object>() });
        Assert.Contains("cannot be off", refusal, StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllText(DriverConfigPath));
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
        File.WriteAllText(DriverConfigPath, """{ "adapter": "talk" }""");
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

        await AnswerAsync(module, "SET_ACTOR", new { actor = "build", choices = new object[] { new { agent = "other" } } });
        Assert.Equal("other", DriverConfig.Load(DriverConfigPath).Adapter);

        var started = await AnswerAsync(module, "START_CHAT", new { repository = "engine", text = "Fix the build.", ownTree = false });

        Assert.Contains("talk spawns nothing", started.GetProperty("message").GetString(), StringComparison.Ordinal);
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
