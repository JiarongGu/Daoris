using System.Text.Json;
using System.Text.Json.Serialization;

namespace Daoris.Driver;

/// <summary>What one attempt of a run's <c>resolve</c> came to: <c>resolved</c> with the commit it finished the rebase at, or failed with why.</summary>
public sealed record ResolveAttempt(string Node, int Attempt, string Outcome, string Said)
{
    public string? Commit { get; init; }
}

/// <summary>The asks made of one stop, by its identity: how many, and the newest's words for the card.</summary>
public sealed record ResolveStopTries(string Node, string Stop, int Tries)
{
    /// <summary>The newest ask's wait, as the card and the run view say it; null once it ended.</summary>
    public string? Waits { get; init; }
}

/// <summary>A run's <c>resolve</c> record: its tries per stop, kept across rebinds and restarts, and what each attempt came to.</summary>
public sealed record ResolveRun
{
    public IReadOnlyList<ResolveStopTries> Stops { get; init; } = [];

    public IReadOnlyList<ResolveAttempt> Attempts { get; init; } = [];
}

/// <summary>
/// The door's record of each run's <c>resolve</c> nodes (<c>&lt;home&gt;/workflows/resolves/&lt;run&gt;.json</c>): each stop's count of asks, by the
/// stop's identity (<see cref="NodeStop"/>), so a stop met again after its rebase ran again goes on from its count, and each attempt's end,
/// which the step's reading reaches it from. The door writes, the runtime reads, and the run's settled end lets it go.
/// </summary>
public sealed class ResolveRecords(string home)
{
    private static readonly object Gate = new();

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    public static string FolderOf(string home) => DaorisHome.Entry(home, HomeEntry.Workflows, "resolves");

    /// <summary>A stop's identity as the record keys it.</summary>
    public static string KeyOf(NodeStop stop) => $"{stop.Sort}:{stop.PatchId}:{string.Join('\n', stop.Paths)}";

    private string PathOf(string run) => Path.Combine(FolderOf(home), $"{run.Replace(':', '-')}.json");

    /// <summary>The run's record; empty where none is written.</summary>
    public ResolveRun Of(string run)
    {
        var path = PathOf(run);
        return File.Exists(path) ? JsonSerializer.Deserialize<ResolveRun>(AtomicFile.ReadText(path), Options) ?? new() : new();
    }

    /// <summary>What <paramref name="attempt"/> of <paramref name="node"/> came to; null while it has not ended.</summary>
    public ResolveAttempt? Of(string run, string node, int attempt) => Of(run).Attempts.LastOrDefault(each => each.Node == node && each.Attempt == attempt);

    /// <summary>The asks <paramref name="node"/> made of <paramref name="stop"/> in this run, whichever attempt made them.</summary>
    public int Tries(string run, string node, NodeStop stop) => Of(run).Stops.FirstOrDefault(each => each.Node == node && each.Stop == KeyOf(stop))?.Tries ?? 0;

    /// <summary>The node's newest wait, said; null where it asks nothing now.</summary>
    public string? WaitOf(string run, string node) => Of(run).Stops.LastOrDefault(each => each.Node == node && each.Waits is not null)?.Waits;

    /// <summary>One more ask of <paramref name="stop"/> counted, its wait written, before it is made: the try it is.</summary>
    public int Ask(string run, string node, NodeStop stop, string waits) => Change(run, all =>
    {
        var key = KeyOf(stop);
        var tries = all.Stops.FirstOrDefault(each => each.Node == node && each.Stop == key)?.Tries ?? 0;
        var stops = all.Stops.Where(each => each.Node != node || each.Stop != key).Select(each => each.Node == node ? each with { Waits = null } : each);
        return (all with { Stops = [.. stops, new ResolveStopTries(node, key, tries + 1) { Waits = waits }] }, tries + 1);
    });

    /// <summary>The node's ask ended: its wait cleared, its count kept.</summary>
    public void Ended(string run, string node) =>
        Change(run, all => (all with { Stops = [.. all.Stops.Select(each => each.Node == node ? each with { Waits = null } : each)] }, 0));

    /// <summary>What the attempt came to, written once.</summary>
    public void Write(string run, ResolveAttempt made) => Change(run, all =>
        (all with { Attempts = [.. all.Attempts.Where(each => each.Node != made.Node || each.Attempt != made.Attempt), made] }, 0));

    /// <summary>The run's record let go of, at its settled end; nothing where none is written.</summary>
    public void Release(string run)
    {
        lock (Gate)
        {
            var path = PathOf(run);
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private T Change<T>(string run, Func<ResolveRun, (ResolveRun Next, T Result)> change)
    {
        lock (Gate)
        {
            var (next, result) = change(Of(run));
            Directory.CreateDirectory(FolderOf(home));
            var path = PathOf(run);
            AtomicFile.WriteText(path, JsonSerializer.Serialize(next, Options).ReplaceLineEndings("\n"));
            HomeWrites.Mine(path);
            return result;
        }
    }
}
