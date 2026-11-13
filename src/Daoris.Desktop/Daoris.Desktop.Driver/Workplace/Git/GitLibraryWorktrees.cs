using System.Text;

namespace Daoris.Driver;

/// <summary>
/// <c>worktree list --porcelain</c> as git prints it, read from the repository's <c>worktrees</c> folder: the main tree first, then each
/// linked tree by path, with its head, its branch or <c>detached</c>, and its lock and the reason. A bare repository, an unborn head, a
/// tree whose directory is gone or whose files say less than a path and a head (git would call it prunable), a lock reason with
/// more than plain printable text, and a list whose order depends on the case of non-ASCII names are git's.
/// </summary>
internal static class GitLibraryWorktrees
{
    private static readonly string[] Command = ["list", "--porcelain"];

    /// <summary>The library's question for <paramref name="rest"/>, the arguments after <c>worktree</c>; null where it is git's.</summary>
    internal static (GitLibraryQuestion, Func<GitLibraryCall, GitLibraryAnswer?>)? Asked(List<string> rest) =>
        rest.SequenceEqual(Command) ? (GitLibraryQuestion.Layout, call => Listed(call) is { } text ? new GitLibraryAnswer(0, text) : null) : null;

    private sealed record Tree(string Path, string Head, string? Branch, bool Locked, string? Reason);

    private static string? Listed(GitLibraryCall call)
    {
        if (!GitLibraryLayout.Plain(call) || call.Repository.Info.IsBare || !call.CommonDir.Replace('\\', '/').EndsWith("/.git", StringComparison.OrdinalIgnoreCase)) return null;
        if (GitLibrarySpelling.Real(Path.GetDirectoryName(call.CommonDir)!) is not { } top || TreeOf(call, call.CommonDir, top, locked: false, reason: null) is not { } main) return null;
        var linked = new List<Tree>();
        var folder = Path.Combine(call.CommonDir, "worktrees");
        if (Directory.Exists(folder))
        {
            foreach (var own in Directory.EnumerateDirectories(folder))
            {
                if (Linked(call, own) is not { } tree) return null;
                linked.Add(tree);
            }
        }

        if (linked.Count > 1 && linked.Any(tree => tree.Path.Any(letter => letter > 127))) return null;
        // The order is by path, blind to case where the repository says its file system is: unsaid, git's.
        if (linked.Count > 1)
        {
            if (GitLibraryLayout.IgnoreCase(call) is not { } ignoreCase) return null;
            linked.Sort((one, other) => string.Compare(one.Path, other.Path, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));
        }

        var text = new StringBuilder();
        foreach (var tree in new[] { main }.Concat(linked))
        {
            text.Append("worktree ").Append(tree.Path).Append('\n').Append("HEAD ").Append(tree.Head).Append('\n')
                .Append(tree.Branch is { } branch ? "branch " + branch : "detached").Append('\n');
            if (tree.Locked) text.Append("locked").Append(tree.Reason is { Length: > 0 } reason ? " " + reason : "").Append('\n');
            text.Append('\n');
        }

        return text.ToString();
    }

    private static Tree? Linked(GitLibraryCall call, string own)
    {
        try
        {
            var pointer = Path.Combine(own, "gitdir");
            if (!File.Exists(pointer) || File.ReadAllText(pointer).Trim() is not { Length: > 0 } written || !Path.IsPathRooted(written)) return null;
            // A tree whose pointer names a file that is gone is one git calls prunable, and says so.
            if (!File.Exists(written) || !written.Replace('\\', '/').EndsWith("/.git", StringComparison.Ordinal)) return null;
            var locked = Path.Combine(own, "locked");
            string? reason = null;
            if (File.Exists(locked) && File.ReadAllText(locked).Trim() is var said && !(said.Length > 0 && said.Any(letter => letter < ' ' || letter > '~' || letter is '"' or '\\'))) reason = said;
            else if (File.Exists(locked)) return null;
            return TreeOf(call, own, written.Replace('\\', '/')[..^"/.git".Length], File.Exists(locked), reason);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static Tree? TreeOf(GitLibraryCall call, string gitDir, string path, bool locked, string? reason)
    {
        if (path.Contains('~') || !File.Exists(Path.Combine(gitDir, "HEAD"))) return null;
        var head = File.ReadAllText(Path.Combine(gitDir, "HEAD")).Trim();
        if (head.StartsWith("ref:", StringComparison.Ordinal))
        {
            var reference = head["ref:".Length..].Trim();
            var tip = call.Repository.Refs[reference]?.ResolveToDirectReference()?.TargetIdentifier;
            return tip is null || !reference.StartsWith("refs/", StringComparison.Ordinal) ? null : new Tree(path, tip, reference, locked, reason);
        }

        return head.Length == 40 && head.All(char.IsAsciiHexDigitLower) ? new Tree(path, head, null, locked, reason) : null;
    }
}
