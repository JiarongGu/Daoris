using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>The bound a stop is held to, and what it rests on: the measured stops it reads and the words that say so.</summary>
/// <param name="Bound">How long the check and the launcher's start may take together.</param>
/// <param name="Samples">How many measured stops the machine has kept (all of them, not only the recent ones read).</param>
/// <param name="Basis">What the bound rests on, in a clause the refusal's sentence carries.</param>
public sealed record StopBound(TimeSpan Bound, int Samples, string Basis);

/// <summary>
/// How long the staged build's check may take on this machine: learned from the stops it measured. The check copies the home
/// and starts the staged build, so its time is the machine's load as much as the build's: a fixed bound refuses a check that load
/// stretches past it, which the retry then passes. The bound is <see cref="Margin"/> times the longest
/// of the last <see cref="Recent"/> stops kept, never under <see cref="Floor"/> (what stands with no history, and under which no bound goes) nor over
/// <see cref="Ceiling"/> (a check that crept up every time would otherwise earn a longer bound each time and never be refused). A stop
/// that hung is refused and not kept, so a hang never teaches the bound.
/// </summary>
public static class StopBounds
{
    /// <summary>What stands before anything is measured, and the least a bound is.</summary>
    public static readonly TimeSpan Floor = TimeSpan.FromMinutes(3);

    /// <summary>The most a bound grows to, however long the stops it measured took.</summary>
    public static readonly TimeSpan Ceiling = TimeSpan.FromMinutes(10);

    /// <summary>How many times the longest recent stop a bound allows: the next check may meet the load of the slowest, and more.</summary>
    public const double Margin = 2.0;

    /// <summary>How many of the newest stops the bound reads, so a machine that got faster is held to a shorter bound again within a few updates.</summary>
    public const int Recent = 5;

    /// <summary>Where the stops are kept: the same history the gates' needs are (<see cref="LearnedNeeds"/>), under a key of the install's own.</summary>
    public static readonly string Key = LearnedNeeds.KeyOf("install", "update", "stop");

    /// <summary>The bound the stops kept in <paramref name="home"/> say, over <paramref name="floor"/>.</summary>
    public static StopBound Read(string home, TimeSpan floor) =>
        Of([.. LearnedNeeds.Samples(home, Key).Where(sample => !sample.TimedOut).OrderBy(sample => sample.At).Select(sample => sample.Seconds)], floor);

    /// <summary>The bound a history says, <paramref name="seconds"/> being each measured stop, oldest first.</summary>
    public static StopBound Of(IReadOnlyList<double> seconds, TimeSpan floor)
    {
        if (seconds.Count == 0) return new StopBound(floor, 0, "the floor: no stop is measured on this machine yet");
        var longest = seconds.TakeLast(Recent).Max();
        var wanted = TimeSpan.FromSeconds(Math.Ceiling(longest * Margin));
        var shown = (int)Math.Round(longest);
        if (wanted < floor) return new StopBound(floor, seconds.Count, $"the floor: the longest of the last stops measured on this machine took {shown} s");
        if (wanted > Ceiling) return new StopBound(Ceiling, seconds.Count, $"the ceiling: the longest of the last stops measured on this machine took {shown} s");
        return new StopBound(wanted, seconds.Count, $"a bound learned from this machine's last stops: twice the longest, {shown} s");
    }

    /// <summary>A stop that ended on its own (applied, or refused by what the check found) kept as measured; a history that cannot be written teaches nothing and breaks nothing.</summary>
    public static void Keep(string home, DateTimeOffset at, TimeSpan took)
    {
        try
        {
            LearnedNeeds.Record(home, Key, new RunSample(at, 0, 0, Math.Max(0, Math.Round(took.TotalSeconds, 1)), false));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // The next stop is held to the bound this history already says.
        }
    }
}
