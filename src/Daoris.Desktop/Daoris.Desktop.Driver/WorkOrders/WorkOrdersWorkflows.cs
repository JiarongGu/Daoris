namespace Daoris.Driver;

/// <summary>
/// What work orders ask of Workflows, which owns the saved workflows and the nodes' records: what a unit still owes in them, whether a saved
/// workflow can be chosen for an ask, and which workflow each kind of a workspace's declaration maps to. Workflows installs it when the driver
/// loads and answers exactly as the work inventory, the ask's door and the intake's kinds read them before it was a port.
/// </summary>
internal interface IWorkOrdersWorkflows
{
    /// <summary>Every unit's obligations under <paramref name="home"/>, by unit id; none where neither the nodes' nor the resources' folder holds a record.</summary>
    IReadOnlyDictionary<string, IReadOnlyList<InventoryEntry>> Obligations(string home, DateTimeOffset now);

    /// <summary>The units that still owe anything: a receipt not ended, or an attempt not settled, or a record that does not read and names one.</summary>
    IReadOnlySet<string> Owing(string home);

    /// <summary>
    /// Why the saved workflow <paramref name="workflow"/> cannot be chosen, in the door's sentence, or null where it is saved here and its
    /// newest version reads (design §3.9).
    /// </summary>
    string? Unusable(string home, string workflow);

    /// <summary>
    /// The workflow a repository's work follows where it names no kind, and, for each of <paramref name="kinds"/> in order, the one that kind
    /// maps to with what it lowers of the person's part against the first (null where either does not read).
    /// </summary>
    (string Otherwise, IReadOnlyList<(string Name, IReadOnlyList<string>? Lowers)> Kinds) KindsOf(
        string home, DriverConfig config, string repository, string workspace, IReadOnlyList<string> kinds);
}

/// <summary>The required Workflows operations, resolved only where a unit's obligations, a saved workflow or a kind's mapping is read.</summary>
internal static class WorkOrdersWorkflows
{
    private static IWorkOrdersWorkflows? _current;

    internal static IWorkOrdersWorkflows Current
    {
        get => _current ?? throw new InvalidOperationException("no work orders workflow operations are installed: Workflows installs them when the driver loads");
        set => _current = value;
    }
}
