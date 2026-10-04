using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>Ask Daoris's <c>agent</c> proposal (HELP6): an Update or a pin, where the Agents screen offers it.</summary>
public sealed class HelpAgentProposalsTests : HelpProposalsFixture
{
    [Theory]
    [InlineData("update", "claude-code", null, "daoris agent update claude-code", "its own updater")]
    [InlineData("update", "claude-code-acp", null, "daoris agent update claude-code-acp", "from 0.84.0 to the newest release")]
    [InlineData("pin", "claude-code", "2.1.300", "daoris agent pin claude-code 2.1.300", "Pin `claude-code` to 2.1.300")]
    [InlineData("pin", "claude-code-acp", "0.85.1", "daoris agent pin claude-code-acp 0.85.1", "to 0.85.1")]
    public void An_agent_is_updated_or_pinned_where_the_agents_screen_offers_it(
        string action, string agent, string? version, string terminal, string says)
    {
        var plan = HelpProposals.Plan(Of("agent", action, agent, version), DriverConfig.Empty, Machine);

        Assert.Null(plan.Refusal);
        Assert.Equal(terminal, plan.Terminal);
        Assert.Contains(says, plan.Describe);
    }

    /// <summary>What the Agents screen would not offer, or its route would refuse, is refused in the route's words.</summary>
    [Theory]
    [InlineData("update", "dsh", null, "offers no Update")]
    [InlineData("update", "codex-acp", null, "not installed")]
    // UX6e2: an agent is installed on its page in the Agents place, its Ways in.
    [InlineData("update", "codex-acp", null, "install it under Agents → the agent's page → Ways in → Install, or with `daoris agent install codex-acp`.")]
    [InlineData("update", "gpt-agent", null, "no agent `gpt-agent`")]
    [InlineData("pin", "dsh", "1.0.0", "no package or release channel")]
    [InlineData("pin", "claude-code", "latest", "never a pointer such as latest")]
    [InlineData("pin", "claude-code", "2.0.1", "before its release manifests were signed")]
    [InlineData("pin", "claude-code-acp", "latest", "one exact release")]
    public void An_agent_action_the_route_would_refuse_is_refused(string action, string agent, string? version, string says)
    {
        var plan = HelpProposals.Plan(Of("agent", action, agent, version), DriverConfig.Empty, Machine);

        Assert.Contains(says, plan.Refusal);
        Assert.Null(plan.Apply);
    }

    [Fact]
    public async Task An_agent_update_or_pin_starts_the_agents_screens_own_action_and_its_end_is_said()
    {
        var (update, doors, later) = await ApplyAsync(Of("agent", "update", "claude-code-acp"));
        var (_, pinned, _) = await ApplyAsync(Of("agent", "pin", "claude-code", "2.1.300") with { Id = "p8" });

        Assert.True(update.Applied);
        Assert.Equal(["HARNESS_ACTION claude-code-acp update"], doors.Calls);
        Assert.Equal(["HARNESS_ACTION claude-code pin 2.1.300"], pinned.Calls);
        Assert.Contains("Started", update.Told);
        Assert.Equal("applied", HelpProposals.Find(_home, "p6")!.State);
        // What the Apply did goes into the conversation first…
        Assert.Equal([update.Told], later);

        // …and the action's end after it, however it ended.
        doors.Ended!(0, null);
        doors.Ended!(1, "the pointer did not answer");
        Assert.Contains("finished", later[1]);
        Assert.Contains("the pointer did not answer", later[2]);
    }

    private static HelpProposal Default(string agent, string? account, string? workspace = null) =>
        new("p7", "agent", "default", agent, workspace, account, null, "the person asked", "h1", "proposed");

