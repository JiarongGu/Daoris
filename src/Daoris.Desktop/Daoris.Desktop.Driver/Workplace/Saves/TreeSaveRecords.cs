using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Daoris.Driver;

/// <summary>
/// The saves' records on this machine, one row a save in the desktop store's <c>tree_saves</c> (<see cref="TreeLedgerStoreStep"/>): kept
/// until the steward or the owner drops a save, its landed mark set once the landed test by content finds its work on the line.
/// </summary>
/// <remarks>
/// A record is written after its ref holds its commits, so a record always names a save git has; a ref no record names is part d's to
/// reconcile. A store that does not read answers no record, and a write it refuses is a <see cref="DriverException"/>.
/// </remarks>
public sealed class TreeSaveRecords(string home, Func<StoreSettled>? settled = null)
{
    public const string Family = "tree_saves";

    private const string Columns = "id, workspace, repository, root, tree, branch, head, save, staged, raw, work, files, bytes, took, at, landed";

    private static readonly JsonSerializerOptions Shape = new(JsonSerializerDefaults.Web);

    private string Location => DesktopStore.LocationIn(home);

    /// <summary>Keeps <paramref name="save"/>'s record.</summary>
    /// <exception cref="DriverException">The store would not take it.</exception>
    public void Keep(TreeSave save) => Write(write => write.Execute(
        $"INSERT INTO tree_saves ({Columns}) VALUES ($id, $ws, $repo, $root, $tree, $branch, $head, $save, $staged, $raw, $work, $files, $bytes, $took, $at, $landed)",
        ("$id", save.Id), ("$ws", save.Workspace), ("$repo", save.Repository), ("$root", save.Root), ("$tree", Key(save.Tree)),
        ("$branch", save.Branch), ("$head", save.Head), ("$save", save.Commit), ("$staged", save.Staged), ("$raw", save.Raw),
        ("$work", JsonSerializer.Serialize(save.Work, Shape)), ("$files", JsonSerializer.Serialize(save.Files, Shape)), ("$bytes", save.Bytes),
        ("$took", save.TookMs), ("$at", Moments.Write(save.At)), ("$landed", save.Landed)));

    /// <summary>Marks a save landed, saying how the test found it.</summary>
    public void Landed(string id, string how) => Write(write => write.Execute("UPDATE tree_saves SET landed = $how WHERE id = $id", ("$id", id), ("$how", how)));

    /// <summary>The save kept under <paramref name="id"/>, the ref with or without its namespace.</summary>
    public TreeSave? Of(string id)
    {
        var full = id.StartsWith(TreeSaves.Namespace, StringComparison.Ordinal) ? id : TreeSaves.Namespace + id;
        return Read($"SELECT {Columns} FROM tree_saves WHERE id = $id", ("$id", full)).FirstOrDefault();
    }

    /// <summary>The newest save of the tree at <paramref name="tree"/>, or null.</summary>
    public TreeSave? LatestOf(string tree) =>
        Read($"SELECT {Columns} FROM tree_saves WHERE tree = $tree AND dropped IS NULL ORDER BY at DESC, id DESC LIMIT 1", ("$tree", Key(tree))).FirstOrDefault();

    /// <summary>The saves not yet found landed nor dropped: of one repository, or of every one.</summary>
    public IReadOnlyList<TreeSave> Pending(string? workspace = null, string? repository = null) => Read(
        $"SELECT {Columns} FROM tree_saves WHERE landed IS NULL AND dropped IS NULL"
        + (repository is null ? "" : " AND workspace = $ws AND repository = $repo") + " ORDER BY at",
        ("$ws", workspace), ("$repo", repository));

    /// <summary>A tree's path as its record keys it: full, with no trailing separator.</summary>
    internal static string Key(string tree) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(tree));

    private IReadOnlyList<TreeSave> Read(string sql, params (string Name, object? Value)[] values)
    {
        try
        {
            using var store = DesktopStoreReader.Open(Location);
            if (store is null || store.Version < DesktopStore.TreeLedgerVersion) return [];
            return store.Query(sql, Row, values);
        }
        catch (Exception error) when (error is SqliteException or IOException or UnauthorizedAccessException or FormatException or JsonException)
        {
            return [];
        }
    }

    private void Write(Action<DesktopWrite> work)
    {
        try
        {
            using var store = DesktopStore.Open(Location, StoreProcess.Options(settled ?? StartConfirmation.Read));
            if (store.Stamp.Version < DesktopStore.TreeLedgerVersion) throw new DriverException("this store keeps no tree saves yet.");
            store.Write(Family, work);
        }
        catch (Exception error) when (error is DesktopStoreException or SqliteException or IOException or UnauthorizedAccessException)
        {
            throw new DriverException($"a tree's save record could not be written: {error.Message}");
        }
    }

    private static TreeSave Row(DbDataReader row) => new(
        row.GetString(0), row.GetString(1), row.GetString(2), row.GetString(3), row.GetString(4), row.IsDBNull(5) ? null : row.GetString(5),
        row.GetString(6), row.GetString(7), row.GetString(8), row.GetString(9),
        JsonSerializer.Deserialize<List<string>>(row.GetString(10), Shape) ?? [],
        JsonSerializer.Deserialize<List<SavedFile>>(row.GetString(11), Shape) ?? [], row.GetInt64(12), row.GetInt64(13),
        DateTimeOffset.Parse(row.GetString(14), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind))
    {
        Landed = row.IsDBNull(15) ? null : row.GetString(15),
    };
}
