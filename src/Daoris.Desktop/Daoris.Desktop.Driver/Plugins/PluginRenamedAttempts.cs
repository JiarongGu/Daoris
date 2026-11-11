namespace Daoris.Driver;

/// <summary>What a rename asks of the node records, which own the attempts in flight: Workflows installs it when the driver loads.</summary>
internal interface IPluginRenamedAttempts
{
    /// <returns>True where it carried any attempt; null where none named the old id or nothing could be written.</returns>
    bool? Carry(string home, string former, string now);
}

/// <summary>The required Workflows operation, resolved only where a rename moves its parts.</summary>
internal static class PluginRenamedAttempts
{
    private static IPluginRenamedAttempts? _current;

    internal static IPluginRenamedAttempts Current
    {
        get => _current ?? throw new InvalidOperationException("no renamed attempts' carrier is installed: Workflows installs it when the driver loads");
        set => _current = value;
    }
}
