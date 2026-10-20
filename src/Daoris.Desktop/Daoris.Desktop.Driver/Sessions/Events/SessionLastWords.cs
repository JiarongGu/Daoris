namespace Daoris.Driver;

/// <summary>
/// What a session last said, read from its record and its transcript: what a parked session said to the person. A session's own,
/// read by the loop and a conversation alike, so not the loop's (LOOPOUT1). What a carry-on or a hand-over is handed of the session
/// it goes on from is Daoris's brief of its line since SESSCTX1b (<see cref="ContextBriefs"/>).
/// </summary>
internal static class SessionLastWords
{
    /// <summary>How much of a parked session's words its card keeps, from the end, where a question list sits (FG5).</summary>
    internal const int LastWordsLimit = 4000;

    /// <summary>
    /// What a parked session said to the person (PARK1): its record's last message whole, and the
    /// transcript's last plain lines only where the record holds none. The record keeps the message as
    /// written; the transcript's reading stops at an indented line, which a question's own list has.
    /// </summary>
    internal static string? ParkedWords(SessionEvents events, string sessionId, string transcript)
    {
        if (events.LastSaid(sessionId) is not { } said) return LastWords(transcript);
        return TextBounds.ClipStart(said, LastWordsLimit);
    }

    /// <summary>
    /// The agent's own last words in a finished transcript (STANDDOWN2): its final block of plain lines,
    /// after its last tool and before the driver's closing line — what a session that parks to ask the
    /// person said to them. Kept to its END, where a question list sits, and null when there is none.
    /// </summary>
    /// <remarks>
    /// Four thousand characters, not seven hundred (FG5): a parked session's message is the person's
    /// whole brief, and the apply session's was 2,500 long — cut to its end, the card kept the command
    /// and lost the question.
    /// </remarks>
    internal static string? LastWords(string transcript, int limit = LastWordsLimit)
    {
        IReadOnlyList<string> lines;
        try
        {
            lines = [.. File.ReadLines(transcript).TakeLast(200)];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        static bool Plain(string line) =>
            line.Length == 0 || !(line.StartsWith('→') || line.StartsWith(' ') || line.StartsWith('—')
                                  || line.StartsWith('[') || line.StartsWith('·'));

        var block = new List<string>();
        for (var at = lines.Count - 1; at >= 0; at--)
        {
            var line = lines[at];
            if (Plain(line))
            {
                block.Add(line);
            }
            else if (block.Any(said => said.Trim().Length > 0))
            {
                break;
            }
        }

        block.Reverse();
        var words = string.Join("\n", block).Trim();
        if (words.Length == 0) return null;
        return TextBounds.ClipStart(words, limit);
    }
}
