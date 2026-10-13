namespace Daoris.Driver;

/// <summary>Whose work is read (D132 §1): an ask's, or one quest's.</summary>
public enum WorkScope
{
    /// <summary>An ask's: every quest it asked, chain steps included, and the ask's intake.</summary>
    Ask,

    /// <summary>One quest's: that quest alone where an ask's starts from what it asked, and no intake.</summary>
    Quest,
}

/// <summary>How a quest joined the work (design §1).</summary>
public enum WorkJoin
{
    /// <summary>Asked by the ask (<c>ask #id</c>), a chain's later step included: the service asks every step on the chain's asker's behalf.</summary>
    Asked,

    /// <summary>The quest a quest's work starts from.</summary>
    Named,

    /// <summary>Published by a session of the work (SESS1): a question it asked of another repository (D79), and so on down.</summary>
    Published,
}

/// <summary>A quest of the work, and how it joined.</summary>
/// <param name="By">For <see cref="WorkJoin.Published"/>, the session of the work that published it, as its record here is keyed; else null.</param>
public sealed record WorkQuest(QuestView Quest, WorkJoin Joined, string? By = null);

/// <summary>A session of the work: one whose quest is in it, or the ask's intake.</summary>
/// <param name="Tree">Its tree, where it is one this home opened (D51); null for a session that ran in the registered root, and for a teammate's.</param>
/// <param name="Branch">That tree's branch, <c>daoris/</c> and the tree's folder name (<see cref="SessionTrees"/>); null where there is no tree here.</param>
public sealed record WorkSession(SessionRecord Record, string? Tree = null, string? Branch = null)
{
    /// <summary>Whether it is the ask's intake (D65 §1b): it answers the ask, and serves no quest.</summary>
    public bool Intake { get; init; }

    /// <summary>A teammate's record (SYNC4): its process and its tree are on their machine (D47 §4), and nothing here reaches either.</summary>
    public bool Teammate => Record.Teammate;
}

/// <summary>One tree of the work on this machine, its branch, and the sessions that worked in it, oldest first: a carry-on goes back into its tree (D80).</summary>
public sealed record WorkTree(string Path, string Repository, string Branch, IReadOnlyList<string> Sessions);

