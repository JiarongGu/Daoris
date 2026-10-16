using System.Globalization;
using System.Text.Json;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>
/// Where one session branch grew from (WSR6): the repository, the branch, the line it was opened on, the commit
/// it started at, and the branch that commit was the tip of when a chain's step grew from the step before's
/// (CHAIN2). What bringing the branch up to date cuts at, so only its own commits are replayed.
/// </summary>
/// <param name="Line">The repository's line when the tree was opened (D86), or null where git named none.</param>
/// <param name="From">The commit the branch started at: the line's tip, or the step before's tip. A branch whose history does not hold it is not this record's.</param>
/// <param name="GrewFrom">The step before's branch it grew from, or null where it grew from the line.</param>
/// <param name="Now">
/// The branch the session's tree is on now where its agent moved it off this one (TREEFOLLOW1), learned at each look so it is known
/// after the tree goes; null where the tree never left this branch. Additive: an entry written before it reads as null.
/// </param>
public sealed record GrownBranch(
    string Repository, string Workspace, string Branch, string? Line, string From, string? GrewFrom, DateTimeOffset At, string? Now = null);

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

    private static readonly RecordList<GrownBranch> List = new(Gate, "branches", Read, ToJson);

    public string FilePath => DaorisHome.Entry(home, HomeEntry.SessionBranches);

    /// <summary>Every branch recorded, in the order they were written.</summary>
    public IReadOnlyList<GrownBranch> All() => List.Read(FilePath);

    /// <summary>The record of that branch in that repository, or null.</summary>
    public GrownBranch? Of(string repository, string branch) =>
        All().LastOrDefault(entry => Same(entry, repository, branch));

    /// <summary>Record one, replacing any entry for the same branch in the same repository.</summary>
    public void Record(GrownBranch entry) => Edit(all => [.. all.Where(each => !Same(each, entry.Repository, entry.Branch)), entry]);

    /// <summary>Forget branches that are gone, or no longer hold the commit they started at.</summary>
    public void Forget(string repository, IReadOnlyCollection<string> branches) => Edit(all =>
        [.. all.Where(each => !(string.Equals(each.Repository, repository, StringComparison.OrdinalIgnoreCase)
                                && branches.Contains(each.Branch, StringComparer.Ordinal)))]);

    private void Edit(Func<IReadOnlyList<GrownBranch>, IReadOnlyList<GrownBranch>> change) => List.Edit(FilePath, change);

    private static bool Same(GrownBranch entry, string repository, string branch) => SameBranch(entry.Repository, entry.Branch, repository, branch);

    /// <summary>
    /// The same branch in the same repository: the repository by name in any case, the branch exactly. The session branches and
    /// the landed branches (<see cref="LandedBranches"/>) name a branch alike, so they share this one predicate (DEDUP5).
    /// </summary>
    internal static bool SameBranch(string entryRepository, string entryBranch, string repository, string branch) =>
        string.Equals(entryRepository, repository, StringComparison.OrdinalIgnoreCase)
        && string.Equals(entryBranch, branch, StringComparison.Ordinal);

    /// <summary>Written by hand, as the driver's other files are, for the AOT reason <see cref="DriverConfig"/> gives.</summary>
    private static string ToJson(IReadOnlyList<GrownBranch> entries) => JsonFile.Serialize(writer =>
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
            writer.WriteString("at", Moments.Write(entry.At));
            if (entry.Now is not null) writer.WriteString("now", entry.Now);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    });

    /// <summary>One entry, or null where it lacks what makes it one: a repository, a branch, the commit it started at.</summary>
    private static GrownBranch? Read(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        var repository = Text(element, "repository");
        var branch = Text(element, "branch");
        var from = Text(element, "from");
        if (repository is null || branch is null || from is null) return null;
        var at = Moments.Read(Text(element, "at")) ?? DateTimeOffset.MinValue;
        return new GrownBranch(repository, Text(element, "workspace") ?? "default", branch, Text(element, "line"), from,
            Text(element, "grewFrom"), at, Text(element, "now"));
    }
}
