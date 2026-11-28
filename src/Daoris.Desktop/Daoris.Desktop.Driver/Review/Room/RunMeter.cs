using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// What each gate of a run took, read from its job as the gate ends and kept under the repository, kind and that one gate: the job's
/// peak memory so far (never less than the gate took) and the cores the gate kept busy. Off Windows there is no job and nothing is learned.
/// </summary>
internal sealed partial class RunMeter(string home, string key, string kind, Func<DateTimeOffset> clock)
{
    private TimeSpan _cpu = TimeSpan.Zero;

    /// <summary>Said by a failed gate's own output when a test hit its time bound: the runners' words for it.</summary>
    [GeneratedRegex(@"\bTest timed out in \d+\s?ms\b|\btimeout of \d+\s?ms exceeded\b|\bExceeded timeout of \d+\s?ms\b", RegexOptions.IgnoreCase)]
    private static partial Regex TimeoutWords();

    /// <summary>Whether a failed gate's output says a test timed out: only a gate that failed counts, so a passing run's words never back the machine off.</summary>
    public static bool TimedOut(bool failed, string? output) => failed && output is { Length: > 0 } && TimeoutWords().IsMatch(output);

    /// <summary>The last 256 KB of a gate's log, where the run keeps one, else null: where its timeout words would be.</summary>
    public static string? LogTail(string? path)
    {
        try
        {
            if (path is null || !File.Exists(path)) return null;
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            stream.Seek(Math.Max(0, stream.Length - (256 << 10)), SeekOrigin.Begin);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Note(string gate, ProcessJob? job, TimeSpan took, bool whole, bool failed, string? output) =>
        Note(gate, job?.PeakMemory, job?.Cpu, took, whole, failed, output);

    /// <summary>
    /// Keep <paramref name="gate"/>'s run: the job's <paramref name="peak"/> and cumulative <paramref name="cpu"/> as it ended, and its wall
    /// time <paramref name="took"/>. Only a <paramref name="whole"/> gate (it ran to its end) teaches a need, since a cut-off, busy or
    /// partly kept one peaked short of one; a failed gate is kept only for the timeouts its words show, which the limit backs off on.
    /// </summary>
    internal void Note(string gate, long? peak, TimeSpan? cpu, TimeSpan took, bool whole, bool failed, string? output)
    {
        var busy = cpu is { } now ? now - _cpu : (TimeSpan?)null;
        if (cpu is { } total) _cpu = total;
        var timedOut = TimedOut(failed, output);
        if ((!whole && !timedOut) || peak is not { } held || held <= 0 || took <= TimeSpan.Zero) return;
        var cores = busy is { } spent ? spent.TotalSeconds / took.TotalSeconds : 0;
        try
        {
            LearnedNeeds.Record(home, LearnedNeeds.KeyOf(key, kind, gate), new RunSample(clock(), held, Math.Round(cores, 1), Math.Round(took.TotalSeconds, 1), timedOut));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or TimeoutException)
        {
            // A run that cannot say what it took has taught the room nothing; the next one will.
        }
    }
}
