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
/// The history a creation makes with real git: <c>git init</c> on the line, one empty commit as the author, no remote and no push, and the
/// reading of a folder that says whether it is the folder a receipt made. Nothing is deleted, whatever is read.
/// </summary>
public static class CreateHistory
{
    /// <summary>Whether git answers: its version, or false where it did not start.</summary>
    public static async Task<bool> AvailableAsync(CancellationToken ct)
    {
        var (code, _, _) = await WorkingTree.GitAsync(AppContext.BaseDirectory, ["--version"], ct).ConfigureAwait(false);
        return code == 0;
    }

    /// <summary>What <paramref name="folder"/> holds, read by git where it holds a repository.</summary>
    public static async Task<CreateFolderReading> ReadAsync(string folder, CancellationToken ct)
    {
        if (!Directory.Exists(folder)) return new(CreateFolderState.Missing, null);
        var entries = Directory.EnumerateFileSystemEntries(folder).Select(Path.GetFileName).ToList();
        if (entries.Count == 0) return new(CreateFolderState.Empty, null);
        if (entries is not [".git"] || !Directory.Exists(Path.Combine(folder, ".git"))) return new(CreateFolderState.Other, null);

        var (code, count, _) = await WorkingTree.GitAsync(folder, ["rev-list", "--count", "--all"], ct).ConfigureAwait(false);
        if (code != 0) return new(CreateFolderState.Other, null);
        if (count.Trim() == "0") return new(CreateFolderState.Partial, null);

        var (headCode, head, _) = await WorkingTree.GitAsync(folder, ["rev-parse", "--verify", "HEAD"], ct).ConfigureAwait(false);
        var (listCode, files, _) = await WorkingTree.GitAsync(folder, ["ls-tree", "-r", "--name-only", "HEAD"], ct).ConfigureAwait(false);
        var (countCode, onLine, _) = await WorkingTree.GitAsync(folder, ["rev-list", "--count", "HEAD"], ct).ConfigureAwait(false);
        return headCode == 0 && listCode == 0 && countCode == 0 && count.Trim() == "1" && onLine.Trim() == "1" && files.Trim().Length == 0
            ? new(CreateFolderState.Fresh, head.Trim())
            : new(CreateFolderState.Other, null);
    }

    /// <summary>
    /// The folder made, <c>git init</c>, its head pointed at <paramref name="line"/>, and one empty commit as <paramref name="author"/> with
    /// <paramref name="message"/>. Hooks and signing are left out of that one commit, so the folder's own state is all it asks of git.
    /// </summary>
    /// <returns>The commit's id, or git's words for why there is none.</returns>
    public static async Task<(string? Commit, string? Problem)> InitAsync(string folder, string line, CreateAuthor author, string message, CancellationToken ct)
    {
        Directory.CreateDirectory(folder);
        (string Verb, string[] Arguments)[] steps =
        [
            ("init", ["init", "-q"]),
            ("symbolic-ref", ["symbolic-ref", "HEAD", $"refs/heads/{line}"]),
            ("commit", [.. GitAuthor.Arguments((author.Name, author.Email)), "-c", "commit.gpgsign=false", "commit", "--allow-empty", "--no-verify", "-q", "-m", message]),
        ];
        foreach (var (verb, arguments) in steps)
        {
            var (code, _, stderr) = await WorkingTree.GitAsync(folder, arguments, ct).ConfigureAwait(false);
            if (code != 0) return (null, $"git {verb} said: {Said(stderr)}");
        }

        var (headCode, head, headError) = await WorkingTree.GitAsync(folder, ["rev-parse", "--verify", "HEAD"], ct).ConfigureAwait(false);
        return headCode == 0 ? (head.Trim(), null) : (null, $"git rev-parse said: {Said(headError)}");
    }

    private static string Said(string text) => text.Trim().Length == 0 ? "nothing" : text.Trim().ReplaceLineEndings(" ");
}
