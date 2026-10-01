using System.Text.Json;

namespace Daoris.Driver;

/// <summary>What a plugin's last failure was: where (<c>start</c>, <c>process</c>, or a point), of what kind, and when.</summary>
public sealed record PluginFailure(string Where, string Kind, DateTimeOffset At);

/// <summary>
/// A plugin's health (D119 §2): its state, since when where that is known, its failure while failing, and the
/// points the loop's process listens on while one is up.
/// </summary>
/// <param name="Since">When a running process started, or when a failing plugin failed; null for the other states.</param>
public sealed record PluginHealthState(string State, DateTimeOffset? Since, PluginFailure? Failure, IReadOnlyList<string> Listening)
{
    /// <summary>When the record's last word about the plugin was: what a terminal says it read, and when (D119 §2).</summary>
    public DateTimeOffset? Word { get; init; }

    /// <summary>Which process said that last word, by the machine log's source; null in the loop's own record.</summary>
    public string? Source { get; init; }
}

/// <summary>One thing that happened to a plugin, as the record takes it: what <see cref="PluginLog"/> writes, and what a log line is read back as.</summary>
public sealed record PluginWord(
    string Event, string Plugin, string? By = null, IReadOnlyList<string>? Points = null, string? Why = null,
    string? Point = null, string? Where = null, string? Kind = null);

/// <summary>
/// The loop's own record of each plugin's health (PLUGUI1d, D119 §2), fed by everything in its process that speaks
/// to a plugin: the hook set, the landing and the hand-off, through <see cref="PluginLog"/>. A terminal, which is
/// another process, reads the same state from the machine log's last word (<see cref="FromLog(PluginEntry, IEnumerable{LogLine})"/>).
/// </summary>
/// <remarks>
/// <para><b>One table of cases holds both readings</b> (<c>PluginHealthTests</c>): the record and the log are both
/// played through <see cref="Apply"/>, and the state is decided from the catalogue's entry and the record by
/// <see cref="Decide"/>.</para>
///
/// <para><b>The five states.</b> <c>off</c>: the person switched it off. <c>refused</c>: on, and the driver refused
/// it. <c>failing</c>: on and sound, and its last word to Daoris was a failure; its next good answer clears it, and
/// so does the person switching it off or updating it, which starts its record afresh, while a restart of its
/// process alone does not. <c>running</c>: on and sound, it speaks at a point the loop asks, and the loop's process
/// for it is up. <c>ready</c>: on and sound with nothing up: it only declares, it speaks only at a landing, or the
/// loop has not started it (yet, or since Daoris stopped).</para>
///
/// <para><b>The record is its process's own.</b> A new process starts with none, so the log's reading starts its
/// record afresh where the process that ran the loop for the plugin starts or stops (<c>app.started</c>,
/// <c>app.stopped</c>), and where another process's loop starts it.</para>
/// </remarks>
public sealed class PluginHealth(Func<DateTimeOffset>? clock = null)
{
    public const string Running = "running";

    public const string Ready = "ready";

    public const string Failing = "failing";

    public const string Refused = "refused";

    public const string Off = "off";

    public static readonly IReadOnlyList<string> States = [Running, Ready, Failing, Refused, Off];

    /// <summary>The events a record is played from: the rest (served, tried, tested) are not the plugin's work.</summary>
    private static readonly IReadOnlySet<string> Played =
        new HashSet<string>([PluginEvents.Started, PluginEvents.Stopped, PluginEvents.Called, PluginEvents.Failed], StringComparer.Ordinal);

    private readonly Func<DateTimeOffset> _clock = clock ?? (() => DateTimeOffset.UtcNow);
    private readonly Dictionary<string, Record> _records = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>What is known about one plugin.</summary>
    /// <param name="LoopSource">Which process's loop it was last started by, read from the log; null in a process's own record.</param>
    internal sealed record Record(
        bool Up, DateTimeOffset? UpSince, IReadOnlyList<string> Listening, PluginFailure? Failure,
        DateTimeOffset? Word = null, string? Source = null, string? LoopSource = null)
    {
        public static readonly Record Empty = new(false, null, [], null);
    }

    /// <summary>One thing that happened to a plugin, now.</summary>
    public void Observe(PluginWord word)
    {
        if (!Played.Contains(word.Event)) return;
        var at = _clock();
        lock (_records)
        {
            _records[word.Plugin] = Apply(_records.GetValueOrDefault(word.Plugin) ?? Record.Empty, word, at);
        }
    }

    /// <summary>The plugin's health now, from the catalogue's word about it and this record.</summary>
    public PluginHealthState Of(PluginEntry entry)
    {
        Record record;
        lock (_records) record = _records.GetValueOrDefault(entry.Manifest.Id) ?? Record.Empty;
        return Decide(entry, record) with { Word = record.Word };
    }

