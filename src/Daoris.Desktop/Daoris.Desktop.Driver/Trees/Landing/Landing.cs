using System.Text;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// The forms a landing rule takes (WSR1, D87). Pushing and opening a pull request is not a form of its
/// own: a branch rule names the plugin that does it (WSR4, D100).
/// </summary>
public static class LandingForm
{
    /// <summary>Into the repository's line (D86), in its own checkout — the merge door as it always was.</summary>
    public const string Merge = "merge";

    /// <summary>Onto a new branch named by a pattern, from the session's branch, for the person — or the rule's plugin — to push.</summary>
    public const string Branch = "branch";
}

/// <summary>
/// How a session's work lands: a form, for <see cref="LandingForm.Branch"/> the pattern that names the
/// branch, and whether the tree and its branch go once a press lands the work (<paramref name="Tidy"/>,
/// D88) — only where git proves the work is on a branch of the person's.
/// </summary>
/// <param name="Plugin">
/// The plugin a branch rule hands the new branch to (WSR4, D100): spoken to on <see cref="HookPoints.Land"/>
/// once the branch exists, it pushes and opens the pull request for its platform. Null — the default — is
/// the branch left for the person to push, as it always was.
/// </param>
/// <param name="AutoAccept">
/// <i>Accept automatically</i> (LAND2a, D145): a quest's done lands its work as the person's Accept would, and the rule's
/// plugin pushes it and opens the pull request with no press, the pull request being the last human step. Only a branch
/// rule takes it; off, the default, is the press as it always was. It rides the rule, so a repository's own rule replaces
/// its workspace's switch with the rest of it. LAND2b acts on it: a session concluding on a done quest becomes due
/// (<see cref="AutoLandings"/>), and the next look lands it (<see cref="AutoLander"/>).
/// </param>
/// <param name="Key">
/// The shape of the repository's ticket keys (PROCESS1b-i, D164's amendment point 2): a regular expression the pattern's
/// <c>{ticket}</c> is found by, in the title, then the request, then its links (<see cref="LandingRules.Ticket"/>). Null is
/// <see cref="LandingRules.DefaultKey"/>. Only a branch rule whose pattern says <c>{ticket}</c> takes one.
/// </param>
/// <param name="Squash">
/// The pull request's branch as one commit (PROCESS1b-ii, D164's amendment point 2 and its PROCESS1b-ii note): Accept makes it
/// from the session's tree, its parent where the session left the line, its message the quest's title and the session's commits'
/// subjects (<see cref="SquashMessage"/>), with git's own identity in the repository. Only a branch rule takes it; off, the
/// default, is the branch at the session's own commits, as it always was. A tree on a branch of its own is never squashed.
/// </param>
public sealed record LandingRule(
    string Form, string? Pattern = null, bool Tidy = false, string? Plugin = null, bool AutoAccept = false, string? Key = null,
    bool Squash = false)
{
    public static readonly LandingRule Merge = new(LandingForm.Merge);

    /// <summary>
    /// The work's cycle after the landing (FABRIC-GRAPH1; the fabric events design §C2, §E7): a graph fragment <c>{ nodes, wires, entries,
    /// end, cleanup }</c>, its JSON text as the file holds it, compact. Null is the default, today's: ended when the line holds the work,
    /// the cleanup at <i>held</i>. It rides the rule (D164's amendment), so a repository's own rule replaces its workspace's cycle with the
    /// rest of it; a door with no field for it keeps it (<see cref="DriverConfig.WithLanding"/>). <see cref="NodeGraphHomes"/> reads it.
    /// The CLI's <c>driverconfig.ts</c> keeps the same field.
    /// </summary>
    public string? Cycle { get; init; }
}

/// <summary>Where a landing rule came from, for the screen and the sentences that name it.</summary>
public static class LandingSource
{
    public const string Repository = "repository";
    public const string Workspace = "workspace";
    public const string Default = "default";
}

/// <summary>A repository's landing rule and what said so.</summary>
public sealed record Landing(LandingRule Rule, string Source)
{
    /// <summary>
    /// Where the repository's access setup said <c>landing: rule</c> (AUTONOMY1c2, D184 point 3), one of <see cref="AccessRules.FromRepository"/>
    /// and the rest, where it changes how this rule lands: a branch rule whose own switch is off, which it accepts automatically
    /// (<see cref="LandingRules.Effective"/>), or a merge rule, which it cannot (D51 rule 6). Null where the setup is the person's, or the
    /// rule accepts by its own switch.
    /// </summary>
    public string? Access { get; init; }
}

/// <summary>
/// What a pattern is expanded from: the session's quest (or the session itself), its title's words, the repository, and the
/// ticket its rule's key found (PROCESS1b-i), the session standing in where it found none.
/// </summary>
public sealed record LandingNames(string Quest, string Slug, string Repository, string Session, string? Ticket = null);

/// <summary>The session a press lands: its id, and its quest and that quest's title where it has one.</summary>
public sealed record LandingSubject(string Session, string? Quest, string? Title)
{
    /// <summary>
    /// The quest's name where it is not its title (SESSUX1j, LANDNAME1): its short title, which the branch is named for. Null
    /// where the name is the title, or there is no quest: the title names the branch.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// The branch name the person entered (LANDNAME1a, D145): it replaces the rule's whole name, the pattern's <c>{slug}</c> and all,
    /// and only where the rule makes a branch. Blank or null keeps the rule's. The automatic landing takes none.
    /// </summary>
    public string? Entered { get; init; }

    /// <summary>The entered name trimmed, or null where none was entered.</summary>
    public string? EnteredName => string.IsNullOrWhiteSpace(Entered) ? null : Entered.Trim();

    /// <summary>
    /// The quest the session itself served (FABRIC-UNITLINK1, D173's CLOSELAND1 note), whatever <see cref="Quest"/> names it for: the
    /// landing's record keeps it on the landing or its advance. Set where a door names the session's landing
    /// (<see cref="SessionTrees.OwnSubjectAsync"/>, an entry's parts); null for a conversation, and where no door set it.
    /// </summary>
    public string? Served { get; init; }

    /// <summary>
    /// That quest's current submission as the door read it (FABRIC-CLOSE1h13), by its machine and sequence, which the record keeps as
    /// what the landing was made on; null for a done and a conversation.
    /// </summary>
    public QuestSubmissionRef? Submission { get; init; }

    /// <summary>
    /// The branch an entry's Accept lands this part on (ACCEPTALL1, D167's 2026-10-15 amendment point 1): the entry's one branch,
    /// named once for its first part, so its parts land together. Null names it by the rule for this subject, as before; a name the
    /// person entered still comes first, and a tree on a branch of its own still lands as that branch (TREEFOLLOW1).
    /// </summary>
    public string? EntryBranch { get; init; }

    /// <summary>
    /// Whether the press joins the entry's branch (ACCEPTALL1b, ACCEPTALL1f): it lands on <see cref="EntryBranch"/>, replaying a part
    /// that ran alongside onto its tip. False where no entry names a branch, or the person entered a name, which lands as entered.
    /// </summary>
    public bool Joins => EntryBranch is not null && EnteredName is null;

