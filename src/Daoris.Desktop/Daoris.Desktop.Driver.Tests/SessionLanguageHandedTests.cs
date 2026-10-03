using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LANG1c (D142 point 6, the language design §7): where the work sets a session language, one line naming it is handed beside
/// the close instruction it governs, in the claiming, resuming, carrying-on and intake instructions and a resumed
/// conversation's appendix; where none is set, every instruction is byte for byte what it was, which the golden files under
/// <c>golden/</c> hold unchanged. Never a chat's nor Ask Daoris's: neither composes from a target.
/// </summary>
public sealed class SessionLanguageHandedTests
{
    private static readonly SessionLanguage Chinese = new("zh", "Simplified Chinese (简体中文)", LanguageSource.Repository);

    private const string Line =
        "Write what you say to a person in Simplified Chinese (简体中文): a question to them, a quest's closing note or a "
        + "decline's reason, and your last words when you stop. Keep code, commands, identifiers, file names and anything you "
        + "quote exactly as written.";

    /// <summary>The close each kind of instruction ends its own paragraph with, which the line follows as a paragraph of its own.</summary>
    [Theory]
    [InlineData("claim", "stand down and finish without changing anything.")]
    [InlineData("resume", "the reason is the part\nthe asker can act on.")]
    [InlineData("carry-on", "Commit as you go, so a second cut-off loses less.")]
    public void A_set_language_adds_exactly_its_line_after_the_close(string which, string close)
    {
        var target = Of(which);
        var unset = TargetPrompt.Compose(target);
        var set = TargetPrompt.Compose(target with { Language = Chinese });

        Assert.DoesNotContain("Write what you say to a person", unset);
        Assert.Equal(1, Count(set, Line));
        Assert.Equal(unset.Replace(close, close + "\n\n" + Line, StringComparison.Ordinal), set);
        Assert.True(set.IndexOf(Line, StringComparison.Ordinal) < set.IndexOf("Look before you ask.", StringComparison.Ordinal), which);
    }

    /// <summary>Unset is today's instruction: the golden files are composed from targets that name no language.</summary>
    [Theory]
    [InlineData("bare")]
    [InlineData("full")]
    [InlineData("carry-on")]
    public void Unset_is_the_golden_instruction_byte_for_byte(string which)
    {
        var target = TargetPromptGoldenTests.Of(which);

        Assert.Null(target.Language);
        Assert.Equal(File.ReadAllText(TargetPromptGoldenTests.GoldenPath($"{which}.md")), TargetPrompt.Compose(target with { Language = null }));
    }

    /// <summary>English is a language too: the line names it as the table does.</summary>
    [Fact]
    public void English_is_named_as_the_table_names_it()
    {
        var english = new SessionLanguage("en", "English", LanguageSource.Workspace);

        Assert.Contains("Write what you say to a person in English: a question to them", TargetPrompt.Compose(TargetPromptGoldenTests.Bare with { Language = english }));
    }

    /// <summary>The intake's line goes after its close (what it says when the declarations do not settle the ask), before its boundary.</summary>
    [Theory]
    [InlineData("full")]
    [InlineData("bare")]
    public void An_intake_is_handed_its_workspaces_line_after_its_close(string which)
    {
        var ask = which == "full" ? IntakePromptGoldenTests.Full : IntakePromptGoldenTests.Bare;
        var unset = IntakePrompt.Compose(ask);
        var set = IntakePrompt.Compose(ask, Chinese with { Source = LanguageSource.Workspace });

        Assert.Equal(File.ReadAllText(IntakePromptGoldenTests.GoldenPath($"{which}.md")), unset);
        Assert.Equal(File.ReadAllText(IntakePromptGoldenTests.GoldenPath($"{which}.md")), IntakePrompt.Compose(ask, null));
        const string Boundary = "Never edit a repository, and never write outside this room";
        Assert.Equal(unset.Replace(Boundary, Line + "\n\n" + Boundary, StringComparison.Ordinal), set);
    }

    /// <summary>
    /// A resumed conversation is told again after the person's words and the go-aheads' answers, since the setting may have
    /// changed since it was handed it; with none set, the appendix is what it was.
    /// </summary>
    [Fact]
    public void A_resumed_conversation_is_told_after_its_appendix_and_only_where_one_is_set()
    {
        Assert.Equal("", SessionLanguageText.Resumed("", null));
        Assert.Equal("The person has also answered…", SessionLanguageText.Resumed("The person has also answered…", null));
        Assert.Equal(Line, SessionLanguageText.Resumed("", Chinese));
        Assert.Equal("The person has also answered…\n\n" + Line, SessionLanguageText.Resumed("The person has also answered…", Chinese));
    }

    /// <summary>The driver hands a start the work's language: its repository's, else its workspace's, else none.</summary>
    [Fact]
    public void A_start_is_handed_its_repositorys_language_else_its_workspaces()
    {
        var config = DriverConfig.Empty.WithLanguage("reports", "zh").WithWorkspaceLanguage("work", "en");
        var quest = new QuestView("q1", "ask #a1", "reports", "Build it", "The report.", "Open");
        SessionTarget Target(string to) => SessionTarget.ForQuest(quest with { To = to }, "C:/work/" + to, "http://stand-in");

        Assert.Equal("zh", Daoris.Driver.Driver.WithLanguage(Target("reports"), config, "work").Language?.Code);
        Assert.Equal(LanguageSource.Workspace, Daoris.Driver.Driver.WithLanguage(Target("checker"), config, "work").Language?.Source);
        Assert.Null(Daoris.Driver.Driver.WithLanguage(Target("checker"), config, "home").Language);
        Assert.Null(Daoris.Driver.Driver.WithLanguage(Target("checker"), DriverConfig.Empty, "work").Language);
    }

    private static SessionTarget Of(string which)
    {
        var bare = TargetPromptGoldenTests.Bare;
        return which switch
        {
            "claim" => bare,
            "resume" => bare with { Answered = new QuestView("q9", "console-ui", "engine", "What does it take?", "The contract.", "Done") { Note = "{ text }" } },
            "carry-on" => bare with { CutOff = "it was cut off." },
            _ => throw new ArgumentOutOfRangeException(nameof(which), which, null),
        };
    }

    private static int Count(string text, string part)
    {
        var count = 0;
        for (var at = text.IndexOf(part, StringComparison.Ordinal); at >= 0; at = text.IndexOf(part, at + part.Length, StringComparison.Ordinal)) count++;
        return count;
    }
}
