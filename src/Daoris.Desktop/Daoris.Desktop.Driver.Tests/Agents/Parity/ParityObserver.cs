using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What a path observed, written into its <see cref="ParityObservation"/> by one reading of each thing, whichever path it came from: the
/// readings handed the sink, the transcript's lines, the harness's own words, the failure as <see cref="AgentError"/> reads it and the end.
/// A failure handed the sink is the failure's observation, read once at the end on both paths.
/// </summary>
internal static class ParityObserver
{
    /// <summary>The sink a path's run is handed: each reading observed as it arrives.</summary>
    public static IReadingSink Sink(ParityObservation seen) => ReadingSinks.On<WireReading>(reading => Read(seen, reading));

    public static void Read(ParityObservation seen, WireReading reading)
    {
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
                seen.Add(ParityChannel.Streams, reading.GetType().Name, ("reading", JsonSerializer.Serialize<object>(reading)));
                break;
            case WireReading.Failed:
                break;
            case WireReading.Loaded loaded:
                // What the init says the harness loaded, never the run's stamp on it, which each path gives its own run.
                seen.Add(ParityChannel.Events, nameof(WireReading.Loaded), ("init", JsonSerializer.Serialize(loaded.Init)));
                break;
            default:
                seen.Add(ParityChannel.Events, reading.GetType().Name, ("reading", JsonSerializer.Serialize<object>(reading)));
                break;
        }
    }

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
            ("modelRefused", end.Ending.ModelRefused.ToString()), ("usage", end.Usage?.ToString()));
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
