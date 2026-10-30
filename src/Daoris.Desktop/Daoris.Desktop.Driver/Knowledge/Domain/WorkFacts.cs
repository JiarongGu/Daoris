namespace Daoris.Driver;

/// <summary>An evidence read in words (EVID1b): the record's lines, the terminal's and the trace's, said alike.</summary>
public static class EvidenceWords
{
    /// <summary>One item as the record says it: its requirement, what it names, its code and what the code means here.</summary>
    internal static string Line(EvidenceRead item) =>
        item.Gate is { } gate
            ? $"- requirement {item.Requirement} gate `{gate}`: {Said(item)}"
            : $"- requirement {item.Requirement} `{item.Path}`: {Said(item)}";

    /// <summary>
    /// What one read came to, in words beside its code: <c>found</c> and whether this work changed it, the spelling a <c>case</c>
    /// read found, where an <c>uncommitted</c> path is, and a gate's <c>no-queue</c>. The record and the trace say it alike.
    /// </summary>
    internal static string Said(EvidenceRead item)
    {
        if (item.Gate is not null)
        {
            return item.Result == EvidenceCodes.NoQueue ? "no-queue, read from the landing queue, which does not run it here" : item.Result;
        }

        var said = item.Result switch
        {
            EvidenceCodes.Case => $"case, the commit spells it `{item.Spelled}`",
            EvidenceCodes.Uncommitted => "uncommitted, in the tree and not in the commit",
            var result => result,
        };
        var changed = item.Changed switch
        {
            true => ", changed by this work",
            false when item.Result == EvidenceCodes.Found => ", unchanged by this work",
            _ => "",
        };
        return said + changed;
    }
}


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

    /// <summary>What a plugin's attempt created (FABRIC-PERFORM1d, §D3), by its receipt: only its plugin removes it, through <c>work/cleanup</c>.</summary>
    public const string PluginResource = "plugin-resource";

    /// <summary>A dispatched attempt whose outcome is not settled (FABRIC-PERFORM1d, §D3): no unit retires while one is.</summary>
    public const string Attempt = "attempt";
}


/// <summary>
/// Why a blocked entry is blocked, as a code the page words in its reader's language with the entry's <see cref="InventoryEntry.Facts"/>
/// (OVERVIEWNOISE1b): the <see cref="InventoryEntry.Detail"/> stays the driver's sentence, which the terminal says and a page that has
/// no words for a code shows as recorded.
/// </summary>
public static class InventoryCode
{
    /// <summary>The look's tidy kept a line-held branch (FABRIC-END4a): facts <c>why</c>, the guard's code, and <c>at</c>, when.</summary>
    public const string Kept = "kept";

    /// <summary>A tree its session recorded that no branch record follows, so only the clean-up's press takes it: fact <c>branch</c>.</summary>
    public const string Unfollowed = "unfollowed";

    /// <summary>A tree of a known unit whose branch git could not read.</summary>
    public const string Unread = "unread";

    /// <summary>An unknown owner: a folder no repository is linked to.</summary>
    public const string UnknownFolder = "unknown-folder";

    /// <summary>An unknown owner: a tree whose branch git could not read.</summary>
    public const string UnknownUnread = "unknown-unread";

    /// <summary>An unknown owner: a tree on a branch no record and no session names: fact <c>branch</c>.</summary>
    public const string UnknownBranch = "unknown-branch";

    /// <summary>An unknown owner: a standing <c>daoris/*</c> branch Daoris did not record (FABRIC-UNKNOWN1): fact <c>branch</c>.</summary>
    public const string Unrecorded = "unrecorded";

    /// <summary>A plugin's resource of a schema its manifest does not declare (FABRIC-PERFORM1d): facts <c>plugin</c> and <c>schema</c>.</summary>
    public const string Undeclared = "undeclared-resource";

    /// <summary>A plugin's resource whose cleanup has failed for seven days (FABRIC-PERFORM1d, §D3): facts <c>plugin</c> and <c>tries</c>.</summary>
    public const string CleanupBlocked = "cleanup-blocked";

    /// <summary>A dispatched attempt held for the person (FABRIC-PERFORM1d): fact <c>node</c>.</summary>
    public const string AttemptHeld = "attempt-held";

