using System.Text.Json.Nodes;
using Daoris.Desktop.Driver.Tests;
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
    private HelpProposal Propose(
        string? steps, int? @base = null, bool choose = false, string scope = "engine", string? name = null,
        string? graph = null, string? route = null, string workflow = Id, string? draft = null)
    {
        var node = new JsonObject
        {
            ["id"] = "p6", ["proposed"] = "2026-10-11T10:00:00.0000000+00:00", ["by"] = new JsonObject { ["session"] = "h1" },
            ["kind"] = "workflow", ["scope"] = scope, ["workflow"] = workflow, ["base"] = @base, ["choose"] = choose,
            ["why"] = "the person asked", ["state"] = "proposed", ["note"] = null,
        };

        // The box writes `name`, `steps`, `graph` and `route` only when given (WORKFLOW1h-name, FABRIC-PIPELINE1i).
        if (steps is not null) node["steps"] = JsonNode.Parse(steps);
        if (graph is not null) node["graph"] = JsonNode.Parse(graph);
        if (route is not null) node["route"] = route;
        if (name is not null) node["name"] = name;
        if (draft is not null) node["draft"] = draft;
        System.IO.File.WriteAllText(Path.Combine(HelpProposals.FolderOf(_home), "p6.json"), node.ToJsonString());
        return HelpProposals.Find(_home, "p6")!;
    }

    private const string Fingerprint = "g1-0123456789abcdef";

    private static string Preset(string id) => WorkflowDefaults.Of(id)!.GraphJson;

    /// <summary>The draft this conversation was told (FLOWCHART2d): the Merge workflow's graph, open for `engine`, as its record keeps it.</summary>
    private HelpMachineFacts Told(string? workflow = null, int? @base = null, string graph = "", HelpMachineFacts? from = null)
    {
        var draft = new JsonObject { ["fingerprint"] = Fingerprint, ["place"] = "engine", ["graph"] = JsonNode.Parse(graph.Length > 0 ? graph : Preset("merge-after-accept")) };
        if (workflow is not null) draft["workflow"] = workflow;
        if (@base is not null) draft["base"] = @base;
        using var document = System.Text.Json.JsonDocument.Parse(draft.ToJsonString());
        var told = WorkflowDraftTold.Read(document.RootElement);
        return Here(from) with { Home = _home, Drafts = WorkflowDraftTold.Told([told.ToldNote()]) };
    }

    /// <summary>Every byte under the home's workflows: what a draft's Apply must leave as it was.</summary>
    private Dictionary<string, string> WorkflowFiles()
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.Workflows);
        return Directory.Exists(folder)
            ? Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).ToDictionary(path => path, path => Convert.ToBase64String(System.IO.File.ReadAllBytes(path)))
            : [];
    }

    /// <summary>
    /// FLOWCHART2e (the flowchart design §4, §8.4): a whole graph proposed against the draft this conversation was told, judged as a save
    /// is, its card said as the draft's changes, with the candidate kept apart from what a save would take, so it saves nothing.
    /// </summary>
    [Fact]
    public void A_change_to_the_open_draft_is_judged_against_the_draft_this_conversation_was_told()
    {
        var proposal = Propose(null, graph: Preset("release"), workflow: "", draft: Fingerprint);
        Assert.Equal(("draft", Fingerprint), (proposal.Door, proposal.Draft));

        var plan = HelpProposals.Plan(proposal, DriverConfig.Empty, Told());
        var card = Card(plan);

        Assert.Equal(Fingerprint, card.Draft);
        Assert.False(card.Saves);
        Assert.Null(card.Graph);
        Assert.Null(card.Steps);
        Assert.Null(card.Use);
        Assert.NotNull(card.Proposed);
        Assert.Contains("step:+find", card.Changes.Select(change => change.Id));
        Assert.Equal(["step:+release"], card.Changes.Single(change => change.Id == "way:+k").Needs.Take(1));
        Assert.Contains("Nothing is saved or chosen", plan.Describe);
        Assert.Equal(("projects", (string?)null), (plan.Go!.View, plan.Go.Part));
    }

    /// <summary>What a save would refuse is the card's to put right, never a refusal of the proposal: the person may take it and fix it.</summary>
    [Fact]
    public void What_a_save_would_refuse_is_on_the_card_to_put_right()
    {
        var graph = JsonNode.Parse(Preset("merge-after-accept"))!;
        graph["nodes"]!.AsArray().Add(new JsonObject { ["id"] = "stray", ["kind"] = "check", ["version"] = 1, ["reach"] = "performed" });
        var unreached = graph.ToJsonString();

        var card = Card(HelpProposals.Plan(Propose(null, graph: unreached, workflow: "", draft: Fingerprint), DriverConfig.Empty, Told()));

        Assert.Contains(card.Problems, problem => problem.Code == GraphRefusal.Unreachable && problem.Node == "stray");
        Assert.Equal(["step:+stray"], card.Changes.Select(change => change.Id));
    }

    [Fact]
    public void A_fingerprint_this_conversation_was_never_told_is_refused()
    {
        var plan = HelpProposals.Plan(Propose(null, graph: Preset("release"), workflow: "", draft: "g1-ffffffffffffffff"), DriverConfig.Empty, Told());

        Assert.Equal("the draft `g1-ffffffffffffffff` was never shown to this conversation: ask again from the editor, whose draft goes beside your words.", plan.Refusal);
    }

    /// <summary>§8.4: a draft mixed with what saves or chooses is refused before anything else is judged.</summary>
    [Theory]
    [InlineData(1, false, null, null)]
    [InlineData(null, true, null, null)]
    [InlineData(null, false, "default", null)]
    [InlineData(null, false, null, "Release")]
    public void A_draft_mixed_with_a_base_a_choice_a_route_or_a_name_is_refused(int? @base, bool choose, string? route, string? name)
    {
        var plan = HelpProposals.Plan(Propose(null, @base, choose, graph: Preset("release"), route: route, name: name, workflow: "", draft: Fingerprint), DriverConfig.Empty, Told());

        Assert.StartsWith("a change to the open draft ", plan.Refusal, StringComparison.Ordinal);
    }

    /// <summary>WORKFLOWCLEAN1 1b: a development pipeline's graph is proposed by the `edit` door, and taken by the `draft` door, which saves nothing.</summary>
    [Fact]
    public void A_development_graph_is_proposed_by_edit_and_taken_by_draft()
    {
        WorkflowStore.Add(_home, new WorkflowVersionAdded(Id, "Merge it", "terminal", "2026-10-30T09:00:00Z", null, JsonNode.Parse(Preset("merge-after-accept"))), kept: []);

        var edit = Card(HelpProposals.Plan(Propose(null, 1, graph: Preset("release")), DriverConfig.Empty, Here()));
        Assert.Equal((true, false, 2), (edit.Saves, edit.Verifies, edit.Version));

        var draft = Card(HelpProposals.Plan(Propose(null, graph: Preset("release"), draft: Fingerprint), DriverConfig.Empty, Told(Id, 1)));
        Assert.Equal((Id, (int?)1, false), (draft.Id, draft.Base, draft.Saves));
    }

    /// <summary>
    /// Its Apply writes no workflow and no choice: pressed from the card it opens the place's Workflow and stands; pressed with every change
    /// taken or left it settles, recording which, and the workflow store and the configuration are as they were, byte for byte.
    /// </summary>
    [Fact]
    public async Task Apply_leaves_the_workflow_store_and_the_configuration_as_they_were()
    {
        WorkflowStore.Add(_home, new WorkflowVersionAdded(Id, "Merge it", "terminal", "2026-10-30T09:00:00Z", null, JsonNode.Parse(Preset("merge-after-accept"))), kept: []);
        var files = WorkflowFiles();
        var facts = Told(Id, 1);
        var proposal = Propose(null, graph: Preset("release"), draft: Fingerprint);
        var plan = HelpProposals.Plan(proposal, Chosen, facts);
        var card = Card(plan);
        var doors = new HelpStandInDoors();

        var opened = await HelpProposals.ApplyAsync(_home, proposal, plan, doors, _ => { }, CancellationToken.None);
        Assert.True(opened.Stands);
        Assert.Equal("projects", opened.Go!.View);
        Assert.Equal("proposed", HelpProposals.Find(_home, "p6")!.State);

        var ids = card.Changes.Select(change => change.Id).ToList();
        var some = proposal with { Taken = ids[..2] };
        var short1 = await HelpProposals.ApplyAsync(_home, some, plan, doors, _ => { }, CancellationToken.None);
        Assert.False(short1.Applied);
        Assert.Equal("proposed", HelpProposals.Find(_home, "p6")!.State);

        var settled = await HelpProposals.ApplyAsync(_home, proposal with { Taken = ids[..2], Left = ids[2..] }, plan, doors, _ => { }, CancellationToken.None);
        Assert.True(settled.Applied);
        Assert.False(settled.Stands);
        var file = JsonNode.Parse(System.IO.File.ReadAllText(Path.Combine(HelpProposals.FolderOf(_home), "p6.json")))!;
        Assert.Equal("applied", file["state"]!.GetValue<string>());
        Assert.Equal(ids[..2], file["taken"]!.AsArray().Select(each => each!.GetValue<string>()));
        Assert.Equal(ids[2..], file["left"]!.AsArray().Select(each => each!.GetValue<string>()));

        Assert.Empty(doors.Calls);
        Assert.Same(DriverConfig.Empty, doors.Config);
        Assert.Equal(files, WorkflowFiles());
    }

    private void SavedGraph(string name = "Documentation to a pull request") =>
        WorkflowStore.Add(_home, new WorkflowVersionAdded(Id, name, "terminal", "2026-10-09T09:00:00Z", null, JsonNode.Parse(Preset("merge-after-accept"))), kept: []);

    /// <summary>A version of steps as an older build saved it: no door saves one now, and a stored one still reads.</summary>
    private void Saved(string steps, string name = "Documentation to a pull request") =>
        StoredSteps.Add(Daoris.Driver.WorkflowPorts.Store(_home), Id, name, "2026-10-09T09:00:00Z", JsonNode.Parse(steps)!);

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

    /// <summary>A verification pipeline's graph: one check over the named gates, its failure as given (the pipeline design §4.1).</summary>
    private static string Verify(string gates = "\"heavy\"", string failed = "wait") =>
        $$$"""{"schemaVersion":2,"entries":["gates"],"end":"gates","nodes":[{"id":"gates","kind":"check","version":1,"reach":"performed","options":{"gates":{{{gates}}},"failed":"{{{failed}}}"}}],"wires":[]}""";

    private void SavedVerification(string graph, string id = Id) =>
        WorkflowStore.Add(_home, new WorkflowVersionAdded(id, "Verify the line", "terminal", "2026-10-21T09:00:00Z", null, JsonNode.Parse(graph))
        {
            Pipeline = WorkflowPipelines.Verification,
        }, kept: []);

    /// <summary>
    /// FABRIC-PIPELINE1i (the pipeline design §1.3): a route with no version chooses a saved workflow, saving nothing, and only the person's
    /// press applies it.
    /// </summary>
    [Fact]
    public async Task A_route_proposed_with_no_version_applies_at_the_person_s_press()
    {
        Saved(Merge);
        var proposal = Propose(null, route: "default");
        Assert.Equal(("default", false), (proposal.Route, proposal.Steps.HasValue));
        var plan = HelpProposals.Plan(proposal, DriverConfig.Empty, Here());
        var card = Card(plan);

        Assert.Equal((false, 1, "default", true), (card.Saves, card.Version, card.Route, card.Choose));
        Assert.StartsWith(
            "Choose `docs-to-pr` (Documentation to a pull request) for `engine`; no version is saved, and it follows its newest, v1.",
            plan.Describe, StringComparison.Ordinal);
        Assert.Equal("daoris workflow use docs-to-pr --repository engine", plan.Terminal);
        Assert.Equal("New work only, in `engine`.", card.AppliesTo);

        var doors = new HelpStandInDoors();
        var applied = await HelpProposals.ApplyAsync(_home, proposal, plan, doors, _ => { }, CancellationToken.None);

        Assert.True(applied.Applied);
        Assert.Equal($"Applied: `#p6` (`{plan.Terminal}`) — `engine` chooses it now.", applied.Told);
        Assert.Equal(Id, doors.Config.Workflows["engine"].Default);
        Assert.Single(WorkflowStore.Load(_home, Id)!.Read.Versions);
    }

    /// <summary>A development graph that integrates its landing, asking as <paramref name="request"/>, saved as the workflow `integrated`.</summary>
    private void SavedIntegrating(string request = "owner", string landing = "{}") =>
        StoredSteps.AddGraph(Daoris.Driver.WorkflowPorts.Store(_home), "integrated", "Integrated into the line", "2026-10-30T09:00:00Z", JsonNode.Parse(
            $$$"""
            {"schemaVersion":2,"entries":["claim"],"end":"merge-in",
             "nodes":[{"id":"claim","kind":"claimed","version":1,"reach":"observed"},
                      {"id":"land","kind":"handed","version":1,"reach":"observed","options":{{{landing}}}},
                      {"id":"merge-in","kind":"integrate","version":1,"reach":"waited","options":{"request":"{{{request}}}"}}],
             "wires":[{"id":"a","kind":"after","from":"claim","to":"land"},{"id":"b","kind":"after","from":"land","to":"merge-in"}]}
            """)!);

    private static DriverConfig Landings(string engine, string game) =>
        DriverConfig.Parse("{\"landings\":{\"engine\":" + engine + ",\"game\":" + game + "}}");

    private const string BranchRule = """{"form":"branch","pattern":"w/{quest}"}""";

    private const string GatedRule = """{"form":"branch","pattern":"w/{quest}","autoAccept":true,"integrate":{"gates":"declared"}}""";

    /// <summary>
    /// INTEGRATE1r part r2 (the integration design §17.7, F42): the card that chooses a graph that integrates its landing says, per repository it
    /// reaches, <i>integrates with no gates</i> where the rule's <c>integrate.gates</c> is absent or <c>none</c>, and only for those.
    /// </summary>
    [Fact]
    public void A_choice_of_a_graph_that_integrates_says_each_repository_it_reaches_that_integrates_with_no_gates()
    {
        SavedIntegrating();
        var plan = HelpProposals.Plan(Propose(null, scope: "work", route: "default", workflow: "integrated"), Landings(BranchRule, GatedRule), Here());
        var card = Card(plan);

        Assert.Empty(card.Needs);
        Assert.Contains("`engine` integrates with no gates", plan.Describe);
        Assert.DoesNotContain("`game` integrates with no gates", plan.Describe);

        // INTEGRATE1r part r3: the card carries the repositories by name, for the page to word the line in its own language; a card with
        // none (a saved version, a repository whose rule declares gates) carries none.
        Assert.Equal(["engine"], card.NoGates);
        Assert.Empty(Card(HelpProposals.Plan(Propose(null, scope: "work", route: "default", workflow: "integrated"), Landings(GatedRule, GatedRule), Here())).NoGates);
    }

    /// <summary>INTEGRATE1r part r2: a repository whose rule is a merge rule cannot carry the integrate; the card is a draft naming it and the Setup row.</summary>
    [Fact]
    public void A_choice_reaching_a_repository_that_cannot_carry_the_integrate_is_a_draft_naming_it_and_the_setup_row()
    {
        SavedIntegrating();
        var plan = HelpProposals.Plan(Propose(null, scope: "work", route: "default", workflow: "integrated"), Landings("""{"form":"merge"}""", BranchRule), Here());
        var card = Card(plan);

        var need = Assert.Single(card.Needs);
        Assert.StartsWith("`engine`: its `merge-in` (`integrate`) integrates the branch the landing `land` makes", need);
        Assert.Contains("Setup → Line and landing → How work lands", need);
    }

    /// <summary>INTEGRATE1r part r2: <c>done</c> needs the declaration's gates read, and the facts the box gathered answer for it; none gathered holds the card.</summary>
    [Fact]
    public void A_choice_that_asks_with_done_needs_the_accepted_declaration_read_and_naming_a_gate()
    {
        SavedIntegrating("done", """{"accept":"automatic"}""");
        var proposal = Propose(null, scope: "engine", route: "default", workflow: "integrated");
        var config = Landings(GatedRule, BranchRule);

        var unread = Card(HelpProposals.Plan(proposal, config, Here()));
        Assert.Contains("could not be read", Assert.Single(unread.Needs));

        var none = Card(HelpProposals.Plan(proposal, config, Here() with { AcceptedGates = new Dictionary<string, AcceptedGates> { ["engine"] = new([]) } }));
        Assert.Contains("no declaration the owner accepted names a gate", Assert.Single(none.Needs));

        var named = Card(HelpProposals.Plan(proposal, config, Here() with { AcceptedGates = new Dictionary<string, AcceptedGates> { ["engine"] = new(["unit"]) } }));
        Assert.Empty(named.Needs);
    }

    /// <summary>INTEGRATE1r part r2: the box reads an accepted declaration only where a saved graph asks with done or steward, and never passes one it cannot read.</summary>
    [Fact]
    public async Task The_boxes_read_of_accepted_gates_is_made_only_for_a_graph_that_needs_it()
    {
        (string Repository, string? Workspace, string? Root)[] registered = [("engine", "work", null)];
        SavedIntegrating();
        Assert.Empty(await IntegrateChoice.ReadForCardsAsync(Chosen, WorkflowStore.List(_home), registered, CancellationToken.None));

        SavedIntegrating("done", """{"accept":"automatic"}""");
        var read = await IntegrateChoice.ReadForCardsAsync(Chosen, WorkflowStore.List(_home), registered, CancellationToken.None);
        Assert.Contains("has no checkout here", read["engine"].Unread);
    }

    /// <summary>FABRIC-PIPELINE1i: a kind's route, as the terminal's <c>use … --kind</c> chooses it; once chosen, a second says so.</summary>
    [Fact]
    public void A_kind_s_route_with_no_version_chooses_it_for_that_kind()
    {
        Saved(Merge);
        var config = WorkflowSelection.Apply(DriverConfig.Empty, new WorkflowDeclare("docs", "work", "Documentation", []));
        var plan = HelpProposals.Plan(Propose(null, scope: "work", route: "kind:docs"), config, Here());

        Assert.StartsWith(
            "Choose `docs-to-pr` (Documentation to a pull request) for the workspace `work`'s kind `docs`;", plan.Describe, StringComparison.Ordinal);
        Assert.Equal("daoris workflow use docs-to-pr --workspace work --kind docs", plan.Terminal);
        Assert.Equal(Id, WorkflowSelection.Apply(config, Card(plan).Use!).WorkspaceWorkflows["work"].KindOf("docs")!.Workflow);

        var chosen = WorkflowSelection.Apply(config, Card(plan).Use!);
        var again = HelpProposals.Plan(Propose(null, scope: "work", route: "kind:docs"), chosen, Here());
        Assert.StartsWith(
            "The workspace `work` chooses `docs-to-pr` for its kind `docs` already: applying changes nothing.", again.Describe, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, "kind:Docs", "`kind:Docs` is not a route: `default`, `landed` for the work that lands, or `kind:<id>` for one of the scope's kinds, such as `kind:docs`.")]
    [InlineData(null, "default", "there is no workflow `docs-to-pr` on this machine: a `route` with no version chooses a workflow saved here.")]
    [InlineData(null, null, "a workflow proposal carries its whole `graph`; or, with none, the `route` it chooses a saved workflow for.")]
    [InlineData(1, "default", "a `route` with no version saves nothing, so it names no `base` and no `name`.")]
    public void A_route_with_no_version_is_refused_where_it_cannot_choose(int? @base, string? route, string refusal)
    {
        Assert.Equal(refusal, HelpProposals.Plan(Propose(null, @base: @base, route: route), DriverConfig.Empty, Here()).Refusal);
    }

    /// <summary>
    /// FABRIC-PIPELINE1i (the pipeline design §2.1): a graph proposed is a verification pipeline's, judged by the mark as a save is, and
    /// made so at its first save; chosen for the landed route, its gates wait for the person's own <i>Accept the gates</i> (D156).
    /// </summary>
    [Fact]
    public async Task A_verification_graph_proposed_is_judged_by_the_mark_and_saved_as_one()
    {
        var landing = """{"schemaVersion":2,"entries":["gates"],"end":"land","nodes":[{"id":"gates","kind":"check","version":1,"reach":"performed"},{"id":"land","kind":"land","version":1,"reach":"performed","options":{"method":"merge"}}],"wires":[{"id":"w1","kind":"after","from":"gates","to":"land"}]}""";
        SavedVerification(Verify(), "line-verify");
        Assert.StartsWith(
            "v2: its graph holds `land`, a `land`, and a verification pipeline's graph lands nothing",
            HelpProposals.Plan(Propose(null, graph: landing, @base: 1, workflow: "line-verify"), DriverConfig.Empty, Here()).Refusal, StringComparison.Ordinal);

        var proposal = Propose(null, graph: Verify(), route: "landed");
        var plan = HelpProposals.Plan(proposal, DriverConfig.Empty, Here());
        var card = Card(plan);

        Assert.Equal((true, true, "landed", 1), (card.Saves, card.Verifies, card.Route, card.Version));
        Assert.Equal(["+ gates · check"], card.Diff);
        Assert.Equal(["gates: you ask for a fix when it fails (*Ask for a fix…*)."], card.YourPart);
        Assert.Empty(card.Outward);
        Assert.Empty(card.Lowers);
        Assert.StartsWith(
            "Save a new verification pipeline `docs-to-pr` as v1, and choose it for the work that lands in `engine`.", plan.Describe, StringComparison.Ordinal);
        Assert.Contains("Its gates wait for your *Accept the gates* on `engine`'s Workflow tab", plan.Describe, StringComparison.Ordinal);
        Assert.Equal("daoris workflow import <file> && daoris workflow use docs-to-pr --repository engine --landed", plan.Terminal);
        Assert.Equal("New work only, in `engine`.", card.AppliesTo);

        var doors = new HelpStandInDoors();
        var applied = await HelpProposals.ApplyAsync(_home, proposal, plan, doors, _ => { }, CancellationToken.None);

        Assert.True(applied.Applied, applied.Told);
        Assert.Equal($"Applied: `#p6` (`{plan.Terminal}`) — `docs-to-pr` v1 is saved, and `engine` chooses it for the work that lands now.", applied.Told);
        var saved = WorkflowStore.Load(_home, Id)!.Read;
        Assert.Equal((true, "ask-daoris:p6"), (saved.Verifies, saved.Versions[0].Door));
        Assert.NotNull(saved.Versions[0].Graph);
        Assert.Equal(new WorkflowLanded(Id, null), doors.Config.Workflows["engine"].Landed);
    }

    /// <summary>FABRIC-PIPELINE1i: each pipeline takes its own versions, and each route its own pipeline, refused in the twins' words.</summary>
    [Fact]
    public void A_version_or_a_route_of_the_other_pipeline_is_refused()
    {
        Saved(Merge);
        Assert.StartsWith(
            "v2: its graph holds no landing",
            HelpProposals.Plan(Propose(null, graph: Verify(), @base: 1), DriverConfig.Empty, Here()).Refusal, StringComparison.Ordinal);
        Assert.Equal(WorkflowSelection.NotVerification(Id), HelpProposals.Plan(Propose(null, route: "landed"), DriverConfig.Empty, Here()).Refusal);

        SavedVerification(Verify(), "line-verify");
        Assert.StartsWith(
            "v2: its graph holds",
            HelpProposals.Plan(Propose(null, graph: Preset("release"), @base: 1, workflow: "line-verify"), DriverConfig.Empty, Here()).Refusal, StringComparison.Ordinal);
        Assert.Equal(
            WorkflowSelection.VerificationElsewhere("line-verify"),
            HelpProposals.Plan(Propose(null, route: "default", workflow: "line-verify"), DriverConfig.Empty, Here()).Refusal);
    }

    /// <summary>
    /// D157 point 10, the pipeline design §1.3: an agent's change that asks less of the person is kept as its proposal, said, and applies
    /// only at the person's press: a check's failure moved off <c>wait</c>, and a landed route pointed at a pipeline running fewer gates.
    /// </summary>
    [Fact]
    public async Task An_agent_s_lowering_is_kept_as_a_proposal_for_the_person_s_press()
    {
        SavedVerification(Verify());
        var proposal = Propose(null, graph: Verify(failed: "ask"), @base: 1);
        var plan = HelpProposals.Plan(proposal, DriverConfig.Empty, Here());
        var card = Card(plan);

        Assert.Equal(["it moves the check `gates`'s failure off your press, to `ask`"], card.Lowers);
        Assert.Equal(["~ gates · check"], card.Diff);
        Assert.Empty(card.YourPart);
        Assert.Equal(["`gates` asks for a fix of each failed gate with no press of yours."], card.Outward);
        Assert.Contains(
            "\nIt asks less of you, so only your press applies it:\n  it moves the check `gates`'s failure off your press, to `ask`\n",
            plan.Describe, StringComparison.Ordinal);
        Assert.Single(WorkflowStore.Load(_home, Id)!.Read.Versions);

        var applied = await HelpProposals.ApplyAsync(_home, proposal, plan, new HelpStandInDoors(), _ => { }, CancellationToken.None);
        Assert.True(applied.Applied, applied.Told);
        Assert.Equal(2, WorkflowStore.Load(_home, Id)!.Read.Versions.Count);

        SavedVerification(Verify(gates: """["unit","e2e"]"""), "full-verify");
        var full = WorkflowSelection.Apply(DriverConfig.Empty, new WorkflowUse("full-verify", "engine", null, null)
        {
            Landed = true, Pipeline = WorkflowPipelines.Verification,
        });
        var narrower = HelpProposals.Plan(Propose(null, graph: Verify(gates: """["unit"]"""), route: "landed", workflow: "quick-verify"), full, Here());

        Assert.Equal(["it verifies the work that lands with fewer of its gates: `e2e`"], Card(narrower).Lowers);
    }

    /// <summary>FLOWCHART2e: `draft` beside the three, which are unchanged.</summary>
    [Fact]
    public void Its_doors_are_new_edit_use_and_draft()
    {
        Assert.Equal(["new", "edit", "use", "draft"], HelpProposalKinds.Find("workflow")!.Doors);
    }

    [Fact]
    public void The_file_reads_its_door_from_its_base()
    {
        var fresh = Propose(Merge, choose: true);
        Assert.Equal(("new", "engine", Id, (int?)null, true), (fresh.Door, fresh.Scope, fresh.Workflow, fresh.Base, fresh.Choose));
        Assert.Equal(2, fresh.Steps!.Value.GetArrayLength());

        Assert.Equal(("edit", (int?)3), (Propose(Merge, @base: 3).Door, Propose(Merge, @base: 3).Base));
    }

    /// <summary>§8.2: a new workflow's card is its first version, every step added, and what applies it; a graph that lands is a development pipeline.</summary>
    [Fact]
    public void A_new_workflow_s_card_is_its_first_version()
    {
        var plan = HelpProposals.Plan(Propose(null, graph: Preset("merge-after-accept")), DriverConfig.Empty, Here());
        var card = Card(plan);

        Assert.Equal((Id, Id, (int?)null, 1, "engine", false, false), (card.Id, card.Name, card.Base, card.Version, card.Scope, card.Choose, card.Verifies));
        Assert.True(card.Saves);
        Assert.NotEmpty(card.Diff);
        Assert.Empty(card.Needs);
        Assert.Equal("New work only, once a repository or a workspace chooses `docs-to-pr`: nothing does now.", card.AppliesTo);
        Assert.Equal("daoris workflow import <file>", plan.Terminal);
        Assert.StartsWith("Save a new workflow `docs-to-pr` as v1.", plan.Describe, StringComparison.Ordinal);
    }

    /// <summary>WORKFLOWCLEAN1 1b: a list of steps is refused by this door as by every other, naming the editor and the graph file.</summary>
    [Fact]
    public void A_list_of_steps_is_refused_naming_the_editor_and_the_graph_file()
    {
        Assert.Equal(WorkflowNamed.StepsRefused(Id), HelpProposals.Plan(Propose(Merge), DriverConfig.Empty, Here()).Refusal);
    }

    /// <summary>WORKFLOW1h-name: a new workflow is named by the person's words, on the card and in the saved version; absent, by its id.</summary>
    [Fact]
    public async Task A_new_workflow_is_saved_under_the_name_given_else_its_id()
    {
        var named = Propose(null, graph: Preset("merge-after-accept"), name: "Docs to a pull request");
        Assert.Equal("Docs to a pull request", named.Name);
        var plan = HelpProposals.Plan(named, DriverConfig.Empty, Here());
        Assert.Equal("Docs to a pull request", Card(plan).Name);

        var applied = await HelpProposals.ApplyAsync(_home, named, plan, new HelpStandInDoors(), _ => { }, CancellationToken.None);
        Assert.True(applied.Applied);
        Assert.Equal("Docs to a pull request", WorkflowStore.Load(_home, Id)!.Read.Name);

        System.IO.File.Delete(WorkflowStore.PathOf(_home, Id));
        Assert.Equal(Id, Card(HelpProposals.Plan(Propose(null, graph: Preset("merge-after-accept")), DriverConfig.Empty, Here())).Name);
    }

    /// <summary>§8.2: an edit's diff by step id, the person's part first, then the steps, then what leaves with no press.</summary>
    [Fact]
    public void An_edit_s_card_names_its_base_the_version_it_makes_and_the_runs_that_keep_theirs()
    {
        WorkflowStore.Add(_home, new WorkflowVersionAdded(Id, "Documentation to a pull request", "terminal", "2026-10-30T09:00:00Z", null, JsonNode.Parse(Preset("merge-after-accept"))), kept: []);
        var facts = Here() with
        {
            WorkflowRuns = [new WorkflowRunBinding("q1", "engine", "work", Id, WorkflowLevels.Repository, DateTimeOffset.UnixEpoch) { Version = 1 }],
        };

        var plan = HelpProposals.Plan(Propose(null, graph: Preset("release"), @base: 1), Chosen, facts);
        var card = Card(plan);

        Assert.Equal(("Documentation to a pull request", (int?)1, 2), (card.Name, card.Base, card.Version));
        Assert.Equal("New work only, in `engine`; 1 run keeps the version it started on.", card.AppliesTo);
        Assert.Equal("daoris workflow apply docs-to-pr <file> --base 1", plan.Terminal);
        Assert.StartsWith("Save `docs-to-pr` v1 → v2 (Documentation to a pull request).", plan.Describe, StringComparison.Ordinal);
    }

    /// <summary>§8.1, D157 §2.6: a base that is not the newest is stale, refused with the newest, never applied over it.</summary>
    [Fact]
    public void A_stale_base_is_refused_with_the_newest()
    {
        Saved(Merge);
        Saved(LookThenBranch);

        Assert.Equal(
            "`docs-to-pr` is at v2 now, not v1: read it again (`daoris workflow show docs-to-pr`) and make the change against v2.",
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

    /// <summary>WORKFLOWCLEAN1 1b: a graph the editor would not save is refused in its words, naming the node (a `handed` is no longer saved).</summary>
    [Fact]
    public void A_graph_the_editor_would_not_save_is_refused_in_its_words()
    {
        var handed = """{"schemaVersion":2,"entries":["claim"],"end":"land","nodes":[{"id":"claim","kind":"claimed","version":1,"reach":"observed"},{"id":"land","kind":"handed","version":1,"reach":"observed"}],"wires":[{"id":"a","kind":"after","from":"claim","to":"land"}]}""";
        var plan = HelpProposals.Plan(Propose(null, graph: handed), DriverConfig.Empty, Here());

        Assert.StartsWith("v1: its graph does not hold: node `land` is a `handed`, which only watches a landing some door made", plan.Refusal, StringComparison.Ordinal);
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
    public async Task A_card_that_needs_something_first_is_a_draft_with_no_apply()
    {
        var proposal = Propose(null, graph: Preset("merge-after-accept"), choose: true);
        var plan = HelpProposals.Plan(proposal, DriverConfig.Empty, Here());
        var card = Card(plan) with { Needs = ["its step needs something declared first."] };

        var doors = new HelpStandInDoors();
        await Assert.ThrowsAsync<DriverException>(() => HelpProposals.ApplyAsync(_home, proposal, plan with { Workflow = card }, doors, _ => { }, CancellationToken.None));
        Assert.Empty(doors.Calls);
        Assert.Null(WorkflowStore.Load(_home, Id));
        Assert.Equal("proposed", HelpProposals.Find(_home, "p6")!.State);
    }

    /// <summary>§8.4: Apply saves the version through the `ask-daoris:` door, then makes the choice through the driver's file.</summary>
    [Fact]
    public async Task Apply_saves_the_version_through_ask_daoris_and_makes_the_choice()
    {
        var proposal = Propose(null, graph: Preset("merge-after-accept"), choose: true);
        var plan = HelpProposals.Plan(proposal, DriverConfig.Empty, Here());
        Assert.Equal("daoris workflow import <file> && daoris workflow use docs-to-pr --repository engine", plan.Terminal);
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
        SavedGraph();
        var proposal = Propose(null, graph: Preset("release"), @base: 1);
        var plan = HelpProposals.Plan(proposal, Chosen, Here());
        Assert.Empty(Card(plan).Needs);
        SavedGraph();

        var doors = new HelpStandInDoors();
        var applied = await HelpProposals.ApplyAsync(_home, proposal, plan, doors, _ => { }, CancellationToken.None);

        Assert.False(applied.Applied);
        Assert.Equal(
            "Not applied: `#p6` — `docs-to-pr` is at v2 now, not v1: read it again (`daoris workflow show docs-to-pr`) and make "
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
        SavedGraph();
        var proposal = Propose(null, graph: Preset("merge-after-accept"), @base: 1);
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
