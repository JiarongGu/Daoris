using Microsoft.Data.Sqlite;

namespace Daoris.Driver;

/// <summary>
/// What the person keeps of a conversation, one row each in the desktop store by the conversation's id (a record's <c>line</c>, else
/// its own id): the name they gave it, when they pinned it, when they archived it, the earlier conversation it started from with
/// what it is handed, and the places it spoke under. Every conversation's, Ask Daoris's and a session's alike.
/// </summary>
/// <remarks>
/// <para><b>This machine's, never the record's.</b> The record is the service's and travels; what one person keeps in their list is not
/// a fact about the work.</para>
/// <para><b>A store that does not read is nothing kept</b>: a conversation lists by its question, an archive mark lost costs a row back in
/// the list, never a row gone. A write the store refuses is a <see cref="DriverException"/>, never a change that seems made.</para>
/// <para><b>A row holding nothing is removed</b>, so a conversation never touched keeps no row.</para>
/// </remarks>
public sealed class ConversationFacts(string home, Func<StoreSettled>? settled = null)
{
    public const string Family = "conversation_facts";

    /// <summary>A name's longest: a title, not a note.</summary>
    public const int NameLimit = 80;

    private const string Columns = "conversation, name, pinned, archived, from_start, handed, places";

    // One writer at a time in this process, so a rename and a pin pressed together each keep the other's field; the store's lock holds the other processes.
    private static readonly Lock Gate = new();

    private string Location => DesktopStore.LocationIn(home);

    /// <summary>What is kept of <paramref name="conversation"/>; <see cref="HelpKept.None"/> for nothing, or a store that does not read.</summary>
    public HelpKept Read(string conversation) =>
        Names(conversation) && Read([conversation]).TryGetValue(conversation, out var kept) ? kept : HelpKept.None;

    /// <summary>What is kept of each of <paramref name="conversations"/> that keeps anything, in one read.</summary>
    public IReadOnlyDictionary<string, HelpKept> Read(IReadOnlyCollection<string> conversations)
    {
        var wanted = conversations.Where(Names).Distinct(StringComparer.Ordinal).ToList();
        if (wanted.Count == 0) return new Dictionary<string, HelpKept>();
        return Query($"SELECT {Columns} FROM conversation_facts WHERE conversation IN ({string.Join(",", wanted.Select((_, at) => "$c" + at))})",
            [.. wanted.Select((id, at) => ("$c" + at, (object?)id))]);
    }

    /// <summary>Every conversation that is archived, and when.</summary>
    public IReadOnlyDictionary<string, DateTimeOffset> Archives() =>
        Query($"SELECT {Columns} FROM conversation_facts WHERE archived IS NOT NULL")
            .Where(pair => pair.Value.Archived is not null)
            .ToDictionary(pair => pair.Key, pair => pair.Value.Archived!.Value, StringComparer.Ordinal);

    /// <summary>Name a conversation, or clear its name with null or blank words: its first question is its title again.</summary>
    /// <exception cref="DriverException">An id that names no conversation, or a name that is not one short line.</exception>
    public void Rename(string conversation, string? name)
    {
        var named = name?.Trim() is { Length: > 0 } words ? words : null;
        if (named is not null && (named.Contains('\n') || named.Contains('\r')))
        {
            throw new DriverException("a conversation's name is one line; it was not renamed.");
        }

        if (named is { Length: > NameLimit })
        {
            throw new DriverException($"a conversation's name is at most {NameLimit} characters; this one is {named.Length}, and it was not renamed.");
        }

        Change(conversation, kept => kept with { Name = named });
    }

    /// <summary>Pin a conversation to the top of the list, or unpin it, which puts it back among the rest.</summary>
    public void Pin(string conversation, bool pinned, DateTimeOffset now) =>
        Change(conversation, kept => kept with { Pinned = pinned ? kept.Pinned ?? now : null });

    /// <summary>Say that a conversation started from an earlier one, and what it is handed of it.</summary>
    public void StartedFrom(string conversation, string from, string handed) =>
        Change(conversation, kept => kept with { From = Checked(from), Handed = handed });

    /// <summary>Archive these conversations, each keeping its first archived time.</summary>
    public void Archive(IEnumerable<string> conversations, DateTimeOffset now)
    {
        foreach (var conversation in conversations.Distinct(StringComparer.Ordinal))
        {
            Change(conversation, kept => kept with { Archived = kept.Archived ?? now });
        }
    }

