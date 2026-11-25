using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// What a run's gates took, read from its job as it ends and kept under its repository, kind and gate (<see cref="LearnedNeeds"/>): the
/// job's peak memory, the cores it kept busy (its CPU time over its wall time) and whether a test of it timed out beside the load.
/// Where there is no job (off Windows) the run learns nothing and its declared cost stands.
/// </summary>
internal sealed partial record RunMeter(string Home, string Key, string Kind, string Gate, Func<DateTimeOffset> Clock)
{
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

    /// <summary>
    /// Keep the run: its peak memory and cores from <paramref name="job"/> (before it is ended), its wall time <paramref name="took"/>. Only a
    /// <paramref name="whole"/> run (every gate ran to its end) teaches a need, since a cut-off, busy or partly kept run peaked short of one;
    /// a failed run is kept only for the timeouts its words show, which the limit backs off on.
    /// </summary>
    public void Note(ProcessJob? job, TimeSpan took, bool whole, bool failed, string? output)
    {
        var timedOut = TimedOut(failed, output);
        if ((!whole && !timedOut) || job?.PeakMemory is not { } peak || peak <= 0 || took <= TimeSpan.Zero) return;
        var cores = job.Cpu is { } cpu ? cpu.TotalSeconds / took.TotalSeconds : 0;
        try
        {
            LearnedNeeds.Record(Home, LearnedNeeds.KeyOf(Key, Kind, Gate), new RunSample(Clock(), peak, Math.Round(cores, 1), Math.Round(took.TotalSeconds, 1), timedOut));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or TimeoutException)
        {
            // A run that cannot say what it took has taught the room nothing; the next one will.
        }
    }
}
