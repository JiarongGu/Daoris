using System.Text;

namespace Daoris.Driver;

/// <summary>
/// The room's opening: the mode its conversation runs in, first, so the agent knows it too;
/// then whose room it is, what a conversation here is for, and that Daoris writes it. A workspace's room
/// says next which of what it lists is the workspace's and which the machine's.
/// </summary>
internal sealed class HelpRoomIntro : IHelpRoomSection
{
    /// <summary>The mode, from the driver's file as the room is written (<see cref="DriverConfig.HelpMode"/>).</summary>
    public HelpMachine Describe(HelpMachine machine, IHelpMachineSources sources) => sources.Describe(this, machine);

    public string Render(HelpMachine machine)
    {
        var text = new StringBuilder();
        text.Append("# Ask Daoris\n\n");
        if (machine.Mode == HelpModeNames.Full)
        {
            // The doctrine still speaks in full access. Words, not a gate; the mode is the gate.
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
        text.Append(Focused(machine.Focus ?? HelpFocus.Home(machine.Home)));
        if (machine.Workspace is { } workspace)
        {
            text.Append(Scope(workspace, machine.Home)).Append(Bound(workspace, machine.Mode == HelpModeNames.Full));
        }

        return text.ToString();
    }

    /// <summary>
    /// What the conversation is about, the person's choice, and that it limits nothing, in the words of the line a change is told in;
    /// a repository's own facts are gathered first, under <i>In focus</i> (<see cref="HelpRoomInFocus"/>).
    /// </summary>
    private static string Focused(HelpFocus focus)
    {
        const string Reach = "The focus limits nothing: name a workspace, or `all`, to look beyond it.";
        return focus.Kind switch
        {
            HelpFocus.MachineKind => "Its focus is this machine: every workspace. The person chose what this conversation is about, and may\n"
                + "choose a workspace or a repository instead; a change reaches you in one line ahead of their words.\n\n",
            HelpFocus.WorkspaceKind => $"Its focus is {focus.Named}: what the person chose this conversation to be about, and where its\n"
                + $"tools start. {Reach}\n\n",
            _ => $"Its focus is {focus.Named}: what the person chose this conversation to be about.\n"
                + $"*In focus*, below, gathers what this room holds of `{focus.Repository}`; the rest is workspace `{focus.Workspace}`'s, where its\n"
                + $"tools start. {Reach}\n\n",
        };
    }

    /// <summary>
    /// What its workspace is to its tools: where they start, never a fence. Restricted: its connector answers for its workspace
    /// unless it names another, or all, and it may read every workspace's checkouts and propose into any. Full access (the trap is
    /// a workspace's label read as confinement): its shell reaches the whole machine, so the workspace is said to be no fence.
    /// Words, not a gate; the mode is the gate.
    /// </summary>
    private static string Bound(string workspace, bool full)
    {
        var text = new StringBuilder();
        if (full)
        {
            text.Append($"Full access is not confined to `{workspace}`: your shell and file edits reach this whole machine, other workspaces'\n");
            text.Append($"checkouts and records among them. `{workspace}` is where this conversation belongs and starts, not a fence around it.\n");
            text.Append($"`daoris-knowledge` answers for `{workspace}` unless a call names another workspace, or `all`, and a proposal may name\n");
            text.Append("another workspace's repository.\n\n");
            return text.ToString();
        }

        text.Append($"`daoris-knowledge` answers for `{workspace}` unless a call names another workspace, or `all`: its registry, its search,\n");
        text.Append($"its reads and its quests are `{workspace}`'s by default. The checkouts you may read are every workspace's, `{workspace}`'s\n");
        text.Append("first, and a proposal may name another workspace or one of its repositories.\n\n");
        return text.ToString();
    }

    /// <summary>
    /// What a workspace's room lists is that workspace's, every count its own, and what the machine holds for every
    /// workspace is said as the machine's; asked about another workspace, the helper says where that is read.
    /// </summary>
    /// <param name="home">The workspace whose room this is, the conversation's home; null for the whole machine's.</param>
    private static string Scope(string workspace, string? home)
    {
        var own = string.Equals(home, workspace, StringComparison.OrdinalIgnoreCase);
        var text = new StringBuilder();
        text.Append(own
            ? $"This conversation is workspace `{workspace}`'s: the person started it from a window on that workspace.\n"
            : $"This conversation is {(home is null ? "the whole machine's" : $"workspace `{home}`'s")}, and its focus is in workspace `{workspace}`.\n");
        text.Append("The repositories and their lines, the sessions, quests and asks, the finished work and where work has\n");
        text.Append($"got to that this room lists are `{workspace}`'s alone, and each count here counts `{workspace}`'s; another\n");
        text.Append("workspace's are not listed. The agents and their accounts, sign-in and usage, the plugins and what the\n");
        text.Append("person approved of them, Daoris's browser, the driver and its cap on sessions at once, and the roles'\n");
        text.Append(own
            ? $"starts and moves of the last 7 days, are this machine's, shared by every workspace. This conversation is `{workspace}`'s and\nstarts there"
            : $"starts and moves of the last 7 days, are this machine's, shared by every workspace. Its tools start in `{workspace}`");
        text.Append(": it reads another workspace by naming it, or every workspace with `all`, and may propose into any.\n\n");
        return text.ToString();
    }
}
