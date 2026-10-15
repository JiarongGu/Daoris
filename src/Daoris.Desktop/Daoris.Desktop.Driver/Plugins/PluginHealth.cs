namespace Daoris.Driver;

/// <summary>
/// The loop's own record of each plugin's health (PLUGUI1d, D119 §2), fed by everything in its process that speaks
/// to a plugin: the hook set, the landing and the hand-off, through <see cref="PluginLog"/>. A terminal, which is
/// another process, reads the same state from the machine log's last word (<see cref="FromLog(PluginEntry, string)"/>).
/// </summary>
/// <remarks>
/// The record and the clock it stamps each word with are this class's; the rules it plays them by, and the five states,
/// are the Domain's <see cref="PluginHealthRules"/> (STRUCT3a), which a terminal's reading plays the log's lines through,
/// and which callers name for a state and for a reading of lines (FLAT1b-i).
/// </remarks>
public sealed class PluginHealth(Func<DateTimeOffset>? clock = null)
{
    private readonly Func<DateTimeOffset> _clock = clock ?? (() => DateTimeOffset.UtcNow);
    private readonly Dictionary<string, PluginHealthRules.Record> _records = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>One thing that happened to a plugin, now.</summary>
    public void Observe(PluginWord word)
    {
        if (!PluginHealthRules.Plays(word.Event)) return;
        var at = _clock();
        lock (_records)
        {
            _records[word.Plugin] = PluginHealthRules.Apply(_records.GetValueOrDefault(word.Plugin) ?? PluginHealthRules.Record.Empty, word, at);
        }
    }

    /// <summary>The plugin's health now, from the catalogue's word about it and this record.</summary>
    public PluginHealthState Of(PluginEntry entry)
    {
        PluginHealthRules.Record record;
        lock (_records) record = _records.GetValueOrDefault(entry.Manifest.Id) ?? PluginHealthRules.Record.Empty;
        return PluginHealthRules.Decide(entry, record) with { Word = record.Word };
    }

    /// <summary>The points the loop's process for the plugin listens on, while one is up: the handshake's answer.</summary>
    public IReadOnlyList<string> Listening(string plugin)
    {
        lock (_records) return _records.GetValueOrDefault(plugin) is { Up: true } record ? record.Listening : [];
    }

    /// <summary>The plugin's health from the machine log under <paramref name="home"/>: every day the log keeps (<see cref="PluginHealthRules.FromLog"/>).</summary>
    public static PluginHealthState FromLog(PluginEntry entry, string home) =>
        PluginHealthRules.FromLog(entry, MachineLogReader.Read(DaorisHome.Entry(home, HomeEntry.Logs),new LogFilter()).Lines);
}
