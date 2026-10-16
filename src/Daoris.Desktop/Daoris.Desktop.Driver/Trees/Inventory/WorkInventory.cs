using System.Globalization;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>What an inventory entry is for (the fabric events design §D3): the roles the milestone reads by.</summary>
public static class InventoryRole
{
    /// <summary>Made for the work and Daoris's to clean at its end.</summary>
    public const string Temporary = "temporary";

    /// <summary>What the work made and what stays.</summary>
    public const string Result = "result";

    /// <summary>History, never the end's to remove.</summary>
    public const string Evidence = "evidence";

    /// <summary>What a removal keeps, to undo it: a moved-aside folder, a recovery ref.</summary>
    public const string Recovery = "recovery";
}

/// <summary>What became of an entry (§D3): every entry has one, and an unknown owner is <see cref="Blocked"/>, never a guess.</summary>
public static class InventoryDisposition
{
    /// <summary>A result or evidence that stays.</summary>
    public const string Kept = "kept";

    /// <summary>A recovery kept until a date, or by its own rule.</summary>
    public const string Retained = "retained";

    /// <summary>Gone, with the proof where one was kept.</summary>
    public const string Removed = "removed";

    /// <summary>Cannot go, and says what blocks it.</summary>
    public const string Blocked = "blocked";

    /// <summary>Not yet: what waits is in the entry's detail.</summary>
    public const string Pending = "pending";
}

/// <summary>The kinds of resource the branch adapter's inventory reads (§D3's table), as the terminal and the page name them.</summary>
public static class InventoryKind
{
    public const string Tree = "tree";
    public const string SessionBranch = "session-branch";
    public const string LandingBranch = "landing-branch";
    public const string Line = "line";
    public const string MovedAside = "moved-aside";
    public const string RecoveryRef = "recovery-ref";
    public const string AutoLanding = "auto-landing";
    public const string History = "history";
    public const string End = "end";

    /// <summary>A quest's own record, the service's (FABRIC-UNIT1a): its evidence pointer.</summary>
    public const string Quest = "quest";

    /// <summary>An ask's own record, the service's (FABRIC-UNIT1a): its evidence pointer.</summary>
    public const string Ask = "ask";

    /// <summary>A file a quest or an ask carries, by its name: the service keeps its bytes.</summary>
    public const string Attachment = "attachment";
}

/// <summary>One resource a unit's work made, with its role and what became of it (FABRIC-MILESTONE1a, the design's §D3).</summary>
/// <param name="Unit">The unit it belongs to (<see cref="WorkMilestone.Unit"/>).</param>
/// <param name="Kind">One of <see cref="InventoryKind"/>.</param>
/// <param name="Resource">What it is, by name: a branch, a folder, a ref, a session's history.</param>
/// <param name="Role">One of <see cref="InventoryRole"/>.</param>
/// <param name="Disposition">One of <see cref="InventoryDisposition"/>.</param>
/// <param name="Detail">Why, in a clause: what waits, what blocks, what the proof was.</param>
public sealed record InventoryEntry(string Unit, string Kind, string Resource, string Role, string Disposition, string Detail)
{
    /// <summary>A recovery's purge date: retained until then.</summary>
    public DateTimeOffset? Until { get; init; }

    /// <summary>A removal's proof, where its record kept one.</summary>
    public string? Proof { get; init; }
}

/// <summary>Whether the unit's end was reached, and on what evidence.</summary>
/// <param name="State"><c>ended</c> (the line holds it, from the kept fact), <c>handed</c>, <c>not-ended</c>, or <c>unknown</c> (no record names its unit).</param>
/// <param name="By">The proof's code (<see cref="EndBy"/>) where ended.</param>
/// <param name="On">Where the proof held.</param>
/// <param name="Revision">The commit judged.</param>
/// <param name="At">When it was first held at that commit.</param>
/// <param name="Said">The state in a sentence.</param>
public sealed record MilestoneEnd(string State, string? By, string? On, string? Revision, DateTimeOffset? At, string Said);

