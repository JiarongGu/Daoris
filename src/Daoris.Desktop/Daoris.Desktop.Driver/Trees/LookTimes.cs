using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// How long each step of <see cref="SessionGroups.LookAsync"/> took (HELPROOM1): the records, the quests, the planner's verdicts,
/// the marks read from the home, and the git check of the trees to judge. Handed in by a caller that wants them said, in the
/// machine log's <c>help.room.look</c> line, so the step that made a look take 25 s on an installed machine is read, not guessed.
/// </summary>
public sealed class LookTimes
{
    private readonly object _gate = new();
    private readonly List<(string Step, long Ms)> _steps = [];

    /// <summary>The steps in the order they ran, each with its milliseconds; a step run twice is said twice, in order.</summary>
    public IReadOnlyList<(string Step, long Ms)> Steps
    {
        get
        {
            lock (_gate) return [.. _steps];
        }
    }

    /// <summary>How many trees the git check was asked about.</summary>
    public int Trees { get; set; }

    /// <summary>Run <paramref name="run"/>, saying how long it took as <paramref name="step"/>.</summary>
    public async Task<T> TimeAsync<T>(string step, Func<Task<T>> run)
    {
        var watch = Stopwatch.StartNew();
        try
        {
            return await run().ConfigureAwait(false);
        }
        finally
        {
            Add(step, watch.ElapsedMilliseconds);
        }
    }

    /// <summary>Run <paramref name="run"/>, saying how long it took as <paramref name="step"/>.</summary>
    public T Time<T>(string step, Func<T> run)
    {
        var watch = Stopwatch.StartNew();
        try
        {
            return run();
        }
        finally
        {
            Add(step, watch.ElapsedMilliseconds);
        }
    }

    private void Add(string step, long ms)
    {
        lock (_gate) _steps.Add((step, ms));
    }
}
