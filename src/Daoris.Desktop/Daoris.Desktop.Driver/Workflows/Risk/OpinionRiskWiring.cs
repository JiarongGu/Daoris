using System.Runtime.CompilerServices;

namespace Daoris.Driver;

/// <summary>The risk policy a second opinion's gate reads, installed when the driver loads (<see cref="IOpinionRisk"/>).</summary>
internal sealed class OpinionRiskOperations : IOpinionRisk
{
    [ModuleInitializer]
    internal static void Install() => OpinionRisk.Current = new OpinionRiskOperations();

    public Func<RiskAct, RiskVerdict>? JudgeAt(DriverConfig config, string? workspace, string? repository, IWorkplaceProcess? process)
    {
        var step = (process as WorkflowProcess)?.Graph?.Nodes.FirstOrDefault(node => node.Kind == NodeKinds.Opinion);
        return RiskStepGates.At(config, workspace, repository) is { } judge ? act => RiskStepGates.Verdict(judge, act.Kind, step)! : null;
    }

    public void Passed(
        string home, DriverConfig config, string subject, string? workspace, string? repository, string? chatRun, IWorkplaceProcess? process,
        OpinionGateState gate, DateTimeOffset now) =>
        PolicyPasses.Gate(home, config, PolicyPassAt.Of(subject, workspace, repository, chatRun, process), gate, now);
}
