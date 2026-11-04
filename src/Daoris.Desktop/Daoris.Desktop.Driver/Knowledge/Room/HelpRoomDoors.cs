using System.Text;

namespace Daoris.Driver;

/// <summary>
/// The doors (D50): each change the room speaks of, where it is on the screen, and the terminal command that
/// does the same, spelled as the CLI's help spells it — a helper that invented a verb would send the person to
/// nothing.
/// </summary>
/// <remarks>
/// <see cref="Doors"/> is the one registry of rows, in the order a person meets the tasks, not grouped by the
/// feature that added each: a new door is a row where the person would look for it. The rows are kept in
/// <see cref="HelpDoorRowsSetup"/>, <see cref="HelpDoorRowsMachine"/> and <see cref="HelpDoorRowsActs"/>, in that order.
/// </remarks>
internal sealed class HelpRoomDoors : IHelpRoomSection
{
    /// <summary>
    /// What each change is, where it is on the screen, and the command that does the same (D50); a row Ask Daoris answers for says how
    /// as its fourth, never in its words (REVIEW2 T12).
    /// </summary>
    internal static readonly HelpDoor[] Doors = [.. HelpDoorRowsSetup.Rows, .. HelpDoorRowsMachine.Rows, .. HelpDoorRowsActs.Rows];

    public string Render(HelpMachine machine)
    {
        var text = new StringBuilder();
        text.Append("## The doors\n\n");
        text.Append("| To | On the screen | At a terminal |\n");
        text.Append("|---|---|---|\n");
        foreach (var door in Doors) text.Append($"| {door.To} | {door.Screen} | {door.Terminal} |\n");
        text.Append('\n');
        return text.ToString();
    }
}
