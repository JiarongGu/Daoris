using System.Text;

namespace Daoris.Driver;

/// <summary>
/// The room's opening (HELP1a): the mode its conversation runs in, first, so the agent knows it too (HELPMODE1a1, D183 point 3);
/// then whose room it is, what a conversation here is for, and that Daoris writes it.
/// </summary>
internal sealed class HelpRoomIntro : IHelpRoomSection
{
    /// <summary>The mode, from the driver's file as the room is written (<see cref="DriverConfig.HelpMode"/>).</summary>
    public HelpMachine Describe(HelpMachine machine, HelpMachineSources sources) => machine with { Mode = sources.Config.HelpMode };

    public string Render(HelpMachine machine)
    {
        var text = new StringBuilder();
        text.Append("# Ask Daoris\n\n");
        if (machine.Mode == HelpModes.Full)
        {
            // D183 point 5: the doctrine still speaks in full access. Words, not a gate; the mode is the gate.
            text.Append("This conversation runs with full access, as the person chose: you may act on this machine yourself, with\n");
            text.Append("no asking (*What you may do*, below). A repository's own work is still asked for through a quest;\n");
            text.Append("Daoris's own housekeeping, its trees and its home, is yours to do.\n\n");
        }
        else
        {
            text.Append("This conversation runs in restricted mode: you read, advise and propose, and the person applies (*What you\n");
            text.Append("may do*, below).\n\n");
        }

        text.Append("This room is Daoris's, not any repository's. A session here talks with the person about Daoris on\n");
        text.Append("this machine: how to set up a workspace, what a screen means, why a start was refused, how to drive\n");
        text.Append("a repository, write a rule, or start a task. Written by Daoris each time the conversation opens; an\n");
        text.Append("edit here is overwritten.\n\n");
        return text.ToString();
    }
}
