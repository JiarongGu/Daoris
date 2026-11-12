using System.Text;

namespace Daoris.Driver;

/// <summary>
/// What an Ask Daoris conversation is told of its focus in the standing block of its first words: the room is shared by every
/// conversation of its home workspace, so its files name no focus, and each reads its own here: the focus, where its tools start, and for
/// a repository that repository's own facts gathered (<see cref="HelpFocusGathered"/>). A go-on goes on in the same conversation, told only
/// a change (<see cref="HelpFocusChange"/>). Agent-facing, in English, never translated.
/// </summary>
public static class HelpFocusTold
{
    private const string Reach = "The focus limits nothing: name a workspace, or `all`, to look beyond it.";

    /// <summary>The words a new run is handed with its first message.</summary>
    /// <param name="whole">The machine's facts as read for its start; null where they were not read, which tells the focus by name alone.</param>
    /// <param name="home">The conversation's home workspace, whose room it runs in; null is <c>default</c>'s.</param>
    public static string Words(HelpMachine? whole, string? home, HelpFocus focus)
    {
        var text = new StringBuilder(Focused(focus));
        var scope = KnowledgeHelp.Current.ScopeOf(home);
        var at = KnowledgeHelp.Current.ScopeOf(focus.Workspace);
        if (!string.Equals(scope, at, StringComparison.OrdinalIgnoreCase))
        {
            var room = scope is null ? "the whole machine's" : $"workspace `{scope}`'s";
            text.Append(at is null
                ? $"This conversation is {room}, and its focus is every workspace: `daoris-knowledge` answers for every workspace unless a call names one,\nwhile the room lists {(scope is null ? "the whole machine" : $"`{scope}`'s")}.\n\n"
                : $"This conversation is {room}, and its focus is in workspace `{at}`. `daoris-knowledge` answers for `{at}` unless a call names another workspace,\nor `all`, while the room lists {(scope is null ? "the whole machine" : $"`{scope}`'s")}.\n\n");
        }

        if (whole is not null && focus.Kind == HelpFocus.RepositoryKind) text.Append(HelpFocusGathered.Render(whole.Within(home, focus)));
        return text.ToString().TrimEnd() + "\n";
    }

    /// <summary>What the conversation is about, the person's choice, and that it limits nothing.</summary>
    private static string Focused(HelpFocus focus) => focus.Kind switch
    {
        HelpFocus.MachineKind => "Its focus is this machine: every workspace. The person chose what this conversation is about, and may\n"
            + "choose a workspace or a repository instead; a change reaches you in one line ahead of their words.\n\n",
        HelpFocus.WorkspaceKind => $"Its focus is {focus.Named}: what the person chose this conversation to be about, and where its\n"
            + $"tools start. {Reach}\n\n",
        _ => $"Its focus is {focus.Named}: what the person chose this conversation to be about.\n"
            + $"Its tools start in workspace `{focus.Workspace}`. {Reach}\n\n",
    };
}