    /// <summary>
    /// The request's body (PROCESS1b-i), where a ticket's key is searched after the title: the quest's, null where it is blank or
    /// there is no quest. A conversation's words are its opening, its <see cref="Title"/>.
    /// </summary>
    public string? Body { get; init; }

    /// <summary>The request's links, searched last for a ticket's key (PROCESS1b-i), each as given; null where it carries none.</summary>
    public IReadOnlyList<string>? Links { get; init; }

    /// <summary>
    /// This subject, named for a part's own quest and title (TREEFOLLOWNAME1): a part on a branch of its own lands as that branch, so
    /// its landing record and its plugin's pull request carry its own words, not the entry's first part's. The session, the entry's
    /// branch and a name entered stay as they are.
    /// </summary>
    public LandingSubject NamedFor(LandingSubject own) =>
        this with { Quest = own.Quest, Title = own.Title, Name = own.Name, Body = own.Body, Links = own.Links };

    /// <summary>The branch pattern's <c>{slug}</c>: the quest's name in the words git takes, its title where it has no other.</summary>
    public string Slug => LandingRules.Slug(Name ?? Title);

    /// <summary>
    /// Whether this is a conversation's subject, its title its opening (<see cref="LandingRules.SubjectAsync"/>): no quest, no name, and
    /// the person's first words as its title (ACCEPTFOOT2, D164's 2026-10-16 note).
    /// </summary>
    public bool NamedByOpening => Quest is null && Name is null && Title is not null;

    /// <summary>
    /// This subject named for its work where it is a conversation's (ACCEPTFOOT2): its name the oldest commit subject its work holds
    /// beyond the line, as PROCESS1b-ii names a squash with no title, or empty where none was read, so its branch is never a slug of
    /// the person's first words. The opening stays its title, for the record and the squash, and is searched for a ticket's key after
    /// that subject. Any other subject is as it was.
    /// </summary>
    public LandingSubject ForWork(string? oldest) =>
        NamedByOpening ? this with { Name = string.IsNullOrWhiteSpace(oldest) ? "" : oldest.Trim() } : this;

    /// <summary>
    /// The title a squash's subject takes (ACCEPTFOOT2b): a quest's own title, but for a conversation, whose title is its opening, the
    /// oldest commit's subject <see cref="ForWork"/> named it by, or null where none was read, so the squash's own fallback takes the
    /// commits' first subject.
    /// </summary>
    public string? SquashTitle => Quest is null && Title is not null ? (string.IsNullOrWhiteSpace(Name) ? null : Name) : Title;

    /// <summary>
    /// The subject a quest is landed as: its title for the record, its name for the branch where they differ, and its body and
    /// links for a ticket's key to be found in. Blank and empty are carried as none, so a quest without them is the subject it was.
    /// </summary>
    public static LandingSubject Of(string session, QuestView quest) => new(session, quest.Id, quest.Title)
    {
        Name = quest.Name == quest.Title ? null : quest.Name,
        Body = string.IsNullOrWhiteSpace(quest.Body) ? null : quest.Body,
        Links = quest.Links.Count > 0 ? quest.Links : null,
    };
}

/// <summary>What a press would do: the form, where the work would go (the line, or the branch it would make), and what said so.</summary>
/// <param name="Plugin">The plugin that pushes the branch once it is made (D100), or null for the person.</param>
/// <param name="Problem">
/// The sentence a press would be refused with now, or null: the plugin cannot land work here, the name entered is refused, or the
/// standing branch may not be moved on (STANDSREFUSE1, <see cref="LandingAdvance.Standing"/>).
/// </param>
/// <param name="AutoAccept">
/// Whether the rule accepts automatically (LAND2b, D145): the quest's done lands the work, with no press, and the session's
/// instruction says so.
/// </param>
public sealed record LandingPlan(string Form, string Target, string Source, string? Plugin = null, string? Problem = null, bool AutoAccept = false)
{
    /// <summary>
    /// The target is the branch the session's own agent made in its tree (TREEFOLLOW1, D164 point 4), which lands as it stands:
    /// no branch is made, the rule's pattern does not name it, and a name entered has nowhere to go. False for a tree still on its
    /// session branch, where the branch form makes the one the pattern names.
    /// </summary>
    public bool Own { get; init; }

    /// <summary>
    /// The branch the press lands on already stands in the repository (ACCEPTALL1g, D167's 2026-10-15 amendment point 2): an entry's or
    /// a chain's branch an earlier landing made, which the press moves on (D145 §3) instead of making it. The plain fact: one the
    /// press may not move on (Daoris did not make it, it moved since, a tree has it checked out) stands too, its refusal said in
    /// <see cref="Problem"/> (STANDSREFUSE1). False for a branch the press would make, for the tree's own branch (<see cref="Own"/>),
    /// and under a merge rule.
    /// </summary>
    public bool Stands { get; init; }

    /// <summary>
    /// The tree's commits the line already holds, which the press would leave out, oldest first (LANDSYNC1, D87's 2026-10-15
    /// amendment point 4): judged against the line as last fetched, since the plan reaches no network. Null where the line holds
    /// none of them, and the press lands the tree as before.
    /// </summary>
    public IReadOnlyList<LeftOutCommit>? LeftOut { get; init; }

    /// <summary>What the press would replay onto the line, oldest first, beside <see cref="LeftOut"/>; null where that is null.</summary>
    public IReadOnlyList<LandingCommit>? Lands { get; init; }

    /// <summary>
    /// Where replaying <see cref="Lands"/> onto the line conflicts now (a trial that writes no ref and no tree), and the words that
    /// would ask the session to rebase: the press would be refused. Null where it replays cleanly, or nothing is left out.
    /// </summary>
    public LandingConflict? Conflict { get; init; }
}

/// <summary>Why a landing leaves a commit out (LANDSYNC1, D87's 2026-10-15 amendment points 1 and 2).</summary>
public static class LeftOutWhy
{
    /// <summary>The run from where the tree left the line whose work reads on the line by content (WSR6's walk): a squash of several.</summary>
    public const string Content = "content";

    /// <summary>A commit the line gained since has its patch id (what <c>git cherry</c> marks <c>-</c>): a one-commit squash or a cherry-pick.</summary>
    public const string Patch = "patch";

    /// <summary>Kept, and found empty by the replay: its changes are already on the line.</summary>
    public const string Empty = "empty";
}

/// <summary>One commit a landing leaves out, and why (<see cref="LeftOutWhy"/>).</summary>
/// <param name="On">The line's commit with the same patch, for <see cref="LeftOutWhy.Patch"/> where git paired one; else null.</param>
public sealed record LeftOutCommit(string Sha, string Subject, string Why, string? On = null);

/// <summary>
/// A replay onto the line that conflicts (LANDSYNC1, D87's amendment point 5): git's <c>CONFLICT</c> lines, and the words the review
/// offers to say to the session, asking it to replay its own commits onto the line, keeping only what the line lacks.
/// </summary>
public sealed record LandingConflict(string Says, string Words);

