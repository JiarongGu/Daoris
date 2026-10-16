using System.Text;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// WORKFLOW1h-c: a workflow proposal's modules half (D157, design §8.1-§8.4). The judge is given the named workflows, the bound
/// runs and the registered repositories only when one is pending; the card's data reaches the page as the page's fields, never
/// the steps or the choice Apply makes; and Apply saves the version under the home with the <c>ask-daoris:</c> door. The service
/// is stood in over a loopback socket and nothing starts a process: the fast half (MOD8).
/// </summary>
public sealed class HelpWorkflowDoorTests : DriverModuleBridge
{
    private const string Merge = """[{"id":"work","kind":"work"},{"id":"land","kind":"landing","form":"merge","accept":"you"}]""";

    [Theory]
    [InlineData("workflow", "new", true)]
    [InlineData("workflow", "edit", true)]
    [InlineData("pause", "pause", false)]
    [InlineData("clear", "quest", false)]
    [InlineData("go", "quests", false)]
    public void The_workflows_are_read_for_a_workflow_proposal_only(string kind, string door, bool read)
    {
        var proposal = new HelpProposal("p1", kind, door, "q1", null, null, null, "why", "h1", "proposed");

        Assert.Equal(read, HelpRoutes.JudgedAgainstWorkflows([proposal]));
        Assert.False(HelpRoutes.JudgedAgainstWorkflows([]));
    }

    [Fact]
    public void The_runs_bound_are_the_files_of_the_bindings_folder_and_a_file_that_is_not_one_is_skipped()
    {
        Directory.CreateDirectory(WorkflowRunBindings.FolderOf(Home));
        var binding = WorkflowRunBindings.Plan(DriverConfig.Empty, Home, "q1", "engine", "aurora", null, null, [], DateTimeOffset.UtcNow);
        Assert.True(WorkflowRunBindings.Bind(Home, binding));
        File.WriteAllText(Path.Combine(WorkflowRunBindings.FolderOf(Home), "q2.json"), "{broken");

        Assert.Equal(["q1"], HelpRoutes.BoundRuns(Home).Select(run => run.Run));
        Assert.Empty(HelpRoutes.BoundRuns(Path.Combine(Home, "nowhere")));
    }

    [Fact]
    public async Task The_card_reaches_the_page_as_its_fields_and_never_the_steps_or_the_choice()
    {
        using var ledger = Ledger();
        var module = await UpAsync(ledger);
        Propose(Merge, choose: true);

        var drawn = await AnswerAsync(module, "HELP_PROPOSALS", new { session = "h1" });

        var card = drawn.GetProperty("proposals")[0].GetProperty("workflow");
        Assert.Equal("docs-to-pr", card.GetProperty("id").GetString());
        Assert.Equal(1, card.GetProperty("version").GetInt32());
        Assert.Equal("engine", card.GetProperty("scope").GetString());
        Assert.True(card.GetProperty("choose").GetBoolean());
        foreach (var field in new[] { "name", "diff", "yourPart", "outward", "needs", "appliesTo" }) Assert.True(card.TryGetProperty(field, out _), field);
        Assert.True(card.GetProperty("diff").GetArrayLength() > 0);
        Assert.False(card.TryGetProperty("steps", out _));
        Assert.False(card.TryGetProperty("use", out _));
        Assert.False(card.TryGetProperty("saves", out _));
    }

    [Fact]
    public async Task A_proposal_naming_no_repository_this_machine_has_is_refused_unshown()
    {
        using var ledger = Ledger();
        var module = await UpAsync(ledger);
        Propose(Merge, scope: "nowhere");

        var drawn = await AnswerAsync(module, "HELP_PROPOSALS", new { session = "h1" });

        Assert.Equal(0, drawn.GetProperty("proposals").GetArrayLength());
    }

    [Fact]
    public async Task Apply_saves_the_version_in_the_home_with_the_ask_daoris_door()
    {
        using var ledger = Ledger();
        var module = await UpAsync(ledger);
        Propose(Merge);

        var applied = await ApplyShownAsync(module, "h1", "p1a2b3c4");

        Assert.True(applied.GetProperty("applied").GetBoolean());
        var version = Assert.Single(WorkflowStore.Load(Home, "docs-to-pr")!.Read.Versions);
        Assert.Equal("ask-daoris:p1a2b3c4", version.Door);
    }

    private void Propose(string steps, bool choose = false, string scope = "engine")
    {
        var folder = HelpProposals.FolderOf(Home);
        Directory.CreateDirectory(folder);
        var node = new JsonObject
        {
            ["id"] = "p1a2b3c4", ["proposed"] = "2026-10-11T10:00:00.0000000+00:00", ["by"] = new JsonObject { ["session"] = "h1" },
            ["kind"] = "workflow", ["scope"] = scope, ["workflow"] = "docs-to-pr", ["base"] = null, ["choose"] = choose,
            ["steps"] = JsonNode.Parse(steps), ["why"] = "the person asked", ["state"] = "proposed", ["note"] = null,
        };
        File.WriteAllText(Path.Combine(folder, "p1a2b3c4.json"), node.ToJsonString());
    }

    private async Task<DriverModule> UpAsync(LoopbackHost ledger)
    {
        var loop = Loop();
        await loop.ComeUpAsync(new ServiceClient(ledger.Address, null));
        return new DriverModule(Bus, loop);
    }

    /// <summary>One registered repository, <c>engine</c>, in the workspace <c>aurora</c>.</summary>
    private LoopbackHost Ledger()
    {
        var ledger = new LoopbackHost();
        ledger.Serve("/api/registry", Encoding.UTF8.GetBytes(
            $$"""[{"repository":"engine","adopted":true,"registered":true,"root":"{{Home.Replace('\\', '/')}}/engine","workspace":"aurora"}]"""));
        foreach (var path in new[] { "/api/quests?includeClosed=true", "/api/quests", "/api/sessions?includeClosed=true", "/api/sessions" })
        {
            ledger.Serve(path, Encoding.UTF8.GetBytes("[]"));
        }

        return ledger;
    }
}
