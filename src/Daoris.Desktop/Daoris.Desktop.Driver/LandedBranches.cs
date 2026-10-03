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

    /// <summary>
    /// The commit its work grew from (WSR6): the start of the session branch it was made from, where that is
    /// recorded, else null. Bringing it up to date cuts here, so only its own commits are replayed.
    /// </summary>
    public string? From { get; init; }

    /// <summary>
    /// When this machine found the branch gone, or no longer the landing's, or removed it (REVIEW2, D113); null while it
    /// stands. A trace is never judged, handed on or moved again: it is kept so the review of the session that landed it
    /// can still say where its work went.
    /// </summary>
    public DateTimeOffset? GoneAt { get; init; }

    /// <summary>What the clean-up proved when it removed the branch (<see cref="LandedKind"/>: on the line, merged, inside another); null where Daoris did not remove it.</summary>
    public string? RemovedAs { get; init; }

    /// <summary>Where that proof held: the form of the line, or the landed branch it was inside.</summary>
    public string? RemovedOn { get; init; }

    /// <summary>
    /// Who accepted it (LAND2b, D145 point 6), one of <see cref="Daoris.Driver.AcceptedBy"/>: the person's press, or the rule's switch at
    /// the quest's done. Null for a landing recorded before it was kept, which a reader says is not kept (D143 point 3).
    /// </summary>
    public string? AcceptedBy { get; init; }

    /// <summary>The landing rule it was made under, as it stood then (LAND2b, design §8); null for one recorded before it was kept.</summary>
    public LandedRule? Rule { get; init; }
}

