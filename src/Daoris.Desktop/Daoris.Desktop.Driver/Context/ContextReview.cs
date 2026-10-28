namespace Daoris.Driver;

/// <summary>
/// What Context asks of the review: the checks a brief reads a call by (design §3.1 item 4), the gates a tree's <c>daoris.gates.json</c>
/// declares and this machine's heavy-gate mapping for a repository. Review installs it when the driver loads and does exactly what the
/// brief did before it was a port.
/// </summary>
internal interface IContextReview
{
    /// <summary>The gates' file, at a checkout's root.</summary>
    string GatesFile { get; }

    /// <summary>The checkout's declared gates, in order.</summary>
    /// <exception cref="DriverException">There is no file, or it does not read.</exception>
    IReadOnlyList<HeavyGate> DeclaredGates(string checkout);

    /// <summary>This machine's gates for a repository, its name trimmed and matched in any case; null where it has none here.</summary>
    IReadOnlyList<HeavyGate>? MappedGates(DriverConfig config, string repository);
}

/// <summary>The required review operations, resolved only where a brief reads its checks.</summary>
internal static class ContextReview
{
    private static IContextReview? _current;

    internal static IContextReview Current
    {
        get => _current ?? throw new InvalidOperationException("no context review operations are installed: Review installs them when the driver loads");
        set => _current = value;
    }
}
