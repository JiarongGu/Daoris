using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// MIXAGENT2a-ii (D163 point 4): a helper's answer waits on the asker's record as a word marked `helper`, which the driver reads
/// as another agent's claim: never the person's words, never a second opinion's findings, and said so in the resumed turn's
/// first line by a reason of its own.
/// </summary>
public sealed class HelperWordsTests
{
    private static SaidWordView Word(string json) => ServiceClient.ReadWord(JsonDocument.Parse(json).RootElement)!;

    [Fact]
    public void A_word_marked_helper_is_read_as_the_helpers_and_is_never_the_persons()
    {
        var helper = Word("""{ "id": "w1", "text": "It is exclusive.", "at": "2026-10-12T10:00:00Z", "files": [], "reopens": true, "helper": "h1a2b3c4" }""");
        var person = Word("""{ "id": "w2", "text": "Go on.", "at": "2026-10-12T10:01:00Z", "files": [], "reopens": true }""");
        var finding = Word("""{ "id": "w3", "text": "Claims.", "at": "2026-10-12T10:02:00Z", "files": [], "reopens": true, "by": "o1" }""");

        Assert.Equal(("h1a2b3c4", null, false), (helper.Helper, helper.By, helper.Persons));
        Assert.Equal((null, true), (person.Helper, person.Persons));
        Assert.Equal((null, "o1", false), (finding.Helper, finding.By, finding.Persons));
    }

    /// <summary>A record names the helpers whose answers wait, each once, apart from the opinions' findings.</summary>
    [Fact]
    public void A_record_names_the_helpers_whose_answers_wait_apart_from_its_findings()
    {
        var at = DateTimeOffset.Parse("2026-10-12T10:00:00Z");
        var record = new PriorSession("s1", "tree", "completed")
        {
            Said =
            [
                new SaidWordView("w1", "a", at, [], true) { Helper = "h1" },
                new SaidWordView("w2", "b", at, [], true) { Helper = "h1" },
                new SaidWordView("w3", "c", at, [], true) { By = "o1" },
                new SaidWordView("w4", "d", at, [], true),
            ],
        };

        Assert.Equal(["h1"], record.Helpers);
        Assert.Equal(["o1"], record.Findings);
    }

    /// <summary>
    /// The notes a helper writes are each a code with facts only, and every refusal a reason of its own family, each declared once:
    /// the agent, the session and why, never an account's name or a path.
    /// </summary>
    [Fact]
    public void The_helpers_notes_are_codes_with_facts_only_and_every_refusal_a_declared_reason()
    {
        var reasons = typeof(HelperWhy).GetFields()
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .Order(StringComparer.Ordinal);
        Assert.Equal(reasons, NoteCodes.Helper.All.Select(reason => reason.Code).Order(StringComparer.Ordinal));

        var asked = HelperNotes.Asked("codex-acp", "h1a2b3c4");
        Assert.Equal(["helper.asked"], NoteAssert.Codes(asked.Parts));
        Assert.Equal(("codex-acp", "h1a2b3c4"), (asked.Parts[0].Value("agent"), asked.Parts[0].Value("session")));
        NoteAssert.Holds(asked);

        Assert.Equal(["helper.answered"], NoteAssert.Codes(HelperNotes.Answered("codex-acp", "h1", lastWords: false).Parts));
        Assert.Equal(["helper.answered-last-words"], NoteAssert.Codes(HelperNotes.Answered("codex-acp", "h1", lastWords: true).Parts));

        var refused = HelperNotes.Refused(HelperWhy.NoAnswer, "codex-acp");
        Assert.Equal(["helper.refused-by"], NoteAssert.Codes(refused.Parts));
        Assert.Equal(("codex-acp", "no-answer"), (refused.Parts[0].Value("agent"), refused.Parts[0].Value("why")));
        Assert.Equal("Helper codex-acp did not answer this question: the helper ended without an answer.", refused.Note);
        Assert.Equal(["helper.refused"], NoteAssert.Codes(HelperNotes.Refused(HelperWhy.Off).Parts));

        Assert.Equal(["helper.helping"], NoteAssert.Codes(HelperNotes.Helping("s1a2b3c4").Parts));
    }

    [Theory]
    [InlineData(false, ResumedWhy.Helper, "— a helper's answer is the next turn of its own conversation, resumed on `claude-code`.")]
    [InlineData(true, ResumedWhy.WordsAndHelper, "— your words and a helper's answer are the next turn of its own conversation, resumed on `claude-code`.")]
    public void The_resumed_turns_first_line_says_a_helpers_answer_by_a_reason_of_its_own(bool persons, string why, string english)
    {
        var line = Continuations.OpeningNoted("claude-code", null, null, answer: false, findings: false, persons, helper: true);

        Assert.Equal(english, line.Note);
        Assert.Equal(["resumed.opening"], NoteAssert.Codes(line.Parts));
        NoteAssert.Holds(line);
        Assert.Equal(why, line.Parts[0].Value("why"));
    }

    /// <summary>The answer reaches the asker prefaced as the helper's claim, with where it came from, and never as the person's words.</summary>
    [Fact]
    public void The_answer_is_prefaced_as_the_helpers_claim_and_quoted_whole()
    {
        var text = HelperWords.Compose("codex-acp", "h1a2b3c4", "Why is it exclusive?", "It is exclusive at src/report.ts:42.\nThe test uses a one-day window.", lastWords: false);

        Assert.StartsWith("Another agent, `codex-acp` (helper session `h1a2b3c4`), answered the question you asked it", text);
        Assert.Contains("It is a claim to check against the code, not the person's words", text);
        Assert.Contains("> Why is it exclusive?", text);
        Assert.Contains("> It is exclusive at src/report.ts:42.\n> The test uses a one-day window.", text);
        Assert.DoesNotContain("did not use its answer tool", text);

        var last = HelperWords.Compose("codex-acp", "h1a2b3c4", "Why?", "Probably the window.", lastWords: true);
        Assert.Contains("did not use its answer tool", last);
        Assert.Contains("its last words, not a deliberate answer", last);
        Assert.DoesNotContain("capture", last);
    }

    /// <summary>
    /// COAGENT1a (the co-agent design §2.5 point 5): the answer names the capture its helper read, by its name, commit, moment and
    /// what it held, and says the work may have moved since; a capture with nothing uncommitted says so.
    /// </summary>
    [Fact]
    public void The_answer_names_the_capture_its_helper_read_and_says_the_work_may_have_moved()
    {
        var at = DateTimeOffset.Parse("2026-10-22T09:30:00Z");
        var capture = new HelperCaptured(
            "0123456789ab", "2222222222222222222222222222222222222222", at,
            [new CapturedFile("src/a.ts", new string('a', 64), 3), new CapturedFile("b.bin", new string('b', 64), 4)], ["gone.md"], []);

        var text = HelperWords.Compose("codex-acp", "h1a2b3c4", "Why?", "Line 42.", lastWords: false, capture);

        Assert.Contains(
            "It read your work as Daoris captured it at 2026-10-22 09:30 UTC (capture `0123456789ab`: commit `2222222` with 2 changed or new "
            + "files and 1 deletion). Your work may have moved since.", text);
        Assert.True(text.IndexOf("capture `0123456789ab`", StringComparison.Ordinal) < text.IndexOf("> Line 42.", StringComparison.Ordinal));
        var clean = HelperWords.Compose("codex-acp", "h1a2b3c4", "Why?", "Line 42.", lastWords: false, capture with { Files = [], Deleted = [] });
        Assert.Contains("commit `2222222` with nothing uncommitted", clean);
    }
}
