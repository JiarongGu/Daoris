using System.Text;

namespace Daoris.Driver;

/// <summary>
/// Where the helper may take the person (HELP6): the places a go may name, listed from the table the driver
/// judges one by (<see cref="HelpPlaces"/>), so the room and the judge cannot disagree.
/// </summary>
internal sealed class HelpRoomPlaces : IHelpRoomSection
{
    public string Render(HelpMachine machine)
    {
        var text = new StringBuilder();
        // HELP6: the places a go may name, from the table the driver judges one by, so the two cannot disagree.
        text.Append("## Where you may take the person\n\n");
        text.Append("`go_propose` opens one of these places and changes nothing; the person does the rest there. Name the\n");
        text.Append("view, for Settings its domain, and a part where the place has one.\n\n");
        static string Listed(IEnumerable<(string Id, string Name)> places) =>
            string.Join(", ", places.Select(place => $"`{place.Id}` ({place.Name})"));
        text.Append($"- Views: {Listed(HelpPlaces.Views)}.\n");
        text.Append($"- Settings domains: {Listed(HelpPlaces.Domains)}.\n");
        foreach (var within in HelpPlaces.Parts.Select(part => part.Within).Distinct())
        {
            var parts = HelpPlaces.Parts.Where(part => part.Within == within).Select(part => (part.Id, part.Name));
            text.Append(within == "start"
                ? $"- Parts of `start`, the setup guide's steps: {Listed(parts)}.\n"
                : $"- Parts of `{within}`: {Listed(parts)}.\n");
        }

        // UX6e2, HELPSETUP1, UX6g2b: a go carries no item, so the helper is told where the parts that need one land; a
        // workspace's part opens the workspace in view, as a door naming its tab or section does (D150 §4.3).
        text.Append("\nA go names no repository, agent or workspace: `setup` opens the Setup of the repository Repositories has chosen,\n");
        text.Append("where the person picks the one they mean, and a part of `agents` opens the agent that has it.\n");
        text.Append("A `workspace-` part opens that tab or section on the page of the workspace in view, the scope or the only one;\n");
        text.Append("with none in view it opens Repositories, where the person picks the workspace they mean.\n");
        // ENTRY1b (D161's ENTRY1 note): a quest or a session keeps its own conversation, so a go takes the person to the
        // group that waits on them there and leaves the one to open to them. ENTRY1f1 (its ENTRY1f note): a go on Quests may
        // name the one, as the room lists it, and the driver judges it against the machine's records. ENTRY1f2: on Sessions
        // too, a session the room lists as waiting or another the person names; Ask Daoris's own conversation is refused.
        text.Append("A part of `sessions` or `quests` brings that group of its list into view and names no session or quest in it:\n");
        text.Append("the person opens the one they mean there. To open one session, quest or ask, name it as the go's item on its view, with no part:\n");
        text.Append("on `sessions` a session by its id, one this room lists as waiting on the person or another the person names,\n");
        text.Append($"never Ask Daoris's own conversation; on `quests` a quest by its id, an ask as `{HelpPlaces.AskItem}<id>`, each one this room lists.\n");
        text.Append("`overview` has no part: what waits on the person leads it.\n");
        // UX6i2a: Knowledge opens in the mode it was left in (UX6i), so a go meaning one names it.
        text.Append("`knowledge` alone opens Knowledge as the person left it; name its part for Search or Convergence.\n");
        text.Append('\n');
        return text.ToString();
    }
}
