using System.Text;

namespace Daoris.Driver;

/// <summary>
/// How a plugin is made (PLUG9): a plugin runs as the person, so its making is a repository's work, tested
/// and reviewed — proposed as an ask there — and only its installing is a card here. The points come from
/// the wire's own list.
/// </summary>
internal sealed class HelpRoomMakingAPlugin : IHelpRoomSection
{
    /// <summary>What each point a plugin speaks on is for, said where the room tells how a plugin is made (PLUG9).</summary>
    internal static readonly IReadOnlyDictionary<string, string> PointSaid = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [HookPoints.QuestConsider] = "to hold a quest before it starts",
        [HookPoints.SessionEnded] = "to hear how a session ended",
        [HookPoints.Land] = "to push a branch Daoris made and open its pull request",
    };

    public string Render(HelpMachine machine)
    {
        var text = new StringBuilder();
        // PLUG9: a plugin runs as the person, so its making is a repository's work, tested and reviewed,
        // and only its installing is a card here. The points come from the wire's own list.
        text.Append("## Making a plugin\n\n");
        text.Append("A plugin runs on this machine as the person (one that lands work pushes with their sign-in), so making\n");
        text.Append("one is work, not a setting: never write one yourself. Propose it\n");
        text.Append("as an ask (`ask_propose`) at the workspace of the repository that holds plugins, naming it as the receiver:\n");
        text.Append("what the plugin should do, and the point it speaks on ("
            + string.Join("; ", HookPoints.All.Select(point => PointSaid.TryGetValue(point, out var said) ? $"`{point}`, {said}" : $"`{point}`"))
            + ").\n");
        text.Append("The session there makes it with its tests, and the person reviews and lands it. Find that repository\n");
        text.Append("in `registry` by what it says it owns. If none says so,\n");
        text.Append("the person decides where plugins live (a repository of their own, connected like any other), so ask\n");
        text.Append("rather than pick one. Once it has landed, propose adding it with `plugin_propose`, from the folder the\n");
        text.Append("session named; never propose adding one that has not landed.\n\n");
        return text.ToString();
    }
}
