namespace Daoris.Driver;

/// <summary>
/// The open's re-ask as its transport hands it to the session: a refused model is first said as one
/// <see cref="WireReading.ModelRefused"/> on the run's sink, which the offers store's part keeps, then the executor's re-ask is asked, so the
/// walk it runs passes the refusing account.
/// </summary>
internal sealed class RefusalReading(IChoiceReask reask, IReadingSink sink) : IChoiceReask
{
    public Task<ActorAsked?> ReaskAsync(ModelRefusal refusal)
    {
        sink.Read(new WireReading.ModelRefused(refusal));
        return reask.ReaskAsync(refusal);
    }

    /// <summary>The port over <paramref name="reask"/>, or none where none is handed and so nothing is judged.</summary>
    public static IChoiceReask? Of(IChoiceReask? reask, IReadingSink sink) => reask is null ? null : new RefusalReading(reask, sink);
}
