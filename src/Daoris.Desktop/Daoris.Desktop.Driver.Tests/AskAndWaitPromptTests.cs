using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Ask and wait (D79). Measured on a real run: a session whose ticket needed another repository's
/// contract tried to read that repository, was refused, and guessed — because nothing it was handed
/// said the answer was to ask. The target has to say so, and a resumed session has to be told it is
/// resuming, or the claiming instruction sends it to take a quest it already holds.
/// </summary>
public sealed class AskAndWaitPromptTests
{
    private static SessionTarget Target() => new(
        QuestId: "abc123",
        Title: "Add the note field",
        Body: "The report needs a note column.",
        Asker: "ask #9f45",
        Repository: "console-ui",
        Root: "C:/somewhere/console-ui",
        ServiceUrl: "http://localhost:5177");

    private static QuestView Question(string status = "Done", string? note = "The endpoint is POST /notes; it takes { text }.") =>
        new("q2", "console-ui", "notes-api", "What does the notes endpoint take?", "We need the contract.", status)
        {
            Note = note,
        };

    [Fact]
    public void A_quests_target_says_to_ask_the_repository_that_knows_and_wait()
    {
        var prompt = TargetPrompt.Compose(Target());

        Assert.Contains("do not read into it and do not guess", prompt);
        Assert.Contains("respond to `#abc123` with `wait`", prompt);
        Assert.Contains("commit what you have", prompt);
        Assert.Contains("end your turn", prompt);
        // Said as the canon speaks — it travels to repositories that know nothing of this numbering.
        Assert.DoesNotContain("D79", prompt);
        Assert.Contains("Never write outside", prompt);
    }

    /// <summary>
    /// 🔴 The claiming instruction says "if the quest is already taken, stand down" — which a resumed
    /// session's quest always is. Handed that, it would finish having done nothing, every time.
    /// </summary>
    [Fact]
    public void A_resumed_target_says_the_quest_is_already_its_own_and_carries_the_answer()
    {
        var prompt = TargetPrompt.Compose(Target() with { Answered = Question() });

        Assert.Contains("resuming quest `#abc123`", prompt);
        Assert.Contains("do not take it again", prompt);
        Assert.Contains("do not stand down", prompt);
        Assert.DoesNotContain("First take the quest", prompt);
        Assert.DoesNotContain("stand down and finish", prompt);
        Assert.Contains("`#q2`", prompt);
        Assert.Contains("`notes-api` closed it done", prompt);
        Assert.Contains("> The endpoint is POST /notes", prompt);
        Assert.Contains("read its commits", prompt);
        // It may need to ask again, and the boundary still holds.
        Assert.Contains("with `wait`", prompt);
        Assert.Contains("Never write outside", prompt);
    }

    [Fact]
    public void A_declined_question_is_said_to_be_declined_with_its_reason()
    {
        var prompt = TargetPrompt.Compose(Target() with { Answered = Question("Declined", "Not ours — the gateway owns notes.") });

        Assert.Contains("`notes-api` declined it", prompt);
        Assert.Contains("> Not ours — the gateway owns notes.", prompt);
    }

    /// <summary>
    /// D80: a session carrying a quest on after a cut-off is told what cut the last one off, that the
    /// quest is already its own, and where the earlier work is — uncommitted as well as committed,
    /// because a timeout lands mid-change.
    /// </summary>
    [Fact]
    public void A_carried_on_target_says_the_quest_is_its_own_and_what_cut_the_last_session_off()
    {
        var prompt = TargetPrompt.Compose(Target() with { CutOff = "timed out after 30 minutes and was killed." });

        Assert.Contains("carrying on quest `#abc123`", prompt);
        Assert.Contains("do not take it again", prompt);
        Assert.Contains("timed out after 30 minutes and was killed.", prompt);
        Assert.Contains("uncommitted", prompt);
        Assert.DoesNotContain("First take the quest", prompt);
        Assert.Contains("with `wait`", prompt);
        Assert.Contains("Never write outside", prompt);
    }

    /// <summary>A close with no note says so and where the answer is, rather than quoting nothing.</summary>
    [Fact]
    public void A_question_closed_without_a_note_points_at_the_quest_and_what_landed()
    {
        var prompt = TargetPrompt.Compose(Target() with { Answered = Question(note: null) });

        Assert.Contains("without a note", prompt);
        Assert.DoesNotContain("> ", prompt);
    }
}
