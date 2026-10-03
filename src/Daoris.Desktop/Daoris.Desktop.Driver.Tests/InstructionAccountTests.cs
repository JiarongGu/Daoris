using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What a session was handed, section by section (CONTEXT1, D143 point 1): the composer's account of each instruction it
/// composes, each section's size, its source and what its bound left out. The words are the goldens' (<see
/// cref="TargetPromptGoldenTests"/>); this holds the account beside them, and every bound's cut.
/// </summary>
public sealed class InstructionAccountTests
{
    private static readonly DateTimeOffset Asked = DateTimeOffset.Parse("2026-10-01T02:26:00Z");

    private static HandedSection Section(InstructionAccount account, string name) =>
        Assert.Single(account.Sections, section => section.Name == name);

    private static HandedCut Cut(HandedSection section, string code) =>
        Assert.Single(section.Cuts ?? [], cut => cut.Code == code);

    /// <summary>The instructions every kind of start composes, from the goldens' targets and a resume and an intake.</summary>
    public static TheoryData<string> Every() => new() { "bare", "full", "carry-on", "resume", "intake", "everything" };

    private static SessionTarget Of(string which) => which switch
    {
        "resume" => TargetPromptGoldenTests.Full with
        {
            Answered = new QuestView("q2", "reports", "bridge", "What does the tile feed take?", "We need the contract.", "Done")
            {
                Note = "It takes the v3 tile id.",
            },
        },
        "intake" => TargetPromptGoldenTests.Bare with { Ask = "a1b2c3", Prompt = "You are the intake for workspace `default`.\n\nThe person asked." },
        "everything" => TargetPromptGoldenTests.Full with
        {
            Attachments = [new QuestFileView("spec.pdf", "00", 10, "C:/daoris/files/spec.pdf"), new QuestFileView("shot.png", "01", 10, null)],
            Parent = "p1",
            Then = [new QuestStepView("bridge", "Verify the report", "Check it.")],
            CodeMap = "docs/code-map.json",
            LandsOn = new LandingPlan(LandingForm.Branch, "feature/report", LandingSource.Repository),
            WritesAcross = [new AcrossCheckout("bridge", "C:/work/bridge")],
            PersonSaid = "also the changelog",
            Language = new SessionLanguage("zh", "Simplified Chinese (简体中文)", LanguageSource.Workspace),
        },
        _ => TargetPromptGoldenTests.Of(which),
    };

    [Theory]
    [MemberData(nameof(Every))]
    public void The_composed_text_is_the_instruction_and_its_handed_sections_add_up_to_it(string which)
    {
        var target = Of(which);
        var composed = TargetPrompt.Composed(target);

        Assert.Equal(TargetPrompt.Compose(target), composed.Text);
        Assert.Equal(composed.Text.Length, composed.Account.Chars);
        var handed = composed.Account.Sections.Where(section => section.None is null && section.Chars is not null).ToList();
        Assert.NotEmpty(handed);
        Assert.Equal(composed.Text.Length, handed.Sum(section => section.Chars!.Value));
        // Each section once: a section the composer joins in two pieces (the close, around the language line) is one section.
        Assert.Equal(composed.Account.Sections.Count, composed.Account.Sections.Select(section => section.Name).Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(Every))]
    public void Every_code_the_composer_writes_is_declared(string which)
    {
        var account = TargetPrompt.Composed(Of(which)).Account;
        var sections = HandedCodesTests.Declared(typeof(HandedSections));
        var sources = HandedCodesTests.Declared(typeof(HandedSources));
        var nones = HandedCodesTests.Declared(typeof(HandedNones));
        foreach (var section in account.Sections)
        {
            Assert.Contains(section.Name, sections);
            Assert.Contains(section.Source, sources);
            if (section.None is { } none) Assert.Contains(none, nones);
            foreach (var cut in section.Cuts ?? []) Assert.True(HandedCuts.Values.ContainsKey(cut.Code), cut.Code);
            Assert.False(string.IsNullOrWhiteSpace(section.Said), section.Name);
        }
    }

