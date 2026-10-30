using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Daoris.Driver;

/// <summary>One piece of news that happened: its key, when, and the notice it leaves, if any.</summary>
/// <param name="Key">The occurrence's one key (§4.5): the store and journal position of a record event, a turn's session, run and moment, a Confirm's press, an account's cool-off.</param>
/// <param name="At">When it happened; one older than <see cref="News.Horizon"/> is never told.</param>
/// <param name="Notice">The notice it keeps in <i>Done and failed</i> or <i>Messages</i>; null for news that leaves none, a park.</param>
/// <param name="Renew">Whether the notice replaces an older one of its id (a conversation's later answer) and not only adds it.</param>
public sealed record NewsOccurrence(string Key, DateTimeOffset At, Notice? Notice = null, bool Renew = false);

/// <summary>What one claim came to.</summary>
/// <param name="Told">The occurrences claimed now, newest first: the ones to show.</param>
/// <param name="Refused">Why the store would not take the claim, in which case nothing is to be shown; else null.</param>
public sealed record NewsClaimed(IReadOnlyList<NewsOccurrence> Told, string? Refused = null);

/// <summary>
/// The one owner of the news (FOUNDATION1 m-i, the foundation design §4.3, §4.5, §19.6): every process that tells news, the lease's loop for
/// driven work and the shell for its conversations, submits what happened here, and a piece is told at most once. Its key is claimed in the
/// desktop store's <c>news_said</c>, durably, in the one transaction that keeps its notice and moves the teller's journal cursor, before a
/// balloon or a toast is shown: a crash between the claim and the showing loses that one announcement and never repeats it, and a claim the
/// store refuses shows nothing.
/// </summary>
/// <remarks>
/// <para><b>Apart from the visible notices</b>, so a prune or a dismissal never forgets what was told: the said-set is bounded by
/// <see cref="Horizon"/> alone, one week as the notices are, and a key older than it is forgotten with it, since nothing that old is told.</para>
/// <para><b>One cursor for each teller</b> (<c>journal_cursors</c>), with the store identity it was read against.</para>
/// </remarks>
/// <param name="home">The home whose store this is.</param>
/// <param name="settled">The reading of the start's confirmation; null reads this install's swap journal.</param>
public sealed class News(string home, Func<StoreSettled>? settled = null)
{
    /// <summary>The family's name in <c>store_revisions</c>.</summary>
    public const string Family = "news";

    /// <summary>How old an occurrence may be and still be told, and how long the said-set keeps a key.</summary>
    public static readonly TimeSpan Horizon = Notices.Week;

    /// <summary>The most one claim tells; the rest are claimed and not shown, as the notices keep at most this many.</summary>
    public const int Most = Notices.Most;

    private const string Moment = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    /// <summary>The key of a record event: the store's identity and the journal position, since a position alone carries no store (§4.5).</summary>
    public static string KeyOf(string store, long position) => $"journal:{store}:{position.ToString(CultureInfo.InvariantCulture)}";

    private DesktopStore Open() => DesktopStore.Open(DesktopStore.LocationIn(home), StoreProcess.Options(settled ?? StartConfirmation.Read));

    /// <summary>
    /// Claim <paramref name="occurrences"/> for <paramref name="teller"/>, and keep their notices and the teller's <paramref name="cursor"/>, in
    /// one transaction. Those younger than the horizon and not claimed before are told, at most <see cref="Most"/> of them, the newest;
    /// every eligible key is claimed, so the rest are never told later either.
    /// </summary>
    public NewsClaimed Claim(string teller, IReadOnlyList<NewsOccurrence> occurrences, JournalPosition? cursor, DateTimeOffset now)
    {
        try
        {
            using var store = Open();
            var inStore = NoticeStore.InUse(home);
            var told = store.Write(
                Family,
                write =>
                {
                    // A baseline's key is the branch's one first run and never expires with the horizon.
                    write.Execute(
                        "DELETE FROM news_said WHERE at < $before AND key NOT LIKE 'baseline:%'",
                        ("$before", (now - Horizon).UtcDateTime.ToString(Moment, CultureInfo.InvariantCulture)));
                    var claimed = new List<NewsOccurrence>();
                    foreach (var each in occurrences.Where(each => now - each.At < Horizon).OrderByDescending(each => each.At))
                    {
                        var won = write.Execute(
                            "INSERT OR IGNORE INTO news_said (key, at) VALUES ($key, $at)",
                            ("$key", each.Key), ("$at", each.At.UtcDateTime.ToString(Moment, CultureInfo.InvariantCulture))) == 1;
                        if (won) claimed.Add(each);
                    }

                    var shown = claimed.Take(Most).ToList();
                    if (inStore)
                    {
                        foreach (var each in shown.Where(each => each.Notice is not null)) NoticeStore.Put(write, each.Notice!, each.Renew);
                    }

                    if (cursor is not null) Keep(write, teller, cursor, now);
                    return shown;
                });

            // A home whose notices are still the file's (before their import) keeps them as it always did, after the claim.
            if (!inStore)
            {
                foreach (var each in told.Where(each => each.Notice is not null))
                {
                    _ = each.Renew ? Notices.Renew(home, each.Notice!, now) : Notices.Add(home, each.Notice!, now);
                }
            }

            return new NewsClaimed(told);
        }
        catch (Exception error) when (error is DesktopStoreException or SqliteException or IOException or UnauthorizedAccessException)
        {
            return new NewsClaimed([], error.Message);
        }
    }