/// <summary>
/// The work of an ask, or of one quest (D132 §1, design §1): its quests, its sessions, its trees and branches here, and the
/// landings that name a session of it. Every piece a pause (PAUSE1b) or an abandon (PAUSE1d) would act on, and nothing else.
/// </summary>
public sealed record WorkPieces(
    WorkScope Scope, string Id, IReadOnlyList<WorkQuest> Quests, IReadOnlyList<WorkSession> Sessions,
    IReadOnlyList<WorkTree> Trees, IReadOnlyList<LandedBranch> Landings)
{
    /// <summary>The ask's intake, where it is in the work: only an ask's work has one.</summary>
    public IReadOnlyList<WorkSession> Intake => [.. Sessions.Where(session => session.Intake)];

    /// <summary>Whether a quest is in the work, compared without case: what the look's paused set asks (PAUSE1b).</summary>
    public bool Has(string quest) => Quests.Any(each => string.Equals(each.Quest.Id, quest.Trim().TrimStart('#'), StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Everything one reading of a work is made from (design §1): the service's quests, closed ones included; its session
/// records, closed ones included (<see cref="SessionRecords.Door"/>); and this machine's landings, standing and traces
/// (<see cref="LandedBranches.Entries"/>).
/// </summary>
public sealed record AskWorkLook(IReadOnlyList<QuestView> Quests, IReadOnlyList<SessionRecord> Records)
{
    /// <summary>Every landing recorded here, standing or a trace (D102, D113).</summary>
    public IReadOnlyList<LandedBranch> Landings { get; init; } = [];
}

/// <summary>
/// The one reader of an ask's work, and of one quest's (PAUSE1a, D132 point 1): the pause and the abandon read this answer,
/// and the screen's <c>WORK_PLAN</c> and <c>daoris-driver</c> print it, so they cannot disagree, as
/// <see cref="SessionGroups.Read"/> is one reader for both (D126 §2.4).
/// </summary>
/// <remarks>
/// <para><b>Pure.</b> It decides from an <see cref="AskWorkLook"/> and touches nothing; the door that asks gathers the look,
/// and says whether a path is one of this home's trees.</para>
///
/// <para><b>Read again at every look, never kept as a list</b> (design §1): a chain's step published by a close, or a
/// question asked a second before a pause, joins the work as it appears, so nothing slips past a pause made a moment
/// earlier.</para>
///
/// <para><b>Questions belong to the work.</b> A question exists because a session of the work asked it: once the work stops,
/// its answer is wanted by nobody, and left open it starts a session in another repository to answer it.</para>
///
/// <para><b>A teammate's record is named and holds nothing here.</b> Its tree and its process are on its machine (D47 §4),
/// and a landing here names this machine's sessions only.</para>
/// </remarks>
public static class AskWork
{
    /// <summary>The work of an ask (<paramref name="scope"/> <see cref="WorkScope.Ask"/>) or of one quest, named as a person writes it.</summary>
    /// <param name="held">Whether a path is a tree this home opened (<see cref="SessionTrees.Holds"/>): only such a tree, and its branch, is the work's here.</param>
    /// <exception cref="ArgumentException">A blank id: a work is read for an ask or a quest it names.</exception>
    public static WorkPieces Read(AskWorkLook look, WorkScope scope, string id, Func<string, bool> held)
    {
        var named = id?.Trim().TrimStart('#').Trim() ?? "";
        if (named.Length == 0) throw new ArgumentException("a work is read for the ask or the quest it names.", nameof(id));

        var quests = new List<WorkQuest>();
        var inWork = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Join(WorkQuest quest)
        {
            if (inWork.Add(quest.Quest.Id)) quests.Add(quest);
        }

        // 1. The quests asked: by the ask, chain steps included (each names the ask as its sender, D65 §4), or the one named.
        var asker = SenderOf(named);
        foreach (var quest in look.Quests)
        {
            if (scope == WorkScope.Ask ? string.Equals(quest.From, asker, StringComparison.OrdinalIgnoreCase)
                    : string.Equals(quest.Id, named, StringComparison.OrdinalIgnoreCase))
            {
                Join(new WorkQuest(quest, scope == WorkScope.Ask ? WorkJoin.Asked : WorkJoin.Named));
            }
        }

        // 2. The sessions of each quest in the work, and every quest a session of it published, applied again to what that
        // adds until nothing more joins. An ask's intake is a session of its work from the start.
        var sessions = new List<SessionRecord>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var unread = new Queue<SessionRecord>();
        void Admit(SessionRecord record)
        {
            if (!seen.Add(record.Id)) return;
            sessions.Add(record);
            unread.Enqueue(record);
        }

        var intake = new HashSet<string>(StringComparer.Ordinal);
        if (scope == WorkScope.Ask)
        {
            foreach (var record in look.Records.Where(record => string.Equals(record.Ask, named, StringComparison.OrdinalIgnoreCase)))
            {
                Admit(record);
                intake.Add(record.Id);
            }
        }

        var next = 0;
        while (next < quests.Count || unread.Count > 0)
        {
            for (; next < quests.Count; next++)
            {
                var quest = quests[next].Quest.Id;
                foreach (var record in look.Records.Where(record => string.Equals(record.Quest, quest, StringComparison.OrdinalIgnoreCase)))
                {
                    Admit(record);
                }
            }

            while (unread.TryDequeue(out var record))
            {
                foreach (var quest in look.Quests.Where(quest => quest.PublishedBy is { } by && Spells(record, by)))
                {
                    Join(new WorkQuest(quest, WorkJoin.Published, record.Id));
                }
            }
        }

        // 3. Each session's tree and branch here: a teammate's names none of this machine's.
        var placed = sessions
            .OrderBy(record => record.Created).ThenBy(record => record.Id, StringComparer.Ordinal)
            .Select(record => Place(record, held) with { Intake = intake.Contains(record.Id) })
            .ToList();
        var trees = placed
            .Where(session => session.Tree is not null)
            .GroupBy(session => SessionGroups.Normal(session.Tree!), StringComparer.OrdinalIgnoreCase)
            .Select(tree => new WorkTree(
                tree.First().Tree!, tree.First().Record.Repository, tree.First().Branch!, [.. tree.Select(session => session.Record.Id)]))
            .ToList();

        // 4. Every landing that names a session of the work, standing or a trace.
        var ids = new HashSet<string>(placed.Select(session => session.Record.Id), StringComparer.OrdinalIgnoreCase);
        // A landing a session of the work made, or moved on (LAND2c).
        var landings = look.Landings.Where(landing => ids.Contains(landing.Session) || landing.Advances.Any(advance => ids.Contains(advance.Session))).ToList();

        return new WorkPieces(scope, named, quests, placed, trees, landings);
    }

    /// <summary>
    /// The sender every quest an ask asked names (D65 §4), chain steps included: the service's <c>AskDesk.SenderOf</c>, a copy
    /// kept on purpose since the two share no code, and held to the service's spelling by <c>AskWorkTests</c>.
    /// </summary>
    public static string SenderOf(string ask) => $"ask #{ask}";

    /// <summary>
    /// Whether a quest's <c>publishedBy</c> names this record's session: as the record is keyed, or, for a teammate's record
    /// (keyed <c>origin/id</c>, SYNC4), as their machine spells it.
    /// </summary>
    private static bool Spells(SessionRecord record, string by) =>
        string.Equals(record.Id, by, StringComparison.Ordinal)
        || (record.Teammate && string.Equals(record.Id[(record.Id.IndexOf('/') + 1)..], by, StringComparison.Ordinal));

    /// <summary>A session as the work names it: its tree and branch where the tree is this home's, and none for a teammate's.</summary>
    private static WorkSession Place(SessionRecord record, Func<string, bool> held)
    {
        if (record.Teammate || record.Tree is not { Length: > 0 } tree || !Held(held, tree)) return new WorkSession(record);
        var normal = SessionGroups.Normal(tree);
        return new WorkSession(record, tree, $"daoris/{normal[(normal.LastIndexOf('/') + 1)..]}");
    }

    private static bool Held(Func<string, bool> held, string tree)
    {
        try
        {
            return held(tree);
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // A path no folder could have is no tree of this home's, as the session list reads it.
            return false;
        }
    }
}
