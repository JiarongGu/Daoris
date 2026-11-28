using static Daoris.Driver.TreeGit;

namespace Daoris.Driver;

/// <summary>
/// The terminal's doors to the save step and the ledger, for the steward's trial of saving and removing trees, the host's <c>trees</c>
/// verbs: <c>save &lt;tree&gt; [--work &lt;ignored path&gt;]…</c>, <c>restore &lt;save&gt; &lt;folder&gt;</c>, <c>remove &lt;tree&gt; --saved</c>,
/// <c>drop &lt;save&gt;</c> and <c>ledger [--days n] [--record]</c>. Nothing here is wired into sessions, the look or a sweep, and how sessions
/// get trees is unchanged.
/// </summary>
/// <remarks>
/// <para><b>Remove only behind a save that holds every file.</b> <c>--saved</c> is the one remover's forced removal of a session tree, its
/// branch kept, asked only through <see cref="TreeSaveGate"/> once the remover holds the tree's landing hold and its lock: a file written
/// after the save, an ignored file no <c>--work</c> named, a moved index or a lost ref keeps the tree, and so does a quest still open or
/// taken that a session served in it. It needs the service, which says no session holds the tree.</para>
/// <para><b>Landed only by the proof.</b> A pending save is marked landed only where the landed proof the repository's landing kind
/// chooses answers landed (<see cref="LandedProofKinds"/>); no door marks one by hand. <c>drop</c> gives a save up: its ref goes, verified at
/// its commit, and its record says when.</para>
/// <para>Exit codes keep the family contract: 0 done · 1 refused, the tree untouched · 2 a word it does not take.</para>
/// </remarks>
public static class TreeSaveDoors
{
    public const string Usage =
        "usage: daoris trees save <tree> [--work <ignored path>]... | restore <save> <empty folder> [--repository <name>] | remove <tree> --saved\n"
        + "                   | drop <save> [--repository <name>] | ledger [--days <1-90>] [--record]";