/// <summary>Whether the unit's cleanup is pending, and what waits, or completed, and what went.</summary>
/// <param name="State"><c>pending</c> or <c>completed</c>.</param>
/// <param name="Waits">What waits, each entry saying why.</param>
/// <param name="Gone">What was removed.</param>
public sealed record MilestoneCleanup(string State, IReadOnlyList<InventoryEntry> Waits, IReadOnlyList<InventoryEntry> Gone);

/// <summary>
/// The milestone the person sees for one unit (the design's §D0): end reached, result kept, cleanup, recovery retained; and the
/// entries it was read from. Read from the unit (<see cref="Of"/>), the same way for every kind, and stored nowhere.
/// </summary>
/// <param name="Unit">The unit's id, or a name derived from its branch for work from before units were kept.</param>
/// <param name="Repository">The owner's repository; empty for an ask, which is a workspace's.</param>
/// <param name="Branch">A branch unit's branch; null for every other kind.</param>
public sealed record WorkMilestone(
    string Unit, string Repository, string? Branch, string? Session, MilestoneEnd End, IReadOnlyList<InventoryEntry> Result,
    MilestoneCleanup Cleanup, IReadOnlyList<InventoryEntry> Recovery, IReadOnlyList<InventoryEntry> Entries)
{
    /// <summary>
    /// The unit's milestone (FABRIC-UNIT1a reads MILESTONE1a's through the projection): the cleanup waits on every temporary entry
    /// pending or blocked and on any entry blocked, and on the end itself while nothing else waits and the end is not over; it is
    /// completed only once the end is over and nothing waits. An unknown owner's unit names no end to wait for: its blocked entry says why.
    /// </summary>
    public static WorkMilestone Of(WorkUnit unit)
    {
        var over = WorkEndState.Over(unit.End.State);
        var entries = unit.Entries;
        var waits = entries.Where(entry => entry.Role == InventoryRole.Temporary
            && entry.Disposition is InventoryDisposition.Pending or InventoryDisposition.Blocked).ToList();
        waits.AddRange(entries.Where(entry => entry.Role != InventoryRole.Temporary && entry.Disposition == InventoryDisposition.Blocked));
        if (!over && waits.Count == 0 && unit.OwnerKnown)
        {
            waits.Add(new InventoryEntry(unit.Id, InventoryKind.End, "the end", InventoryRole.Temporary, InventoryDisposition.Pending, unit.End.Said));
        }

        var gone = entries.Where(entry => entry.Role == InventoryRole.Temporary && entry.Disposition == InventoryDisposition.Removed).ToList();
        return new WorkMilestone(
            unit.Id, unit.Owner.Repository ?? "", unit.Kind == WorkUnitKind.Branch ? unit.Subject.Artifact : null, unit.Executors.FirstOrDefault(),
            unit.End, [.. entries.Where(entry => entry.Role == InventoryRole.Result)],
            new MilestoneCleanup(over && waits.Count == 0 ? "completed" : "pending", waits, gone),
            [.. entries.Where(entry => entry.Role == InventoryRole.Recovery)], entries);
    }
}

/// <summary>A recovery ref a content-held removal kept (<c>refs/daoris/discarded/&lt;branch&gt;</c>), by its repository.</summary>
public sealed record RecoveryRef(string Repository, string Ref);

/// <summary>What the records, the disk and git said, gathered for <see cref="WorkInventory.Derive"/> to read: nothing here is judged.</summary>
/// <param name="Branches">Every session branch entry, standing and traces.</param>
/// <param name="Landings">Every landing entry, standing and traces.</param>
/// <param name="Trees">The trees under the home's trees folder, and the folders moved aside.</param>
/// <param name="AutoLandings">The auto-landing entries.</param>
/// <param name="Refs">The recovery refs found in each repository read.</param>
/// <param name="InUse">The trees a session still running or waiting holds.</param>
/// <param name="HasHistory">Whether a session's record folder is on this machine.</param>
public sealed record InventoryFacts(
    IReadOnlyList<GrownBranch> Branches, IReadOnlyList<LandedBranch> Landings, IReadOnlyList<SessionTree> Trees,
    IReadOnlyList<AutoLanding> AutoLandings, IReadOnlyList<RecoveryRef> Refs, IReadOnlySet<string> InUse,
    Func<string, bool> HasHistory, DateTimeOffset Now);

