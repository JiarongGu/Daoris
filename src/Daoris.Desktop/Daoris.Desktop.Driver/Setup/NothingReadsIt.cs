namespace Daoris.Driver;

/// <summary>
/// The reading behind <see cref="ISetupWorld.NothingReadsItAsync"/>: a repository whose every ref holds the creation's one commit on its
/// line, and which no session record names.
/// </summary>
public static class NothingReadsIt
{
    public static async Task<bool> JudgeAsync(RepoView repository, string line, IReadOnlyList<SessionRun> runs, CancellationToken ct)
    {
        if (repository.Root is null || !BranchName.IsValid(line)) return false;
        if (runs.Any(run => string.Equals(run.Repository, repository.Repository, StringComparison.OrdinalIgnoreCase))) return false;

        var (allCode, all, _) = await WorkingTree.GitAsync(repository.Root, ["rev-list", "--count", "--all"], ct).ConfigureAwait(false);
        var (lineCode, onLine, _) = await WorkingTree.GitAsync(repository.Root, ["rev-list", "--count", $"refs/heads/{line}"], ct).ConfigureAwait(false);
        return allCode == 0 && lineCode == 0 && all.Trim() == "1" && onLine.Trim() == "1";
    }
}
