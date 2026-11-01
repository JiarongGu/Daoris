using Daoris.Desktop.Driver.Tests;
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

    // AGENTSPLACE1k: the `place` door, a place's quick set-up proposed as the plan `place … --plan` prints.

    private static async Task<(HelpProposal Proposal, HelpMachineFacts Facts)> PlacedAsync(PlaceAsk ask)
    {
        var planned = await PlacePlans.PlanAsync(new PlacePlanTests.World(), DriverConfig.Empty, "no-home", new SetupPresetsTests.Catalog(), SessionWire.Acp, ask);
        var proposal = Of("setup", "place", ask.Repositories[0]) with { Place = ask };
        return (proposal, Facts with { Places = new Dictionary<string, PlacePlanned> { [proposal.Id] = planned } });
    }

    [Fact]
    public async Task A_place_door_proposes_the_plan_and_hands_the_ask_to_the_page_that_applies_it()
    {
        var ask = new PlaceAsk(["ENGINE", "web"], SetupPresets.Merge) { Adoption = true, Builds = SetupBuilds.Library };
        var (proposal, facts) = await PlacedAsync(ask);

        var plan = HelpProposals.Plan(proposal, DriverConfig.Empty, facts);

        Assert.Null(plan.Refusal);
        Assert.Equal("daoris-driver place engine web --preset merge-after-accept --with adoption --what-it-builds library --plan", plan.Terminal);
        Assert.Contains("`engine`, `web`", plan.Describe);
        Assert.Contains("Set up…", plan.Describe);
        Assert.Contains("nothing is written until you press Apply there", plan.Describe);
        // The registry's spelling, one workspace: the page of that place, with the ask seeded.
        var go = plan.Go!;
        Assert.Equal(("agents", "roles", "aurora", null), (go.View, go.Part, go.Workspace, go.Repository));
        Assert.Equal(["engine", "web"], go.Setup!.Repositories);
        Assert.Equal((SetupPresets.Merge, true, SetupBuilds.Library), (go.Setup.Preset, go.Setup.Adoption, go.Setup.Builds));
        Assert.Equal("aurora", plan.Place!.Workspace);
        Assert.Equal(["engine", "web"], plan.Place.Asked.Repositories);
        Assert.NotEmpty(plan.Place.Digest);
    }

    [Fact]
    public async Task A_place_door_for_one_repository_goes_to_that_repository()
    {
        var (proposal, facts) = await PlacedAsync(new PlaceAsk(["engine"], SetupPresets.Integrated));

        var go = HelpProposals.Plan(proposal, DriverConfig.Empty, facts).Go!;

        Assert.Equal(("aurora", "engine"), (go.Workspace, go.Repository));
    }

    /// <summary>The plan's own refusals, every one at once and in its words; a refused plan opens nothing.</summary>
    [Fact]
    public async Task A_place_the_plan_refuses_is_refused_in_the_plans_words()
    {
        var (proposal, facts) = await PlacedAsync(new PlaceAsk(["solo", "ghost"], SetupPresets.NotDriven) { Drive = true });

        var plan = HelpProposals.Plan(proposal, DriverConfig.Empty, facts);

        Assert.Contains("*Not driven* takes no general driving", plan.Refusal);
        Assert.Contains("ghost", plan.Refusal);
        Assert.Null(plan.Go);
        Assert.Null(plan.Place);
        Assert.StartsWith("daoris-driver place solo ghost", plan.Terminal);
    }

    [Fact]
    public async Task A_place_across_workspaces_is_refused_since_a_set_up_is_one_workspaces()
    {
        var (proposal, facts) = await PlacedAsync(new PlaceAsk(["solo", "engine"], SetupPresets.Merge));

        var plan = HelpProposals.Plan(proposal, DriverConfig.Empty, facts);

        Assert.Contains("one workspace", plan.Refusal);
        Assert.Contains("`default`", plan.Refusal);
        Assert.Contains("`aurora`", plan.Refusal);
        Assert.Null(plan.Go);
    }

    [Fact]
    public void A_place_whose_plan_was_not_read_or_which_carries_no_ask_is_refused_and_never_guessed()
    {
        var unread = Of("setup", "place", "engine") with { Place = new PlaceAsk(["engine"], SetupPresets.Merge) };
        Assert.Contains("was not read here", HelpProposals.Plan(unread, DriverConfig.Empty, Facts).Refusal);
        Assert.Contains("carries its ask", HelpProposals.Plan(Of("setup", "place", "engine"), DriverConfig.Empty, Facts).Refusal);
    }

    /// <summary>The ask is read from the file's <c>place</c>, the first repository standing where the file names none.</summary>
    [Fact]
    public void A_place_ask_is_read_from_the_proposals_file()
    {
        System.IO.File.WriteAllText(Path.Combine(HelpProposals.FolderOf(_home), "p7.json"), """
            {"id":"p7","kind":"setup","door":"place","target":"engine","workspace":null,"value":null,"sentence":null,"why":"asked","by":{"session":"h1"},"state":"proposed",
             "place":{"preset":"pull-request","repositories":["engine","web"],"opinion":true,"look":false,"adoption":true,"builds":"service","drive":true}}
            """);

        var read = HelpProposals.Find(_home, "p7")!;

        Assert.Equal(["engine", "web"], read.Place!.Repositories);
        Assert.Equal(("pull-request", true, false, true, "service", true), (read.Place.Preset, read.Place.Opinion, read.Place.Look, read.Place.Adoption, read.Place.Builds, read.Place.Drive));
        Assert.Equal(["engine", "web"], HelpProposalKinds.Owned(read).Repositories);
    }

    /// <summary>Apply never plans or presses: it settles as a go to the page, with nothing written beside the proposal.</summary>
    [Fact]
    public async Task A_place_applies_as_a_go_and_writes_nothing()
    {
        var (proposal, facts) = await PlacedAsync(new PlaceAsk(["engine"], SetupPresets.Integrated));
        var doors = new HelpStandInDoors();

        var (applied, _, _) = await ApplyAsync(proposal, doors, facts);

        Assert.True(applied.Applied);
        Assert.Empty(doors.Calls);
        Assert.Equal("engine", applied.Go!.Repository);
        Assert.Equal(SetupPresets.Integrated, applied.Go.Setup!.Preset);
        Assert.Contains("Nothing else changed", applied.Told);
        Assert.Equal("applied", HelpProposals.Find(_home, "p6")!.State);
    }
}
