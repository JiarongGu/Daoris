using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// PERM2b: the connector's `permission_propose` is only useful if a session knows it is there. The
/// composed target named every other connector verb it relies on (take, close, publish) and not this
/// one, so a session refused a command its work genuinely needed had nothing to reach for but a
/// decline — which is honest, and leaves the person no rule to say yes to.
/// </summary>
public sealed class ProposePromptTests
{
    private static SessionTarget Target() => new(
        QuestId: "abc123",
        Title: "Tag the release",
        Body: "Commit the note, then tag it.",
        Asker: "Platform",
        Repository: "Game",
        Root: "C:/somewhere/Game",
        ServiceUrl: "http://localhost:5177");

    /// <summary>
    /// Named, with when to use it and what it does not do: the narrowest rule, with the reason, and a
    /// widening waits for the person — so the session finishes or declines rather than waiting on it.
    /// </summary>
    [Fact]
    public void A_quests_target_says_a_refused_command_may_be_proposed_and_what_that_does_not_do()
    {
        var prompt = TargetPrompt.Compose(Target());

        Assert.Contains("`permission_propose`", prompt);
        Assert.Contains("narrowest", prompt);
        Assert.Contains("reason", prompt);
        Assert.Contains("waits for the person", prompt);
        // Said as the canon speaks — it travels to repositories that know nothing of this one's numbering.
        Assert.DoesNotContain("PERM", prompt);
        Assert.DoesNotContain("D74", prompt);
        // Part of the work, after the claim, and never a reason to skip the close.
        Assert.True(prompt.IndexOf("First take the quest", StringComparison.Ordinal)
            < prompt.IndexOf("permission_propose", StringComparison.Ordinal));
        Assert.Contains("Never write outside", prompt);
    }

    /// <summary>An intake's own words stand: it answers an ask, and its room's rules are not its job to widen.</summary>
    [Fact]
    public void An_intake_is_not_told_about_proposing()
    {
        var prompt = TargetPrompt.Compose(Target() with { Prompt = "Answer the ask." });

        Assert.Equal("Answer the ask.", prompt);
    }
}
