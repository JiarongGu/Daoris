using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;
using Shenora.Core.Events;

namespace Daoris.Desktop;

/// <summary>
/// The quests as every window holds them, a pushed slice (PUSH1d3, D182): every quest the service answers, closed ones and every
/// workspace's included, allow-listed to the fields the page reads, so a tick reads <c>/api/quests</c> again in no window, and a quest a
/// connector wrote reaches the windows with no desktop dispatch.
/// </summary>
/// <remarks>
/// <para><b>The sessions' shape</b> (<see cref="SessionsStateSlice"/>, D182's PUSH1d2 note): the page holds the widest list and reads
/// each view's from it; a partition per workspace, keyed by quest, so a quest's move pushes that quest alone.</para>
///
/// <para><b>A file's path is never pushed.</b> The service answers a kept file's path to a caller on this machine, and the page reads
/// only whether there is one (<i>can be opened</i>), so each file carries <c>kept</c> in its place.</para>
///
/// <para><b>Its wakes.</b> A change a press declared to the quests and the service coming up, on the kit's bus; and the change stream's
/// <c>quests</c> family, and its <c>sessions</c>, since whether an open quest may be deleted reads whether a session named it
/// (<c>QuestExchange.DeletableAsync</c>), heard where the watch tells the loop its moves (<see cref="DriverLoop.HeardMove"/>).</para>
///
/// <para><b>Desktop-only.</b> A set-up's address and the folder it served are this machine's (D47, D182 point 2).</para>
/// </remarks>
public static class QuestsStateSlice
{
    /// <summary>The slice's name, in the page's words.</summary>
    public const string Name = "quests";

    /// <summary>
    /// What each quest carries: the fields of the page's <c>Quest</c> type, as the service names them. A field the page comes to read is
    /// pushed once it is named here, which <c>QuestsStateSliceTests</c> holds against the page's type.
    /// </summary>
    public static readonly IReadOnlyList<string> Fields =
    [
        "id", "from", "to", "title", "body", "status", "note", "filed", "updated", "workspace", "noteParts", "links", "attachments", "then",
        "parent", "conflicts", "awaits", "publishedBy", "deletable", "lanes", "requirements", "answers", "held", "accepted", "short", "hold",
        "review", "setUpIn", "setUps", "verdicts", "closure", "submission", "grewFrom", "sourceReopened", "settled", "holder", "here",
        "reopened",
    ];

    /// <summary>What the service answers of a quest that no view reads from the list: what was read of its evidence.</summary>
    public static readonly IReadOnlyList<string> PulledOnly = ["awaitsEvidence", "evidence"];

    /// <summary>What each file a quest or an ask carries is pushed with, as the service answers it.</summary>
    public static readonly IReadOnlyList<string> FileFields = ["name", "sha256", "bytes"];

    /// <summary>What a file carries in its path's place: whether this machine keeps its bytes, which is all the page reads of the path.</summary>
    public const string Kept = "kept";

    /// <summary>What the service answers of a file that is never pushed: where this machine keeps it.</summary>
    public static readonly IReadOnlyList<string> FilePulledOnly = ["path"];

    /// <summary>The change stream's families that move a quest as the service answers it.</summary>
    public static readonly IReadOnlyList<string> Families = ["quests", "sessions"];

    /// <summary>What wakes it on the kit's bus: a change declared to the quests and the service coming up.</summary>
    public static readonly IReadOnlyList<SliceWake> WakesOn =
    [
        new(StatePublisher.Type, payload => payload is StateChanged changed && changed.Scopes.Contains("quests")),
        new("DRIVER_READY"),
    ];

    /// <summary>The slice the shell's loop owns, reading <paramref name="loop"/>'s service once it answers.</summary>
    public static StateSlice Of(DriverLoop loop, IEventBus bus) =>
        new(Name, bus, async ct => loop.Service is { } service ? Judge(await service.QuestsJsonAsync(ct).ConfigureAwait(false)) : null)
        {
            Keyed = true,
            WakesOn = WakesOn,
            Hears = [wake => HearLedger(loop, Families, wake)],
            // The service away or refusing: the windows keep what was judged, and the next wake reads it again.
            Failed = error => loop.Log?.Warn("state.slice", ("slice", Name), ("message", error.Message)),
        };

    /// <summary>One judgement of what the service answered: a partition per workspace, each quest under its id with the page's fields.</summary>
    public static IReadOnlyList<SliceJudged> Judge(string answered) => Partitioned(answered, Fields, "quests");

    /// <summary>
    /// A partition per workspace, named by it, each record under its id with <paramref name="fields"/>, a file's path replaced by whether
    /// it is kept here. A record with no id is no record; one with no workspace is the default's, as the service names it.
    /// </summary>
    internal static IReadOnlyList<SliceJudged> Partitioned(string answered, IReadOnlyList<string> fields, string what)
    {
        var records = JsonNode.Parse(answered) as JsonArray ?? throw new JsonException($"the service answered the {what} as no list");
        var parts = new SortedDictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var node in records)
        {
            if (node is not JsonObject record || Text(record, "id") is not { } id) continue;
            var workspace = Text(record, "workspace") ?? "default";
            var picked = new JsonObject();
            foreach (var field in fields)
            {
                if (record.TryGetPropertyValue(field, out var value)) picked[field] = field == "attachments" ? Files(value) : value?.DeepClone();
            }

            if (!parts.TryGetValue(workspace, out var part)) parts[workspace] = part = new JsonObject();
            part[id] = picked;
        }

        return [.. parts.Select(each => new SliceJudged(each.Key, each.Value))];
    }

    /// <summary>Each file with its allow-listed fields and <see cref="Kept"/>: true where the service answered a path, which it does only where the bytes are here.</summary>
    private static JsonNode? Files(JsonNode? answered)
    {
        if (answered is not JsonArray files) return answered?.DeepClone();
        var pushed = new JsonArray();
        foreach (var node in files)
        {
            if (node is not JsonObject file) continue;
            var picked = new JsonObject();
            foreach (var field in FileFields)
            {
                if (file.TryGetPropertyValue(field, out var value)) picked[field] = value?.DeepClone();
            }

            picked[Kept] = Text(file, "path") is not null;
            pushed.Add(picked);
        }

        return pushed;
    }

    /// <summary>
    /// Wake the slice (<paramref name="wake"/>) where the loop hears the change stream name one of <paramref name="families"/>: a record
    /// written by any door, a connector's among them. Another family, or another topic, wakes nothing. Disposed, it hears no more.
    /// </summary>
    public static IDisposable HearLedger(DriverLoop loop, IReadOnlyList<string> families, Action wake)
    {
        void Heard(string topic, string subject)
        {
            if (topic == ChangeTopics.Ledger
                && subject.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Any(family => families.Contains(family, StringComparer.Ordinal)))
            {
                wake();
            }
        }

        loop.MoveHeard += Heard;
        return new Hearing(() => loop.MoveHeard -= Heard);
    }

    private static string? Text(JsonObject record, string field) =>
        record[field] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) ? text : null;

    private sealed class Hearing(Action stop) : IDisposable
    {
        private int _stopped;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _stopped, 1) == 0) stop();
        }
    }
}