/// <summary>What came of a press. A refusal is an answer, as the merge door's are, and names what the person would do.</summary>
/// <param name="Plugin">What the rule's plugin answered once the branch was made (D100) — null where no plugin was spoken to.</param>
/// <param name="Tidied">
/// What the rule's tidy did with the other session branches the landed work holds (LAND3), each removed or kept and why —
/// null where no tidy ran. The message says the same, so the landing's note keeps it.
/// </param>
public sealed record TreeLanding(
    bool Landed, string Message, string? Branch = null, PluginLanding? Plugin = null, IReadOnlyList<TidiedBranch>? Tidied = null)
{
    /// <summary>
    /// A refusal's code (LAND2b), one of <see cref="AutoLandingCode"/>: <c>uncommitted</c>, <c>nothing</c>, <c>exists</c> or, where a
    /// chain's branch was merged, <c>completed</c> (LAND2c) where the branch form said so, else null, which a landing at done keeps
    /// as <c>refused</c>. The message says it to a person.
    /// </summary>
    public string? Refusal { get; init; }

    /// <summary>
    /// Why the rule's plugin could not land work here, where a landing at done made the branch without it (LAND2b, D145
    /// point 2): nobody is there to fix the plugin, so the branch is made and the push is not tried. Null at a press, which
    /// refuses before anything is made instead (D100).
    /// </summary>
    public string? Unready { get; init; }

    /// <summary>
    /// Why its push was held back where its plugin could have pushed (FABRIC-CLOSE1h2): the automatic landing's quest was held, taken
    /// back or submitted again after the branch was made and before the plugin was told. The branch stands, recorded, and no plugin
    /// was spoken to. Null wherever the push went on, and at every other door, which asks no judgement there.
    /// </summary>
    public string? Withheld { get; init; }

    /// <summary>
    /// Where the landing moved a branch Daoris made on from (LAND2c, D149 point 2): the commit it stood at, recorded, before this
    /// session's work fast-forwarded it. Null for a branch made new, and for every merge.
    /// </summary>
    public string? AdvancedFrom { get; init; }

    /// <summary>
    /// What the landing left out because the line holds it, and what its replay found empty, oldest first (LANDSYNC1, D87's
    /// 2026-10-15 amendment point 4). Null where the line held none of the tree's commits and it landed them as made.
    /// </summary>
    public IReadOnlyList<LeftOutCommit>? LeftOut { get; init; }

    /// <summary>The replay's conflict, where that refused the landing (D87's amendment point 5); null otherwise.</summary>
    public LandingConflict? Conflict { get; init; }
}

/// <summary>
/// What a plugin answered at a landing (WSR4, D100): whether it pushed the branch, the pull request it
/// opened where it opened one, and its own sentence.
/// </summary>
/// <param name="Failed">
/// The plugin's step did not complete — it could not start, answered late or wrongly, or refused the
/// call — and <paramref name="Message"/> is Daoris's sentence saying which. A plugin that answered it
/// did not push is not a failure of the wire: its own sentence says why.
/// </param>
public sealed record PluginLanding(string Plugin, bool Pushed, string? PullRequest, string Message, bool Failed = false);

/// <summary>One commit a landing carries, oldest first: its full id and its subject line.</summary>
public sealed record LandingCommit(string Sha, string Subject);

/// <summary>
/// What a plugin is told at a landing (D100) — the one frame on <see cref="HookPoints.Land"/>, sent once
/// the branch exists.
/// </summary>
/// <param name="Root">The repository's own checkout on this machine, where the plugin runs its push.</param>
/// <param name="Branch">The branch Daoris just made, holding the session's work.</param>
/// <param name="Base">The line the work grew from, which a pull request goes into — null where git can name none.</param>
/// <param name="Title">What the work is called: the quest's title, else what the person first said, else null.</param>
/// <param name="Quest">The chain's first quest (WSR5), or null for a conversation.</param>
/// <param name="PullRequest">
/// The pull request the landing record holds for this branch (LAND2c, D149 point 4): one a plugin opened at the chain's first
/// landing, which this push grows, so the plugin pushes and opens no second one. Null where none is open from it yet.
/// </param>
/// <param name="AcceptedBy">
/// Who accepted the work (LAND2c, D145 point 3), one of <see cref="Daoris.Driver.AcceptedBy"/>: the description a plugin
/// writes says it, and it is not always the person who reviewed it.
/// </param>
/// <param name="Tip">
/// The commit the landing judged and put the branch at (LANDPIN2, D173's FABRIC-END7 note), which the plugin pushes to the branch
/// (<c>git push origin &lt;tip&gt;:refs/heads/&lt;branch&gt;</c>) rather than the branch as it stands at the push: a tree on a branch of
/// its own (TREEFOLLOW1) has no ref made for it, so a commit made there after the judgement would otherwise ride the push unseen.
/// Null in a frame from before it, where a plugin pushes the branch by name.
/// </param>
public sealed record LandingFrame(
    string Repository, string Workspace, string Root, string Branch, string? Base, string? Title,
    string? Quest, string Session, IReadOnlyList<LandingCommit> Commits, string? PullRequest = null,
    string AcceptedBy = Daoris.Driver.AcceptedBy.Person, string? Tip = null);

/// <summary>
/// What an advance of a standing branch is judged by (LAND2c, D149 points 2–3), read from git at the press or the look: the
/// record's entry for it, where it stands, where it is checked out, whether the session's work grows from it, and whether its
/// work already reads on the line.
/// </summary>
/// <param name="Recorded">The landing record's standing entry for this branch in this repository, or null: not Daoris's.</param>
/// <param name="Tip">The commit the branch stands at now.</param>
/// <param name="CheckedOutAt">The working tree that has it checked out, or null.</param>
/// <param name="Descends">Whether the session's work grows from the recorded tip: its tip has that commit in its history.</param>
/// <param name="Ahead">How many commits the session's work holds beyond the recorded tip.</param>
/// <param name="Completed">
/// Where D102's proof found the recorded tip's work on the line — a form of it, such as <c>origin/main</c> — so its pull
/// request was merged; null where it does not read there.
/// </param>
public sealed record AdvanceFacts(LandedBranch? Recorded, string Tip, string? CheckedOutAt, bool Descends, int Ahead, string? Completed);

/// <summary>
/// Whether a landing may move a standing branch on (LAND2c, D145 §3, D149): pure, so its table holds every refusal without
/// git. Only a branch Daoris made and recorded, standing at the recorded tip, checked out nowhere, whose work does not
/// already read on the line, to a commit that grows from that tip with something new.
/// </summary>
public static class LandingAdvance
{
    /// <summary>
    /// What the session's work must grow from to move the branch on: the tip the record says was squashed, under a squash rule where
    /// it says one (PROCESS1b-ii point 4), else the tip the branch was made or last moved at. 🔴 Work replayed onto the branch's tip (a
    /// part joining its entry's branch, ACCEPTALL1b, or past what the line holds, LANDSYNC1) grows from that tip under a squash too
    /// (ACCEPTALL1d, D87's 2026-10-15 amendment point 3): the squashed tip is another tree's, which the replay does not hold.
    /// </summary>
    /// <param name="squash">The rule squashes the work into one commit.</param>
    /// <param name="replayed">The work was replayed onto the branch's tip, rather than being the session's own tip.</param>
    public static string? GrewFrom(LandedBranch? recorded, bool squash, bool replayed) =>
        squash && !replayed ? recorded?.Squashed ?? recorded?.Tip : recorded?.Tip;

