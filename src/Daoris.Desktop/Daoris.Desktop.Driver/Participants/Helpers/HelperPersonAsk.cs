namespace Daoris.Driver;

/// <summary>What the person's ask of a helper did: its id where it is written, else the code of why not, and a sentence.</summary>
public sealed record HelperPersonAsked(string? Asked, string? Why, string Message);

/// <summary>
/// The person's own ask of a helper, written for both doors (the page's <c>SESSION_HELPER_ASK</c> and the terminal's <c>sessions helper</c>).
/// It judges what the file alone carries: an <i>Ask again…</i> follows an answered ask of the same
/// session; and where the answer goes to the session, it keeps <c>helper.person-asked</c> on it at the ask, the person's word on the line,
/// so the answers-alone count reads the answer that follows as the first alone. An answer kept for the person keeps no note: it counts
/// nowhere, since it never reaches the session by itself.
/// </summary>
/// <remarks>Who may ask, the question's bound and the agent's one word are the doors' own judges, said before this is asked.</remarks>
public static class HelperPersonAsk
{
    /// <param name="after">The ask this one asks again after, or null for a first ask.</param>
    /// <param name="toPerson">The answer is the person's, reaching the session only by <i>Pass to the lead</i>.</param>
    public static HelperPersonAsked Write(
        HelperFiles files, ISessionEvents events, string asker, string question, string? agent, string? after, bool toPerson, DateTimeOffset at)
    {
        var follows = after?.Trim().TrimStart('#');
        if (follows is { Length: > 0 })
        {
            var earlier = files.Asks().FirstOrDefault(ask => ask.Id == follows && string.Equals(ask.Asker, asker, StringComparison.Ordinal));
            if (earlier is null)
            {
                return new(null, HelperAskWhy.After, $"Session {asker} holds no ask #{follows} to ask again after. Nothing was asked.");
            }

            if (files.ReadState(earlier.Id) is not { State: HelperStates.Answered })
            {
                return new(null, HelperAskWhy.Unanswered, $"#{follows} has not answered yet: ask again once it has. Nothing was asked.");
            }
        }
        else
        {
            follows = null;
        }

        var asked = files.WriteAsk(asker, question, agent, at, follows, toPerson);
        if (!toPerson)
        {
            var noted = HelperNotes.PersonAsked();
            events.Keep(asker, new SessionEvent { Kind = SessionEventKind.Note, Text = noted.Note, Parts = noted.Parts }, say: null);
        }

        return new(asked, null, toPerson
            ? $"Asked #{asked} about session {asker}'s work: the driver starts a helper on another agent at its next look, read-only, and "
              + "its answer comes to you; it reaches that session only once you pass it to the lead."
            : $"Asked #{asked} for session {asker}: the driver starts a helper on another agent at its next look, read-only, and "
              + "its answer comes back to that session as its next turn, marked as the helper's.");
    }
}
