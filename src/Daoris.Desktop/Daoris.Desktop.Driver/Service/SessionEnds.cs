namespace Daoris.Driver;

/// <summary>
/// What a session record's successful move tells whoever holds what the record's end frees (FOUNDATION1 mod-iv, the design §14.3: the lower
/// declares a port, the higher implements it): the client hands the record the service answered, once the move is made, and awaits what
/// the hook does, so a lock a session held is let go before the move returns (TREELOCK2). The workplace's hook is installed when the driver's
/// assembly loads (<c>Trees/ServiceWiring.cs</c>).
/// </summary>
public static class SessionEnds
{
    /// <summary>Told the record a move answered, in the service's own spelling of it, never a teammate's.</summary>
    public static Func<PriorSession, Task> Ended { get; set; } = _ => Task.CompletedTask;
}
