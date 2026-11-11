using System.Text;

namespace Daoris.Driver;

/// <summary>
/// The room's paragraph on the families of acts the person granted in restricted mode (<see cref="ActFamilies"/>): each named by the
/// verbs it covers, pressed with no card as the person's own, and what still holds them.
/// </summary>
internal static class HelpRoomFamilies
{
    /// <summary>The paragraph for <paramref name="granted"/>, in the page's order; one line per family.</summary>
    public static string Granted(IReadOnlyList<string> granted)
    {
        var text = new StringBuilder();
        text.Append("The person granted you these families of acts: a press of one runs at once with no card, as theirs, said on\n");
        text.Append("this conversation's console:\n\n");
        foreach (var family in ActFamilies.Built.Where(granted.Contains)) text.Append($"- {Said(family)}\n");
        text.Append('\n');
        text.Append("The grant is read at each press, so one the person withdraws puts your next press on a card. Each press is\n");
        text.Append("judged by its route as ever: nothing is forced, and a branch a running or waiting session holds is kept. Look\n");
        text.Append("first (`daoris tree clean` with no `--yes` lists what would go and why), say what you will change before you\n");
        text.Append("press it, and what went or changed after.\n\n");
        return text.ToString();
    }

    private static string Said(string family) => family switch
    {
        ActFamilies.CleanUp => "clean-up: `daoris tree clean [--workspace <name>] --yes`, the session and landed branches the clean-up clears, with their trees",
        ActFamilies.Discard => "discard: `daoris tree remove <session|branch> [--repository <name>]`, one session branch with its tree, unforced",
        ActFamilies.Settings => "settings: `daoris settings notify|cap|cooloff|quiet …` and a session language, `daoris place language …`",
        _ => "pause: an ask's or a quest's pause and resume, a repository's hold and its release, and a session's stop",
    };
}
