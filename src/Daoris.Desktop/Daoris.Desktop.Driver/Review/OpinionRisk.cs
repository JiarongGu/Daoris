namespace Daoris.Driver;

/// <summary>
/// What the second opinion's gate asks of the risk policy, which Workflows keeps: the judge at the work's place, which answers a
/// dispute or commits nobody read as <c>work.accept</c>, and the record of a pass it gave, journaled once on the work's run. The review speaks
/// the act and the verdict (the settings' vocabulary) and never names the judge; Workflows installs this when the driver loads.
/// </summary>
internal interface IOpinionRisk
{
    /// <summary>The judge at the place whose policy and access <paramref name="config"/> holds, with the rules of the <c>opinion</c> step <paramref name="process"/> binds; null where none is read.</summary>
    Func<RiskAct, RiskVerdict>? JudgeAt(DriverConfig config, string? workspace, string? repository, IWorkplaceProcess? process);

    /// <summary>
    /// A dispute the policy passed in <paramref name="gate"/>, journaled once on the chat run or on the run and <c>opinion</c> step
    /// <paramref name="process"/> binds; nothing for a gate the policy did not answer. A failed append throws.
    /// </summary>
    void Passed(
        string home, DriverConfig config, string subject, string? workspace, string? repository, string? chatRun, IWorkplaceProcess? process,
        OpinionGateState gate, DateTimeOffset now);
}

/// <summary>The Workflows operation, resolved where a second opinion's gate is read; unwired, it reads no judge, so nothing passes by the policy.</summary>
internal static class OpinionRisk
{
    private static IOpinionRisk? _current;

    internal static IOpinionRisk Current
    {
        get => _current ?? Unwired;
        set => _current = value;
    }

    internal static IOpinionRisk Unwired { get; } = new UnwiredOpinionRisk();

    private sealed class UnwiredOpinionRisk : IOpinionRisk
    {
        public Func<RiskAct, RiskVerdict>? JudgeAt(DriverConfig config, string? workspace, string? repository, IWorkplaceProcess? process) => null;

        public void Passed(
            string home, DriverConfig config, string subject, string? workspace, string? repository, string? chatRun, IWorkplaceProcess? process,
            OpinionGateState gate, DateTimeOffset now)
        {
        }
    }
}
