namespace Daoris.Driver;

/// <summary>
/// What a conversation's record keeps of a landing, a hand-off and a part left out or brought in: machine-local notes whose lead is
/// Daoris's and whose sentence beneath is the program's, as written, since it carries git's and the plugin's words.
/// </summary>
public static class LandingNotes
{
    /// <summary>How the conversation's note of a press opens: what the trace knows an acceptance by.</summary>
    public const string PersonAccepted = "the person accepted this work:";

    /// <summary>How the conversation's note of a landing with no press opens: who accepted it, which the review's note and the trace name.</summary>
    public const string AutoAccepted = "accepted automatically when its quest was done:";

    /// <summary>The whole sentence the press said, the plugin's part in it.</summary>
    public static SessionEvent Note(TreeLanding landed) => Press(ChatNoteCodes.LandingPersonAccepted, PersonAccepted, landed.Message);

    /// <summary>A hand-off after the landing, kept where the landing's own note is.</summary>
    public static SessionEvent HandNote(TreeHand handed) =>
        Press(ChatNoteCodes.LandingPersonHanded, "the person handed this work on:", handed.Message);

    private static SessionEvent Press(NoteCode code, string lead, string message)
    {
        var said = Noted.Of(code, lead).Then(" ", Noted.Said(message, NoteBy.Program));
        return new SessionEvent { Kind = SessionEventKind.Note, Text = said.Note, Parts = said.Parts };
    }

    /// <summary>
    /// A part the person left out of its entry's Accept: the branch its line of work landed on, so its review says so and leads with
    /// Discard. Landing it later is a press of its own.
    /// </summary>
    public static SessionEvent LeftOutNote(string branch)
    {
        var said = Noted.Of(ChatNoteCodes.LandingLeftOut,
            $"the person left this part out when its line of work landed on `{branch}`: its work did not land there.", ("branch", branch));
        return new SessionEvent { Kind = SessionEventKind.Note, Text = said.Note, Parts = said.Parts };
    }

    /// <summary>A member whose work a combined Accept brought into the head's landing: where it went and which operation did it.</summary>
    public static SessionEvent BroughtIntoNote(TogetherBrought brought)
    {
        var (code, where) = brought.Line ? (ChatNoteCodes.LandingBroughtIntoLine, $"the line `{brought.Into}`") : (ChatNoteCodes.LandingBroughtInto, $"`{brought.Into}`");
        var said = Noted.Of(code, $"its work was brought into {where} by another session's Accept (operation {brought.Operation}), so it lands no second time.",
            ("branch", brought.Into), ("operation", brought.Operation));
        return new SessionEvent { Kind = SessionEventKind.Note, Text = said.Note, Parts = said.Parts };
    }

    /// <summary>
    /// Where a part's review reads it left out: the branch the newest <see cref="LeftOutNote"/> names, while no acceptance came after
    /// it; null where none says it.
    /// </summary>
    public static string? LeftOutOf(IReadOnlyList<SessionEvent> events)
    {
        for (var at = events.Count - 1; at >= 0; at--)
        {
            if (events[at] is not { Kind: SessionEventKind.Note } note) continue;
            if (note.Text is { } text && IsAcceptance(text)) return null;
            if (note.Parts?.FirstOrDefault(part => part.Code == ChatNoteCodes.LandingLeftOut.Code)?.Value("branch") is string branch) return branch;
        }

        return null;
    }

    /// <summary>Whether a conversation's note is an acceptance of the session's work: a press's, or a landing with no press.</summary>
    public static bool IsAcceptance(string text) =>
        text.StartsWith(PersonAccepted, StringComparison.Ordinal) || text.StartsWith(AutoAccepted, StringComparison.Ordinal);
}
