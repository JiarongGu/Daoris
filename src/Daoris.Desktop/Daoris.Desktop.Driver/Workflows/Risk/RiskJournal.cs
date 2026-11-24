using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// One <c>risk.judged</c> line: when, at the hold's open or its release, which hold (its key and today's name for it), the act,
/// the actor, the place and its mode, the run and step where it is a run's, the subject, the verdict (outcome, class, role, rule, revision,
/// reason), and beside it what today's gate did (<see cref="Today"/>, <see cref="TodayHeld"/>), so a disagreement is counted.
/// </summary>
/// <param name="Actor">Who answers the hold today: <c>person</c>, an agent by its name, or the line a declaration landed on.</param>
public sealed record RiskJudged(
    DateTimeOffset At, string Stage, string Key, string Hold, string Kind, string? Qualifier, string Actor, string? Workspace, string? Repository,
    string Mode, string? Run, string? Step, string Subject, string Outcome, string Class, string? By, string Source, string Revision, string Reason,
    string Today, bool TodayHeld)
{
    public const string Event = "risk.judged";
    public const string Opened = "open";
    public const string Released = "release";

    public string Name => Event;

    /// <summary>Whether the verdict agrees with today's gate: both hold, or both let it go.</summary>
    public bool Agrees => TodayHeld == (Outcome != RiskVerdict.Pass);

    /// <summary>The line written, one JSON object.</summary>
    public string Line()
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("event", Event);
            writer.WriteString("at", At);
            writer.WriteString("stage", Stage);
            writer.WriteString("key", Key);
            writer.WriteString("hold", Hold);
            writer.WriteString("kind", Kind);
            writer.WriteString("qualifier", Qualifier);
            writer.WriteString("actor", Actor);
            writer.WriteString("workspace", Workspace);
            writer.WriteString("repository", Repository);
            writer.WriteString("mode", Mode);
            writer.WriteString("run", Run);
            writer.WriteString("step", Step);
            writer.WriteString("subject", Subject);
            writer.WriteString("outcome", Outcome);
            writer.WriteString("class", Class);
            writer.WriteString("by", By);
            writer.WriteString("source", Source);
            writer.WriteString("revision", Revision);
            writer.WriteString("reason", Reason);
            writer.WriteString("today", Today);
            writer.WriteBoolean("todayHeld", TodayHeld);
            writer.WriteBoolean("agrees", Agrees);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>The line read, or null where it is no <c>risk.judged</c> line of this shape.</summary>
    public static RiskJudged? Of(string line)
    {
        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            string? Text(string name) => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            if (Text("event") != Event || !root.TryGetProperty("at", out var at) || !at.TryGetDateTimeOffset(out var when)) return null;
            if (Text("stage") is not { } stage || Text("key") is not { } key || Text("kind") is not { } kind || Text("outcome") is not { } outcome) return null;
            return new(when, stage, key, Text("hold") ?? "", kind, Text("qualifier"), Text("actor") ?? "", Text("workspace"), Text("repository"),
                Text("mode") ?? "", Text("run"), Text("step"), Text("subject") ?? "", outcome, Text("class") ?? "", Text("by"), Text("source") ?? "",
                Text("revision") ?? "", Text("reason") ?? "", Text("today") ?? "",
                root.TryGetProperty("todayHeld", out var held) && held.ValueKind == JsonValueKind.True);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The line as <c>daoris settings risk why</c> says it.</summary>
    public string Said()
    {
        var act = Qualifier is { } qualifier ? $"{Kind}/{qualifier}" : Kind;
        var by = By is { } role ? $" by `{role}`" : "";
        var differs = Agrees ? "" : " — differs from today";
        var at = At.UtcDateTime.ToString("yyyy-MM-dd HH:mm'Z'", CultureInfo.InvariantCulture);
        return $"{at} {Stage} {Step ?? Subject} {act}: {Outcome} ({Class}{by}) — rule {Source} at {Revision}: {Reason} — today: {Today}{differs}";
    }
}

/// <summary>
/// The home's journal of the judge's verdicts (<c>data/workflows/risk/judged.jsonl</c>), one <see cref="RiskJudged"/> line each, appended per
/// hold's open and release, never per tool call; history, so a purge moves it out (<see cref="HomeHistory.WorkflowRecords"/>).
/// </summary>
public static class RiskJournal
{
    /// <summary>The folder under the workflows' records.</summary>
    public const string Folder = "risk";

    private static readonly object Writing = new();

    public static string PathOf(string home) => Path.Combine(DaorisHome.Entry(home, HomeEntry.Workflows, Folder), "judged.jsonl");

    /// <summary>One line appended.</summary>
    public static void Append(string home, RiskJudged judged)
    {
        var path = PathOf(home);
        lock (Writing)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.AppendAllText(path, judged.Line() + "\n");
        }
    }

    /// <summary>Every line the journal holds, oldest first; a line that does not read is passed over.</summary>
    public static IReadOnlyList<RiskJudged> Read(string home)
    {
        var path = PathOf(home);
        if (!File.Exists(path)) return [];
        var lines = new List<string>();
        lock (Writing)
        {
            // read-shares: the verdict journal, appended a line at a time and read while the loop may append.
            using var reader = new StreamReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete));
            while (reader.ReadLine() is { } line) lines.Add(line);
        }

        return [.. lines.Where(line => line.Length > 0).Select(RiskJudged.Of).OfType<RiskJudged>()];
    }
}

/// <summary>Why a run's holds stood: each verdict journaled for it (and one step), as <c>daoris settings risk why &lt;run&gt; [&lt;step&gt;]</c> prints it.</summary>
public static class RiskWhy
{
    public static IReadOnlyList<RiskJudged> Rows(string home, string run, string? step) =>
        [.. RiskJournal.Read(home).Where(line => line.Run == run && (step is null || line.Step == step))];

    /// <summary>Each row said, or the one sentence that none is journaled.</summary>
    public static IReadOnlyList<string> Lines(string home, string run, string? step) =>
        Rows(home, run, step) is { Count: > 0 } rows
            ? [.. rows.Select(row => row.Said())]
            : [step is null ? $"no verdict is journaled for the run `{run}`." : $"no verdict is journaled for the step `{step}` of the run `{run}`."];

    /// <summary>The lines the terminal says: a heading and each row, or the one sentence that none is journaled.</summary>
    public static IReadOnlyList<string> Said(string home, string run, string? step) =>
        Rows(home, run, step) is { Count: > 0 } rows
            ? [$"daoris: the risk verdicts journaled for the run `{run}`{(step is null ? "" : $", step `{step}`")}:", .. rows.Select(row => $"  {row.Said()}")]
            : [$"daoris: {Lines(home, run, step)[0]}"];
}
