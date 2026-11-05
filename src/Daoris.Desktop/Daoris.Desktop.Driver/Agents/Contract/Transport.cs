namespace Daoris.Driver;

/// <summary>The two shared, agent-neutral ways a door talks to its process (the agent-provider design §2.6).</summary>
public enum Transport
{
    /// <summary>The process per run with its target on its command line, read line by line: today's pipe door.</summary>
    Native,

    /// <summary>The Agent Client Protocol on stdio: today's protocol door.</summary>
    Protocol,
}
