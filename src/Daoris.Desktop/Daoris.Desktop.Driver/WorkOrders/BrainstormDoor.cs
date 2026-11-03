using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The terminal's way to the brainstorm desk (ONEWAY1t): the running service's <c>/api/brainstorms</c> doors, which are the one
/// writer of a request, a send and a stop. With no service running the verb says so and writes nothing; it never writes the files
/// itself, and what it prints of a press is the desk's own sentence.
/// </summary>
internal static class BrainstormDoor
{
    /// <summary>The path of a press on one brainstorm or meeting: <c>send</c>, <c>stop</c> or none for the clear.</summary>
    public static string PathOf(string id, string? press, bool meeting, int? round = null)
    {
        var query = new List<string>();
        if (meeting) query.Add("kind=meeting");
        if (round is { } number) query.Add($"round={number}");
        return $"/api/brainstorms/{Uri.EscapeDataString(id)}{(press is null ? "" : $"/{press}")}{(query.Count == 0 ? "" : $"?{string.Join('&', query)}")}";
    }

    /// <summary>The body of an ask: the words as typed, which the desk trims and judges.</summary>
    public static string AskBody(
        string question, string? repository, IReadOnlyList<string> agents, IReadOnlyList<string> sources, string? id, bool meeting)
    {
        var body = new JsonObject { ["question"] = question };
        if (meeting) body["kind"] = "meeting";
        if (repository is not null) body["repository"] = repository;
        body["agents"] = new JsonArray([.. agents.Select(agent => (JsonNode?)agent)]);
        if (sources.Count > 0) body["sources"] = new JsonArray([.. sources.Select(source => (JsonNode?)source)]);
        if (id is not null) body["id"] = id;
        return body.ToJsonString();
    }

    /// <summary>
    /// One press through the service: its sentence printed, 0 where the desk did it, 1 where it refused or no service answers.
    /// <paramref name="doors"/> null finds the service as a terminal does (the variable, else the running desktop's address).
    /// </summary>
    public static int Press(IServiceDoors? doors, ServiceVerb verb, string path, string? json, TextWriter output)
    {
        var own = false;
        try
        {
            if (doors is null)
            {
                doors = ServiceHttp.FromEnvironment();
                own = true;
            }

            var reply = doors.SendAsync(verb, path, json, CancellationToken.None).GetAwaiter().GetResult();
            output.WriteLine(reply.Ok ? Said(reply.Payload, "message")
                : string.IsNullOrWhiteSpace(reply.Payload)
                    ? $"daoris-driver: the service at {doors.BaseUrl} answered {reply.Status} with no sentence; is it older than this driver?"
                    : $"daoris-driver: {Said(reply.Payload, "error")}");
            return reply.Ok ? 0 : 1;
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or TaskCanceledException)
        {
            output.WriteLine($"daoris-driver: Daoris is not running, or did not answer: {error.Message} "
                + "Start Daoris (its desktop, or the local service) and ask again; nothing was written.");
            return 1;
        }
        finally
        {
            if (own) doors?.Dispose();
        }
    }

    private static string Said(string payload, string name)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty(name, out var text)
                && text.ValueKind == JsonValueKind.String)
            {
                return text.GetString()!;
            }
        }
        catch (JsonException)
        {
        }

        return payload.Length > 200 ? payload[..200] : payload;
    }
}
