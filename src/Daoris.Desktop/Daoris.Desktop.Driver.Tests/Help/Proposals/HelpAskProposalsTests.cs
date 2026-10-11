using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>Ask Daoris's <c>ask</c> proposal (HELP1c): something to start, made through the ask door.</summary>
public sealed class HelpAskProposalsTests : HelpProposalsFixture
{
    [Fact]
    public void An_ask_is_planned_as_the_ask_door_and_its_terminal_twin()
    {
        var ask = new HelpProposal("p2", "ask", "ask", null, "work", null, "fix the cold-cache stall", "why", "h1", "proposed");

        var plan = HelpProposals.Plan(ask, DriverConfig.Empty, Facts);

        Assert.Null(plan.Refusal);
        Assert.Equal("daoris-driver ask --workspace work \"fix the cold-cache stall\"", plan.Terminal);
        Assert.Contains("fix the cold-cache stall", plan.Describe);
        Assert.DoesNotContain("review", plan.Describe);
        Assert.Null(plan.Apply); // an ask is made through the ask door, not an edit to the file
        Assert.Contains("no workspace `nope`", HelpProposals.Plan(ask with { Workspace = "nope" }, DriverConfig.Empty, Facts).Refusal);
    }

    /// <summary>
    /// ENTRY1c: the review choice the person said goes with the ask, as the composer sends it and the terminal's
    /// <c>--review rule|on|&lt;environment&gt;|off [--review-words "…"]</c> takes it; <c>rule</c> sends none. The rows are the
    /// service box's (<c>HelpAskProposalTests</c>) and <c>AskReviewCommandTests</c>'s, so what the box writes the driver takes.
    /// </summary>
    [Theory]
    [InlineData(null, null, "", "")]
    [InlineData("rule", null, "", "")]
    [InlineData("on", null, "--review on ", " — review it in the default environment before it lands")]
    [InlineData("off", "a readme change", "--review off --review-words \"a readme change\" ", " — no review before it lands: “a readme change”")]
    [InlineData("dev", "it touches the page", "--review dev --review-words \"it touches the page\" ",
        " — review it in `dev` before it lands: “it touches the page”")]
    public void An_asks_review_choice_is_planned_as_the_composer_sends_it(string? review, string? words, string flags, string said)
    {
        var ask = new HelpProposal("p3", "ask", "ask", null, "work", null, "add the compare setting", "why", "h1", "proposed")
        {
            Review = review,
            ReviewWords = words,
        };

        var plan = HelpProposals.Plan(ask, DriverConfig.Empty, Facts);

        Assert.Null(plan.Refusal);
        Assert.Equal($"daoris-driver ask --workspace work {flags}\"add the compare setting\"", plan.Terminal);
        Assert.Equal($"Ask at workspace `work`: “add the compare setting”{said}", plan.Describe);
    }

    /// <summary>
    /// ENTRY1c: a choice the composer would not send is refused in its words (<see cref="AskReviewCommand.Compose"/>), the rule's
    /// own judgement of a name among them. Whether a name is one the ask's workspace declares is the ask door's, at Apply.
    /// </summary>
    [Theory]
    [InlineData("Dev", null, "`Dev`")]
    [InlineData("prod", null, "`prod`")]
    [InlineData("none", null, "`none`")]
    [InlineData(null, "a readme change", "--review-words")]
    [InlineData("rule", "a readme change", "--review-words")]
    public void A_review_choice_the_composer_would_not_send_is_refused(string? review, string? words, string named)
    {
        var ask = new HelpProposal("p4", "ask", "ask", null, "work", null, "add the compare setting", "why", "h1", "proposed")
        {
            Review = review,
            ReviewWords = words,
        };

        Assert.Contains(named, HelpProposals.Plan(ask, DriverConfig.Empty, Facts).Refusal);
    }

    /// <summary>ENTRY1c: the choice and its words are read from the file as the service's box writes them.</summary>
    [Fact]
    public void An_asks_review_choice_is_read_from_the_file()
    {
        var node = new JsonObject
        {
            ["id"] = "p5", ["proposed"] = "2026-10-11T10:00:00.0000000+00:00", ["by"] = new JsonObject { ["session"] = "h1" },
            ["kind"] = "ask", ["door"] = "ask", ["target"] = null, ["workspace"] = "work", ["value"] = null,
            ["sentence"] = "add the compare setting", ["review"] = "dev", ["reviewWords"] = "it touches the page",
            ["why"] = "the person asked", ["state"] = "proposed", ["note"] = null,
        };
        System.IO.File.WriteAllText(Path.Combine(HelpProposals.FolderOf(_home), "p5.json"), node.ToJsonString());

        var ask = HelpProposals.Find(_home, "p5")!;

        Assert.Equal(("dev", "it touches the page"), (ask.Review, ask.ReviewWords));
    }

