using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>fixtures/line-counts.json</c> as the driver's tests read it (CONTEXTDRIVE1a): records in the shape the service answers, and the
/// events of a part as short tokens. The service's <c>LineKeysFixtureTests</c> reads the same file for the same lines.
/// </summary>
internal static class LineCountsFixture
{
    public static JsonElement Read()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "line-counts.json")));
        return document.RootElement.Clone();
    }

    /// <summary>A row's records as the service would answer them in one list; null where the row has the service not answer.</summary>
    public static string? Json(JsonElement row) =>
        row.GetProperty("records") is { ValueKind: JsonValueKind.Array } records ? records.GetRawText() : null;

    public static IReadOnlyDictionary<string, PriorSession> Records(JsonElement row) =>
        Json(row) is { } json ? ServiceClient.ReadRecords(json) : new Dictionary<string, PriorSession>();

    public static IReadOnlyDictionary<string, PartEvents?> Events(JsonElement row)
    {
        var events = new Dictionary<string, PartEvents?>(StringComparer.Ordinal);
        if (!row.TryGetProperty("events", out var each)) return events;
        foreach (var part in each.EnumerateObject())
        {
            var trimmed = part.Value.TryGetProperty("trimmed", out var cut) && cut.GetBoolean();
            var seq = trimmed ? 5L : 1L;
            events[part.Name] = new PartEvents(
                [.. part.Value.GetProperty("tokens").EnumerateArray().Select(token => Event(token.GetString()!, seq++))], trimmed);
        }

        return events;
    }

    /// <summary>When a row's admission was seeded: a part it has not read is fed only what it kept after.</summary>
    public static readonly DateTimeOffset Seeded = DateTimeOffset.Parse("2026-11-03T09:00:00Z");

    /// <summary>
    /// A row's admission as the store would keep it (COMMS1g): its epochs in order, its go-ons not yet shown by the events, and the last event
    /// it read of each part; null where the row has none kept, so the events count it.
    /// </summary>
    public static AloneRows? Admission(JsonElement row, string line)
    {
        if (!row.TryGetProperty("admission", out var kept)) return null;
        var epochs = kept.GetProperty("epochs").EnumerateArray().Select((epoch, index) => new AloneEpoch(
            index, epoch.GetProperty("cause").GetString()!, null, epoch.GetProperty("n").GetInt32(), epoch.GetProperty("known").GetString()!,
            epoch.TryGetProperty("why", out var why) ? why.GetString() : null, Seeded));
        var admitted = kept.TryGetProperty("admitted", out var each)
            ? each.EnumerateArray().Select((go, index) => new AloneAdmitted(
                index + 1, 0, go.GetProperty("source").GetString()!, [.. go.GetProperty("words").EnumerateArray().Select(word => word.GetString()!)], null, Seeded))
            : [];
        var read = kept.GetProperty("read").EnumerateObject().ToDictionary(part => part.Name, part => part.Value.GetInt64(), StringComparer.Ordinal);
        return new AloneRows(line, [.. epochs], [.. admitted], read, AloneScan.Start);
    }

    private static SessionEvent Event(string token, long seq) => token switch
    {
        "started" => new() { Seq = seq, Kind = SessionEventKind.User, Origin = "target", Text = "The quest's instruction." },
        "brief" => new() { Seq = seq, Kind = SessionEventKind.User, Origin = "target", Continues = "s0", Text = "The brief of the line." },
        "person" => new() { Seq = seq, Kind = SessionEventKind.User, Origin = "person", Text = "Go on." },
        "answer" => new() { Seq = seq, Kind = SessionEventKind.User, Origin = "target", Helper = "h1", Text = "Another agent answered." },
        "acts" => new() { Seq = seq, Kind = SessionEventKind.Message, Text = "I went on." },
        "end" => new() { Seq = seq, Kind = SessionEventKind.Turn, StopReason = "end_turn" },
        _ => throw new InvalidOperationException($"no event token {token}"),
    };
}
