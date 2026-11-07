using System.Text;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// Whether a landing may move a standing branch on: pure, so its table holds every refusal without
/// git. Only a branch Daoris made and recorded, standing at the recorded tip, checked out nowhere, whose work does not
/// already read on the line, to a commit that grows from that tip with something new.
/// </summary>
public static class LandingAdvance
{
    /// <summary>
    /// What the session's work must grow from to move the branch on: the tip the record says is squashed, under a squash rule where
    /// it says one, else the tip the branch is made or last moved at. 🔴 Work replayed onto the branch's tip (a
    /// part joining its entry's branch, or past what the line holds) grows from that tip under a squash too:
    /// the squashed tip is another tree's, which the replay does not hold.
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
            // The transport's own integration (the rule's integrate): no pull request or plugin was in it.
            if (facts.Recorded is { Integrate: not null, Pushed: false, PullRequest: null })
            {
                return new TreeLanding(false,
                    $"`{name}`'s work was integrated into `{where}` by Daoris, so Daoris does not move it on: commits added to it now "
                    + "would land on a branch the line already holds. Bring the repository up to date (`daoris tree sync`, or "
                    + "Bring up to date on the page), which replays this session's own commits onto the line and removes that branch; "
                    + $"then Accept lands this work on a fresh `{name}`, which Daoris integrates into the line in turn.")
                { Refusal = LandingCode.Completed };
            }

            return new TreeLanding(false,
                $"`{name}`'s work already reads on `{where}` — its pull request was merged — so Daoris does not move it on: commits "
                + "added to it now would ride no pull request. Bring the repository up to date (`daoris tree sync`, or "
                + "Bring up to date on the page), which replays this session's own commits onto the line and removes that branch; "
                + $"then Accept lands this work on a fresh `{name}`, and its plugin opens a new pull request.")
            { Refusal = LandingCode.Completed };
        }

        if (!facts.Descends)
        {
            return Exists($"`{sessionBranch}` does not grow from `{name}`, so moving it on would not be a fast-forward, and Daoris "
                + "merges nothing into a branch. Land this work by hand, or change the pattern with `daoris place landing`.");
        }

        return facts.Ahead > 0
            ? null
            : new TreeLanding(false, $"`{name}` already holds everything on `{sessionBranch}` — nothing to land.")
            {
                Refusal = LandingCode.Nothing,
            };
    }

    /// <summary>
    /// The refusal a standing branch meets from what is cheap to read, or null where those facts allow the move:
    /// not the record's, moved since the recorded tip, checked out in a working tree, or its pull request
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
                + "Rename or delete it there, or change the pattern with `daoris place landing`.");
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

        // The platform's kept word refuses as git's own proof does, since git cannot see a squash once
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
                    ? "Bring the repository up to date (`daoris tree sync`, or Bring up to date on the page), which replays "
                      + "this session's own commits onto the line and removes that branch; "
                    : $"Delete or rename `{name}` once you no longer want its work; ")
                + $"then Accept lands this work on a fresh `{name}`, and its plugin opens a new pull request.")
            { Refusal = LandingCode.Completed };
        }

        return null;
    }

    private static TreeLanding Exists(string why) => new(false, why) { Refusal = LandingCode.Exists };

    private static string Short(string commit) => TextBounds.ShortCommit(commit, 8);
}
