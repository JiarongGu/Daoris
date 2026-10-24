using System.Text.Json;

namespace Daoris.Desktop;

/// <summary>
/// The kinds of a person's write on a quest's records that make it the owner's for the run (STEWARDROLE1a-ii, D209 §5.4), exactly: a
/// session's move, a word to one of its sessions, a hand-over, a status changed after its publication. A twin
/// (<c>.claude/knowledge/twins.md</c>): the service's <c>PersonDoors</c> declares, door by door, which kind its effect is and the
/// evidence it leaves (<c>OwnerMark</c>); <see cref="OwnerRecords"/> reads that evidence; <c>OwnerSetCensusTests</c> holds every
/// declared evidence to one this reader knows.
/// </summary>
public static class OwnerRecordKinds
{
    public const string SessionMove = "session-move";
    public const string SessionWord = "session-word";
    public const string HandOver = "hand-over";
    public const string Status = "status";

    /// <summary>Every kind, the closed list; a door that writes none of them declares <c>none</c> with its reason.</summary>
    public static IReadOnlyList<string> All { get; } = [SessionMove, SessionWord, HandOver, Status];
}

/// <summary>
/// A quest's records as the owner's mark reads them (§5.4): its moves, with their moments; its note's coded parts and when its status
/// last moved; the ask's words that name it; and each of its sessions' note parts, its moment and whether a person's word waits on it.
/// </summary>
public sealed record OwnerRecordFacts(
    IReadOnlyList<(string Kind, DateTimeOffset At)> Moves,
    IReadOnlyList<string> QuestNote,
    DateTimeOffset? QuestUpdated,
    IReadOnlyList<(string Kind, DateTimeOffset At)> Words,
    IReadOnlyList<(IReadOnlyList<string> Note, DateTimeOffset? Updated, bool PersonSaid)> Sessions);

/// <summary>
/// The owner's mark by the record (STEWARDROLE1a-ii, D209 §5.4): the service's person doors reach records without the instance, so the
/// <c>owner-set</c> condition also reads the quest's records and finds, after this run's start, the evidence one of those doors leaves.
/// A quest's publication and its ask's own words (<c>added</c>) are never evidence, so a quest the owner set going this run is not the
/// owner's from birth.
/// </summary>
/// <remarks>
/// Evidence is spelled <c>move:&lt;kind&gt;</c> (a move of that kind, as the moves door spells it), <c>note:&lt;code&gt;</c> (the quest's
/// note part with that code, its status moved since), <c>session-note:&lt;code&gt;</c> (a note part on one of its sessions, its record
/// moved since), <c>said:person</c> (a person's word waiting on one of its sessions), <c>word:&lt;kind&gt;</c> (an ask's word naming it),
/// or <c>instance:&lt;ROUTE&gt;</c> (the person's press at the instance's door, which marks it in memory as it runs).
/// </remarks>
public static class OwnerRecords
{
    /// <summary>Every evidence this reader reads, what a service door may declare.</summary>
    public static IReadOnlyList<string> Evidence { get; } =
    [
        "move:accepted", "move:verdict", "move:reopened", "move:rebound", "move:deleted", "move:dismissed",
        "note:quest.person-done", "session-note:ledger.answered", "said:person", "word:answered", "word:reopened",
        "instance:WORK_ABANDON",
    ];

    /// <summary>The evidence in <paramref name="facts"/> written after <paramref name="since"/>, or null where none is.</summary>
    public static string? Marked(OwnerRecordFacts facts, DateTimeOffset since)
    {
        foreach (var evidence in Evidence)
        {
            var (kind, value) = (evidence[..evidence.IndexOf(':', StringComparison.Ordinal)], evidence[(evidence.IndexOf(':', StringComparison.Ordinal) + 1)..]);
            var found = kind switch
            {
                "move" => facts.Moves.Any(each => each.At > since && string.Equals(each.Kind, value, StringComparison.OrdinalIgnoreCase)),
                "note" => facts.QuestUpdated > since && facts.QuestNote.Contains(value, StringComparer.Ordinal),
                "session-note" => facts.Sessions.Any(each => each.Updated > since && each.Note.Contains(value, StringComparer.Ordinal)),
                "said" => facts.Sessions.Any(each => each.PersonSaid),
                "word" => facts.Words.Any(each => each.At > since && string.Equals(each.Kind, value, StringComparison.OrdinalIgnoreCase)),
                _ => false,
            };
            if (found) return evidence;
        }

        return null;
    }

