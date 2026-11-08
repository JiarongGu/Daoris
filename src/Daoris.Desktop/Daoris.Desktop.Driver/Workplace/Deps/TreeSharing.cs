namespace Daoris.Driver;

/// <summary>What the files stage's shares said, and what the session is told of them.</summary>
internal sealed record TreeShared(IReadOnlyList<Noted> Said, string? Told);

/// <summary>
/// The files stage's shares (the tree-pool design §35.5, §37.4): each path whose effective rule says <c>reach: read</c> or <c>write</c>
/// linked in the tree to its one place in the checkout, before the copies, so no copy of Daoris's writes through a share. A share is
/// made only into an absent entry, recorded before its link is made, and only where the person's permissions let <c>share(path)</c>
/// through, git ignores the path in the tree and the checkout, and the place stands, is no link and lies in no other repository.
/// What stands at a share's path and is not its link is the line's own and is never written over; a share lost since the last stage is said.
/// </summary>
internal static class TreeSharing
{
    /// <summary>Windows' word for a symbolic link made without the privilege, which Developer Mode grants.</summary>
    private const int PrivilegeNotHeld = unchecked((int)0x80070522);

    internal static async Task<TreeShared> RunAsync(
        string home, string root, string tree, string repository, FileRules rules, FileFloorFacts floor, CancellationToken ct)
    {
        var said = new List<Noted>();
        TreeShares.Holding(home, tree);
        if (TreeFilesRecord.Read(home, tree) is { Lost.Count: > 0 } lost)
        {
            foreach (var path in lost.Lost) said.Add(Lost(path));
            TreeFilesRecord.Change(home, tree, state => state with { Lost = [] });
        }

        var paths = new List<string>();
        foreach (var rule in rules.Rules.Select(each => each.Rule).Where(rule => !rule.FirstForm && FileKindSets.Shares(rule)))
        {
            foreach (var (pattern, path) in rule.Paths.Where(pattern => !pattern.StartsWith('!')).Select(pattern => (pattern, GitPattern.LiteralOf(rule, pattern))))
            {
                if (path is null) said.Add(Declined(pattern, ShareWhy.NoOnePath()));
                else if (!paths.Contains(path)) paths.Add(path);
            }
        }

        var shared = new List<TreeShare>();
        foreach (var path in paths)
        {
            if (rules.For(path, floor).Share is not { } reach) continue;
            var share = new TreeShare(path, Path.GetFullPath(TreeShares.Full(root, path)), reach);
            if (Permitted(home, repository, rules, path) is { } held) said.Add(Declined(path, held));
            else if (await WhyNotAsync(home, root, tree, share, ct).ConfigureAwait(false) is { } why) said.Add(Declined(path, why));
            else shared.Add(share);
        }

        if (shared.Count == 0) return new(said, null);
        said.Add(Shared(shared));
        return new(said, Told(shared));
    }

    /// <summary>Why the person's permissions keep the share from this tree, or null where they let it through.</summary>
    private static DepsReason? Permitted(string home, string repository, FileRules rules, string path)
    {
        var act = FileActs.Share(path);
        switch (rules.Judge(act))
        {
            case FileVerdict.Denied:
                FileAsks.Close(home, repository, FileAsk.Share, act);
                return DepsWhy.DeniedBy(act);
            case FileVerdict.Held:
                FileAsks.Note(home, new FileAsk(repository, Moments.Write(DateTimeOffset.UtcNow), FileAsk.Share, act) { Person = rules.NamesPerson(act) });
                return DepsWhy.HeldFor(act);
            default:
                FileAsks.Close(home, repository, FileAsk.Share, act);
                return null;
        }
    }

    /// <summary>The share kept or made in the tree, or why not: the floor's reads of the place and the path, then what stands in the tree.</summary>
    private static async Task<DepsReason?> WhyNotAsync(string home, string root, string tree, TreeShare share, CancellationToken ct)
    {
        var folder = share.Path.EndsWith('/');
        var place = new FileInfo(share.Place);
        if ((int)place.Attributes == -1 || place.Attributes.HasFlag(FileAttributes.Directory) != folder) return ShareWhy.PlaceAbsent();
        if (place.Attributes.HasFlag(FileAttributes.ReparsePoint)) return ShareWhy.PlaceIsLink();
        if (InRepository(root, folder ? share.Place : Path.GetDirectoryName(share.Place)!)) return ShareWhy.InRepository();
        if (!await IgnoredAsync(tree, share.Path, ct).ConfigureAwait(false) || !await IgnoredAsync(root, share.Path, ct).ConfigureAwait(false))
        {
            return ShareWhy.NotIgnored();
        }

        var recorded = TreeFilesRecord.Read(home, tree)?.Shares.Any(each => each == share) == true;
        switch (TreeShares.Standing(tree, share))
        {
            case TreeShareStanding.Holds when recorded:
                return null;
            case TreeShareStanding.Absent when !LinkOnTheWay(tree, share.Path):
                return Make(home, tree, share);
            default:
                return ShareWhy.StandsThere();
        }
    }

