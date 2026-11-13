using Microsoft.Data.Sqlite;

namespace Daoris.Driver;

/// <summary>A hand-off's phases, each written ahead of what it begins.</summary>
public static class TreeHandOffPhases
{
    /// <summary>The row written; nothing in the tree yet.</summary>
    public const string Reserved = "reserved";

    /// <summary>The tree stands at the stop with what the rebase settled staged.</summary>
    public const string Prepared = "prepared";

    /// <summary>The resolver is asked; its session is on the row.</summary>
    public const string Asked = "asked";

    /// <summary>The resolver is being stopped and the tree put back.</summary>
    public const string Restoring = "restoring";
}

/// <summary>
/// A tree handed to a step's resolver: the tree, its repository, branch and tip as handed, the <c>resolve</c> attempt that holds it, the
/// stop it asks about, the resolver, the ask's key its own turn carries, and how far the hand-off has come.
/// </summary>
/// <param name="Tree">The tree's key (<see cref="TreeHandOffs.KeyOf"/>).</param>
/// <param name="Ask">The ask's key, <c>resolve/&lt;run&gt;/&lt;node&gt;/&lt;attempt&gt;/&lt;try&gt;</c>: a start carrying it in this tree is the resolver's own.</param>
/// <param name="Until">When the ask's time runs out, as the card says it.</param>
public sealed record TreeHandOff(
    string Tree, string Repository, string Branch, string Tip, string Run, string Node, int Attempt, string Stop, string Resolver, string Ask,
    string Phase, DateTimeOffset Opened, DateTimeOffset Until)
{
    /// <summary>The row's id in the store.</summary>
    public long Id { get; init; }

    /// <summary>The resolver's session, once asked.</summary>
    public string? Session { get; init; }

    /// <summary>When the restoration closed it; null while it reserves the tree.</summary>
    public DateTimeOffset? Closed { get; init; }

    /// <summary><see cref="Until"/> as its words say it, the hour and minute here.</summary>
    public string UntilSaid => Until.ToLocalTime().ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The ask's key of try <paramref name="ask"/> of <paramref name="attempt"/> of a run's <c>resolve</c> node.</summary>
    public static string AskKey(string run, string node, int attempt, int ask) => $"resolve/{run}/{node}/{attempt}/{ask}";
}

/// <summary>
/// The trees handed to resolvers, a table of the desktop store (the family <i>tree hand-offs</i>): one open row per tree, written before
/// anything is written in the tree, each phase written ahead, and closed by the restoration.
/// </summary>
/// <remarks>
/// A row, not an act of the journal: the journal's acts close within their steps' deadlines and every writer of a checkout reconciles them,
/// so a hand-off there would hold another run's promotion for the resolver's whole ask, or be closed while the resolver still writes. Its
/// readers are the trees' holders (<see cref="TidyHolders"/>), the removal under the tree's landing hold, and every start inside its
/// hold (<see cref="TreeLock.TryStarting"/>); a writer that writes no session tree reads none.
/// </remarks>
/// <param name="home">The home whose store this is.</param>
/// <param name="settled">The reading of the start's confirmation; null reads this install's swap journal.</param>
/// <param name="clock">The clock of every moment written.</param>
public sealed class TreeHandOffs(string home, Func<StoreSettled>? settled = null, TimeProvider? clock = null)
{
    /// <summary>The family's name in <c>store_revisions</c>.</summary>
    public const string Family = "tree-hand-offs";

    private const string Columns = "id, tree, repository, branch, tip, run, node, attempt, stop, resolver, ask, session, phase, until, opened, closed";

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    /// <summary>The open row on <paramref name="tree"/>, or null where nothing reserves it.</summary>
    public TreeHandOff? Of(string tree) =>
        Reading(reader => reader.Query($"SELECT {Columns} FROM tree_hand_offs WHERE tree = $tree AND closed IS NULL", Row, ("$tree", KeyOf(tree)))
            .FirstOrDefault(), null);

    /// <summary>Every open row: what the driver's start restores before any pass, and the trees the holders name.</summary>
    public IReadOnlyList<TreeHandOff> Open() =>
        Reading(reader => reader.Query($"SELECT {Columns} FROM tree_hand_offs WHERE closed IS NULL ORDER BY id", Row), []);

