using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// CONVNOTE2a (D152's CONVNOTE2a note, D142 points 2–4): the line a resumed conversation opens with carries its lines by code
/// beside its English, so the page words it in the reader's language. The English stays byte for byte what the terminal, the
/// trace and an older page read; who spoke is a reason of its own family and the version that moved a fact, never a sentence.
/// </summary>
public sealed class ResumedOpeningNoteTests
{
    /// <summary>Each way the words are said is a reason of the family, beside the English the record always kept.</summary>
    [Theory]
    [InlineData(true, false, true, ResumedWhy.Answer, "— your answer is the next turn of its own conversation, resumed on `claude-code`.")]
    [InlineData(false, false, true, ResumedWhy.Words, "— your words are the next turn of its own conversation, resumed on `claude-code`.")]
    [InlineData(false, true, false, ResumedWhy.Findings, "— another agent's findings are the next turn of its own conversation, resumed on `claude-code`.")]
    [InlineData(true, true, true, ResumedWhy.AnswerAndFindings, "— your answer and another agent's findings are the next turn of its own conversation, resumed on `claude-code`.")]
    [InlineData(false, true, true, ResumedWhy.WordsAndFindings, "— your words and another agent's findings are the next turn of its own conversation, resumed on `claude-code`.")]
    public void Each_way_the_words_are_said_is_a_reason_beside_its_english(bool answer, bool findings, bool persons, string why, string english)
    {
        var line = Continuations.OpeningNoted("claude-code", null, null, answer, findings, persons);

        Assert.Equal(english, line.Note);
        Assert.Equal(english, Continuations.Opening("claude-code", null, null, answer, findings, persons));
        Assert.Equal(["resumed.opening"], NoteAssert.Codes(line.Parts));
        NoteAssert.Holds(line);
        Assert.Equal(("claude-code", why), (line.Parts[0].Value("adapter"), line.Parts[0].Value("why")));
    }

    /// <summary>A version that moved is a part of its own, riding inside the line, and the English says it as it always did.</summary>
    [Fact]
    public void A_version_that_moved_rides_inside_the_line_as_two_facts()
    {
        var line = Continuations.OpeningNoted("claude-code", "2.1.300", "2.1.281");

        Assert.Equal("— your answer is the next turn of its own conversation, resumed on `claude-code` 2.1.300; it opened on 2.1.281.", line.Note);
        Assert.Equal(["resumed.opening", "resumed.moved"], NoteAssert.Codes(line.Parts));
        NoteAssert.Holds(line);
        Assert.Equal(("2.1.300", "2.1.281"), (line.Parts[1].Value("now"), line.Parts[1].Value("then")));
    }

    /// <summary>A version that did not move, or that the record never kept, says nothing of versions.</summary>
    [Theory]
    [InlineData("0.84.0", "0.84.0")]
    [InlineData("2.1.300", null)]
    [InlineData(null, "2.1.281")]
    public void A_version_that_did_not_move_is_no_part(string? now, string? then)
    {
        Assert.Equal(["resumed.opening"], NoteAssert.Codes(Continuations.OpeningNoted("claude-code", now, then).Parts));
    }

    /// <summary>The native door's line for words said while it worked is a code of its own.</summary>
    [Fact]
    public void The_native_door_s_line_is_a_code_of_its_own()
    {
        var line = NativeWords.Opening("claude-code", [new ChatMessage("And 9090.", [])])[0];

        Assert.Equal("— what you said while it worked is the next turn of its own conversation, resumed on `claude-code`.", line.Text);
        Assert.Equal(["resumed.native"], NoteAssert.Codes(line.Parts));
        NoteAssert.Holds(line.Text, line.Parts);
        Assert.Equal("claude-code", line.Parts![0].Value("adapter"));
    }

    /// <summary>The ask writes the line's parts into the record with its English; an ask handed English alone writes none.</summary>
    [Fact]
    public void The_ask_keeps_the_parts_beside_its_first_line()
    {
        var coded = new ResumeAsk("conv-1", [], Continuations.OpeningNoted("claude-code", "2.1.300", "2.1.281"));
        var first = coded.Opening()[0];
        Assert.Equal(["resumed.opening", "resumed.moved"], NoteAssert.Codes(first.Parts));

        var plain = new ResumeAsk("conv-1", [], "— your words are the next turn of its own conversation.");
        Assert.Null(plain.Opening()[0].Parts);
    }
}