/// <summary>
/// The branches this machine's landings made (WSR5): <c>&lt;home&gt;/landings.json</c>. Written at the
/// landing, read by the clean-up and the hand-off, and kept as a trace once the branch is gone (REVIEW2, D113), each
/// repository's newest <see cref="TracesKept"/> traces (LEFT3).
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

    /// <summary>
    /// How many traces of gone branches each repository keeps (LEFT3): the newest, by when each went. A standing entry
    /// is never dropped. Every reader parses the whole file, and a trace is kept only so a session's review can say where
    /// its work went, which is asked of recent sessions.
    /// </summary>
    public const int TracesKept = 50;

    // One writer at a time in this process: two presses in one shell would otherwise read the same file and
    // the second write would drop the first's branch.
    private static readonly object Gate = new();

    public string FilePath => Path.Combine(home, FileName);

    /// <summary>Every branch recorded that still stands, in the order they landed: what the clean-up, the hand-off and Ask Daoris act on.</summary>
    public IReadOnlyList<LandedBranch> All() => [.. Everything().Where(entry => entry.GoneAt is null)];

    /// <summary>
    /// The newest landing of one session, standing or a trace (REVIEW2, D113): what its review says of where its work
    /// went, since a tidy took its tree and the clean-up may have taken the branch since.
    /// </summary>
    public LandedBranch? Landing(string session) =>
        Everything().LastOrDefault(entry => string.Equals(entry.Session, session, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Every branch <paramref name="plugin"/> answered that it pushed, standing or a trace, in the order they landed:
    /// what a plugin's Activity lists as its pushes (PLUGUI1d, D119 §3.2).
    /// </summary>
    public IReadOnlyList<LandedBranch> PushedBy(string plugin) =>
        [.. Everything().Where(entry => entry.Pushed && string.Equals(entry.Plugin, plugin, StringComparison.OrdinalIgnoreCase))];

    /// <summary>
    /// Every entry, standing and traces, in the order they landed: what a work reads for the landings that name its sessions
    /// (PAUSE1a, <see cref="AskWork"/>), since a session a landing names keeps its tree whatever git says (D132 point 7).
    /// </summary>
    public IReadOnlyList<LandedBranch> Entries() => Everything();

    /// <summary>Every entry, standing and traces, in the order they landed.</summary>
    private IReadOnlyList<LandedBranch> Everything()
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

    /// <summary>
    /// Record one, replacing any standing entry for the same branch in the same repository. An earlier session's trace
    /// of that name stays, since its review still says where that session's work went.
    /// </summary>
    public void Record(LandedBranch entry) => Edit(all => [.. all.Where(each => !Standing(each, entry.Repository, entry.Branch)), entry]);

    /// <summary>What a plugin answered when it pushed the branch (D100), kept on its entry.</summary>
    public void Pushed(string repository, string branch, PluginLanding said, string tip) => Edit(all =>
        [.. all.Select(each => Standing(each, repository, branch)
            ? each with { Plugin = said.Plugin, Pushed = said.Pushed, PullRequest = said.PullRequest, PushedTip = tip }
            : each)]);

    /// <summary>
    /// Where bringing it up to date replayed it (WSR6): its new tip, and the line's commit it now grows from.
    /// Daoris moved it, so it stays the landing's; a branch someone else rebased no longer holds its tip and is not.
    /// </summary>
    public void Moved(string repository, string branch, string tip, string from) => Edit(all =>
        [.. all.Select(each => Standing(each, repository, branch) ? each with { Tip = tip, From = from } : each)]);

    /// <summary>
    /// Branches found gone, or no longer the landing's (a name that is the person's now): kept as traces, never judged
    /// again (REVIEW2, D113). Until D113 they were forgotten, and the review of the session that landed one then had
    /// nothing to say of where its work went.
    /// </summary>
    public void Gone(string repository, IReadOnlyCollection<string> branches) => Edit(all =>
        [.. all.Select(each => string.Equals(each.Repository, repository, StringComparison.OrdinalIgnoreCase)
                               && each.GoneAt is null && branches.Contains(each.Branch, StringComparer.Ordinal)
            ? each with { GoneAt = DateTimeOffset.UtcNow }
            : each)]);

    /// <summary>A branch the clean-up removed once its work read on the line (WSR5): a trace, with what the proof found and where.</summary>
    public void Removed(string repository, string branch, string kind, string? where) => Edit(all =>
        [.. all.Select(each => Standing(each, repository, branch)
            ? each with { GoneAt = DateTimeOffset.UtcNow, RemovedAs = kind, RemovedOn = where }
            : each)]);

    private void Edit(Func<IReadOnlyList<LandedBranch>, IReadOnlyList<LandedBranch>> change)
    {
        lock (Gate)
        {
            Directory.CreateDirectory(home);
            // Every entry, traces included: a write that read only the standing ones would drop every trace (D113).
            AtomicFile.WriteText(FilePath, ToJson(Bounded(change(Everything()))));
        }
    }

    /// <summary>
    /// The entries with each repository's traces beyond its newest <see cref="TracesKept"/> dropped (LEFT3), the one that
    /// went first first, whatever its place in the file; the rest keep the order they landed in.
    /// </summary>
    private static IReadOnlyList<LandedBranch> Bounded(IReadOnlyList<LandedBranch> entries)
    {
        var dropped = entries
            .Select((entry, at) => (Entry: entry, At: at))
            .Where(each => each.Entry.GoneAt is not null)
            .GroupBy(each => each.Entry.Repository, StringComparer.OrdinalIgnoreCase)
            .SelectMany(traces => traces.OrderByDescending(each => each.Entry.GoneAt).ThenByDescending(each => each.At).Skip(TracesKept))
            .Select(each => each.At)
            .ToHashSet();
        return dropped.Count == 0 ? entries : [.. entries.Where((_, at) => !dropped.Contains(at))];
    }

    private static bool Same(LandedBranch entry, string repository, string branch) =>
        string.Equals(entry.Repository, repository, StringComparison.OrdinalIgnoreCase)
        && string.Equals(entry.Branch, branch, StringComparison.Ordinal);

    private static bool Standing(LandedBranch entry, string repository, string branch) => entry.GoneAt is null && Same(entry, repository, branch);

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
                if (entry.From is not null) writer.WriteString("from", entry.From);
                if (entry.GoneAt is { } gone) writer.WriteString("goneAt", gone.ToString("O", CultureInfo.InvariantCulture));
                if (entry.RemovedAs is not null) writer.WriteString("removedAs", entry.RemovedAs);
                if (entry.RemovedOn is not null) writer.WriteString("removedOn", entry.RemovedOn);
                if (entry.AcceptedBy is not null) writer.WriteString("acceptedBy", entry.AcceptedBy);
                if (entry.Rule is { } rule)
                {
                    writer.WriteStartObject("rule");
                    if (rule.Plugin is not null) writer.WriteString("plugin", rule.Plugin);
                    writer.WriteBoolean("autoAccept", rule.AutoAccept);
                    writer.WriteString("source", rule.Source);
                    writer.WriteEndObject();
                }

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
            From = Text(element, "from"),
            GoneAt = DateTimeOffset.TryParse(Text(element, "goneAt"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var gone)
                ? gone
                : null,
            RemovedAs = Text(element, "removedAs"),
            RemovedOn = Text(element, "removedOn"),
            AcceptedBy = Text(element, "acceptedBy"),
            Rule = element.TryGetProperty("rule", out var rule) && rule.ValueKind == JsonValueKind.Object
                ? new LandedRule(
                    Text(rule, "plugin"),
                    rule.TryGetProperty("autoAccept", out var auto) && auto.ValueKind == JsonValueKind.True,
                    Text(rule, "source") ?? LandingSource.Default)
                : null,
        };
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
