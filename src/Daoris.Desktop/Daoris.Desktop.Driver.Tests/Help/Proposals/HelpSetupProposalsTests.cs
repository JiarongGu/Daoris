using Daoris.Desktop.Driver.Tests;
using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// Ask Daoris's <c>setup</c> proposal (D170 point 1; WORKFLOWCLEAN1 1f): the one door is <c>place</c>, a place's set-up with adoption in its
/// ask, proposed as the plan <c>daoris place … --plan</c> prints and opened on the page that applies it.
/// </summary>
public sealed class HelpSetupProposalsTests : HelpProposalsFixture
{
    /// <summary>The repository-alone door is gone, and a door the kind never had is refused alike, each naming the one door.</summary>
    [Theory]
    [InlineData("setup")]
    [InlineData("register")]
    public void A_door_the_kind_does_not_take_is_refused_naming_the_place_door(string door)
    {
        var plan = HelpProposals.Plan(Of("setup", door, "atlas"), DriverConfig.Empty, Facts);

        Assert.Contains($"`{door}` is not a door of a set-up: the one door is `place`", plan.Refusal);
        Assert.Null(plan.Go);
    }

    [Fact]
    public void The_kind_takes_the_place_door_alone()
    {
        Assert.Equal(["place"], new HelpSetupProposals().Doors);
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
        Assert.Equal("daoris place engine web --preset merge-after-accept --adoption --what-it-builds library --plan", plan.Terminal);
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

    /// <summary>SETUPUX1j: Ask Daoris proposes keeping how a place lands, and the page opens on it with the ask carried whole.</summary>
    [Fact]
    public async Task A_place_door_proposes_keeping_how_it_lands_and_hands_that_ask_to_the_page()
    {
        var (proposal, facts) = await PlacedAsync(new PlaceAsk(["engine"], SetupPresets.Keep) { Opinion = false, Drive = true });

        var plan = HelpProposals.Plan(proposal, DriverConfig.Empty, facts);

        Assert.Null(plan.Refusal);
        Assert.Equal("daoris place engine --preset keep --drive --plan", plan.Terminal);
        Assert.Equal((SetupPresets.Keep, true), (plan.Go!.Setup!.Preset, plan.Go.Setup.Drive));
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
        Assert.StartsWith("daoris place solo ghost", plan.Terminal);
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
