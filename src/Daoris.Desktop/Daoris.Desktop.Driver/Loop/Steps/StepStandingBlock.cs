namespace Daoris.Driver;

/// <summary>
/// The standing block of the session that performs a plugin's step: the place it works at and its one plugin's brief, with no knowledge
/// section, since the step's ask carries everything the step needs.
/// </summary>
public static class StepStandingBlock
{
    /// <summary>The facts of the step's session at <paramref name="ask"/>'s place, handed only <paramref name="tools"/>.</summary>
    public static StandingFacts Of(string home, StepAgentAsk ask, ServerEntry tools) => new()
    {
        Repository = ask.Repository,
        Workspace = ask.Workspace,
        Knowledge = false,
        PluginBriefs = PluginBriefs.At(home, new PluginPlace(ask.Repository, ask.Workspace), [tools]),
    };
}
