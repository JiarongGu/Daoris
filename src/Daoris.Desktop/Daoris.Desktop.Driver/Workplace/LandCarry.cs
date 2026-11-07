namespace Daoris.Driver;

/// <summary>
/// What the performer's carry-on tells a session: where a landing is refused because the session's tree
/// holds uncommitted work or its replay onto the line conflicts, the session is told what to do about it, in Daoris's own words through
/// the say door, once per tip and status. Daoris never commits for the session: a claim is not an act, only a clean
/// tree lands.
/// </summary>
public static class LandCarry
{
    /// <summary>How many times one Accept carries a session on: the press's words, and one more where its turn ends still dirty or conflicted.</summary>
    public const int Bound = TurnCarry.Bound;

    public const string Uncommitted = "uncommitted";

    public const string Conflicted = "conflict";

    /// <summary>The landing the kept yes accepted (<see cref="LandTryNotes.Of"/>'s <c>carry</c>): the work lands once its tree is clean.</summary>
    public const string Accepted = "accepted";

    /// <summary>What a refused landing asks of its session, or null where the refusal is no work of the session's to do.</summary>
    public static string? WhyOf(TreeLanding landed) =>
        landed.Landed ? null
        : landed.Conflict is not null ? Conflicted
        : landed.Refusal == LandingCode.Uncommitted ? Uncommitted
        : null;

    /// <summary>
    /// Whether the session may be carried on at all: never one the person stopped or declined, nor one that stood down, which nothing
    /// they asked for reaches (the guards of <c>DrivenInbox</c>, <c>ReopenTelling</c> and the planner's stops).
    /// </summary>
    public static bool Skips(SessionRecord? record) => record is { State: "stopped" or "declined" or "stood-down" };

    /// <summary>The words that ask a session to clean its tree: commit by its repository's conventions, or restore what is not wanted and say which.</summary>
    public static string DirtyWords(int paths) =>
        $"Your work is accepted, and it lands once your tree is clean. Your tree has {paths} uncommitted path(s). {CommitWords}: Daoris "
        + "lands your branch once the tree is clean.";

    /// <summary>
    /// What a carry-on asks of a dirty tree, shared by an Accept's words and a done quest's (<see cref="TurnCarries.WordsOf"/>, part 2):
    /// commit by the repository's conventions or restore what is not wanted, and make no branch of its own.
    /// </summary>
    public const string CommitWords =
        "Commit them on the tree's branch by this repository's conventions, or restore the ones you do not want and say which. "
        + "Do not create, switch or push branches";

    /// <summary>The words for a carry-on: a conflict's are the rebase words, a dirty tree's <see cref="DirtyWords"/>.</summary>
    public static string WordsOf(string why, LandingConflict? conflict, int paths) =>
        why == Conflicted && conflict is not null ? conflict.Words : DirtyWords(paths);

    /// <summary>What a carry-on is keyed on: a dirty tree's status fingerprint, or the tip a conflict is found at.</summary>
    public static string BasisOf(string why, string? tip, string status) => why == Conflicted ? $"conflict-{tip}" : status;

    /// <summary>The words said under their key; the door's answer, or an untold one where it failed (the refusal then stands unchanged).</summary>
    public static async Task<SayAnswer?> TellAsync(RebaseTelling.Say say, string session, string words, string key, CancellationToken ct)
    {
        try
        {
            return await say(session, words, key, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException)
        {
            return new SayAnswer(false, error.Message);
        }
    }
}