    /// <summary>The claiming instruction's sections, in the order it holds them, then what was not handed and why.</summary>
    [Fact]
    public void A_claims_sections_follow_the_instruction_and_the_ones_not_handed_say_why()
    {
        var account = TargetPrompt.Composed(TargetPromptGoldenTests.Bare).Account;

        Assert.Equal(
            ["quest", "close", "look", "attributed", "asking", "closing", "proposing", "boundary",
             "requirements", "words", "go-aheads", "standing", "language", "map", "indexes"],
            account.Sections.Select(section => section.Name));
        Assert.Equal(
            [("requirements", "empty"), ("words", "no-ask"), ("go-aheads", "no-ask"), ("standing", "not-set"),
             ("language", "not-set"), ("map", "empty"), ("indexes", "empty")],
            account.Sections.Where(section => section.None is not null).Select(section => (section.Name, section.None!)));
        Assert.All(account.Sections.Where(section => section.None is not null), section => Assert.Equal(0, section.Chars));

        var quest = Section(account, "quest");
        Assert.Equal(("quest", "abc123"), (quest.Source, quest.From));
        Assert.StartsWith("the quest: ", quest.Said);
        Assert.Contains("quest #abc123 asked by `console-api`", quest.Said);
        Assert.Equal("driver", Section(account, "closing").Source);
        Assert.Equal("the standing answer: none set for `console-ui` on this machine", Section(account, "standing").Said);
    }

    [Fact]
    public void A_quests_sources_are_named_by_what_each_came_from()
    {
        var account = TargetPrompt.Composed(Of("everything")).Account;

        Assert.Equal(("ask", "a1b2c3", 3, 3), (Section(account, "words").Source, Section(account, "words").From, Section(account, "words").Shown, Section(account, "words").Of));
        Assert.Equal(("ask", "a1b2c3", 2, 2), (Section(account, "go-aheads").Source, Section(account, "go-aheads").From, Section(account, "go-aheads").Shown, Section(account, "go-aheads").Of));
        var standing = Section(account, "standing");
        Assert.Equal(("repository", "reports", Asked.AddDays(-1)), (standing.Source, standing.From, standing.At));
        Assert.Equal(("workspace", "zh"), (Section(account, "language").Source, Section(account, "language").From));
        Assert.Equal(("tree", "docs/code-map.json"), (Section(account, "map").Source, Section(account, "map").From));
        Assert.Equal(("tree", 3, 3), (Section(account, "indexes").Source, Section(account, "indexes").Shown, Section(account, "indexes").Of));
        Assert.Equal(("landing", "feature/report"), (Section(account, "landing").Source, Section(account, "landing").From));
        Assert.Equal(("checkouts", 1), (Section(account, "boundary").Source, Section(account, "boundary").Shown));
        Assert.Equal("written", Section(account, "written-to").Source);
        Assert.Equal(("quest", "abc123"), (Section(account, "carried").Source, Section(account, "carried").From));
        // The file this machine does not hold is named without a path, and said to be.
        Assert.Equal(1, Cut(Section(account, "carried"), "files-elsewhere").Count);
        Assert.DoesNotContain(account.Sections, section => section.None is not null);
    }

    [Fact]
    public void A_resume_names_the_question_it_waited_on_and_a_carry_on_what_the_session_before_left()
    {
        var resumed = TargetPrompt.Composed(Of("resume")).Account;
        Assert.Equal(("quest", "q2"), (Section(resumed, "answered").Source, Section(resumed, "answered").From));
        Assert.DoesNotContain(resumed.Sections, section => section.Name is "carry-on" or "written-to");

        var carried = TargetPrompt.Composed(TargetPromptGoldenTests.CarryOn).Account;
        var left = Section(carried, "carry-on");
        Assert.Equal("record", left.Source);
        Assert.Contains("1 uncommitted change", left.Said);
        Assert.DoesNotContain(carried.Sections, section => section.Name is "answered" or "written-to");
    }

