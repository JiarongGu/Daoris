using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// Ask Daoris's <c>workflow</c> proposal (WORKFLOW1h-b, D157 point 13, the workflow design §8): a whole version of a named
/// workflow, new or an edit on the newest, for a repository or a workspace, and optionally its choice there. Judged as the editor
/// judges a version (<see cref="WorkflowNamed"/>), its base held to the newest, its diff said by <see cref="WorkflowDiff"/> with the
/// person's part first, and each declaration it needs where it applies. A card that needs something first is a draft with no
/// Apply; Apply is the person's press, which judges the base again and saves through the <c>ask-daoris:</c> door (§8.4).
/// </summary>
public sealed class HelpWorkflowProposalsTests : HelpProposalsFixture
{
    private const string Id = "docs-to-pr";

    private const string Merge = """[{"id":"work","kind":"work"},{"id":"land","kind":"landing","form":"merge","accept":"you"}]""";

    private const string LookThenBranch =
        """[{"id":"work","kind":"work"},{"id":"look","kind":"look"},{"id":"land","kind":"landing","form":"branch","accept":"you"}]""";

    private const string Automatic =
        """[{"id":"work","kind":"work"},{"id":"land","kind":"landing","form":"branch","accept":"automatic","plugin":"pr-plugin"},{"id":"pr","kind":"pull-request"}]""";

    /// <summary>`engine` lands on a branch its rule names a pattern for, and chooses the workflow as its default.</summary>
    private static readonly DriverConfig Chosen = WorkflowSelection.Apply(
        DriverConfig.Empty.WithLanding("engine", new LandingRule(LandingForm.Branch, "work/{quest}-{slug}")),
        new WorkflowUse(Id, "engine", null, null));

    /// <summary>The proposal's file as the service's box writes it (WORKFLOW1h-a), read back as the driver reads it.</summary>
    private HelpProposal Propose(string steps, int? @base = null, bool choose = false, string scope = "engine", string? name = null)
    {
        var node = new JsonObject
        {
            ["id"] = "p6", ["proposed"] = "2026-10-11T10:00:00.0000000+00:00", ["by"] = new JsonObject { ["session"] = "h1" },
            ["kind"] = "workflow", ["scope"] = scope, ["workflow"] = Id, ["base"] = @base, ["choose"] = choose,
            ["steps"] = JsonNode.Parse(steps), ["why"] = "the person asked", ["state"] = "proposed", ["note"] = null,
        };

        // The box writes `name` only when one is given (WORKFLOW1h-name).
        if (name is not null) node["name"] = name;
        System.IO.File.WriteAllText(Path.Combine(HelpProposals.FolderOf(_home), "p6.json"), node.ToJsonString());
        return HelpProposals.Find(_home, "p6")!;
    }

    private void Saved(string steps, string name = "Documentation to a pull request") =>
        WorkflowStore.Add(_home, new WorkflowVersionAdded(Id, name, "terminal", "2026-10-09T09:00:00Z", JsonNode.Parse(steps)), kept: []);

    /// <summary>The machine with this home's workflows, each repository in the workspace `work`.</summary>
    private HelpMachineFacts Here(HelpMachineFacts? from = null) => (from ?? Machine) with
    {
        Workflows = WorkflowStore.List(_home),
        Registered = [("engine", "work", null), ("game", "work", null)],
    };

    private static HelpWorkflowCard Card(HelpPlan plan)
    {
        Assert.Null(plan.Refusal);
        return Assert.IsType<HelpWorkflowCard>(plan.Workflow);
    }

    /// <summary>
    /// FABRIC-PIPELINE1a (the pipeline design §1.3, §2.1): a proposal carries steps, and a verification pipeline's versions are graphs, so
    /// an agent's proposal can neither change one nor choose one for a default; FABRIC-PIPELINE1i lets it propose a graph and a route.
    /// </summary>
    [Fact]
    public void A_proposal_on_a_verification_pipeline_is_refused_as_its_versions_are_graphs()
    {
        WorkflowStore.Add(_home, new WorkflowVersionAdded(Id, "Verify the line", "terminal", "2026-10-21T09:00:00Z", null, JsonNode.Parse(
            """{"schemaVersion":2,"entries":["gates"],"end":"gates","nodes":[{"id":"gates","kind":"check","version":1,"reach":"performed"}],"wires":[]}"""))
        {
            Pipeline = WorkflowPipelines.Verification,
        }, kept: []);

        var plan = HelpProposals.Plan(Propose(Merge, @base: 1, choose: true), DriverConfig.Empty, Here());

        Assert.Null(plan.Workflow);
        Assert.Equal(
            "`docs-to-pr` is a verification pipeline, whose versions are graphs, and a proposal carries steps: it changes and chooses none "
            + "yet. `daoris driver workflow apply docs-to-pr <file>` saves a graph version, and `daoris driver workflow use docs-to-pr "
            + "--repository <name> --landed` names it for work that lands.",
            plan.Refusal);
    }

