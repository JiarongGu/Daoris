using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A stand-in service that answers the sessions' change door (<c>GET /api/sessions/changes</c>) in the contract's shape, from what
/// it answers the whole list: every record changed, in the list's order, as a first read is. A stand-in derives from it and
/// answers its other doors in <see cref="Respond"/>, so none writes the change's shape itself.
/// </summary>
internal abstract class SessionChangesHandler : HttpMessageHandler
{
    private const string ChangesPath = "/api/sessions/changes";
    private const string ListDoor = "/api/sessions?includeClosed=true";

    protected sealed override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (request.Method != HttpMethod.Get || request.RequestUri!.AbsolutePath != ChangesPath) return Respond(request, ct);
        using var list = new HttpRequestMessage(HttpMethod.Get, new Uri(request.RequestUri, ListDoor));
        var listed = Respond(list, ct).GetAwaiter().GetResult();
        var text = listed.Content.ReadAsStringAsync(ct).GetAwaiter().GetResult();
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(Whole(text), Encoding.UTF8, "application/json"),
        });
    }

    /// <summary>The stand-in's answer to one request, the whole session list included; never the change door.</summary>
    protected abstract Task<HttpResponseMessage> Respond(HttpRequestMessage request, CancellationToken ct);

    /// <summary>The change that carries every record of <paramref name="sessions"/>, a JSON array, as a first read.</summary>
    internal static string Whole(string sessions)
    {
        var records = JsonNode.Parse(sessions)!.AsArray();
        var order = new JsonArray();
        foreach (var record in records) order.Add(record!["id"]!.GetValue<string>());
        return new JsonObject { ["mark"] = "m1", ["order"] = order, ["changed"] = records.DeepClone(), ["whole"] = true }.ToJsonString();
    }
}
