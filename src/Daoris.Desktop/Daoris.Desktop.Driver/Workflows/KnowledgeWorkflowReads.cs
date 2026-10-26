using System.Runtime.CompilerServices;

namespace Daoris.Driver;

internal sealed class KnowledgeWorkflowReads : IKnowledgeWorkflows
{
    [ModuleInitializer]
    internal static void Install() => KnowledgeWorkflows.Current = new KnowledgeWorkflowReads();

    public (string Workflow, string Level) Select(DriverConfig config, string repository, string workspace)
    {
        var selected = WorkflowSelection.Resolve(config, repository, workspace, null);
        return (selected.Workflow, selected.Level);
    }
}
