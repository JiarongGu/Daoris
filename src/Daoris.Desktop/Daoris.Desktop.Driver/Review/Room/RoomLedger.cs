using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>The machine's seams the room reads: the clock, the processors' time and Daoris's own share of the memory (by home). This machine's unless a test holds them.</summary>
public sealed record RoomWorld(Func<DateTimeOffset> Clock, Func<CpuTicks?> Cpu, Func<string, long?> Daoris)
{
    /// <summary>This machine's.</summary>
    public static RoomWorld Machine { get; } = new(AdmissionWaiting.Default.Clock, MachineCpu.Read, DaorisShare.Read);
}

/// <summary>What the room keeps between two looks, shared by every process on the machine: the samples, the limit, the processors' last reading and who asked lately.</summary>
internal sealed record RoomState(IReadOnlyList<RoomSample> Samples, RoomLimit Limit, CpuTicks? Cpu, IReadOnlyDictionary<string, DateTimeOffset> Asks)
{
    public static RoomState Empty(int ceiling) => new([], RoomLimit.Initial(ceiling), null, new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal));
}

/// <summary>
/// The room's state on disk (<c>admission/room.json</c> of the home) and the look that updates it. One look, made under the
/// admission's lock, takes a sample where one is due, reads the pressure signals, moves the limit and notes who asked, then hands the
/// pure judgement (<see cref="RoomJudge"/>) its view. Every waiting run looks again every half second or two, so the samples are
/// <see cref="PersonMeasure.SampleEvery"/> apart however many ask.
/// </summary>
internal static class RoomLedger
{
    /// <summary>How long an ask is remembered: an hour, far past <see cref="RoomJudge.Fair"/>, so the file stays small.</summary>
    private static readonly TimeSpan AskKept = TimeSpan.FromHours(1);

    public static string PathOf(string home) => DaorisHome.Entry(home, HomeEntry.Locks, "admission", "room.json");

    /// <summary>The view now and the state it rests on, the state written: for the admission's judgement, which holds the admission's lock.</summary>
    public static RoomView Observe(string home, CheckRules rules, string? project, string? agent, int ceiling)
    {
        var world = rules.World;
        var now = world.Clock();
        var state = Read(home) ?? RoomState.Empty(ceiling);
        var reading = rules.Memory();
        var samples = state.Samples;
        var cpu = state.Cpu;
        if (samples.Count == 0 || now - samples[^1].At >= PersonMeasure.SampleEvery)
        {
            var reached = world.Cpu();
            var busy = MachineCpu.Between(cpu, reached);
            cpu = reached ?? cpu;
            if (reading is { Total: { } total, Physical: { } free } && world.Daoris(home) is { } share)
                samples = PersonMeasure.Add(samples, new RoomSample(now, Math.Max(0, total - free), share, busy), now);
            else samples = PersonMeasure.Add(samples, null, now);
        }

        var floor = rules.FloorOf();
        var view = ViewOf(state with { Samples = samples }, reading, floor, now);
        var signal = SignalOf(view, LearnedNeeds.LastTimeout(home), state.Limit);
        var limit = RoomPressure.Next(state.Limit, signal, ceiling, now);
        var asks = new Dictionary<string, DateTimeOffset>(state.Asks.Where(pair => now - pair.Value <= AskKept), StringComparer.Ordinal);
        if (project is not null) asks[project] = now;
        if (project is not null && agent is not null) asks[$"{project}|{agent}"] = now;
        var next = new RoomState(samples, limit, cpu, asks);
        Write(home, next);
        return ViewOf(next, reading, floor, now);
    }

    /// <summary>The view a state and a memory reading give at <paramref name="now"/>: no file read or written.</summary>
    public static RoomView ViewOf(RoomState state, MemoryReading? reading, long floor, DateTimeOffset now) => new(
        reading?.Least, reading?.Commit ?? reading?.Physical, floor, PersonMeasure.Person(state.Samples, now, reading?.Total), state.Limit.Limit,
        state.Limit.Signal, PersonMeasure.Busy(state.Samples, now), state.Asks, now);

    /// <summary>The pressure the view shows, by the signal's name, or null: memory under what is kept, the processors saturated, a run's tests timed out since the last pressure.</summary>
    internal static string? SignalOf(RoomView view, DateTimeOffset? lastTimeout, RoomLimit limit) =>
        view.Basis is { } basis && view.Keep > 0 && basis < view.Keep ? RoomLimit.Memory
        : view.Busy >= PersonMeasure.Saturated ? RoomLimit.Cpu
        : lastTimeout > limit.PressureAt ? RoomLimit.Timeouts
        : null;

    /// <summary>The state kept, or null where there is none or it does not read.</summary>
    public static RoomState? Read(string home)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(PathOf(home)));
            var root = document.RootElement;
            var limit = root.GetProperty("limit");
            return new RoomState(
                [.. root.GetProperty("samples").EnumerateArray().Select(Sample)],
                new RoomLimit(limit.GetProperty("limit").GetInt32(), At(limit, "changedAt"), At(limit, "pressureAt"), limit.GetProperty("signal").GetString() ?? RoomLimit.Start),
                root.TryGetProperty("cpu", out var cpu) ? new CpuTicks(cpu.GetProperty("busy").GetInt64(), cpu.GetProperty("total").GetInt64()) : null,
                root.GetProperty("asks").EnumerateObject().ToDictionary(pair => pair.Name, pair => DateTimeOffset.Parse(pair.Value.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal), StringComparer.Ordinal));
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    private static DateTimeOffset At(JsonElement from, string name) =>
        DateTimeOffset.Parse(from.GetProperty(name).GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

    private static RoomSample Sample(JsonElement raw) =>
        new(At(raw, "at"), raw.GetProperty("used").GetInt64(), raw.GetProperty("daoris").GetInt64(), raw.TryGetProperty("busy", out var busy) ? busy.GetDouble() : null);

    private static void Write(string home, RoomState state)
    {
        var path = PathOf(home);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schema", 1);
            writer.WriteStartObject("limit");
            writer.WriteNumber("limit", state.Limit.Limit);
            writer.WriteString("changedAt", MachineAdmission.Stamp(state.Limit.ChangedAt));
            writer.WriteString("pressureAt", MachineAdmission.Stamp(state.Limit.PressureAt));
            writer.WriteString("signal", state.Limit.Signal);
            writer.WriteEndObject();
            if (state.Cpu is { } cpu)
            {
                writer.WriteStartObject("cpu");
                writer.WriteNumber("busy", cpu.Busy);
                writer.WriteNumber("total", cpu.Total);
                writer.WriteEndObject();
            }

            writer.WriteStartArray("samples");
            foreach (var sample in state.Samples)
            {
                writer.WriteStartObject();
                writer.WriteString("at", MachineAdmission.Stamp(sample.At));
                writer.WriteNumber("used", sample.Used);
                writer.WriteNumber("daoris", sample.Daoris);
                if (sample.Busy is { } busy) writer.WriteNumber("busy", Math.Round(busy, 3));
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteStartObject("asks");
            foreach (var (key, at) in state.Asks.OrderBy(pair => pair.Key, StringComparer.Ordinal)) writer.WriteString(key, MachineAdmission.Stamp(at));
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        AtomicFile.WriteText(path, Encoding.UTF8.GetString(buffer.ToArray()).ReplaceLineEndings("\n") + "\n");
    }



}
