using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>The service's session records, closed ones included (SESSUX1a): asked of its door, and read.</summary>
public static class SessionRecords
{
    /// <summary>The list's door for every record, closed ones included, as the page reads them.</summary>
    public const string Door = "/api/sessions?includeClosed=true";

    /// <summary>Every record the answer holds, in its order; one with no id is skipped.</summary>
    public static IReadOnlyList<SessionRecord> Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var records = new List<SessionRecord>();
        if (document.RootElement.ValueKind != JsonValueKind.Array) return records;
        foreach (var session in document.RootElement.EnumerateArray())
        {
            if (Read(session) is { } record) records.Add(record);
        }

        return records;
    }

    /// <summary>One record as the service answers it alone (<see cref="SessionListDoors.OneJsonAsync"/>), read as the list's are; null with no id.</summary>
    public static SessionRecord? One(string json)
    {
        using var document = JsonDocument.Parse(json);
        return Read(document.RootElement);
    }

    private static SessionRecord? Read(JsonElement session)
    {
        if (session.ValueKind != JsonValueKind.Object || Text(session, "id") is not { Length: > 0 } id) return null;
        return new SessionRecord(id, Text(session, "repository") ?? "", Text(session, "state") ?? "")
            {
                Quest = Text(session, "quest") is { Length: > 0 } quest ? quest : null,
                TookQuest = Text(session, "tookQuest") is { Length: > 0 } tookQuest ? tookQuest : null,
                Kind = Text(session, "kind") ?? "driven",
                Adapter = Text(session, "door") is { Length: > 0 } adapter ? adapter : null,
                Tree = Text(session, "tree") is { Length: > 0 } tree ? tree : null,
                Ask = Text(session, "ask"),
                Created = Time(session, "created"),
                Updated = Time(session, "updated"),
                Said = SessionReads.ReadSaid(session),
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
                Head = Text(session, "head") is { Length: > 0 } head ? head : null,
                // HELPMODE1a2: an Ask Daoris record's mode, answered on loopback alone; absent from a host before it.
                HelpMode = Text(session, "helpMode") is { Length: > 0 } mode ? mode : null,
                Actor = Text(session, "actor"), Choice = Text(session, "choice"),
                Asked = SessionReads.ActorRead<ActorAsked>(session, "asked"),
                Override = SessionReads.ActorRead<JsonElement?>(session, "override"),
                Ran = SessionReads.ActorRead<ActorRan>(session, "ran"),
                Chosen = SessionReads.ActorRead<ActorChosen>(session, "chosen"),
                // COAGENT1b: a helper's or a partner's lead, and a partner's piece, kept only beside it, as the service keeps them.
                Serves = Text(session, "serves") is { Length: > 0 } serves ? serves : null,
                Piece = Text(session, "serves") is { Length: > 0 } && Text(session, "piece") is { Length: > 0 } piece ? piece : null,
                // COMMS1i: a reviewer's opinion, which At work lists by the line it serves.
                Opinion = Text(session, "opinion") is { Length: > 0 } opinion ? opinion : null,
                // HELPERWAIT1: a completed record's note read for its code alone, and only a completed one's.
                AwaitsHelper = Text(session, "state") == "completed" && Carries(session, RecordNotes.EndedAwaitsHelper),
                // LIMITRESUME1d (D206 §3.1): an account's limit made its failure; absent from a host before the flag.
                Limit = session.TryGetProperty("limit", out var limit) && limit.ValueKind == JsonValueKind.True,
            };
    }

    /// <summary>
    /// The person's words waiting on a record as the service answers it, joined by a blank line, or null where none wait (ONEWAY1m): read
    /// from its keyed <c>said</c> words by the one sender rule, never from a joined field.
    /// </summary>
    public static string? PersonsSaid(JsonObject record) =>
        record["said"] is JsonArray said
            ? SaidWordView.PersonsJoined(said.OfType<JsonNode>().Select(word => SessionReads.ReadWord(JsonSerializer.SerializeToElement(word))).OfType<SaidWordView>())
            : null;

    /// <summary>Whether a record's note carries a line of <paramref name="code"/>, read from its parts without composing them.</summary>
    private static bool Carries(JsonElement session, string code) =>
        session.TryGetProperty("noteParts", out var parts) && parts.ValueKind == JsonValueKind.Array
        && parts.EnumerateArray().Any(part => part.ValueKind == JsonValueKind.Object && Text(part, "code") == code);

    private static DateTimeOffset Time(JsonElement element, string name) =>
        Moments.Read(element, name) ?? DateTimeOffset.MinValue;
}
