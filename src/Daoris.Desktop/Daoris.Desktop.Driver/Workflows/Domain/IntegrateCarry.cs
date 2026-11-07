namespace Daoris.Driver;

/// <summary>
/// The gates a repository's accepted integration declaration names, as a door reads them: <see cref="Names"/> where it reads (none where nothing is accepted for the line's tip, or the declaration names no
/// gate), else <see cref="Unread"/>, why it could not be. Handed to the pure check, which reads no git.
/// </summary>
/// <param name="Names">The declaration's gate names; null where it could not be read.</param>
/// <param name="Unread">Why it could not be read; null where it reads.</param>
public sealed record AcceptedGates(IReadOnlyList<string>? Names, string? Unread = null)
{
    /// <summary>A reading that failed: nothing is passed on a guess.</summary>
    public static AcceptedGates NotRead(string why) => new(null, why);
}

/// <summary>
/// Whether a repository's declarations can carry a graph version's <c>integrate</c> and its plugin's nodes (a step chooses among what is declared, never beyond it). Pure: a version that cannot is
/// refused where it is chosen and its gate holds it as <i>cannot start</i>, each case its own sentence naming the Setup row that settles it.
/// </summary>
public static class IntegrateCarry
{
    private const string DeclarationRow = "Repositories → the repository's page → Setup → Line and landing → Integration declaration";

    /// <summary>
    /// Why the repository's declarations cannot carry the graph's <c>integrate</c>, <c>pushed</c> or <c>pull-request</c>, one sentence each;
    /// empty where they can, and for a graph holding none of them or no landing.
    /// </summary>
    /// <param name="policy">The line's policy (<see cref="DriverConfig.LinePolicyOf"/>): its gates are the line's declaration.</param>
    /// <param name="landing">The landing the version makes (<see cref="WorkflowProcess.Of"/>): its form, pattern, plugin and press.</param>
    /// <param name="accepted">What the door read of the accepted declaration; null where it did not ask, so that fact is not judged here.</param>
    public static IReadOnlyList<string> Cannots(string repository, NodeGraph graph, LandingIntegrate? policy, Landing landing, AcceptedGates? accepted)
    {
        var integrate = graph.Nodes.FirstOrDefault(node => node.Kind == NodeKinds.Integrate);
        if (graph.Nodes.FirstOrDefault(node => node.Kind == NodeKinds.Land) is not { } land || integrate is null) return [];

        var said = new List<string>();
        if (integrate is not null)
        {
            var request = IntegrateNodes.RequestOf(integrate);
            var step = $"its `{integrate.Id}` (`{NodeKinds.Integrate}`)";
            if (landing.Form == LandingForm.Merge || landing.Pattern is null)
            {
                said.Add($"{step} integrates the branch the landing `{land.Id}` makes, and the landing names no pattern for one (a merge makes none): "
                    + "the workflow's `land` names one.");
            }

            if (request is LandingIntegrate.Done or LandingIntegrate.Steward)
            {
                if (policy?.Gates != LandingIntegrate.Declared)
                {
                    said.Add($"{step} asks with `{request}`, which needs the line's gates, and `{repository}`'s line policy does not set `gates` to "
                        + "`declared` (an accepted declaration alone is not gates turned on): it integrates with no gates, so the request is the owner's.");
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
                said.Add($"{step} asks with `done`, which a landing with no press starts, and the landing `{land.Id}` waits for your Accept (a `review` "
                    + "stands before it): a version with no `review` lands with no press.");
            }
        }

        return said;
    }

    /// <summary>Whether the line integrates with no gates under its policy: its <c>gates</c> is absent or <c>none</c>.</summary>
    public static bool NoGates(LandingIntegrate? policy) => policy?.Gates != LandingIntegrate.Declared;

    /// <summary>
    /// What a door says of a repository a choice reaches whose line integrates with no gates: the person's Accept moves the
    /// line, and a version that asks with the unit's done is refused there.
    /// </summary>
    public static string NoGatesSaid(string repository) =>
        $"`{repository}` integrates with no gates: its line policy declares none, so the line moves on your Accept, and a version with no press is refused there.";
}
