using System.Globalization;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// One branch a landing made (WSR5): the repository, the branch, the line it grew from, the commit it was
/// made at, and the session, quest and title it was made for — what the clean-up judges and the hand-off
/// tells a plugin.
/// </summary>
/// <param name="Line">The line the work grew from when it landed (D86), or null where git named none.</param>
/// <param name="Tip">The commit the landing made the branch at. A branch whose history does not hold it is not the landing's.</param>
public sealed record LandedBranch(
    string Repository, string Workspace, string Branch, string? Line, string Tip,
    string Session, string? Quest, string? Title, DateTimeOffset LandedAt)
{
    /// <summary>The plugin that last pushed it — at the landing or a hand-off after it (D100) — or null.</summary>
    public string? Plugin { get; init; }

    /// <summary>Whether a plugin answered that it pushed the branch.</summary>
    public bool Pushed { get; init; }

    /// <summary>The pull request that plugin answered with, or null.</summary>
    public string? PullRequest { get; init; }

    /// <summary>The branch's commit when the plugin pushed it: a hand-off at the same commit would push nothing new.</summary>
    public string? PushedTip { get; init; }
}

/// <summary>
/// The branches this machine's landings made (WSR5): <c>&lt;home&gt;/landings.json</c>. Written at the
/// landing, read by the clean-up and the hand-off, and forgotten once the branch is gone.
/// </summary>
/// <remarks>
/// <para><b>Machine-local, under the home (D63), never in the repository.</b> Which branches a landing on
/// this machine made is a fact about this machine, like its trees: another machine's landings are its
/// own, and a tracked list would put one person's branches in everyone's history.</para>
///
/// <para><b>The record is what makes a branch Daoris's to judge.</b> A branch a pattern happens to match
/// is not enough — <c>feature/{quest}-{slug}</c> is how people name their own branches — so the clean-up
/// and the hand-off act only on what is written here, and only while the branch still holds the commit
/// the landing made it at.</para>
///
/// <para><b>A file that does not read is no record at all</b>, never a failure: a landing has already
/// made its branch and is owed its answer, and a branch nobody recorded is never judged — the safe side.</para>
/// </remarks>
public sealed class LandedBranches(string home)
{
    public const string FileName = "landings.json";

    // One writer at a time in this process: two presses in one shell would otherwise read the same file and
    // the second write would drop the first's branch.
    private static readonly object Gate = new();

    public string FilePath => Path.Combine(home, FileName);

    /// <summary>Every branch recorded, in the order they landed.</summary>
    public IReadOnlyList<LandedBranch> All()
    {
        try
        {
            if (!File.Exists(FilePath)) return [];
            using var document = JsonDocument.Parse(File.ReadAllText(FilePath));
            if (!document.RootElement.TryGetProperty("branches", out var branches) || branches.ValueKind != JsonValueKind.Array) return [];
            return [.. branches.EnumerateArray().Select(Read).OfType<LandedBranch>()];
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>The recorded branch of that name in that repository, or null.</summary>
    public LandedBranch? Of(string repository, string branch) =>
        All().LastOrDefault(entry => Same(entry, repository, branch));

    /// <summary>
    /// The recorded branches a person names — by the session that landed it, or by the branch — in one
    /// repository where they say which. The newest first, since a session's latest landing is the one it means.
    /// </summary>
    public IReadOnlyList<LandedBranch> Find(string sessionOrBranch, string? repository = null) =>
        [.. All()
            .Where(entry => repository is null || string.Equals(entry.Repository, repository, StringComparison.OrdinalIgnoreCase))
            .Where(entry => string.Equals(entry.Session, sessionOrBranch, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(entry.Branch, sessionOrBranch, StringComparison.Ordinal))
            .Reverse()];

    /// <summary>Record one, replacing any entry for the same branch in the same repository.</summary>
    public void Record(LandedBranch entry) => Edit(all => [.. all.Where(each => !Same(each, entry.Repository, entry.Branch)), entry]);

    /// <summary>What a plugin answered when it pushed the branch (D100), kept on its entry.</summary>
    public void Pushed(string repository, string branch, PluginLanding said, string tip) => Edit(all =>
        [.. all.Select(each => Same(each, repository, branch)
            ? each with { Plugin = said.Plugin, Pushed = said.Pushed, PullRequest = said.PullRequest, PushedTip = tip }
            : each)]);

    /// <summary>Forget branches that are gone, or no longer the landing's.</summary>
    public void Forget(string repository, IReadOnlyCollection<string> branches) => Edit(all =>
        [.. all.Where(each => !(string.Equals(each.Repository, repository, StringComparison.OrdinalIgnoreCase)
                                && branches.Contains(each.Branch, StringComparer.Ordinal)))]);

    private void Edit(Func<IReadOnlyList<LandedBranch>, IReadOnlyList<LandedBranch>> change)
    {
        lock (Gate)
        {
            Directory.CreateDirectory(home);
            AtomicFile.WriteText(FilePath, ToJson(change(All())));
        }
    }

    private static bool Same(LandedBranch entry, string repository, string branch) =>
        string.Equals(entry.Repository, repository, StringComparison.OrdinalIgnoreCase)
        && string.Equals(entry.Branch, branch, StringComparison.Ordinal);

    /// <summary>Written by hand, as the driver's other files are, for the AOT reason <see cref="DriverConfig"/> gives.</summary>
    private static string ToJson(IReadOnlyList<LandedBranch> entries)
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
                writer.WriteString("tip", entry.Tip);
                writer.WriteString("session", entry.Session);
                if (entry.Quest is not null) writer.WriteString("quest", entry.Quest);
                if (entry.Title is not null) writer.WriteString("title", entry.Title);
                writer.WriteString("landedAt", entry.LandedAt.ToString("O", CultureInfo.InvariantCulture));
                if (entry.Plugin is not null) writer.WriteString("plugin", entry.Plugin);
                if (entry.Pushed) writer.WriteBoolean("pushed", true);
                if (entry.PullRequest is not null) writer.WriteString("pullRequest", entry.PullRequest);
                if (entry.PushedTip is not null) writer.WriteString("pushedTip", entry.PushedTip);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n") + "\n";
    }

    /// <summary>One entry, or null where it lacks what makes it one: a repository, a branch, the commit it was made at, a session.</summary>
    private static LandedBranch? Read(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        var repository = Text(element, "repository");
        var branch = Text(element, "branch");
        var tip = Text(element, "tip");
        var session = Text(element, "session");
        if (repository is null || branch is null || tip is null || session is null) return null;
        var at = DateTimeOffset.TryParse(Text(element, "landedAt"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var when)
            ? when
            : DateTimeOffset.MinValue;
        return new LandedBranch(repository, Text(element, "workspace") ?? "default", branch, Text(element, "line"), tip, session,
            Text(element, "quest"), Text(element, "title"), at)
        {
            Plugin = Text(element, "plugin"),
            Pushed = element.TryGetProperty("pushed", out var pushed) && pushed.ValueKind == JsonValueKind.True,
            PullRequest = Text(element, "pullRequest"),
            PushedTip = Text(element, "pushedTip"),
        };
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
