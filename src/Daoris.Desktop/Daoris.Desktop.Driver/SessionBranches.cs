using System.Globalization;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// Where one session branch grew from (WSR6): the repository, the branch, the line it was opened on, the commit
/// it started at, and the branch that commit was the tip of when a chain's step grew from the step before's
/// (CHAIN2). What bringing the branch up to date cuts at, so only its own commits are replayed.
/// </summary>
/// <param name="Line">The repository's line when the tree was opened (D86), or null where git named none.</param>
/// <param name="From">The commit the branch started at: the line's tip, or the step before's tip. A branch whose history does not hold it is not this record's.</param>
/// <param name="GrewFrom">The step before's branch it grew from, or null where it grew from the line.</param>
public sealed record GrownBranch(
    string Repository, string Workspace, string Branch, string? Line, string From, string? GrewFrom, DateTimeOffset At);

/// <summary>
/// Where this machine's session branches grew from (WSR6): <c>&lt;home&gt;/session-branches.json</c>. Written
/// when a tree is opened and when bringing a branch up to date moves it; read by that same press.
/// </summary>
/// <remarks>
/// <para><b>Machine-local, under the home (D63), never in the repository</b> — the same reason as the landings'
/// record: which commit a tree on this machine started at is a fact about this machine. Git's own config would
/// hold it too, but that is a file of the person's repository, and Daoris writes nothing there to remember
/// its own trees.</para>
///
/// <para><b>Why a record, when the session record carries a base commit.</b> That base is where each session
/// began, and a session carried on in the same tree begins where the one before stopped: the tree's branch
/// has one start, and a chain's step starts on another branch's tip, which nothing else writes down.</para>
///
/// <para><b>A file that does not read is no record at all</b>, never a failure: opening a tree is owed its
/// answer, and a branch with no record is cut where its work first differs from the line instead.</para>
/// </remarks>
public sealed class SessionBranches(string home)
{
    public const string FileName = "session-branches.json";

    // One writer at a time in this process, as the landings' record has: a tick and a press in one shell.
    private static readonly object Gate = new();

    public string FilePath => Path.Combine(home, FileName);

    /// <summary>Every branch recorded, in the order they were written.</summary>
    public IReadOnlyList<GrownBranch> All()
    {
        try
        {
            if (!File.Exists(FilePath)) return [];
            using var document = JsonDocument.Parse(File.ReadAllText(FilePath));
            if (!document.RootElement.TryGetProperty("branches", out var branches) || branches.ValueKind != JsonValueKind.Array) return [];
            return [.. branches.EnumerateArray().Select(Read).OfType<GrownBranch>()];
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>The record of that branch in that repository, or null.</summary>
    public GrownBranch? Of(string repository, string branch) =>
        All().LastOrDefault(entry => Same(entry, repository, branch));

    /// <summary>Record one, replacing any entry for the same branch in the same repository.</summary>
    public void Record(GrownBranch entry) => Edit(all => [.. all.Where(each => !Same(each, entry.Repository, entry.Branch)), entry]);

    /// <summary>Forget branches that are gone, or no longer hold the commit they started at.</summary>
    public void Forget(string repository, IReadOnlyCollection<string> branches) => Edit(all =>
        [.. all.Where(each => !(string.Equals(each.Repository, repository, StringComparison.OrdinalIgnoreCase)
                                && branches.Contains(each.Branch, StringComparer.Ordinal)))]);

    private void Edit(Func<IReadOnlyList<GrownBranch>, IReadOnlyList<GrownBranch>> change)
    {
        lock (Gate)
        {
            Directory.CreateDirectory(home);
            AtomicFile.WriteText(FilePath, ToJson(change(All())));
        }
    }

    private static bool Same(GrownBranch entry, string repository, string branch) =>
        string.Equals(entry.Repository, repository, StringComparison.OrdinalIgnoreCase)
        && string.Equals(entry.Branch, branch, StringComparison.Ordinal);

    /// <summary>Written by hand, as the driver's other files are, for the AOT reason <see cref="DriverConfig"/> gives.</summary>
    private static string ToJson(IReadOnlyList<GrownBranch> entries)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteStartArray("branches");
            foreach (var entry in entries)
            {
                writer.WriteStartObject();
                writer.WriteString("repository", entry.Repository);
                writer.WriteString("workspace", entry.Workspace);
                writer.WriteString("branch", entry.Branch);
                if (entry.Line is not null) writer.WriteString("line", entry.Line);
                writer.WriteString("from", entry.From);
                if (entry.GrewFrom is not null) writer.WriteString("grewFrom", entry.GrewFrom);
                writer.WriteString("at", entry.At.ToString("O", CultureInfo.InvariantCulture));
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n") + "\n";
    }

    /// <summary>One entry, or null where it lacks what makes it one: a repository, a branch, the commit it started at.</summary>
    private static GrownBranch? Read(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        var repository = Text(element, "repository");
        var branch = Text(element, "branch");
        var from = Text(element, "from");
        if (repository is null || branch is null || from is null) return null;
        var at = DateTimeOffset.TryParse(Text(element, "at"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var when)
            ? when
            : DateTimeOffset.MinValue;
        return new GrownBranch(repository, Text(element, "workspace") ?? "default", branch, Text(element, "line"), from,
            Text(element, "grewFrom"), at);
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
