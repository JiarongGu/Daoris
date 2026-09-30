using System.Text;

namespace Daoris.Driver;

/// <summary>
/// Daoris's own plugins (PLUG9 d): the install carries its example plugins as offers, named by id on the
/// wire, so the helper can propose installing one before proposing that one be made. Said only where the
/// install offers one this machine has not installed.
/// </summary>
internal sealed class HelpRoomOwnPlugins : IHelpRoomSection
{
    public HelpMachine Describe(HelpMachine machine, HelpMachineSources sources) => machine with
    {
        Offers = [.. sources.Offers.Where(offer => !offer.Installed && offer.Problem is null).Select(offer =>
            new HelpOffer(offer.Id, offer.Manifest.Name, offer.Manifest.Version, offer.Manifest.Hooks?.Points ?? [], offer.Needs)
            {
                Harnesses = [.. offer.Manifest.Harnesses.Select(harness => harness.Name)],
                Servers = [.. offer.Manifest.Servers.Select(server => server.Name)],
            })],
    };

    public string Render(HelpMachine machine)
    {
        if (machine.Offers.Count == 0) return "";

        var text = new StringBuilder();
        text.Append("## Daoris's own plugins\n\n");
        text.Append("This install carries plugins of Daoris's own, none of them installed here. Nothing of one runs until the\n");
        text.Append("person installs it, and one that lands work runs only where a branch rule names it. Before making a\n");
        text.Append("plugin, see whether one of these does the job: propose installing it with `plugin_propose` (`add`, and\n");
        text.Append("`offer` its id, never a path), and say what it needs, which the person sets up themselves.\n\n");
        foreach (var offer in machine.Offers) text.Append($"- {OfferLine(offer)}\n");
        text.Append('\n');
        return text.ToString();
    }

    /// <summary>An offer as the room lists it: its id, name and version, what it speaks on or hands, and what it needs.</summary>
    private static string OfferLine(HelpOffer offer)
    {
        var titled = string.Join(" ", new[] { offer.Name != offer.Id ? offer.Name : "", offer.Version }.Where(part => part.Length > 0));
        var parts = new List<string>();
        if (offer.Points.Count > 0)
        {
            parts.Add("speaks on " + string.Join(", ", offer.Points.Select(point =>
                HelpRoomMakingAPlugin.PointSaid.TryGetValue(point, out var said) ? $"`{point}`, {said}" : $"`{point}`")));
        }

        if (offer.Harnesses.Count > 0) parts.Add($"declares {string.Join(", ", offer.Harnesses.Select(name => $"`{name}`"))}");
        if (offer.Servers.Count > 0) parts.Add($"hands every session {string.Join(", ", offer.Servers.Select(name => $"`{name}`"))}");
        if (offer.Needs.Count > 0) parts.Add($"needs: {string.Join("; ", offer.Needs)}");
        return $"`{offer.Id}`{(titled.Length > 0 ? $" ({titled})" : "")}: {string.Join("; ", parts)}";
    }
}

/// <summary>
/// One of Daoris's own plugins the install offers and this machine has not installed (PLUG9 d), as the room
/// lists it: by id, with what it speaks on and what it needs, so an add can name it — never by a path.
/// </summary>
public sealed record HelpOffer(string Id, string Name, string Version, IReadOnlyList<string> Points, IReadOnlyList<string> Needs)
{
    /// <summary>The harnesses it declares and the servers it hands every session, by name.</summary>
    public IReadOnlyList<string> Harnesses { get; init; } = [];

    public IReadOnlyList<string> Servers { get; init; } = [];
}

public sealed partial record HelpMachine
{
    /// <summary>The install's own plugins not installed here and sound, which an add may name by id (PLUG9 d).</summary>
    public IReadOnlyList<HelpOffer> Offers { get; init; } = [];
}
