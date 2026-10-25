using System.Globalization;
using System.Text.Json;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>The service's session records, closed ones included (SESSUX1a): asked of its door, and read.</summary>
public static class SessionRecords
{
    /// <summary>The door the records are read by: every record, closed ones included, as the page reads them.</summary>
    public const string Door = "/api/sessions?includeClosed=true";

    /// <summary>Every record the answer holds, in its order; one with no id is skipped.</summary>
    public static IReadOnlyList<SessionRecord> Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var records = new List<SessionRecord>();
        if (document.RootElement.ValueKind != JsonValueKind.Array) return records;
        foreach (var session in document.RootElement.EnumerateArray())
        {
            if (session.ValueKind != JsonValueKind.Object || Text(session, "id") is not { Length: > 0 } id) continue;
            records.Add(new SessionRecord(id, Text(session, "repository") ?? "", Text(session, "state") ?? "")
            {
                Quest = Text(session, "quest") is { Length: > 0 } quest ? quest : null,
                Kind = Text(session, "kind") ?? "driven",
                Tree = Text(session, "tree") is { Length: > 0 } tree ? tree : null,
                Ask = Text(session, "ask"),
                Created = Time(session, "created"),
                Updated = Time(session, "updated"),
                // The service keeps a blank answer as its own words, so an empty one is none.
                Answer = Text(session, "answer") is { Length: > 0 } answer ? answer : null,
                // A host older than the field says nothing, and nothing is no delete (SESSUX1f).
                Deletable = session.TryGetProperty("deletable", out var deletable) && deletable.ValueKind == JsonValueKind.True,
                BaseCommit = Text(session, "baseCommit") is { Length: > 0 } baseCommit ? baseCommit : null,
                // Absent is no take of its own: a host older than STANDDOWN2, or a teammate's record (PAUSE1d).
                Took = session.TryGetProperty("took", out var took) && took.ValueKind == JsonValueKind.True,
                Workspace = RemoteTarget.Workspace(Text(session, "workspace")),
                // Its commits and what was read of its done's evidence (D46 §4, EVID1b): the done's commit a check reads from.
                Evidence = Text(session, "evidence") is { Length: > 0 } evidence ? evidence : null,
                // SESSONE1b: the key's group, and what a part continues; absent from a host before them.
                Entry = Text(session, "entry") is { Length: > 0 } entry ? entry : null,
                From = Text(session, "from") is { Length: > 0 } from ? from : null,
                FromWhy = Text(session, "fromWhy") is { Length: > 0 } why ? why : null,
                // HELPMODE1a2: an Ask Daoris record's mode, answered on loopback alone; absent from a host before it.
                HelpMode = Text(session, "helpMode") is { Length: > 0 } mode ? mode : null,
                Actor = Text(session, "actor"), Choice = Text(session, "choice"),
                Asked = ServiceClient.ActorRead<ActorAsked>(session, "asked"),
                Override = ServiceClient.ActorRead<JsonElement?>(session, "override"),
                Ran = ServiceClient.ActorRead<ActorRan>(session, "ran"),
                // COAGENT1b: a helper's or a partner's lead, and a partner's piece, kept only beside it, as the service keeps them.
                Serves = Text(session, "serves") is { Length: > 0 } serves ? serves : null,
                Piece = Text(session, "serves") is { Length: > 0 } && Text(session, "piece") is { Length: > 0 } piece ? piece : null,
                // HELPERWAIT1: a completed record's note read for its code alone, and only a completed one's.
                AwaitsHelper = Text(session, "state") == "completed" && Carries(session, RecordNotes.EndedAwaitsHelper),
                // LIMITRESUME1d (D206 §3.1): an account's limit made its failure; absent from a host before the flag.
                Limit = session.TryGetProperty("limit", out var limit) && limit.ValueKind == JsonValueKind.True,
            });
        }

        return records;
    }

    /// <summary>Whether a record's note carries a line of <paramref name="code"/>, read from its parts without composing them.</summary>
    private static bool Carries(JsonElement session, string code) =>
        session.TryGetProperty("noteParts", out var parts) && parts.ValueKind == JsonValueKind.Array
        && parts.EnumerateArray().Any(part => part.ValueKind == JsonValueKind.Object && Text(part, "code") == code);

    /// <summary>
    /// The records as the service answers them, over a client of this reader's own. A refusal is the driver's sentence
    /// (<see cref="DriverException"/>), as every read through <see cref="DriverHttp"/> says it.
    /// </summary>
    /// <remarks>
    /// <see cref="ServiceClient"/> reads these records for the strikes and each quest's last run and keeps them to itself;
    /// its file is another branch's while this one is built (TOOL4d), so the reader asks the same door with the same key
    /// rather than growing that class. Moving it onto the client later changes no answer.
    /// </remarks>
    /// <param name="handler">The test seam, as <see cref="DriverHttp.Client"/>'s; production passes none.</param>
    public static async Task<string> ReadAsync(
        string baseUrl, string? key, HttpMessageHandler? handler = null, CancellationToken ct = default)
    {
        using var http = DriverHttp.Client(key, handler);
        return await DriverHttp.GetAsync(http, baseUrl.TrimEnd('/') + Door, ct).ConfigureAwait(false);
    }

    private static DateTimeOffset Time(JsonElement element, string name) =>
        Moments.Read(element, name) ?? DateTimeOffset.MinValue;
}
