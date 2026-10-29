namespace Daoris.Driver;

/// <summary>
/// What the participants read of the work orders: the tools the connector offers, which a partner is denied but for those it is offered.
/// Work orders keeps the list and implements this, installed when the driver loads (<c>WorkOrders/Work/ParticipantsWorkOrdersWiring.cs</c>);
/// the operation answers exactly what its caller read before it was a port.
/// </summary>
internal interface IParticipantsWorkOrders
{
    /// <summary>Every tool the connector offers, in the list's order (see <c>BrainstormPermissions.ConnectorTools</c>).</summary>
    IReadOnlyList<string> ConnectorTools { get; }
}

internal static class ParticipantsWorkOrders
{
    private static IParticipantsWorkOrders? _current;

    internal static IParticipantsWorkOrders Current
    {
        get => _current ?? throw new InvalidOperationException("no participants work-orders operations are installed: Work orders installs them when the driver loads");
        set => _current = value;
    }
}
