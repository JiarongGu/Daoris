using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>The plugin's first line to the relay, checked whole against its closed schema (the event hook design §6.1 step 3).</summary>
public sealed record RelayFirst(string Run, long Seq, int Grammar, string Build);

/// <summary>
/// One <c>work/event</c> frame the relay read and checked against <see cref="WorkEventGrammar"/> (§6.1 step 4): made only by
/// <see cref="RelayChecks.Frame"/>, so a sentence built from one holds nothing but ids in their minted shapes, closed codes and counts.
/// </summary>
public sealed class RelayFrame
{
    internal RelayFrame(string run, long seq, long missed, string kind, IReadOnlyDictionary<string, string> fields) =>
        (Run, Seq, Missed, Kind, Fields) = (run, seq, missed, kind, fields);

    public string Run { get; }

    public long Seq { get; }

    public long Missed { get; }

    public string Kind { get; }

    /// <summary>The kind's own fields, each spelled as its frame carried it, every one checked.</summary>
    public IReadOnlyDictionary<string, string> Fields { get; }
}

/// <summary>
/// What the relay takes from its pipe and its arguments, each checked before any of it is used (EVENTHOOK1c; §6.1 steps 3 to 5, §8): a
/// value that fails is refused whole and never printed.
/// </summary>
public static class RelayChecks
{
    /// <summary>The longest first line taken.</summary>
    public const int FirstLineBytes = 512;

    /// <summary>The longest frame read; a longer one is read no further.</summary>
    public const int FrameBytes = 4096;

    /// <summary>The plugin's name on its first line.</summary>
    public const string Plugin = "event-relay";

    /// <summary>The largest <c>seq</c> taken: 2^53, every integer below it exact in any JSON reader.</summary>
    public const long SeqLimit = 9_007_199_254_740_992;

    /// <summary>The run a relay with no watermark names: one the set never mints, so the next relay's first line says the gap.</summary>
    public const string NoRun = "000000000000";

    private static readonly Regex Run = new(@"\A[0-9a-f]{12}\z", RegexOptions.CultureInvariant);
    private static readonly Regex Build = new(@"\A[0-9a-f]{64}\z", RegexOptions.CultureInvariant);
    private static readonly string[] FirstFields = ["plugin", "run", "seq", "grammar", "build"];

    /// <summary>The first line, or null where it is not exactly the schema's five fields in their shapes.</summary>
    public static RelayFirst? First(string line)
    {
        if (Encoding.UTF8.GetByteCount(line) > FirstLineBytes || Parse(line) is not { } root) return null;
        using (root)
        {
            var top = root.RootElement;
            if (top.ValueKind != JsonValueKind.Object || top.EnumerateObject().Count() != FirstFields.Length) return null;
            if (FirstFields.Any(field => !top.TryGetProperty(field, out _))) return null;
            if (Text(top, "plugin") != Plugin || Text(top, "run") is not { } run || !Run.IsMatch(run) || run == NoRun) return null;
            if (Text(top, "build") is not { } build || !Build.IsMatch(build)) return null;
            if (Count(top, "seq", 0) is not { } seq || Count(top, "grammar", 1) is not { } grammar || grammar > int.MaxValue) return null;
            return new RelayFirst(run, seq, (int)grammar, build);
        }
    }

    /// <summary>A <c>--since</c> watermark, <c>&lt;run&gt;:&lt;seq&gt;</c> as a batch's closing line brackets it, or null.</summary>
    public static (string Run, long Seq)? Since(string text)
    {
        var colon = text.IndexOf(':', StringComparison.Ordinal);
        if (colon < 0 || !Run.IsMatch(text[..colon])) return null;
        var digits = text[(colon + 1)..];
        return digits.Length is > 0 and <= 16 && digits.All(char.IsAsciiDigit)
               && long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var seq) && seq <= SeqLimit
            ? (text[..colon], seq)
            : null;
    }

    /// <summary>A frame of the grammar, or null: its common fields and its kind's closed list each in shape, its run never the sample.</summary>
    public static RelayFrame? Frame(string line, IReadOnlySet<string> adapters)
    {
        if (Encoding.UTF8.GetByteCount(line) > FrameBytes || Parse(line) is not { } root) return null;
        using (root)
        {
            var top = root.RootElement;
            if (top.ValueKind != JsonValueKind.Object) return null;
            var frame = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var field in top.EnumerateObject())
            {
                if (frame.ContainsKey(field.Name)) return null;
                frame[field.Name] = field.Value.ValueKind switch
                {
                    JsonValueKind.String => field.Value.GetString(),
                    JsonValueKind.Number when field.Value.TryGetInt64(out var number) && number <= SeqLimit => number,
                    _ => field.Value.GetRawText(),
                };
            }

            if (WorkEventGrammar.Check(frame, adapters).Count > 0 || frame["run"] is not string run || !Run.IsMatch(run)) return null;
            var fields = frame.Where(pair => !WorkEventGrammar.Common.Contains(pair.Key))
                .ToDictionary(pair => pair.Key, pair => Convert.ToString(pair.Value, CultureInfo.InvariantCulture)!, StringComparer.Ordinal);
            return new RelayFrame(run, (long)frame["seq"]!, (long)frame["missed"]!, (string)frame["kind"]!, fields);
        }
    }

    private static JsonDocument? Parse(string line)
    {
        try
        {
            return JsonDocument.Parse(line);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Text(JsonElement top, string field) =>
        top.GetProperty(field) is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;

    private static long? Count(JsonElement top, string field, long least) =>
        top.GetProperty(field) is { ValueKind: JsonValueKind.Number } value && value.TryGetInt64(out var number) && number >= least && number <= SeqLimit
            ? number
            : null;
}
