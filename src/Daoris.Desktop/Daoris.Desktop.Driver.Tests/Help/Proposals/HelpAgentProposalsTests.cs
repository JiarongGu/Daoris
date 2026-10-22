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
    /// POOL1b (D197 point 6, the account-pool design §5.3): a stored <c>default</c>, which HELP10 made for the machine or a workspace, is
    /// refused naming <c>order</c>, the door that says where the set's starts begin; a door's accounts are its owner's (AGT7). Before
    /// POOL1b these held the default made, and refused where the account or workspace was none or outside its scope's list.
    /// </summary>
    [Theory]
    [InlineData("claude-code", "work", null)]
    [InlineData("claude-code-acp", "work", "work")]
    [InlineData("claude-code", null, null)]
    public void A_stored_default_is_refused_naming_order(string agent, string? account, string? workspace)
    {
        var plan = HelpProposals.Plan(Default(agent, account, workspace), DriverConfig.Empty, Machine);

        Assert.Equal("`default` is retired: every account of Daoris's serves every workspace (D197), and the order says where starts "
            + $"begin — `daoris agent profile order claude-code {account ?? "<account>"}` puts one first. Propose `order` with the account "
            + "first.", plan.Refusal);
    }

    /// <summary>A stored <c>default</c> reaching its Apply is settled refused with nothing pressed (POOL1b, §5.3).</summary>
    [Fact]
    public async Task A_stored_default_is_settled_refused_at_its_apply_and_nothing_is_pressed()
    {
        var (applied, doors, _) = await ApplyAsync(Default("claude-code-acp", "work", "work"));

        Assert.False(applied.Applied);
        Assert.Contains("`default` is retired", applied.Told);
        Assert.Empty(doors.Calls);
        Assert.Equal("refused", HelpProposals.Find(_home, "p7")!.State);
    }

    /// <summary>
    /// <c>default</c> left the doors with POOL1b, so a refusal names the five left. Before POOL1b this listed six, <c>default</c>
    /// among them.
    /// </summary>
    [Fact]
    public void An_agent_change_of_no_door_names_the_doors_left()
    {
        var plan = HelpProposals.Plan(new HelpProposal("p8", "agent", "inherit", "claude-code", null, null, null, "the person asked", "h1", "proposed"),
            DriverConfig.Empty, Machine);

        Assert.Contains("`update`, `pin`, `use`, `order` or `ready`", plan.Refusal);
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

    private static HelpProposal Use(string agent, UseChange change, string? workspace = null, bool set = true) =>
        new("p9", "agent", "use", agent, workspace, null, null, "the person asked", "h1", "proposed") { AccountUse = change, ForSet = set };

    /// <summary>
    /// TOOL4g's <c>use</c> (D130 §9, §16.6; POOL1b, §5.3): how the set is used, as <c>daoris agent profile use</c> takes it, said in its
    /// words; a door's accounts are its owner's (AGT7), so the command and the sentence name the owner. Before POOL1b it was said for
    /// the machine (<i>On this machine</i>) or a workspace (<i>In `lab`</i>), whose terminal named <c>--workspace</c>.
    /// </summary>
    [Fact]
    public void How_the_set_is_used_is_judged_and_said_as_the_terminal_says_it()
    {
        var machine = HelpProposals.Plan(
            Use("claude-code-acp", new UseChange("order", "play", Early: false, Near: 85)), DriverConfig.Empty, Listed);
        var none = HelpProposals.Plan(Use("claude-code", new UseChange(NoKeep: true)), DriverConfig.Empty, Listed);

        Assert.Null(machine.Refusal);
        Assert.Equal("daoris agent profile use claude-code order --keep play --early off --near 85", machine.Terminal);
        // SWITCH1d: the old names said in the curve's terms, as the file reads them on.
        Assert.Equal("For every workspace, `claude-code` uses its accounts one by one, in order, keeps `play` for conversations, does not "
            + "switch accounts while a session runs and holds its own curve's five-hour window at 85%.", machine.Describe);
        Assert.Equal("daoris agent profile use claude-code --no-keep", none.Terminal);
        Assert.Contains("keeps no account for conversations", none.Describe);
    }

    /// <summary>
    /// POOL1b (§5.3): a <c>use</c> or an <c>order</c> naming a workspace is refused in the terminal's words, and one the service wrote
    /// before the <c>set</c> mark is refused as written for a list that is gone, never applied to the set.
    /// </summary>
    [Fact]
    public void A_workspace_or_an_unmarked_proposal_is_refused_in_d197_s_words()
    {
        var scoped = HelpProposals.Plan(Use("claude-code", new UseChange(Use: "goal"), "lab"), DriverConfig.Empty, Listed);
        var unmarked = HelpProposals.Plan(Use("claude-code", new UseChange(Use: "goal"), set: false), DriverConfig.Empty, Listed);
        var scopedOrder = HelpProposals.Plan(Order("claude-code", ["work"], "lab"), DriverConfig.Empty, Walked);
        var scopedClear = HelpProposals.Plan(Order("claude-code", [], "lab", clear: true), DriverConfig.Empty, Walked);
        var unmarkedOrder = HelpProposals.Plan(Order("claude-code", ["work"]) with { ForSet = false }, DriverConfig.Empty, Walked);

        Assert.Equal("Daoris's accounts serve every workspace (D197), so `workspace` names nothing here.", scoped.Refusal);
        Assert.Equal("Daoris's accounts serve every workspace (D197), so `workspace` names nothing here.", scopedOrder.Refusal);
        Assert.Equal("Daoris's accounts serve every workspace (D197), so `workspace` names nothing here.", scopedClear.Refusal);
        Assert.Equal("this `use` was written for a list that is gone (D197): propose it again.", unmarked.Refusal);
        Assert.Equal("this `order` was written for a list that is gone (D197): propose it again.", unmarkedOrder.Refusal);
    }

    /// <summary>An unmarked <c>use</c> or <c>order</c> that reaches its Apply is settled refused with nothing pressed (POOL1b, §5.3).</summary>
    [Fact]
    public async Task An_unmarked_proposal_is_settled_refused_at_its_apply_and_nothing_is_pressed()
    {
        var (use, useDoors, _) = await ApplyAsync(Use("claude-code", new UseChange(Use: "goal"), set: false), facts: Listed);
        var (order, orderDoors, _) = await ApplyAsync(Order("claude-code", ["work"]) with { Id = "p15", ForSet = false }, facts: Walked);

        Assert.False(use.Applied);
        Assert.Contains("written for a list that is gone (D197): propose it again", use.Told);
        Assert.Empty(useDoors.Calls);
        Assert.False(order.Applied);
        Assert.Empty(orderDoors.Calls);
        Assert.Equal("refused", HelpProposals.Find(_home, "p15")!.State);
    }

    /// <summary>
    /// SWITCH1d (D165 point 2; the switch design §5): the <c>use</c> door's <c>switch</c> and <c>curve</c>, as <c>profile use
    /// --switch</c>, <c>--curve</c> and <c>--no-curve</c> take them, said in the curve's terms.
    /// </summary>
    [Fact]
    public void The_switch_and_the_curve_are_judged_and_said_as_the_terminal_says_them()
    {
        var weekly = new UseCurve(new Dictionary<string, IReadOnlyList<CurvePoint>>
        {
            ["weekly"] = [new CurvePoint(0, 30), new CurvePoint(50, 70), new CurvePoint(100, 100)],
        });
        var own = HelpProposals.Plan(Use("claude-code", new UseChange(Switch: "curve", Curve: weekly)), DriverConfig.Empty, Listed);
        var smart = HelpProposals.Plan(Use("claude-code", new UseChange(Switch: "smart")), DriverConfig.Empty, Listed);
        var off = HelpProposals.Plan(Use("claude-code", new UseChange(Switch: "off", NoCurve: true)), DriverConfig.Empty, Listed);

        Assert.Null(own.Refusal);
        Assert.Equal("daoris agent profile use claude-code --switch curve --curve weekly=0:30,50:70,100:100", own.Terminal);
        Assert.Equal("For every workspace, `claude-code` switches accounts by its own curve and sets its own curve's weekly window to "
            + "0:30, 50:70, 100:100.", own.Describe);
        Assert.Equal("For every workspace, `claude-code` switches accounts by Daoris's curve.", smart.Describe);
        Assert.Equal("daoris agent profile use claude-code --switch off --no-curve", off.Terminal);
        Assert.Equal("For every workspace, `claude-code` does not switch accounts while a session runs and clears its own curve, every window "
            + "back to Daoris's.", off.Describe);
    }

    /// <summary>SWITCH1d: a switch this build does not know, and a curve that breaks a rule, refused in the door's words.</summary>
    [Fact]
    public void A_switch_or_a_curve_the_route_would_refuse_is_refused()
    {
        var unknown = HelpProposals.Plan(Use("claude-code", new UseChange(Switch: "sometimes")), DriverConfig.Empty, Listed);
        var broken = HelpProposals.Plan(
            Use("claude-code", new UseChange(Switch: "curve")) with { AccountCurveRefused = "`weekly` has 0 rows — a window's curve has one to twelve." },
            DriverConfig.Empty, Listed);

        Assert.Contains("`sometimes` is not when to switch accounts — smart (Daoris's curve), curve (your own) or off.", unknown.Refusal);
        Assert.Equal("`weekly` has 0 rows — a window's curve has one to twelve.", broken.Refusal);
    }

    /// <summary>
    /// <c>profile use</c>'s refusals, in its words, before the person ever sees a card. Since POOL1b (§5.3) on the set: an agent with no
    /// account of Daoris's has nothing to use, a kept account is one of the set, and any workspace is refused; before, a scope with no list
    /// of its own was refused, and a workspace's list held a kept account alone.
    /// </summary>
    [Theory]
    [InlineData("claude-code", null, null, null, false, null, null, "a use names how the accounts are used")]
    [InlineData("dsh", null, "goal", null, false, null, null, "`dsh` has no account of Daoris's, so there is nothing to use")]
    [InlineData("claude-code", null, "spread", null, false, null, null, "`spread` is not a way to use accounts")]
    [InlineData("claude-code", null, null, null, false, null, 49, "a whole percent from 50 to 99, not 49")]
    [InlineData("claude-code", null, null, "solo", false, null, null, "`solo` is not one of `claude-code`'s accounts (work, then play)")]
    [InlineData("claude-code", "elsewhere", "goal", null, false, null, null, "so `workspace` names nothing here")]
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
        var (applied, doors, later) = await ApplyAsync(Use("claude-code-acp", new UseChange("order", Early: true)), facts: Listed);
        var (refused, _, _) = await ApplyAsync(
            Use("claude-code", new UseChange(Use: "goal")) with { Id = "p10" }, new HelpStandInDoors { UseRefusal = "the list moved." }, Listed);

        Assert.True(applied.Applied);
        Assert.Equal(["ACCOUNT_USE claude-code-acp use=order keep= noKeep=False early=True near= switch= curve= noCurve=False"], doors.Calls);
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

    /// <summary>
    /// SWITCH1d: the file's <c>switch</c> and <c>curve</c>, as the service's writer writes them: a curve of JSON null takes the
    /// person's curve out, and one that breaks a rule is kept as the twins' sentence the card refuses with.
    /// </summary>
    [Fact]
    public void The_use_doors_switch_and_curve_are_read_from_its_file()
    {
        void Write(string id, string fields) => System.IO.File.WriteAllText(Path.Combine(HelpProposals.FolderOf(_home), $"{id}.json"), $$"""
            { "id": "{{id}}", "kind": "agent", "door": "use", "target": "claude-code", "workspace": null, "value": null, {{fields}},
              "why": "the person asked", "state": "proposed", "by": { "session": "h1" } }
            """);
        Write("s1", """ "switch": "curve", "curve": { "weekly": [[0, 30], [100, 100]] } """);
        Write("s2", """ "curve": null """);
        Write("s3", """ "switch": "curve", "curve": { "weekly": [] } """);

        var set = HelpProposals.Find(_home, "s1")!;
        var cleared = HelpProposals.Find(_home, "s2")!;
        var broken = HelpProposals.Find(_home, "s3")!;

        Assert.Equal("curve", set.AccountUse!.Switch);
        Assert.Equal([new CurvePoint(0, 30), new CurvePoint(100, 100)], set.AccountUse.Curve!["weekly"]);
        Assert.Null(set.AccountCurveRefused);
        Assert.True(cleared.AccountUse!.NoCurve);
        Assert.Equal("`weekly` has 0 rows — a window's curve has one to twelve.", broken.AccountCurveRefused);
    }

    /// <summary>
    /// TOOL4l-b: the use, order and ready doors are listed, since the service's <c>agent_propose</c> writes them; <c>default</c> left with
    /// POOL1b (D197 point 6), listed before it.
    /// </summary>
    [Fact]
    public void The_account_doors_are_listed_beside_update_and_pin()
    {
        Assert.Equal(["update", "pin", "use", "order", "ready"], new HelpAgentProposals().Doors);
    }

    /// <summary>The <c>set</c> mark is read from a <c>use</c> or an <c>order</c> file (POOL1b, §5.3); one without it is unmarked.</summary>
    [Fact]
    public void The_set_mark_is_read_from_a_use_or_an_order_file()
    {
        void Write(string id, string door, string fields) => System.IO.File.WriteAllText(Path.Combine(HelpProposals.FolderOf(_home), $"{id}.json"), $$"""
            { "id": "{{id}}", "kind": "agent", "door": "{{door}}", "target": "claude-code", "workspace": null, "value": null, {{fields}}
              "why": "the person asked", "state": "proposed", "by": { "session": "h1" } }
            """);
        Write("m1", "use", """ "use": "goal", "set": true, """);
        Write("m2", "order", """ "accounts": ["work"], "set": true, """);
        Write("m3", "order", """ "accounts": ["work"], """);

        Assert.True(HelpProposals.Find(_home, "m1")!.ForSet);
        Assert.True(HelpProposals.Find(_home, "m2")!.ForSet);
        Assert.False(HelpProposals.Find(_home, "m3")!.ForSet);
    }

    /// <summary>
    /// The machine of an <c>order</c> (TOOL4l-b): <c>claude-code</c> has three accounts, a list of its own on the machine and a
    /// default and a kept account in it, and <c>lab</c> a list and a default of its own, <c>trial</c> a default only.
    /// </summary>
    private static readonly HelpMachineFacts Walked = Machine with
    {
        Workspaces = ["default", "work", "lab", "trial", "quiet"],
        Doors =
        [
            new HelpDoorFacts("claude-code") { Present = true, Owner = "claude-code", Accounts = ["work", "play", "spare"] },
            new HelpDoorFacts("claude-code-acp") { Present = true, Owner = "claude-code", Accounts = ["work", "play", "spare"] },
        ],
        Wiring = new HarnessSettings
        {
            Rotation = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase) { ["claude-code"] = ["work", "play"] },
            Defaults = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["claude-code"] = "work" },
            WorkspaceRotation = new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>(StringComparer.OrdinalIgnoreCase)
            {
                ["lab"] = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase) { ["claude-code"] = ["play", "spare"] },
            },
            Workspaces = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["lab"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["claude-code"] = "play" },
                ["trial"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["claude-code"] = "spare" },
            },
        }.WithUse("claude-code", new UseChange(Keep: "play")).WithUse("claude-code", new UseChange(Keep: "play"), "lab"),
    };

    private static HelpProposal Order(string agent, string[] accounts, string? workspace = null, bool clear = false) =>
        new("p11", "agent", "order", agent, workspace, null, null, "the person asked", "h1", "proposed")
        {
            AccountOrder = new AccountOrder(accounts, clear), ForSet = true,
        };

    /// <summary>
    /// TOOL4l-b (D130 §9; POOL1b, §5.3): the set's order, the accounts named first and the rest after in the set's order, or cleared back
    /// to the ids' order, as <c>daoris agent profile order</c> takes it, said in its words; a door's accounts are its owner's (AGT7).
    /// Before POOL1b this wrote a scope's list whole (<i>its starts may run on those accounts</i>) or sent a workspace back to the
    /// machine's accounts, clearing its default with <c>profile default --clear</c>.
    /// </summary>
    [Fact]
    public void An_order_is_judged_and_said_as_the_terminal_says_it()
    {
        var first = HelpProposals.Plan(Order("claude-code-acp", ["spare"]), DriverConfig.Empty, Walked);
        var back = HelpProposals.Plan(Order("claude-code", [], clear: true), DriverConfig.Empty, Walked);

        Assert.Null(first.Refusal);
        Assert.Equal("daoris agent profile order claude-code spare", first.Terminal);
        Assert.Equal("`claude-code`'s order is `spare`, then `work`, then `play`, for every workspace: one by one, in order, begins at the "
            + "first, and make the most of them uses the order only to break a tie.", first.Describe);
        Assert.Null(back.Refusal);
        Assert.Equal("daoris agent profile order claude-code --clear", back.Terminal);
        Assert.Equal("`claude-code`'s order goes back to its accounts' ids: `play`, then `spare`, then `work`. No setting is cleared.", back.Describe);
    }

    /// <summary>
    /// <c>profile order</c>'s refusals, in its words, before the person ever sees a card. Since POOL1b no default or kept account binds
    /// the set's order, which holds every account; those rows, a workspace's clear and an unknown workspace were held here before.
    /// </summary>
    [Theory]
    [InlineData("claude-code", new string[0], null, false, "names the accounts to put first, in order, or clears")]
    [InlineData("claude-code", new[] { "work" }, null, true, "never both")]
    [InlineData("claude-code", new[] { "work", "gone" }, null, false, "has no account `gone` on this machine")]
    [InlineData("claude-code", new[] { "work", "work" }, null, false, "`work` is named twice")]
    [InlineData("claude-code", new[] { "work" }, "elsewhere", false, "so `workspace` names nothing here")]
    [InlineData("gpt-agent", new[] { "work" }, null, false, "no agent `gpt-agent`")]
    public void An_order_the_route_would_refuse_is_refused(string agent, string[] accounts, string? workspace, bool clear, string says)
    {
        var plan = HelpProposals.Plan(Order(agent, accounts, workspace, clear), DriverConfig.Empty, Walked);

        Assert.Contains(says, plan.Refusal);
    }

    [Fact]
    public async Task An_order_is_applied_through_the_screens_own_door_and_a_refusal_there_settles_it_refused()
    {
        var (applied, doors, later) = await ApplyAsync(Order("claude-code-acp", ["work", "play", "spare"]), facts: Walked);
        var (back, backDoors, _) = await ApplyAsync(Order("claude-code", [], clear: true) with { Id = "p12" }, facts: Walked);
        var (refused, _, _) = await ApplyAsync(
            Order("claude-code", ["work", "play"]) with { Id = "p13" }, new HelpStandInDoors { OrderRefusal = "the list moved." }, Walked);

        Assert.True(applied.Applied);
        Assert.Equal(["ACCOUNT_USE claude-code-acp order work,play,spare clear=False"], doors.Calls);
        Assert.Equal([applied.Told], later);
        Assert.Equal(["ACCOUNT_USE claude-code order  clear=True"], backDoors.Calls);
        Assert.True(back.Applied);
        Assert.False(refused.Applied);
        Assert.Contains("the list moved.", refused.Told);
        Assert.Equal("refused", HelpProposals.Find(_home, "p13")!.State);
    }

    /// <summary>The order door's fields from the service's file (TOOL4l-a): the list in order, or <c>clear</c>; no other door reads them.</summary>
    [Fact]
    public void The_order_doors_fields_are_read_from_its_file()
    {
        System.IO.File.WriteAllText(Path.Combine(HelpProposals.FolderOf(_home), "o1.json"), """
            { "id": "o1", "kind": "agent", "door": "order", "target": "claude-code", "workspace": null, "value": null,
              "accounts": ["work", "play"], "why": "the person asked", "state": "proposed", "by": { "session": "h1" } }
            """);
        System.IO.File.WriteAllText(Path.Combine(HelpProposals.FolderOf(_home), "o2.json"), """
            { "id": "o2", "kind": "agent", "door": "order", "target": "claude-code", "workspace": "lab", "value": null,
              "clear": true, "why": "the person asked", "state": "proposed", "by": { "session": "h1" } }
            """);
        File("o3", "agent", "default", "claude-code", value: "work");

        Assert.Equal(["work", "play"], HelpProposals.Find(_home, "o1")!.AccountOrder!.Accounts);
        Assert.False(HelpProposals.Find(_home, "o1")!.AccountOrder!.Clear);
        Assert.True(HelpProposals.Find(_home, "o2")!.AccountOrder!.Clear);
        Assert.Null(HelpProposals.Find(_home, "o3")!.AccountOrder);
    }

    /// <summary>The <c>Walked</c> machine with <c>play</c> and the tool's own sign-in cooling (TOOL4l-b), as the cool-off file holds them.</summary>
    private static readonly HelpMachineFacts Cooling = Walked with
    {
        Cooling =
        [
            new CoolingEntry("claude-code", "play", DateTimeOffset.UtcNow.AddHours(2), true, "weekly", DateTimeOffset.UtcNow, "s1a2b3c4"),
            new CoolingEntry("claude-code", null, DateTimeOffset.UtcNow.AddHours(1), false, null, DateTimeOffset.UtcNow, null),
        ],
    };

    private static HelpProposal Ready(string agent, string? account) =>
        new("p14", "agent", "ready", agent, null, null, null, "the person asked", "h1", "proposed") { ReadyAccount = account };

    /// <summary>
    /// TOOL4l-b (D125 §2.3): <i>Try now</i>, an account's cool-off ended early as <c>daoris agent profile ready</c> ends it, for an
    /// account of the door's owner that is cooling, or for the tool's own sign-in; said in its words.
    /// </summary>
    [Fact]
    public void A_ready_is_judged_and_said_as_the_terminal_says_it()
    {
        var account = HelpProposals.Plan(Ready("claude-code-acp", "play"), DriverConfig.Empty, Cooling);
        var own = HelpProposals.Plan(Ready("claude-code", "own"), DriverConfig.Empty, Cooling);

        Assert.Null(account.Refusal);
        Assert.Equal("daoris agent profile ready claude-code play", account.Terminal);
        Assert.StartsWith("Try `play` now: its cool-off ends", account.Describe);
        Assert.Contains("cools again", account.Describe);
        Assert.Null(own.Refusal);
        Assert.Equal("daoris agent profile ready claude-code --own", own.Terminal);
        Assert.StartsWith("End the old cool-off of `claude-code`'s own sign-in", own.Describe);
        Assert.Contains("Daoris starts no session in it", own.Describe);
        Assert.DoesNotContain("starts may run on it again", own.Describe);
    }

    [Theory]
    [InlineData("claude-code", null, "names the account whose cool-off ends, or `own`")]
    [InlineData("claude-code", "gone", "has no account `gone` on this machine")]
    [InlineData("claude-code", "spare", "`spare` is not cooling, so there is nothing to end — cooling now: `play`, its own sign-in")]
    [InlineData("gpt-agent", "play", "no agent `gpt-agent`")]
    public void A_ready_the_route_would_refuse_is_refused(string agent, string? account, string says)
    {
        var plan = HelpProposals.Plan(Ready(agent, account), DriverConfig.Empty, Cooling);

        Assert.Contains(says, plan.Refusal);
    }

    [Fact]
    public void Nothing_cooling_is_nothing_to_end()
    {
        var plan = HelpProposals.Plan(Ready("claude-code", "own"), DriverConfig.Empty, Walked);

        Assert.Contains("no account of `claude-code` is cooling", plan.Refusal);
    }

    [Fact]
    public async Task A_ready_is_applied_through_the_screens_own_door_and_one_no_longer_cooling_settles_refused()
    {
        var (applied, doors, later) = await ApplyAsync(Ready("claude-code-acp", "play"), facts: Cooling);
        var (own, ownDoors, _) = await ApplyAsync(Ready("claude-code", "own") with { Id = "p15" }, facts: Cooling);
        var (gone, _, _) = await ApplyAsync(Ready("claude-code", "play") with { Id = "p16" }, new HelpStandInDoors { ReadyEnded = false }, Cooling);

        Assert.True(applied.Applied);
        Assert.Equal(["ACCOUNT_USE claude-code-acp ready play"], doors.Calls);
        Assert.Equal([applied.Told], later);
        Assert.Equal(["ACCOUNT_USE claude-code ready own"], ownDoors.Calls);
        Assert.True(own.Applied);
        Assert.False(gone.Applied);
        Assert.Contains("is not cooling any more", gone.Told);
        Assert.Equal("refused", HelpProposals.Find(_home, "p16")!.State);
    }

    /// <summary>The ready door's account from the service's file (TOOL4l-a): an id, or <c>own</c>.</summary>
    [Fact]
    public void The_ready_doors_account_is_read_from_its_file()
    {
        System.IO.File.WriteAllText(Path.Combine(HelpProposals.FolderOf(_home), "r1.json"), """
            { "id": "r1", "kind": "agent", "door": "ready", "target": "claude-code", "workspace": null, "value": null,
              "account": "own", "why": "the person asked", "state": "proposed", "by": { "session": "h1" } }
            """);
        File("r2", "agent", "default", "claude-code", value: "work");

        Assert.Equal("own", HelpProposals.Find(_home, "r1")!.ReadyAccount);
        Assert.Null(HelpProposals.Find(_home, "r2")!.ReadyAccount);
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