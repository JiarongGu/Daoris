namespace Daoris.Driver;

/// <summary>
/// The terminal's doors to the save step and the ledger, for the steward's trial of saving and removing copies, the host's <c>trees</c> verbs:
/// <c>save &lt;tree&gt; [--work &lt;ignored path&gt;]…</c>, <c>restore &lt;save&gt; &lt;folder&gt;</c>, <c>remove &lt;tree&gt; --saved</c> and
/// <c>ledger [--days n] [--record]</c>. Nothing here is wired into sessions, the look or a sweep, and how sessions get trees is unchanged.
/// </summary>
/// <remarks>
/// <para><b>Remove only behind a save that reads the tree as it is.</b> <c>--saved</c> removes a session tree only where its newest save
/// record stands and the tree, read again, holds that save's head and every listed file with its hash, nothing more: a file written
/// after the save holds the tree until it is saved again. The removal is the tree's one remover, its links let go of first, its branch
/// kept, since the save holds the work and the branch holds the head.</para>
/// <para>Exit codes keep the family contract: 0 done · 1 refused, the tree untouched · 2 a word it does not take.</para>
/// </remarks>
public static class TreeSaveDoors
{
    public const string Usage =
        "usage: daoris trees save <tree> [--work <ignored path>]... | restore <save> <empty folder> | remove <tree> --saved\n"
        + "                   | ledger [--days <1-90>] [--record]";

    /// <param name="service">The service a removal asks whether a session still holds the tree, or none.</param>
    /// <param name="clock">The root's clock: the saves' and the moves' times, and the day the ledger reads back from.</param>
    public static async Task<int> RunAsync(
        string[] args, SessionTrees trees, Func<ServiceClient?> service, TimeProvider clock, TextWriter output, TextWriter error, CancellationToken ct = default)
    {
        switch (args)
        {
            case ["save", var tree, .. var rest] when Work(rest) is { } work:
                return await SaveAsync(trees, tree, work, clock, output, error, ct).ConfigureAwait(false);
            case ["restore", var id, var folder]:
                return await RestoreAsync(trees, id, folder, clock, output, error, ct).ConfigureAwait(false);
            case ["remove", var tree, "--saved"]:
                return await RemoveAsync(trees, tree, service, clock, output, error, ct).ConfigureAwait(false);
            case ["ledger", .. var rest] when TreeLedgerPrint.Read(rest) is { } asked:
                if (asked.Record) await RecordAsync(trees, clock, output, error, ct).ConfigureAwait(false);
                return TreeLedgerPrint.Run(rest, trees.Context.Home, clock.GetUtcNow, output);
            default:
                error.WriteLine(Usage);
                return 2;
        }
    }

    /// <summary>The ignored paths <c>--work</c> names, each once; null where a word is not <c>--work &lt;path&gt;</c>.</summary>
    private static List<string>? Work(IReadOnlyList<string> rest)
    {
        var work = new List<string>();
        for (var at = 0; at < rest.Count; at += 2)
        {
            if (rest[at] != "--work" || at + 1 >= rest.Count || rest[at + 1].StartsWith('-')) return null;
            work.Add(rest[at + 1].Replace('\\', '/').TrimEnd('/'));
        }

        return work.Distinct(StringComparer.Ordinal).ToList();
    }

    private static async Task<int> SaveAsync(
        SessionTrees trees, string tree, IReadOnlyList<string> work, TimeProvider clock, TextWriter output, TextWriter error, CancellationToken ct)
    {
        var full = TreeSaveRecords.Key(tree);
        if (!trees.Context.Holds(full)) return Refused(error, $"{tree} is not a session tree: these doors take only trees under {trees.TreesRoot}.");
        var (workspace, repository) = trees.Context.OwnerOf(full);
        var saved = await new TreeSaves(trees.Context.Home, clock).SaveAsync(full, workspace, repository, work, ct).ConfigureAwait(false);
        if (saved.Save is not { } save) return Refused(error, $"trees: {saved.Said}");
        output.WriteLine($"trees: {saved.Said}");
        var landing = await JudgeAsync(trees, save, ct).ConfigureAwait(false);
        output.WriteLine(landing.Landed ? $"  landed by {landing.How}: its work is on the line." : $"  not landed: {landing.Unread ?? landing.How}; the save stays until it is dropped.");
        return 0;
    }

    private static async Task<int> RestoreAsync(
        SessionTrees trees, string id, string folder, TimeProvider clock, TextWriter output, TextWriter error, CancellationToken ct)
    {
        var home = trees.Context.Home;
        if (new TreeSaveRecords(home).Of(id) is not { } save) return Refused(error, $"trees: no save `{id}` is recorded on this machine.");
        var proof = await TreeSaves.RestoreAsync(save, folder, ct).ConfigureAwait(false);
        Note(home, new TreeMove(save.Workspace, save.Repository, folder, TreeMoves.Restored, 0, FolderBytes.Measure(folder).Own, clock.GetUtcNow()), error);
        if (!proof.Whole) return Refused(error, $"trees: {proof.Said}");
        output.WriteLine($"trees: {proof.Said}");
        return 0;
    }

