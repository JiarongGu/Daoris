using System.Globalization;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// A tree an abandon discarded, or a branch it deleted alone (design §4.2): never its path, and its tip, which brings the
/// branch back while git keeps its commits (<c>git branch &lt;branch&gt; &lt;tip&gt;</c> in its repository).
/// </summary>
/// <param name="Sessions">The sessions that worked in it, oldest first.</param>
/// <param name="Alone">A branch whose tree was already gone, deleted on its own.</param>
public sealed record AbandonedTree(
    string Repository, string Branch, string? Tip, int? Commits, int? Uncommitted, IReadOnlyList<string> Sessions, bool Alone = false);

/// <summary>A piece an abandon kept, and why (design §3.2), in words from fixed lists: <see cref="AbandonWhy"/>.</summary>
/// <param name="Piece">The piece's key (<see cref="WorkAbandoning.QuestKey"/>, <see cref="WorkAbandoning.SessionKey"/>, <see cref="WorkAbandoning.TreeKey"/>).</param>
public sealed record AbandonKeep(string Piece, string Why)
{
    /// <summary>For a quest taken on another machine, or a teammate's session, the machine where it is known.</summary>
    public string? Machine { get; init; }

    /// <summary>For a tree whose commits are elsewhere, a ref that holds one, as git names it short.</summary>
    public string? Where { get; init; }

    /// <summary>
    /// For a piece listed to go that was kept, whether it changed since the list (true) or a step could not take it (false,
    /// with the service's or git's sentence in <see cref="Detail"/>); null for a piece the list itself kept.
    /// </summary>
    public bool? Changed { get; init; }

    /// <summary>What refused it, in the service's or git's words: for the terminal and the log of what was tried. Never a path in the record.</summary>
    public string? Detail { get; init; }
}

/// <summary>A shared decline's answer from the abandon's sync pass (design §5.2): <see cref="DeclineAnswer"/>.</summary>
public sealed record AbandonDecline(string Quest, string Answer);

/// <summary>The answers a shared decline gets from the abandon's pass (D132 point 10).</summary>
public static class DeclineAnswer
{
    /// <summary>The remote took it.</summary>
    public const string Confirmed = "confirmed";

    /// <summary>Another machine's take reached the remote first: it stands, and the decline is a conflict on the quest.</summary>
    public const string Lost = "lost";

    /// <summary>The pass did not reach the remote, or left it behind: it travels on the next pass, where the same rule applies.</summary>
    public const string Unconfirmed = "unconfirmed";
}

/// <summary>One abandon, as this machine keeps it (design §4.2): the scope, when, by which door, the reason, what went and what stayed.</summary>
/// <param name="Scope"><c>ask</c> or <c>quest</c>.</param>
/// <param name="Door"><c>screen</c> or <c>terminal</c>.</param>
/// <param name="Reason">The person's reason, verbatim, as each declined quest keeps it.</param>
public sealed record AbandonEntry(string Scope, string Id, DateTimeOffset At, string Door, string Reason)
{
    /// <summary>Each quest declined.</summary>
    public IReadOnlyList<string> Declined { get; init; } = [];

    /// <summary>Whether the ask was closed with the reason (ask scope).</summary>
    public bool Closed { get; init; }

    /// <summary>Each tree discarded with its branch, and each branch deleted alone (<see cref="AbandonedTree.Alone"/>).</summary>
    public IReadOnlyList<AbandonedTree> Trees { get; init; } = [];

    /// <summary>Each session stopped, or ended unanswered.</summary>
    public IReadOnlyList<string> Stopped { get; init; } = [];

    /// <summary>Each session archived.</summary>
    public IReadOnlyList<string> Archived { get; init; } = [];

    /// <summary>Each piece kept, and why.</summary>
    public IReadOnlyList<AbandonKeep> Stayed { get; init; } = [];

    /// <summary>Each shared decline's answer.</summary>
    public IReadOnlyList<AbandonDecline> Declines { get; init; } = [];
}

/// <summary>
/// This machine's record of what each abandon took and kept (PAUSE1d, D132 point 9, design §4.2):
/// <c>&lt;home&gt;/abandoned.json</c>, written atomically by the driver library at both doors, as <c>landings.json</c> is
/// (D102).
/// </summary>
/// <remarks>
/// <para><b>Under the home, not on the record.</b> The trees, the branches and their tips are this machine's facts, and a
/// quest's record travels (D47 §4). An ask's does not, but one home for both scopes keeps one reader.</para>
///
/// <para><b>No path, and no conversation's words.</b> A tree is its repository and branch; the reason is the person's
/// answer, which every declined quest carries too.</para>
///
/// <para><b>A missing or unreadable file is no record</b>, and the abandon still abandons. An entry stays while its ask or
/// quest has a record here: it is the only trace of a discarded tree, and it is small.</para>
/// </remarks>
public sealed class AbandonRecord(string home)
{
    public const string FileName = "abandoned.json";

    // One writer at a time in this process: two presses would otherwise read the same file, and the second drop the first.
    private static readonly object Gate = new();

    public string FilePath => Path.Combine(home, FileName);

    /// <summary>Every entry, oldest first; none when the file is missing or does not read.</summary>
    public IReadOnlyList<AbandonEntry> Entries()
    {
        try
        {
            if (!File.Exists(FilePath)) return [];
            using var document = JsonDocument.Parse(File.ReadAllText(FilePath));
            if (!document.RootElement.TryGetProperty("abandoned", out var all) || all.ValueKind != JsonValueKind.Array) return [];
            return [.. all.EnumerateArray().Select(Read).OfType<AbandonEntry>()];
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return [];
        }
    }

