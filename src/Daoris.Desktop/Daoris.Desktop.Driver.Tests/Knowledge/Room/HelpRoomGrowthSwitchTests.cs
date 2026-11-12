using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// The growth switch in Ask Daoris's room: the room says that the person alone changes it and that no mode lets the agent press it,
/// and lists it among no verb it may press, in either mode.
/// </summary>
public sealed class HelpRoomGrowthSwitchTests
{
    private static string Flat(string text) => System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");

    [Fact]
    public void The_section_says_the_person_alone_changes_the_switch_and_that_it_is_no_verb_of_the_agents_in_any_mode()
    {
        var growth = Flat(new HelpRoomGrowth().Render(HelpRoomFixture.Machine));

        Assert.Contains("The person alone changes the switch that holds opening a place to the access mode, to a card even in full access, or to off", growth);
        Assert.Contains("it is no verb of yours in any mode, and a press to change it is refused.", growth);
        Assert.Contains("Where it is off the route refuses in its own words; where it asks, the card is the person's.", growth);
    }

    [Theory]
    [InlineData(HelpModeNames.Restricted)]
    [InlineData(HelpModeNames.Full)]
    public void No_verb_the_room_lists_presses_the_switch_in_either_mode(string mode)
    {
        var machine = HelpRoomFixture.Machine with { Mode = mode };

        var verbs = Flat(new HelpRoomVerbs().Render(machine));
        var mayDo = Flat(new HelpRoomMayDo().Render(machine));

        Assert.DoesNotContain("settings growth", verbs, StringComparison.Ordinal);
        Assert.DoesNotContain("settings growth", mayDo, StringComparison.Ordinal);
        Assert.DoesNotContain("SET_GROWTH", verbs + mayDo, StringComparison.Ordinal);
    }
}
