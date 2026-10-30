namespace Daoris.Driver;

/// <summary>
/// The gates a repository's accepted integration declaration names, as a door read them (INTEGRATE1r part r2; the integration design
/// §17.7's choice): <see cref="Names"/> where it was read (none where nothing is accepted for the line's tip, or the declaration names no
/// gate), else <see cref="Unread"/>, why it could not be. Handed to the pure check, which reads no git.
/// </summary>
/// <param name="Names">The declaration's gate names; null where it could not be read.</param>
/// <param name="Unread">Why it could not be read; null where it was.</param>
public sealed record AcceptedGates(IReadOnlyList<string>? Names, string? Unread = null)
{
    /// <summary>A reading that failed: nothing is passed on a guess.</summary>
    public static AcceptedGates NotRead(string why) => new(null, why);
}

/// <summary>
/// Whether a repository's declarations can carry a graph version's <c>integrate</c> and its plugin's nodes (INTEGRATE1r part r2; the
/// integration design §17.7 and §17.4; D157 §2.3: a step chooses among what is declared, never beyond it). Pure: a version that cannot is
/// refused where it is chosen and its gate holds it as <i>cannot start</i>, each case its own sentence naming the Setup row that settles it.
/// </summary>
public static class IntegrateCarry
{
    private const string LandingRow = "Repositories → the repository's page → Setup → Line and landing → How work lands";

    private const string DeclarationRow = "Repositories → the repository's page → Setup → Line and landing → Integration declaration";

    /// <summary>
    /// Why the repository's declarations cannot carry the graph's <c>integrate</c>, <c>pushed</c> or <c>pull-request</c>, one sentence each;
    /// empty where they can, and for a graph holding none of them or no landing.
    /// </summary>
    /// <param name="rule">The repository's own landing rule: its <c>integrate</c> is the line's declaration (§17.7).</param>
    /// <param name="landing">The landing the version makes of it (<see cref="WorkflowProcess.Of"/>): its form, pattern, plugin and press.</param>
    /// <param name="accepted">What the door read of the accepted declaration; null where it did not ask, so that fact is not judged here.</param>
    public static IReadOnlyList<string> Cannots(string repository, NodeGraph graph, LandingRule rule, LandingRule landing, AcceptedGates? accepted)
    {
        var integrate = graph.Nodes.FirstOrDefault(node => node.Kind == NodeKinds.Integrate);
        var pushes = graph.Nodes.Where(node => node.Kind is NodeKinds.Pushed or NodeKinds.PullRequest).ToList();
        if (graph.Nodes.FirstOrDefault(node => node.Kind == NodeKinds.Handed) is not { } land || (integrate is null && pushes.Count == 0)) return [];

        var said = new List<string>();
        if (integrate is not null)
        {
            var request = IntegrateNodes.RequestOf(integrate);
            var step = $"its `{integrate.Id}` (`{NodeKinds.Integrate}`)";
            if (landing.Form == LandingForm.Merge || landing.Pattern is null)
            {
                said.Add($"{step} integrates the branch the landing `{land.Id}` makes, and neither the step nor `{repository}`'s landing rule names a pattern "
                    + $"for one (a merge rule names none): `daoris driver landing {repository} branch <pattern>` declares it ({LandingRow}).");
            }

            if (landing.Plugin is { } plugin)
            {
                var whose = !string.IsNullOrEmpty(NodeOptions.Text(land, "plugin")) ? "the step names" : $"`{repository}`'s landing rule names";
                said.Add($"{step} integrates a branch no plugin lands, and the landing `{land.Id}` is landed by the plugin `{plugin}`, which {whose}: that "
                    + $"repository's platform integrates its work, so it chooses a pull-request version, or its landing rule drops the plugin ({LandingRow}).");
            }

            if (request is LandingIntegrate.Done or LandingIntegrate.Steward)
            {
                if (rule.Integrate?.Gates != LandingIntegrate.Declared)
                {
                    said.Add($"{step} asks with `{request}`, which needs the line's gates, and `{repository}`'s landing rule does not set `integrate.gates` to "
                        + $"`declared` (an accepted declaration alone is not gates turned on): it integrates with no gates, so the request is the owner's ({LandingRow}).");
                }

                if (accepted?.Unread is { } why)
                {
                    said.Add($"{step} asks with `{request}`, and whether the declaration the owner accepted names a gate could not be read ({why.TrimEnd('.')}): "
                        + $"nothing is passed on a guess ({DeclarationRow}).");
                }
                else if (accepted is { Names.Count: 0 })
                {
                    said.Add($"{step} asks with `{request}`, which needs the line's gates, and no declaration the owner accepted names a gate for `{repository}`'s "
                        + $"line: the declaration is accepted at its line's tip ({DeclarationRow}).");
                }
            }

            if (request == LandingIntegrate.Done && !landing.AutoAccept)
            {
                said.Add($"{step} asks with `done`, which a landing accepted automatically starts, and the landing `{land.Id}` accepts on your press (its `accept` is "
                    + $"`you`, and `{repository}`'s landing rule's *Accept automatically* is off): the version says `accept: automatic`, or the rule's switch is on ({LandingRow}).");
            }
        }

        if (landing.Plugin is null)
        {
            foreach (var push in pushes)
            {
                said.Add($"its `{push.Id}` (`{push.Kind}`) pushes the landing's branch through a plugin, and neither the landing `{land.Id}` nor `{repository}`'s "
                    + $"landing rule names one: `daoris driver landing {repository} branch <pattern> --plugin <id>` declares one ({LandingRow}).");
            }
        }

        return said;
    }

    /// <summary>Whether the line integrates with no gates under the rule: its <c>integrate.gates</c> is absent or <c>none</c> (§17.7, F42).</summary>
    public static bool NoGates(LandingRule rule) => rule.Integrate?.Gates != LandingIntegrate.Declared;

    /// <summary>
    /// What a door says of a repository a choice reaches whose line integrates with no gates (§17.7, F42): the person's Accept moves the
    /// line, as today's merge rule lands on a press, and a version that asks with the unit's done is refused there.
    /// </summary>
    public static string NoGatesSaid(string repository) =>
        $"`{repository}` integrates with no gates: its landing rule declares none, so the line moves on your Accept as a merge does today, and a version with no press is refused there.";
}
