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
    string Repository, string Workspace, string Branch, string? Line, string From, string? GrewFrom, DateTimeOffset At, string? Now = null)
{
    /// <summary>
    /// The branch work unit's id (FABRIC-END2, the fabric events design §D2): written once, when its tree opens, and kept by every later
    /// record of the branch; a landing names the units whose work it carries (<see cref="LandedBranch.Units"/>). Null for an entry from
    /// before it, which no landing can name.
    /// </summary>
    public string? Unit { get; init; }

    /// <summary>That the line held its committed work, and the evidence (§B5, §C7); null where not yet judged held. Written only by <see cref="EndKeeping"/>.</summary>
    public HeldFact? Held { get; init; }

    /// <summary>
    /// When its branch went and the entry was kept as a trace (§B5, §D3): held, or its unit still open on a standing landing. Null while
    /// it stands. A trace is never judged again.
    /// </summary>
    public DateTimeOffset? GoneAt { get; init; }

    /// <summary>
    /// What kept the line-held branch when the look's tidy last tried it (FABRIC-END4a, the design §D3's <i>blocked</i>, with why): the
    /// guard's code (<see cref="TidyKept"/>, or the press's <see cref="SweepKind.Dirty"/> and <see cref="SweepKind.InUse"/>) and when.
    /// Written only by the tidy's effect (<see cref="TidyEffect"/>); dropped when the branch is recorded again or goes. Additive: an
    /// entry written before it reads as null.
    /// </summary>
    public TidyBlock? Blocked { get; init; }

    /// <summary>A new unit's id: written once, where a tree opens.</summary>
    public static string NewUnit() => $"unit-{Guid.NewGuid():N}";
}

