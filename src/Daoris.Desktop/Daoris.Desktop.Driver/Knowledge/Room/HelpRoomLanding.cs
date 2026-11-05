using System.Text;

namespace Daoris.Driver;

/// <summary>
/// What a landing rule may say (HELP1a, HELP8): the words a branch pattern takes, which the first real
/// conversation had to guess, and the plugins a branch rule may name here (D100), which the helper can only
/// name where it sees them.
/// </summary>
internal sealed class HelpRoomLanding : IHelpRoomSection
{
    public HelpMachine Describe(HelpMachine machine, IHelpMachineSources sources) => sources.Describe(this, machine);

    /// <summary>
    /// The plugins a branch rule may name on this machine (HELP8): each one the landing route itself
    /// would take, so the room never lists one a proposal would then be refused for.
    /// </summary>
    internal static IReadOnlyList<string> LandingPluginsOf(IKnowledgePlugins catalog) => catalog.LandingPlugins();

    public string Render(HelpMachine machine)
    {
        var text = new StringBuilder();
        // What `LandingRules` accepts, said here: the first real conversation had to guess it (HELP1a).
        text.Append("A landing pattern may say `{quest}`, `{session}`, `{slug}` (the quest's title, as words) and\n");
        text.Append("`{repository}`. It needs `{quest}` or `{session}`, or every session's work would land on one branch,\n");
        text.Append("and git must take what it comes out as: `feature/{quest}-{slug}` is a pattern that works.\n\n");
        // LAND2a (D145 point 5): the switch is the person's standing say-so for a landing with no press, so the helper
        // proposes it only when they ask for it.
        text.Append("A landing rule may add `--auto-accept`: a quest's done then lands its work with no press, onto a\n");
        text.Append("branch or into its line. It is the person's standing say-so, so propose it only when they ask for it.\n");
        // G8: a pull request is a plugin's performed step in the workflow, never a landing method.
        text.Append("A pull request is a step of a workflow, performed by a plugin after Daoris pushes the branch; a landing\n");
        text.Append("rule never names one.\n");
        var offeredLanders = machine.Offers.Where(offer => offer.Points.Contains(HookPoints.Perform, StringComparer.Ordinal)).Select(offer => $"`{offer.Id}`").ToList();
        text.Append(machine.LandingPlugins.Count > 0
            ? $"Plugins that perform pull-request steps here: {string.Join(", ", machine.LandingPlugins.Select(id => $"`{id}`"))}.\n\n"
            // UX6i2a: Settings → Plugins retired into the Plugins place with UX6j (D150 §2).
            : "No plugin that performs pull-request steps is installed here: the person installs one (`daoris plugin add <folder>`,\n"
              + "or the Plugins place), so never propose a workflow naming one"
              + (offeredLanders.Count > 0
                  ? $" until it is installed; this install offers {string.Join(" and ", offeredLanders)}, which you may propose installing first.\n\n"
                  : ".\n\n"));
        return text.ToString();
    }
}