    /// <summary>The refusal, in its code and its sentence, or null where the branch may move on.</summary>
    /// <param name="name">The branch the pattern names.</param>
    /// <param name="sessionBranch">The session's own branch, whose work would move it.</param>
    public static TreeLanding? Refusal(string name, string repository, string sessionBranch, AdvanceFacts facts)
    {
        if (Standing(name, repository, facts.Recorded, facts.Tip, facts.CheckedOutAt) is { } standing) return standing;

        if (facts.Completed is { } where)
        {
            return new TreeLanding(false,
                $"`{name}`'s work already reads on `{where}` — its pull request was merged — so Daoris does not move it on: commits "
                + "added to it now would ride no pull request. Bring the repository up to date (`daoris-driver trees sync`, or "
                + "Bring up to date on the page), which replays this session's own commits onto the line and removes that branch; "
                + $"then Accept lands this work on a fresh `{name}`, and its plugin opens a new pull request.")
            { Refusal = AutoLandingCode.Completed };
        }

        if (!facts.Descends)
        {
            return Exists($"`{sessionBranch}` does not grow from `{name}`, so moving it on would not be a fast-forward, and Daoris "
                + "merges nothing into a branch. Land this work by hand, or change the pattern with `daoris driver landing`.");
        }

        return facts.Ahead > 0
            ? null
            : new TreeLanding(false, $"`{name}` already holds everything on `{sessionBranch}` — nothing to land.")
            {
                Refusal = AutoLandingCode.Nothing,
            };
    }

    /// <summary>
    /// The refusal a standing branch meets from what is cheap to read, or null where those facts allow the move (STANDSREFUSE1,
    /// D167's ACCEPTALL1g note): not the record's, moved since the recorded tip, checked out in a working tree, or its pull request
    /// closed by the platform's kept word. The plan says it before the press in these words, which are the press's own (<see
    /// cref="Refusal"/> asks this first); git's proof that its work reads on the line, and how the session's work grows from it, are
    /// the press's alone, since they need the line's forms and the work it would land.
    /// </summary>
    /// <param name="tip">The commit the branch stands at now.</param>
    /// <param name="checkedOutAt">The working tree that has it checked out, or null; read only where the record and the tip allow a move.</param>
    public static TreeLanding? Standing(string name, string repository, LandedBranch? recorded, string tip, string? checkedOutAt)
    {
        if (recorded is null)
        {
            return Exists($"`{name}` is already a branch in `{repository}`, and Daoris does not move a branch it did not make. "
                + "Rename or delete it there, or change the pattern with `daoris driver landing`.");
        }

        if (!string.Equals(tip, recorded.Tip, StringComparison.OrdinalIgnoreCase))
        {
            return Exists($"`{name}` moved since Daoris landed on it (it stood at {Short(recorded.Tip)}, and stands at {Short(tip)} "
                + "now), so Daoris does not move it on: what moved it may be somebody's. Land this work by hand, or rename "
                + "that branch and accept again.");
        }

        if (checkedOutAt is { } at)
        {
            return Exists($"`{name}` is checked out at {at}, and Daoris does not move a branch a working tree stands on. "
                + "Switch that tree to another branch, then accept again.");
        }

        // PLUGHOOK1a (D149 point 3): the platform's kept word refuses as git's own proof does, since git cannot see a squash once
        // the line has moved on, and a closed pull request takes no new commits.
        if (recorded.PullRequestState is { State: PullRequestStates.Completed or PullRequestStates.Abandoned } kept)
        {
            var completed = kept.State == PullRequestStates.Completed;
            var answered = (kept.Plugin is { } plugin ? $", as `{plugin}` answered" : "")
                + (kept.AskedAt > DateTimeOffset.MinValue ? $" at {kept.AskedAt.UtcDateTime:yyyy-MM-dd HH:mm} UTC" : "");
            return new TreeLanding(false,
                $"`{name}`'s pull request {(completed ? "completed" : "was abandoned")}{answered}, so Daoris does not move it on: "
                + "commits added to it now would ride no pull request. "
                + (completed
                    ? "Bring the repository up to date (`daoris-driver trees sync`, or Bring up to date on the page), which replays "
                      + "this session's own commits onto the line and removes that branch; "
                    : $"Delete or rename `{name}` once you no longer want its work; ")
                + $"then Accept lands this work on a fresh `{name}`, and its plugin opens a new pull request.")
            { Refusal = AutoLandingCode.Completed };
        }

        return null;
    }

    private static TreeLanding Exists(string why) => new(false, why) { Refusal = AutoLandingCode.Exists };

    private static string Short(string commit) => TextBounds.ShortCommit(commit, 8);
}

/// <summary>What a session branch holds, for the clean-up's list (D88). Only the empty, the landed and the carried go.</summary>
public static class SweepKind
{
    /// <summary>Nothing beyond the line.</summary>
    public const string Empty = "empty";

    /// <summary>
    /// Every commit on a branch of the person's; or, where a squash merge or a cherry-pick left them on none, its work there by
    /// content (SQUASHTIDY1, <see cref="SweepItem.HeldBy"/>).
    /// </summary>
    public const string Landed = "landed";

    /// <summary>Commits only Daoris's branches hold — kept, and named.</summary>
    public const string Unlanded = "unlanded";

    /// <summary>
    /// Commits no branch of the person's holds, which a landed branch's completed pull request carried, as its plugin answered and
    /// git confirms (PLUGHOOK1a, D148 point 4): what a squash leaves once the platform deleted the source branch.
    /// </summary>
    public const string Carried = "carried";

    /// <summary>Its tree holds uncommitted work — kept.</summary>
    public const string Dirty = "dirty";

    /// <summary>A session still running or waiting names its tree — kept.</summary>
    public const string InUse = "in-use";
}

/// <param name="Commits">Unlanded and carried: how many only Daoris holds. Landed: how many it carried.</param>
/// <param name="Where">Landed: the first branch of the person's that holds it, or by content what holds its work. Empty: the line. Carried: the landed branch whose pull request carried it.</param>
/// <param name="Detail">Git's own lines where they say more: the unlanded commits, the uncommitted count.</param>
public sealed record SweepItem(
    string Repository, string Workspace, string Branch, string? Tree, string Kind, int Commits, string? Where, string? Detail)
{
    public bool Removable => Kind is SweepKind.Empty or SweepKind.Landed or SweepKind.Carried;

    /// <summary>Carried: the landing entry whose completed pull request carried it (PLUGHOOK1a), on which its removal is kept.</summary>
    public LandedBranch? CarriedBy { get; init; }

    /// <summary>
    /// Landed by content (SQUASHTIDY1): where its work is, though no branch of the person's holds its commits, and how it was
    /// found there. Null for a branch landed by ancestry, and for every other kind.
    /// </summary>
    public ContentHold? HeldBy { get; init; }

    /// <summary>
    /// git could not answer one of the guards (AUTOTIDY1): what its tree holds, or what its commits are. The kind is the safe
    /// side's (dirty, unlanded), and the look's own tidy says why it stays, once.
    /// </summary>
    public bool Unread { get; init; }

    /// <summary>
    /// How many ignored paths its tree holds that the checkout holds too (AUTOTIDY1): a build's output, which goes with the tree
    /// at the press (SQUASHTIDY1c), and with its folder moved aside at the look's own tidy (AUTOTIDY2a). Zero for a branch with no
    /// tree here.
    /// </summary>
    public int IgnoredShared { get; init; }

    /// <summary>
    /// It made commits since it grew (AUTOTIDY1; on every kind once its tip is read, AUTOTIDY2a): its tip is not the commit it
    /// started at, by the record of where it grew from (WSR6); false where the record has no entry for it. A branch that never
    /// moved is a conversation's place, which words to it go on in (D137), so the look's own tidy leaves its tree for the press.
    /// </summary>
    public bool Worked { get; init; }

    /// <summary>
    /// Landed, judged for the look's own tidy (AUTOTIDY2a): its work is on the line itself, every commit an ancestor of the line's
    /// own branch or origin's copy, or held there by content; or a squashing landing's branch the line holds. False where it is
    /// only on another branch of the person's, whose session the person may still ask to change it: the look waits for the line,
    /// and the press may take it before. False on every row the press's list judged.
    /// </summary>
    public bool OnLine { get; init; }

    /// <summary>
    /// The branch the session's tree moved onto (TREEFOLLOW1): its agent checked out a branch of its own there, as its repository's
    /// rules ask, so no tree has this session branch out any more and the work is on that branch. Null where the tree is on this
    /// one, or gone. The tree is that branch's: it is never taken with this one.
    /// </summary>
    public string? TreeOn { get; init; }
}

