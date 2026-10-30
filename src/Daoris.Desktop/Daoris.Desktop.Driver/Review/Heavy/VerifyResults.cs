using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>Where a verification run's files lie (FABRIC-PIPELINE1d; the pipeline design §4.3): a folder of its own under the home's workflows.</summary>
public static class VerifyFiles
{
    /// <summary>The folder under the home's workflows that holds the landed and the person's verification runs.</summary>
    public const string Lines = "lines";

    /// <summary>The name of an attempt's result in its folder: written once, atomically, before the executor says it has an answer.</summary>
    public const string Result = "result.json";

    public static string LinesFolder(string home) => Path.Combine(DaorisHome.Entry(home, HomeEntry.Workflows), Lines);

    /// <summary>A run's folder (<c>lines/&lt;run&gt;/</c>): its attempts, their results and the logs it keeps.</summary>
    public static string RunFolder(string home, string run) =>
        SafeNames.IsPlainId(run) ? Path.Combine(LinesFolder(home), run) : throw new DriverException($"`{run}` is not a run's id.");

    /// <summary>An attempt's folder, named by its invocation.</summary>
    public static string AttemptFolder(string home, string run, string invocation) =>
        SafeNames.IsPlainId(invocation)
            ? Path.Combine(RunFolder(home, run), "attempts", invocation)
            : throw new DriverException($"`{invocation}` is not an invocation's id.");

    public static string ResultOf(string home, string run, string invocation) => Path.Combine(AttemptFolder(home, run, invocation), Result);
}

/// <summary>A check's verdict (the pipeline design §4.1): reached, failed, or unknown, never passed or failed by a guess.</summary>
public static class VerifyVerdicts
{
    public const string Reached = "reached";

    public const string Failed = "failed";

    public const string Unknown = "unknown";
}

/// <summary>One gate as a check ran it: its name as the declaration writes it, its verdict (the runner's words), its exit, its wall and its log.</summary>
public sealed record VerifyGate(string Name, string Verdict, int? Exit = null)
{
    public double? WallSeconds { get; init; }

    /// <summary>Where its log stays: the runner's own place, or the attempt's folder for a gate the executor ran.</summary>
    public string? Log { get; init; }
}

/// <summary>
/// A check attempt's result (FABRIC-PIPELINE1d, schema 1), written in its attempt's folder once the job is empty and both slots are let go:
/// the commit it judged, its verdict and why, each gate's evidence, and the declaration's blob it ran under.
/// </summary>
public sealed record VerifyResult(string Invocation, string Commit, string Verdict, string? Why, IReadOnlyList<VerifyGate> Gates)
{
    /// <summary>The tip's declaration is not the one accepted: nothing ran, and the person's door is <i>Accept the gates</i>.</summary>
    public const string Changed = "changed";

    /// <summary>It ran past its bound and was ended.</summary>
    public const string Bound = "bound";

    /// <summary>The person stopped it, and its job was ended.</summary>
    public const string Stopped = "stopped";

    /// <summary>The runner's own run held its lock through every offer.</summary>
    public const string Busy = "busy";

    /// <summary>Why an unknown is what it is, where a door answers it: <see cref="Changed"/>, <see cref="Bound"/>, <see cref="Stopped"/>, <see cref="Busy"/>.</summary>
    public string? Code { get; init; }

    /// <summary>The gate a failed check names (a runner's name for an opaque one).</summary>
    public string? Failed { get; init; }

    /// <summary>The blob of <c>daoris.gates.json</c> it ran under, or <c>none</c>.</summary>
    public string? Declaration { get; init; }

    public double? WallSeconds { get; init; }

    public void Write(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        AtomicFile.WriteText(path, Serialize());
    }