    /// <summary>Take these conversations' archive marks away; the ones that were not archived, named, and nothing written for them.</summary>
    public IReadOnlyList<string> Unarchive(IEnumerable<string> conversations)
    {
        var asked = conversations.Distinct(StringComparer.Ordinal).ToList();
        var marked = Read(asked);
        var notArchived = asked.Where(id => marked.GetValueOrDefault(id)?.Archived is null).ToList();
        foreach (var id in asked.Except(notArchived)) Change(id, kept => kept with { Archived = null });
        return notArchived;
    }

    /// <summary>Remove these conversations' rows whole, as a delete or a clear of the conversation does; the ones that held a row, named.</summary>
    public IReadOnlyList<string> Forget(IEnumerable<string> conversations)
    {
        var ids = conversations.Where(Names).Distinct(StringComparer.Ordinal).ToList();
        var gone = new List<string>();
        if (ids.Count == 0) return gone;
        Keep(write =>
        {
            gone.Clear();
            foreach (var id in ids)
            {
                if (write.Execute("DELETE FROM conversation_facts WHERE conversation = $id", ("$id", id)) > 0) gone.Add(id);
            }
        });
        return gone;
    }

    /// <summary>Edit what is kept of a conversation under the one writer's gate; a row left holding nothing is removed.</summary>
    /// <exception cref="DriverException">An id that names no conversation, or a store that will not take the write.</exception>
    public void Change(string conversation, Func<HelpKept, HelpKept> edit)
    {
        Checked(conversation);
        Keep(write =>
        {
            var before = write.Query($"SELECT {Columns} FROM conversation_facts WHERE conversation = $id", Row, ("$id", conversation)).FirstOrDefault().Value
                ?? HelpKept.None;
            var next = edit(before);
            if (next == before) return;
            if (next == HelpKept.None)
            {
                write.Execute("DELETE FROM conversation_facts WHERE conversation = $id", ("$id", conversation));
                return;
            }

            write.Execute(
                $"""
                INSERT INTO conversation_facts ({Columns}) VALUES ($id, $name, $pinned, $archived, $from, $handed, $places)
                ON CONFLICT (conversation) DO UPDATE SET name = excluded.name, pinned = excluded.pinned, archived = excluded.archived,
                  from_start = excluded.from_start, handed = excluded.handed, places = excluded.places
                """,
                ("$id", conversation), ("$name", next.Name), ("$pinned", Moments.Write(next.Pinned)), ("$archived", Moments.Write(next.Archived)),
                ("$from", next.From), ("$handed", next.Handed), ("$places", ConversationPlaces.Write(next)));
        });
    }

    /// <summary>An id that names a conversation, or the refusal: nothing under the home is reached through any other.</summary>
    private static string Checked(string conversation) =>
        Names(conversation) ? conversation : throw new DriverException($"`{conversation}` is not a session id, so it names no conversation.");

    /// <summary>A session's id, or a teammate's record's <c>machine/id</c>, which this machine may archive too.</summary>
    private static bool Names(string conversation) =>
        SessionEventText.IsId(conversation)
        || (conversation.Split('/') is [var origin, var id] && SessionEventText.IsId(origin) && SessionEventText.IsId(id));

    private void Keep(Action<DesktopWrite> work)
    {
        lock (Gate)
        {
            try
            {
                using var store = DesktopStore.Open(Location, StoreProcess.Options(settled ?? StartConfirmation.Read));
                store.Write(Family, work);
            }
            catch (Exception error) when (error is DesktopStoreException or SqliteException or IOException or UnauthorizedAccessException)
            {
                throw new DriverException($"what is kept of a conversation could not be written: {error.Message}");
            }
        }
    }

    private IReadOnlyDictionary<string, HelpKept> Query(string sql, params (string Name, object? Value)[] values)
    {
        try
        {
            using var store = DesktopStoreReader.Open(Location);
            if (store is null || store.Version < DesktopStore.ConversationFactsVersion) return new Dictionary<string, HelpKept>();
            return store.Query(sql, Row, values).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        }
        catch (Exception error) when (error is SqliteException or IOException or UnauthorizedAccessException or FormatException)
        {
            return new Dictionary<string, HelpKept>();
        }
    }

    private static KeyValuePair<string, HelpKept> Row(SqliteDataReader row)
    {
        string? Text(int at) => row.IsDBNull(at) ? null : row.GetString(at);
        var kept = new HelpKept(Text(1), Moments.Read(Text(2)), Text(4), Text(5)) { Archived = Moments.Read(Text(3)) };
        return new(row.GetString(0), ConversationPlaces.Read(kept, Text(6)));
    }
}