/// <summary>What the clean-up did with one branch, in the driver's words.</summary>
public sealed record SweepResult(SweepItem Item, bool Removed, string Message)
{
    /// <summary>Kept by the look's own tidy (AUTOTIDY1): why, as the machine log's code (<see cref="TidyKept"/>); null where the press kept it.</summary>
    public string? Kept { get; init; }

    /// <summary>Which <see cref="TidyKept.Unread"/> it was (AUTOTIDY1a2, <see cref="TidyCause"/>); null for any other reason.</summary>
    public string? Cause { get; init; }

    /// <summary>Removed by the look's own tidy (AUTOTIDY1): where its tree's folder was moved aside to; null where it had no folder here.</summary>
    public string? MovedTo { get; init; }

    /// <summary>
    /// Removed though held by content (SQUASHTIDY1c): the ref its commits were kept on, for the look's report and log to name
    /// (AUTOTIDY2a); null where none was kept.
    /// </summary>
    public string? KeptRef { get; init; }
}

/// <summary>
/// The landing rules (WSR1, D87): which applies to a repository, whether a pattern can name a branch,
/// and what it names for one session. The CLI's <c>driverconfig.ts</c> holds the same pattern rule.
/// </summary>
public static class LandingRules
{
    /// <summary>
    /// What a pattern may say. One of the first three is required, or every session's work would go to one branch: a ticket names
    /// a branch as the quest does (PROCESS1b-i), two sessions on one ticket meeting the second's <c>exists</c> refusal.
    /// </summary>
    public static readonly IReadOnlyList<string> Placeholders = ["quest", "session", "ticket", "slug", "repository"];

    /// <summary>What a pattern is tried against before it is kept — the CLI's twin tries the same names.</summary>
    private static readonly LandingNames Sample = new("0fda18", "sample-work", "engine", "s1a2b3c4", "ABC-123");

    private static readonly Regex Placeholder = new(@"\{([^{}]*)\}", RegexOptions.CultureInvariant);

    /// <summary>
    /// The shape of a ticket's key where the rule names none (PROCESS1b-i, D164's amendment point 2): a project's capitals, a dash
    /// and its number, as <c>TK-2202</c>. The CLI's <c>driverconfig.ts</c> names the same shape.
    /// </summary>
    public const string DefaultKey = @"[A-Z][A-Z0-9]+-\d+";

    /// <summary>How long a key may be: a shape, not a program. The CLI's twin holds the same bound.</summary>
    public const int KeyLimit = 64;

    /// <summary>
    /// The letters a key may escape (PROCESS1b-i): the classes and controls .NET and JavaScript read alike. <c>\x</c>, <c>\u</c>,
    /// <c>\c</c>, <c>\p</c> and the rest differ, or are refused by one engine. The CLI's <c>SHARED_ESCAPES</c> is the same list.
    /// </summary>
    public const string SharedEscapes = "dDwWsSbBnrtfv";

    /// <summary>How long one search for a key may run before its words are passed over: a key is the person's, and need not be quick.</summary>
    private static readonly TimeSpan KeyTimeout = TimeSpan.FromMilliseconds(100);