    /// <param name="service">The service: whose tree a save is, and whether a session or an open quest still holds a tree a removal takes.</param>
    /// <param name="clock">The root's clock: the saves' and the moves' times, and the day the ledger reads back from.</param>
    public static async Task<int> RunAsync(
        string[] args, SessionTrees trees, Func<ServiceClient?> service, TimeProvider clock, TextWriter output, TextWriter error, CancellationToken ct = default)
    {
        switch (args)
        {
            case ["save", var tree, .. var rest] when Work(rest) is { } work:
                return await SaveAsync(trees, tree, work, service, clock, output, error, ct).ConfigureAwait(false);
            case ["restore", var id, var folder, .. var rest] when Repository(rest) is var (ok, repository) && ok:
                return await RestoreAsync(trees, id, folder, repository, clock, output, error, ct).ConfigureAwait(false);
            case ["remove", var tree, "--saved"]:
                return await RemoveAsync(trees, tree, service, clock, output, error, ct).ConfigureAwait(false);
            case ["drop", var id, .. var rest] when Repository(rest) is var (ok, repository) && ok:
                return await DropAsync(trees, id, repository, clock, output, error, ct).ConfigureAwait(false);
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

    /// <summary>The repository <c>--repository</c> names, or none; not ok where the words are anything else.</summary>
    private static (bool Ok, string? Repository) Repository(IReadOnlyList<string> rest) => rest switch
    {
        [] => (true, null),
        ["--repository", var name] when !name.StartsWith('-') => (true, name),
        _ => (false, null),
    };

    /// <summary>The one save <paramref name="id"/> names, or the refusal: none, or more than one repository's without <c>--repository</c>.</summary>
    private static (TreeSave? Save, string? Refused) One(string home, string id, string? repository)
    {
        var matching = new TreeSaveRecords(home).Matching(id, repository);
        return matching.Count switch
        {
            0 => (null, $"trees: no save `{id}` is recorded on this machine{(repository is null ? "" : $" for {repository}")}."),
            1 => (matching[0], null),
            _ => (null, $"trees: `{id}` names saves of {string.Join(", ", matching.Select(save => $"{save.Workspace}/{save.Repository}"))}; name one with --repository."),
        };
    }

    private static async Task<int> SaveAsync(
        SessionTrees trees, string tree, IReadOnlyList<string> work, Func<ServiceClient?> service, TimeProvider clock, TextWriter output, TextWriter error, CancellationToken ct)
    {
        var full = TreeSaveRecords.Key(tree);
        if (!trees.Context.Holds(full)) return Refused(error, $"{tree} is not a session tree: these doors take only trees under {trees.TreesRoot}.");
        var (workspace, repository) = trees.Context.OwnerOf(full);
        var session = await SessionOfAsync(service, full, ct).ConfigureAwait(false);
        var saved = await new TreeSaves(trees.Context.Home, clock).SaveAsync(full, workspace, repository, work, session, ct).ConfigureAwait(false);
        if (saved.Save is not { } save) return Refused(error, $"trees: {saved.Said}");
        output.WriteLine($"trees: {saved.Said}");
        var landing = await JudgeAsync(trees, save, ct).ConfigureAwait(false);
        output.WriteLine(landing.Landed ? $"  landed: {landing.Evidence}." : $"  not landed ({landing.Verdict}): {landing.Evidence}; the save stays until it is dropped.");
        return 0;
    }

    /// <summary>The newest session whose record names <paramref name="tree"/>, where the service answers; none otherwise.</summary>
    private static async Task<string?> SessionOfAsync(Func<ServiceClient?> service, string tree, CancellationToken ct)
    {
        using var asked = service();
        if (asked is null) return null;
        try
        {
            return (await asked.RecordsAsync(ct).ConfigureAwait(false))
                .Where(record => record.Tree is { Length: > 0 } at && SamePath(at, tree))
                .MaxBy(record => record.Created ?? DateTimeOffset.MinValue)?.Session;
        }
        catch (DriverException)
        {
            return null;
        }
    }

    private static async Task<int> RestoreAsync(
        SessionTrees trees, string id, string folder, string? repository, TimeProvider clock, TextWriter output, TextWriter error, CancellationToken ct)
    {
        var home = trees.Context.Home;
        var (save, refused) = One(home, id, repository);
        if (save is null) return Refused(error, refused!);
        var proof = await TreeRestores.RestoreAsync(save, folder, ct).ConfigureAwait(false);
        if (!proof.Whole) return Refused(error, $"trees: {proof.Said}");
        Note(home, new TreeMove(save.Workspace, save.Repository, folder, TreeMoves.Restored, 0, Own(folder), clock.GetUtcNow()), error);
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

        var (workspace, repository) = trees.Context.OwnerOf(full);
        var before = Own(full);
        using var asked = service();
        var removal = await trees.RemoveSavedAsync(
            full, asked, check => asked is null ? Task.FromResult<string?>(null) : TreeSaveGate.RefusalAsync(trees.Context, save, full, asked, check), ct).ConfigureAwait(false);
        if (!removal.Removed) return Refused(error, $"trees: {removal.Message}");
        Note(home, new TreeMove(workspace, repository, full, TreeMoves.Removed, before, Directory.Exists(full) ? Own(full) : 0, clock.GetUtcNow()), error);
        output.WriteLine($"trees: {removal.Message} Its work is kept in `{save.Id}`.");
        return 0;
    }

    private static async Task<int> DropAsync(
        SessionTrees trees, string id, string? repository, TimeProvider clock, TextWriter output, TextWriter error, CancellationToken ct)
    {
        var home = trees.Context.Home;
        var (save, refused) = One(home, id, repository);
        if (save is null) return Refused(error, refused!);
        try
        {
            new TreeSaveRecords(home).Dropped(save, clock.GetUtcNow());
        }
        catch (DriverException failed)
        {
            return Refused(error, $"trees: `{save.Id}` stays: its record could not say it is dropped: {failed.Message}");
        }

        var kept = await TreeSaves.DropRefAsync(save, ct).ConfigureAwait(false);
        output.WriteLine($"trees: dropped `{save.Id}` of {save.Workspace}/{save.Repository}{(save.Landed is null ? ", which no proof found landed" : "")}; "
            + (kept is null ? "its ref is gone, so git may collect its commits." : $"its ref stays, since git would not delete it ({kept})."));
        return 0;
    }

    /// <summary>A reading of every repository's trees, after the pending saves are judged by the landed proof.</summary>
    private static async Task RecordAsync(SessionTrees trees, TimeProvider clock, TextWriter output, TextWriter error, CancellationToken ct)
    {
        var records = new TreeSaveRecords(trees.Context.Home);
        foreach (var save in records.Pending())
        {
            if ((await JudgeAsync(trees, save, ct).ConfigureAwait(false)) is { Landed: true } landed)
            {
                try
                {
                    records.Landed(save, landed);
                }
                catch (DriverException failed)
                {
                    error.WriteLine($"trees: `{save.Id}` is landed ({landed.Evidence}), but its record could not say so: {failed.Message}");
                }
            }
        }

        try
        {
            var reading = TreeLedgerReadings.Record(LedgerWorld.OfThisMachine(trees.Context.Home, clock));
            foreach (var alert in reading.Alerts) output.WriteLine($"trees ledger: {alert.Said}.");
        }
        catch (DriverException failed)
        {
            error.WriteLine($"trees ledger: the reading could not be kept: {failed.Message}");
        }
    }

    /// <summary>The landed proof's answer for the save against its repository's line; unknown where the line is not known.</summary>
    private static async Task<LandedAnswer> JudgeAsync(SessionTrees trees, TreeSave save, CancellationToken ct)
    {
        var line = (await trees.Context.LineAsync(save.Root, save.Repository, save.Workspace, ct).ConfigureAwait(false)).Branch;
        return line is null ? LandedAnswer.Unknown("its repository's line is not known") : await LandedProofKinds.JudgeAsync(new(save.Root, save.Commit, line), ct).ConfigureAwait(false);
    }

    /// <summary>A tree's own bytes, a hard link not counted, by the one walk every measure of bytes shares.</summary>
    private static long Own(string folder)
    {
        var measure = PlacesWalk.Measure([folder], [], WiringBudget.Default, CancellationToken.None, once: new FileRecords(FileLinks.RecordOf));
        return measure.Bytes - measure.Linked;
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
