namespace Daoris.Driver;

/// <summary>
/// The second opinion's lines teed into the bus's log beside the machine log's: every <c>opinion.asked</c>,
/// <c>opinion.given</c> and <c>opinion.answered</c> the landing line's channel carries (<see cref="ServiceClient.LandingLined"/>, which the
/// look's gate and the deliverer write through) is appended as its envelope (<see cref="OpinionBusFacts.OfLanding"/>), no consumer
/// reading it yet.
/// </summary>
/// <remarks>A line it cannot tee is counted and said by the log, and the channel's other watchers are told it as before.</remarks>
public sealed class OpinionBusTee : IDisposable
{
    private readonly ServiceClient _service;
    private readonly BusLog _bus;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, string> _passes = new(StringComparer.Ordinal);

    public OpinionBusTee(ServiceClient service, BusLog bus)
    {
        _service = service;
        _bus = bus;
        service.LandingLined += OnLanding;
    }

    public void Dispose() => _service.LandingLined -= OnLanding;

    private void OnLanding(LandingLine line) =>
        _bus.Tee(() =>
        {
            lock (_gate) return OpinionBusFacts.OfLanding(line, _passes);
        });
}
