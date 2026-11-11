using Daoris.Contracts.Quests;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The person's words a new ask carries (D277): <c>--require</c> beside <c>--to</c> and <c>--through</c> on a new ask, <c>ask through
/// &lt;id&gt;</c> on one already made, and the judgement of an agent's press against the person's own messages in its conversation.
/// </summary>
public sealed class AskThroughArgumentsTests
{
    [Fact]
    public void A_new_ask_to_a_receiver_takes_requirements_and_through_words()
    {
        var read = AskArguments.Read(
            ["--to", "reports", "--require", "merge all changes to remote too", "it reaches its pull request", "--through", "please do without my accept", "ship it"],
            out var problem);

        Assert.Null(problem);
        Assert.Equal(new QuestRequirementWire("merge all changes to remote too", "it reaches its pull request"), Assert.Single(read!.Requirements));
        Assert.Equal("please do without my accept", read.Through);
        Assert.Equal("ship it", read.Sentence);
    }

    [Fact]
    public void A_requirement_on_a_new_ask_with_no_receiver_is_a_problem()
    {
        Assert.Null(AskArguments.Read(["--require", "merge it", "it is merged", "ship it"], out var problem));
        Assert.Contains("--to", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void Ask_through_sets_an_asks_words_or_takes_them_back_and_goes_with_no_other_act()
    {
        var set = AskArguments.Read(["--set-through", "a1", "please", "do", "without", "my", "accept"], out var problem);
        Assert.Null(problem);
        Assert.Equal(("a1", "please do without my accept", false), (set!.SetThrough, set.Sentence, set.ClearWorkflow));

        Assert.True(AskArguments.Read(["--set-through", "a1", "--clear"], out _)!.ClearWorkflow);
        Assert.Null(AskArguments.Read(["--close", "a1", "--through", "please"], out var mixed));
        Assert.Contains("ask through", mixed, StringComparison.Ordinal);
        Assert.Equal("--set-through", AskActs.Words["through"]);
    }

    [Fact]
    public void Ask_daoris_puts_on_an_ask_only_words_the_person_said_in_its_conversation()
    {
        IReadOnlyList<string> Said() => ["We should merge all changes (all repo) to remote too.", "please do\nwithout my ACCEPT"];
        var carried = new AskCarried([new QuestRequirementWire("merge all changes (all repo) to remote too", "it reaches its pull request")], "Please do without my accept");

        Assert.Null(AskQuotes.Refused(carried, TerminalTierBy.AskDaoris, Said));

        var invented = carried with { Requirements = [new QuestRequirementWire("merge to main without review", "it is merged")] };
        var refused = AskQuotes.Refused(invented, TerminalTierBy.AskDaoris, Said);
        Assert.Contains("merge to main without review", refused, StringComparison.Ordinal);
        Assert.Contains("Nothing was asked", refused, StringComparison.Ordinal);
    }

    [Fact]
    public void The_persons_own_press_is_their_words_and_a_working_sessions_own_press_carries_none()
    {
        var carried = new AskCarried([], "please do without my accept");
        IReadOnlyList<string> Never() => throw new InvalidOperationException("the person's press reads no conversation");

        Assert.Null(AskQuotes.Refused(carried, TerminalTierBy.Card, Never));
        Assert.Null(AskQuotes.Refused(carried, TerminalTierBy.Window, Never));
        Assert.Contains("working session", AskQuotes.Refused(carried, TerminalTierBy.Session, Never), StringComparison.Ordinal);
        Assert.Null(AskQuotes.Refused(new AskCarried([], null), TerminalTierBy.Session, Never));
    }

    [Fact]
    public void The_persons_messages_are_read_from_the_conversations_events_alone()
    {
        IReadOnlyList<SessionEvent> events =
        [
            new() { Kind = SessionEventKind.User, Origin = "target", Text = "the brief" },
            new() { Kind = SessionEventKind.User, Origin = "person", Text = "please do without my accept" },
            new() { Kind = SessionEventKind.Message, Text = "I will press it for you" },
        ];

        Assert.Equal(["please do without my accept"], AskQuotes.PersonSaid(events));
    }
}
