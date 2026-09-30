using System.Net;
using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Where the driver reads lanes (D115 §2.2, DEV4): the registry answers each repository's lanes as
/// their words, and the intake's room lists them so an intake can address one; a quest answers the
/// lanes it addresses beside its repository. The driver never reads a lane's paths through the
/// service: those are the line's file's, and its reader is DEV6's.
/// </summary>
public sealed class LaneReadingTests
{
    private static readonly IReadOnlyList<DeclarationView> Circle =
    [
        new("engine", Adopted: true, Registered: true, "The engine.", ["the runtime"], ["a runtime bug"], Root: null)
        {
            Lanes =
            [
                new LaneView("core", "Core", "Simulation and rendering.", Steward: false),
                new LaneView("assets", "Assets", "The asset pipeline.", Steward: false),
                new LaneView("records", "Records", "", Steward: true),
            ],
        },
        new("game", Adopted: true, Registered: true, "The game.", ["gameplay"], ["a gameplay bug"], Root: null),
    ];

    [Fact]
    public void The_room_lists_each_repositorys_lanes_by_the_address_that_asks_them()
    {
        var room = IntakeRoom.Render("work", Circle);

        Assert.Contains("`engine:core` — Core: Simulation and rendering.", room);
        Assert.Contains("`engine:assets` — Assets: The asset pipeline.", room);
        Assert.Contains("`engine:records` — Records (the steward's: it keeps the records)", room);
        // A repository that declares none is asked whole, and the room says nothing of lanes for it.
        var game = room[room.IndexOf("### `game`", StringComparison.Ordinal)..];
        Assert.DoesNotContain("lanes", game);
    }

    [Fact]
    public void The_intakes_instruction_says_a_lane_is_addressed_as_repository_colon_lane()
    {
        var prompt = IntakePrompt.Compose(new AskView("a1b2c3", "work", "cap the frame's work", "Proposed", "declarations"));

        Assert.Contains("`repository:lane`", prompt);
    }

    /// <summary>The registry's answer and a quest's, as the service spells them, read onto the driver's views.</summary>
    [Fact]
    public async Task The_registry_answers_lanes_onto_declarations_and_a_quest_answers_its_lanes()
    {
        using var client = new ServiceClient("http://service.example", null, new HttpClient(new Canned(new()
        {
            ["/api/registry"] = """
                [{ "repository": "engine", "adopted": true, "registered": true, "summary": "The engine.",
                   "owns": [], "accepts": [], "root": null,
                   "lanes": [{ "id": "core", "title": "Core", "summary": "The runtime.", "steward": false },
                             { "id": "records", "title": "Records", "summary": "", "steward": true }] },
                 { "repository": "game", "adopted": true, "registered": true, "owns": [], "accepts": [] }]
                """,
            ["/api/quests"] = """
                [{ "id": "a1", "from": "game", "to": "engine", "title": "t", "body": "b", "status": "Open", "lanes": ["core"] },
                 { "id": "b2", "from": "game", "to": "engine", "title": "t", "body": "b", "status": "Open" }]
                """,
        })));

        var declarations = await client.DeclarationsAsync("work");
        var engine = declarations.Single(d => d.Repository == "engine");
        Assert.Equal(["core", "records"], engine.Lanes.Select(lane => lane.Id));
        Assert.True(engine.Lanes[1].Steward);
        Assert.Empty(declarations.Single(d => d.Repository == "game").Lanes);

        var quests = await client.EveryQuestAsync();
        Assert.Equal(["core"], quests.Single(q => q.Id == "a1").Lanes);
        Assert.Empty(quests.Single(q => q.Id == "b2").Lanes);
    }

    /// <summary>A quest reads as the address that asked it, as the service spells one.</summary>
    [Fact]
    public void A_quest_reads_as_its_repository_and_its_lanes()
    {
        var quest = new QuestView("a1", "game", "engine", "t", "b", "Open");

        Assert.Equal("engine", quest.Address);
        Assert.Equal("engine:assets+core", (quest with { Lanes = ["assets", "core"] }).Address);
    }

    /// <summary>Answers each path with what it was given, whatever the query — the doors' own JSON.</summary>
    private sealed class Canned(Dictionary<string, string> answers) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(answers.TryGetValue(request.RequestUri!.AbsolutePath, out var body)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") }
                : new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("""{ "error": "no" }""") });
    }
}
