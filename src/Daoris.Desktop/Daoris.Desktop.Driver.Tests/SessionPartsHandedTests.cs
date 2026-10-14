using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// SESSONE1d (D167 point 4 as amended): a further part of a session is handed what the parts before it did, their trees and
/// branches, and where its own tree stands to theirs. The goldens (<see cref="TargetPromptGoldenTests"/>) hold the part gone into
/// a free tree and the part run alongside; these hold a tree grown again after its session's tree went, and the bound.
/// </summary>
public sealed class SessionPartsHandedTests
{
    private static readonly EarlierPart Landed = new("283ef343", "3068f747", "completed") { Branch = "feature/3068f747-schema" };

    [Fact]
    public void A_part_whose_sessions_tree_went_is_told_it_grew_from_the_branch_that_work_landed_on()
    {
        var target = TargetPromptGoldenTests.Bare with
        {
            Parts = new SessionParts([Landed]) { Gone = "283ef343", GrewFrom = "feature/3068f747-schema" },
        };

        var text = TargetPrompt.Compose(target);

        Assert.Contains("Branch `feature/3068f747-schema`.", text);
        Assert.Contains("The tree session `283ef343` worked in is gone, so yours grew from `feature/3068f747-schema`", text);
        Assert.DoesNotContain("You are in session", text);
        var section = Assert.Single(TargetPrompt.Composed(target).Account.Sections, s => s.Name == HandedSections.CarryOn);
        Assert.Equal(HandedSources.Record, section.Source);
        Assert.Contains("grown from `feature/3068f747-schema`", section.Said);
    }

    [Fact]
    public void A_part_whose_sessions_tree_went_with_nothing_landed_is_told_it_grew_from_the_line()
    {
        var target = TargetPromptGoldenTests.Bare with
        {
            Parts = new SessionParts([Landed with { Branch = null }]) { Gone = "283ef343" },
        };

        var text = TargetPrompt.Compose(target);

        Assert.Contains("The tree session `283ef343` worked in is gone, and no branch holds its work here, so yours grew from the line", text);
    }

    [Fact]
    public void Only_the_newest_parts_are_listed_and_the_rest_counted()
    {
        EarlierPart[] parts = [.. Enumerable.Range(1, 5).Select(n => new EarlierPart($"s{n}", $"q{n}", "completed"))];
        var target = TargetPromptGoldenTests.Bare with { Parts = new SessionParts(parts) { HeldBy = "s1" } };

        var text = TargetPrompt.Compose(target);

        Assert.Contains("- Session `s3` (quest `#q3`), completed.", text);
        Assert.DoesNotContain("`s4`", text);
        Assert.Contains("- And 2 earlier sessions.", text);
        var section = Assert.Single(TargetPrompt.Composed(target).Account.Sections, s => s.Name == HandedSections.CarryOn);
        Assert.Equal(TargetPrompt.PartsShown, section.Shown);
        Assert.Equal(5, section.Of);
    }

    /// <summary>A note is said in a line, bounded: the record keeps it whole.</summary>
    [Fact]
    public void An_earlier_parts_note_is_its_first_line_bounded()
    {
        var note = "first line " + new string('x', 300) + "\nsecond line";
        var target = TargetPromptGoldenTests.Bare with
        {
            Parts = new SessionParts([new EarlierPart("s1", "q1", "failed") { Note = note }]) { HeldBy = "s1" },
        };

        var text = TargetPrompt.Compose(target);

        Assert.DoesNotContain("second line", text);
        Assert.Contains("x…", text);
    }

    /// <summary>A start of no session's line, a carry-on and a resume read as they did: the parts are a further part's alone.</summary>
    [Fact]
    public void Only_a_further_parts_first_start_is_handed_its_earlier_parts()
    {
        Assert.DoesNotContain("line of work already under way", TargetPrompt.Compose(TargetPromptGoldenTests.Bare));
        var carried = TargetPromptGoldenTests.CarryOn with { Parts = new SessionParts([Landed]) { HeldBy = "283ef343" } };
        Assert.DoesNotContain("line of work already under way", TargetPrompt.Compose(carried));
    }
}
