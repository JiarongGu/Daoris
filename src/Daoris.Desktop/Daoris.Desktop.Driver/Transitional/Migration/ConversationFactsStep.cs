using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Daoris.Driver;

/// <summary>
/// What an older build kept of conversations in two places, into <see cref="ConversationFacts"/> once: Ask Daoris's
/// <c>&lt;session&gt;.help.json</c> files (name, pin, focus and its marks, the earlier conversation one started from), and the
/// archive marks of <c>archived.json</c>, a mark per record. A copy of each file is kept first and the file is taken out, so nothing
/// reads one after.
/// </summary>
/// <remarks>
/// <para><b>A conversation is archived where its newest part was marked</b>, at that mark's time, its parts read from the ledger's
/// <c>line</c>; a mark on an older part alone is dropped, since the list showed the newest. Where the ledger holds no record of a marked
/// session, or none of its parts, the session is its own conversation.</para>
/// <para><b>Run again it changes nothing</b>: a fact already kept stands, an older file only fills what is not kept yet.</para>
/// <para>It ends when no supported home holds either file; the row that deletes it is CONVO1's.</para>
/// </remarks>
public sealed class ConversationFactsStep : IHomeMigrationStep
{
    private const string HelpSuffix = ".help.json";
    private const string ArchiveFile = "archived.json";

    private const string StepName = "conversation-facts";

    public string Name => StepName;

    public void Run(HomeMigrationRun run)
    {
        var folder = DaorisHome.Entry(run.Home, HomeEntry.Sessions);
        if (!Directory.Exists(folder)) return;
        var helps = Directory.EnumerateFiles(folder, "*" + HelpSuffix).Order(StringComparer.Ordinal).ToList();
        var archive = Path.Combine(folder, ArchiveFile);
        if (helps.Count == 0 && !File.Exists(archive)) return;

        run.Keep(DesktopStore.LocationIn(run.Home));
        var facts = new ConversationFacts(run.Home, () => StoreSettled.Yes);
        foreach (var path in helps)
        {
            Import(facts, path);
            run.Keep(path);
            File.Delete(path);
            run.Said(path);
            run.Count(Name, "help-files");
        }

        if (!File.Exists(archive)) return;
        ImportMarks(run, facts, archive);
        run.Keep(archive);
        File.Delete(archive);
        run.Said(archive);
    }

    private static void Import(ConversationFacts facts, string path)
    {
        var session = Path.GetFileName(path)[..^HelpSuffix.Length];
        if (!SessionEventText.IsId(session)) return;
        HelpKept old;
        try
        {
            using var document = JsonDocument.Parse(AtomicFile.ReadText(path));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return;
            old = ConversationPlaces.Read(
                new HelpKept(Json.NonEmpty(root, "name"), Moments.Read(root, "pinned"), Json.NonEmpty(root, "from"), Json.NonEmpty(root, "handed")), root);
        }
        catch (JsonException)
        {
            // A file that does not read was nothing kept; its copy stays in the salvage folder.
            return;
        }

        facts.Change(session, now => now with
        {
            Name = now.Name ?? old.Name,
            Pinned = now.Pinned ?? old.Pinned,
            From = now.From ?? old.From,
            Handed = now.Handed ?? old.Handed,
            Focus = now.Focus ?? old.Focus,
            FocusNext = now.FocusNext ?? old.FocusNext,
            Foci = now.Foci ?? old.Foci,
            Marks = now.Marks.Count > 0 ? now.Marks : old.Marks,
        });
    }

    private static void ImportMarks(HomeMigrationRun run, ConversationFacts facts, string path)
    {
        var marks = new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal);
        try
        {
            using var document = JsonDocument.Parse(AtomicFile.ReadText(path));
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("archived", out var listed) && listed.ValueKind == JsonValueKind.Array)
            {
                foreach (var mark in listed.EnumerateArray().Where(each => each.ValueKind == JsonValueKind.Object))
                {
                    if (Json.NonEmpty(mark, "session") is not { } session) continue;
                    var at = Moments.Read(mark, "at") ?? DateTimeOffset.MinValue;
                    marks[session] = marks.TryGetValue(session, out var first) && first < at ? first : at;
                }
            }
        }
        catch (JsonException)
        {
            // A file that does not read was no mark: a row back in the list, never a row gone.
            return;
        }

        var parts = Parts(run.Home);
        foreach (var (conversation, at) in Conversations(marks, parts))
        {
            facts.Change(conversation, now => now with { Archived = now.Archived ?? at });
            run.Count(StepName, "archived");
        }
    }

    /// <summary>Each conversation whose newest part was marked, with that mark's time.</summary>
    private static IEnumerable<(string Conversation, DateTimeOffset At)> Conversations(
        IReadOnlyDictionary<string, DateTimeOffset> marks, IReadOnlyDictionary<string, (string Line, string Created)> parts)
    {
        var newest = parts.GroupBy(part => part.Value.Line, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(part => part.Value.Created, StringComparer.Ordinal).ThenBy(part => part.Key, StringComparer.Ordinal).Last().Key,
                StringComparer.Ordinal);
        foreach (var (session, at) in marks)
        {
            if (!parts.TryGetValue(session, out var part)) yield return (session, at);
            else if (newest[part.Line] == session) yield return (part.Line, at);
        }
    }

    /// <summary>Every record's id with its conversation and when it began, from the ledger; none where the ledger is not there or does not read.</summary>
    private static Dictionary<string, (string Line, string Created)> Parts(string home)
    {
        var parts = new Dictionary<string, (string, string)>(StringComparer.Ordinal);
        var path = DaorisHome.Entry(home, HomeEntry.Knowledge);
        if (!File.Exists(path)) return parts;
        try
        {
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
            connection.Open();
            using var probe = connection.CreateCommand();
            probe.CommandText = "SELECT COUNT(*) FROM pragma_table_info('sessions') WHERE name IN ('id', 'line', 'created')";
            if (Convert.ToInt32(probe.ExecuteScalar(), CultureInfo.InvariantCulture) < 3) return parts;
            using var read = connection.CreateCommand();
            read.CommandText = "SELECT id, line, created FROM sessions WHERE line IS NOT NULL";
            using var reader = read.ExecuteReader();
            while (reader.Read()) parts[reader.GetString(0)] = (reader.GetString(1), reader.GetString(2));
        }
        catch (SqliteException)
        {
            // A ledger that does not read gives no parts: each marked session is then its own conversation.
        }

        return parts;
    }
}
