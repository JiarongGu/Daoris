using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The wait of a press that is the review's word for a run whose <c>land</c> the runtime performs:
/// it forgets a refusal nothing changed since (the press is the person's try again), asks a whole pass, and reads what the landing came to within
/// <see cref="GraphPasses.Bound"/>. It lands nothing and builds no plugin wire.
/// </summary>
internal static class PerformedWaits
{
    /// <summary>The landing the performer made of <paramref name="unit"/> at <paramref name="tip"/>, or null where none ended within the bound.</summary>
    public static async Task<TreeLanding?> AwaitAsync(DriverLoop loop, string session, string unit, string tip, CancellationToken ct)
    {
        PressSteps.Reach(PressSteps.Pass);
        LandPerformer.Retry(loop.Home, session, unit, tip);
        await loop.PassWholeAsync(unit, GraphPasses.Bound, ct).ConfigureAwait(false);
        var deadline = DateTimeOffset.UtcNow + GraphPasses.Bound;
        TreeLanding? outcome;
        while ((outcome = LandOutcomes.Of(unit, tip)) is null && DateTimeOffset.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(200), ct).ConfigureAwait(false);
        }

        return outcome;
    }
}
