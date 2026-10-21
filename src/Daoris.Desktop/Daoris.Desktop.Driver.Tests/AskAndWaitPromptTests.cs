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
    /// D83, reached on purpose: FG5's second verify session needed a sign-in only the person could give,
    /// and DECLINED — because its instruction offered only done or decline, so the quest closed with the
    /// work unchecked. A session told it may stop and ask parks instead, and the person's answer carries
    /// the quest on in the same tree.
    /// </summary>
    [Fact]
    public void A_quests_target_says_what_only_the_person_can_give_is_asked_for_by_stopping_not_declining()
    {
        // Flat, as the sentence tests below read it: a phrase is found however the instruction wraps it.
        var prompt = Flat(TargetPrompt.Compose(Target()));

        Assert.Contains("only for what you cannot decide", prompt);
        Assert.Contains("end your turn with the quest still taken", prompt);
        Assert.Contains("rather than declining", prompt);
    }

    /// <summary>The three driven instructions: claiming, resuming after an answer, and carrying on after a cut-off.</summary>
    private static IEnumerable<(string Which, string Prompt)> EveryInstruction(SessionTarget target) =>
    [
        ("claiming", TargetPrompt.Compose(target)),
        ("resuming", TargetPrompt.Compose(target with { Answered = Question() })),
        ("carrying on", TargetPrompt.Compose(target with { CutOff = "timed out after 30 minutes and was killed." })),
    ];

    /// <summary>The prompt with every run of whitespace one space, so a sentence is found however it wraps.</summary>
    private static string Flat(string prompt) => System.Text.RegularExpressions.Regex.Replace(prompt, @"\s+", " ");

    /// <summary>
    /// WSSETUP9 (D124 §6.1): a driven session in a report repository stopped to ask the person which report
    /// a ticket meant, a choice its own notes and code could settle, because its instruction offered "a
    /// choice between options that is theirs" as a reason to stop. Every driven instruction now sends it to
    /// the sources first, names them, and has it decide what they settle.
    /// </summary>
    [Fact]
    public void Every_instruction_says_to_look_before_asking_and_names_where()
    {
        foreach (var (which, prompt) in EveryInstruction(Target()))
        {
            var flat = Flat(prompt);
            Assert.True(flat.Contains("Look before you ask.", StringComparison.Ordinal), which);
            Assert.True(flat.Contains("The quest, its links and its files;", StringComparison.Ordinal), which);
            Assert.True(flat.Contains(
                "this repository's own documents, code and history (its log, and the commits that last changed what you are changing);",
                StringComparison.Ordinal), which);
            Assert.True(flat.Contains("the workspace's knowledge, through your connector's `knowledge_search`.", StringComparison.Ordinal), which);
            Assert.True(flat.Contains(
                "A question one of these settles is not a question: decide it, and keep what settled it for your closing note.",
                StringComparison.Ordinal), which);
        }
    }

    /// <summary>
    /// A reading the evidence leans to is taken and said, not asked: it is committed on the session's branch,
    /// named in its close, and reviewed with the diff, so the person corrects it in review rather than being
    /// stopped by it (D124 §6.1).
    /// </summary>
    [Fact]
    public void A_reading_the_evidence_leans_to_is_taken_and_said_in_the_close()
    {
        foreach (var (which, prompt) in EveryInstruction(Target()))
        {
            Assert.True(Flat(prompt).Contains(
                "Where the evidence leans one way without settling it, take that reading, carry on, and say in your closing note "
                + "which reading you took and on what evidence, so the person can correct it in review rather than be stopped by it.",
                StringComparison.Ordinal), which);
        }
    }

    /// <summary>
    /// AUTONOMY1e (D184 point 4): a session's questions work as Claude Code's console does. Whatever it can decide it decides,
    /// records why and keeps going; on the install, parks waited 7 to 57 minutes on questions such as which report a page
    /// shows, or a name, which "a preference nothing records" (D124 §6.1) had let through. It asks only what it cannot decide,
    /// and no go-ahead it asked for before is dropped (D184 point 5): outside the repository and on a production system still
    /// are, and so is one the repository or the quest names.
    /// </summary>
    [Fact]
    public void It_decides_what_it_can_and_asks_only_what_it_cannot()
    {
        foreach (var (which, prompt) in EveryInstruction(Target()))
        {
            var flat = Flat(prompt);
            Assert.True(flat.Contains(
                "Decide whatever you reasonably can, such as a name or which of two sound ways: say it under "
                + "**Readings** with why, and keep going.",
                StringComparison.Ordinal), which);
            Assert.True(flat.Contains(
                "Ask the person only for what you cannot decide: a credential, an account or a sign-in; a choice the quest or "
                + "the person's words leave to them; a go-ahead for an act outside this repository or on a production system, "
                + "or for one this repository or the quest says needs it.",
                StringComparison.Ordinal), which);
            Assert.False(flat.Contains("a preference nothing records", StringComparison.Ordinal), which);
            Assert.False(flat.Contains("choice between options", StringComparison.Ordinal), which);
        }
    }

    /// <summary>
    /// AUTONOMY1e (D184 point 4): the question is asked at its turn's end, in its own words, in its own conversation, and the
    /// person's answer goes on in that same conversation (ANSWER1a, D131 §1), which the instruction once told as a fresh start
    /// "here, in this tree".
    /// </summary>
    [Fact]
    public void It_asks_at_its_turns_end_and_goes_on_in_this_conversation_with_the_answer()
    {
        foreach (var (which, prompt) in EveryInstruction(Target()))
        {
            var flat = Flat(prompt);
            Assert.True(flat.Contains(
                "Ask it in your last message, with why and what you looked at; commit what you have, and "
                + "end your turn with the quest still taken, rather than declining. The person answers in this conversation, and "
                + "you go on here with their words.",
                StringComparison.Ordinal), which);
            Assert.False(flat.Contains("you are started again here, in this tree, with their words", StringComparison.Ordinal), which);
        }
    }

    /// <summary>
    /// KNOWUSE1c (D135 §4): 31 of the 46 items put to the owner came in closing notes, readings put as decisions under
    /// headings such as "Decisions for you" beside the production yeses, and none quoting the ticket's line it rested on.
    /// The close keeps what only the person can give apart from what the session took, and every item names its source.
    /// </summary>
    [Fact]
    public void A_closing_note_keeps_what_needs_the_person_apart_from_readings_each_naming_its_source()
    {
        foreach (var (which, prompt) in EveryInstruction(Target()))
        {
            var flat = Flat(prompt);
            Assert.True(flat.Contains(
                "Your closing note, and your last message whenever you stop, keeps two lists apart.", StringComparison.Ordinal), which);
            Assert.True(flat.Contains(
                // AUTONOMY1e (D184 point 4): what it could not decide, never a preference it could have made itself.
                "Under **Needs you**, only what you could not decide — a go-ahead, an agreement this repository's own "
                + "documents require, a credential or a sign-in, a choice left to the person — each with why, and what you "
                + "looked at first.",
                StringComparison.Ordinal), which);
            Assert.True(flat.Contains(
                "Under **Readings**, everything else you decided or took on the evidence, each said as your reading rather than "
                + "asked as a question, and each naming what it rests on: the line of the quest or of the ticket it links, quoted; "
                + "a document's path and line; or a code path.",
                StringComparison.Ordinal), which);
            Assert.True(flat.Contains(
                "An item that names nothing it checked has not been looked into yet: look before you write it.",
                StringComparison.Ordinal), which);
        }
    }

    /// <summary>
    /// KNOWUSE1d (D135 §5, D133 §1): a fresh attempt found the person's answer, misread, in its first attempt's closed note
    /// ("Per your answer") and in a document on that branch, and wrote into the repository's knowledge that the owner had
    /// settled it. The person's words are those the instruction quotes as theirs, which the ask's record holds; a quote met
    /// anywhere else is someone's reading, relied on only as one and never recorded as theirs.
    /// </summary>
    [Fact]
    public void The_persons_words_quoted_second_hand_are_only_a_reading()
    {
        foreach (var (which, prompt) in EveryInstruction(Target()))
        {
            var flat = Flat(prompt);
            Assert.True(flat.Contains("Only the words this instruction quotes as the person's own are theirs.", StringComparison.Ordinal), which);
            Assert.True(flat.Contains(
                "Their words quoted anywhere else — in a document, a closed quest's note, a commit, an earlier session's record — "
                + "are someone's reading of them, however firmly attributed (\"per your answer\", \"as the owner decided\").",
                StringComparison.Ordinal), which);
            Assert.True(flat.Contains(
                "Rely on one only as a reading: it goes under **Readings** in your closing note, naming where you found it, and "
                + "nothing you write in this repository records it as the person's words or decision.",
                StringComparison.Ordinal), which);
        }
    }

    /// <summary>
    /// Said with the look, where such a quote is met, and below the words the instruction quotes as the person's from the
    /// ask's record (DRIFT1b), so what "quotes as theirs" points to is already above it.
    /// </summary>
    [Fact]
    public void A_second_hand_quote_is_said_after_the_look_and_below_the_words_the_ask_holds()
    {
        foreach (var (which, prompt) in EveryInstruction(TargetPromptGoldenTests.Full))
        {
            var words = prompt.IndexOf("The person's own words on ask `#a1b2c3`", StringComparison.Ordinal);
            var look = prompt.IndexOf("Look before you ask.", StringComparison.Ordinal);
            var attributed = prompt.IndexOf("Only the words this instruction quotes", StringComparison.Ordinal);
            var neighbour = prompt.IndexOf("do not guess: ask it.", StringComparison.Ordinal);
            Assert.True(words >= 0 && words < look && look < attributed && attributed < neighbour, which);
        }
    }

    /// <summary>The close's two lists are said after the stop for the person, which is one of the two places they are written.</summary>
    [Fact]
    public void The_closing_notes_two_lists_come_after_the_stop_for_the_person()
    {
        foreach (var (which, prompt) in EveryInstruction(Target()))
        {
            var person = prompt.IndexOf("Ask the person only for what", StringComparison.Ordinal);
            var close = prompt.IndexOf("Your closing note, and your last message", StringComparison.Ordinal);
            var propose = prompt.IndexOf("If a command the work genuinely needs is refused", StringComparison.Ordinal);
            Assert.True(person >= 0 && person < close && close < propose, which);
        }
    }

    /// <summary>
    /// KNOWUSE1c (D135 §4): the look names the indexes the repository keeps for its own documents, where its tree keeps any,
    /// so a session starts from them rather than from "this repository's own documents" alone. None, and the look reads as
    /// it did.
    /// </summary>
    [Fact]
    public void The_look_names_the_repositorys_own_indexes_where_its_tree_keeps_them()
    {
        foreach (var (which, prompt) in EveryInstruction(Target()))
        {
            Assert.False(prompt.Contains("indexes its own documents", StringComparison.Ordinal), which);
        }

        foreach (var (indexes, named) in new (string[], string)[]
        {
            (["docs/README.md"], "`docs/README.md`"),
            (["docs/README.md", ".claude/INDEX.md"], "`docs/README.md` and `.claude/INDEX.md`"),
            (["docs/README.md", ".claude/rules/RULES_INDEX.md", ".claude/rules/RULES_INDEX_CROSS.md"],
                "`docs/README.md`, `.claude/rules/RULES_INDEX.md` and `.claude/rules/RULES_INDEX_CROSS.md`"),
        })
        {
            foreach (var (which, prompt) in EveryInstruction(Target() with { Indexes = indexes }))
            {
                Assert.True(Flat(prompt).Contains(
                    $"through your connector's `knowledge_search`. This repository indexes its own documents in {named}: start the "
                    + "look there, and read every document whose entry matches this work. A question one of these settles",
                    StringComparison.Ordinal), $"{which}, {indexes.Length}");
            }
        }
    }

    /// <summary>A pointer, not a listing: past <see cref="TargetPrompt.IndexLimit"/> the rest are counted, not named.</summary>
    [Fact]
    public void The_look_names_a_bounded_number_of_indexes_and_counts_the_rest()
    {
        var indexes = Enumerable.Range(1, TargetPrompt.IndexLimit + 2).Select(n => $"docs/INDEX_{n:00}.md").ToArray();

        var flat = Flat(TargetPrompt.Compose(Target() with { Indexes = indexes }));

        Assert.Contains($"`docs/INDEX_{TargetPrompt.IndexLimit:00}.md` and 2 more like them: start the look there", flat);
        Assert.DoesNotContain($"docs/INDEX_{TargetPrompt.IndexLimit + 1:00}.md", flat);
    }

    /// <summary>
    /// The look comes first, then asking another repository, then stopping for the person: the order a session
    /// should reach for them in.
    /// </summary>
    [Fact]
    public void The_look_comes_before_asking_another_repository_and_that_before_stopping_for_the_person()
    {
        foreach (var (which, prompt) in EveryInstruction(Target()))
        {
            var look = prompt.IndexOf("Look before you ask.", StringComparison.Ordinal);
            var neighbour = prompt.IndexOf("do not read into it and do not guess: ask it.", StringComparison.Ordinal);
            var person = prompt.IndexOf("Ask the person only for what", StringComparison.Ordinal);
            Assert.True(look >= 0 && look < neighbour && neighbour < person, which);
        }
    }

    /// <summary>
    /// The other checkouts are a source only where the session may read them (D107), and are pointed to where
    /// this instruction lists them: above, read only; below, in the boundary, where each is also a declared
    /// target; or both. Where reading across is off, no checkout is named, as before.
    /// </summary>
    [Fact]
    public void The_other_checkouts_are_a_source_only_where_the_session_may_read_across()
    {
        var app = new AcrossCheckout("app", "C:/work/app");
        var engine = new AcrossCheckout("engine", "C:/work/engine");

        foreach (var (which, prompt) in EveryInstruction(Target()))
        {
            Assert.False(Flat(prompt).Contains("other checkouts", StringComparison.Ordinal), which);
        }

        foreach (var (target, where) in new[]
        {
            (Target() with { ReadsAcross = [app] }, "above"),
            (Target() with { ReadsAcross = [engine], WritesAcross = [engine] }, "below"),
            (Target() with { ReadsAcross = [app, engine], WritesAcross = [engine] }, "above and below"),
        })
        {
            foreach (var (which, prompt) in EveryInstruction(target))
            {
                Assert.True(Flat(prompt).Contains(
                    $"through your connector's `knowledge_search`; and the other checkouts listed {where}. A question",
                    StringComparison.Ordinal), $"{which}, {where}");
            }
        }
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

    /// <summary>
    /// 🔴 SAYHELD1, the owner's case on the install (2026-10-21): the person wrote to the session that asked while its quest waited,
    /// and the resume opened a new session that was never handed their words. A resume is handed every word the session that asked
    /// never took, after what came back: the person's quoted whole, and Daoris's as Daoris's. A resume handed none reads as it did.
    /// </summary>
    [Fact]
    public void A_resumed_target_is_handed_the_words_said_to_the_session_that_asked_while_it_waited()
    {
        var at = DateTimeOffset.Parse("2026-10-21T10:48:45Z");
        var prompt = TargetPrompt.Compose(Target() with
        {
            Answered = Question(),
            Handed =
            [
                new SaidWordView("w1", "Use the staging database,\nnot production.", at, [], Reopens: true),
                new SaidWordView("w2", "The person reopened the work it grew from.", at, [], Reopens: true) { From = DaorisWords.From },
            ],
        });

        var answer = prompt.IndexOf("> The endpoint is POST /notes", StringComparison.Ordinal);
        var said = prompt.IndexOf("  > Use the staging database,\n  > not production.", StringComparison.Ordinal);
        var daoris = prompt.IndexOf("  > The person reopened the work it grew from.", StringComparison.Ordinal);
        Assert.True(answer >= 0 && said > answer && daoris > said, prompt);
        Assert.Contains("the person wrote to that session", prompt);
        Assert.Contains("Daoris told that session", prompt);

        var none = TargetPrompt.Compose(Target() with { Answered = Question() });
        Assert.DoesNotContain("wrote to that session", none);
        Assert.DoesNotContain("Daoris told that session", none);
    }

    /// <summary>
    /// SAYHELD1: the resume's record opens with the target, then each word it was handed from the session that asked under the id that
    /// record gave it, the person's as theirs and Daoris's as Daoris's, so the conversation shows them where it took them (D137 §3.1).
    /// </summary>
    [Fact]
    public void A_resume_opens_with_the_target_and_then_each_word_it_was_handed_under_its_id()
    {
        var at = DateTimeOffset.Parse("2026-10-21T10:48:45Z");
        IReadOnlyList<SaidWordView> handed =
        [
            new SaidWordView("w1", "Use staging.", at, ["notes.txt"], Reopens: true),
            new SaidWordView("w2", "A reopen was told.", at, [], Reopens: true) { From = DaorisWords.From },
        ];

        var opening = HarnessCapture.Opening("the target", null, handed: handed);

        Assert.Equal(
            [
                ("user", "target", (string?)null, "the target", (string?)null),
                ("user", "person", "w1", "Use staging.", null),
                ("user", "target", "w2", "A reopen was told.", DaorisWords.From),
            ],
            opening.Select(e => (e.Kind, e.Origin, e.Id, e.Text, e.From)));
        Assert.Equal(["notes.txt"], opening[1].Files);
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

    /// <summary>
    /// STANDDOWN2: a carry-on after the person answered a parked session is told what they said, in
    /// their words, and that the session before asked rather than was cut off.
    /// </summary>
    [Fact]
    public void A_carry_on_after_the_persons_answer_is_handed_the_answer_in_their_words()
    {
        var prompt = TargetPrompt.Compose(Target() with
        {
            CutOff = "asked the person (merge, sign-in, apply), and was answered: Signed in; apply to dev.",
            PersonSaid = "Signed in; apply to dev.",
        });

        Assert.Contains("carrying on quest `#abc123`", prompt);
        Assert.Contains("stopped to ask the person, and they answered", prompt);
        Assert.Contains("> Signed in; apply to dev.", prompt);
        Assert.DoesNotContain("was cut off", prompt);
    }

    /// <summary>
    /// SESSUX1b (D126 §3.4): a carry-on after the person released their stop is told that they stopped the session before
    /// and released the quest, never that it was cut off, and that the quest is still its own.
    /// </summary>
    [Fact]
    public void A_carry_on_after_the_person_released_their_stop_says_they_stopped_it_and_released_it()
    {
        var prompt = TargetPrompt.Compose(Target() with { CutOff = "the person stopped it.", Released = true });

        Assert.Contains("carrying on quest `#abc123`", prompt);
        Assert.Contains("do not take it again", prompt);
        Assert.Contains(
            "An earlier session on this quest was stopped by the person before it closed it, and they have since released the "
            + "quest for you to carry on. Its record reads: the person stopped it.", prompt);
        Assert.DoesNotContain("was cut off", prompt);
    }

    /// <summary>
    /// 🔴 An answer to a parked session was nowhere on the page after it was sent. The carry-on's
    /// record opens with the target and then the person's answer, as theirs, so the conversation shows
    /// what they said rather than folding it inside the target.
    /// </summary>
    [Fact]
    public void A_carry_on_after_the_persons_answer_opens_with_the_target_and_then_their_words()
    {
        var opening = HarnessCapture.Opening("the target", "go ahead with the PUT");

        Assert.Equal(
            [("user", "target", "the target"), ("user", "person", "go ahead with the PUT")],
            opening.Select(e => (e.Kind, e.Origin, e.Text)));
        Assert.Equal([("user", "target", "the target")], HarnessCapture.Opening("the target", null).Select(e => (e.Kind, e.Origin, e.Text)));
    }

    /// <summary>
    /// CHAIN2: a next step whose tree grew from its parent's branch is told the work is here, so it
    /// neither asks for a merge nor tries to make one — FG5's verify step asked for exactly that.
    /// </summary>
    [Fact]
    public void A_next_step_grown_from_its_parents_branch_is_told_the_work_is_in_its_tree()
    {
        var prompt = TargetPrompt.Compose(Target() with { Parent = "p1", GrewFrom = "daoris/s-a900f1ad" });

        Assert.Contains("It follows quest `#p1`", prompt);
        Assert.Contains("grew from `daoris/s-a900f1ad`", prompt);
        Assert.Contains("no merge to wait for", prompt);
        Assert.DoesNotContain("grew from", TargetPrompt.Compose(Target() with { Parent = "p1" }));
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