    /// <summary>The share recorded, then its link made, so a cut-off making leaves no link the record does not name.</summary>
    private static DepsReason? Make(string home, string tree, TreeShare share)
    {
        TreeFilesRecord.Change(home, tree, state => state with { Shares = [.. state.Shares.Where(each => each.Path != share.Path), share] });
        try
        {
            TreeShares.Make(tree, share);
            return null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            TreeFilesRecord.Change(home, tree, state => TreeShares.Drop(state, [share.Path], []));
            return !share.Path.EndsWith('/') && (error is UnauthorizedAccessException || error.HResult == PrivilegeNotHeld)
                ? ShareWhy.NoFileLink()
                : ShareWhy.FailedSaying(error.Message.Trim());
        }
    }

    /// <summary>Whether <paramref name="place"/> is, or lies inside, a folder below the checkout's top that holds a <c>.git</c> or is a bare repository.</summary>
    private static bool InRepository(string root, string place)
    {
        var top = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        for (var folder = Path.TrimEndingDirectorySeparator(place); folder.Length > top.Length; folder = Path.GetDirectoryName(folder)!)
        {
            if (Path.Exists(Path.Combine(folder, ".git")) || FileFloor.LooksBare(folder)) return true;
        }

        return false;
    }

    private static async Task<bool> IgnoredAsync(string folder, string path, CancellationToken ct)
    {
        var (code, _, _) = await WorkingTree.GitAsync(folder, ["check-ignore", "-q", "--", path], ct).ConfigureAwait(false);
        return code == 0;
    }

    /// <summary>Whether a folder between the tree's top and the share's entry is a link, which the share's link would be made through.</summary>
    private static bool LinkOnTheWay(string tree, string path)
    {
        var top = Path.TrimEndingDirectorySeparator(Path.GetFullPath(tree));
        for (var folder = Path.GetDirectoryName(TreeShares.Full(top, path)); folder is not null && folder.Length > top.Length; folder = Path.GetDirectoryName(folder))
        {
            if (Path.Exists(folder) && File.GetAttributes(folder).HasFlag(FileAttributes.ReparsePoint)) return true;
        }

        return false;
    }

    private static string Listed(IEnumerable<TreeShare> shares) => string.Join(", ", shares.Select(share => $"`{share.Path}`"));

    private static Noted Shared(IReadOnlyList<TreeShare> shares) => Noted.Of(
        StartNoteCodes.StartedTreeShared,
        $"{Listed(shares)}: each is a link to one place in your checkout that every tree of this repository uses, so what is written through it "
        + "reaches them all. Remove such a link itself, never what lies under it: a recursive delete that follows a link deletes the shared "
        + "place's files, as Windows PowerShell 5.1's `Remove-Item -Recurse` over a junction does, and git's worktree removal. A single file "
        + "shared is a link a save by rename replaces, which makes it this tree's own file.",
        ("shared", (IReadOnlyList<string>)[.. shares.Select(share => share.Path)]));

    private static Noted Declined(string path, DepsReason why) =>
        Noted.Of(StartNoteCodes.StartedShareDeclined, $"`{path}` was not shared into this tree: {why.Text}.", why.With(("path", path)));

    private static Noted Lost(string path) => Noted.Of(
        StartNoteCodes.TreeShareLost,
        $"`{path}` is no longer the link Daoris made to its shared place (a save by rename or a folder made again replaces one), so it is "
        + "this line's own now, sorted by the rules as any path is; nothing of the shared place was touched.",
        ("path", path));

    /// <summary>What the session's agent is told of its shares: which are read-only by Daoris's word alone, and how a link is removed.</summary>
    private static string Told(IReadOnlyList<TreeShare> shares)
    {
        var read = shares.Where(share => share.Reach == "read").ToList();
        var write = shares.Where(share => share.Reach == "write").ToList();
        return $"Daoris linked these paths to one place in the repository's checkout that every tree uses: {Listed(shares)}. "
            + (read.Count == 0 ? "" : $"{Listed(read)} {(read.Count == 1 ? "is" : "are")} shared read-only: Daoris writes nothing through "
                + "it, but nothing stops your own shell or tools writing there, and such a write lands in the checkout and every tree, so leave it as it is. ")
            + (write.Count == 0 ? "" : $"What you write under {Listed(write)} reaches every tree. ")
            + "Never delete through such a link: remove the link itself (`Remove-Item` without `-Recurse`, or `rmdir` on a junction), since a "
            + "recursive delete that follows it deletes the shared files. Saving a shared single file by rename replaces its link with a file of this tree's own.";
    }
}
