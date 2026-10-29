namespace Daoris.Driver;

/// <summary>What an Accept's carry-on came to: whether the session was told, what it was carried on for, and the sentence a door says.</summary>
/// <param name="Told">The door kept the words, and the person's yes is kept on the session (<see cref="CarriedAccepts"/>).</param>
/// <param name="Why"><see cref="LandCarry.Uncommitted"/> or <see cref="LandCarry.Conflicted"/>.</param>
public sealed record CarryAnswer(bool Told, string Why, string Sentence);

/// <summary>
/// The Accept that carries a session on (LANDBACK1 part 1, D87's note): where the landing the person asked for is refused because the
/// session's tree holds uncommitted work or its replay onto the line conflicts, the session is told what to do about it, in Daoris's own
/// words through the say door, once per tip and status, and the person's yes is kept on it, so the landing runs once its tree is clean
/// (<see cref="AutoLander"/>'s look). Daoris never commits for the session (D164's rejection): a claim is not an act, only a clean tree lands.
/// </summary>
/// <remarks>
/// A mirror of <see cref="RebaseTelling"/>, which the automatic landing says a conflict with where nobody pressed: here somebody did, so
/// the yes outlasts the refusal, and uncommitted work is told as a conflict is.
/// </remarks>
public static class LandCarry
{
    /// <summary>How many times one Accept carries a session on: the press's words, and one more where its turn ends still dirty or conflicted.</summary>
    public const int Bound = 2;

    public const string Uncommitted = "uncommitted";

    public const string Conflicted = "conflict";

    /// <summary>The landing the kept yes accepted (<see cref="AutoLandingNotes.Of"/>'s <c>carry</c>): the work landed once its tree was clean.</summary>
    public const string Accepted = "accepted";

    /// <summary>What a refused landing asks of its session, or null where the refusal is no work of the session's to do.</summary>
    public static string? WhyOf(TreeLanding landed) =>
        landed.Landed ? null
        : landed.Conflict is not null ? Conflicted
        : landed.Refusal == AutoLandingCode.Uncommitted ? Uncommitted
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

    /// <summary>The words for a carry-on: a conflict's are the rebase words (LANDREBASE1), a dirty tree's <see cref="DirtyWords"/>.</summary>
    public static string WordsOf(string why, LandingConflict? conflict, int paths) =>
        why == Conflicted && conflict is not null ? conflict.Words : DirtyWords(paths);

    /// <summary>What a carry-on is keyed on: a dirty tree's status fingerprint, or the tip a conflict was found at.</summary>
    public static string BasisOf(string why, string? tip, string status) => why == Conflicted ? $"conflict-{tip}" : status;

    /// <summary>
    /// The Accept's carry-on: the session told through <paramref name="say"/> at its tree's state, and the person's yes kept where the door
    /// kept the words. Null where the refusal is not one a session can clear, or the session is one that must not be told (the door says
    /// what it said before). A door that kept nothing says why and gives the person the words, and keeps no yes.
    /// </summary>
    /// <param name="record">The session's record as the door read it, or null where it read none.</param>
    public static async Task<CarryAnswer?> CarryAsync(
        RebaseTelling.Say say, string home, string session, string tree, TreeLanding landed, SessionRecord? record, DateTimeOffset now,
        CancellationToken ct = default)
    {
        if (WhyOf(landed) is not { } why || Skips(record)) return null;
        var (tip, status, paths) = await AutoLander.ReadTreeAsync(tree, ct).ConfigureAwait(false);
        if (tip is null) return new CarryAnswer(false, why, NotTold(session, "its tree's commit could not be read", WordsOf(why, landed.Conflict, paths)));
        var words = WordsOf(why, landed.Conflict, paths);
        // The yes is kept before the words go, and counted as the press's own carry-on (a new press starts the count again): a session that
        // goes on at once may end its turn before this door returns, and its conclusion must find the yes it is due by. Words the door
        // keeps nothing of take it back.
        var accepts = new CarriedAccepts(home);
        accepts.Keep(session, tree, now);
        accepts.CarriedOn(session);
        var said = await TellAsync(say, session, words, CarryKeys.Of(session, BasisOf(why, tip, status), carryOn: 1), ct).ConfigureAwait(false);
        if (said is not { Kept: true })
        {
            accepts.Take(session);
            return new CarryAnswer(false, why, NotTold(session, said is null ? "nothing here reaches its conversation" : said.Message, words));
        }

        return new CarryAnswer(true, why, Told(session, why));
    }

    /// <summary>The words said under their key; the door's answer, or an untold one where it failed (the refusal then stands as it was).</summary>
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

    /// <summary>The sentence for a session the words reached: it was carried on, accepted, and lands when its tree is clean.</summary>
    public static string Told(string session, string why) =>
        $"Accepted: its session `{session}` was carried on to {(why == Conflicted ? "replay its own commits onto the line and resolve the conflict" : "commit or restore its uncommitted work")} "
        + "in its own tree, and its work lands when its tree is clean. Nothing was landed yet.";

    /// <summary>The sentence for a session the words did not reach: why, and the words, for the person to say to it.</summary>
    public static string NotTold(string session, string why, string words) =>
        $"Its session `{session}` could not be carried on ({why.TrimEnd('.')}), so nothing was kept of your Accept. Say this to it: {words}";
}