    /// <summary>
    /// A landing's branch whose quest is closed and which the line has not held for a day (LANDRETIRE1, <see cref="LandedStanding"/>): facts
    /// <c>branch</c>, <c>quest</c>, and <c>since</c>, the moment the day passed. The person's to withdraw, or to wait on, never the clean-up's.
    /// </summary>
    public const string LandingStale = "landing-stale";
}


/// <summary>A tree a session's record names (D51): the join from a tree to the session that recorded it (OVERVIEWNOISE1b).</summary>
public sealed record RecordedTree(string Session, string Tree);

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

    /// <summary>The repository a recovery belongs to (RECOVERYVIEW1), so a workspace's page lists its own; null for an entry that is no recovery.</summary>
    public string? Repository { get; init; }

    /// <summary>The commit a recovery ref names (RECOVERYDROP1), which the person's delete is verified against; null for an entry that is no recovery ref.</summary>
    public string? Commit { get; init; }

    /// <summary>A removal's proof, where its record kept one.</summary>
    public string? Proof { get; init; }

    /// <summary>Why a blocked entry is blocked (<see cref="InventoryCode"/>), which the page words; null where it says only its detail.</summary>
    public string? Code { get; init; }

    /// <summary>The values its code is worded with, by name; null where it has no code.</summary>
    public IReadOnlyDictionary<string, string>? Facts { get; init; }
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

/// <summary>What one repository's part of the inventory could not read (FACTSPLIT1a): the repository, as data, and the clause `work state` says.</summary>
public sealed record InventoryUnread(string Repository, string Said);

/// <summary>Whose work is read (D132 §1): an ask's, or one quest's.</summary>
public enum WorkScope
{
    /// <summary>An ask's: every quest it asked, chain steps included, and the ask's intake.</summary>
    Ask,

    /// <summary>One quest's: that quest alone where an ask's starts from what it asked, and no intake.</summary>
    Quest,
}


/// <summary>
/// Whose pause holds a quest (PAUSE1b, D132 point 3): an ask's, over every quest of its work, or a quest's own, over its
/// work. What the planner's <see cref="StartVerdict.Paused"/> names, and what *Resume* takes away.
/// </summary>
/// <param name="Id">The ask's or the quest's id, as <c>driver.json</c> spells it.</param>
public sealed record PausedBy(WorkScope Scope, string Id)
{
    /// <summary><c>ask</c> or <c>quest</c>: the scope's public spelling, which the page and the machine log read.</summary>
    public string Word => Scope == WorkScope.Ask ? "ask" : "quest";

    /// <summary>The terminal's door that resumes it (D50, design §7.2).</summary>
    public string Door => Scope == WorkScope.Ask ? $"daoris-driver ask --resume {Id}" : $"daoris-driver quest resume {Id}";

    /// <summary>
    /// What the record of a session this pause stopped says (design §4.1): a person reads it, and nothing decides from it
    /// (D104). Never translated: a record's note is data.
    /// </summary>
    public string Note => Noted.Note;

    /// <summary><see cref="Note"/> with its code and the work's id (LANG1a, the language design §4 rows 52–53).</summary>
    public Noted Noted => Scope == WorkScope.Ask
        ? Noted.Of(NoteCodes.StoppedPausedAsk, $"paused with ask `#{Id}`.", ("ask", Id))
        : Noted.Of(NoteCodes.StoppedPausedQuest, $"paused with quest `#{Id}`.", ("quest", Id));
}


/// <summary>
/// What one session's own tree holds that no branch of the person's does, by D88's proof: its unlanded commits and its
/// uncommitted paths. Null in either is git unable to say.
/// </summary>
public sealed record TreeWork(int? Commits, int? Uncommitted)
{
    /// <summary>
    /// Whether there is work nobody has accepted yet. Only a proven zero is none: a count git could not give is kept, as
    /// the clean-up keeps what it cannot clear (D88). Commits the line holds by content, a squash's or a landing's replay, are
    /// landed (REVIEWLANDED1): none, where an unforced discard of the tree would go; a tree that stays for what no commit holds
    /// is still the person's to review.
    /// </summary>
    public bool Holds => (Commits != 0 && !Landed) || Uncommitted != 0;

