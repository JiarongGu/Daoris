namespace Daoris.Knowledge.Http.Routes;

/// <summary>What a route module is handed at start: the composed service, and what this start draws or is given.</summary>
/// <param name="Service">The composed service every route answers from.</param>
/// <param name="Mode">Local or shared; a module maps a door a shared host must not serve only where this is local.</param>
/// <param name="Run">The id this host drew at its start, answered on every response's <c>x-daoris-run</c>.</param>
/// <param name="Home">A local host's home's id; null on a shared host, and where the home's id does not read.</param>
/// <param name="PersonKey">The person key this start is handed; null where none is.</param>
/// <param name="Gate">The person gate, which judges a door's forms and tells an agent's call from the person's.</param>
/// <param name="Confirmations">What askers have asked the person to confirm, held in this start's memory; used on a local host only.</param>
/// <param name="Changes">The change stream a local host watches its ledger by; null on a shared host.</param>
/// <param name="Workspace">The one circle a shared host serves; null on a local host, which holds every circle this machine wired.</param>
/// <param name="RepositoryRoot">Where the repositories are, which an import with no folder scans.</param>
/// <param name="Stopping">The host's stop, which answers a held wait at once.</param>
public sealed record HostWiring(
    ComposedService Service, ServiceMode Mode, string Run, string? Home, PersonKey? PersonKey, PersonGate Gate,
    PersonConfirmations Confirmations, ChangeStream? Changes, string? Workspace, string RepositoryRoot, CancellationToken Stopping)
{
    /// <summary>
    /// Whether <paramref name="http"/> is a caller on a local host's own machine, the only caller a machine path is answered to. A
    /// shared host answers no one: structural absence, not a policed permission. No remote address is the in-process server's call.
    /// </summary>
    public bool MachineLocal(HttpContext http) =>
        Mode == ServiceMode.Local
        && (http.Connection.RemoteIpAddress is null || System.Net.IPAddress.IsLoopback(http.Connection.RemoteIpAddress));
}
