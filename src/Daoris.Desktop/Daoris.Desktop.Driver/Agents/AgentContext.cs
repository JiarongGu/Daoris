namespace Daoris.Driver;

/// <summary>Context retains transcript storage and instruction/accounting composition; Agents declares the required operations.</summary>
internal interface IAgentContext
{
    TextWriter OpenTranscript(string path, bool append);
    ComposedInstruction Account(SessionTarget target, IReadOnlyList<TargetPrompt.Piece> pieces);
    ComposedInstruction Intake(SessionTarget target, string prompt);
    string Parts(SessionTarget target);
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
