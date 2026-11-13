namespace Daoris.Driver;

/// <summary>Where an ask's branch stands against its repository's line, as one read of a pass found it.</summary>
/// <param name="Ask">The ask the run delivers.</param>
/// <param name="Line">The repository's line; null where none is set or guessed.</param>
/// <param name="Base">The commit the ask's branch and the line share, which its review compares from; null where the branch is not there yet.</param>
public sealed record AskBranchRead(string Ask, string? Line, string? Base);

/// <summary>The one git read a pass makes for the open delivery runs of asks: their branches against the line.</summary>
public static class AskRunReads
{
    /// <summary>Each open ask run's branch read, by run; a run ended, or whose repository has no checkout here, is left out.</summary>
    public static async Task<IReadOnlyDictionary<string, AskBranchRead>> ReadAsync(
        string home, IReadOnlyDictionary<string, string> roots, DriverConfig config, CancellationToken ct)
    {
        var read = new Dictionary<string, AskBranchRead>(StringComparer.Ordinal);
        var folder = WorkflowRunBindings.FolderOf(home);
        if (!Directory.Exists(folder)) return read;
        var records = new NodeRecordFiles(home);
        foreach (var file in Directory.GetFiles(folder, $"{AskRunKey.Prefix}*.json"))
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

            if (binding is null || !AskRunKey.Is(binding) || records.Read(RunUnits.IdOf(binding.Run))?.Ended is not null) continue;
            if (!roots.TryGetValue(binding.Repository, out var root)) continue;
            var line = (await CanonicalLine.ResolveAsync(root, binding.Repository, binding.Workspace, config, ct).ConfigureAwait(false)).Branch;
            var (code, shared, _) = line is null
                ? (1, "", "")
                : await WorkingTree.GitAsync(root, ["merge-base", $"refs/heads/{line}", $"refs/heads/{TrainBranch.Of(binding.Ask!)}"], ct).ConfigureAwait(false);
            read[binding.Run] = new AskBranchRead(binding.Ask!, line, code == 0 ? shared.Trim() : null);
        }

        return read;
    }
}
