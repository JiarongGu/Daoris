using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The service standing in for a test that reads quests and posts what it read of them (EVID1b3): each request answered by the
/// scenario's own routes, a bare 404 where they answer none, and a quest written as the service's list carries it. One writer
/// of that wire, so <see cref="EvidenceAtEndTests"/> and <see cref="QuestCheckCommandTests"/> cannot carry a quest two ways,
/// as their own writers had: one wrote every evidence item as a path, a gate as a path that is none.
/// </summary>
internal sealed class QuestStandIn(Func<HttpRequestMessage, (HttpStatusCode Status, string Body)?> answer) : HttpMessageHandler
{
    /// <summary>A client over <paramref name="answer"/>, as the driver is handed one.</summary>
    public static ServiceClient Client(Func<HttpRequestMessage, (HttpStatusCode Status, string Body)?> answer) =>
        new("http://stand-in", null, new HttpClient(new QuestStandIn(answer)));

    /// <summary>The quest list as the service answers <c>GET /api/quests</c>.</summary>
    public static string List(IEnumerable<QuestView> quests) => new JsonArray([.. quests.Select(Json)]).ToJsonString();

    /// <summary>
    /// One quest as the service's list carries it: its words and status, the hold and whether it waits on evidence, each
    /// requirement with its evidence (a path or a gate, <c>[]</c> where none, as EVID1a writes it), each answer, and what was
    /// last read of its evidence where something was.
    /// </summary>
    public static JsonNode Json(QuestView quest)
    {
        var json = new JsonObject
        {
            ["id"] = quest.Id, ["from"] = quest.From, ["to"] = quest.To, ["title"] = quest.Title, ["body"] = quest.Body, ["status"] = quest.Status,
            ["held"] = quest.Held, ["hold"] = quest.Hold, ["awaitsEvidence"] = quest.AwaitsEvidence,
            ["requirements"] = new JsonArray([.. quest.Requirements.Select(requirement => (JsonNode)new JsonObject
            {
                ["quote"] = requirement.Quote, ["check"] = requirement.Check,
                ["evidence"] = new JsonArray([.. requirement.Evidence.Select(Item)]),
            })]),
            ["answers"] = new JsonArray([.. quest.Answers.Select(Answer)]),
        };
        if (quest.Evidence is { } read) json["evidence"] = JsonNode.Parse(read.Json());
        return json;
    }

    /// <summary>A request's body, as the door reads it.</summary>
    public static JsonObject Body(HttpRequestMessage request) => JsonNode.Parse(request.Content!.ReadAsStringAsync().Result)!.AsObject();

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var (status, body) = answer(request) ?? (HttpStatusCode.NotFound, "");
        return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }

    private static JsonNode Item(QuestEvidenceItem item) =>
        item.Path is { } path ? new JsonObject { ["path"] = path } : new JsonObject { ["gate"] = item.Gate };

    private static JsonNode Answer(QuestAnswerView answer)
    {
        var json = new JsonObject { ["requirement"] = answer.Requirement };
        if (answer.Met is { } met) json["met"] = met;
        if (answer.Departed is { } departed) json["departed"] = departed;
        if (answer.Quote is { } quote) json["quote"] = quote;
        return json;
    }
}
