using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// ASKSCOPE1c (D158's ASKSCOPE1 note point 5): a proposal is judged against the workspace its conversation's record keeps, the
/// modules' half. A conversation in <c>alpha</c>'s room proposing for <c>beta</c> is never drawn: it is settled refused, saying
/// so; one proposing for <c>alpha</c> is drawn; <c>default</c>'s conversation proposes for the whole machine. The service is stood
/// in by a handler, and nothing starts a process: the fast half (MOD8).
/// </summary>
public sealed class HelpScopeDoorTests : DriverModuleBridge
{
    private sealed class Host(IReadOnlyDictionary<string, string> answers) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(answers.GetValueOrDefault(request.RequestUri!.PathAndQuery, "[]"), System.Text.Encoding.UTF8, "application/json"),
            });
    }

    [Fact]
    public async Task A_conversation_s_proposal_for_another_workspace_is_refused_and_its_own_is_drawn()
    {
        var module = await UpAsync();

        Proposal("p1a2b3c4", "h1alpha0", "beta");
        Assert.Empty((await AnswerAsync(module, "HELP_PROPOSALS", new { session = "h1alpha0" })).GetProperty("proposals").EnumerateArray());
        var settled = Settled("p1a2b3c4");
        Assert.Equal("refused", (string?)settled["state"]);
        Assert.StartsWith("this conversation is workspace `alpha`'s", (string?)settled["note"]);

        Proposal("p2b3c4d5", "h1alpha0", "alpha");
        var card = Assert.Single((await AnswerAsync(module, "HELP_PROPOSALS", new { session = "h1alpha0" })).GetProperty("proposals").EnumerateArray());
        Assert.Equal("p2b3c4d5", card.GetProperty("id").GetString());
    }

    /// <summary><c>default</c>'s room is the whole machine (ASKSCOPE1b): its conversation's proposals are judged as before.</summary>
    [Fact]
    public async Task The_whole_machine_s_conversation_proposes_for_any_workspace()
    {
        var module = await UpAsync();

        Proposal("p3c4d5e6", "h2whole0", "beta");
        var card = Assert.Single((await AnswerAsync(module, "HELP_PROPOSALS", new { session = "h2whole0" })).GetProperty("proposals").EnumerateArray());
        Assert.Equal("p3c4d5e6", card.GetProperty("id").GetString());
    }

    /// <summary>
    /// ASKSCOPE1c2: a delete naming a quest by its id is judged against the workspace the service files the quest under, read with
    /// the records a delete is judged against: <c>beta</c>'s is settled refused, naming it; <c>alpha</c>'s is drawn.
    /// </summary>
    [Fact]
    public async Task A_conversation_s_delete_of_another_workspace_s_quest_is_refused_and_of_its_own_is_drawn()
    {
        var module = await UpAsync();

        Proposal("p4d5e6f7", "h1alpha0", null, kind: "delete", door: "quest", target: "qb1beta0");
        Assert.Empty((await AnswerAsync(module, "HELP_PROPOSALS", new { session = "h1alpha0" })).GetProperty("proposals").EnumerateArray());
        var settled = Settled("p4d5e6f7");
        Assert.Equal("refused", (string?)settled["state"]);
        Assert.StartsWith("this conversation is workspace `alpha`'s, and this proposal names quest `#qb1beta0`, of workspace `beta`", (string?)settled["note"]);

        Proposal("p5e6f7a8", "h1alpha0", null, kind: "delete", door: "quest", target: "qa1alpha");
        var card = Assert.Single((await AnswerAsync(module, "HELP_PROPOSALS", new { session = "h1alpha0" })).GetProperty("proposals").EnumerateArray());
        Assert.Equal("p5e6f7a8", card.GetProperty("id").GetString());
    }

    private void Proposal(string id, string session, string? workspace, string kind = "ask", string door = "ask", string? target = null)
    {
        var folder = HelpProposals.FolderOf(Home);
        Directory.CreateDirectory(folder);
        var node = new JsonObject
        {
            ["id"] = id, ["proposed"] = "2026-10-19T10:00:00.0000000+00:00", ["by"] = new JsonObject { ["session"] = session },
            ["kind"] = kind, ["door"] = door, ["target"] = target, ["workspace"] = workspace, ["value"] = null,
            ["sentence"] = "fix the stall", ["why"] = "the person asked", ["state"] = "proposed", ["note"] = null,
        };
        File.WriteAllText(Path.Combine(folder, $"{id}.json"), node.ToJsonString());
    }

    private JsonObject Settled(string id) => JsonNode.Parse(File.ReadAllText(Path.Combine(HelpProposals.FolderOf(Home), $"{id}.json")))!.AsObject();

    /// <summary>
    /// The machine the judge reads: <c>engine</c> in <c>alpha</c> and <c>game</c> in <c>beta</c>, two Ask Daoris records, one
    /// started in <c>alpha</c>'s room and one in <c>default</c>'s, and an open quest of each workspace that may be deleted.
    /// </summary>
    private async Task<DriverModule> UpAsync()
    {
        static JsonObject Help(string id, string workspace, string tree) => new()
        {
            ["id"] = id, ["repository"] = HelpRoom.Repository, ["adapter"] = "claude-code", ["state"] = "completed", ["kind"] = "chat",
            ["workspace"] = workspace, ["tree"] = tree, ["created"] = "2026-10-19T09:00:00Z", ["updated"] = "2026-10-19T09:01:00Z",
        };
        var sessions = new JsonArray(Help("h1alpha0", "alpha", HelpRoom.PathOf(Home, "alpha")), Help("h2whole0", "default", HelpRoom.PathOf(Home)));
        var registry = new JsonArray(
            new JsonObject { ["repository"] = "engine", ["adopted"] = true, ["registered"] = true, ["workspace"] = "alpha" },
            new JsonObject { ["repository"] = "game", ["adopted"] = true, ["registered"] = true, ["workspace"] = "beta" });
        static JsonObject Quest(string id, string to, string workspace) => new()
        {
            ["id"] = id, ["from"] = "person", ["to"] = to, ["title"] = "a quest", ["body"] = "", ["status"] = "Open",
            ["workspace"] = workspace, ["deletable"] = true,
        };
        var answers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [SessionRecords.Door] = sessions.ToJsonString(),
            ["/api/registry"] = registry.ToJsonString(),
            ["/api/quests?includeClosed=true"] = new JsonArray(Quest("qa1alpha", "engine", "alpha"), Quest("qb1beta0", "game", "beta")).ToJsonString(),
        };

        var loop = Loop();
        var module = new DriverModule(Bus, loop) { Offers = Path.Combine(Home, "no-offers") };
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(new Host(answers))));
        return module;
    }
}
