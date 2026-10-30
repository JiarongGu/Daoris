using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The look's half of rows that touch the same files (INTEGRATE1l, the integration design §9): what each running unit of this machine has
/// changed since it opened, for the planner's comparison at a start (<see cref="Snapshot.Changed"/>), and the observed overlap of two running
/// units of one repository, each told once as an ordinary word.
/// </summary>
/// <remarks>
/// One git read per running session of this machine, so the reads are bounded by its running sessions; a teammate's session is read by its
/// own machine. The told words' keys are kept in <c>overlaps-told.json</c> beside the driver's state, so a restart tells nothing twice.
/// </remarks>
public static class WorkOverlaps
{
    /// <summary>The paths a tree changed since <c>before</c>, committed or not, or null where git cannot say.</summary>
    public delegate Task<IReadOnlyList<string>?> Diff(string tree, string before, CancellationToken ct);

    /// <summary>Says a word to a running session under its key.</summary>
    public delegate Task Say(string session, string text, string key, CancellationToken ct);

    private static string ToldPath(string home) => DaorisHome.Entry(home, HomeEntry.OverlapsTold);

    /// <summary>The snapshot with each running unit's changed paths and the quests whose integration request is open.</summary>
    public static async Task<Snapshot> LookAsync(Snapshot snapshot, IReadOnlySet<string> integrating, Diff diff, CancellationToken ct = default)
    {
        var changed = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var session in Own(snapshot))
        {
            if (changed.ContainsKey(session.Quest!)) continue;
            if (await diff(session.Tree!, session.BaseCommit!, ct).ConfigureAwait(false) is { } paths) changed[session.Quest!] = paths;
        }

        return snapshot with { Changed = changed, Integrating = integrating };
    }

    /// <summary>git's names of the paths <c>before</c> differs from the tree in, where the tree is its own checkout's top level.</summary>
    public static async Task<IReadOnlyList<string>?> DiffAsync(string tree, string before, CancellationToken ct)
    {
        if (!WorkingTree.IsCommitId(before) || !await WorkingTree.IsTopLevelAsync(tree, ct).ConfigureAwait(false)) return null;
        var (code, stdout, _) = await WorkingTree.GitAsync(tree, ["diff", "--name-only", before], ct).ConfigureAwait(false);
        return code == 0
            ? [.. stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Order(StringComparer.Ordinal)]
            : null;
    }

    /// <summary>
    /// Tells each of two running units of one repository whose changes share a path, once for that pair: the other quest and the shared paths,
    /// so whichever lands second knows it meets the other's work. Keys of sessions no longer running are let go.
    /// </summary>
    public static async Task TellAsync(string home, Snapshot snapshot, Say say, CancellationToken ct = default)
    {
        var running = Own(snapshot).Where(s => snapshot.Changed.ContainsKey(s.Quest!)).ToList();
        var told = Told(home);
        var kept = told.Where(key => running.Any(s => key.StartsWith($"overlap/{s.Id}/", StringComparison.Ordinal))).ToHashSet(StringComparer.Ordinal);
        foreach (var one in running)
        {
            foreach (var other in running.Where(other => other.Quest != one.Quest
                                                         && string.Equals(other.Repository, one.Repository, StringComparison.OrdinalIgnoreCase)))
            {
                var shared = PathOverlap.Shared(snapshot.Changed[one.Quest!], snapshot.Changed[other.Quest!]);
                var key = $"overlap/{one.Id}/{other.Quest}";
                if (shared.Count == 0 || kept.Contains(key)) continue;
                await say(one.Id, Words(other.Quest!, shared), key, ct).ConfigureAwait(false);
                kept.Add(key);
            }
        }

        if (kept.SetEquals(told)) return;
        var path = ToldPath(home);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        AtomicFile.WriteText(path, JsonSerializer.Serialize(kept.Order(StringComparer.Ordinal)));
    }

    /// <summary>The ordinary word a running unit is told.</summary>
    public static string Words(string quest, IReadOnlyList<string> shared) =>
        $"Daoris: your work {new OverlapFact(quest, shared).Said} — quest `#{quest}`'s branch changes the same files, so whichever of you "
        + "is integrated second meets the other's work there. Keep to your own change, and say so if the two must be settled together.";

    private static IEnumerable<SessionView> Own(Snapshot snapshot) =>
        snapshot.Active.Where(s => !s.Id.Contains('/') && s is { Quest.Length: > 0, Tree.Length: > 0, BaseCommit.Length: > 0 });

    private static HashSet<string> Told(string home)
    {
        var path = ToldPath(home);
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<List<string>>(AtomicFile.ReadText(path))?.ToHashSet(StringComparer.Ordinal) ?? []
                : [];
        }
        catch (Exception error) when (error is JsonException or IOException)
        {
            return [];
        }
    }
}
