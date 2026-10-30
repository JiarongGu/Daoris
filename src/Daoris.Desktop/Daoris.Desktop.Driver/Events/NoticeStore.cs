using System.Globalization;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;

namespace Daoris.Driver;

/// <summary>What <see cref="Notices.ImportFile"/> came to.</summary>
public enum NoticesImported
{
    /// <summary>The store holds the file's notices now.</summary>
    Done,

    /// <summary>An earlier import did it: the store is this home's record.</summary>
    Already,

    /// <summary>There is no notices file to import.</summary>
    NoFile,

    /// <summary>The file is from a newer build, and is left as it is.</summary>
    Unread,

    /// <summary>The start's confirmation is not settled, or the store would not take it; the file stays the record.</summary>
    Held,
}

/// <summary>The outcome of one import and, where held, why.</summary>
public sealed record NoticesImport(NoticesImported Outcome, string? Why = null);

/// <summary>
/// The notices in the desktop store (FOUNDATION1 z-v, D212; the design §13.6): <c>data/notices.json</c> imported once into <c>notices</c> and
/// left in place as the backup, so a build before the store reads the file as it stood. A home keeps its notices in the store from its first
/// import (an <c>imports</c> receipt of this family); before it, and in a home with no store, <see cref="Notices"/> reads and writes the
/// file as it always did. A file a later build wrote inside a notice keeps no such field in the store, which the file's backup still holds.
/// </summary>
internal static class NoticeStore
{
    public const string Family = "notices";

    private const string Moment = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    private const string Columns = "id, kind, at, read, workspace, session, quest, repository, state, route, act, code, count";

    private static string Location(string home) => DesktopStore.LocationIn(home);

