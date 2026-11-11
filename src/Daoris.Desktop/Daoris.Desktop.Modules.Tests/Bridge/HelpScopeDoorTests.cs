using System.Text.Json.Nodes;
using Daoris.Desktop.Driver.Tests;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A proposal is never refused for the workspace it names, the modules' half: a conversation in <c>alpha</c>'s room proposing
/// for <c>beta</c> is drawn as one proposing for <c>alpha</c> is, and <c>default</c>'s conversation proposes for the whole
/// machine. The service is stood in by a handler, and nothing starts a process: the fast half.
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
    public async Task A_conversation_s_proposal_for_another_workspace_is_drawn_as_its_own_is()
    {
        var module = await UpAsync();

        Proposal("p1a2b3c4", "h1alpha0", "beta");
        Proposal("p2b3c4d5", "h1alpha0", "alpha");
        var cards = (await AnswerAsync(module, "HELP_PROPOSALS", new { session = "h1alpha0" })).GetProperty("proposals").EnumerateArray();

        Assert.Equal(["p1a2b3c4", "p2b3c4d5"], cards.Select(card => card.GetProperty("id").GetString()).Order(StringComparer.Ordinal));
        Assert.Equal("proposed", (string?)Settled("p1a2b3c4")["state"]);
    }

    /// <summary><c>default</c>'s room is the whole machine: its conversation's proposals are judged as before.</summary>
    [Fact]
    public async Task The_whole_machine_s_conversation_proposes_for_any_workspace()
    {
        var module = await UpAsync();

        Proposal("p3c4d5e6", "h2whole0", "beta");
        var card = Assert.Single((await AnswerAsync(module, "HELP_PROPOSALS", new { session = "h2whole0" })).GetProperty("proposals").EnumerateArray());
        Assert.Equal("p3c4d5e6", card.GetProperty("id").GetString());
    }

    private void Proposal(string id, string session, string? workspace)
    {
        var folder = HelpProposals.FolderOf(Home);
        Directory.CreateDirectory(folder);
        var node = new JsonObject
        {
            ["id"] = id, ["proposed"] = "2026-10-19T10:00:00.0000000+00:00", ["by"] = new JsonObject { ["session"] = session },
            ["kind"] = "ask", ["door"] = "ask", ["target"] = null, ["workspace"] = workspace, ["value"] = null,
            ["sentence"] = "fix the stall", ["why"] = "the person asked", ["state"] = "proposed", ["note"] = null,
        };
        File.WriteAllText(Path.Combine(folder, $"{id}.json"), node.ToJsonString());
    }

    private JsonObject Settled(string id) => JsonNode.Parse(File.ReadAllText(Path.Combine(HelpProposals.FolderOf(Home), $"{id}.json")))!.AsObject();

    /// <summary>
    /// The machine the judge reads: <c>engine</c> in <c>alpha</c> and <c>game</c> in <c>beta</c>, two Ask Daoris records, one
    /// started in <c>alpha</c>'s room and one in <c>default</c>'s.
    /// </summary>
    private async Task<DriverModule> UpAsync()
    {
        static JsonObject Help(string id, string workspace, string tree) => new()
        {
            ["id"] = id, ["repository"] = HelpRoom.Repository, ["door"] = "claude-code", ["state"] = "completed", ["kind"] = "chat",
            ["workspace"] = workspace, ["tree"] = tree, ["created"] = "2026-10-19T09:00:00Z", ["updated"] = "2026-10-19T09:01:00Z",
        };
        var sessions = new JsonArray(Help("h1alpha0", "alpha", HelpRoom.PathOf(Home, "alpha")), Help("h2whole0", "default", HelpRoom.PathOf(Home)));
        var registry = new JsonArray(
            new JsonObject { ["repository"] = "engine", ["adopted"] = true, ["registered"] = true, ["workspace"] = "alpha" },
            new JsonObject { ["repository"] = "game", ["adopted"] = true, ["registered"] = true, ["workspace"] = "beta" });
        var answers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [SessionRecords.Door] = sessions.ToJsonString(),
            ["/api/registry"] = registry.ToJsonString(),
        };

        var loop = Loop();
        var module = new DriverModule(Bus, loop) { Offers = Path.Combine(Home, "no-offers") };
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(new SessionChanges(new Host(answers)))));
        return module;
    }
}
