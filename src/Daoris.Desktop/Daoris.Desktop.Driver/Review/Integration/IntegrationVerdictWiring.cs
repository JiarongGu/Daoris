using System.Runtime.CompilerServices;

namespace Daoris.Driver;

/// <summary>
/// What the workplace asks of the review when it integrates a candidate, installed when the driver loads (<see cref="IIntegrationVerdicts"/>;
/// INTEGRATE1f part f2): the candidate's gates run in one job and judged as the merge tool judges them.
/// </summary>
internal sealed class IntegrationVerdictOperations(IntegrationWorld? world = null) : IIntegrationVerdicts
{
    [ModuleInitializer]
    internal static void Install() => IntegrationVerdicts.Current = new IntegrationVerdictOperations();

    /// <inheritdoc />
    public Task<IntegrationVerdict> JudgeAsync(IntegrationVerdictAsk ask) =>
        // The environment gates are composed from is read for each judgement: it is the install's as it stands, not as it was when the driver loaded.
        new IntegrationJudging(ask, world ?? new IntegrationWorld()).RunAsync();
}
