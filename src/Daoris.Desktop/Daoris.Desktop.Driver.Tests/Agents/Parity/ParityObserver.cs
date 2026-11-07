using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What a path observed, written into its <see cref="ParityObservation"/> by one reading of each thing, whichever path it came from: the
/// readings handed the sink, the lineage's lines, the streams beside, the transcript's lines, the harness's own words, the failure as
/// <see cref="AgentError"/> reads it and the end. A failure handed the sink is the failure's observation, read once at the end on both paths.
/// </summary>
internal static class ParityObserver
{
    /// <summary>
    /// The sink a path's run is handed: watched, as a console someone watches is, so the streams the agent runs beside itself are kept apart
    /// from its own lines; each reading observed as it arrives.
    /// </summary>
    public static IReadingSink Sink(ParityObservation seen) => new ObservedSink(seen);

    public static void Read(ParityObservation seen, WireReading reading)
    {
        // The line a child's reading writes to the session's lineage (LineageReadings), stamped at the run's start: the file's own stamp is
        // the wall clock's, which no two runs share.
        if (ChildBindings.ChildLine(reading, seen.Start) is { } line) seen.Add(ParityChannel.Lineage, "line", Lineage(line));

        switch (reading)
        {
            case WireReading.ConversationNamed named:
                seen.Add(ParityChannel.Conversation, "named", ("conversation", named.Conversation));
                break;
            case WireReading.Shown shown:
                seen.Add(ParityChannel.Console, "shown", ("text", shown.Text));
                break;
            case WireReading.Said said:
                seen.Add(ParityChannel.Events, "said", Fields(said.Event));
                break;
            case WireReading.WindowsSaid windows:
                seen.Add(ParityChannel.Windows, "windows", ("windows", windows.Windows.GetRawText()));
                break;
            case WireReading.StreamOpened or WireReading.StreamSaid or WireReading.StreamEnded:
                seen.Add(ParityChannel.Streams, reading.GetType().Name, Whole(reading));
                break;
            case WireReading.Failed:
                break;
            default:
                seen.Add(ParityChannel.Events, reading.GetType().Name, Whole(reading));
                break;
        }
    }

    /// <summary>
    /// A reading whole, its run's stamp with it, but for its process: the lineage token each process is issued is a generated id, compared by
    /// first appearance, where the session, the generation and the place in the run's order are compared exactly.
    /// </summary>
    private static (string Name, string? Value)[] Whole(WireReading reading) =>
        [("reading", JsonSerializer.Serialize<object>(reading with { Process = null })), (ParityProjection.Process, reading.Process)];

    /// <summary>The transcript's lines, in order.</summary>
    public static void Transcript(ParityObservation seen, string file)
    {
        foreach (var line in File.Exists(file) ? File.ReadAllLines(file) : []) seen.Add(ParityChannel.Transcript, "line", ("text", line));
    }

    /// <summary>The end as the conclusion reads it, and the harness's own words its ending kept.</summary>
    public static void Ended(ParityObservation seen, AgentRunEnd end, bool stopped)
    {
        foreach (var line in end.Ending.Lines) seen.Add(ParityChannel.HarnessWords, "word", ("text", line));
        seen.Add(
            ParityChannel.End, "end", ("exit", end.Exit?.ToString()), ("named", end.Named.ToString()), ("failed", end.Ending.Failed.ToString()),
            ("stopped", stopped.ToString()),
            ("modelRefused", end.Ending.ModelRefused.ToString()), ("cut", string.Join('\n', end.Ending.BackgroundCut)), ("usage", end.Usage?.ToString()));
    }

    /// <summary>The failure as <see cref="AgentError"/> reads it, with the delivery the path read for the turn's words.</summary>
    public static void Failed(ParityObservation seen, AgentError error, FailureDelivery delivery)
    {
        var limit = error.Limit;
        seen.Add(
            ParityChannel.Failure, "error", ("kind", error.Kind.ToString()), ("phase", error.Failure.Where),
            ("access", error.Access?.ToString()), ("hit", limit?.Hit), ("window", limit?.Window), ("stated", limit?.Stated.ToString()),
            (ParityProjection.Until, limit?.Until.ToString("O")), (ParityProjection.By, error.By), ("delivery", delivery.ToString()));
    }

    /// <summary>
    /// Today's reading of a failed turn's words, from what the path heard of the turn (the agent-lyntai design §5.3): a tool step heard is
    /// uncertain, the agent's words, thinking or a plan heard taken, nothing heard never sent.
    /// </summary>
    public static FailureDelivery Heard(ParityObservation seen)
    {
        var said = seen.Seen.Where(each => each.Channel == ParityChannel.Events && each.What == "said").Select(each => each["kind"]).ToList();
        if (said.Contains(SessionEventKind.Tool)) return FailureDelivery.Uncertain;
        return said.Any(kind => kind is SessionEventKind.Message or SessionEventKind.Thought or SessionEventKind.Plan)
            ? FailureDelivery.Taken
            : FailureDelivery.NeverSent;
    }

    /// <summary>
    /// A lineage line's fields in its own order, its process token named as the projection compares one; its <c>by</c> is the signal that
    /// said the child, no provenance, so it is compared exactly.
    /// </summary>
    private static (string Name, string? Value)[] Lineage(string line) =>
    [
        .. JsonDocument.Parse(line).RootElement.EnumerateObject().Select(property => (
            property.Name switch { "proc" => ParityProjection.Process, "by" => "signal", var name => name },
            property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : property.Value.GetRawText())),
    ];

    private sealed class ObservedSink(ParityObservation seen) : IReadingSink
    {
        public bool Watched => true;

        public void Read(WireReading reading) => ParityObserver.Read(seen, reading);
    }

    private static (string Name, string? Value)[] Fields(SessionEvent e)
    {
        var fields = new List<(string, string?)>();
        foreach (var property in JsonSerializer.SerializeToElement(e).EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Null) continue;
            var name = char.ToLowerInvariant(property.Name[0]) + property.Name[1..];
            var value = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : property.Value.GetRawText();
            fields.Add((name, value));
        }

        // The models a turn's tokens were spent on are carried in memory for the usage history, so the record's own spelling leaves them out.
        if (e.Models is { } models) fields.Add(("models", JsonSerializer.Serialize(models)));
        return [.. fields];
    }
}
