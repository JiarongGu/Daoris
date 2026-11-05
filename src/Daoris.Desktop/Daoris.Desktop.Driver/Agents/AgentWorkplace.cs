namespace Daoris.Driver;

/// <summary>Workplace composes a start's resolved process instruction; its git is the shell's <see cref="ISessionGit"/>.</summary>
public interface IAgentWorkplace
{
    string? ProcessWords(IStartCommits? commits, IStartLanding? plan);
}

/// <summary>The required Workplace operations installed when the driver loads.</summary>
public static class AgentWorkplace
{
    private static IAgentWorkplace? _current;
    public static IAgentWorkplace Current
    {
        get => _current ?? throw new InvalidOperationException("no agent workplace operations are installed: Workplace installs them when the driver loads");
        set => _current = value;
    }
}
