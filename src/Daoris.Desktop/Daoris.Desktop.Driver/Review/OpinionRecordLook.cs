namespace Daoris.Driver;

/// <summary>
/// The bus comparer's look (<see cref="IBusLook"/>) as the opinion's record holds it, read from the service (<see cref="OpinionDoors.ReadAsync"/>):
/// a pass asked is handled once the record is given or failed; a pass given once its findings are answered or it found none; findings
/// delivered once answered. The gate's judging and the run's step are no reading here (unknown), and neither are the checks: their
/// readers join with their jobs.
/// </summary>
public sealed class OpinionRecordLook(Func<string, CancellationToken, Task<OpinionView?>> read) : IBusLook
{
    public async Task<BusLookReading> ReadAsync(BusTimer timer, CancellationToken ct)
    {
        var kind = timer.Kind;
        if (kind != BusTypes.OpinionAsked.Kind && kind != BusTypes.OpinionGiven.Kind && kind != BusTypes.OpinionDelivered.Kind) return BusLookReading.None;
        OpinionView? view;
        try
        {
            view = await read(timer.Subject, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OperationCanceledException and not OutOfMemoryException)
        {
            return new BusLookReading(BusLookStates.Unknown, "unread");
        }

        return Of(kind, view);
    }

    /// <summary>The reading of <paramref name="view"/> for a timer of <paramref name="kind"/>.</summary>
    public static BusLookReading Of(string kind, OpinionView? view)
    {
        if (view is null) return new BusLookReading(BusLookStates.Unknown, "no-record");
        if (kind == BusTypes.OpinionAsked.Kind)
        {
            return view.State == OpinionViews.Reading
                ? new BusLookReading(BusLookStates.Open, view.State)
                : new BusLookReading(BusLookStates.Handled, view.State);
        }

        if (view.Answers.Count > 0) return new BusLookReading(BusLookStates.Handled, "answered");
        if (kind == BusTypes.OpinionGiven.Kind && view.Findings is { Count: 0 }) return new BusLookReading(BusLookStates.Handled, "clean");
        return new BusLookReading(BusLookStates.Open, "unanswered");
    }
}