/// <summary>
/// The branch adapter (FABRIC-UNIT1a; the fabric events design §D2) and its ownership inventory, derived (FABRIC-MILESTONE1a; §D0 and
/// §D3): every entry gets a role and a disposition from the records that already name the resource, each unit is read from them as a
/// <see cref="WorkUnit"/> of kind <see cref="WorkUnitKind.Branch"/>, and its milestone from the unit. Pure over
/// <see cref="InventoryFacts"/>: nothing is stored, nothing removed, no fact written twice.
/// </summary>
/// <remarks>
/// <para><b>The canonical records</b> (§D2's table): the session branch's entry in <see cref="SessionBranches.FileName"/>, with the
/// landings in <see cref="LandedBranches.FileName"/> that name its unit (<see cref="LandedBranch.Units"/>); their one writer is the
/// driver. A landing from before units, and an auto-landing alone, name a unit of their own from their record.</para>
/// <para><b>The end is the kept fact</b> (<see cref="HeldFact"/>), never judged again here: a fact whose branch moved since is
/// cleared by the next look (§B5), so this reads what the keeper last proved. Absent, the work is <i>handed</i> where a landing
/// carries it and the line does not hold that, else <i>not ended</i> (<see cref="EndState"/>'s words).</para>
/// <para><b>An unknown owner blocks</b> (canary 27): a tree no record names, or one git cannot read the branch of, is
/// <see cref="InventoryDisposition.Blocked"/> and says so, in a unit of its own that is never read as ended.</para>
/// </remarks>
public static partial class WorkInventory
{
    private sealed class Draft(string id, string repository, string workspace)
    {
        public string Id { get; } = id;
        public string Repository { get; } = repository;
        public string Workspace { get; } = workspace;
        public GrownBranch? Branch { get; set; }
        public List<LandedBranch> Landings { get; } = [];
        public List<InventoryEntry> Extra { get; } = [];
        public bool Unowned { get; set; }
        public HashSet<string> Sessions { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The milestone of every unit the records name, and of what no record names, oldest first: each read from its unit.</summary>
    public static IReadOnlyList<WorkMilestone> Derive(InventoryFacts facts) => [.. Units(facts).Select(WorkMilestone.Of)];

    /// <summary>
    /// Every branch unit the records name, and a unit of its own for what no record names, oldest first (the branch adapter, §D2). A
    /// landing that carries one unit alone links it to the quest the landing names; one carrying several does not say which served it.
    /// </summary>
    public static IReadOnlyList<WorkUnit> Units(InventoryFacts facts)
    {
        var drafts = new Dictionary<string, Draft>(StringComparer.Ordinal);
        foreach (var branch in facts.Branches)
        {
            var key = branch.Unit ?? $"branch:{branch.Repository.ToLowerInvariant()}/{branch.Branch}";
            drafts[key] = new Draft(key, branch.Repository, branch.Workspace) { Branch = branch };
        }

        foreach (var landing in facts.Landings)
        {
            var targets = landing.Units.Where(drafts.ContainsKey).ToList();
            if (targets.Count == 0)
            {
                var key = $"landing:{landing.Repository.ToLowerInvariant()}/{landing.Branch}";
                drafts[key] = new Draft(key, landing.Repository, landing.Workspace);
                targets = [key];
            }

            foreach (var key in targets) drafts[key].Landings.Add(landing);
        }

        foreach (var draft in drafts.Values)
        {
            foreach (var landing in draft.Landings)
            {
                draft.Sessions.Add(landing.Session);
                foreach (var advance in landing.Advances) draft.Sessions.Add(advance.Session);
            }
        }

        var trees = AttachTrees(facts, drafts, out var treeOf);
        AttachAutoLandings(facts, drafts, treeOf);
        AttachRefs(facts, drafts);

        var units = new List<WorkUnit>();
        foreach (var draft in drafts.Values) units.Add(Unit(draft, facts, trees.GetValueOrDefault(draft.Id) ?? []));
        return units;
    }

    private static string Name(string path) => Path.GetFileName(Path.TrimEndingDirectorySeparator(path));

    /// <summary>The tree's name from a folder moved aside (<c>&lt;name&gt;-&lt;moment&gt;</c>).</summary>
    private static string AsideName(string path)
    {
        var name = Name(path);
        var dash = name.LastIndexOf('-');
        return dash > 0 ? name[..dash] : name;
    }

    private static bool SameRepository(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static string Full(string path)
    {
        try { return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)); }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException) { return path; }
    }

