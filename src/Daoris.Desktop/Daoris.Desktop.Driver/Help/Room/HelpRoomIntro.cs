using System.Text;

namespace Daoris.Driver;

/// <summary>The room's opening (HELP1a): whose room it is, what a conversation here is for, and that Daoris writes it.</summary>
internal sealed class HelpRoomIntro : IHelpRoomSection
{
    public string Render(HelpMachine machine)
    {
        var text = new StringBuilder();
        text.Append("# Ask Daoris\n\n");
        text.Append("This room is Daoris's, not any repository's. A session here talks with the person about Daoris on\n");
        text.Append("this machine: how to set up a workspace, what a screen means, why a start was refused, how to drive\n");
        text.Append("a repository, write a rule, or start a task. Written by Daoris each time the conversation opens; an\n");
        text.Append("edit here is overwritten.\n\n");
        return text.ToString();
    }
}
