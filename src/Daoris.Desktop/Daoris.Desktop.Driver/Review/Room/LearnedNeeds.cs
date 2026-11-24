using System.Text.Json;

namespace Daoris.Driver;

/// <summary>One finished run of a gate on this machine: its peak memory, the cores it kept busy on average, how long it took and whether a test of it timed out.</summary>
public sealed record RunSample(DateTimeOffset At, long PeakMemory, double Cores, double Seconds, bool TimedOut);

/// <summary>A gate's need as admission reads it: its cost, whether <c>learned</c> from runs or <c>declared</c>, and the runs it rests on.</summary>
public sealed record NeedReading(LoadCost Cost, string Source, int Samples)
{
    public const string Learned = "learned";
    public const string DeclaredSource = "declared";
}

/// <summary>
/// What each gate has been seen to need on this machine: its last <see cref="Keep"/> runs per repository, kind and gate, in
/// <c>admission/learned.json</c> of the home. After <see cref="LearnedAfter"/> runs the learned need replaces the declared one, which
/// stood only until then. A file that does not read is no history, so the declared cost stands.
/// </summary>
public static class LearnedNeeds
{
    /// <summary>How many runs make a need learned: a single run may be the odd one, a second says what the gate usually asks.</summary>
    public const int LearnedAfter = 2;

    /// <summary>How many runs of a gate are kept.</summary>
    public const int Keep = 8;

    /// <summary>How many of the newest runs the need reads, so a gate that got lighter is read as lighter within a few runs.</summary>
    public const int Recent = 5;

    /// <summary>The margin over the largest recent peak: a tenth, since a run's peak is a floor of what the next may reach.</summary>
    public const double Margin = 1.1;

    /// <summary>Where the history is, beside the machine's admission.</summary>
    public static string PathOf(string home) => DaorisHome.Entry(home, HomeEntry.Locks, "admission", "learned.json");

    /// <summary>The history's key: a repository's kind and gate.</summary>
    public static string KeyOf(string repository, string kind, string gate) => $"{repository}|{kind}|{gate}";

    /// <summary>The need the <paramref name="samples"/> say, or the <paramref name="declared"/> cost until there are <see cref="LearnedAfter"/>.</summary>
    public static NeedReading Need(IReadOnlyList<RunSample> samples, LoadCost declared)
    {
        if (samples.Count < LearnedAfter) return new NeedReading(declared, NeedReading.DeclaredSource, samples.Count);
        var recent = samples.OrderByDescending(sample => sample.At).Take(Recent).ToList();
        var cores = recent.Select(sample => sample.Cores).OrderBy(value => value).ToList();
        var median = cores.Count % 2 == 1 ? cores[cores.Count / 2] : (cores[cores.Count / 2 - 1] + cores[cores.Count / 2]) / 2;
        return new NeedReading(new LoadCost((long)(recent.Max(sample => sample.PeakMemory) * Margin), Math.Round(median, 1)), NeedReading.Learned, samples.Count);
    }

    /// <summary>The runs kept for <paramref name="key"/>, oldest first.</summary>
    public static IReadOnlyList<RunSample> Samples(string home, string key) =>
        Read(PathOf(home)).GetValueOrDefault(key) ?? [];

    /// <summary>When a run of any gate last had a test time out, or null where none has.</summary>
    public static DateTimeOffset? LastTimeout(string home) =>
        Read(PathOf(home)).Values.SelectMany(list => list).Where(sample => sample.TimedOut).Select(sample => (DateTimeOffset?)sample.At).Max();

    /// <summary>Keep a finished run under <paramref name="key"/>: the file changed whole, under its keeping lock, the oldest past <see cref="Keep"/> dropped.</summary>
    public static void Record(string home, string key, RunSample sample)
    {
        var path = PathOf(home);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var held = KeepLock.Take(path);
        var all = Read(path);
        var list = all.GetValueOrDefault(key)?.ToList() ?? [];
        list.Add(sample);
        all[key] = [.. list.OrderBy(each => each.At).TakeLast(Keep)];
        AtomicFile.WriteText(path, Write(all));
    }

    internal static Dictionary<string, IReadOnlyList<RunSample>> Read(string path)
    {
        var all = new Dictionary<string, IReadOnlyList<RunSample>>(StringComparer.Ordinal);
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty("needs", out var needs) || needs.ValueKind != JsonValueKind.Object) return all;
            foreach (var entry in needs.EnumerateObject())
            {
                if (entry.Value.ValueKind != JsonValueKind.Array) continue;
                all[entry.Name] = [.. entry.Value.EnumerateArray().Select(Sample).OfType<RunSample>()];
            }
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
        }

        return all;
    }

    private static RunSample? Sample(JsonElement raw)
    {
        if (raw.ValueKind != JsonValueKind.Object || !raw.TryGetProperty("at", out var at) || at.ValueKind != JsonValueKind.String
            || !DateTimeOffset.TryParse(at.GetString(), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var when)
            || !raw.TryGetProperty("memory", out var memory) || !memory.TryGetInt64(out var peak)
            || !raw.TryGetProperty("cores", out var cores) || !cores.TryGetDouble(out var busy)
            || !raw.TryGetProperty("seconds", out var seconds) || !seconds.TryGetDouble(out var took))
        {
            return null;
        }

        return new RunSample(when, peak, busy, took, raw.TryGetProperty("timedOut", out var timed) && timed.ValueKind == JsonValueKind.True);
    }

    private static string Write(Dictionary<string, IReadOnlyList<RunSample>> all)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schema", 1);
            writer.WriteStartObject("needs");
            foreach (var (key, list) in all.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                writer.WriteStartArray(key);
                foreach (var sample in list)
                {
                    writer.WriteStartObject();
                    writer.WriteString("at", MachineAdmission.Stamp(sample.At));
                    writer.WriteNumber("memory", sample.PeakMemory);
                    writer.WriteNumber("cores", sample.Cores);
                    writer.WriteNumber("seconds", sample.Seconds);
                    if (sample.TimedOut) writer.WriteBoolean("timedOut", true);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(buffer.ToArray()).ReplaceLineEndings("\n") + "\n";
    }
}