    /// <summary>Whether this home's notices are the store's: it is there at the build's version and holds an import of this family.</summary>
    public static bool InUse(string home)
    {
        try
        {
            using var store = DesktopStoreReader.Open(Location(home));
            return store is { Version: >= 9 }
                   && store.Query("SELECT 1 FROM imports WHERE family = $family LIMIT 1", row => row.GetInt32(0), ("$family", Family)).Count > 0;
        }
        catch (Exception error) when (error is SqliteException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>The store's notices younger than the week; null where the store would not be read this moment.</summary>
    public static IReadOnlyList<Notice>? Read(string home, DateTimeOffset now)
    {
        try
        {
            using var store = DesktopStoreReader.Open(Location(home));
            if (store is null) return null;
            return [.. Notices.Young(store.Query($"SELECT {Columns} FROM notices", Map), now)];
        }
        catch (Exception error) when (error is SqliteException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// One write: the notices read, <paramref name="change"/> made onto them, pruned by age and count, and only what differs written, in
    /// one transaction. Nothing is written, and no revision moves, where nothing changed. Null where written; else why not.
    /// </summary>
    public static string? Edit(string home, DateTimeOffset now, Func<Dictionary<string, Notice>, bool> change, Func<StoreSettled>? settled = null)
    {
        try
        {
            using var store = DesktopStore.Open(Location(home), StoreProcess.Options(settled ?? StartConfirmation.Read));
            if (Planned(store.Query($"SELECT {Columns} FROM notices", Map), now, change) is null) return null;
            store.Write(Family, write =>
            {
                var plan = Planned(write.Query($"SELECT {Columns} FROM notices", Map), now, change);
                if (plan is null) return;
                foreach (var id in plan.Gone) write.Execute("DELETE FROM notices WHERE id = $id", ("$id", id));
                foreach (var notice in plan.Put) Upsert(write, notice);
            });
            HomeWrites.Mine(DesktopStore.WakeOf(Location(home)));
            return null;
        }
        catch (Exception error) when (error is DesktopStoreException or SqliteException or IOException or UnauthorizedAccessException)
        {
            return error.Message;
        }
    }

    /// <summary>The rows the store holds, their kept set and what differs, or null where <paramref name="change"/> and the prune move nothing.</summary>
    private static Plan? Planned(IReadOnlyList<Notice> rows, DateTimeOffset now, Func<Dictionary<string, Notice>, bool> change)
    {
        var read = rows.ToDictionary(notice => notice.Id, StringComparer.Ordinal);
        var notices = new Dictionary<string, Notice>(read, StringComparer.Ordinal);
        change(notices);
        var kept = Notices.Young(notices.Values, now).Take(Notices.Most).ToDictionary(notice => notice.Id, StringComparer.Ordinal);
        var gone = read.Keys.Where(id => !kept.ContainsKey(id)).ToList();
        var put = kept.Values.Where(notice => !read.TryGetValue(notice.Id, out var held) || held != notice).ToList();
        return gone.Count == 0 && put.Count == 0 ? null : new Plan(gone, put);
    }

    /// <summary>
    /// Import <c>notices.json</c> once the start's confirmation is settled, under the file's keep lock, so no file writer is between its
    /// read and its rename. A file that does not read is set aside under its dated name, as the writer does, and the store begins empty;
    /// one from a newer build is left as it is and nothing is imported.
    /// </summary>
    public static NoticesImport Import(string home, DateTimeOffset now, Func<StoreSettled>? settled)
    {
        var path = Notices.PathOf(home);
        if (!File.Exists(path)) return new(NoticesImported.NoFile);
        settled ??= StartConfirmation.Read;
        if (settled() is { Is: false } waiting) return new(NoticesImported.Held, waiting.Said ?? "the start's confirmation is not settled");
        try
        {
            using var held = KeepLock.Take(path, null);
            if (InUse(home)) return new(NoticesImported.Already);
            byte[] bytes;
            try
            {
                bytes = AtomicFile.ReadBytes(path);
            }
            catch (FileNotFoundException)
            {
                return new(NoticesImported.NoFile);
            }

            var record = Notices.ParseRecord(System.Text.Encoding.UTF8.GetString(bytes));
            if (record is { Newer: true }) return new(NoticesImported.Unread);
            var receipt = Path.GetRelativePath(home, path).Replace('\\', '/');
            var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            var notices = record?.Notices.Values.ToList() ?? [];
            using var store = DesktopStore.Open(Location(home), StoreProcess.Options(settled));
            var outcome = store.Import(
                new StoreImport(Family, receipt, bytes.Length, sha, notices.Count, ImportClass.Left),
                write =>
                {
                    foreach (var notice in notices) Upsert(write, notice);
                });
            if (!outcome.Done) return new(NoticesImported.Held, outcome.Said ?? "the store would not take it");
            if (record is null) Notices.SetAside(path, now);
            return new(NoticesImported.Done);
        }
        catch (Exception error) when (error is DesktopStoreException or SqliteException or DriverException or IOException or UnauthorizedAccessException)
        {
            return new(NoticesImported.Held, error.Message);
        }
    }

    private static void Upsert(DesktopWrite write, Notice notice) =>
        write.Execute(
            $"""
            INSERT INTO notices ({Columns}) VALUES ($id, $kind, $at, $read, $workspace, $session, $quest, $repository, $state, $route, $act, $code, $count)
            ON CONFLICT (id) DO UPDATE SET kind = excluded.kind, at = excluded.at, read = excluded.read, workspace = excluded.workspace,
              session = excluded.session, quest = excluded.quest, repository = excluded.repository, state = excluded.state,
              route = excluded.route, act = excluded.act, code = excluded.code, count = excluded.count
            """,
            ("$id", notice.Id), ("$kind", notice.Kind), ("$at", notice.At.UtcDateTime.ToString(Moment, CultureInfo.InvariantCulture)),
            ("$read", notice.Read ? 1 : 0), ("$workspace", notice.Workspace), ("$session", notice.Session), ("$quest", notice.Quest),
            ("$repository", notice.Repository), ("$state", notice.State), ("$route", notice.Route), ("$act", notice.Act),
            ("$code", notice.Code), ("$count", notice.Count));

    private static Notice Map(SqliteDataReader row)
    {
        string? Text(int at) => row.IsDBNull(at) ? null : row.GetString(at);
        return new(
            row.GetString(0), row.GetString(1),
            DateTimeOffset.Parse(row.GetString(2), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal),
            row.GetInt32(3) != 0, Text(4), Text(5), Text(6), Text(7), Text(8), Text(9), Text(10), Text(11),
            row.IsDBNull(12) ? null : row.GetInt32(12));
    }

    private sealed record Plan(List<string> Gone, List<Notice> Put);
}
