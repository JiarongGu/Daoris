using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests.Conversations;

/// <summary>
/// A brief's section caps weigh the section as it is told: a request or a note of many short lines gains <c>&gt; </c> on each line when it
/// is quoted, so the clip leaves room for it and the section stays under its own cap.
/// </summary>
public sealed class ContextBriefQuoteTests
{
    private static string ShortLines(int count) => string.Join("\n", Enumerable.Repeat("a", count));

    private static BriefLine LineWith(string request, string? note)
    {
        List<SessionEvent> events =
        [
            new() { Kind = SessionEventKind.User, Origin = "person", Text = request },
            new() { Kind = SessionEventKind.Message, Text = "Started." },
            new() { Kind = SessionEventKind.Turn, StopReason = "end_turn" },
        ];
        return new BriefLine(HandOverWhy.Press, [new BriefPart("c0nv0001", "codex-acp", events)]) { Conversation = true, Note = note };
    }

    [Fact]
    public void A_request_of_many_short_lines_stays_under_the_openings_cap_once_quoted()
    {
        var brief = ContextBrief.Compose(LineWith(ShortLines(3_000), note: null));

        var opening = Assert.Single(brief.Sections, section => section.Name == HandedSections.BriefOpening);
        Assert.True(opening.Chars <= ContextBrief.OpeningCap, $"the opening runs to {opening.Chars}");
        Assert.Contains("characters left out here", brief.Text);
    }

    [Fact]
    public void A_note_of_many_short_lines_stays_under_the_notes_cap_once_quoted()
    {
        var brief = ContextBrief.Compose(LineWith("Add a dark mode.", ShortLines(3_000)));

        var note = Assert.Single(brief.Sections, section => section.Name == HandedSections.BriefNote);
        Assert.True(note.Chars <= ContextBrief.NoteCap, $"the note runs to {note.Chars}");
        Assert.Contains("characters left out here", brief.Text);
    }
}
