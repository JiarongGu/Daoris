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
        // SWITCH1d: the old names said in the curve's terms, as the file reads them on.
        Assert.Equal("On this machine, `claude-code` uses its accounts one by one, in order, keeps `play` for conversations, does not "
            + "switch accounts while a session runs and holds its own curve's five-hour window at 85%.", machine.Describe);
        Assert.Equal("daoris agent profile use claude-code goal --workspace lab", lab.Terminal);
        Assert.Equal("In `lab`, `claude-code` makes the most of its accounts.", lab.Describe);
        Assert.Equal("daoris agent profile use claude-code --no-keep", none.Terminal);
        Assert.Contains("keeps no account for conversations", none.Describe);
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
        var off = HelpProposals.Plan(Use("claude-code", new UseChange(Switch: "off", NoCurve: true), "lab"), DriverConfig.Empty, Listed);

        Assert.Null(own.Refusal);
        Assert.Equal("daoris agent profile use claude-code --switch curve --curve weekly=0:30,50:70,100:100", own.Terminal);
        Assert.Equal("On this machine, `claude-code` switches accounts by its own curve and sets its own curve's weekly window to "
            + "0:30, 50:70, 100:100.", own.Describe);
        Assert.Equal("On this machine, `claude-code` switches accounts by Daoris's curve.", smart.Describe);
        Assert.Equal("daoris agent profile use claude-code --switch off --no-curve --workspace lab", off.Terminal);
        Assert.Equal("In `lab`, `claude-code` does not switch accounts while a session runs and clears its own curve, every window "
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
        Assert.Equal(["ACCOUNT_USE claude-code-acp use=order keep= noKeep=False early=True near= switch= curve= noCurve=False lab"], doors.Calls);
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

    /// <summary>TOOL4l-b: the use, order and ready doors are listed, since the service's <c>agent_propose</c> writes them.</summary>
    [Fact]
    public void The_account_doors_are_listed_beside_update_pin_and_default()
    {
        Assert.Equal(["update", "pin", "default", "use", "order", "ready"], new HelpAgentProposals().Doors);
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
            AccountOrder = new AccountOrder(accounts, clear),
        };

    /// <summary>
    /// TOOL4l-b (D130 §9): an account list written whole, or a workspace sent back to the machine's accounts, as
    /// <c>daoris agent profile order</c> takes it, said in its words; a door's accounts are its owner's (AGT7).
    /// </summary>
    [Fact]
    public void An_order_is_judged_and_said_as_the_terminal_says_it()
    {
        var machine = HelpProposals.Plan(Order("claude-code-acp", ["work", "spare", "play"]), DriverConfig.Empty, Walked);
        var lab = HelpProposals.Plan(Order("claude-code", ["spare", "play"], "lab"), DriverConfig.Empty, Walked);
        var back = HelpProposals.Plan(Order("claude-code", [], "lab", clear: true), DriverConfig.Empty, Walked);
        var defaulted = HelpProposals.Plan(Order("claude-code", [], "trial", clear: true), DriverConfig.Empty, Walked);

        Assert.Null(machine.Refusal);
        Assert.Equal("daoris agent profile order claude-code work spare play", machine.Terminal);
        Assert.Equal("On this machine, `claude-code`'s list is `work`, then `spare`, then `play`: its starts may run on those accounts, "
            + "in that order.", machine.Describe);
        Assert.Null(lab.Refusal);
        Assert.Equal("daoris agent profile order claude-code spare play --workspace lab", lab.Terminal);
        Assert.StartsWith("In `lab`, `claude-code`'s list is", lab.Describe);
        Assert.Null(back.Refusal);
        Assert.Equal("daoris agent profile order claude-code --clear --workspace lab; daoris agent profile default claude-code --clear --workspace lab", back.Terminal);
        Assert.Equal("`lab` goes back to this machine's accounts for `claude-code`: its own list and default are cleared.", back.Describe);
        Assert.Null(defaulted.Refusal);
        Assert.Equal("daoris agent profile default claude-code --clear --workspace trial", defaulted.Terminal);
    }

    /// <summary><c>profile order</c>'s refusals, and what the scope binds, in its words, before the person ever sees a card.</summary>
    [Theory]
    [InlineData("claude-code", new string[0], null, false, "names the accounts in walk order, or clears")]
    [InlineData("claude-code", new string[0], null, true, "names the workspace")]
    [InlineData("claude-code", new[] { "work" }, null, true, "never both")]
    [InlineData("claude-code", new[] { "work", "gone" }, null, false, "has no account `gone` on this machine")]
    [InlineData("claude-code", new[] { "work", "work" }, null, false, "`work` is named twice")]
    [InlineData("claude-code", new[] { "play", "spare" }, null, false, "default for `claude-code` is `work`, and the list would not hold it")]
    [InlineData("claude-code", new[] { "work", "spare" }, null, false, "`play` is kept for conversations, and the list does not hold it")]
    [InlineData("claude-code", new[] { "play" }, "lab", false, "`play` is kept for conversations, and the list would hold no other account")]
    [InlineData("claude-code", new[] { "work" }, "elsewhere", false, "there is no workspace `elsewhere`")]
    [InlineData("claude-code", new string[0], "quiet", true, "has no list or default of its own for `claude-code`, so there is nothing to clear")]
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
        var (back, backDoors, _) = await ApplyAsync(Order("claude-code", [], "lab", clear: true) with { Id = "p12" }, facts: Walked);
        var (refused, _, _) = await ApplyAsync(
            Order("claude-code", ["work", "play"]) with { Id = "p13" }, new HelpStandInDoors { OrderRefusal = "the list moved." }, Walked);

        Assert.True(applied.Applied);
        Assert.Equal(["ACCOUNT_USE claude-code-acp order work,play,spare clear=False"], doors.Calls);
        Assert.Equal([applied.Told], later);
        Assert.Equal(["ACCOUNT_USE claude-code order  clear=True lab"], backDoors.Calls);
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