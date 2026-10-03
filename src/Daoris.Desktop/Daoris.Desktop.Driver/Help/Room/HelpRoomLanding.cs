using System.Text;

namespace Daoris.Driver;

/// <summary>
/// What a landing rule may say (HELP1a, HELP8): the words a branch pattern takes, which the first real
/// conversation had to guess, and the plugins a branch rule may name here (D100), which the helper can only
/// name where it sees them.
/// </summary>
internal sealed class HelpRoomLanding : IHelpRoomSection
{
    public HelpMachine Describe(HelpMachine machine, HelpMachineSources sources) =>
        machine with { LandingPlugins = LandingPluginsOf(sources.Plugins) };

    /// <summary>
    /// The plugins a branch rule may name on this machine (HELP8): each one the landing route itself
    /// would take, so the room never lists one a proposal would then be refused for.
    /// </summary>
    internal static IReadOnlyList<string> LandingPluginsOf(PluginCatalog catalog) =>
        [.. catalog.Plugins.Select(plugin => plugin.Manifest.Id)
            .Where(id => LandingRules.PluginProblem(id, catalog) is null)
            .Order(StringComparer.Ordinal)];

    public string Render(HelpMachine machine)
    {
        var text = new StringBuilder();
        // What `LandingRules` accepts, said here: the first real conversation had to guess it (HELP1a).
        text.Append("A landing pattern may say `{quest}`, `{session}`, `{slug}` (the quest's title, as words) and\n");
        text.Append("`{repository}`. It needs `{quest}` or `{session}`, or every session's work would land on one branch,\n");
        text.Append("and git must take what it comes out as: `feature/{quest}-{slug}` is a pattern that works.\n\n");
        // HELP8: a branch rule may name a plugin (D100), and the helper can only name one it can see.
        text.Append("A branch rule may add `--plugin <id>`: once Daoris has made the branch, that plugin pushes it and\n");
        text.Append("opens the pull request, as the person's own platform tools are signed in.\n");
        // LAND2a (D145 point 5): the switch is the person's standing say-so for a push with no press, so the helper
        // proposes it only when they ask for it.
        text.Append("A branch rule may also add `--auto-accept`: a quest's done then lands its work with no press, and the\n");
        text.Append("rule's plugin pushes it and opens a pull request without asking each time. It is the person's standing\n");
        text.Append("say-so for that push, so propose it only when they ask for it, and never on a merge.\n");
        var offeredLanders = machine.Offers.Where(offer => offer.Points.Contains(HookPoints.Land, StringComparer.Ordinal)).Select(offer => $"`{offer.Id}`").ToList();
        text.Append(machine.LandingPlugins.Count > 0
            ? $"Plugins that can land work here: {string.Join(", ", machine.LandingPlugins.Select(id => $"`{id}`"))}.\n\n"
            : "No plugin that lands work is installed here: the person installs one (`daoris plugin add <folder>`,\n"
              + "Settings → Plugins), so never propose a rule naming one"
              + (offeredLanders.Count > 0
                  ? $" until it is installed; this install offers {string.Join(" and ", offeredLanders)}, which you may propose installing first.\n\n"
                  : ".\n\n"));
        return text.ToString();
    }
}

public sealed partial record HelpMachine
{
    /// <summary>The plugins a branch rule may name here, by id (HELP8, D100).</summary>
    public IReadOnlyList<string> LandingPlugins { get; init; } = [];
}
