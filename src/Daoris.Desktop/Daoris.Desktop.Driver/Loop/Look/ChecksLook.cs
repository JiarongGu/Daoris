namespace Daoris.Driver;

/// <summary>
/// The checks asked of the broker: the next one started beside the look under the machine's admission, and each verdict handed to the
/// session that asked, which ended its turn while the check waited.
/// </summary>
[LookPass("checks", LookPhase.Read, "helpers")]
internal sealed class ChecksLook(ILookStagesWiring wiring) : ILookPass
{
    public Task RunAsync(LookRound round, CancellationToken ct) =>
        wiring.Stages.TimeAsync("checks", () => wiring.Checks.LookAsync(ct));
}