    /// <summary>A web address in a title (LANDNAME2): never a branch's words, as <c>feature/https-…-browse-a267d192</c> was.</summary>
    private static readonly Regex WebAddress = new(@"\b(?:https?://|www\.)\S*", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    /// <summary>
    /// What is wrong with a rule's shape, in a sentence, or null when it can land work. Whether its plugin
    /// is on this machine is <see cref="PluginProblem"/>'s question: a file stays readable wherever it is.
    /// </summary>
    public static string? Problem(LandingRule rule) => rule.Form switch
    {
        // A merge makes no branch, and the plugin starts from the branch Daoris made (D100).
        LandingForm.Merge when rule.Plugin is not null =>
            "only a branch rule hands its work to a plugin — the plugin pushes the branch Daoris made, and a merge "
            + "makes none. `branch <pattern> --plugin <id>` is the form that does.",
        // A merge writes into the person's checkout, and with no press nothing would stand between the work and the
        // line (D145 point 1, D51 rule 6).
        LandingForm.Merge when rule.AutoAccept =>
            "only a branch rule accepts automatically — a merge writes into your checkout, and with no press nothing would "
            + "stand between the work and the line. `branch <pattern> --auto-accept` is the form that does.",
        // A ticket's key names a branch (PROCESS1b-i), and a merge makes none.
        LandingForm.Merge when rule.Key is not null =>
            "only a branch rule reads a ticket's key — a merge makes no branch to name. `branch <pattern> --key <shape>` is the form that does.",
        // A squash makes the pull request's branch one commit (PROCESS1b-ii), and a merge makes no branch.
        LandingForm.Merge when rule.Squash =>
            "only a branch rule squashes — a merge makes no branch to squash the work onto. `branch <pattern> --squash` is the form that does.",
        LandingForm.Merge => null,
        // A plugin's id is the catalogue's own shape, asked of its one owner (REFAC1), so a rule never names a path.
        LandingForm.Branch => Problem(rule.Pattern ?? "") ?? (rule.Plugin is { } plugin && !PluginCatalog.IsId(plugin)
            ? $"`{plugin}` is not a plugin id — one is lowercase letters, digits, dots and dashes, like `example.github-pull-request`."
            : null) ?? KeyProblem(rule),
        _ => $"`{rule.Form}` is not a way work lands here — `{LandingForm.Merge}` or `{LandingForm.Branch}`. "
             + "Pushing and opening a pull request is a plugin's to do, named on the branch form: "
             + "`branch <pattern> --plugin <id>`.",
    };

    /// <summary>
    /// Why the plugin a rule names cannot land work on this machine, in a sentence, or null when it can
    /// (D100): not installed, switched off, refused by the catalogue, or speaking on no
    /// <see cref="HookPoints.Land"/> point. Asked when the rule is set and again at the press, so a plugin
    /// gone since is refused before anything is made. The CLI's <c>driverconfig.ts</c> is the twin.
    /// </summary>
    public static string? PluginProblem(string plugin, PluginCatalog catalog)
    {
        var entry = catalog.Plugins.FirstOrDefault(p => string.Equals(p.Manifest.Id, plugin, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            return $"the landing rule names plugin `{plugin}`, which is not installed on this machine — "
                + "`daoris plugin add <folder>` installs one, and `daoris plugin list` shows what there is.";
        }

        if (!entry.Enabled)
        {
            return $"plugin `{plugin}` is switched off on this machine — `daoris plugin enable {entry.Manifest.Id}` switches it on.";
        }

        if (entry.Problem is { } problem) return $"plugin `{plugin}` contributes nothing: {problem}";

        return entry.Manifest.Hooks?.Points.Contains(HookPoints.Land, StringComparer.Ordinal) == true
            ? null
            : $"plugin `{plugin}` does not land work — its manifest speaks on no `{HookPoints.Land}` point.";
    }

    /// <summary>
    /// What a door says as <i>Accept automatically</i> is set (LAND2a, D145 point 5, design §6): the switch is the
    /// person's standing say-so for a push with no press, so it is said where it is given. With no plugin, the warning
    /// that nothing leaves the machine. The CLI's <c>driverconfig.ts</c> says the same words; the page says them in its
    /// own catalogue.
    /// </summary>
    public static string AutoAcceptSays(string? plugin) => plugin is { } named
        ? $"when a quest here is done, its work is put on its branch, and `{named}` pushes it and opens a pull request without "
          + "asking you each time. The pull request is where it is judged. Switch it off to accept each one yourself."
        : "when a quest here is done, its work is put on its branch, and nothing leaves this machine: no plugin opens a pull "
          + "request, so each done's branch waits here for you to push it. Switch it off to accept each one yourself.";

    /// <summary>
    /// What finds a branch's ticket (PROCESS1b-i), said as a rule is set: by <c>daoris-driver driver landing</c> and Ask Daoris's card
    /// (TWINWORDS1, <c>landing-says.json</c>'s <c>ticket</c>).
    /// </summary>
    public static string TicketSays(string? key) =>
        $"`{{ticket}}` is the first match of `{key ?? DefaultKey}` in the title, then the request, then its links, as written"
        + $"{(key is null ? " (the default shape)" : "")}; where none is found, the session's id stands in.";

    /// <summary>
    /// What Accept makes under a rule that squashes (PROCESS1b-ii), in the terminal's three lines; Ask Daoris's card says them as one
    /// (<see cref="Squashed"/>, <c>landing-says.json</c>'s <c>squash</c>).
    /// </summary>
    public static IReadOnlyList<string> SquashSays { get; } =
    [
        "Squashed: Accept makes the pull request's branch one commit from the session's work — the quest's title, then",
        "each of the session's commits' subjects — made by git as the repository's own `user.name` and `user.email`;",
        "where git has none there, Accept is refused and says how to set them. A tree on a branch of its own lands as it stands.",
    ];

    /// <summary><see cref="SquashSays"/> as one sentence.</summary>
    public static string Squashed => string.Join(' ', SquashSays);

    /// <summary>
    /// What the conversation's record keeps of a landing (D100): the whole sentence the press said, the
    /// plugin's part in it. Machine-local, like every note there — a plugin's word never rides the
    /// session record, which travels (D64 §4).
    /// </summary>
    public static SessionEvent Note(TreeLanding landed) => Press(NoteCodes.LandingPersonAccepted, PersonAccepted, landed.Message);

    /// <summary>
    /// A press's note by its code (CONVNOTE2c): the lead is Daoris's, its sentence the program's words beneath, as written, for
    /// they carry git's and the plugin's words beside Daoris's. The English is the lead, a space and the sentence, as before.
    /// </summary>
    private static SessionEvent Press(NoteCode code, string lead, string message)
    {
        var said = Noted.Of(code, lead).Then(" ", Noted.Said(message, NoteBy.Program));
        return new SessionEvent { Kind = SessionEventKind.Note, Text = said.Note, Parts = said.Parts };
    }

    /// <summary>
    /// What a part's conversation keeps where the person left it out of its entry's Accept (ACCEPTALL1c, D167's 2026-10-15 amendment
    /// point 3): the branch its line of work landed on, or the line it merged into, so its review says so and leads with Discard.
    /// Machine-local, as every note there is; landing it later is a press of its own.
    /// </summary>
    public static SessionEvent LeftOutNote(string branch)
    {
        var said = Noted.Of(NoteCodes.LandingLeftOut,
            $"the person left this part out when its line of work landed on `{branch}`: its work did not land there.", ("branch", branch));
        return new SessionEvent { Kind = SessionEventKind.Note, Text = said.Note, Parts = said.Parts };
    }

    /// <summary>
    /// Where a part's review reads it left out (ACCEPTALL1c): the branch the newest <see cref="LeftOutNote"/> names, while no acceptance
    /// (<see cref="IsAcceptance"/>, a press of its own or a landing at done) came after it; null where none says it.
    /// </summary>
    public static string? LeftOutOf(IReadOnlyList<SessionEvent> events)
    {
        for (var at = events.Count - 1; at >= 0; at--)
        {
            if (events[at] is not { Kind: SessionEventKind.Note } note) continue;
            if (note.Text is { } text && IsAcceptance(text)) return null;
            if (note.Parts?.FirstOrDefault(part => part.Code == NoteCodes.LandingLeftOut.Code)?.Value("branch") is string branch) return branch;
        }

        return null;
    }

    /// <summary>How the conversation's note of a press opens: what the trace knows an acceptance by (TRACE1).</summary>
    public const string PersonAccepted = "the person accepted this work:";

    /// <summary>
    /// How the conversation's note of a landing at done opens (LAND2b, D145 point 4): who accepted it, which the review's note
    /// and the trace name. The rest of the note is its code's (<see cref="AutoLandingNotes"/>).
    /// </summary>
    public const string AutoAccepted = "accepted automatically when its quest was done:";

    /// <summary>Whether a conversation's note is an acceptance of the session's work: a press's, or a landing at done.</summary>
    public static bool IsAcceptance(string text) =>
        text.StartsWith(PersonAccepted, StringComparison.Ordinal) || text.StartsWith(AutoAccepted, StringComparison.Ordinal);

    /// <summary>
    /// What the conversation's record keeps of a hand-off after the landing (WSR5b): the whole sentence, the
    /// plugin's part in it — kept where the landing's own note is, and for the same reason (D100).
    /// </summary>
    public static SessionEvent HandNote(TreeHand handed) =>
        Press(NoteCodes.LandingPersonHanded, "the person handed this work on:", handed.Message);

    /// <summary>What is wrong with a branch pattern, in a sentence, or null when it names one branch per session.</summary>
    public static string? Problem(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return "a branch pattern needs a name — e.g. `feature/{quest}-{slug}`.";
        }

        foreach (Match used in Placeholder.Matches(pattern))
        {
            if (!Placeholders.Contains(used.Groups[1].Value))
            {
                return $"`{used.Value}` is not something a pattern can say — "
                    + string.Join(", ", Placeholders.Select(name => $"`{{{name}}}`")) + ".";
            }
        }

        // A ticket names a branch as a quest does (PROCESS1b-i); the sentence still offers the two that are one per session.
        if (!pattern.Contains("{quest}", StringComparison.Ordinal) && !pattern.Contains("{session}", StringComparison.Ordinal)
            && !pattern.Contains("{ticket}", StringComparison.Ordinal))
        {
            return "a pattern needs `{quest}` or `{session}`, or every session's work would be put on one branch.";
        }

        var sample = Expand(pattern, Sample);
        return sample.Contains('{') || sample.Contains('}') || !BranchName.IsValid(sample)
            ? $"`{pattern}` does not make a branch name git would take — it gives `{sample}`."
            : null;
    }

    /// <summary>
    /// What is wrong with a rule's ticket key, in a sentence, or null where it has none or can name a branch (PROCESS1b-i): read
    /// only for <c>{ticket}</c>, and held to <see cref="KeyProblem(string)"/>'s shape.
    /// </summary>
    private static string? KeyProblem(LandingRule rule) => rule.Key is not { } key ? null
        : !(rule.Pattern ?? "").Contains("{ticket}", StringComparison.Ordinal)
            ? $"a ticket's key is read only for `{{ticket}}`, and `{rule.Pattern}` does not say it — add `{{ticket}}` to the pattern, or take the key away."
            : KeyProblem(key);

    /// <summary>
    /// What is wrong with a ticket's key, in a sentence, or null where both readers of a rule take it the same way (PROCESS1b-i):
    /// the driver matches it in .NET and the CLI checks it in JavaScript, so it is held to what the two engines read alike — at
    /// most <see cref="KeyLimit"/> characters, no <c>(?</c> group (lookaround, named group, inline option), no back-reference,
    /// a letter escaped only as one of <see cref="SharedEscapes"/> (a punctuation mark may be escaped), and no empty class. Each
    /// engine's own compile is the last word. The CLI's <c>keyProblem</c> is the twin, sentence for sentence.
    /// </summary>
    /// <remarks>
    /// 🔴 KEYPARITY: a key one engine takes and the other refuses was written by the CLI and then not read by the driver
    /// (<c>TK\_\d+</c>, <c>\x</c> with no digits, <c>[^]</c>); <c>[]a]</c> compiles in both and means two things.
    /// </remarks>
    public static string? KeyProblem(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return $"a ticket's key needs a shape — e.g. `{DefaultKey}`, the one read where a rule names none.";
        }

        if (key.Length > KeyLimit) return $"a ticket's key is at most {KeyLimit} characters, and this one is {key.Length}.";

        var inClass = false;
        for (var at = 0; at < key.Length; at++)
        {
            var next = at + 1 < key.Length ? key[at + 1] : '\0';
            if (key[at] == '\\')
            {
                if (next is 'k' or (>= '1' and <= '9'))
                {
                    return "a ticket's key may not refer back to a group (`\\k` or `\\1` to `\\9`), which the driver and the CLI read differently.";
                }

                // A punctuation mark or symbol escaped is itself in both; `_`, a digit, a space and any other letter are not read alike.
                var punctuation = next is > ' ' and < '\x7f' && !char.IsAsciiLetterOrDigit(next) && next != '_';
                if (next != '\0' && !SharedEscapes.Contains(next) && !punctuation)
                {
                    return $"a ticket's key may escape only a punctuation mark or one of `{string.Join(' ', SharedEscapes.Select(c => $"\\{c}"))}`, "
                        + $"which the driver and the CLI read alike, and `\\{next}` is neither.";
                }

                at++; // the escaped character is the escape's own: `\\1` is a backslash and a one, `\(?` a bracket made optional
            }
            else if (inClass)
            {
                // .NET reads `[a-z-[aeiou]]` as a class minus a class, JavaScript as a class then `]` (KEYPARITY2).
                if (key[at] == '-' && next == '[')
                {
                    return "a ticket's key may not subtract a class (`-[` inside a class), which the driver and the CLI read differently.";
                }

                if (key[at] == ']') inClass = false;
            }
            else if (key[at] == '(' && next == '?')
            {
                return "a ticket's key may not use `(?` — a lookaround, a named group or an inline option, which the driver and the "
                    + "CLI read differently.";
            }
            else if (key[at] == '[')
            {
                // `[]` and `[^]` are an empty class in JavaScript, and a class holding `]` in .NET.
                if (next == ']' || (next == '^' && at + 2 < key.Length && key[at + 2] == ']'))
                {
                    return "a ticket's key may not hold an empty class, `[]` or `[^]`, which the driver and the CLI read differently.";
                }

                inClass = true;
            }
        }

        try
        {
            _ = new Regex(key, RegexOptions.CultureInvariant, KeyTimeout);
            return null;
        }
        catch (ArgumentException)
        {
            return $"a ticket's key `{key}` is not a regular expression — e.g. `{DefaultKey}`.";
        }
    }