    /// <summary>
    /// HELP10: an account made the default, for the machine or a workspace, as the Agents screen's *Make default* and
    /// *use for a workspace* make it — a door's default is its owner's (AGT7), so the command names the owner.
    /// </summary>
    [Theory]
    [InlineData("claude-code", "work", null, "daoris agent profile default claude-code work", "This machine runs `claude-code` as `work` by default.")]
    [InlineData("claude-code-acp", "work", "work", "daoris agent profile default claude-code work --workspace work",
        "Sessions in `work` run `claude-code` as `work`.")]
    public void An_account_is_made_the_default_for_the_machine_or_a_workspace(
        string agent, string account, string? workspace, string terminal, string says)
    {
        var plan = HelpProposals.Plan(Default(agent, account, workspace), DriverConfig.Empty, Machine);

        Assert.Null(plan.Refusal);
        Assert.Equal(terminal, plan.Terminal);
        Assert.Contains(says, plan.Describe);
    }

    /// <summary>HELP10: an account or a workspace a helper can invent is refused, as `daoris agent profile default` refuses it.</summary>
    [Theory]
    [InlineData("claude-code", "play", null, "`claude-code` has no account `play` on this machine — accounts that exist: work")]
    [InlineData("dsh", "work", null, "`dsh` has no account `work` on this machine — accounts that exist: (none)")]
    [InlineData("claude-code", null, null, "names the account")]
    [InlineData("claude-code", "work", "elsewhere", "there is no workspace `elsewhere` on this machine")]
    [InlineData("gpt-agent", "work", null, "no agent `gpt-agent`")]
    public void A_default_the_route_would_refuse_is_refused(string agent, string? account, string? workspace, string says)
    {
        var plan = HelpProposals.Plan(Default(agent, account, workspace), DriverConfig.Empty, Machine);

        Assert.Contains(says, plan.Refusal);
    }

    [Fact]
    public async Task A_default_is_made_through_the_agents_screens_own_action()
    {
        var (applied, doors, later) = await ApplyAsync(Default("claude-code-acp", "work", "work"));

        Assert.True(applied.Applied);
        Assert.Equal(["HARNESS_ACTION claude-code-acp profile-default work work"], doors.Calls);
        Assert.Contains("Applied", applied.Told);
        Assert.Equal([applied.Told], later);
        Assert.Equal("applied", HelpProposals.Find(_home, "p7")!.State);
    }

    [Fact]
    public async Task A_default_the_door_refuses_is_settled_refused_in_its_words()
    {
        var (applied, _, _) = await ApplyAsync(Default("claude-code", "work"), new HelpStandInDoors { DefaultRefusal = "Daoris manages no toolchain for `claude-code`." });

        Assert.False(applied.Applied);
        Assert.Contains("Daoris manages no toolchain", applied.Told);
        Assert.Equal("refused", HelpProposals.Find(_home, "p7")!.State);
    }