    [Fact]
    public void Its_doors_are_new_edit_and_use()
    {
        Assert.Equal(["new", "edit", "use"], HelpProposalKinds.Find("workflow")!.Doors);
    }

    [Fact]
    public void The_file_reads_its_door_from_its_base()
    {
        var fresh = Propose(Merge, choose: true);
        Assert.Equal(("new", "engine", Id, (int?)null, true), (fresh.Door, fresh.Scope, fresh.Workflow, fresh.Base, fresh.Choose));
        Assert.Equal(2, fresh.Steps!.Value.GetArrayLength());

        Assert.Equal(("edit", (int?)3), (Propose(Merge, @base: 3).Door, Propose(Merge, @base: 3).Base));
    }

    /// <summary>§8.2: a new workflow's card is its first version, every step added, and what applies it.</summary>
    [Fact]
    public void A_new_workflow_s_card_is_its_first_version()
    {
        var plan = HelpProposals.Plan(Propose(Merge), DriverConfig.Empty, Here());
        var card = Card(plan);

        Assert.Equal((Id, Id, (int?)null, 1, "engine", false), (card.Id, card.Name, card.Base, card.Version, card.Scope, card.Choose));
        Assert.Equal(["+ work · work", "+ land · landing: form `merge`; accept `you`"], card.Diff);
        Assert.Equal(["+ land: you accept each piece of work before it lands."], card.YourPart);
        Assert.Empty(card.Outward);
        Assert.Empty(card.Needs);
        Assert.Equal("New work only, once a repository or a workspace chooses `docs-to-pr`: nothing does now.", card.AppliesTo);
        Assert.Equal("daoris driver workflow import <file>", plan.Terminal);
        Assert.StartsWith("Save a new workflow `docs-to-pr` as v1.\nYour part:\n", plan.Describe, StringComparison.Ordinal);
    }

    /// <summary>WORKFLOW1h-name: a new workflow is named by the person's words, on the card and in the saved version; absent, by its id.</summary>
    [Fact]
    public async Task A_new_workflow_is_saved_under_the_name_given_else_its_id()
    {
        var named = Propose(Merge, name: "Docs to a pull request");
        Assert.Equal("Docs to a pull request", named.Name);
        var plan = HelpProposals.Plan(named, DriverConfig.Empty, Here());
        Assert.Equal("Docs to a pull request", Card(plan).Name);

        var applied = await HelpProposals.ApplyAsync(_home, named, plan, new HelpStandInDoors(), _ => { }, CancellationToken.None);
        Assert.True(applied.Applied);
        Assert.Equal("Docs to a pull request", WorkflowStore.Load(_home, Id)!.Read.Name);

        System.IO.File.Delete(WorkflowStore.PathOf(_home, Id));
        Assert.Equal(Id, Card(HelpProposals.Plan(Propose(Merge), DriverConfig.Empty, Here())).Name);
    }

