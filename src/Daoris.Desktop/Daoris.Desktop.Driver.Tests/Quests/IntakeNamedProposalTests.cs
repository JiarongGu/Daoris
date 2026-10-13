using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// ASKNAME1b: the declarations tier proposes first a repository the ask's sentence names (ASKNAME1), with that name alone
/// as its evidence. The intake's instruction said every proposal was <i>by words alone</i>; a named one now says the ask
/// names it, and the rest keep their words. Runs no process, so it is in the fast half (MOD8).
/// </summary>
public sealed class IntakeNamedProposalTests
{
    private const string Sentence = "In storefront: the cart total rounds wrong on the reports page";

    private static AskView Ask(IReadOnlyList<string> proposed, IReadOnlyList<string> named) =>
        new("a1b2c3", "work", Sentence, "Proposed", "declarations") { Proposed = proposed, Named = named };

    [Fact]
    public void A_named_proposal_is_said_as_named_and_a_word_match_keeps_its_words()
    {
        var prompt = IntakePrompt.Compose(Ask(["storefront", "reports"], ["storefront"]));

        Assert.Contains(
            "tool answers the declarations, live. The ask names `storefront`; a name is proposed first, but is not a "
            + "decision: a sentence can name a repository it only mentions. By words alone the declarations proposed "
            + "`reports` — a word match, not a decision.\n\n",
            prompt);
    }

    [Fact]
    public void Only_named_proposals_say_nothing_of_words()
    {
        var prompt = IntakePrompt.Compose(Ask(["storefront", "checker"], ["storefront", "checker"]));

        Assert.Contains("The ask names `storefront`, `checker`; a name is proposed first", prompt);
        Assert.DoesNotContain("By words alone", prompt);
    }

    [Fact]
    public void Word_matches_alone_read_as_they_did()
    {
        var prompt = IntakePrompt.Compose(Ask(["reports", "checker"], []));

        Assert.Contains(
            "live. By words alone the declarations proposed `reports`, `checker` — a word match, not a decision.\n\n", prompt);
        Assert.DoesNotContain("The ask names", prompt);
    }

    /// <summary>
    /// A proposal is named when its evidence is exactly its repository's name, without case, as the tier finds the name; a
    /// one-word match that is some other word is a word match, and so is a name among other words.
    /// </summary>
    [Fact]
    public void The_reader_takes_a_proposal_whose_evidence_is_its_own_name_as_named()
    {
        var ask = ServiceClient.ReadAskJson(
            """
            {"id":"a1","workspace":"work","sentence":"s","state":"Proposed","tier":"declarations",
             "proposal":[{"repository":"storefront","score":7,"matched":["storefront"]},
                         {"repository":"Checker","score":6,"matched":["checker"]},
                         {"repository":"reports","score":2,"matched":["report"]},
                         {"repository":"cart","score":2,"matched":["cart","total"]},
                         {"repository":"ledger","score":1}]}
            """);

        Assert.Equal(["storefront", "Checker", "reports", "cart", "ledger"], ask.Proposed);
        Assert.Equal(["storefront", "Checker"], ask.Named);
    }

    [Fact]
    public void An_ask_with_no_proposal_names_none()
    {
        var ask = ServiceClient.ReadAskJson("""{"id":"a1","workspace":"w","sentence":"s","state":"Open","tier":"named"}""");

        Assert.Empty(ask.Proposed);
        Assert.Empty(ask.Named);
    }
}
