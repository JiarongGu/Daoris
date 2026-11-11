using System.Text;

namespace Daoris.Driver;

/// <summary>
/// Where the helper may take the person: the places a go link may name, listed from the driver's twin of the page's table
/// (<see cref="HelpPlaces"/>), so the room and the window that opens the link cannot disagree.
/// </summary>
internal sealed class HelpRoomPlaces : IHelpRoomSection
{
    /// <summary>A go link's address before its fields, as the page's <c>help/goLink.ts</c> reads it.</summary>
    public const string Link = "daoris:go";

    public string Render(HelpMachine machine)
    {
        var text = new StringBuilder();
        text.Append("## Where you may take the person\n\n");
        // A link the window opens, never a card: it asks no yes and changes nothing.
        text.Append($"Take the person to one of these places with a link in your answer, `[the words they read]({Link}?view=<view>&…)`:\n");
        text.Append("pressing it opens the place in Daoris's window and changes nothing; the person does the rest there. Name the\n");
        text.Append("`view`, for Settings its `domain`, and a `part` where the place has one, each as a field of the link, a value's\n");
        text.Append($"space written `%20`: `[Settings → Git]({Link}?view=settings&domain=git)`. A link no place answers opens nothing.\n\n");
        static string Listed(IEnumerable<(string Id, string Name)> places) =>
            string.Join(", ", places.Select(place => $"`{place.Id}` ({place.Name})"));
        text.Append($"- Views: {Listed(KnowledgeHelp.Current.Views)}.\n");
        text.Append($"- Settings domains: {Listed(KnowledgeHelp.Current.Domains)}.\n");
        foreach (var within in KnowledgeHelp.Current.Parts.Select(part => part.Within).Distinct())
        {
            var parts = KnowledgeHelp.Current.Parts.Where(part => part.Within == within).Select(part => (part.Id, part.Name));
            text.Append(within == "start"
                ? $"- Parts of `start`, the setup guide's steps: {Listed(parts)}.\n"
                : $"- Parts of `{within}`: {Listed(parts)}.\n");
        }

        // A go names no repository or agent, so the helper is told where the parts that need one
        // land; a workspace's part opens the workspace in view, as a door naming its tab or section does.
        text.Append("\nA go names no repository or agent: `setup` opens the Setup of the repository Repositories has chosen,\n");
        text.Append("where the person picks the one they mean; `branches` and `history` open its Branches and History tabs, likewise;\n");
        text.Append("and a part of `agents` opens the agent that has it, except `roles`, which opens the Agents place in its Roles mode.\n");
        text.Append("A `workspace-` part opens that tab or section on the page of the workspace in view, the scope or the only one;\n");
        text.Append("with none in view it opens Repositories, where the person picks the workspace they mean.\n");
        // Adding or importing needs a folder, which stays the person's pick, so a
        // go opens the drawer with what Ask Daoris knows filled; the driver refuses a workspace anywhere else, or as a folder.
        text.Append("`add` and `import` may name the go's `workspace`, one this room lists or a new name:\n");
        text.Append("the drawer opens with it filled, and the person picks the folder there and registers it. Never name a folder.\n");
        // New conversation's composer opens with what Ask Daoris knows, the person reading and sending it.
        text.Append("`sessions`'s `new` may name the go's `repository`, as this room's registry lists it with a checkout here, and its `sentence`:\n");
        text.Append("the composer opens with them filled, and the person reads and sends them. With neither it opens empty.\n");
        // A quest or a session keeps its own conversation, so a go takes the person to the
        // group that waits on them there and leaves the one to open to them. A go on Quests may
        // name the one, as the room lists it, and the driver judges it against the machine's records. On Sessions
        // too, a session the room lists as waiting or another the person names; Ask Daoris's own conversation is refused.
        text.Append("A part of `sessions` or `quests` brings that group of its list into view and names no session or quest in it:\n");
        text.Append("the person opens the one they mean there. To open one session, quest, ask or plugin, name it as the go's item on its view, with no part:\n");
        text.Append("on `sessions` a session by its id, one this room lists as waiting on the person or another the person names,\n");
        text.Append($"never Ask Daoris's own conversation; on `quests` a quest by its id, an ask as `{KnowledgeHelp.Current.AskItem}<id>`, each one this room lists;\n");
        text.Append($"on `plugins` an installed plugin by its id, or one of Daoris's own not installed as `{KnowledgeHelp.Current.OfferItem}<id>`, by the ids this room lists.\n");
        text.Append("`overview` has no part: what waits on the person leads it.\n");
        // Knowledge opens in the mode it is left in, so a go meaning one names it.
        text.Append("`knowledge` alone opens Knowledge as the person left it; name its part for Search or Convergence.\n");
        text.Append('\n');
        return text.ToString();
    }
}