    /// <summary>§8.2: an edit's diff by step id, the person's part first, then the steps, then what leaves with no press.</summary>
    [Fact]
    public void An_edit_s_card_says_your_part_first_then_the_steps_and_the_outward_acts()
    {
        Saved(LookThenBranch);
        var facts = Here(WithPlugin("pr-plugin")) with
        {
            WorkflowRuns = [new WorkflowRunBinding("q1", "engine", "work", Id, WorkflowLevels.Repository, DateTimeOffset.UnixEpoch) { Version = 1 }],
        };

        var plan = HelpProposals.Plan(Propose(Automatic, @base: 1), Chosen, facts);
        var card = Card(plan);

        Assert.Equal(("Documentation to a pull request", (int?)1, 2), (card.Name, card.Base, card.Version));
        Assert.Equal(
            ["− look: you no longer look at it running before it lands.", "− land: you no longer accept each piece of work before it lands.",
             "+ pr: you merge its pull request on the platform."],
            card.YourPart);
        Assert.Equal(
            ["  work · work", "− look · look", "~ land · landing: accept `you` → `automatic`; plugin as declared → `pr-plugin`", "+ pr · pull-request"],
            card.Diff);
        Assert.Equal(["`pr-plugin` pushes each branch with no press of yours."], card.Outward);
        Assert.Empty(card.Needs);
        Assert.Equal("New work only, in `engine`; 1 run keeps the version it started on.", card.AppliesTo);
        Assert.Equal("daoris driver workflow apply docs-to-pr <file> --base 1", plan.Terminal);

        var part = plan.Describe.IndexOf("Your part:", StringComparison.Ordinal);
        var steps = plan.Describe.IndexOf("Steps, by id:", StringComparison.Ordinal);
        var outward = plan.Describe.IndexOf("Without asking you each time:", StringComparison.Ordinal);
        Assert.True(part > 0 && part < steps && steps < outward, plan.Describe);
        Assert.StartsWith("Save `docs-to-pr` v1 → v2 (Documentation to a pull request).", plan.Describe, StringComparison.Ordinal);
    }

    /// <summary>§8.1, D157 §2.6: a base that is not the newest is stale, refused with the newest, never applied over it.</summary>
    [Fact]
    public void A_stale_base_is_refused_with_the_newest()
    {
        Saved(Merge);
        Saved(LookThenBranch);

        Assert.Equal(
            "`docs-to-pr` is at v2 now, not v1: read it again (`daoris driver workflow show docs-to-pr`) and make the change against v2.",
            HelpProposals.Plan(Propose(Merge, @base: 1), Chosen, Here()).Refusal);
    }

    [Fact]
    public void A_new_workflow_whose_id_is_saved_already_is_refused()
    {
        Saved(Merge);

        Assert.Equal(
            "`docs-to-pr` is a workflow here already, at v1: a change to it names its base, v1.",
            HelpProposals.Plan(Propose(Merge), DriverConfig.Empty, Here()).Refusal);
    }

    [Fact]
    public void An_edit_of_no_workflow_is_refused()
    {
        Assert.Equal(
            "there is no workflow `docs-to-pr` on this machine: a new workflow names no base.",
            HelpProposals.Plan(Propose(Merge, @base: 1), DriverConfig.Empty, Here()).Refusal);
    }