    private static async Task<int> RemoveAsync(
        SessionTrees trees, string tree, Func<ServiceClient?> service, TimeProvider clock, TextWriter output, TextWriter error, CancellationToken ct)
    {
        var full = TreeSaveRecords.Key(tree);
        var home = trees.Context.Home;
        if (!trees.Context.Holds(full)) return Refused(error, $"{tree} is not a session tree: these doors take only trees under {trees.TreesRoot}.");
        if (new TreeSaveRecords(home).LatestOf(full) is not { } save)
        {
            return Refused(error, $"trees: the tree at {tree} stays: it has no save; `daoris trees save {tree}` makes one.");
        }

        var (listing, refused) = await TreeSaves.ListAsync(full, save.Work, ct).ConfigureAwait(false);
        if (listing is null) return Refused(error, $"trees: the tree at {tree} stays: {refused!.Said}");
        if (Changed(save, listing) is { } changed)
        {
            return Refused(error, $"trees: the tree at {tree} stays: it changed since its save `{save.Id}` ({changed}); save it again.");
        }

        var (workspace, repository) = trees.Context.OwnerOf(full);
        var before = FolderBytes.Measure(full).Own;
        using var asked = service();
        var removal = await trees.RemoveSavedAsync(full, asked, ct).ConfigureAwait(false);
        if (!removal.Removed) return Refused(error, $"trees: {removal.Message}");
        Note(home, new TreeMove(workspace, repository, full, TreeMoves.Removed, before, Directory.Exists(full) ? FolderBytes.Measure(full).Own : 0, clock.GetUtcNow()), error);
        output.WriteLine($"trees: {removal.Message} Its work is kept in `{save.Id}`.");
        return 0;
    }

    /// <summary>What the tree holds that its save does not read: another head, a file not listed, one listed and gone, one whose hash moved.</summary>
    private static string? Changed(TreeSave save, TreeSaves.Listing listing)
    {
        if (listing.Head != save.Head) return $"its head is {listing.Head[..Math.Min(12, listing.Head.Length)]}, not the save's";
        var saved = save.Files.ToDictionary(file => file.Path, file => file.Sha256, StringComparer.Ordinal);
        var now = listing.Files.ToDictionary(file => file.Path, file => file.Sha256, StringComparer.Ordinal);
        var moved = now.Where(pair => !saved.TryGetValue(pair.Key, out var sha) || sha != pair.Value).Select(pair => pair.Key)
            .Concat(saved.Keys.Where(path => !now.ContainsKey(path))).Order(StringComparer.Ordinal).ToList();
        return moved.Count == 0 ? null : $"{string.Join(", ", moved.Take(5))}{(moved.Count > 5 ? $" and {moved.Count - 5} more" : "")}";
    }

    /// <summary>A reading of every repository's copies, after the pending saves are judged landed by content where they are.</summary>
    private static async Task RecordAsync(SessionTrees trees, TimeProvider clock, TextWriter output, TextWriter error, CancellationToken ct)
    {
        var records = new TreeSaveRecords(trees.Context.Home);
        foreach (var save in records.Pending())
        {
            if ((await JudgeAsync(trees, save, ct).ConfigureAwait(false)) is { Landed: true } landed)
            {
                try
                {
                    records.Landed(save.Id, landed.How);
                }
                catch (DriverException failed)
                {
                    error.WriteLine($"trees: `{save.Id}` is landed by {landed.How}, but its record could not say so: {failed.Message}");
                }
            }
        }

        try
        {
            var reading = TreeLedgerReadings.Record(LedgerWorld.OfThisMachine(trees.Context.Home, trees.Context.Config(), clock));
            foreach (var alert in reading.Alerts) output.WriteLine($"trees ledger: {alert.Said}.");
        }
        catch (DriverException failed)
        {
            error.WriteLine($"trees ledger: the reading could not be kept: {failed.Message}");
        }
    }

    /// <summary>The landed test against the save's repository's line; unread where the line is not known.</summary>
    private static async Task<SaveLanding> JudgeAsync(SessionTrees trees, TreeSave save, CancellationToken ct)
    {
        var line = (await trees.Context.LineAsync(save.Root, save.Repository, save.Workspace, ct).ConfigureAwait(false)).Branch;
        return line is null ? new(false, "unread", "its repository's line is not known") : await TreeSaveLanded.JudgeAsync(save, line, ct).ConfigureAwait(false);
    }

    private static void Note(string home, TreeMove move, TextWriter error)
    {
        try
        {
            new TreeLedger(home).Note(move);
        }
        catch (DriverException failed)
        {
            error.WriteLine($"trees ledger: the {move.Move} of {move.Tree} could not be noted: {failed.Message}");
        }
    }

    private static int Refused(TextWriter error, string said)
    {
        error.WriteLine(said);
        return 1;
    }
}
