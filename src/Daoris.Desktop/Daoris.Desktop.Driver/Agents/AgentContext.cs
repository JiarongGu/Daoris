namespace Daoris.Driver;

/// <summary>Context retains transcript storage and instruction/accounting composition; Agents declares the required operations.</summary>
internal interface IAgentContext
{
    TextWriter OpenTranscript(string path, bool append);
    ComposedInstruction Account(SessionTarget target, IReadOnlyList<TargetPrompt.Piece> pieces, StandingBlockTold? block);
    ComposedInstruction Intake(SessionTarget target, string prompt, StandingBlockTold? block);
    string Parts(SessionTarget target);

    /// <summary>The standing block of <paramref name="facts"/> after <paramref name="before"/> characters: inline, or filed.</summary>
    StandingBlockTold Block(StandingFacts facts, int before);

    /// <summary><paramref name="facts"/> bound to the harness that runs them, in <paramref name="tree"/>.</summary>
    StandingFacts OnHarness(StandingFacts facts, AgentDoor adapter, string tree);
}

internal static class AgentContext
{
    private static IAgentContext? _current;
    internal static IAgentContext Current
    {
        get => _current ?? throw new InvalidOperationException("no agent context operations are installed: Context installs them when the driver loads");
        set => _current = value;
    }
}
