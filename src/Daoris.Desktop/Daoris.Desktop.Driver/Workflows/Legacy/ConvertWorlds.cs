namespace Daoris.Driver;

/// <summary>The conversion's reads on this machine: the file, the registry, the saved workflows and the bound runs with the state of their units.</summary>
public static class ConvertWorlds
{
    /// <summary>The world now, through the service's registry and quests and the home's files.</summary>
    public static async Task<ConvertWorld> ReadAsync(
        Func<CancellationToken, Task<IReadOnlyList<RepoView>>> registry, Func<CancellationToken, Task<IReadOnlyList<QuestView>>> quests, DriverConfig config, string home, CancellationToken ct)
    {
        var rows = await registry(ct).ConfigureAwait(false);
        var every = await quests(ct).ConfigureAwait(false);
        return new(config, home, rows, WorkflowPorts.Store(home), RunsOf(home, every));
    }

    private static string Bare(string id) => id.Trim().TrimStart('#');

    /// <summary>Every run bound here that reads, with whether a quest of its chain (its first, or one it published) is still <c>Open</c> or <c>Taken</c>.</summary>
    public static IReadOnlyList<LegacyRun> RunsOf(string home, IReadOnlyList<QuestView> quests)
    {
        var folder = WorkflowRunBindings.FolderOf(home);
        if (!Directory.Exists(folder)) return [];
        var runs = new List<LegacyRun>();
        var store = WorkflowPorts.Runs(home);
        foreach (var file in Directory.EnumerateFiles(folder, "*.json").Order(StringComparer.Ordinal))
        {
            if (store.Read(Path.GetFileNameWithoutExtension(file)) is not { } binding) continue;
            var chain = quests.Where(quest => Bare(quest.Id) == Bare(binding.Run) || (quest.Parent is { } parent && Bare(parent) == Bare(binding.Run))).ToList();
            var open = chain.FirstOrDefault(quest => quest.Status is "Open" or "Taken");
            runs.Add(new(binding, open is not null, (open ?? chain.FirstOrDefault())?.Id ?? binding.Run));
        }

        return runs;
    }

    /// <summary>
    /// The places with a Current unit open now, as <see cref="WorkflowLegacy.Held"/> keys (a repository's and its workspace's): the run files read as
    /// they are, and a binding made since <paramref name="since"/> counted open, since its quest is not in the snapshot <paramref name="quests"/> holds.
    /// </summary>
    public static IReadOnlySet<string> OpenCurrentNow(string home, IReadOnlyList<QuestView> quests, DateTimeOffset since)
    {
        var open = new HashSet<string>(StringComparer.Ordinal);
        foreach (var run in RunsOf(home, quests).Where(each => each.Binding.IsCurrent && (each.Open || each.Binding.At >= since)))
        {
            open.Add("repository:" + run.Binding.Repository.ToLowerInvariant());
            open.Add("workspace:" + RemoteTarget.Workspace(run.Binding.Workspace).ToLowerInvariant());
        }

        return open;
    }
}