    /// <summary>
    /// A pattern filled with one session's names. A name that is empty — a title that was only a web address leaves no slug —
    /// takes with it the separator (<c>-</c>, <c>_</c>, <c>.</c>, <c>/</c>) it would leave doubled, leading or trailing, keeping a
    /// <c>/</c> where the other side's is not one. A ticket the key did not find is the session's id.
    /// </summary>
    public static string Expand(string pattern, LandingNames names)
    {
        var text = new StringBuilder();
        var gaps = new List<int>();
        var from = 0;
        foreach (Match used in Placeholder.Matches(pattern))
        {
            text.Append(pattern, from, used.Index - from);
            from = used.Index + used.Length;
            var value = used.Groups[1].Value switch
            {
                "quest" => names.Quest,
                "session" => names.Session,
                "ticket" => names.Ticket ?? names.Session,
                "slug" => names.Slug,
                "repository" => names.Repository,
                _ => used.Value,
            };
            // Two empty names side by side leave one gap.
            if (value.Length > 0) text.Append(value);
            else if (gaps.Count == 0 || gaps[^1] != text.Length) gaps.Add(text.Length);
        }

        text.Append(pattern, from, pattern.Length - from);

        // From the last gap back, so a removal never moves a gap still to come.
        for (var at = gaps.Count - 1; at >= 0; at--)
        {
            var gap = gaps[at];
            var before = gap > 0 && IsSeparator(text[gap - 1]);
            var after = gap < text.Length && IsSeparator(text[gap]);
            if (before && after) text.Remove(text[gap - 1] == '/' && text[gap] != '/' ? gap : gap - 1, 1);
            else if (before && gap == text.Length) text.Remove(gap - 1, 1);
            else if (after && gap == 0) text.Remove(gap, 1);
        }

        return text.ToString();
    }

    private static bool IsSeparator(char c) => c is '-' or '_' or '.' or '/';

    /// <summary>
    /// A title in the words git takes: lower-case letters and digits, joined by hyphens, whole words up to 40 characters, with
    /// every web address left out (LANDNAME2). A title with no words git takes — one written in another script — is `work`; a
    /// title whose words were all an address leaves nothing, and <see cref="Expand"/> takes its separator with it.
    /// </summary>
    public static string Slug(string? title) => Slug(title, ticket: null);

