namespace Daoris.Driver;

/// <summary>
/// This machine, named: the hosts an http:// location may have (the tools' rule 5), and the hosts a registration follower reads as a
/// local service. The rule is the service's, which every reader of an address asks; <see cref="Tools.IsLoopback"/> answers through it.
/// </summary>
public static class LoopbackHosts
{
    /// <summary>The names a host may have to be this machine, as a URL spells them.</summary>
    public static readonly IReadOnlyList<string> Names = ["localhost", "127.0.0.1", "[::1]"];

    /// <summary>Whether a host, as a URL names it, is this machine. The CLI's <c>isLoopbackHost</c>.</summary>
    public static bool Is(string host) => Names.Contains(host, StringComparer.Ordinal);
}