    /// <summary>Each tree to the unit whose branch it is on, or to a unit of its own where no record names it.</summary>
    private static Dictionary<string, List<SessionTree>> AttachTrees(
        InventoryFacts facts, Dictionary<string, Draft> drafts, out Dictionary<string, string> treeOf)
    {
        var attached = new Dictionary<string, List<SessionTree>>(StringComparer.Ordinal);
        treeOf = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tree in facts.Trees)
        {
            var aside = tree.Branch == SessionTree.SetAside;
            var name = aside ? AsideName(tree.Path) : Name(tree.Path);
            var found = drafts.Values.FirstOrDefault(draft => draft.Branch is { } branch && SameRepository(branch.Repository, tree.Repository)
                && (aside || tree.Branch is SessionTree.NotATree or "(unreadable)"
                    ? branch.Branch == TreeFollows.SessionPrefix + name
                    : branch.GoneAt is null && (branch.Branch == tree.Branch || branch.Now == tree.Branch)));
            if (found is null)
            {
                found = new Draft($"{(aside ? "aside" : "tree")}:{Full(tree.Path)}", tree.Repository, tree.Workspace) { Unowned = true };
                drafts[found.Id] = found;
            }

            if (!attached.TryGetValue(found.Id, out var list)) attached[found.Id] = list = [];
            list.Add(tree);
            treeOf[Full(tree.Path)] = found.Id;
        }

