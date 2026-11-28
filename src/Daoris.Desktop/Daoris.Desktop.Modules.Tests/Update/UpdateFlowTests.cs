namespace Daoris.Desktop.Modules.Tests;

/// <summary>An update's one state and the table of what may follow what (<see cref="UpdateFlow"/>): each transition has its why, an unlisted one is refused.</summary>
public sealed class UpdateFlowTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 27, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void The_states_are_the_eight_words_the_page_and_the_terminal_read()
    {
        Assert.Equal(
            ["none", "staged", "draining", "deferred", "stopping", "applying", "refused", "installed"], UpdateStates.All);
    }

    /// <summary>Every state may follow every other, except that the launcher is started in `stopping` alone: `applying` follows nothing else.</summary>
    [Fact]
    public void Every_state_may_follow_every_other_but_applying_which_follows_stopping_alone()
    {
        foreach (var from in UpdateStates.All)
        {
            foreach (var to in UpdateStates.All)
            {
                var expected = from != to && (to != UpdateStates.Applying || from == UpdateStates.Stopping);
                Assert.True(expected == UpdateFlow.May(from, to), $"{from} -> {to}");
            }
        }
    }

    [Fact]
    public void A_move_the_table_does_not_list_is_refused_and_changes_nothing()
    {
        var flow = new UpdateFlow();

        Assert.Throws<InvalidOperationException>(() => flow.Move(UpdateStates.Applying, UpdateWhy.Checked, "b1", Now));

        Assert.Equal(UpdateStates.None, flow.State);
        Assert.Null(flow.Last);
    }

    [Fact]
    public void A_move_says_what_it_was_what_it_became_and_why()
    {
        var flow = new UpdateFlow();

        var made = flow.Move(UpdateStates.Draining, UpdateWhy.Seen, "b1", Now);

        Assert.Equal(new UpdateTransition("none", "draining", "seen", Now, "b1"), made);
        Assert.Equal(UpdateStates.Draining, flow.State);
        Assert.Equal(Now, flow.Since);
        Assert.Equal("seen", flow.Why);
        Assert.Equal(made, flow.Last);
    }

    /// <summary>A look that changed nothing says nothing; the same state for another build is a transition (a restage), its since unchanged where the state is.</summary>
    [Fact]
    public void The_same_state_for_the_same_build_is_no_transition_and_for_another_build_is_one()
    {
        var flow = new UpdateFlow();
        flow.Move(UpdateStates.Draining, UpdateWhy.Seen, "b1", Now);

        Assert.Null(flow.Move(UpdateStates.Draining, UpdateWhy.WhenIdle, "b1", Now.AddMinutes(1)));
        Assert.Equal(Now, flow.Since);

        var restaged = flow.Move(UpdateStates.Draining, UpdateWhy.Restaged, "b2", Now.AddMinutes(2));

        Assert.Equal(new UpdateTransition("draining", "draining", "restaged", Now.AddMinutes(2), "b2"), restaged);
        Assert.Equal("b2", flow.Build);
    }

    [Fact]
    public void Every_why_a_transition_says_is_a_word_of_its_own()
    {
        var whys = typeof(UpdateWhy).GetFields().Select(field => (string)field.GetRawConstantValue()!).ToList();

        Assert.Equal(whys.Count, whys.Distinct(StringComparer.Ordinal).Count());
        Assert.All(whys, why => Assert.Matches("^[a-z]+(-[a-z]+)*$", why));
    }
}
