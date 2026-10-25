namespace Daoris.Driver;

/// <summary>
/// What the remote sync asks of a checkout's git, as the service declares it and the workplace answers (FOUNDATION1 mod-iv, the design §14.3: the
/// lower declares a port, the higher implements it): the line the person set or git guesses, where the checkout stands, whether it is
/// clean, whether one file is as HEAD has it, and how its commit stands to one the deployment holds. Each answer is git's own, asked at
/// the moment of the feed (D48 §6).
/// </summary>
public interface ICheckoutReads
{
    /// <summary>The repository's line (WSR2): what the person set for it, for its workspace, else the checkout's own guess; null where nothing names one.</summary>
    Task<string?> LineAsync(string root, string repository, string? workspace, DriverConfig choices, CancellationToken ct);

    /// <summary>Where this checkout stands, or null where git cannot say, which is a tree that feeds nothing.</summary>
    Task<TreeProvenance?> ProvenanceAsync(string root, CancellationToken ct);

    /// <summary>Whether the checkout is a git repository with nothing uncommitted.</summary>
    Task<bool> CleanAsync(string root, CancellationToken ct);

    /// <summary>Whether one file is exactly as HEAD has it: tracked, and nothing staged, changed or untracked about it (SYNC5b).</summary>
    Task<bool> UnmodifiedAsync(string root, string path, CancellationToken ct);

    /// <summary>How <paramref name="head"/> stands to <paramref name="held"/>, the question a deployment cannot ask (SYNC5a).</summary>
    Task<TreeRelation> RelationAsync(string root, string held, string head, CancellationToken ct);
}

/// <summary>
/// The checkout reads in force in this process: the workplace's git, installed when the driver's assembly loads (<c>Trees/ServiceWiring.cs</c>), so
/// every host and test that builds a sync reads the checkout's git as it always has. A test that wants another sets it and puts it back.
/// </summary>
public static class CheckoutReads
{
    private static ICheckoutReads? _current;

    /// <summary>The reads in force.</summary>
    /// <exception cref="InvalidOperationException">None was installed: the assembly's own initializer has not run.</exception>
    public static ICheckoutReads Current
    {
        get => _current ?? throw new InvalidOperationException("no checkout reads are installed: the workplace installs them when the driver loads");
        set => _current = value;
    }
}