    public string Serialize()
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schema", 1);
            writer.WriteString("invocation", Invocation);
            writer.WriteString("commit", Commit);
            writer.WriteString("verdict", Verdict);
            if (Why is { } why) writer.WriteString("why", why);
            if (Code is { } code) writer.WriteString("code", code);
            if (Failed is { } failed) writer.WriteString("failed", failed);
            if (Declaration is { } declaration) writer.WriteString("declaration", declaration);
            if (WallSeconds is { } wall) writer.WriteNumber("wallSeconds", Math.Round(wall, 1));
            writer.WriteStartArray("gates");
            foreach (var gate in Gates)
            {
                writer.WriteStartObject();
                writer.WriteString("name", gate.Name);
                writer.WriteString("verdict", gate.Verdict);
                if (gate.Exit is { } exit) writer.WriteNumber("exit", exit);
                if (gate.WallSeconds is { } seconds) writer.WriteNumber("wallSeconds", Math.Round(seconds, 1));
                if (gate.Log is { } log) writer.WriteString("log", log);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray()).ReplaceLineEndings("\n") + "\n";
    }

    /// <summary>The result at <paramref name="path"/>; null where there is none or it does not read as schema 1.</summary>
    public static VerifyResult? Read(string path)
    {
        string text;
        try
        {
            if (!File.Exists(path)) return null;
            text = File.ReadAllText(path, Encoding.UTF8);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("schema", out var schema) || !schema.TryGetInt32(out var number) || number != 1
                || Text(root, "invocation") is not { } invocation || Text(root, "commit") is not { } commit || Text(root, "verdict") is not { } verdict
                || !root.TryGetProperty("gates", out var list) || list.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var gates = new List<VerifyGate>();
            foreach (var each in list.EnumerateArray())
            {
                if (each.ValueKind != JsonValueKind.Object || Text(each, "name") is not { } name || Text(each, "verdict") is not { } said) return null;
                gates.Add(new VerifyGate(name, said, Number(each, "exit")) { WallSeconds = Seconds(each), Log = Text(each, "log") });
            }

            return new VerifyResult(invocation, commit, verdict, Text(root, "why"), gates)
            {
                Code = Text(root, "code"), Failed = Text(root, "failed"), Declaration = Text(root, "declaration"), WallSeconds = Seconds(root),
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static int? Number(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : null;

    private static double? Seconds(JsonElement element) =>
        element.TryGetProperty("wallSeconds", out var value) && value.TryGetDouble(out var seconds) ? seconds : null;
}

/// <summary>What a run of the runner came to: its result, or busy (the runner's own run holds its lock), which the executor waits out.</summary>
public sealed record VerifyJudged(VerifyResult? Result, bool Busy)
{
    public static readonly VerifyJudged WasBusy = new(null, true);
}

/// <summary>
/// A check's verdict from what its runner said (the pipeline design §4.2's contract): a runner that answers by file is judged from its
/// gates alone, against the accepted declaration and the gates the check names; an opaque runner is one check, by its exit.
/// </summary>
public static class VerifyJudge
{
    /// <summary>The name an opaque runner's one check carries: the declaration's key for it.</summary>
    public const string RunnerGate = "verification";

    /// <summary>
    /// A runner's answer by file, read gate by gate (<see cref="RunnerAnswers.Read(string?, RunnerAsk)"/>, schema 1): failed where any gate
    /// failed, reached where every gate <paramref name="required"/> passed or flaked, unknown otherwise; busy by its answer or its exit 75.
    /// </summary>
    public static VerifyJudged ByFile(
        string invocation, string commit, int exit, string? answer, IReadOnlyList<string> declared, IReadOnlyList<string> required, string log)
    {
        if (exit == RunnerInputs.BusyExit) return VerifyJudged.WasBusy;
        var read = RunnerAnswers.Read(answer, new RunnerAsk(commit, declared));
        if (read.Kind == RunnerKind.Busy) return VerifyJudged.WasBusy;
        if (read.Kind != RunnerKind.Gates) return Unknown(invocation, commit, read.Why ?? "the runner's answer does not read", [new VerifyGate(RunnerGate, RunnerVerdicts.Ended, exit) { Log = log }]);

        var gates = read.Gates.Select(gate => new VerifyGate(gate.Name, gate.Verdict, gate.Exit) { WallSeconds = gate.WallSeconds, Log = gate.Log ?? log }).ToList();
        var standing = RunnerAnswers.Standing(read.Gates, required);
        return standing.Kind switch
        {
            RunnerStandingKind.Reached => new VerifyJudged(new VerifyResult(invocation, commit, VerifyVerdicts.Reached, Flaked(standing.Flaked), gates), false),
            RunnerStandingKind.Failed => new VerifyJudged(
                new VerifyResult(invocation, commit, VerifyVerdicts.Failed, standing.Why, gates) { Failed = gates.First(gate => gate.Verdict == RunnerVerdicts.Fail).Name }, false),
            _ => Unknown(invocation, commit, standing.Why ?? "the runner's answer does not show the gates", gates),
        };
    }

    /// <summary>An opaque runner, one check by its exit (the family's): 0 reached, 1 failed naming the runner, 75 busy, anything else unknown.</summary>
    public static VerifyJudged Opaque(string invocation, string commit, int exit, string log) => exit switch
    {
        0 => new(new VerifyResult(invocation, commit, VerifyVerdicts.Reached, null, [new VerifyGate(RunnerGate, RunnerVerdicts.Pass, 0) { Log = log }]), false),
        1 => new(new VerifyResult(invocation, commit, VerifyVerdicts.Failed, $"the runner failed with exit 1", [new VerifyGate(RunnerGate, RunnerVerdicts.Fail, 1) { Log = log }])
        {
            Failed = RunnerGate,
        }, false),
        RunnerInputs.BusyExit => VerifyJudged.WasBusy,
        _ => Unknown(invocation, commit, $"the runner exited {exit.ToString(CultureInfo.InvariantCulture)}, which is neither a pass nor a failure",
            [new VerifyGate(RunnerGate, RunnerVerdicts.Ended, exit) { Log = log }]),
    };

    private static VerifyJudged Unknown(string invocation, string commit, string why, IReadOnlyList<VerifyGate> gates) =>
        new(new VerifyResult(invocation, commit, VerifyVerdicts.Unknown, why, gates), false);

    private static string? Flaked(IReadOnlyList<string> flaked) =>
        flaked.Count == 0 ? null : $"passed on a second run alone: {string.Join(", ", flaked)}";
}
