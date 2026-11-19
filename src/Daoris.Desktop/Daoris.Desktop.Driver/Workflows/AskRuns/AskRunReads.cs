namespace Daoris.Driver;

/// <summary>An ask's branch against its repository's line (null where none is set or guessed) and the commit they share, which its review compares from.</summary>
public sealed record AskBranchRead(string Ask, string? Line, string? Base);

/// <summary>Each open ask run's branch read by run, and the ask of every open run, whose plan a part's run reads its waits and joint checks from.</summary>
public sealed record AskRunsRead(IReadOnlyDictionary<string, AskBranchRead> Branches, IReadOnlyList<string> Asks);

/// <summary>The one read a pass makes of the runs for asks: every binding read once, and the open ask runs' branches against the line.</summary>
public static class AskRunReads
{
    /// <summary>Each open run's ask, and each open ask run's branch read; a run ended is left out, and a branch whose repository has no checkout here.</summary>
    public static async Task<AskRunsRead> ReadAsync(string home, IReadOnlyDictionary<string, string> roots, DriverConfig config, CancellationToken ct)
    {
        var read = new Dictionary<string, AskBranchRead>(StringComparer.Ordinal);
        var asks = new List<string>();
        var folder = WorkflowRunBindings.FolderOf(home);
        if (!Directory.Exists(folder)) return new AskRunsRead(read, asks);
        var records = new NodeRecordFiles(home);
        foreach (var file in Directory.GetFiles(folder, "*.json"))
        {
            WorkflowRunBinding? binding;
            try
            {
                binding = WorkflowRunFiles.Parse(AtomicFile.ReadText(file));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            if (binding is null || records.Read(RunUnits.IdOf(binding.Run))?.Ended is not null) continue;
            if (string.IsNullOrEmpty(binding.Ask)) continue;
            var ask = binding.Ask;
            var askRun = AskRunKey.Is(binding);
            if (askRun || GraphRun.Names(binding)) asks.Add(ask);
            if (!askRun || !roots.TryGetValue(binding.Repository, out var root)) continue;
            var line = (await CanonicalLine.ResolveAsync(root, binding.Repository, binding.Workspace, config, ct).ConfigureAwait(false)).Branch;
            var shared = line is null
                ? null
                : await MachinePorts.Repository.MergeBaseAsync(root, $"refs/heads/{line}", $"refs/heads/{TrainBranch.Of(ask)}", ct).ConfigureAwait(false);
            read[binding.Run] = new AskBranchRead(ask, line, shared);
        }

        return new AskRunsRead(read, [.. asks.Distinct(StringComparer.Ordinal)]);
    }
}
