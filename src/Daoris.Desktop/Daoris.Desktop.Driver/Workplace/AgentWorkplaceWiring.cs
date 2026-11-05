using System.Runtime.CompilerServices;

namespace Daoris.Driver;

internal sealed class WorkingAgentWorkplace : IAgentWorkplace
{
    [ModuleInitializer]
    internal static void Install() => AgentWorkplace.Current = new WorkingAgentWorkplace();

    public string? ProcessWords(IStartCommits? commits, IStartLanding? plan) => Daoris.Driver.ProcessWords.Said(commits, plan);
}