    /// <summary>
    /// The slug of a title the pattern also names by its ticket (PROCESS1b-i): the ticket's own words left out too, in any case,
    /// so the branch does not say them twice; a title that was only the ticket leaves nothing.
    /// </summary>
    public static string Slug(string? title, string? ticket)
    {
        var text = WebAddress.Replace(title ?? "", " ");
        if (ticket is { Length: > 0 }) text = text.Replace(ticket, " ", StringComparison.OrdinalIgnoreCase);
        var slug = Words(text);
        return slug.Length > 0 ? slug : Words(title).Length > 0 ? "" : "work";
    }

    private static string Words(string? text)
    {
        var slug = new StringBuilder();
        foreach (var word in Regex.Split((text ?? "").ToLowerInvariant(), "[^a-z0-9]+").Where(word => word.Length > 0))
        {
            if (slug.Length + (slug.Length > 0 ? 1 : 0) + word.Length > 40) break;
            if (slug.Length > 0) slug.Append('-');
            slug.Append(word);
        }

        return slug.ToString();
    }

    /// <summary>
    /// The ticket a subject's work is for (PROCESS1b-i, D164's amendment point 2): the first match of <paramref name="key"/>, else
    /// <see cref="DefaultKey"/>, in its name, its title, its request's body, then each of its links, in that order, as written. A
    /// match git would not take in a branch's name is passed over, as are words a search runs past its time on. Null where none is
    /// found: the session's id stands in.
    /// </summary>
    public static string? Ticket(LandingSubject subject, string? key)
    {
        Regex shape;
        try
        {
            shape = new Regex(key ?? DefaultKey, RegexOptions.CultureInvariant, KeyTimeout);
        }
        catch (ArgumentException)
        {
            return null; // a key the rule's own check refuses is never kept, so none is found by it
        }

        string?[] sources = [subject.Name, subject.Title, subject.Body, .. subject.Links ?? []];
        foreach (var source in sources.OfType<string>())
        {
            try
            {
                for (var match = shape.Match(source); match.Success; match = match.NextMatch())
                {
                    if (BranchName.IsValid(match.Value)) return match.Value;
                }
            }
            catch (RegexMatchTimeoutException)
            {
                // These words took the key too long; the next source is searched.
            }
        }

        return null;
    }

    /// <summary>
    /// What one session's branch is named from under a rule: where the pattern says <c>{ticket}</c>, the ticket its key finds, and
    /// the slug without it (PROCESS1b-i); otherwise the names as they always were.
    /// </summary>
    public static LandingNames Names(LandingSubject subject, LandingRule rule, string repository)
    {
        var ticket = (rule.Pattern ?? "").Contains("{ticket}", StringComparison.Ordinal) ? Ticket(subject, rule.Key) : null;
        var slug = ticket is null ? subject.Slug : Slug(subject.Name ?? subject.Title, ticket);
        return new(subject.Quest ?? subject.Session, slug, repository, subject.Session, ticket);
    }

    /// <summary>The branch a branch rule's pattern names for one session: what the plan says and the press makes.</summary>
    public static string Branch(LandingSubject subject, LandingRule rule, string repository) =>
        Expand(rule.Pattern ?? "", Names(subject, rule, repository));

    /// <summary>
    /// Whether a rule's pattern reads a subject's words (ACCEPTFOOT2): <c>{slug}</c>, or <c>{ticket}</c>, searched in them. A pattern
    /// of ids alone names the branch the same whatever the work's words, so nothing need be read for them.
    /// </summary>
    public static bool ReadsWords(LandingRule rule) =>
        (rule.Pattern ?? "").Contains("{slug}", StringComparison.Ordinal) || (rule.Pattern ?? "").Contains("{ticket}", StringComparison.Ordinal);

    /// <summary>The git log format <see cref="OldestSubject"/> reads: each commit's subject after a unit separator, oldest first.</summary>
    public const string SubjectsFormat = "--format=%x1f%s";

    /// <summary>
    /// The oldest commit's subject from <c>git log --reverse</c> in <see cref="SubjectsFormat"/> (ACCEPTFOOT2): the first commit's,
    /// trimmed, empty where its message is, never a later commit's; null where the log names no commit.
    /// </summary>
    public static string? OldestSubject(string log)
    {
        var commits = log.Split('\u001f');
        return commits.Length < 2 ? null : commits[1].Trim();
    }

    /// <summary>
    /// What a landing is named for (WSR5): the session, and the chain's FIRST quest with its title — the
    /// one the ask became — walking up each step's parent; a chat with no quest has its opening line.
    /// </summary>
    /// <remarks>
    /// 🔴 A chain lands from its last step (WSR1), so naming it for that step's quest named TK-2202's work
    /// `feature/verify-in-prod-…-381807d1f7bd`, after its verify step. The walk stops where the service no
    /// longer answers for a parent, and after twenty steps, which no chain reaches.
    /// </remarks>
    public static async Task<LandingSubject> SubjectAsync(
        string session, string? quest, Func<string, Task<QuestView?>> find, string? opening)
    {
        if (quest is null) return new LandingSubject(session, null, opening);

        var current = await find(quest).ConfigureAwait(false);
        if (current is null) return new LandingSubject(session, quest, opening);
        for (var steps = 0; steps < 20 && current.Parent is { Length: > 0 } parent; steps++)
        {
            if (await find(parent).ConfigureAwait(false) is not { } above) break;
            current = above;
        }

        return LandingSubject.Of(session, current);
    }

    /// <summary>The repository's rule, else its workspace's (a repository in no workspace is in `default`'s), else merge.</summary>
    public static Landing Choose(DriverConfig config, string repository, string? workspace)
    {
        if (config.Landings.TryGetValue(repository, out var own)) return new(own, LandingSource.Repository);
        if (config.WorkspaceLandings.TryGetValue(RemoteTarget.Workspace(workspace), out var shared)) return new(shared, LandingSource.Workspace);
        return new(LandingRule.Merge, LandingSource.Default);
    }

    /// <summary>
    /// The rule work lands by (AUTONOMY1c2, D184 points 2, 3 and 5): the chosen rule (<see cref="Choose"/>), its switch on where it is a
    /// branch rule and the repository's access setup resolves <c>landing</c> to <c>rule</c>, full access among them, and where that was
    /// set (<see cref="Landing.Access"/>). Every place that judges whether done work lands with no press reads it, so none disagrees: the
    /// conclusion, the look's judgement and the gate's process (<see cref="WorkflowProcess.Current"/>), the landing's own refusal and the
    /// session's plan. Composed here and never written to <c>driver.json</c>, so the settings page, which reads the file's rules, still
    /// shows each rule's own switch. A merge rule keeps no switch whatever the setup: a merge writes into the person's checkout with no
    /// step after it (D51 rule 6, D145), so <i>Accept…</i> stays.
    /// </summary>
    public static Landing Effective(DriverConfig config, string repository, string? workspace)
    {
        var chosen = Choose(config, repository, workspace);
        var access = AccessRules.Resolve(config, repository, workspace);
        if (access.Value(AccessRules.Landing) != AccessRules.Rule || chosen.Rule is { Form: LandingForm.Branch, AutoAccept: true }) return chosen;
        var source = access.Source(AccessRules.Landing);
        return chosen.Rule.Form == LandingForm.Branch
            ? chosen with { Rule = chosen.Rule with { AutoAccept = true }, Access = source }
            : chosen with { Access = source };
    }
}
