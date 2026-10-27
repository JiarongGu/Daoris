namespace Daoris.Driver;

/// <summary>
/// One plugin as the workplace judges it (D100): the id its manifest names, whether the person switched it on, why it contributes
/// nothing, and the points it speaks on. Plugins keeps the catalogue's entry, which implements this directly.
/// </summary>
public interface IWorkplacePluginEntry
{
    /// <summary>The id the plugin's manifest names.</summary>
    string Id { get; }

    /// <summary>The person's word, independent of whether the plugin is sound.</summary>
    bool Enabled { get; }

    /// <summary>Why it contributes nothing, in a sentence, or null when sound.</summary>
    string? Problem { get; }

    /// <summary>The points its manifest speaks on; none where it declares no hooks.</summary>
    IReadOnlyList<string> Points { get; }
}

/// <summary>
/// The plugins under the home, as the workplace asks them: whether one stands and how, and which contribute. Plugins keeps the
/// catalogue, which implements this directly; every caller that holds the catalogue passes it as it is.
/// </summary>
public interface IWorkplacePluginCatalog
{
    /// <summary>The plugin whose id is <paramref name="plugin"/>, matched without regard to case, or null where none is installed.</summary>
    IWorkplacePluginEntry? Find(string plugin);

    /// <summary>The ones the host drives, enabled and sound, in the catalogue's order.</summary>
    IReadOnlyList<IWorkplacePluginEntry> Contributing { get; }
}

/// <summary>
/// What the workplace holds of Plugins that is made, not read: the plugin wire over a home, which a session's trees take by default, and
/// the home's catalogue as it loads. Plugins installs it when the driver loads.
/// </summary>
internal interface IWorkplacePluginOperations
{
    /// <summary>The wire over <paramref name="home"/>, which speaks to the home's own plugins and says nothing where it is not handed a way to.</summary>
    IWorkplacePluginWire Wire(string home);

    /// <summary>The home's plugins, read as the driver reads them — a harness name this build carries refused.</summary>
    IWorkplacePluginCatalog Load(string home);
}

internal static class WorkplacePlugins
{
    private static IWorkplacePluginOperations? _current;

    internal static IWorkplacePluginOperations Current
    {
        get => _current ?? throw new InvalidOperationException("no workplace plugin operations are installed: Plugins installs them when the driver loads");
        set => _current = value;
    }
}
