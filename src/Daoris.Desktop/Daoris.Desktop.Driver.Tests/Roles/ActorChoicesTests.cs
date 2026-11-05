using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The actors' one judge and one reading (MODELROLE1c, D205 §3.1, §4.0, §6.1, §6.4, design §13's unit row): <c>Judge</c>'s table,
/// <c>Effective</c>'s for every actor, the reading from today's keys, the write through them, Chat and Brainstorm made explicit on every
/// change of <c>adapter</c>, an older build's projection, an entry that does not read, and the drain's Intake off.
/// </summary>
[Trait(Category.Name, Category.Census)]
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

    /// <summary>Row n: what a start's asked choice reads from its environment is its door's spawn's to say, never a name in the roles' code.</summary>
    [Fact]
    public void The_asked_choice_reads_the_environment_by_the_variables_the_doors_spawn_names()
    {
        var info = new System.Diagnostics.ProcessStartInfo("agent");
        info.Environment["ANTHROPIC_MODEL"] = "opus";
        info.Environment["CLAUDE_CODE_EFFORT_LEVEL"] = "high";

        var claude = ActorAsked.ForSpawn("claude-code", info, new ClaudeCodeSpawn().ChoiceVariables);
        Assert.Equal(("environment", "opus"), (claude.Model.Source, claude.Model.Value));
        Assert.Equal(("environment", "high"), (claude.Effort.Source, claude.Effort.Value));

        var other = ActorAsked.ForSpawn("codex-acp", info, SpawnChoiceVariables.None);
        Assert.Equal("account-default", other.Model.Source);
        Assert.Equal("account-default", other.Effort.Source);
        Assert.Equal(new ClaudeCodeSpawn().ChoiceVariables, new ClaudeAcpSpawn().ChoiceVariables);
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
    public void The_order_survives_the_file()
    {
        var config = ActorChoices.With(DriverConfig.Empty with { Actors = StaffedRoles.On("claude-code") }, "build", [Codex, Opus]);
        config = ActorChoices.With(config, "intake", [Sonnet]);

        var read = DriverConfig.Parse(config.ToJson());

        Assert.Equal(config.Actors["build"], read.Actors["build"]);
        Assert.Equal(config.Actors["chat"], read.Actors["chat"]);
        Assert.Equal(config.Actors["intake"], read.Actors["intake"]);
        Assert.Empty(read.ActorErrors);
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

    /// <summary>
    /// FOUNDATION1 mod-viii: the room's facts read the first agent of an order through the settings' <c>ActorKey.FirstAgent</c>, which the
    /// actors' own reading answers through, so the two are one rule: the same agent for every actor in each state an order is in (stored, off,
    /// as before, as before with no key), and where an entry does not read the settings' is none where the actors' holds the start.
    /// </summary>
    [Fact]
    public void The_settings_first_agent_is_what_the_actors_read_for_every_actor_in_every_state()
    {
        var states = new[]
        {
            DriverConfig.Empty,
            DriverConfig.Empty with { Actors = StaffedRoles.On("claude-code", "codex-acp", "dsh") },
            ActorChoices.With(DriverConfig.Empty with { Actors = StaffedRoles.On("claude-code") }, "build", [Codex, Opus]),
            ActorChoices.With(ActorChoices.With(DriverConfig.Empty with { Actors = StaffedRoles.On("claude-code", "codex-acp") }, "intake", []), "ask-daoris", [Sonnet]),
        };

        foreach (var config in states)
        {
            foreach (var actor in ActorKey.Ids)
            {
                Assert.Equal(ActorChoices.Effective(actor, config).FirstOrDefault()?.Agent, ActorKey.FirstAgent(actor, config));
                Assert.Equal(ActorChoices.FirstAgent(actor, config), ActorKey.FirstAgent(actor, config));
            }
        }

        var unreadable = DriverConfig.Parse("""{ "adapter": "claude-code", "actors": { "build": [{ "model": "opus" }] } }""");
        Assert.Throws<ActorUnreadableException>(() => ActorChoices.FirstAgent("build", unreadable));
        Assert.Null(ActorKey.FirstAgent("build", unreadable));
        Assert.Null(ActorKey.FirstAgent("chat", unreadable));
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

    /// <summary>
    /// s-iv: the page's actors offer a door's session scope. Claude Code's native door carries `max`, its protocol door and the account's
    /// settings never do, and Codex, which states no settings file, offers only what its open has listed.
    /// </summary>
    [Fact]
    public void The_actors_offer_each_door_s_session_efforts_with_max_only_where_a_session_may_carry_it()
    {
        var home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "session-choices", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(home);
        try
        {
            var registry = ProviderRegistry.Of(AdapterSet.Built());

            var native = SessionChoices.Of(home, registry, "claude-code");
            var protocol = SessionChoices.Of(home, registry, "claude-code-acp");

            Assert.Equal([.. AgentSettings.Efforts, "max"], native?.Efforts);
            Assert.Equal(AgentSettings.Efforts, protocol?.Efforts);
            Assert.DoesNotContain("max", AgentSettings.Efforts);
            Assert.Null(SessionChoices.Of(home, registry, "codex-acp"));

            Assert.Null(AgentOffers.Offered(home, "codex-acp", "1.0.0", "acct", ["gpt-5"], ["low", "high"], DateTimeOffset.UtcNow));
            Assert.Equal(["low", "high"], SessionChoices.Of(home, registry, "codex-acp")?.Efforts);
        }
        finally
        {
            GitSeed.Discard(home);
        }
    }
}
