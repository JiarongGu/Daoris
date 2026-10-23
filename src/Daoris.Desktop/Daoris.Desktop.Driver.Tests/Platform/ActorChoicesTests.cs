using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The actors' one judge and one reading (MODELROLE1c, D205 §3.1, §4.0, §6.1, §6.4, design §13's unit row): <c>Judge</c>'s table,
/// <c>Effective</c>'s for every actor, the reading from today's keys, the write through them, Chat and Brainstorm made explicit on every
/// change of <c>adapter</c>, an older build's projection, an entry that does not read, and the drain's Intake off.
/// </summary>
public sealed class ActorChoicesTests
{
    private static readonly ActorChoice Opus = new("claude-code", "opus", "high");
    private static readonly ActorChoice Sonnet = new("claude-code", "sonnet");
    private static readonly ActorChoice Codex = new("codex-acp");

    public static TheoryData<string, ActorChoice[]?, string?> Judged => new()
    {
        { "build", null, null },
        { "build", [Opus, Sonnet], null },
        { "build", [Opus, Sonnet, Codex, new("claude-code", "haiku")], null },
        { "build", [Opus, Sonnet, Codex, new("claude-code", "haiku"), new("dsh")], "at most 4" },
        { "build", [], "cannot be off" },
        { "chat", [], "cannot be off" },
        { "intake", [], null },
        { "ask-daoris", [], null },
        { "build", [Opus, Opus], "choice 2 is choice 1 again" },
        // An explicit tool alias `default` is a model, not account default.
        { "build", [new("claude-code", "default"), new("claude-code")], null },
        { "build", [new(" claude-code")], "spaces around it" },
        { "build", [new("claude-code", "")], "is empty" },
        { "build", [new("claude-code", "opus", "hi\u0007gh")], "control character" },
        { "brainstorm", [Opus, Codex], null },
        { "brainstorm", [Opus, Sonnet], "voices, one each" },
        { "builder", [Opus], "is not an actor" },
    };

    [Theory]
    [MemberData(nameof(Judged))]
    public void The_judge_refuses_alike_for_every_door(string actor, ActorChoice[]? choices, string? refusal)
    {
        var said = ActorChoices.Judge(actor, choices);

        if (refusal is null) Assert.Null(said);
        else Assert.Contains(refusal, said, StringComparison.Ordinal);
    }

    [Fact]
    public void As_before_each_actor_reads_today_s_keys_at_account_default()
    {
        var config = DriverConfig.Empty with { Adapter = "claude-code" };
        config = config.WithIntake("codex-acp").WithHelper("claude-code-acp");

        Assert.Equal([new ActorEffective("claude-code", null, null, "before")], ActorChoices.Effective("build", config));
        Assert.Equal([new ActorEffective("claude-code", null, null, "before")], ActorChoices.Effective("chat", config));
        Assert.Equal([new ActorEffective("claude-code", null, null, "before")], ActorChoices.Effective("brainstorm", config));
        Assert.Equal([new ActorEffective("codex-acp", null, null, "before")], ActorChoices.Effective("intake", config));
        Assert.Equal([new ActorEffective("claude-code-acp", null, null, "before")], ActorChoices.Effective("ask-daoris", config));
        // Second opinion and Partner name no agent of their own, and Helper as before is HelperChoice.Candidates: their callers decide.
        Assert.Empty(ActorChoices.Effective("opinion", config));
        Assert.Empty(ActorChoices.Effective("partner", config));
        Assert.Empty(ActorChoices.Effective("helper", config));
        // Absent means off, as today.
        Assert.Empty(ActorChoices.Effective("intake", DriverConfig.Empty));
        Assert.Equal("before", ActorView.Of("build", config).State);
        Assert.Equal("off", ActorView.Of("intake", DriverConfig.Empty).State);
    }

    [Fact]
    public void A_set_order_is_read_in_its_order_each_entry_carrying_what_it_names()
    {
        var config = ActorChoices.With(DriverConfig.Empty, "build", [Opus, Sonnet]);

        var order = ActorChoices.Effective("build", config);

        Assert.Equal([new ActorEffective("claude-code", "opus", "high", "1"), new ActorEffective("claude-code", "sonnet", null, "2")], order);
        Assert.Equal(new ActorAsked("claude-code", new("entry", "opus", true), new("entry", "high", true)), order[0].Asked);
        Assert.Equal(new ActorAsked("claude-code", new("entry", "sonnet", true), new("account-default", null, false)), order[1].Asked);
        Assert.Equal("set", ActorView.Of("build", config).State);
    }

    [Fact]
    public void A_pick_a_handed_agent_and_a_fence_take_the_actor_s_entries_for_their_agent()
    {
        var config = ActorChoices.With(DriverConfig.Empty, "build", [Codex, Opus, Sonnet]);
        config = ActorChoices.With(config, "opinion", [Opus]);

        Assert.Equal([new ActorEffective("claude-code", "opus", "high", "pick")], ActorChoices.Effective("build", config, pick: "claude-code"));
        Assert.Equal([new ActorEffective("dsh", null, null, "handed")], ActorChoices.Effective("build", config, handed: "dsh"));
        // A rule's agents decide who; each takes the actor's entries for it, else runs at account default.
        Assert.Equal(
            [new ActorEffective("codex-acp", null, null, "rule-default"), new ActorEffective("claude-code", "opus", "high", "1")],
            ActorChoices.Effective("opinion", config, fence: ["codex-acp", "claude-code"]));
    }

