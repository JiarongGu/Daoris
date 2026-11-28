using static Daoris.Driver.TreeGit;

namespace Daoris.Driver;

/// <summary>Whether a save's work is on the line, and how the test found it; <paramref name="Unread"/> where git could not say, which is never landed.</summary>
public sealed record SaveLanding(bool Landed, string How, string? Unread = null);

/// <summary>
/// The landed test by content: a save's work is landed when the line holds every commit of it by patch id and the save
/// holds nothing beyond its head, or else when merging the line into the save, in git's own scratch index, leaves the line's tree as it
/// is. Anything git could not answer, a merge that conflicts, or a merged tree that differs is unlanded: the test never calls unlanded
/// work landed, and an unlanded save stays until the steward or the owner drops it.
/// </summary>
public static class TreeSaveLanded
{
    /// <summary>Judges <paramref name="save"/> against <paramref name="line"/>, a branch or commit of its repository.</summary>
    public static async Task<SaveLanding> JudgeAsync(TreeSave save, string line, CancellationToken ct = default)
    {
        var cherry = await GitRuns.GitAsync(save.Root, ["cherry", line, save.Head], ct).ConfigureAwait(false);
        if (cherry.Code != 0) return new(false, "unread", $"git could not compare the save's commits with `{line}`: {FirstLine(cherry.Stderr)}");
        var lacking = cherry.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries).Count(each => each.StartsWith('+'));
        if (lacking == 0 && save.Files.Count == 0) return new(true, "patch id");

        var merged = await GitRuns.GitAsync(save.Root, ["merge-tree", "--write-tree", "--no-messages", line, save.Commit], ct).ConfigureAwait(false);
        if (merged.Code == 1) return new(false, $"merging `{line}` into the save conflicts");
        if (merged.Code != 0) return new(false, "unread", $"git could not merge `{line}` into the save: {FirstLine(merged.Stderr)}");
        var lineTree = await GitRuns.GitAsync(save.Root, ["rev-parse", "--verify", "--quiet", $"{line}^{{tree}}"], ct).ConfigureAwait(false);
        if (lineTree.Code != 0) return new(false, "unread", $"git names no tree for `{line}`.");
        var tree = merged.Stdout.Split('\n', StringSplitOptions.TrimEntries).FirstOrDefault() ?? "";
        return tree.Length > 0 && tree == lineTree.Stdout.Trim()
            ? new(true, "content")
            : new(false, lacking > 0 ? $"`{line}` lacks {lacking} of its commit(s) and some of its files" : $"`{line}` lacks some of its files");
    }
}