    /// <summary>
    /// The row written <see cref="TreeHandOffPhases.Reserved"/>, or null where an open one reserves its tree already. The caller holds the
    /// tree's landing hold and the repository's lock alone, having read the holders (<c>TreeReserving</c>).
    /// </summary>
    internal TreeHandOff? Write(TreeHandOff row)
    {
        var keyed = row with { Tree = KeyOf(row.Tree), Phase = TreeHandOffPhases.Reserved, Opened = _clock.GetUtcNow(), Closed = null };
        using var store = Open(home);
        return store.Write(Family, write =>
        {
            if (write.Scalar<long>("SELECT COUNT(*) FROM tree_hand_offs WHERE tree = $tree AND closed IS NULL", ("$tree", keyed.Tree)) > 0) return null;
            write.Execute(
                $"INSERT INTO tree_hand_offs ({Columns[4..]}) VALUES ($tree, $repository, $branch, $tip, $run, $node, $attempt, $stop, $resolver, $ask, NULL, $phase, $until, $opened, NULL)",
                ("$tree", keyed.Tree), ("$repository", keyed.Repository), ("$branch", keyed.Branch), ("$tip", keyed.Tip), ("$run", keyed.Run),
                ("$node", keyed.Node), ("$attempt", keyed.Attempt), ("$stop", keyed.Stop), ("$resolver", keyed.Resolver), ("$ask", keyed.Ask),
                ("$phase", keyed.Phase), ("$until", Moments.Write(keyed.Until)), ("$opened", Moments.Write(keyed.Opened)));
            return keyed with { Id = write.Scalar<long>("SELECT last_insert_rowid()") };
        });
    }

    /// <summary>The open row moved to <paramref name="phase"/>, with the resolver's session once asked; false where it closed meanwhile.</summary>
    public bool Advance(TreeHandOff row, string phase, string? session = null) =>
        Update(row, "phase = $phase, session = COALESCE($session, session)", ("$phase", phase), ("$session", session));

    /// <summary>The row closed by its restoration: the tree is free again.</summary>
    public bool Close(TreeHandOff row) => Update(row, "closed = $closed", ("$closed", Moments.Write(_clock.GetUtcNow())));

    /// <summary>
    /// The run's own step the hand-off's preparation, its restoration and its resolver's own start and session lock run under, so the gate
    /// passes their git in the tree it reserves.
    /// </summary>
    public static StepAuthority AuthorityFor(TreeHandOff row) => new(row.Run, row.Node, row.Attempt, row.Ask);

    /// <summary>The ask key <paramref name="session"/>'s start in <paramref name="tree"/> carries where the open row asked it; else null.</summary>
    public string? AskOf(string tree, string session) =>
        Of(tree) is { Phase: TreeHandOffPhases.Asked } row && string.Equals(row.Session, session, StringComparison.Ordinal) ? row.Ask : null;

    /// <summary>A tree's key: its full path, without a trailing separator, in one case on Windows.</summary>
    public static string KeyOf(string tree)
    {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(tree)).Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        return OperatingSystem.IsWindows() ? full.ToLowerInvariant() : full;
    }

    private bool Update(TreeHandOff row, string set, params (string Name, object? Value)[] values)
    {
        using var store = Open(home);
        return store.Write(Family, write => write.Execute(
            $"UPDATE tree_hand_offs SET {set} WHERE id = $id AND closed IS NULL", [("$id", row.Id), .. values]) == 1);
    }

    private static TreeHandOff Row(DbDataReader row) =>
        new(row.GetString(1), row.GetString(2), row.GetString(3), row.GetString(4), row.GetString(5), row.GetString(6), row.GetInt32(7),
            row.GetString(8), row.GetString(9), row.GetString(10), row.GetString(12), Moment(row, 14), Moment(row, 13))
        {
            Id = row.GetInt64(0),
            Session = row.IsDBNull(11) ? null : row.GetString(11),
            Closed = row.IsDBNull(15) ? null : Moment(row, 15),
        };

    private static DateTimeOffset Moment(DbDataReader row, int at) =>
        Moments.Read(row.GetString(at)) ?? throw new DesktopStoreException("a hand-off's moment does not read as one");

    private DesktopStore Open(string home) => DesktopStore.Open(DesktopStore.LocationIn(home), StoreProcess.Options(settled ?? StartConfirmation.Read));

    private T Reading<T>(Func<DesktopStoreReader, T> read, T none)
    {
        using var reader = DesktopStoreReader.Open(DesktopStore.LocationIn(home));
        return reader is not null && reader.Version >= DesktopStore.HandOffsVersion ? read(reader) : none;
    }
}
