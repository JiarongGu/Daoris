using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The press's runs, per home, in this process (KEEP1e): one at a time, under the home's run lock, tried and never waited for (the cleanup
/// design §3.1). A press while a run is under way here joins it; one while another process, or the look's pass, holds the lock is refused
/// naming its holder. The last run is kept, so a page opened again reads the same run.
/// </summary>
public sealed class SpaceRuns
{
    /// <summary>This process's.</summary>
    public static SpaceRuns Shared { get; } = new();

    private readonly object _gate = new();
    private readonly Dictionary<string, SpaceRun> _last = new(StringComparer.Ordinal);

    /// <summary>The run under way under <paramref name="home"/>, or the last that ended; null where this process ran none.</summary>
    public SpaceRun? Last(string home)
    {
        lock (_gate) return _last.TryGetValue(Key(home), out var run) ? run : null;
    }

    /// <summary>
    /// Start a run of <paramref name="asked"/> through <paramref name="door"/>, in the background: the run lock taken here and held to its
    /// end. <paramref name="told"/> hears each kind's end and the whole's.
    /// </summary>
    public SpaceStart Start(
        SpaceFreeWorld world, string door, IReadOnlyList<SpaceAsked> asked, Action<SpaceRun>? told = null, CancellationToken ct = default)
    {
        var key = Key(world.Home);
        lock (_gate)
        {
            if (_last.TryGetValue(key, out var under) && under.Outcome is null) return new(under, true, null);
            var held = KeepingRun.TryTake(world.Home, door);
            if (held is null) return new(null, false, KeepingRun.Holder(world.Home));
            var kinds = SpaceFree.Kinds.Where(kind => asked.Any(unit => unit.Kind == kind));
            var run = new SpaceRun(held.Id, door, world.Now(), kinds);
            if (told is not null) run.KindEnded += _ => told(run);
            _last[key] = run;
            _ = Task.Run(async () =>
            {
                try
                {
                    await SpaceFree.RunAsync(world, asked, run, ct).ConfigureAwait(false);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException or OperationCanceledException
                                                  or JsonException or InvalidOperationException)
                {
                    // A read that failed mid-run stops it: what it removed is said in its kinds done, and the rest stays.
                    run.Stopped();
                }
                finally
                {
                    held.Dispose();
                    run.Stopped();
                    run.Release();
                    try
                    {
                        told?.Invoke(run);
                    }
                    catch (Exception error) when (error is not OutOfMemoryException)
                    {
                        // Best-effort, as each kind's.
                    }
                }
            }, CancellationToken.None);
            return new(run, false, null);
        }
    }

    private static string Key(string home)
    {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(home));
        return OperatingSystem.IsWindows() ? full.ToUpperInvariant() : full;
    }
}
