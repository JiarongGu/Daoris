using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>The clean-up's and bringing up to date's answers as the bridge carries them and the sweep slice pushes them: one builder for both doors.</summary>
public static class SweepWires
{
    /// <summary>
    /// A session branch's row: what the proof found, and whether its discard is offered beside it, by the
    /// driver's own rule, so the page offers it beside the rows `daoris tree clean` prints its line beside: a
    /// failed or superseded attempt's commits, which no clean-up takes. The tree's path stays here; the page is told only
    /// whether there is one.
    /// </summary>
    /// <param name="session">
    /// The session its tree is, or null: named by id and how it ended, with
    /// whether its landing is offered by the driver's rule (<see cref="SessionBranchDoors.LandingOffered"/>), the line `trees clean`
    /// prints beside the same row. A clean-up's press answers none.
    /// </param>
    public static object SweepRow(SweepItem item, SessionRecord? session = null) => new
    {
        item.Repository, item.Workspace, item.Branch, HasTree = item.Tree is not null,
        item.Kind, item.Commits, item.Where, item.Detail, item.Removable,
        Discardable = SessionBranchDoors.RemovalOffered(item) is not null,
        Session = item.Tree is not null && session is not null ? new { session.Id, session.State } : null,
        Landable = SessionBranchDoors.LandingOffered(item, session) is not null,
    };

    /// <summary>
    /// A branch's row in bringing up to date, the list's and the press's alike: what the press does with it, where its own
    /// commits start, how many it carries, and how many the line already holds, which the replay leaves out (LANDSYNC3b, D87's
    /// 2026-10-16 note), as `trees sync` says them.
    /// </summary>
    public static object RebaseRow(RebaseItem item) => new
    {
        item.Repository, item.Workspace, item.Branch, item.Landed, item.Kind, item.Onto, item.Cut, item.CutBy, item.GrewFrom,
        item.Commits, item.LeftOut, item.Detail, item.Replays,
    };

    /// <summary>
    /// The clean-up's list as <c>SWEEP_PLAN</c> answers it and the pushed <c>sweep</c> slice carries each repository's part of it
    ///: one builder, two doors. A row names its session only where it could offer that
    /// session's landing (<see cref="OffersLanding"/>), the only rows the door reads the records for, so a row says the same whichever
    /// other rows stand beside it.
    /// </summary>
    /// <param name="records">The sessions' records: the service's at the door, the machine's facts' look in the slice.</param>
    public static object SweepAnswer(SweepPlan plan, IReadOnlyList<SessionRecord> records) => new
    {
        Branches = plan.Sessions
            .Select(item => SweepRow(item, OffersLanding(item) ? SessionBranchDoors.SessionOfTree(records, item.Tree) : null)).ToArray(),
        Landed = plan.Landed.Select(LandedRow).ToArray(),
    };

    /// <summary><see cref="SweepAnswer(SweepPlan, IReadOnlyList{SessionRecord})"/> with the words <c>trees clean</c> prints for it.</summary>
    public static object SweepAnswer(SweepPlan plan, IReadOnlyList<SessionRecord> records, TreesSaid said) => new
    {
        Branches = plan.Sessions
            .Select(item => SweepRow(item, OffersLanding(item) ? SessionBranchDoors.SessionOfTree(records, item.Tree) : null)).ToArray(),
        Landed = plan.Landed.Select(LandedRow).ToArray(),
        Said = said.ToWire(),
    };

    /// <summary>Whether a kept branch's row could offer its session's landing: unlanded, its tree still here.</summary>
    public static bool OffersLanding(SweepItem item) => item.Kind == SweepKind.Unlanded && item.Tree is not null;

    /// <summary>A landed branch's row: what the proof found, and the files that keep it where some do.</summary>
    public static object LandedRow(LandedItem item) => new
    {
        item.Repository, item.Workspace, item.Branch, item.Kind, item.Where, item.Files, item.Detail,
        item.PullRequest, item.Commits, item.Removable,
        // LANDRETIRE1 part 2: when the landing recorded it, and its quest with whether the service says it is closed.
        item.LandedAt, item.Quest, item.QuestClosed,
    };
}