    [Fact]
    public async Task An_ask_is_applied_through_the_ask_door()
    {
        var (ask, asked, _) = await ApplyAsync(new HelpProposal("p7", "ask", "ask", null, "work", null, "start it", "why", "h1", "proposed"));

        Assert.Equal(["ask work start it"], asked.Calls);
        Assert.Contains("Applied", ask.Told);
    }

    /// <summary>HEAD2e: the short title is read from the file, said on the card, and carried to the ask door at Apply.</summary>
    [Fact]
    public async Task An_asks_short_title_is_read_said_and_applied_through_the_ask_door()
    {
        var node = new JsonObject
        {
            ["id"] = "p9", ["proposed"] = "2026-10-12T10:00:00.0000000+00:00", ["by"] = new JsonObject { ["session"] = "h1" },
            ["kind"] = "ask", ["door"] = "ask", ["target"] = null, ["workspace"] = "work", ["value"] = null,
            ["sentence"] = "start it", ["short"] = "Cold-cache stall", ["review"] = null, ["reviewWords"] = null,
            ["why"] = "the person asked", ["state"] = "proposed", ["note"] = null,
        };
        System.IO.File.WriteAllText(Path.Combine(HelpProposals.FolderOf(_home), "p9.json"), node.ToJsonString());
        var read = HelpProposals.Find(_home, "p9")!;

        Assert.Equal("Cold-cache stall", read.ShortTitle);
        Assert.Contains("short title “Cold-cache stall”", HelpProposals.Plan(read, DriverConfig.Empty, Facts).Describe);

        var (ask, asked, _) = await ApplyAsync(read with { Id = "p10" });

        Assert.Equal(["ask work start it [short Cold-cache stall]"], asked.Calls);
        Assert.Contains("Applied", ask.Told);
    }

    /// <summary>HEAD2e2: the card's terminal line says the short title as the terminal takes it, after the review flags, quoted for any shell.</summary>
    [Theory]
    [InlineData(null, null, "")]
    [InlineData("  ", null, "")]
    [InlineData("Cold-cache stall", null, " --short \"Cold-cache stall\"")]
    [InlineData("Stall", null, " --short Stall")]
    [InlineData("Say \"hi\"", "dev", " --short <title>")]
    public void An_asks_short_title_is_said_on_the_terminal_line(string? title, string? review, string said)
    {
        var ask = new HelpProposal("p11", "ask", "ask", null, "work", null, "add the compare setting", "why", "h1", "proposed")
        {
            Review = review,
            ShortTitle = title,
        };

        var flags = review is null ? "" : "--review dev ";
        Assert.Equal($"daoris-driver ask --workspace work {flags}\"add the compare setting\"{said}", HelpProposals.Plan(ask, DriverConfig.Empty, Facts).Terminal);
    }

    /// <summary>ENTRY1c: the ask door hears the choice and the words as the composer sends them.</summary>
    [Fact]
    public async Task An_asks_review_choice_is_applied_through_the_ask_door()
    {
        var (ask, asked, _) = await ApplyAsync(new HelpProposal("p8", "ask", "ask", null, "work", null, "start it", "why", "h1", "proposed")
        {
            Review = "off",
            ReviewWords = "  a readme change ",
        });

        Assert.Equal(["ask work start it [review off: a readme change]"], asked.Calls);
        Assert.Contains("--review off --review-words \"a readme change\"", ask.Told);
    }
}

public sealed partial class HelpStandInDoors
{
    public Task<AskAnswer> AskAsync(string workspace, string sentence, string? review, string? reviewWords, CancellationToken ct, string? shortTitle = null)
    {
        var chosen = review is null ? "" : $" [review {review}{(reviewWords is null ? "" : $": {reviewWords}")}]";
        Calls.Add($"ask {workspace} {sentence}{chosen}{(shortTitle is null ? "" : $" [short {shortTitle}]")}");
        return Task.FromResult(new AskAnswer(true, "Asked.", "a1", null));
    }
}