    /// <summary>
    /// <paramref name="quest"/>'s facts from the service's answers as their doors give them: the quest (<c>/api/quests</c>, a list or
    /// one), its moves (<c>/api/quests/{id}/moves</c>), the asks (<c>/api/asks</c>) and the session records (<c>/api/sessions</c>).
    /// </summary>
    public static OwnerRecordFacts Read(string quest, string? quests, string? moves, string? asks, string? sessions)
    {
        var id = quest.Trim().TrimStart('#');
        var movesRead = new List<(string, DateTimeOffset)>();
        var note = new List<string>();
        DateTimeOffset? updated = null;
        var words = new List<(string, DateTimeOffset)>();
        var sessionsRead = new List<(IReadOnlyList<string>, DateTimeOffset?, bool)>();

        if (moves is not null)
        {
            using var document = JsonDocument.Parse(moves);
            var list = document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("moves", out var named) ? named : document.RootElement;
            foreach (var move in Each(list))
            {
                if (Text(move, "kind") is { } kind && Moment(move, "at") is { } at) movesRead.Add((kind, at));
            }
        }

        if (quests is not null)
        {
            using var document = JsonDocument.Parse(quests);
            var root = document.RootElement;
            var one = root.ValueKind == JsonValueKind.Object && Text(root, "id") is not null
                ? root
                : Each(root.ValueKind == JsonValueKind.Object && root.TryGetProperty("quests", out var listed) ? listed : root)
                    .FirstOrDefault(each => Same(Text(each, "id"), id));
            if (one.ValueKind == JsonValueKind.Object)
            {
                updated = Moment(one, "updated");
                note.AddRange(Codes(one));
            }
        }

        if (asks is not null)
        {
            using var document = JsonDocument.Parse(asks);
            var root = document.RootElement;
            foreach (var ask in Each(root.ValueKind == JsonValueKind.Object && root.TryGetProperty("asks", out var listed) ? listed : root))
            {
                foreach (var word in Each(ask.ValueKind == JsonValueKind.Object && ask.TryGetProperty("words", out var said) ? said : default))
                {
                    if (Same(Text(word, "quest"), id) && Text(word, "kind") is { } kind && Moment(word, "at") is { } at) words.Add((kind, at));
                }
            }
        }

        if (sessions is not null)
        {
            using var document = JsonDocument.Parse(sessions);
            var root = document.RootElement;
            foreach (var record in Each(root.ValueKind == JsonValueKind.Object && root.TryGetProperty("sessions", out var listed) ? listed : root))
            {
                if (!Same(Text(record, "quest"), id)) continue;
                var person = Each(record.ValueKind == JsonValueKind.Object && record.TryGetProperty("said", out var said) ? said : default)
                    .Any(word => Text(word, "by") is null && Text(word, "helper") is null && Text(word, "from") is null);
                sessionsRead.Add(([.. Codes(record)], Moment(record, "updated"), person));
            }
        }

        return new OwnerRecordFacts(movesRead, note, updated, words, sessionsRead);
    }

    private static IEnumerable<string> Codes(JsonElement record) =>
        Each(record.TryGetProperty("noteParts", out var parts) ? parts : default).Select(part => Text(part, "code")).OfType<string>();

    private static IEnumerable<JsonElement> Each(JsonElement array) =>
        array.ValueKind == JsonValueKind.Array ? array.EnumerateArray() : [];

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static DateTimeOffset? Moment(JsonElement element, string name) =>
        Text(element, name) is { } text && DateTimeOffset.TryParse(text, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind, out var at)
            ? at
            : null;

    private static bool Same(string? one, string other) =>
        one is not null && string.Equals(one.TrimStart('#'), other, StringComparison.OrdinalIgnoreCase);
}