    /// <summary>An intake's instruction is its own composer's, whose parts are not counted apart: said so, with its size.</summary>
    [Fact]
    public void An_intakes_instruction_is_one_section_saying_its_parts_are_not_counted_apart()
    {
        var composed = TargetPrompt.Composed(Of("intake"));

        var intake = Assert.Single(composed.Account.Sections);
        Assert.Equal(("intake", "ask", "a1b2c3", composed.Text.Length), (intake.Name, intake.Source, intake.From, intake.Chars));
        Assert.Contains("its parts are not counted apart", intake.Said);
    }

    [Fact]
    public void Requirements_past_the_bound_are_left_out_and_counted()
    {
        var long1 = new string('a', 5_000);
        var target = TargetPromptGoldenTests.Bare with
        {
            Requirements = [new(long1, "c1"), new(long1, "c2"), new("short", "c3")],
        };

        var section = Section(TargetPrompt.Composed(target).Account, "requirements");

        Assert.Equal((1, 3), (section.Shown, section.Of));
        var cut = Cut(section, "requirements-bound");
        Assert.Equal((2, TargetPrompt.RequirementsLimit), (cut.Count, cut.Limit));
        Assert.Equal("requirements 2 to 3 left out past its bound of 8,000 characters; `quest_list` shows each whole", cut.Said);
    }

    [Fact]
    public void The_persons_older_words_past_the_bound_and_a_long_word_are_said_to_be_cut()
    {
        var words = new AskWords("a1",
        [
            new AskWordView(AskWordView.Asked, "the ask", Asked),
            new AskWordView(AskWordView.Added, new string('o', 1_500), Asked.AddHours(1)),
            new AskWordView(AskWordView.Added, new string('p', 1_500), Asked.AddHours(2)),
            .. Enumerable.Range(0, 4).Select(i => new AskWordView(AskWordView.Added, new string('n', 1_900), Asked.AddHours(3 + i))),
            new AskWordView(AskWordView.Added, new string('l', 2_500), Asked.AddHours(8)),
        ], KeptFrom: Asked.AddMinutes(-5));
        var section = Section(TargetPrompt.Composed(TargetPromptGoldenTests.Bare with { Asker = "ask #a1", Words = words }).Account, "words");

        // The sentence, then the newest that fit 8,000: the 2,500 counts as 2,000, and three of the four 1,900s.
        Assert.Equal((5, 8), (section.Shown, section.Of));
        var older = Cut(section, "words-older");
        Assert.Equal((3, AskWordsText.WordsLimit, Asked.AddHours(1), Asked.AddHours(3)), (older.Count, older.Limit, older.From, older.To));
        Assert.Equal(
            "3 older words, said from 2026-10-01 03:26 UTC to 2026-10-01 05:26 UTC, left out past its bound of 8,000 characters; "
            + "the ask's record keeps every one",
            older.Said);
        var cutLong = Cut(section, "words-long");
        Assert.Equal((1, AskWordsText.WordLimit), (cutLong.Count, cutLong.Limit));
        Assert.Equal(Asked.AddMinutes(-5), Cut(section, "words-not-kept").From);
    }

    [Fact]
    public void Words_that_could_not_be_read_are_handed_as_their_sentence_and_said_to_be_unread()
    {
        var target = TargetPromptGoldenTests.Bare with { Asker = "ask #a1", Words = AskWords.Unread("a1") };
        var account = TargetPrompt.Composed(target).Account;

        var words = Section(account, "words");
        Assert.Equal(AskWordsText.UnreadSentence("a1").Length + 2, words.Chars);
        Assert.Null(words.Shown);
        Assert.Equal("they could not be read for this start, so none after the ask itself were handed", Cut(words, "words-unread").Said);
        Assert.Equal("not-answered", Section(account, "go-aheads").None);
    }

