using static Daoris.Driver.TreeGit;

namespace Daoris.Driver;

/// <summary>
/// The one check a removal behind a save passes, asked by the one remover once it holds the tree's landing hold and its lock, right
/// before the tree goes (<see cref="SessionTrees.RemoveSavedAsync"/>): no file a person or agent made is lost.
/// </summary>
/// <remarks>
/// It keeps the tree, saying why, where any of these fails: no session in it serves a quest still open or taken (a resume rebuilds from
/// the branch and reads no save yet); the save's ref still resolves to the save's commit and its commits of the index and of the files are
/// present; the tree holds the save's head, its index writes the staged commit's tree, and every changed and named ignored file reads by
/// its hash, nothing more; and every ignored file only this tree holds (the content holds' own, their scratch and their folders too large
/// to read included) lies in the save's list. Until the repository's own file names its work, what no <c>--work</c> named keeps the
/// tree.
/// </remarks>
internal static class TreeSaveGate
{
    internal static async Task<string?> RefusalAsync(TreeContext context, TreeSave save, string tree, ServiceClient service, CancellationToken ct)
    {
        if (await OpenQuestAsync(service, tree, ct).ConfigureAwait(false) is { } quest)
        {
            return $"quest #{quest} served in it is still open or taken, and a resume rebuilds its tree from the branch, which reads no save yet; "
                + $"once it closes the tree may go, and `daoris trees restore {save.Id} <empty folder>` brings its work back after";
        }

        var held = await GitRuns.GitAsync(save.Root, ["rev-parse", "--verify", "--quiet", save.Id], ct).ConfigureAwait(false);
        if (held.Stdout.Trim() != save.Commit) return $"`{save.Id}` no longer holds its save in {save.Root}; save it again";
        foreach (var commit in new[] { save.Staged, save.Raw })
        {
            if ((await GitRuns.GitAsync(save.Root, ["rev-parse", "--verify", "--quiet", $"{commit}^{{commit}}"], ct).ConfigureAwait(false)).Code != 0)
            {
                return $"the commit {commit[..Math.Min(12, commit.Length)]} of `{save.Id}` is missing in {save.Root}; save it again";
            }
        }

        var (listing, refused) = await TreeSaveListing.ListAsync(tree, save.Work, ct).ConfigureAwait(false);
        if (listing is null) return refused!.Said;
        if (Changed(save, listing) is { } changed) return $"it changed since its save `{save.Id}` ({changed}); save it again";
        var index = await TreeSaves.IndexTreeAsync(listing, ct).ConfigureAwait(false);
        var staged = (await GitRuns.GitAsync(save.Root, ["rev-parse", "--verify", "--quiet", $"{save.Staged}^{{tree}}"], ct).ConfigureAwait(false)).Stdout.Trim();
        if (index is null || index != staged) return $"its index is not the one its save `{save.Id}` holds; save it again";

        var root = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(listing.CommonDir))!;
        var owner = context.OwnerOf(listing.Tree);
        var holds = await ContentHolds.HoldsAsync(
            listing.Tree, root, await context.ScratchAsync(root, owner.Repository, owner.Workspace, ct).ConfigureAwait(false), ct,
            new LinePlaces(context.Home).SlotAt(owner.Workspace, owner.Repository, listing.Tree), TreeFilesRecord.Read(context.Home, listing.Tree),
            await FileRemade.OfAsync(context.Home, owner.Repository, listing.Tree, ct).ConfigureAwait(false)).ConfigureAwait(false);
        if (holds is null) return "Daoris cannot tell which ignored files only this tree holds";
        var unsaved = Unsaved(save, [.. holds.Ignored, .. holds.Scratch, .. holds.Written, .. holds.TooLarge]);
        return unsaved.Count == 0 ? null
            : $"it holds {unsaved.Count} ignored path(s) only it has that its save does not: {string.Join(", ", unsaved.Take(5))}{(unsaved.Count > 5 ? $" and {unsaved.Count - 5} more" : "")}; "
              + $"save it again naming them with `--work`, or move them somewhere of yours";
    }

    /// <summary>The paths of <paramref name="held"/> the save does not hold: neither listed nor under a path it took as work.</summary>
    internal static IReadOnlyList<string> Unsaved(TreeSave save, IEnumerable<string> held)
    {
        var listed = save.Files.Select(file => file.Path).ToHashSet(StringComparer.Ordinal);
        bool Covered(string path)
        {
            var bare = path.TrimEnd('/');
            return listed.Contains(bare) || save.Work.Any(work => bare == work || bare.StartsWith(work + "/", StringComparison.Ordinal));
        }

        return [.. held.Where(path => !Covered(path)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
    }

    /// <summary>What the tree holds that its save does not read: another head, a file not listed, one listed and gone, one whose hash moved.</summary>
    private static string? Changed(TreeSave save, SaveListing listing)
    {
        if (listing.Head != save.Head) return $"its head is {listing.Head[..Math.Min(12, listing.Head.Length)]}, not the save's";
        var saved = save.Files.ToDictionary(file => file.Path, file => file.Sha256, StringComparer.Ordinal);
        var now = listing.Files.ToDictionary(file => file.Path, file => file.Sha256, StringComparer.Ordinal);
        var moved = now.Where(pair => !saved.TryGetValue(pair.Key, out var sha) || sha != pair.Value).Select(pair => pair.Key)
            .Concat(saved.Keys.Where(path => !now.ContainsKey(path))).Order(StringComparer.Ordinal).ToList();
        return moved.Count == 0 ? null : $"{string.Join(", ", moved.Take(5))}{(moved.Count > 5 ? $" and {moved.Count - 5} more" : "")}";
    }

    /// <summary>The quest still open or taken that a session served in <paramref name="tree"/>; null where none.</summary>
    private static async Task<string?> OpenQuestAsync(ServiceClient service, string tree, CancellationToken ct)
    {
        var records = service.RecordsAsync(ct);
        var quests = service.EveryQuestAsync(ct);
        await Task.WhenAll(records, quests).ConfigureAwait(false);
        var open = (await quests.ConfigureAwait(false)).Where(quest => quest.Status is "Open" or "Taken").Select(quest => quest.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return (await records.ConfigureAwait(false))
            .Where(record => record.Tree is { Length: > 0 } at && SamePath(at, tree) && record.Quest is { Length: > 0 } quest && open.Contains(quest))
            .Select(record => record.Quest).FirstOrDefault();
    }
}
