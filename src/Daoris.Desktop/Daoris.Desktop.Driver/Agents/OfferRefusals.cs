namespace Daoris.Driver;

/// <summary>
/// The offers store's part of a run's sink (the agent-provider design §11 row l, §3.2's <c>ModelRefused</c>): an open's refused model kept as
/// the account's refusal of it at its door's version, so the open's re-ask and every later walk pass that account for that model. A store that
/// will not take it costs the reading, said through <paramref name="unkept"/>, never the run.
/// </summary>
internal sealed class OfferRefusals(string home, string door, string? version, string? account, Action<string>? unkept = null) : IReadingSink
{
    public bool Watched => false;

    public void Read(WireReading reading)
    {
        if (reading is not WireReading.ModelRefused { Replayed: false } refused) return;
        if (AgentOffers.Refused(home, door, version, account, refused.Refusal.Value, DateTimeOffset.UtcNow) is { } why) unkept?.Invoke(why);
    }
}
