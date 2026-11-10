namespace Daoris.Driver;

/// <summary>
/// The places whose hand is refused at their start (<see cref="PluginHostToken"/>): no identity serves origin, it is read only, Write
/// is denied, or it holds no API token. No process starts, so this is no start that failed: it is said as
/// <see cref="PluginEvents.Refused"/>, never counted as an exit (so never unsound), and asked again once <see cref="Again"/> has passed,
/// so a token the person sets starts the process at a later look with no press, and a refused place runs no git at every look.
/// </summary>
internal sealed class HandRefusals
{
    /// <summary>How long a refused place waits before its hand is asked again.</summary>
    public static readonly TimeSpan Again = TimeSpan.FromMinutes(1);

    private readonly Dictionary<string, DateTimeOffset> _at = new(StringComparer.Ordinal);

    /// <summary>Whether <paramref name="key"/>'s hand is refused less than <see cref="Again"/> before <paramref name="now"/>.</summary>
    public bool Waits(string key, DateTimeOffset now) => _at.TryGetValue(key, out var at) && now - at < Again;

    /// <summary>The refusal kept at <paramref name="now"/>, and the start's failure marked with its own kind, naming the plugin.</summary>
    public DriverException Refused(string key, DateTimeOffset now, string plugin, string refusal)
    {
        _at[key] = now;
        return PluginFailures.Mark(new DriverException($"plugin `{plugin}`: {refusal}."), PluginEvents.Refused);
    }
}
