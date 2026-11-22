using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The look's half of rows that touch the same files: what each running unit of this machine has
/// changed since its own merge base (<see cref="BaseAsync"/>), for the planner's comparison at a start (<see cref="Snapshot.Changed"/>), and the observed overlap of two running
/// units of one repository, each told once as an ordinary word.
/// </summary>
/// <remarks>
/// A running session of this machine is read where its tree moved since the last look (<see cref="TreeChangesKept"/>), and the pairs are
/// compared in memory; a teammate's session is read by its own machine. The told words' keys are kept in <c>overlaps-told.json</c> beside the driver's state, so a restart tells nothing twice.
/// </remarks>
public static class WorkOverlaps
{
    /// <summary>The paths a tree changed since <c>before</c>, committed or not, or null where git cannot say; <c>line</c> is the line the person set, null to ask git's guess.</summary>
    public delegate Task<IReadOnlyList<string>?> Diff(string tree, string before, string? line, CancellationToken ct);

    /// <summary>Says a word to a running session under its key.</summary>
    public delegate Task Say(string session, string text, string key, CancellationToken ct);

    private static string ToldPath(string home) => DaorisHome.Entry(home, HomeEntry.OverlapsTold);

    /// <summary>The snapshot with each running unit's changed paths and the quests whose integration request is open; <paramref name="lineOf"/> says the line set for a session's repository.</summary>
    public static async Task<Snapshot> LookAsync(
        Snapshot snapshot, IReadOnlySet<string> integrating, Diff diff, CancellationToken ct = default, Func<SessionView, string?>? lineOf = null)
    {
        var changed = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var session in Own(snapshot))
        {
            if (changed.ContainsKey(session.Quest!)) continue;
            if (await diff(session.Tree!, session.BaseCommit!, lineOf?.Invoke(session), ct).ConfigureAwait(false) is { } paths) changed[session.Quest!] = paths;
        }

        return snapshot with { Changed = changed, Integrating = integrating };
    }

    /// <summary>git's names of the paths <c>before</c> differs from the tree in, where the tree is its own checkout's top level.</summary>
    public static async Task<IReadOnlyList<string>?> DiffAsync(string tree, string before, string? line, CancellationToken ct) =>
        !WorkingTree.IsCommitId(before) || !await WorkingTree.IsTopLevelAsync(tree, ct).ConfigureAwait(false) ? null : await NamesAsync(tree, before, line, ct).ConfigureAwait(false);

    /// <summary>git's names of the paths the tree changed since every one of <see cref="BasesAsync"/>, the tree known to be its checkout's top level.</summary>
    internal static async Task<IReadOnlyList<string>?> NamesAsync(string tree, string before, string? line, CancellationToken ct)
    {
        IReadOnlyList<string>? kept = null;
        foreach (var from in await BasesAsync(tree, before, line, ct).ConfigureAwait(false))
        {
            var (code, stdout, _) = await WorkingTree.GitAsync(tree, ["--no-optional-locks", "diff", "--name-only", from], ct).ConfigureAwait(false);
            if (code != 0) return null;
            var names = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            kept = kept is null ? [.. names.Order(StringComparer.Ordinal)] : [.. kept.Intersect(names, StringComparer.Ordinal)];
        }

        return kept;
    }

    /// <summary>The commits a tree's own changes are counted from: <paramref name="before"/> and its merge bases with the line's local and remote refs, those no other leads on to.</summary>
    internal static async Task<IReadOnlyList<string>> BasesAsync(string tree, string before, string? line, CancellationToken ct)
    {
        var name = line is { Length: > 0 } ? line : await LineGuesses.Shared.GuessAsync(tree, ct).ConfigureAwait(false);
        if (name is not { Length: > 0 }) return [before];
        name = name.StartsWith("origin/", StringComparison.Ordinal) ? name["origin/".Length..] : name;
        var bases = new List<string> { before };
        foreach (var fork in new[] { $"refs/heads/{name}", $"refs/remotes/origin/{name}" })
        {
            var (code, shared, _) = await WorkingTree.GitAsync(tree, ["--no-optional-locks", "merge-base", "HEAD", fork], ct).ConfigureAwait(false);
            if (code == 0 && WorkingTree.IsCommitId(shared.Trim()) && !bases.Contains(shared.Trim(), StringComparer.Ordinal)) bases.Add(shared.Trim());
        }

        if (bases.Count == 1) return bases;
        var (independent, names, _) = await WorkingTree.GitAsync(tree, ["--no-optional-locks", "merge-base", "--independent", .. bases], ct).ConfigureAwait(false);
        return independent == 0 && names.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) is { Length: > 0 } latest
            ? latest
            : bases;
    }

    /// <summary>
    /// Tells each of two running units of one repository whose changes share a path, once for that pair: the other quest and the shared paths,
    /// so whichever lands second knows it meets the other's work. Keys of sessions that stopped running are let go.
    /// </summary>
    public static async Task TellAsync(string home, Snapshot snapshot, Say say, CancellationToken ct = default)
    {
        var own = Own(snapshot).ToList();
        var running = own.Where(s => snapshot.Changed.ContainsKey(s.Quest!)).ToList();
        var told = Told(home);

        var kept = told.Where(key => own.Any(s => key.StartsWith($"overlap/{s.Id}/", StringComparison.Ordinal))).ToHashSet(StringComparer.Ordinal);
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