    [Fact]
    public void Go_aheads_past_the_bound_and_a_long_act_are_said_to_be_cut()
    {
        var request = new GoAheadRequestView("s1", "q1", Asked, "why");
        var held = Enumerable.Range(1, 30)
            .Select(n => new GoAheadView(n, "write", "production", n == 1 ? new string('x', 400) : $"act {n} " + new string('w', 290), [request])
            {
                Answer = new GoAheadAnswerView(true, new string('y', 200), Asked),
            })
            .ToList();
        var target = TargetPromptGoldenTests.Bare with { Asker = "ask #a1", Words = new AskWords("a1", []) { GoAheads = held } };

        var section = Section(TargetPrompt.Composed(target).Account, "go-aheads");

        Assert.Equal(30, section.Of);
        Assert.True(section.Shown is > 0 and < 30);
        var bound = Cut(section, "go-aheads-bound");
        Assert.Equal((30 - section.Shown, GoAheadsText.Limit), (bound.Count, bound.Limit));
        Assert.StartsWith($"go-aheads {section.Shown + 1} to 30 left out past its bound of 8,000 characters", bound.Said);
        Assert.Equal((1, GoAheadsText.ActLimit), (Cut(section, "acts-long").Count, Cut(section, "acts-long").Limit));
    }

    [Fact]
    public void A_standing_answer_past_its_bound_says_how_much_was_cut()
    {
        var target = TargetPromptGoldenTests.Bare with { Standing = new StandingAnswer(new string('s', StandingText.Limit + 345), Asked) };

        var cut = Cut(Section(TargetPrompt.Composed(target).Account, "standing"), "standing-long");

        Assert.Equal((345, StandingText.Limit), (cut.Count, cut.Limit));
        Assert.Equal("345 characters of it left out past its bound of 2,000", cut.Said);
    }

    [Fact]
    public void Indexes_past_the_most_the_look_names_are_counted()
    {
        var target = TargetPromptGoldenTests.Bare with { Indexes = [.. Enumerable.Range(1, 11).Select(n => $"docs/INDEX{n}.md")] };

        var section = Section(TargetPrompt.Composed(target).Account, "indexes");

        Assert.Equal((TargetPrompt.IndexLimit, 11), (section.Shown, section.Of));
        Assert.Equal((3, TargetPrompt.IndexLimit), (Cut(section, "indexes-bound").Count, Cut(section, "indexes-bound").Limit));
    }

    [Fact]
    public void A_carry_ons_plan_past_its_bound_is_counted()
    {
        var target = TargetPromptGoldenTests.CarryOn with
        {
            LastPlan = [.. Enumerable.Range(1, TargetPrompt.PlanLimit + 7).Select(n => new PlanEntry($"step {n}", "pending"))],
            LastWords = "I stopped at the menu entries.",
        };

        var section = Section(TargetPrompt.Composed(target).Account, "carry-on");

        Assert.Equal((TargetPrompt.PlanLimit, TargetPrompt.PlanLimit + 7), (section.Shown, section.Of));
        Assert.Equal((7, TargetPrompt.PlanLimit), (Cut(section, "plan-bound").Count, Cut(section, "plan-bound").Limit));
    }

    /// <summary>The rules are handed beside the instruction: after the sections handed, before the ones that were not.</summary>
    [Fact]
    public void What_is_handed_beside_goes_after_the_sections_handed_and_before_those_that_were_not()
    {
        var account = TargetPrompt.Composed(TargetPromptGoldenTests.Bare).Account
            .Beside(new HandedSection("rules", "permissions", "the permission rules: 3 handed beside it") { Shown = 3 });

        var names = account.Sections.Select(section => section.Name).ToList();
        Assert.Equal(names.IndexOf("boundary") + 1, names.IndexOf("rules"));
        Assert.Equal(names.IndexOf("rules") + 1, names.IndexOf("requirements"));
    }
}