    /// <summary>The commits counted are on a form of the line by content and the tree would go unforced (REVIEWLANDED1).</summary>
    public bool Landed => Held is { Held.OnLine: true, Keeps: not null };

    /// <summary>
    /// Where the commits D88 counted are held by content (SQUASHTIDY1b): a squash merge or a cherry-pick put them elsewhere, so
    /// nothing offers to land them again. Null where they are held nowhere, or nothing was counted.
    /// </summary>
    public HeldWork? Held { get; init; }

    /// <summary>
    /// The branch the tree is on where its agent moved it onto one of its own (TREEFOLLOW1), as the repository's rules ask; null
    /// where it is on the session branch the layout names (or detached). The commits counted are those that branch holds beyond
    /// every other branch of the person's, so a landing of it is that branch as it stands.
    /// </summary>
    public string? Branch { get; init; }
}


/// <summary>
/// A tree whose commits no branch of the person's holds by ancestry, found held by content (SQUASHTIDY1b, SQUASHTIDY1's proof
/// with SQUASHTIDY1c's guarantees), and what an unforced discard of it would do now.
/// </summary>
/// <param name="Held">Where its work is, and how it was found: the clause Discard says (<see cref="ContentHold.Said"/>).</param>
/// <param name="Keeps">
/// The ref an unforced discard would keep its commits at (SQUASHTIDY1c), named as the discard numbers it; null where the
/// discard would not go now.
/// </param>
/// <param name="Stays">Why the discard would not go now: what the tree holds that no commit does, in a clause; null where it would.</param>
public sealed record HeldWork(ContentHold Held, string? Keeps, string? Stays);

/// <summary>
/// Where a session branch's work is held by content though no branch of the person's holds its commits (SQUASHTIDY1): a squash
/// merge makes one new commit on the line from the branch's content, and a cherry-pick copies a change onto another branch
/// under a commit of its own, so ancestry (D88) sees neither.
/// </summary>
/// <param name="Where">What holds it, as git names it: a form of the line (<c>main</c>, <c>origin/main</c>), or a branch of the person's.</param>
/// <param name="Squash">Its whole tree is the tree of a commit on the line since it left it: what a squash merge of it leaves.</param>
public sealed record ContentHold(string Where, bool Squash)
{
    /// <summary>
    /// A squash on the line carried it with other work (SQUASHSEEN1, <see cref="SquashCarries"/>): merging it into the line as it stands
    /// adds nothing. Never set with <see cref="Squash"/>.
    /// </summary>
    public bool Carried { get; init; }

    /// <summary>
    /// A landing replayed it (REVIEWLANDED1, <see cref="ReplayHolds"/>): each of its commits is on the line under an id of its own, as a
    /// landing reads them. Never set with <see cref="Squash"/> or <see cref="Carried"/>.
    /// </summary>
    public bool Replayed { get; init; }

    /// <summary>
    /// <see cref="Where"/> is a form of the line, not a branch of the person's (REVIEWLANDED1): the work landed, so the session leaves the
    /// review (<see cref="TreeWork.Holds"/>).
    /// </summary>
    public bool OnLine { get; init; }

    /// <summary>How its work was found held: the clause a removal's sentence and the clean-up's row in the terminal say.</summary>
    public string Said => Squash
        ? $"its work is on `{Where}` by content (a squash merge)"
        : Carried
            ? $"its work is on `{Where}` by content (a squash carried it: merging it there adds nothing)"
            : Replayed
                ? $"its work is on `{Where}` by content (a landing replayed each of its commits there)"
                : $"its work is on `{Where}` by content (each file it changed reads the same there)";

    /// <summary>
    /// Where the commits a content-held removal deleted are kept (SQUASHTIDY1c), and the two things a person may do with them:
    /// bring the branch back, or let them go. A content proof compares files, so a commit's message, an empty commit and
    /// history the final tree does not show are held by this ref alone.
    /// </summary>
    public static string KeptAt(string branch, string reference) =>
        $"Its commits stay at `{reference}` until you delete that ref; `git branch {branch} {reference}` brings the branch back.";
}