/// <summary>A guard that kept a line-held branch at the look's tidy (FABRIC-END4a): its code, and when it last kept it.</summary>
public sealed record TidyBlock(string Why, DateTimeOffset At)
{
    /// <summary>
    /// The sentence the tidy kept it with (FABRIC-END4a, second review), so What needs you says why and not only which guard; null where
    /// none was said. Additive: a block written before it reads as null.
    /// </summary>
    public string? Reason { get; init; }
}

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

    /// <summary>
    /// How many retired traces each repository keeps (FABRIC-END2, as the landings' record keeps its own, LEFT3): the newest, by when each
    /// went. A standing entry is never dropped, and neither is an open unit's trace (§D3).
    /// </summary>
    public const int TracesKept = 50;

    // One writer at a time in this process, as the landings' record has: a tick and a press in one shell.
    private static readonly object Gate = new();

    private static readonly RecordList<GrownBranch> List = new(Gate, "branches", Read, ToJson);

    public string FilePath => DaorisHome.Entry(home, HomeEntry.SessionBranches);

    /// <summary>The record's lock across processes (FABRIC-END2): the loop and a verb beside it edit this file one at a time.</summary>
    public string LockPath => RecordLock.PathOf(home, FileName);

    /// <summary>Every branch recorded that still stands, in the order they were written: what a start, a replay and a tidy act on.</summary>
    public IReadOnlyList<GrownBranch> All() => [.. Entries().Where(entry => entry.GoneAt is null)];

    /// <summary>Every entry, standing and traces (FABRIC-END2), in the order they were written.</summary>
    public IReadOnlyList<GrownBranch> Entries() => List.Read(FilePath);

    /// <summary>The standing record of that branch in that repository, or null.</summary>
    public GrownBranch? Of(string repository, string branch) =>
        All().LastOrDefault(entry => Same(entry, repository, branch));

    /// <summary>
    /// Record one, replacing the standing entry for the same branch in the same repository. Its unit is written once and kept, and so is
    /// its held fact, which only the end judgement writes (<see cref="Hold"/>); a moved tip makes that fact stop holding at its next
    /// judgement. A trace of the same name stays.
    /// </summary>
    public void Record(GrownBranch entry) => Edit(all =>
    {
        var standing = all.LastOrDefault(each => Standing(each, entry.Repository, entry.Branch));
        return [.. all.Where(each => !Standing(each, entry.Repository, entry.Branch)),
            entry with { Unit = standing?.Unit ?? entry.Unit, Held = standing?.Held, GoneAt = null, Blocked = null }];
    });

    /// <summary>
    /// The branch grows from <paramref name="from"/> now (FABRIC-CLOSE1c5): a step's own commits replayed onto its source's new
    /// submission. Its standing entry moved there, or one written where it had none, so the next sync and the landing cut there.
    /// </summary>
    public void GrewOnto(string repository, string workspace, string branch, string from, DateTimeOffset at) =>
        Record(Of(repository, branch) is { } standing
            ? standing with { From = from, At = at }
            : new GrownBranch(repository, workspace, branch, null, from, null, at));

    /// <summary>
    /// The tidy's effect's write (FABRIC-END4a, <see cref="TidyEffect"/>, its only caller): the guard that kept the standing branch, written
    /// only where it says something new, so a refusal repeated on every wake writes the file once.
    /// </summary>
    /// <remarks>
    /// Only the home's lease holder writes it, as only it keeps ends (FABRIC-END2): a process beside it (<c>drive --share</c>) writes nothing.
    /// The reason is compared again inside the record's lock, so two writers never rewrite one reason with two moments.
    /// </remarks>
    internal void Block(string repository, string branch, string why, string? reason, DateTimeOffset at)
    {
        if (!HomeLease.HeldHere(home)) return;
        if (Of(repository, branch) is not { } standing || Said(standing.Blocked)) return;
        Edit(all => [.. all.Select(each => Standing(each, repository, branch) && !Said(each.Blocked) ? each with { Blocked = new TidyBlock(why, at) { Reason = reason } } : each)]);

        bool Said(TidyBlock? blocked) => blocked?.Why == why && blocked.Reason == reason;
    }

    /// <summary>
    /// The entry that follows the tree of <paramref name="branch"/> (TREEFOLLOW1; FABRIC-END4a, second review): the standing one, else the
    /// newest trace, which keeps where the tree went once the tidy removed the branch, so a live tree is never left unfollowed.
    /// </summary>
    internal GrownBranch? Following(string repository, string branch) =>
        Of(repository, branch) ?? Entries().LastOrDefault(entry => entry.GoneAt is not null && Same(entry, repository, branch));

    /// <summary>
    /// Where the tree of <paramref name="branch"/> is now (TREEFOLLOW1), written on the entry that follows it (<see cref="Following"/>): a
    /// trace's is written in place and stays a trace, never recorded standing again.
    /// </summary>
    internal void Follow(string repository, string branch, string? now) => Edit(all =>
    {
        var at = -1;
        for (var index = 0; index < all.Count; index++)
        {
            // The standing entry wins; else the newest trace.
            if (Same(all[index], repository, branch) && (all[index].GoneAt is null || at < 0 || all[at].GoneAt is not null)) at = index;
        }

        return at < 0 ? all : [.. all.Select((entry, index) => index == at ? entry with { Now = now } : entry)];
    });

    /// <summary>
    /// The end judgement's write (FABRIC-END2, <see cref="EndKeeping"/>, its only caller): the standing entry's held fact, or none where it
    /// stopped holding. A trace is never written again.
    /// </summary>
    internal void Hold(string repository, string branch, HeldFact? held) => Edit(all =>
        [.. all.Select(each => Standing(each, repository, branch) ? each with { Held = held } : each)]);

    /// <summary>
    /// Forget branches that are gone, or no longer hold the commit they started at. An entry the line held, or whose unit a standing
    /// landing still carries, is kept as a trace instead (§B5, §D3): a branch's removal is not its unit's retirement, and a quest's close
    /// and the page can still say where its work went.
    /// </summary>
    public void Forget(string repository, IReadOnlyCollection<string> branches)
    {
        var open = OpenUnits(home);
        var owing = WorkObligations.Owing(home);
        Edit(all =>
        {
            var now = DateTimeOffset.UtcNow;
            var kept = new List<GrownBranch>();
            foreach (var each in all)
            {
                if (each.GoneAt is not null || !string.Equals(each.Repository, repository, StringComparison.OrdinalIgnoreCase)
                    || !branches.Contains(each.Branch, StringComparer.Ordinal))
                {
                    kept.Add(each);
                }
                // FABRIC-PERFORM1d: a unit that still owes a receipt's cleanup or an attempt's outcome keeps its trace too (§D3).
                else if (each.Held is not null || (each.Unit is { } unit && (open.Contains(unit) || owing.Contains(unit))))
                {
                    kept.Add(each with { GoneAt = now, Blocked = null });
                }
            }

            return kept;
        });
    }

    /// <summary>
    /// The look's tidy removed these branches (FABRIC-END4a review): each standing entry whose tree moved onto a branch of its own
    /// (<see cref="GrownBranch.Now"/>) becomes its trace at once, keeping where the tree's work is, since <c>ForgetGoneAsync</c> keeps
    /// such an entry standing while that tree or branch exists, and a standing entry of a deleted branch would stay pending for ever.
    /// The rest are forgotten as ever, by <c>ForgetGoneAsync</c>.
    /// </summary>
    public void Gone(string repository, IReadOnlyCollection<string> branches)
    {
        if (!All().Any(each => each.Now is not null && Same(each, repository, each.Branch) && branches.Contains(each.Branch, StringComparer.Ordinal))) return;
        var now = DateTimeOffset.UtcNow;
        Edit(all => [.. all.Select(each => each.GoneAt is null && each.Now is not null
            && string.Equals(each.Repository, repository, StringComparison.OrdinalIgnoreCase) && branches.Contains(each.Branch, StringComparer.Ordinal)
                ? each with { GoneAt = now, Blocked = null }
                : each)]);
    }

    /// <summary>
    /// The units a standing landing carries and the line does not hold yet (§D3): their work is handed, not ended, so a trace of theirs
    /// is open. Read from the landings' record, which an edit here never holds.
    /// </summary>
    public static IReadOnlySet<string> OpenUnits(string home) =>
        new LandedBranches(home).All().Where(entry => entry.Held is null).SelectMany(entry => entry.Units).ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Whether a trace's unit is still open (§D3): not held itself, and its work on a standing landing the line does not hold yet; or, held or
    /// not (FABRIC-PERFORM1d, canary 35), its unit still owes a receipt's cleanup or an attempt's outcome (<see cref="WorkObligations.Owing"/>).
    /// </summary>
    public static bool Open(GrownBranch entry, IReadOnlySet<string> openUnits, IReadOnlySet<string>? owing = null) =>
        entry.GoneAt is not null && entry.Unit is { } unit
        && ((entry.Held is null && openUnits.Contains(unit)) || owing?.Contains(unit) == true);

    private void Edit(Func<IReadOnlyList<GrownBranch>, IReadOnlyList<GrownBranch>> change) =>
        List.Edit(FilePath, all => Bounded(change(all)), LockPath);

    /// <summary>
    /// The entries with each repository's retired traces beyond its newest <see cref="TracesKept"/> dropped, the one that went first
    /// first. An open unit's trace is never dropped nor counted (§D3); the landings are read only where a repository has too many.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>Nor is the trace that follows a live tree</b> (FABRIC-END4a, second review): one that names where its tree went (<c>Now</c>)
    /// while that tree's folder stands is the live tree's tracking, which a branch's removal never retires. 🔴 <b>Only that one</b> (third
    /// review): the newest trace of its name where none of that name stands, as <see cref="Following"/> reads; a name recorded and removed
    /// again and again leaves older traces naming the same folder, and those are retired like any other.
    /// </remarks>
    private IReadOnlyList<GrownBranch> Bounded(IReadOnlyList<GrownBranch> entries)
    {
        var traces = entries.Select((entry, at) => (Entry: entry, At: at)).Where(each => each.Entry.GoneAt is not null).ToList();
        if (!traces.GroupBy(each => each.Entry.Repository, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > TracesKept)) return entries;
        var open = OpenUnits(home);
        var owing = WorkObligations.Owing(home);
        var following = entries.Select((entry, at) => (Entry: entry, At: at))
            .GroupBy(each => $"{each.Entry.Repository.ToUpperInvariant()}\n{each.Entry.Branch}", StringComparer.Ordinal)
            .Where(name => name.All(each => each.Entry.GoneAt is not null))
            .Select(name => name.MaxBy(each => each.At))
            .Where(newest => Lives(newest.Entry))
            .Select(newest => newest.At)
            .ToHashSet();
        var dropped = traces
            .Where(each => !Open(each.Entry, open, owing) && !following.Contains(each.At))
            .GroupBy(each => each.Entry.Repository, StringComparer.OrdinalIgnoreCase)
            .SelectMany(group => group.OrderByDescending(each => each.Entry.GoneAt).ThenByDescending(each => each.At).Skip(TracesKept))
            .Select(each => each.At)
            .ToHashSet();
        return dropped.Count == 0 ? entries : [.. entries.Where((_, at) => !dropped.Contains(at))];
    }

    /// <summary>Whether a trace names a tree that still stands: one whose agent moved it onto a branch of its own, its folder there.</summary>
    private bool Lives(GrownBranch trace)
    {
        if (trace.Now is null || !trace.Branch.StartsWith(TreeFollows.SessionPrefix, StringComparison.Ordinal)) return false;
        var tree = Path.Combine(DaorisHome.Entry(home, HomeEntry.Trees), trace.Workspace, trace.Repository, trace.Branch[TreeFollows.SessionPrefix.Length..]);
        return File.Exists(Path.Combine(tree, ".git"));
    }

    private static bool Same(GrownBranch entry, string repository, string branch) => SameBranch(entry.Repository, entry.Branch, repository, branch);

    private static bool Standing(GrownBranch entry, string repository, string branch) => entry.GoneAt is null && Same(entry, repository, branch);

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
            // FABRIC-END2: each only where it is known, so an entry from before reads as it did.
            if (entry.Unit is not null) writer.WriteString("unit", entry.Unit);
            if (entry.Held is { } held) HeldFact.Write(writer, held);
            if (entry.GoneAt is { } gone) writer.WriteString("goneAt", Moments.Write(gone));
            // FABRIC-END4a: only where a guard kept it, so an entry from before reads as it did.
            if (entry.Blocked is { } blocked)
            {
                writer.WriteStartObject("blocked");
                writer.WriteString("why", blocked.Why);
                writer.WriteString("at", Moments.Write(blocked.At));
                // Second review: only where one was said, so a block from before reads as it did.
                if (blocked.Reason is not null) writer.WriteString("reason", blocked.Reason);
                writer.WriteEndObject();
            }

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
            Text(element, "grewFrom"), at, Text(element, "now"))
        {
            Unit = Text(element, "unit"),
            Held = HeldFact.Read(element),
            GoneAt = Moments.Read(Text(element, "goneAt")),
            Blocked = element.TryGetProperty("blocked", out var blocked) && blocked.ValueKind == JsonValueKind.Object
                && Text(blocked, "why") is { } why && Moments.Read(Text(blocked, "at")) is { } when
                    ? new TidyBlock(why, when) { Reason = Text(blocked, "reason") }
                    : null,
        };
    }
}
