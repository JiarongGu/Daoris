namespace Daoris.Driver;

/// <summary>
/// The message of the one commit a squashing landing makes (PROCESS1b-ii, D164's PROCESS1b-ii note point 2): the quest's title as
/// its subject, the session's commits' subjects oldest first as its body, one line each, and the line naming the session last.
/// Bounded, so a long chain or a conversation's opening never makes a commit nobody can read.
/// </summary>
public static class SquashMessage
{
    /// <summary>How long the subject may be: the width a pull request's title and <c>git log --oneline</c> show whole.</summary>
    public const int SubjectLimit = 72;

    /// <summary>How many lines the body's list may take, the line counting what it left out among them.</summary>
    public const int BodyLines = 50;

    /// <summary>How many characters the body's list may take, its line breaks among them.</summary>
    public const int BodyLimit = 4000;

    /// <summary>The whole message: the subject, a blank line, the list where there is one and another blank line, and the ending.</summary>
    /// <param name="title">The quest's title, or a conversation's own: its opening.</param>
    /// <param name="commits">The commits squashed, oldest first: at a chain's later landing, only those added since the last.</param>
    public static string Of(string? title, IReadOnlyList<LandingCommit> commits, string session)
    {
        var body = Body(commits);
        return Subject(title, commits, session) + "\n\n" + (body.Length > 0 ? body + "\n\n" : "") + Ending(session);
    }

    /// <summary>The last line, naming the session the commit was squashed from.</summary>
    public static string Ending(string session) => $"Squashed by Daoris from session `{session}`";

    /// <summary>
    /// The title on one line, at most <see cref="SubjectLimit"/> characters, cut with an ellipsis where it is longer. A blank title
    /// takes the oldest commit's subject; with none, the session names it.
    /// </summary>
    public static string Subject(string? title, IReadOnlyList<LandingCommit> commits, string session)
    {
        var said = TextBounds.Fold(title);
        if (said.Length == 0 && commits.Count > 0) said = TextBounds.Fold(commits[0].Subject);
        if (said.Length == 0) said = $"Work from session {session}";
        return TextBounds.Clip(said, SubjectLimit, keep: SubjectLimit - 1, trimEnd: true, wholePairs: true);
    }

    /// <summary>
    /// Each commit's subject on a line of its own, oldest first, within <see cref="BodyLines"/> lines and <see cref="BodyLimit"/>
    /// characters. What does not fit is counted on the last line, so the commit never claims to carry every subject when it does not.
    /// </summary>
    public static string Body(IReadOnlyList<LandingCommit> commits)
    {
        var lines = commits.Select(commit => "- " + TextBounds.Fold(commit.Subject)).ToList();
        if (lines.Count <= BodyLines && lines.Sum(line => line.Length) + Math.Max(0, lines.Count - 1) <= BodyLimit)
        {
            return string.Join('\n', lines);
        }

        // As many as fit with the line that counts the rest after them.
        var kept = 0;
        var length = 0;
        for (var take = 1; take <= Math.Min(lines.Count - 1, BodyLines - 1); take++)
        {
            var next = length + (take > 1 ? 1 : 0) + lines[take - 1].Length;
            if (next + 1 + More(lines.Count - take).Length > BodyLimit) break;
            kept = take;
            length = next;
        }

        return string.Join('\n', lines.Take(kept).Append(More(lines.Count - kept)));
    }

    private static string More(int left) => left == 1 ? "… and 1 more commit" : $"… and {left} more commits";
}
