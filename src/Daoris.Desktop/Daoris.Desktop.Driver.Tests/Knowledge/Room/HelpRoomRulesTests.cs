using Daoris.Desktop.Driver.Tests;
using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// CONTEXTDRIVE1k (the context counters design §7.6): the room says the supervisor's rules where the home holds some, in the terminal's words and
/// by place, the machine's first; a room built by hand, or a home with none, says nothing of them.
/// </summary>
public sealed class HelpRoomRulesTests
{
    private static readonly HelpMachine Hand = new();

    [Fact]
    public void CONTEXTDRIVE1k_a_room_with_no_rules_says_nothing_of_them()
    {
        Assert.Equal("", new HelpRoomRules().Render(Hand));
    }

    [Fact]
    public void CONTEXTDRIVE1k_each_place_s_rules_are_said_with_the_hold_as_waiting_for_the_person_and_its_door()
    {
        var text = new HelpRoomRules().Render(Hand with
        {
            Rules = ["`machine` (revision 3): answers-alone at 2: hold for every role", "`repository:daoris` (revision 1): answers-alone at 3: hold for every role"],
        });

        Assert.Contains("### The supervisor's rules", text);
        Assert.Contains("- `machine` (revision 3): answers-alone at 2: hold for every role\n- `repository:daoris` (revision 1): answers-alone at 3: hold for every role\n", text);
        Assert.Contains("waits for you", text);
        Assert.Contains("*Go on with this\nanswer*", text);
        Assert.DoesNotContain("strike", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CONTEXTDRIVE1k_the_facts_read_off_a_home_say_the_machine_first_and_a_home_with_no_rules_none()
    {
        var home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "help-room-rules", Guid.NewGuid().ToString("N")[..8]);
        try
        {
            Directory.CreateDirectory(home);
            var lines = HelpRuleFacts.Read(home);

            Assert.All(lines, line => Assert.StartsWith("`", line));
            Assert.Equal(lines.Count == 0 ? 0 : 1, lines.Count(line => line.StartsWith("`machine`", StringComparison.Ordinal)));
        }
        finally
        {
            GitSeed.Discard(home);
        }
    }
}