    /// <summary>The newest abandon of this ask or quest here, or null: what its page shows as *What went* and *What stayed*.</summary>
    public AbandonEntry? Last(WorkScope scope, string id) =>
        Entries().LastOrDefault(entry => entry.Scope == WorkPausing.Word(scope)
            && string.Equals(entry.Id, id.Trim().TrimStart('#'), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Keep one more entry, dropping those whose ask or quest no longer has a record here.
    /// </summary>
    /// <param name="known">Whether an entry's ask or quest still has a record here; null keeps every entry.</param>
    public void Write(AbandonEntry entry, Func<AbandonEntry, bool>? known = null)
    {
        lock (Gate)
        {
            var kept = Entries().Where(each => known is null || known(each)).Append(entry).ToList();
            Directory.CreateDirectory(home);
            AtomicFile.WriteText(FilePath, ToJson(kept));
        }
    }

    /// <summary>Written by hand, as the driver's other files are, for the AOT reason <see cref="DriverConfig"/> gives.</summary>
    private static string ToJson(IReadOnlyList<AbandonEntry> entries)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteStartArray("abandoned");
            foreach (var entry in entries)
            {
                writer.WriteStartObject();
                writer.WriteString("scope", entry.Scope);
                writer.WriteString("id", entry.Id);
                writer.WriteString("at", entry.At.ToString("O", CultureInfo.InvariantCulture));
                writer.WriteString("door", entry.Door);
                writer.WriteString("reason", entry.Reason);
                Strings(writer, "declined", entry.Declined);
                if (entry.Closed) writer.WriteBoolean("closed", true);
                writer.WriteStartArray("trees");
                foreach (var tree in entry.Trees)
                {
                    writer.WriteStartObject();
                    writer.WriteString("repository", tree.Repository);
                    writer.WriteString("branch", tree.Branch);
                    if (tree.Tip is not null) writer.WriteString("tip", tree.Tip);
                    if (tree.Commits is { } commits) writer.WriteNumber("commits", commits);
                    if (tree.Uncommitted is { } uncommitted) writer.WriteNumber("uncommitted", uncommitted);
                    Strings(writer, "sessions", tree.Sessions);
                    if (tree.Alone) writer.WriteBoolean("alone", true);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                Strings(writer, "stopped", entry.Stopped);
                Strings(writer, "archived", entry.Archived);
                writer.WriteStartArray("stayed");
                foreach (var keep in entry.Stayed)
                {
                    writer.WriteStartObject();
                    writer.WriteString("piece", keep.Piece);
                    writer.WriteString("why", keep.Why);
                    if (keep.Machine is not null) writer.WriteString("machine", keep.Machine);
                    if (keep.Where is not null) writer.WriteString("where", keep.Where);
                    if (keep.Changed is { } changed) writer.WriteBoolean("changed", changed);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteStartArray("declines");
                foreach (var decline in entry.Declines)
                {
                    writer.WriteStartObject();
                    writer.WriteString("quest", decline.Quest);
                    writer.WriteString("answer", decline.Answer);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n") + "\n";
    }

    private static void Strings(Utf8JsonWriter writer, string name, IReadOnlyList<string> values)
    {
        writer.WriteStartArray(name);
        foreach (var value in values) writer.WriteStringValue(value);
        writer.WriteEndArray();
    }

    /// <summary>One entry, or null where it lacks what makes it one: a scope, an id, a door and a reason.</summary>
    private static AbandonEntry? Read(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        var scope = Text(element, "scope");
        var id = Text(element, "id");
        var door = Text(element, "door");
        var reason = Text(element, "reason");
        if (scope is not ("ask" or "quest") || id is null || door is null || reason is null) return null;
        var at = DateTimeOffset.TryParse(Text(element, "at"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var when)
            ? when
            : DateTimeOffset.MinValue;
        return new AbandonEntry(scope, id, at, door, reason)
        {
            Declined = Strings(element, "declined"),
            Closed = element.TryGetProperty("closed", out var closed) && closed.ValueKind == JsonValueKind.True,
            Trees = [.. Objects(element, "trees").Select(tree => Text(tree, "repository") is { } repository && Text(tree, "branch") is { } branch
                ? new AbandonedTree(repository, branch, Text(tree, "tip"), Number(tree, "commits"), Number(tree, "uncommitted"),
                    Strings(tree, "sessions"), tree.TryGetProperty("alone", out var alone) && alone.ValueKind == JsonValueKind.True)
                : null).OfType<AbandonedTree>()],
            Stopped = Strings(element, "stopped"),
            Archived = Strings(element, "archived"),
            Stayed = [.. Objects(element, "stayed").Select(keep => Text(keep, "piece") is { } piece && Text(keep, "why") is { } why
                ? new AbandonKeep(piece, why)
                {
                    Machine = Text(keep, "machine"),
                    Where = Text(keep, "where"),
                    Changed = keep.TryGetProperty("changed", out var changed) && changed.ValueKind is JsonValueKind.True or JsonValueKind.False
                        ? changed.GetBoolean()
                        : null,
                }
                : null).OfType<AbandonKeep>()],
            Declines = [.. Objects(element, "declines").Select(decline => Text(decline, "quest") is { } quest && Text(decline, "answer") is { } answer
                ? new AbandonDecline(quest, answer)
                : null).OfType<AbandonDecline>()],
        };
    }

    private static IEnumerable<JsonElement> Objects(JsonElement element, string name) =>
        element.TryGetProperty(name, out var array) && array.ValueKind == JsonValueKind.Array
            ? array.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.Object)
            : [];

    private static IReadOnlyList<string> Strings(JsonElement element, string name) =>
        element.TryGetProperty(name, out var array) && array.ValueKind == JsonValueKind.Array
            ? [.. array.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString()!)]
            : [];

    private static int? Number(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : null;

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