    /// <summary>The machine with lists of its own (TOOL4g): the machine's <c>work, play</c>, and <c>lab</c>'s <c>work</c>.</summary>
    private static readonly HelpMachineFacts Listed = Machine with
    {
        Workspaces = ["default", "work", "lab"],
        Wiring = new HarnessSettings
        {
            Rotation = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase) { ["claude-code"] = ["work", "play"] },
            WorkspaceRotation = new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>(StringComparer.OrdinalIgnoreCase)
            {
                ["lab"] = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase) { ["claude-code"] = ["play"] },
            },
        },
    };

    /// <summary>
    /// TOOL4g (D130 §3.1): a default is where its scope's starts begin within its own list, so Ask Daoris's <c>default</c>
    /// refuses an account the list does not hold, as the terminal and the screen do; a scope with no list takes any account.
    /// </summary>
    [Theory]
    [InlineData("work", "lab", "`claude-code`'s list in `lab` is play, and `work` is not in it", "`daoris agent profile order claude-code play work --workspace lab`")]
    public void A_default_outside_its_scopes_own_list_is_refused_naming_the_order_that_adds_it(
        string account, string? workspace, string says, string door)
    {
        var plan = HelpProposals.Plan(Default("claude-code-acp", account, workspace), DriverConfig.Empty, Listed);

        Assert.Contains(says, plan.Refusal);
        Assert.Contains(door, plan.Refusal);
    }

    [Theory]
    [InlineData("work", null)]
    [InlineData("work", "work")]
    public void A_default_inside_its_scopes_list_or_in_a_scope_with_none_is_made(string account, string? workspace)
    {
        var plan = HelpProposals.Plan(Default("claude-code", account, workspace), DriverConfig.Empty, Listed);

        Assert.Null(plan.Refusal);
    }

    private static HelpProposal Use(string agent, UseChange change, string? workspace = null) =>
        new("p9", "agent", "use", agent, workspace, null, null, "the person asked", "h1", "proposed") { AccountUse = change };

    /// <summary>
    /// TOOL4g's <c>use</c> (D130 §9, §16.6): how a scope's list is used, as <c>daoris agent profile use</c> takes it, said in its
    /// words; a door's accounts are its owner's (AGT7), so the command and the sentence name the owner.
    /// </summary>
    [Fact]
    public void How_a_list_is_used_is_judged_and_said_as_the_terminal_says_it()
    {
        var machine = HelpProposals.Plan(
            Use("claude-code-acp", new UseChange("order", "play", Early: false, Near: 85)), DriverConfig.Empty, Listed);
        var lab = HelpProposals.Plan(Use("claude-code", new UseChange(Use: "goal"), "lab"), DriverConfig.Empty, Listed);
        var none = HelpProposals.Plan(Use("claude-code", new UseChange(NoKeep: true)), DriverConfig.Empty, Listed);

        Assert.Null(machine.Refusal);
        Assert.Equal("daoris agent profile use claude-code order --keep play --early off --near 85", machine.Terminal);
        Assert.Equal("On this machine, `claude-code` uses its accounts one by one, in order, keeps `play` for conversations, does not "
            + "switch before the limit and counts an account near its limit at 85%.", machine.Describe);
        Assert.Equal("daoris agent profile use claude-code goal --workspace lab", lab.Terminal);
        Assert.Equal("In `lab`, `claude-code` makes the most of its accounts.", lab.Describe);
        Assert.Equal("daoris agent profile use claude-code --no-keep", none.Terminal);
        Assert.Contains("keeps no account for conversations", none.Describe);
    }

    /// <summary><c>profile use</c>'s refusals, in its words, before the person ever sees a card.</summary>
    [Theory]
    [InlineData("claude-code", null, null, null, false, null, null, "a use names how the list is used")]
    [InlineData("claude-code", "work", "goal", null, false, null, null, "`work` has no list of its own for `claude-code`")]
    [InlineData("claude-code", null, "spread", null, false, null, null, "`spread` is not a way to use accounts")]
    [InlineData("claude-code", null, null, null, false, null, 49, "a whole percent from 50 to 99, not 49")]
    [InlineData("claude-code", null, null, "solo", false, null, null, "`solo` is not in `claude-code`'s list on this machine (work, then play)")]
    [InlineData("claude-code", "lab", null, "play", false, null, null, "holds no account but `play`")]
    [InlineData("claude-code", "elsewhere", "goal", null, false, null, null, "there is no workspace `elsewhere`")]
    [InlineData("gpt-agent", null, "goal", null, false, null, null, "no agent `gpt-agent`")]
    public void A_use_the_route_would_refuse_is_refused(
        string agent, string? workspace, string? use, string? keep, bool noKeep, bool? early, int? near, string says)
    {
        var plan = HelpProposals.Plan(Use(agent, new UseChange(use, keep, noKeep, early, near), workspace), DriverConfig.Empty, Listed);

        Assert.Contains(says, plan.Refusal);
    }

    [Fact]
    public async Task A_use_is_applied_through_the_screens_own_door_and_a_refusal_there_settles_it_refused()
    {
        var (applied, doors, later) = await ApplyAsync(Use("claude-code-acp", new UseChange("order", Early: true), "lab"), facts: Listed);
        var (refused, _, _) = await ApplyAsync(
            Use("claude-code", new UseChange(Use: "goal")) with { Id = "p10" }, new HelpStandInDoors { UseRefusal = "the list moved." }, Listed);

        Assert.True(applied.Applied);
        Assert.Equal(["ACCOUNT_USE claude-code-acp use=order keep= noKeep=False early=True near= lab"], doors.Calls);
        Assert.Equal([applied.Told], later);
        Assert.False(refused.Applied);
        Assert.Contains("the list moved.", refused.Told);
        Assert.Equal("refused", HelpProposals.Find(_home, "p10")!.State);
    }

    /// <summary>
    /// The file's four fields, as the service's writer is to write them (TOOL4g): a field left out is no change, and a
    /// <c>keep</c> of JSON null keeps none. A file of any other door names none of them.
    /// </summary>
    [Fact]
    public void The_use_doors_fields_are_read_from_its_file()
    {
        System.IO.File.WriteAllText(Path.Combine(HelpProposals.FolderOf(_home), "u1.json"), """
            { "id": "u1", "kind": "agent", "door": "use", "target": "claude-code", "workspace": "lab", "value": null,
              "use": "order", "keep": null, "early": false, "near": 85, "why": "the person asked", "state": "proposed",
              "by": { "session": "h1" } }
            """);
        File("u2", "agent", "default", "claude-code", value: "work");

        var read = HelpProposals.Find(_home, "u1")!;

        Assert.Equal(new UseChange("order", null, NoKeep: true, Early: false, Near: 85), read.AccountUse);
        Assert.Null(HelpProposals.Find(_home, "u2")!.AccountUse);
    }

    /// <summary>A pin already installed ends before its start is answered: its end is still said second.</summary>
    [Fact]
    public async Task An_action_that_ends_at_once_is_said_after_what_the_Apply_did()
    {
        var (applied, _, later) = await ApplyAsync(Of("agent", "pin", "claude-code", "2.1.300"), new HelpStandInDoors { EndsAtOnce = true });

        Assert.Equal(2, later.Count);
        Assert.Equal(applied.Told, later[0]);
        Assert.Contains("finished", later[1]);
    }
}

