using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>What a done leaves by its own record: the requirements it departed from, accepted or not, and nothing for any other quest.</summary>
public sealed class QuestLeftTests
{
    private static readonly QuestView Quest = new("q1", "game", "engine", "Title", "Body", "Done")
    {
        Answers = [new(2, null, "held", "their words"), new(1, "met", null, null), new(3, null, "  ", "their words")],
    };

    [Fact]
    public void A_done_leaves_the_requirements_it_departed_from_in_their_order_with_or_without_the_persons_yes()
    {
        Assert.Equal([2], QuestLeft.Departed(Quest).Select(answer => answer.Requirement));
        Assert.Equal([2], QuestLeft.Departed(Quest with { Held = true }).Select(answer => answer.Requirement));
        Assert.True(QuestLeft.Has(Quest));
    }

    [Fact]
    public void A_departure_waits_on_the_person_until_they_accept_it_and_is_their_word_after()
    {
        Assert.True(QuestLeft.Waits(Quest with { Held = true }));
        Assert.False(QuestLeft.Accepted(Quest with { Held = true }));
        Assert.True(QuestLeft.Accepted(Quest));
        Assert.False(QuestLeft.Waits(Quest));
        Assert.False(QuestLeft.Accepted(Quest with { Answers = [] }));
    }

    [Theory]
    [InlineData("Taken")]
    [InlineData("Open")]
    [InlineData("Declined")]
    public void A_quest_that_is_not_done_leaves_nothing(string status) =>
        Assert.False(QuestLeft.Has(Quest with { Status = status }));

    [Fact]
    public void A_done_answering_every_requirement_met_or_none_at_all_leaves_nothing()
    {
        Assert.False(QuestLeft.Has(Quest with { Answers = [new(1, "met", null, null)] }));
        Assert.False(QuestLeft.Has(Quest with { Answers = [] }));
        Assert.False(QuestLeft.Has(null));
    }
}
