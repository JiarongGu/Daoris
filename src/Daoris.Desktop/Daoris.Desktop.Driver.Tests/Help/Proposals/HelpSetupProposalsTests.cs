using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// Ask Daoris's <c>setup</c> proposal (LAYOUT8, D170 point 1, D117 §6.1): a registered repository set up for every agent, judged
/// by the set-up press's own plan, the one <c>daoris-driver setup &lt;repository&gt; --plan</c> prints, and applied through the
/// press itself: the rule into the repository's scope, then one ask to it, as the terminal's press runs it.
/// </summary>
public sealed class HelpSetupProposalsTests : HelpProposalsFixture
{
    /// <summary>A repository nobody adopted, driven here in a tree of its own, with the doctrine tool found: a press publishes.</summary>
    private static readonly SetupPlan Pressable = new("atlas")
    {
        Workspace = "work",
        Facts = new LayoutFacts { Line = "main", Commit = "0123456789abcdef0123456789abcdef01234567" },
        Tools = new SetupTools("node", "v24.1.0", null, "daoris", "0.9.0", null),
        Case = SetupCase.Whole,
        Title = "Set up this repository for every agent (2026-10-16)",
        Body = "What is asked, and whose it is.",
        Landing = LandingRules.Choose(DriverConfig.Empty, "atlas", "work"),
        Adapter = "claude-code-acp",
    };

    private static HelpMachineFacts With(params SetupPlan[] setups) => Facts with { Setups = setups };

    [Fact]
    public void A_set_up_the_press_would_publish_is_proposed_with_its_plan_and_the_terminals_command()
    {
        var plan = HelpProposals.Plan(Of("setup", "setup", "atlas"), DriverConfig.Empty, With(Pressable));

        Assert.Null(plan.Refusal);
        Assert.Equal("daoris-driver setup atlas", plan.Terminal);
        Assert.Contains("Set `atlas` up for every agent: publish one quest to it in workspace `work`, as your ask", plan.Describe);
        Assert.Contains("“Set up this repository for every agent (2026-10-16)”", plan.Describe);
        Assert.Contains("carried by its own session on `claude-code-acp`", plan.Describe);
        Assert.Contains("merged into its line", plan.Describe);
        Assert.Contains($"add the {SetupPressRules.Rules.Count} doctrine commands its session runs to `atlas`'s rules", plan.Describe);
        Assert.Contains("Daoris writes nothing into `atlas`", plan.Describe);
        // The card's plan is the press's own, which Apply sends as the terminal's press does.
        Assert.Same(Pressable, plan.Setup);
    }

    /// <summary>The registry's spelling is the plan's, so a name typed in another case is judged and said as Repositories lists it.</summary>
    [Fact]
    public void A_repository_is_found_whatever_its_case_and_said_as_the_registry_spells_it()
    {
        var plan = HelpProposals.Plan(Of("setup", "setup", " ATLAS "), DriverConfig.Empty, With(Pressable));

        Assert.Null(plan.Refusal);
        Assert.Equal("daoris-driver setup atlas", plan.Terminal);
    }

    /// <summary>
    /// What `--plan` refuses is refused in its words, every refusal at once and in the press's order (D124 §2.1): never shown to
    /// the person, and the helper hears each door that answers it.
    /// </summary>
    [Fact]
    public void What_the_press_refuses_is_refused_in_its_own_words_every_one_at_once()
    {
        var refused = Pressable with
        {
            Refusals =
            [
                new SetupRefusal(SetupRefusals.NotDriven, "`atlas` is not driven here: drive it with `daoris driver drive atlas`."),
                new SetupRefusal(SetupRefusals.NoOwnTree, "`atlas`'s sessions run in its checkout: `daoris driver trees atlas on`."),
            ],
        };

        var plan = HelpProposals.Plan(Of("setup", "setup", "atlas"), DriverConfig.Empty, With(refused));

        Assert.Equal(
            "`atlas` is not driven here: drive it with `daoris driver drive atlas`. "
            + "`atlas`'s sessions run in its checkout: `daoris driver trees atlas on`.",
            plan.Refusal);
        Assert.Equal("daoris-driver setup atlas", plan.Terminal);
        Assert.Null(plan.Setup);
    }

    [Fact]
    public void A_repository_whose_set_up_was_not_read_is_refused_and_never_guessed()
    {
        var plan = HelpProposals.Plan(Of("setup", "setup", "ghost"), DriverConfig.Empty, With(Pressable));

        Assert.Equal("what a set-up of `ghost` would ask was not read here.", plan.Refusal);
        Assert.Null(plan.Setup);
    }

    [Fact]
    public void A_door_the_kind_does_not_take_yet_is_refused()
    {
        var plan = HelpProposals.Plan(Of("setup", "register", "atlas"), DriverConfig.Empty, With(Pressable));

        Assert.Contains("`register` is not a door of a set-up — `setup`", plan.Refusal);
    }

    /// <summary>
    /// Apply is the press (D50): the plan judged when the person pressed, through the press's own door, its outcome said in the
    /// service's words with the ask and the quest it made and the rules it added, and where those are taken back.
    /// </summary>
    [Fact]
    public async Task Apply_presses_the_set_up_and_says_what_it_published_and_added()
    {
        var doors = new HelpStandInDoors
        {
            SetUp = new SetupOutcome(true, "Published one quest to `atlas`.", "a1b2c3d4", "q1a2b3c4", ["Bash(daoris --version)", "Bash(daoris sync)"]),
        };

        var (applied, _, _) = await ApplyAsync(Of("setup", "setup", "atlas"), doors, With(Pressable));

        Assert.True(applied.Applied);
        Assert.Equal(["SETUP_PRESS atlas"], doors.Calls);
        Assert.Contains("Applied: `#p6` (`daoris-driver setup atlas`) — Published one quest to `atlas`.", applied.Told);
        Assert.Contains("ask `#a1b2c3d4`, quest `#q1a2b3c4`", applied.Told);
        Assert.Contains("added to `atlas`'s rules: `Bash(daoris --version)`, `Bash(daoris sync)`", applied.Told);
        Assert.Contains("Repositories → the repository's page → Setup → Reach", applied.Told);
        Assert.Equal("applied", HelpProposals.Find(_home, "p6")!.State);
    }

    /// <summary>A press the service refuses publishes nothing and takes back the rules it added, as the terminal says it.</summary>
    [Fact]
    public async Task A_press_the_service_refuses_is_settled_refused_in_its_words()
    {
        var doors = new HelpStandInDoors { SetUp = new SetupOutcome(false, "The service refused the ask: no such workspace.", null, null, []) };

        var (applied, _, _) = await ApplyAsync(Of("setup", "setup", "atlas"), doors, With(Pressable));

        Assert.False(applied.Applied);
        Assert.Contains("Not applied: `#p6` (`daoris-driver setup atlas`) — The service refused the ask: no such workspace.", applied.Told);
        Assert.Contains("nothing was published, and the rules this press added were taken back", applied.Told);
        Assert.Equal("refused", HelpProposals.Find(_home, "p6")!.State);
    }
}
