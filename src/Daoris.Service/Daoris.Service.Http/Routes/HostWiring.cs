namespace Daoris.Knowledge.Http.Routes;

/// <summary>What a route module is handed at start: the composed service, and what this start drew or was given.</summary>
/// <param name="Service">The composed service every route answers from.</param>
/// <param name="Mode">Local or shared; a module maps a door a shared host must not serve only where this is local.</param>
/// <param name="Run">The id this host drew at its start, answered on every response's <c>x-daoris-run</c>.</param>
/// <param name="Home">A local host's home's id; null on a shared host, and where the home's id does not read.</param>
/// <param name="PersonKey">The person key this start was handed; null where none was.</param>
/// <param name="Gate">The person gate, which judges a door's forms and tells an agent's call from the person's.</param>
public sealed record HostWiring(ComposedService Service, ServiceMode Mode, string Run, string? Home, PersonKey? PersonKey, PersonGate Gate)
{
    /// <summary>
    /// Whether <paramref name="http"/> is a caller on a local host's own machine, the only caller a machine path is answered to. A
    /// shared host answers no one: structural absence, not a policed permission. No remote address is the in-process server's call.
    /// </summary>
    public bool MachineLocal(HttpContext http) =>
        Mode == ServiceMode.Local
        && (http.Connection.RemoteIpAddress is null || System.Net.IPAddress.IsLoopback(http.Connection.RemoteIpAddress));
}
