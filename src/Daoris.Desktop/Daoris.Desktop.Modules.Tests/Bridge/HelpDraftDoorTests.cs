using System.Text.Json;
using Daoris.Desktop.Driver.Tests;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A change to the draft open in the person's editor, over the bridge. The card is judged
/// against the draft the conversation's record says it has been told, and carries the changes, what a save would refuse and the candidate graph;
/// its Apply opens the place's Workflow and stands, and the editor's press naming each change taken or left settles it. Neither writes a
/// workflow or a choice: the workflow store and the driver's file are as they were, byte for byte.
/// </summary>
public sealed class HelpDraftDoorTests : DriverModuleBridge
{
    private const string Config = """{ "adapter": "claude-code-acp", "drivable": ["engine"] }""";

    private const string Fingerprint = "g1-0123456789abcdef";

    private readonly Registry _service = new();

    [Fact]
    public void A_draft_proposal_is_judged_against_the_drafts_its_conversation_was_told()
    {
        var draft = new HelpProposal("p1", "workflow", "draft", null, null, null, null, "why", "h1", "proposed") { Draft = Fingerprint };

        Assert.True(HelpProposalFacts.JudgedAgainstDrafts([draft]));
        Assert.False(HelpProposalFacts.JudgedAgainstDrafts([draft with { Door = "edit" }, draft with { Kind = "go" }]));
    }

    [Fact]
    public async Task A_draft_card_carries_its_changes_and_its_candidate_and_its_apply_writes_no_workflow_and_no_choice()
    {
        File.WriteAllText(DriverConfigPath, Config);
        var module = await UpAsync();
        Told(WorkflowDefaults.Of("merge-after-accept")!.GraphJson);
        Proposal(WorkflowDefaults.Of("release")!.GraphJson);
        var config = File.ReadAllBytes(DriverConfigPath);
        var workflows = Directory.Exists(DaorisHome.Entry(Home, HomeEntry.Workflows));

        var drawn = await AnswerAsync(module, "HELP_PROPOSALS", new { session = "h1" });

        var card = Assert.Single(drawn.GetProperty("proposals").EnumerateArray());
        var workflow = card.GetProperty("workflow");
        Assert.Equal(Fingerprint, workflow.GetProperty("draft").GetString());
        Assert.False(workflow.GetProperty("saves").GetBoolean());
        var changes = workflow.GetProperty("changes").EnumerateArray().ToList();
        Assert.Contains(changes, change => change.GetProperty("id").GetString() == "step:+find" && change.GetProperty("what").GetString() == "step");
        Assert.Equal(["step:+release", "step:+publish"], changes.Single(change => change.GetProperty("id").GetString() == "way:+k")
            .GetProperty("needs").EnumerateArray().Select(each => each.GetString()));
        Assert.Equal(JsonValueKind.Array, workflow.GetProperty("problems").ValueKind);
        Assert.Equal("find", workflow.GetProperty("proposed").GetProperty("nodes")[8].GetProperty("id").GetString());
        var revision = card.GetProperty("revision").GetString();

        var opened = await AnswerAsync(module, "HELP_APPLY", new { id = "p1a2b3c4", revision });
        Assert.True(opened.GetProperty("stands").GetBoolean());
        Assert.Equal("projects", opened.GetProperty("go").GetProperty("view").GetString());
        Assert.Equal("proposed", HelpProposals.Find(Home, "p1a2b3c4")!.State);

        var ids = changes.Select(change => change.GetProperty("id").GetString()!).ToArray();
        var settled = await AnswerAsync(module, "HELP_APPLY", new { id = "p1a2b3c4", revision, taken = ids[..3], left = ids[3..] });
        Assert.True(settled.GetProperty("applied").GetBoolean());
        Assert.False(settled.GetProperty("stands").GetBoolean());
        Assert.Equal("applied", HelpProposals.Find(Home, "p1a2b3c4")!.State);

        Assert.Equal(config, File.ReadAllBytes(DriverConfigPath));
        Assert.Equal(workflows, Directory.Exists(DaorisHome.Entry(Home, HomeEntry.Workflows)));
        Assert.Empty(WorkflowStore.List(Home));
        lock (_service.Asked) Assert.All(_service.Asked, asked => Assert.StartsWith("GET ", asked));
    }

    /// <summary>The conversation's record holding the draft as told, as its message's turn records it.</summary>
    private void Told(string graph)
    {
        using var draft = JsonDocument.Parse($$"""{"fingerprint":"{{Fingerprint}}","place":"engine","graph":{{graph}}}""");
        new SessionEvents(DaorisHome.Entry(Home, HomeEntry.Sessions)).Append("h1", WorkflowDraftTold.Read(draft.RootElement).ToldNote());
    }

    private void Proposal(string graph)
    {
        var folder = HelpProposals.FolderOf(Home);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "p1a2b3c4.json"), $$"""
            {"id":"p1a2b3c4","kind":"workflow","scope":"engine","workflow":"","draft":"{{Fingerprint}}","base":null,"choose":false,"why":"add a release",
             "state":"proposed","by":{"session":"h1"},"graph":{{graph}}}
            """);
    }

    private async Task<DriverModule> UpAsync()
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(new SessionChanges(_service))));
        lock (_service.Asked) _service.Asked.Clear();
        return module;
    }

    /// <summary>The registry: aurora's engine, its checkout elsewhere.</summary>
    private sealed class Registry : HttpMessageHandler
    {
        public List<string> Asked { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            lock (Asked) Asked.Add($"{request.Method} {path}");
            var body = path == "/api/registry" ? """[{"repository":"engine","workspace":"aurora","adopted":true,"root":"elsewhere-engine"}]""" : "[]";
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }
}
