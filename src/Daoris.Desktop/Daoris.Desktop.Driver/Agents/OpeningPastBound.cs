namespace Daoris.Driver;

/// <summary>
/// A held session's open passed its bound (<see cref="TransportProtocol.OpenBound"/>): its harness did not open or resume its conversation in
/// time, as one waiting on a server it is handed that never comes up does not. The run ends its process as for any failed open.
/// </summary>
public sealed class OpeningPastBound(TimeSpan bound)
    : Exception($"the harness did not open its conversation within {(int)bound.TotalSeconds} s")
{
    public TimeSpan Bound { get; } = bound;
}
