using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// KNOWUSE1b (D135 §3): a standing answer the person keeps for a repository on this machine (<c>standing</c> in
/// <c>driver.json</c>) is handed to every session there, beneath its quest: a claim, a resume, a carry-on on any account and
/// a follow-up step alike, and none once it is cleared. The owner's development-first answer was given once, on one quest,
/// and read by no later session (`docs/2026-10-03-knowledge-use-evidence.md` §5.1).
/// </summary>
public sealed class StandingHandedTests
{
    private const string Says = "dev writes allowed; test locally against dev; prod only on a yes.";

    private static readonly DateTimeOffset At = new(2026, 10, 3, 9, 0, 0, TimeSpan.Zero);

    private static readonly DriverConfig Config = DriverConfig.Empty
        .WithStanding("dashboards", Says, At)
        .WithStanding("checker", "verify in the dev browser profile only.", At);

    private static QuestView Quest(string id = "q2", string to = "dashboards", string? parent = null) =>
        new(id, "ask #a1", to, "Verify the figure", "Look at the tile.", "Taken") { Parent = parent };

    /// <summary>The target as the driver composes it for a start: the quest, then this machine's answer for its repository.</summary>
    private static SessionTarget Target(DriverConfig config, QuestView? quest = null) =>
        Daoris.Driver.Driver.WithStanding(SessionTarget.ForQuest(quest ?? Quest(), "C:/somewhere/dashboards", "http://stand-in"), config);

    /// <summary>
    /// The proof: a claim, a resume, a carry-on on another account and a follow-up step are each handed the standing answer,
    /// verbatim, beneath the quest and before the look.
    /// </summary>
    [Fact]
    public void A_claim_a_resume_a_carry_on_and_a_follow_up_are_each_handed_the_standing_answer()
    {
        var question = new QuestView("q9", "dashboards", "engine", "What does it take?", "The contract.", "Done") { Note = "{ text }" };
        var followUp = Quest("q3", "dashboards", parent: "q2");

        foreach (var (which, prompt) in new[]
                 {
                     ("claim", TargetPrompt.Compose(Target(Config))),
                     ("resume", TargetPrompt.Compose(Target(Config) with { Answered = question })),
                     ("carry-on", TargetPrompt.Compose(Target(Config) with { CutOff = "it was cut off.", AccountChanged = true })),
                     ("follow-up", TargetPrompt.Compose(Target(Config, followUp))),
                 })
        {
            var body = prompt.IndexOf("Look at the tile.", StringComparison.Ordinal);
            var standing = prompt.IndexOf("  > " + Says, StringComparison.Ordinal);
            var look = prompt.IndexOf("Look before you ask.", StringComparison.Ordinal);
            Assert.True(body >= 0 && body < standing && standing < look, $"{which}: {prompt}");
            Assert.Contains("What the person has told this machine holds for every quest in `dashboards`", prompt);
            Assert.Contains("set 2026-10-03 09:00 UTC", prompt);
        }
    }

    /// <summary>Each repository's own answer: a follow-up step in another repository is handed that repository's, never its parent's.</summary>
    [Fact]
    public void A_step_in_another_repository_is_handed_that_repositorys_answer()
    {
        var prompt = TargetPrompt.Compose(Target(Config, Quest("q3", "Checker", parent: "q2")));

        Assert.Contains("  > verify in the dev browser profile only.", prompt);
        Assert.DoesNotContain(Says, prompt);
    }

    /// <summary>Gone once removed: a repository with none, and one whose answer was cleared, read exactly as they did.</summary>
    [Fact]
    public void A_repository_with_none_or_one_cleared_is_handed_nothing()
    {
        var cleared = TargetPrompt.Compose(Target(Config.WithStanding("dashboards", null, At)));
        var never = TargetPrompt.Compose(Target(DriverConfig.Empty));

        Assert.DoesNotContain("holds for every quest in", cleared);
        Assert.Equal(never, cleared);
        Assert.Equal(TargetPrompt.Compose(SessionTarget.ForQuest(Quest(), "C:/somewhere/dashboards", "http://stand-in")), never);
    }

    /// <summary>It answers what it covers, and the quest and the person's words on its ask are newer.</summary>
    [Fact]
    public void It_says_it_answers_what_it_covers_and_that_newer_words_win()
    {
        var prompt = TargetPrompt.Compose(Target(Config)).ReplaceLineEndings(" ");

        Assert.Contains("do not ask them for what it already says", prompt);
        Assert.Contains("newer and win where they differ", prompt);
    }

    /// <summary>A time the file does not say is not said; words past the bound a door takes are cut, and said to be.</summary>
    [Fact]
    public void An_unknown_time_is_not_said_and_words_past_the_bound_are_cut_and_said_to_be()
    {
        var unknown = TargetPrompt.Compose(Target(DriverConfig.Parse("""{"standing":{"dashboards":{"says":"dev only"}}}""")));
        var longer = new string('x', StandingText.Limit + 40);
        var cut = TargetPrompt.Compose(Target(DriverConfig.Parse("{\"standing\":{\"dashboards\":{\"says\":\"" + longer + "\"}}}")));

        Assert.Contains("in their own words:", unknown.ReplaceLineEndings(" "));
        Assert.DoesNotContain(" set ", unknown[unknown.IndexOf("What the person has told", StringComparison.Ordinal)..][..200]);
        Assert.Contains("  > " + new string('x', StandingText.Limit) + "\n", cut);
        Assert.Contains("and 40 more characters", cut);
    }
}
