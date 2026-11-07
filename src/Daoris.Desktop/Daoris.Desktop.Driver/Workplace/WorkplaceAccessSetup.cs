namespace Daoris.Driver;

/// <summary>
/// A repository's access setup as it resolved, pause by pause, as the workplace reads the landing pause of it:
/// Participants keeps the rule and its resolved answer, which implements this directly.
/// </summary>
internal interface IWorkplaceResolvedAccess
{
    /// <summary>The value <paramref name="pause"/> resolves to.</summary>
    string Value(string pause);

    /// <summary>Where <paramref name="pause"/>'s value is set.</summary>
    string Source(string pause);
}

/// <summary>
/// The access setup's resolution and its words (5), as the landing's effective rule and the merges note read
/// them: Participants keeps the rule (<c>Domain/AccessRules</c> lives there), so the workplace names only
/// these operations, which Participants installs when the driver loads.
/// </summary>
internal interface IWorkplaceAccessSetup
{
    /// <summary>A repository's setup as it resolves from <paramref name="config"/>: its own, its workspace's and this machine's.</summary>
    IWorkplaceResolvedAccess Resolve(DriverConfig config, string repository, string? workspace);

    /// <summary>Where a value is set, in a sentence.</summary>
    string Whose(string source);

    /// <summary>The source a pause nothing set resolves from: today's behaviour.</summary>
    string DefaultSource { get; }
}

internal static class WorkplaceAccessSetup
{
    private static IWorkplaceAccessSetup? _current;

    internal static IWorkplaceAccessSetup Current
    {
        get => _current ?? throw new InvalidOperationException("no workplace access setup is installed: Participants installs it when the driver loads");
        set => _current = value;
    }
}
