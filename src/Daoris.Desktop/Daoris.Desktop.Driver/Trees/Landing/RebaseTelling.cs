namespace Daoris.Driver;

/// <summary>
/// The rebase words said to the session itself where its landing's replay onto the line conflicts (LANDREBASE1, D87's note point 3):
/// through the say door as Daoris's words, keyed by the session and the tip that conflicted (<see cref="DaorisKeys.Rebase"/>), so its
/// own conversation goes on with them in its own tree, where the context to resolve the conflict is. The automatic landing says them
/// at the tip it judged; a terminal's <c>trees land</c> says them at its tree's tip (LANDREBASE2), since a terminal has no second press
/// for the window's <i>Ask it to rebase</i>.
/// </summary>
public static class RebaseTelling
{
    /// <summary>The say door for Daoris's words under a key; null where there is none, and nothing is said.</summary>
    public delegate Task<SayAnswer?> Say(string session, string text, string key, CancellationToken ct);

    /// <summary>The words said at <paramref name="tip"/>; the door's answer, or null where there was no tip or no door answered.</summary>
    public static async Task<SayAnswer?> TellAsync(Say say, string session, string? tip, LandingConflict conflict, CancellationToken ct)
    {
        if (tip is null) return null;
        try
        {
            return await say(session, conflict.Words, DaorisKeys.Rebase(session, tip), ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException)
        {
            // Untold, the refusal is said as any other is, and the person's review offers the words.
            return new SayAnswer(false, error.Message);
        }
    }

    /// <summary>
    /// A terminal's landing (LANDREBASE2): the words said at the tree's HEAD, and the sentence the terminal says after the refusal: that
    /// the session was asked and the landing can be run again once it has, or why it was not, with the words for the person to pass on.
    /// </summary>
    public static async Task<string> TellTreeAsync(Say say, string session, string tree, LandingConflict conflict, CancellationToken ct = default)
    {
        var (code, head, _) = await WorkingTree.GitAsync(tree, ["rev-parse", "--verify", "--quiet", "HEAD^{commit}"], ct).ConfigureAwait(false);
        if (code != 0) return NotTold(session, "its tree's commit could not be read", conflict);
        var said = await TellAsync(say, session, head.Trim(), conflict, ct).ConfigureAwait(false);
        return said is { Kept: true } ? Told(session)
            : NotTold(session, said is null ? "nothing here reaches its conversation" : said.Message, conflict);
    }

    /// <summary>The sentence for a session the words reached.</summary>
    public static string Told(string session) =>
        $"Its session `{session}` was asked to rebase in its own tree, in the words the review offers; its conversation goes on with them. "
        + $"Run `trees land {session}` again once its tree has changed.";

    /// <summary>The sentence for a session the words did not reach: why, and the words, for the person to say to it.</summary>
    public static string NotTold(string session, string why, LandingConflict conflict) =>
        $"Its session `{session}` could not be asked to rebase ({why.TrimEnd('.')}). Say this to it: {conflict.Words}";
}