    /// <summary>§2.5, §3.9: steps the editor would not save are refused in its words, naming the step.</summary>
    [Fact]
    public void Steps_the_editor_would_not_save_are_refused_in_its_words()
    {
        var plan = HelpProposals.Plan(
            Propose("""[{"id":"work","kind":"work"},{"id":"land","kind":"landing","form":"merge","accept":"automatic"}]"""),
            DriverConfig.Empty, Here());

        Assert.StartsWith("v1, step `land`: only a branch lands with no press", plan.Refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void A_scope_this_machine_does_not_have_is_refused()
    {
        Assert.StartsWith(
            "there is no repository or workspace `nowhere` on this machine",
            HelpProposals.Plan(Propose(Merge, scope: "nowhere"), DriverConfig.Empty, Here()).Refusal,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// §8.1, §8.2: what a step needs that is not declared where it applies, and a kind this build does not run, are *Needs first*:
    /// the card is a draft, and its Apply is refused before anything is saved, the card left standing.
    /// </summary>
    [Fact]
    public async Task What_a_step_needs_first_makes_a_draft_with_no_apply()
    {
        var steps = """
            [{"id":"work","kind":"work"},{"id":"look","kind":"look"},{"id":"land","kind":"landing","form":"branch","accept":"you"},
             {"id":"deploy","kind":"stage","plugin":"pr-plugin","stage":"staging"}]
            """;
        var proposal = Propose(steps, choose: true);
        var plan = HelpProposals.Plan(proposal, DriverConfig.Empty, Here());
        var card = Card(plan);

        Assert.Equal(
            [
                "its look `look` reads the work in an environment, and `engine` declares no review environment: `daoris driver review engine "
                + "<environment> --kind local|deployed --procedure <path>` declares one.",
                "its landing `land` puts the work on a branch, and neither the step nor `engine`'s landing rule names a pattern for it: "
                + "`daoris driver landing engine branch <pattern>` declares one.",
                "its step `deploy` is a `stage`, which this build does not run yet.",
            ],
            card.Needs);
        Assert.Contains("\nNeeds first:\n", plan.Describe, StringComparison.Ordinal);

        var doors = new HelpStandInDoors();
        await Assert.ThrowsAsync<DriverException>(() => HelpProposals.ApplyAsync(_home, proposal, plan, doors, _ => { }, CancellationToken.None));
        Assert.Empty(doors.Calls);
        Assert.Null(WorkflowStore.Load(_home, Id));
        Assert.Equal("proposed", HelpProposals.Find(_home, "p6")!.State);
    }

    /// <summary>§8.4: Apply saves the version through the `ask-daoris:` door, then makes the choice through the driver's file.</summary>
    [Fact]
    public async Task Apply_saves_the_version_through_ask_daoris_and_makes_the_choice()
    {
        var proposal = Propose(Merge, choose: true);
        var plan = HelpProposals.Plan(proposal, DriverConfig.Empty, Here());
        Assert.Equal("daoris driver workflow import <file> && daoris driver workflow use docs-to-pr --repository engine", plan.Terminal);
        Assert.Equal("New work only, in `engine`.", Card(plan).AppliesTo);

        var doors = new HelpStandInDoors();
        var applied = await HelpProposals.ApplyAsync(_home, proposal, plan, doors, _ => { }, CancellationToken.None);

        Assert.True(applied.Applied);
        Assert.Equal($"Applied: `#p6` (`{plan.Terminal}`) — `docs-to-pr` v1 is saved, and `engine` chooses it now.", applied.Told);
        var version = Assert.Single(WorkflowStore.Load(_home, Id)!.Read.Versions);
        Assert.Equal((1, "ask-daoris:p6"), (version.Version, version.Door));
        Assert.Equal(["change"], doors.Calls);
        Assert.Equal(Id, doors.Config.Workflows["engine"].Default);
        Assert.Equal("applied", HelpProposals.Find(_home, "p6")!.State);
    }

    /// <summary>§8.1: the press judges the base again, so a version saved since the card was drawn makes it stale, never applied over.</summary>
    [Fact]
    public async Task The_press_judges_again_and_a_version_saved_since_makes_it_stale()
    {
        Saved(Merge);
        var proposal = Propose("""[{"id":"work","kind":"work"},{"id":"land","kind":"landing","form":"branch","accept":"you"}]""", @base: 1);
        var plan = HelpProposals.Plan(proposal, Chosen, Here());
        Assert.Empty(Card(plan).Needs);
        Saved(Merge);

        var doors = new HelpStandInDoors();
        var applied = await HelpProposals.ApplyAsync(_home, proposal, plan, doors, _ => { }, CancellationToken.None);

        Assert.False(applied.Applied);
        Assert.Equal(
            "Not applied: `#p6` — `docs-to-pr` is at v2 now, not v1: read it again (`daoris driver workflow show docs-to-pr`) and make "
            + "the change against v2.",
            applied.Told);
        Assert.Equal(2, WorkflowStore.Load(_home, Id)!.Read.Versions.Count);
        Assert.Empty(doors.Calls);
        Assert.Equal("refused", HelpProposals.Find(_home, "p6")!.State);
    }

    /// <summary>D48 §6: an edit that says what the newest says changes nothing, said and never refused; its Apply saves nothing.</summary>
    [Fact]
    public async Task An_edit_that_changes_nothing_is_said_and_saves_nothing()
    {
        Saved(Merge);
        var proposal = Propose(Merge, @base: 1);
        var plan = HelpProposals.Plan(proposal, Chosen, Here());

        Assert.Null(plan.Refusal);
        Assert.StartsWith("`docs-to-pr` v1 already says this: applying changes nothing.", plan.Describe, StringComparison.Ordinal);

        var doors = new HelpStandInDoors();
        var applied = await HelpProposals.ApplyAsync(_home, proposal, plan, doors, _ => { }, CancellationToken.None);

        Assert.True(applied.Applied);
        Assert.StartsWith("Applied: `#p6` — nothing changed: ", applied.Told, StringComparison.Ordinal);
        Assert.Single(WorkflowStore.Load(_home, Id)!.Read.Versions);
        Assert.Empty(doors.Calls);
    }
}