    /// <summary>The points the loop's process for the plugin listens on, while one is up: the handshake's answer.</summary>
    public IReadOnlyList<string> Listening(string plugin)
    {
        lock (_records) return _records.GetValueOrDefault(plugin) is { Up: true } record ? record.Listening : [];
    }

    /// <summary>
    /// The plugin's health as the machine log's last word about it says (D119 §2): its lines played through the
    /// same rules as the record, oldest first. A line of another plugin, or of an event the record does not take,
    /// is passed over.
    /// </summary>
    public static PluginHealthState FromLog(PluginEntry entry, IEnumerable<LogLine> lines)
    {
        var id = entry.Manifest.Id;
        var record = Record.Empty;
        foreach (var line in lines)
        {
            if (line.Event is "app.started" or "app.stopped")
            {
                // The process that ran the plugin's loop went, and its record with it: the next starts with none.
                if (record.LoopSource == line.Source) record = Record.Empty with { Word = record.Word, Source = record.Source };
                continue;
            }

            if (!Played.Contains(line.Event) || Text(line.Data, "plugin") is not { } plugin
                || !string.Equals(plugin, id, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var word = new PluginWord(
                line.Event, plugin, By: Text(line.Data, "by"),
                Points: Text(line.Data, "points") is { } points ? points.Split(',', StringSplitOptions.RemoveEmptyEntries) : null,
                Why: Text(line.Data, "why"), Point: Text(line.Data, "point"), Where: Text(line.Data, "where"), Kind: Text(line.Data, "kind"));

            // Another process's loop starting it begins a record of that process's own.
            if (word.By == PluginEvents.ByLoop && word.Event == PluginEvents.Started
                && record.LoopSource is { } running && running != line.Source)
            {
                record = Record.Empty;
            }

            record = Apply(record, word, line.Time) with
            {
                Word = line.Time,
                Source = line.Source,
                LoopSource = word.By == PluginEvents.ByLoop ? line.Source : record.LoopSource,
            };
        }

        return Decide(entry, record) with { Word = record.Word, Source = record.Source };
    }

    /// <summary>The plugin's health from the machine log under <paramref name="home"/>: every day the log keeps.</summary>
    public static PluginHealthState FromLog(PluginEntry entry, string home) =>
        FromLog(entry, MachineLogReader.Read(Path.Combine(home, MachineLog.Folder), new LogFilter()).Lines);

    /// <summary>The rules: what one word does to a record. The same for the loop's own record and the log's.</summary>
    internal static Record Apply(Record record, PluginWord word, DateTimeOffset at)
    {
        var loop = word.By == PluginEvents.ByLoop;
        record = record with { Word = at };
        switch (word.Event)
        {
            case PluginEvents.Started:
                // A landing's or a hand-off's process lives for one frame, and is not the loop's.
                return loop ? record with { Up = true, UpSince = at, Listening = word.Points ?? [] } : record;

            case PluginEvents.Called:
                // A good answer clears a failure, wherever it was answered.
                return record with { Failure = null };

            case PluginEvents.Failed:
                var failed = record with { Failure = new PluginFailure(word.Where ?? "", word.Kind ?? "", at) };
                // The loop's process is gone when it could not start, exited between calls, or exited at a call.
                return loop && (word.Where is PluginEvents.AtStart or PluginEvents.AtProcess || word.Kind == PluginEvents.Exited)
                    ? failed with { Up = false, UpSince = null, Listening = [] }
                    : failed;

            case PluginEvents.Stopped:
                // The person switching it off, removing it or updating it starts its record afresh.
                var stopped = word.Why is PluginEvents.Off or PluginEvents.Removed or PluginEvents.Updated
                    ? record with { Failure = null }
                    : record;
                return loop ? stopped with { Up = false, UpSince = null, Listening = [] } : stopped;

            default:
                return record;
        }
    }

    /// <summary>The state, from what the catalogue says and what the record knows.</summary>
    internal static PluginHealthState Decide(PluginEntry entry, Record record)
    {
        if (!entry.Enabled) return new(Off, null, null, []);
        if (entry.Problem is not null) return new(Refused, null, null, []);
        var listening = record.Up ? record.Listening : [];
        if (record.Failure is { } failure) return new(Failing, failure.At, failure, listening);

        var speaksInLoop = entry.Manifest.Hooks?.Points.Any(point => HookPoints.Loop.Contains(point, StringComparer.Ordinal)) == true;
        return record.Up && speaksInLoop
            ? new(Running, record.UpSince, null, listening)
            : new(Ready, null, null, []);
    }

    private static string? Text(JsonElement data, string key) =>
        data.ValueKind == JsonValueKind.Object && data.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
