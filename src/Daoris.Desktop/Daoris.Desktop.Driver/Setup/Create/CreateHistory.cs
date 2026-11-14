namespace Daoris.Driver;

/// <summary>What a creation's folder holds, read as it stands.</summary>
public enum CreateFolderState
{
    /// <summary>No folder there.</summary>
    Missing,

    /// <summary>A folder with nothing in it.</summary>
    Empty,

    /// <summary>A repository with no commit yet: a <c>git init</c> stopped before its commit.</summary>
    Partial,

    /// <summary>A repository holding exactly one commit with no parent and no file: the empty commit a creation makes.</summary>
    Fresh,

    /// <summary>Anything else: not a creation's to take.</summary>
    Other,
}

/// <summary>A creation's folder and, where it is the empty commit, that commit's id.</summary>
public sealed record CreateFolderReading(CreateFolderState State, string? Commit);

/// <summary>
/// The history a creation makes through the repository port: an init on the line, one empty commit as the author, no remote and no push,
/// and the reading of a folder that says whether it is the folder a receipt made. Nothing is deleted, whatever is read.
/// </summary>
public static class CreateHistory
{
    /// <summary>Whether git answers: its version, or false where it did not start.</summary>
    public static Task<bool> AvailableAsync(CancellationToken ct) => MachinePorts.Repository.AvailableAsync(ct);

    /// <summary>What <paramref name="folder"/> holds, read by git where it holds a repository.</summary>
    public static async Task<CreateFolderReading> ReadAsync(string folder, CancellationToken ct)
    {
        if (!Directory.Exists(folder)) return new(CreateFolderState.Missing, null);
        var entries = Directory.EnumerateFileSystemEntries(folder).Select(Path.GetFileName).ToList();
        if (entries.Count == 0) return new(CreateFolderState.Empty, null);
        if (entries is not [".git"] || !Directory.Exists(Path.Combine(folder, ".git"))) return new(CreateFolderState.Other, null);

        var repository = MachinePorts.Repository;
        if (await repository.RefsAsync(folder, ["refs"], null, null, ct).ConfigureAwait(false) is not { } refs) return new(CreateFolderState.Other, null);
        if (refs.Count == 0) return new(CreateFolderState.Partial, null);

        var head = await repository.HeadAsync(folder, ct).ConfigureAwait(false);
        var files = await repository.EntriesAsync(folder, "HEAD", [], true, ct).ConfigureAwait(false);
        var onLine = await repository.CountAsync(folder, "HEAD", [], ct).ConfigureAwait(false);
        return head is not null && files is { Count: 0 } && onLine == 1 && refs.All(each => each.Commit == head)
            ? new(CreateFolderState.Fresh, head)
            : new(CreateFolderState.Other, null);
    }

    /// <summary>
    /// The folder made, initialised, its head pointed at <paramref name="line"/>, and one empty commit as <paramref name="author"/> with
    /// <paramref name="message"/>. Hooks and signing are left out of that one commit, so the folder's own state is all it asks of git.
    /// </summary>
    /// <returns>The commit's id, or git's words for why there is none.</returns>
    public static async Task<(string? Commit, string? Problem)> InitAsync(string folder, string line, CreateAuthor author, string message, CancellationToken ct)
    {
        var repository = MachinePorts.Repository;
        var init = await repository.InitAsync(folder, line, ct).ConfigureAwait(false);
        if (!init.Ok) return (null, $"git init said: {Said(init.Error)}");
        var commit = await repository.CommitEmptyAsync(folder, author.Name, author.Email, message, ct).ConfigureAwait(false);
        if (!commit.Ok) return (null, $"git commit said: {Said(commit.Error)}");
        return await repository.HeadAsync(folder, ct).ConfigureAwait(false) is { } head ? (head, null) : (null, "git rev-parse said: nothing");
    }

    private static string Said(string text) => text.Trim().Length == 0 ? "nothing" : text.Trim().ReplaceLineEndings(" ");
}