    /// <summary>The key a notice's arrival is claimed by: its id and the moment it was written, so a notice renewed at a later answer is news again.</summary>
    public static string KeyOfNotice(Notice notice) => $"notice:{notice.Id}:{notice.At.UtcTicks.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>
    /// Whether this is the first run of branch <paramref name="branch"/> on this home (§4.5: only a home with no said-set yet takes what stands as
    /// said): true once, the call that claims its baseline key, so what stands then is claimed and told to no one, and false for every call after, in
    /// any process. A store that will not take it answers false, which tells what stands rather than swallow it silently.
    /// </summary>
    public bool First(string branch, DateTimeOffset now)
    {
        try
        {
            using var store = Open();
            return store.Write(
                Family,
                write => write.Execute(
                    "INSERT OR IGNORE INTO news_said (key, at) VALUES ($key, $at)",
                    ("$key", "baseline:" + branch), ("$at", now.UtcDateTime.ToString(Moment, CultureInfo.InvariantCulture))) == 1);
        }
        catch (Exception error) when (error is DesktopStoreException or SqliteException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string CoverKey(string session) => "parkcovers:" + session;

    /// <summary>A quest's park said, its last session's end is one piece of news with it: marked in the said-set, so the end is not told again (FOUNDATION1 z-v).</summary>
    public void Cover(IEnumerable<string> sessions, DateTimeOffset now)
    {
        try
        {
            using var store = Open();
            store.Write(
                Family,
                write =>
                {
                    foreach (var session in sessions)
                    {
                        write.Execute(
                            "INSERT OR IGNORE INTO news_said (key, at) VALUES ($key, $at)",
                            ("$key", CoverKey(session)), ("$at", now.UtcDateTime.ToString(Moment, CultureInfo.InvariantCulture)));
                    }
                });
        }
        catch (Exception error) when (error is DesktopStoreException or SqliteException or IOException or UnauthorizedAccessException)
        {
            // A cover not kept tells the end beside the park: a repeat, never a loss.
        }
    }

    /// <summary>Whether a said park covers <paramref name="session"/>'s end.</summary>
    public bool Covered(string session)
    {
        try
        {
            using var store = DesktopStoreReader.Open(DesktopStore.LocationIn(home));
            return store is { Version: >= 9 }
                && store.Query("SELECT 1 FROM news_said WHERE key = $key", row => row.GetInt32(0), ("$key", CoverKey(session))).Any();
        }
        catch (Exception error) when (error is SqliteException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>The place a teller read to, or null where it has told nothing on this home yet; the store's failure reads as none.</summary>
    public JournalPosition? Cursor(string teller)
    {
        try
        {
            using var store = DesktopStoreReader.Open(DesktopStore.LocationIn(home));
            if (store is not { Version: >= 9 }) return null;
            return store.Query(
                "SELECT store, position FROM journal_cursors WHERE teller = $teller",
                row => new JournalPosition(row.GetString(0), row.GetInt64(1)),
                ("$teller", teller)).FirstOrDefault();
        }
        catch (Exception error) when (error is SqliteException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Move a teller's cursor alone, for a batch that told nothing. Null where written; else why not.</summary>
    public string? Advance(string teller, JournalPosition cursor)
    {
        try
        {
            using var store = Open();
            store.Write(Family, write => Keep(write, teller, cursor, DateTimeOffset.UtcNow));
            return null;
        }
        catch (Exception error) when (error is DesktopStoreException or SqliteException or IOException or UnauthorizedAccessException)
        {
            return error.Message;
        }
    }

    private static void Keep(DesktopWrite write, string teller, JournalPosition cursor, DateTimeOffset now) =>
        write.Execute(
            """
            INSERT INTO journal_cursors (teller, store, position, at) VALUES ($teller, $store, $position, $at)
            ON CONFLICT (teller) DO UPDATE SET store = excluded.store, position = excluded.position, at = excluded.at
            """,
            ("$teller", teller), ("$store", cursor.Store), ("$position", cursor.Position),
            ("$at", now.UtcDateTime.ToString(Moment, CultureInfo.InvariantCulture)));
}