    [Fact]
    public void Setting_build_writes_adapter_through_and_keeps_chat_and_brainstorm_on_the_agent_they_ran()
    {
        var config = DriverConfig.Empty with { Adapter = "claude-code" };

        var after = ActorChoices.With(config, "build", [Codex, Opus], out var kept);

        Assert.Equal("codex-acp", after.Adapter);
        Assert.Equal(["chat", "brainstorm"], kept);
        Assert.Equal([new ActorEffective("claude-code", null, null, "1")], ActorChoices.Effective("chat", after));
        Assert.Equal([new ActorEffective("claude-code", null, null, "1")], ActorChoices.Effective("brainstorm", after));
    }

    [Fact]
    public void Set_build_then_clear_chat_then_change_build_leaves_chat_on_the_agent_it_ran()
    {
        // Round 3 (Codex 7): a cleared Chat reads `adapter` until the next write that would change it, which freezes Chat where it stood.
        var config = ActorChoices.With(DriverConfig.Empty with { Adapter = "claude-code" }, "build", [Codex]);
        config = ActorChoices.With(config, "chat", null);
        Assert.Equal("codex-acp", ActorChoices.FirstAgent("chat", config));

        config = ActorChoices.With(config, "build", [Opus], out var kept);

        Assert.Equal(["chat"], kept);
        Assert.Equal("codex-acp", ActorChoices.FirstAgent("chat", config));
        Assert.Equal("claude-code", ActorChoices.FirstAgent("brainstorm", config));
        Assert.Equal("claude-code", config.Adapter);
    }

    [Fact]
    public void A_write_that_leaves_adapter_as_it_was_makes_nothing_explicit()
    {
        var config = DriverConfig.Empty with { Adapter = "claude-code" };

        var after = ActorChoices.With(config, "build", [Opus, Codex], out var kept);

        Assert.Empty(kept);
        Assert.False(after.Actors.ContainsKey("chat"));
        Assert.Equal("claude-code", ActorChoices.FirstAgent("chat", after));
    }

    [Fact]
    public void Intake_and_ask_daoris_write_their_keys_through_and_off_is_absent()
    {
        var config = ActorChoices.With(DriverConfig.Empty, "intake", [Codex, Opus]);
        config = ActorChoices.With(config, "ask-daoris", []);

        Assert.Equal("codex-acp", config.IntakeAdapter);
        Assert.Null(config.HelperAdapter);
        Assert.Equal("off", ActorView.Of("ask-daoris", config).State);
    }

    [Fact]
    public void The_order_survives_the_file_and_an_older_build_reads_the_legacy_projection()
    {
        var config = ActorChoices.With(DriverConfig.Empty with { Adapter = "claude-code" }, "build", [Codex, Opus]);
        config = ActorChoices.With(config, "intake", [Sonnet]);

        var text = config.ToJson();
        var read = DriverConfig.Parse(text);

        Assert.Equal(config.Actors["build"], read.Actors["build"]);
        Assert.Equal(config.Actors["chat"], read.Actors["chat"]);
        Assert.Empty(read.ActorErrors);
        // What a build before row c reads (round 4, Codex 4): `adapter` for Build, Chat and an unnamed Brainstorm, Build's first agent;
        // `intakeAdapter`; no model anywhere in the keys it knows.
        Assert.Contains("\"adapter\": \"codex-acp\"", text, StringComparison.Ordinal);
        Assert.Contains("\"intakeAdapter\": \"claude-code\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void An_entry_that_does_not_read_holds_its_starts_and_is_never_read_as_before()
    {
        var read = DriverConfig.Parse("""{ "adapter": "claude-code", "actors": { "build": [{ "model": "opus" }], "chat": "codex-acp", "later": [] } }""");

        Assert.Contains("names no agent", read.ActorErrors["build"], StringComparison.Ordinal);
        Assert.Contains("not a list", read.ActorErrors["chat"], StringComparison.Ordinal);
        var held = Assert.Throws<ActorUnreadableException>(() => ActorChoices.Effective("build", read));
        Assert.Contains("actors.build", held.Message, StringComparison.Ordinal);
        Assert.Equal("unreadable", ActorView.Of("build", read).State);
        // An actor this build does not know is not read, and kept by the writer where it stands.
        Assert.Contains("\"later\"", ActorChoices.With(read, "build", [Opus]).ToJson(), StringComparison.Ordinal);
        // Setting it again reads.
        Assert.Equal("claude-code", ActorChoices.FirstAgent("build", ActorChoices.With(read, "build", [Opus])));
    }

    [Fact]
    public void The_old_intake_and_helper_doors_take_their_actor_back_to_as_before()
    {
        var config = ActorChoices.With(DriverConfig.Empty, "intake", [Codex]);

        config = config.WithIntake("claude-code-acp");

        Assert.False(config.Actors.ContainsKey("intake"));
        Assert.Equal("claude-code-acp", ActorChoices.FirstAgent("intake", config));
    }

    [Fact]
    public void The_drain_turns_a_set_intake_off_in_what_effective_reads()
    {
        var config = ActorChoices.With(DriverConfig.Empty, "intake", [Codex]);

        var drained = InstallUpdate.Drained(config);

        Assert.Empty(ActorChoices.Effective("intake", drained));
        Assert.Null(ActorChoices.FirstAgent("intake", drained));
    }

    [Fact]
    public void A_refused_order_changes_nothing()
    {
        var config = DriverConfig.Empty;

        Assert.Throws<DriverException>(() => ActorChoices.With(config, "build", []));
        Assert.Empty(config.Actors);
    }
}
