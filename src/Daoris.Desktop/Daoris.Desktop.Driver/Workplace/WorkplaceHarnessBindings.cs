namespace Daoris.Driver;

/// <summary>
/// The harness conversations a home keeps, as the workplace reads and lets them go (FOUNDATION1 z-iii): Participants keeps the store, so the
/// workplace names only these four operations, which the conversations implement directly.
/// </summary>
internal interface IWorkplaceHarnessBindings
{
    /// <summary>What this machine keeps for the session's conversation, apart from whether it reads: whether anything is kept, and whether what is kept reads.</summary>
    (bool Kept, bool Reads) Kept(string session);

    /// <summary>Where the desktop store, which holds the bindings once they are imported, lies in this home.</summary>
    string StoreLocation { get; }

    /// <summary>Which of these sessions the store holds a binding for, read without writing anything; throws what the store's file throws where it does not read.</summary>
    IReadOnlyList<string> BindingsOf(IReadOnlyCollection<string> sessions);

    /// <summary>The bindings of these deleted records gone from the store, those that held one named; a store that will not take the delete throws.</summary>
    IReadOnlyList<string> ForgetBindings(IReadOnlyCollection<string> sessions);
}

/// <summary>Participants constructs the conversations over a home; the workplace holds that construction for its two readers.</summary>
internal interface IWorkplaceHarnessBindingStores
{
    IWorkplaceHarnessBindings Open(string home);
}

internal static class WorkplaceHarnessBindings
{
    private static IWorkplaceHarnessBindingStores? _current;

    internal static IWorkplaceHarnessBindingStores Current
    {
        get => _current ?? throw new InvalidOperationException("no workplace harness-binding store factory is installed: Participants installs it when the driver loads");
        set => _current = value;
    }
}
