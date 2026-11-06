namespace Daoris.Driver;

/// <summary>What the look's plugin pass reaches of the driver beyond <see cref="ILookWiring"/>: the adapters it was built with, the hooks, and the taking up of a read catalogue.</summary>
internal interface IPluginWiring : ILookWiring
{
    /// <summary>The adapters this build carries, before any plugin's.</summary>
    ProviderRegistry Built { get; }

    /// <summary>The loop's hooks, reconciled with each read catalogue.</summary>
    HookSet? Hooks { get; }

    /// <summary>
    /// Takes a read catalogue up: the adapters with its plugins, its servers, its catalogue, and the harness roster over them, with
    /// the one set of Daoris's accounts (POOL1a, D197) folded again where a held lock refused it.
    /// </summary>
    void Adopt(PluginCatalog catalog);
}
