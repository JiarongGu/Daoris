using System.Text;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>What a press would do: the form, where the work would go (the line, or the branch it would make), and what said so.</summary>
/// <param name="Plugin">The plugin that pushes the branch once it is made, or null for the person.</param>
/// <param name="Problem">
/// The sentence a press would be refused with now, or null: the plugin cannot land work here, the name entered is refused, or the
/// standing branch may not be moved on (<see cref="LandingAdvance.Standing"/>).
/// </param>
/// <param name="AutoAccept">
/// Whether the rule accepts automatically: the quest's done lands the work, with no press, and the session's
/// instruction says so.
/// </param>
public sealed record LandingPlan(string Form, string Target, string Source, string? Plugin = null, string? Problem = null, bool AutoAccept = false) : IStartLanding
{
    object? IStartLanding.LeftOut => LeftOut;
    object? IStartLanding.Lands => Lands;
    object? IStartLanding.Conflict => Conflict;

    /// <summary>
    /// The target is the branch the session's own agent made in its tree, which lands as it stands:
    /// no branch is made, the rule's pattern does not name it, and a name entered has nowhere to go. False for a tree still on its
    /// session branch, where the branch form makes the one the pattern names.
    /// </summary>
    public bool Own { get; init; }

    /// <summary>
    /// The branch the press lands on already stands in the repository: an entry's or
    /// a chain's branch an earlier landing made, which the press moves on instead of making it. The plain fact: one the
    /// press may not move on (Daoris did not make it, it moved since, a tree has it checked out) stands too, its refusal said in
    /// <see cref="Problem"/>. False for a branch the press would make, for the tree's own branch (<see cref="Own"/>),
    /// and under a merge rule.
    /// </summary>
    public bool Stands { get; init; }

    /// <summary>
    /// The branch form makes one commit of the tree's work on its branch, as the run's <c>land</c> option <c>squash</c> says: the session is told so at its start. False under a merge, and for a tree's own branch, which lands as it stands.
    /// </summary>
    public bool Squash { get; init; }

    /// <summary>
    /// The tree's commits the line already holds, which the press would leave out, oldest first: judged against the line as last fetched, since the plan reaches no network. Empty where the line holds none
    /// of them but moved since the tree left it, and the press replays them all onto it first. Null where the
    /// press lands the tree's commits as made: the line has not moved, or the branch is one the press moves on or may not rewrite.
    /// </summary>
    public IReadOnlyList<LeftOutCommit>? LeftOut { get; init; }

    /// <summary>What the press would replay onto the line, oldest first, beside <see cref="LeftOut"/>; null where that is null.</summary>
    public IReadOnlyList<LandingCommit>? Lands { get; init; }

    /// <summary>
    /// Where replaying <see cref="Lands"/> onto the line conflicts now (a trial that writes no ref and no tree), and the words that
    /// would ask the session to rebase: the press would be refused. Null where it replays cleanly, where only files the repository
    /// marks generated conflict, or where nothing is replayed.
    /// </summary>
    public LandingConflict? Conflict { get; init; }
}
