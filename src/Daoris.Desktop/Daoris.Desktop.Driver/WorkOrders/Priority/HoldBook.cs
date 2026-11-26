using System.Text.Json;

namespace Daoris.Driver;

/// <summary>One hold or lift of an ask or a quest, as it is kept: when, on what, whether it holds or lifts, why and by whom.</summary>
/// <param name="Target"><see cref="PriorityBook.Ask"/> or <see cref="PriorityBook.Quest"/>.</param>
/// <param name="Held">True to hold the target's waiting items aside, false to lift an earlier hold.</param>
public sealed record HoldEvent(DateTimeOffset At, string Target, string Id, string Workspace, bool Held, string Why, string By)
{
    /// <summary>The run a quest's work is (its chain's first quest in its repository), which the queues read; null for an ask.</summary>
    public string? Run { get; init; }
}

/// <summary>
/// *Hold waiting*: an ask's or a quest's queued items stand aside while its running work finishes and keeps its
/// result, and return at their first arrival once the hold is lifted. Each hold and lift is one event, kept in the classes' file
/// (<see cref="PriorityBook"/>, so one lock keeps both) in the order made, and read at each look, never copied into a run. A quest's own newest
/// word stands, else its ask's; a lift is a word like a hold, so a quest of a held ask can be lifted alone. A hold cuts nothing: a running
/// check, a candidate whose gates run and a session in its turn are not queued items, so no queue reads it for them.
/// </summary>
public static class HoldBook
{
    /// <summary>Every hold and lift kept, in the order made.</summary>
    public static IReadOnlyList<HoldEvent> Read(string home) => PriorityBook.Load(home).Holds;

    /// <summary>
    /// Keep one hold or lift, or say why not: no why, or a target that is neither an ask nor a quest. Null where it is kept.
    /// </summary>
    /// <exception cref="IOException">Another writer held the file's keeping lock for the whole of its wait.</exception>
    public static string? Set(string home, HoldEvent change)
    {
        if (string.IsNullOrWhiteSpace(change.Why)) return "a hold is set with why: say it with --why.";
        if (change.Target is not (PriorityBook.Ask or PriorityBook.Quest) || string.IsNullOrWhiteSpace(change.Id)) return "a hold is set on an ask or a quest.";

        var file = PriorityBook.PathOf(home);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        using var kept = KeepLock.Take(file);
        var (events, holds) = PriorityBook.Load(home);
        AtomicFile.WriteText(file, PriorityBook.Serialize(events, [.. holds, change with { Why = change.Why.Trim() }]));
        return null;
    }

    /// <summary>The newest word on one ask or quest, a hold or a lift; null where none is kept.</summary>
    public static HoldEvent? Standing(IReadOnlyList<HoldEvent> holds, string target, string id) =>
        holds.LastOrDefault(each => each.Target == target && string.Equals(each.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Whether a run's queued items stand aside: the newest word on any quest of its chain (the run's own id, or a quest kept with it as its
    /// run), else its ask's. Where the chain's quests differ the newest word among them stands.
    /// </summary>
    public static bool IsHeld(IReadOnlyList<HoldEvent> holds, string run, string? ask) => Reading(holds, run, ask)?.Held == true;

    /// <summary>The word a run is held or lifted by, for a wait line to say whose it is; null where none is kept.</summary>
    public static HoldEvent? Reading(IReadOnlyList<HoldEvent> holds, string run, string? ask)
    {
        if (holds.Count == 0) return null;
        var own = holds.LastOrDefault(each => each.Target == PriorityBook.Quest
            && (string.Equals(each.Id, run, StringComparison.OrdinalIgnoreCase) || string.Equals(each.Run, run, StringComparison.OrdinalIgnoreCase)));
        if (own is not null) return own;
        return ask is { Length: > 0 } ? Standing(holds, PriorityBook.Ask, ask) : null;
    }

    /// <summary>A hold as a wait line says it: <c>held by the person: make room</c>.</summary>
    public static string Said(HoldEvent hold) => $"held by {hold.By}: {hold.Why}";

    internal static HoldEvent? Parse(JsonElement raw)
    {
        if (raw.ValueKind != JsonValueKind.Object) return null;
        string? Text(string name) => raw.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        return Text("at") is { } at && DateTimeOffset.TryParse(at, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var when)
            && Text("target") is { } target && Text("id") is { } id && raw.TryGetProperty("held", out var held) && held.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? new HoldEvent(when, target, id, Text("workspace") ?? "", held.GetBoolean(), Text("why") ?? "", Text("by") ?? "") { Run = Text("run") }
            : null;
    }

    internal static void Write(Utf8JsonWriter writer, HoldEvent each)
    {
        writer.WriteStartObject();
        writer.WriteString("at", each.At.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        writer.WriteString("target", each.Target);
        writer.WriteString("id", each.Id);
        writer.WriteString("workspace", each.Workspace);
        writer.WriteBoolean("held", each.Held);
        writer.WriteString("why", each.Why);
        writer.WriteString("by", each.By);
        if (each.Run is { } run) writer.WriteString("run", run);
        writer.WriteEndObject();
    }
}
