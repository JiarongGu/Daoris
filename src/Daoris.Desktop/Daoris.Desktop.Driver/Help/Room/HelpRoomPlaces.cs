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

        // UX6e2, HELPSETUP1: a go carries no item, so the helper is told where the two parts that need one land.
        text.Append("\nA go names no repository and no agent: `setup` opens the Setup of the repository Repositories has chosen,\n");
        text.Append("where the person picks the one they mean, and a part of `agents` opens the agent that has it.\n");
        // UX6i2a: Knowledge opens in the mode it was left in (UX6i), so a go meaning one names it.
        text.Append("`knowledge` alone opens Knowledge as the person left it; name its part for Search or Convergence.\n");
        text.Append('\n');
        return text.ToString();
    }
}