        return attached;
    }

    private static void AttachAutoLandings(InventoryFacts facts, Dictionary<string, Draft> drafts, Dictionary<string, string> treeOf)
    {
        foreach (var auto in facts.AutoLandings)
        {
            var draft = drafts.Values.FirstOrDefault(each => each.Sessions.Contains(auto.Session))
                ?? (treeOf.TryGetValue(Full(auto.Tree), out var id) ? drafts[id] : null);
            if (draft is null)
            {
                draft = new Draft($"session:{auto.Session}", auto.Repository, auto.Workspace);
                drafts[draft.Id] = draft;
            }

            draft.Sessions.Add(auto.Session);
            var entry = auto.Closed is { } closed
                ? new InventoryEntry(draft.Id, InventoryKind.AutoLanding, auto.Session, InventoryRole.Evidence, InventoryDisposition.Kept,
                    $"its due entry closed {Minute(closed)} UTC" + (auto.Last is { } last ? $" ({last.Code})" : ""))
                : new InventoryEntry(draft.Id, InventoryKind.AutoLanding, auto.Session, InventoryRole.Temporary, InventoryDisposition.Pending,
                    auto.Last is { } tried
                        ? $"due since {Minute(auto.DueAt)} UTC; last tried {Minute(tried.At)} UTC: {tried.Code}; it goes when the unit ends"
                        : $"due since {Minute(auto.DueAt)} UTC and not tried yet; it goes when the unit ends");
            draft.Extra.Add(entry);
        }
    }

    [GeneratedRegex(@"-\d+$")]
    private static partial Regex Numbered();

    private static void AttachRefs(InventoryFacts facts, Dictionary<string, Draft> drafts)
    {
        foreach (var each in facts.Refs)
        {
            var named = each.Ref.StartsWith(ContentHoldsPrefix, StringComparison.Ordinal) ? each.Ref[ContentHoldsPrefix.Length..] : each.Ref;
            var bare = Numbered().Replace(named, "");
            var draft = drafts.Values.FirstOrDefault(candidate => candidate.Branch is { } branch && SameRepository(branch.Repository, each.Repository)
                && (branch.Branch == named || branch.Branch == bare));
            if (draft is null)
            {
                draft = new Draft($"ref:{each.Repository.ToLowerInvariant()}/{named}", each.Repository, "default") { Unowned = true };
                drafts[draft.Id] = draft;
            }

            draft.Extra.Add(new InventoryEntry(draft.Id, InventoryKind.RecoveryRef, each.Ref, InventoryRole.Recovery, InventoryDisposition.Retained,
                "kept by its own rule: Daoris never deletes it, and the end never takes it"));
        }
    }

    private const string ContentHoldsPrefix = "refs/daoris/discarded/";

    private static WorkUnit Unit(Draft draft, InventoryFacts facts, IReadOnlyList<SessionTree> trees)
    {
        var branch = draft.Branch;
        var end = End(draft);
        var ended = end.State == WorkEndState.Ended;
        var entries = new List<InventoryEntry>();

        if (branch is not null)
        {
            entries.Add(branch.GoneAt is { } gone
                ? new InventoryEntry(draft.Id, InventoryKind.SessionBranch, branch.Branch, InventoryRole.Temporary, InventoryDisposition.Removed,
                    $"went {Minute(gone)} UTC") { Proof = branch.Held is { } held ? ProofSaid(held.By, held.On) : null }
                : new InventoryEntry(draft.Id, InventoryKind.SessionBranch, branch.Branch, InventoryRole.Temporary, InventoryDisposition.Pending,
                    ended ? "the look's tidy removes it, with fresh proof" : "waits for the work to end"));
        }

        foreach (var landing in draft.Landings)
        {
            if (landing.GoneAt is { } went)
            {
                entries.Add(new InventoryEntry(draft.Id, InventoryKind.LandingBranch, landing.Branch, InventoryRole.Temporary, InventoryDisposition.Removed,
                    $"went {Minute(went)} UTC") { Proof = landing.RemovedAs is { } by ? ProofSaid(by, landing.RemovedOn) : landing.Held is { } held ? ProofSaid(held.By, held.On) : null });
            }
            else if (landing.Held is { } line)
            {
                entries.Add(new InventoryEntry(draft.Id, InventoryKind.LandingBranch, landing.Branch, InventoryRole.Temporary, InventoryDisposition.Pending,
                    $"the line holds it ({ProofSaid(line.By, line.On)}); the look's tidy removes it"));
            }
            else
            {
                entries.Add(new InventoryEntry(draft.Id, InventoryKind.LandingBranch, landing.Branch, InventoryRole.Result, InventoryDisposition.Kept,
                    "it carries the work, and the line does not hold it yet"));
            }
        }

        if (ended && end.On is { } on)
        {
            entries.Add(new InventoryEntry(draft.Id, InventoryKind.Line, on, InventoryRole.Result, InventoryDisposition.Kept,
                ProofSaid(end.By, on)) { Proof = ProofSaid(end.By, on) });
        }

        foreach (var tree in trees) entries.Add(TreeEntry(draft, tree, facts, ended));
        entries.AddRange(draft.Extra);
        foreach (var id in draft.Sessions.Where(facts.HasHistory))
        {
            entries.Add(new InventoryEntry(draft.Id, InventoryKind.History, id, InventoryRole.Evidence, InventoryDisposition.Kept,
                "the session's record and log: history, which the end never removes (clearing governs it)"));
        }

        // The record that names it, by what the draft was read from; none for what no record names (canary 27).
        var record = draft.Unowned ? null
            : branch is not null ? SessionBranches.FileName
            : draft.Landings.Count > 0 ? LandedBranches.FileName
            : AutoLandings.FileName;
        var quests = draft.Landings.Where(landing => landing.Quest is { Length: > 0 } && landing.Units.Count <= 1)
            .Select(landing => new WorkLink(WorkRelation.ProducedBy, QuestUnits.IdOf(landing.Quest!))).Distinct().ToList();
        return new WorkUnit(
            draft.Id, WorkUnitKind.Branch, new WorkOwner(draft.Repository, draft.Workspace, record, WorkRecord.DriverWriter),
            new WorkSubject(branch?.Branch ?? draft.Landings.FirstOrDefault()?.Branch, end.Revision), end, entries)
        {
            Executors = [.. draft.Sessions],
            Links = quests,
            Records = new WorkUnitRecords(branch, [.. draft.Landings], null),
        };
    }

    private static InventoryEntry TreeEntry(Draft draft, SessionTree tree, InventoryFacts facts, bool ended)
    {
        if (tree.Branch == SessionTree.SetAside)
        {
            var until = tree.MovedAside is { } moved ? moved + BranchTidying.SetAsideFor : (DateTimeOffset?)null;
            return new InventoryEntry(draft.Id, InventoryKind.MovedAside, tree.Path, InventoryRole.Recovery, InventoryDisposition.Retained,
                tree.MovedAside is { } at
                    ? $"moved aside {Minute(at)} UTC; deleted once it is left untouched until {Minute(until!.Value)} UTC"
                    : "moved aside; its name says no date, so the look's purge never takes it")
            { Until = until };
        }

        if (draft.Unowned)
        {
            return new InventoryEntry(draft.Id, InventoryKind.Tree, tree.Path, InventoryRole.Temporary, InventoryDisposition.Blocked,
                tree.Branch is SessionTree.NotATree
                    ? "its owner is unknown: no repository is linked to this folder, so nothing here is cleaned until the person says"
                    : tree.Branch == "(unreadable)"
                        ? "its owner is unknown: git could not read which branch it is on, so nothing here is cleaned"
                        : $"its owner is unknown: no record names `{tree.Branch}`, so nothing here is cleaned");
        }

        if (tree.Branch is SessionTree.NotATree or "(unreadable)")
        {
            return new InventoryEntry(draft.Id, InventoryKind.Tree, tree.Path, InventoryRole.Temporary, InventoryDisposition.Blocked,
                "git could not read which branch it is on, so it is not cleaned");
        }

        var held = facts.InUse.Contains(Full(tree.Path)) || facts.InUse.Any(path => string.Equals(Full(path), Full(tree.Path), StringComparison.OrdinalIgnoreCase));
        return new InventoryEntry(draft.Id, InventoryKind.Tree, tree.Path, InventoryRole.Temporary, InventoryDisposition.Pending,
            held ? "a session still running or waiting holds it, so its folder is not moved aside yet"
                : ended ? "the look's tidy moves its folder aside once nothing holds it" : "waits for the work to end");
    }

    private static MilestoneEnd End(Draft draft)
    {
        if (draft.Unowned) return new MilestoneEnd("unknown", null, null, null, null, "no record names its unit, so its end is not said");
        var held = draft.Landings.Select(landing => landing.Held).OfType<HeldFact>().FirstOrDefault() ?? draft.Branch?.Held;
        if (held is not null)
        {
            return new MilestoneEnd("ended", held.By, held.On, held.Tip, held.Since,
                $"{ProofSaid(held.By, held.On)}, at {TextBounds.ShortCommit(held.Tip, 8)} since {Minute(held.Since)} UTC");
        }

        var removed = draft.Landings.FirstOrDefault(landing => landing.GoneAt is not null && landing.RemovedAs is not null);
        if (removed is { RemovedAs: { } by, GoneAt: { } at })
        {
            return new MilestoneEnd("ended", by, removed.RemovedOn, removed.Tip, at,
                $"{ProofSaid(by, removed.RemovedOn)}, when `{removed.Branch}` went {Minute(at)} UTC");
        }

        var carrying = draft.Landings.FirstOrDefault(landing => landing.GoneAt is null);
        if (carrying is not null)
        {
            return new MilestoneEnd("handed", null, null, null, null, $"`{carrying.Branch}` carries it and the line does not hold that yet");
        }

        return new MilestoneEnd("not-ended", null, null, null, null, draft.Branch?.GoneAt is not null
            ? "its branch went and nothing proved the line holds its work"
            : "nothing was handed on and the line does not hold it");
    }

    /// <summary>What a proof's code says it found, as the end judgement says it.</summary>
    public static string ProofSaid(string? by, string? on) => by switch
    {
        EndBy.Merged => $"every commit of it is on `{on}`",
        EndBy.Empty => $"it holds nothing `{on}` lacks",
        EndBy.OnLine => $"its files read on `{on}` as it left them",
        EndBy.PullRequest => $"its pull request completed and its merge commit is on `{on}`",
        EndBy.Landed => $"its work is on `{on}`, which the line holds",
        EndBy.Carried => $"a completed pull request, `{on}`, carried it and its merge commit is on the line",
        _ => $"{by} on `{on}`",
    };

    internal static string Minute(DateTimeOffset at) => at.ToUniversalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
}
