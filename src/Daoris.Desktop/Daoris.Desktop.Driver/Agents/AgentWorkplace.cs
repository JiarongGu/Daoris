namespace Daoris.Driver;

/// <summary>Workplace prepares a start's git environment and composes its resolved process instruction.</summary>
public interface IAgentWorkplace
{
    void HandGit(IDictionary<string, string?> environment, string? home, string root);
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