public sealed partial class HelpStandInDoors
{
    public Action<int, string?>? Ended { get; private set; }

    /// <summary>An action that ends before its start is answered: a pin already installed does.</summary>
    public bool EndsAtOnce { get; init; }

    public Task StartAgentActionAsync(string harness, string action, string? version, Action<int, string?> ended, CancellationToken ct)
    {
        Calls.Add($"HARNESS_ACTION {harness} {action} {version}".TrimEnd());
        Ended = ended;
        if (EndsAtOnce) ended(0, null);
        return Task.CompletedTask;
    }

    /// <summary>What the default's door refuses with, as `HARNESS_ACTION` would; null to make it.</summary>
    public string? DefaultRefusal { get; init; }

    public Task SetDefaultAccountAsync(string harness, string account, string? workspace, CancellationToken ct)
    {
        if (DefaultRefusal is { } refused) throw new DriverException(refused);
        Calls.Add($"HARNESS_ACTION {harness} profile-default {account} {workspace}".TrimEnd());
        return Task.CompletedTask;
    }

    /// <summary>What the use's door refuses with, as `ACCOUNT_USE` would; null to make it (TOOL4g).</summary>
    public string? UseRefusal { get; init; }

    public Task SetAccountUseAsync(string harness, UseChange change, string? workspace, CancellationToken ct)
    {
        if (UseRefusal is { } refused) throw new DriverException(refused);
        Calls.Add($"ACCOUNT_USE {harness} use={change.Use} keep={change.Keep} noKeep={change.NoKeep} early={change.Early} near={change.Near} {workspace}".TrimEnd());
        return Task.CompletedTask;
    }
}
