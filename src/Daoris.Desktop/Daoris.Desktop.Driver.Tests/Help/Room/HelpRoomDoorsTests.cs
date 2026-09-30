using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>The room's <see cref="HelpRoomDoors"/>: each change, its screen, and the command that does the same.</summary>
public sealed class HelpRoomDoorsTests
{
    /// <summary>
    /// The doors (D50): every change the room speaks of is a screen and the terminal command that does the
    /// same, spelled as the CLI spells it — a helper that invented a verb would send the person to nothing.
    /// </summary>
    [Fact]
    public void The_room_names_the_screen_and_the_terminal_command_for_each_change()
    {
        var agents = HelpRoom.Render(HelpRoomFixture.Machine);

        foreach (var command in new[]
        {
            "daoris driver drive|undrive <repository>", "daoris driver hold|resume <repository>",
            "daoris driver trees <repository> on|off", "daoris driver line <repository> <branch>|--clear",
            "daoris driver landing <repository> merge|branch <pattern>|--clear", "daoris driver intake <agent>|off",
            "daoris driver helper <agent>|off", "daoris driver strikes <n>", "daoris driver timeout <minutes>",
            "daoris driver notify on|off", "daoris agent rules", "daoris-driver ask --workspace <name>",
            "daoris-driver trees clean",
            // WSR6: bringing a repository up to date after its pull request merged.
            "daoris-driver trees sync [--repository <name>] [--yes]",
            // D107: reading and writing across, the CLI's `driver across` verbs.
            "daoris driver across <repository> read on|off|--clear", "daoris driver across <repository> write-to <other> [--clear]",
            // HELP9: every `daoris driver` verb that changes something, and the doors Ask Daoris owes, named where they are.
            "daoris driver retry <quest>", "daoris driver cap <n>", "daoris driver adapter <agent>",
            "daoris agent profile default <agent> <profile> [--workspace <name>]",
            "daoris browser use daoris|edge", "daoris browser links system|daoris",
        })
        {
            Assert.Contains(command, agents);
        }

        Assert.Contains("Settings → Workspace → Lines", agents);
        // HELP9: a parked quest's Retry is on its drawer, which the room names since Ask Daoris cannot propose it yet.
        Assert.Contains("Quests → the quest's drawer → try it again", agents);
        // What a landing pattern may say, as `LandingRules` reads it — the first real conversation had to guess.
        foreach (var token in new[] { "{quest}", "{session}", "{slug}", "{repository}" }) Assert.Contains(token, agents);
        Assert.Contains("Settings → Daoris's own AI", agents);
        // SETUP1b: the setup guide the person may be walked through, by the names the window gives it.
        Assert.Contains("Settings → Get started (the Daoris menu's *Set up Daoris*)", agents);
        // It reads and advises; the moves that stay the person's are named as never its own.
        Assert.Contains("You change nothing yourself", agents);
        // It proposes (HELP1c): a card the person applies, through the connector's two tools.
        Assert.Contains("`setting_propose`", agents);
        Assert.Contains("`ask_propose`", agents);
        Assert.Contains("push, merge, discard, sign in", agents);
    }
}
