using System.Text.Json;
using System.Text.RegularExpressions;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// The page and the machine log (D94): its report into the log (LOG1b) — what is used, and what fails, on
/// the screen — and Settings → Logs, the screen's door to reading it (LOG1c).
/// </summary>
/// <remarks>
/// <para><b><c>EVENT</c></b> carries <c>{ event, data }</c>. The answer is always nothing: the report is
/// fire-and-forget on the page, and a report that could fail would be a reason for the page to handle
/// one.</para>
///
/// <para><b><c>LINES</c></b> reads every source's lines through <see cref="MachineLogReader"/>, the
/// reader <c>daoris-driver logs</c> uses (D50: the two doors filter alike), with the filters applied here
/// and a cap, so the page is handed the recent lines and never the month. <b><c>OPEN_FOLDER</c></b> opens
/// the log's own folder in the file manager; the page names no path. Both are the shell's only: no HTTP
/// route serves the log, and a browser sees none of it (D47 §4).</para>
///
/// <para>🔴 <b>This is where D94's list of what is never logged is enforced.</b> The page is the one writer
/// that could pass a word through by mistake, so the module takes only the catalogue's events
/// (<c>docs/2026-09-30-machine-log-design.md</c> §4), each with only its declared fields, each field only
/// in its declared kind: a name (an identifier, never a sentence), a short text, a count, a flag. Anything
/// else (another event, another field, a sentence where a name goes, words where a count goes) is dropped
/// without a word. A message is its length and how many files it carried; never its text, never a name.</para>
/// </remarks>
public sealed class LogModule(
    MachineLog log,
    // The file manager, for Open the folder. Null where the host carries none: then nothing opens, and
    // the answer says so.
    OpenFolder? openFolder = null,
    // What time it is, for a span back from now; the system's by default.
    Func<DateTimeOffset>? clock = null)
    : ModuleBase
{
    /// <summary>The longest string a field keeps; longer is cut and ends in an ellipsis.</summary>
    public const int MaxText = 120;

    /// <summary>How many lines <c>LINES</c> answers when the page names no limit.</summary>
    public const int DefaultLines = 200;

    /// <summary>The most lines <c>LINES</c> answers, whatever the page asks: a screen of recent lines, not the month.</summary>
    public const int MaxLines = 1000;

    public override string ModuleName => "DAORIS.LOG";

    /// <summary>What a field may hold.</summary>
    private enum Kind
    {
        /// <summary>An identifier: a view, a command, a region, a session id. Never a sentence.</summary>
        Name,

        /// <summary>A short text, cut at <see cref="MaxText"/>: the one free string, a caught error's message.</summary>
        Text,

        /// <summary>A whole number, zero or more.</summary>
        Count,

        /// <summary>True or false.</summary>
        Flag,
    }

    /// <summary>The page's events, each with its fields and each field's kind — the design's §4, and nothing more.</summary>
    private static readonly IReadOnlyDictionary<string, (string Level, (string Field, Kind Kind)[] Fields)> Catalogue =
        new Dictionary<string, (string, (string, Kind)[])>(StringComparer.Ordinal)
        {
            ["view.opened"] = ("info", [("view", Kind.Name)]),
            ["command.run"] = ("info", [("command", Kind.Name)]),
            ["panel.moved"] = ("info", [("view", Kind.Name), ("region", Kind.Name)]),
            ["message.sent"] = ("info", [("session", Kind.Name), ("kind", Kind.Name), ("length", Kind.Count), ("files", Kind.Count)]),
            ["proposal.settled"] = ("info", [("applied", Kind.Flag)]),
            ["page.error"] = ("error", [("where", Kind.Name), ("message", Kind.Text)]),
        };

    /// <summary>An identifier: letters, digits and the joiners ids use (a mirrored session is `origin/id`), no spaces.</summary>
    private static readonly Regex Identifier = new("^[A-Za-z0-9][A-Za-z0-9_.:/-]*$", RegexOptions.CultureInvariant);

    protected override Task<object?> RouteMessageAsync(IpcRequest request, IModuleContext context, CancellationToken cancellationToken)
    {
        switch (request.Type)
        {
            case "EVENT":
                Take(request.Payload);
                return Done();

            case "LINES":
                return Task.FromResult<object?>(Lines(request.Payload));

            case "OPEN_FOLDER":
                return Task.FromResult<object?>(Open());

            default:
                throw UnknownType(request);
        }
    }

    private void Take(JsonElement? payload)
    {
        if (payload is not { ValueKind: JsonValueKind.Object } report
            || !report.TryGetProperty("event", out var named) || named.ValueKind != JsonValueKind.String
            || !Catalogue.TryGetValue(named.GetString()!, out var entry))
        {
            return;
        }

        var data = report.TryGetProperty("data", out var given) && given.ValueKind == JsonValueKind.Object ? given : default;
        var kept = new List<(string Key, object? Value)>();
        foreach (var (field, kind) in entry.Fields)
        {
            if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty(field, out var value) && Keep(value, kind) is { } fit)
            {
                kept.Add((field, fit));
            }
        }

        log.Write(entry.Level, named.GetString()!, kept);
    }

    /// <summary>The value as the log may hold it, or null when it is not of its field's kind.</summary>
    private static object? Keep(JsonElement value, Kind kind) => kind switch
    {
        Kind.Name when value.ValueKind == JsonValueKind.String && value.GetString() is { } name && Identifier.IsMatch(name) => Cut(name),
        Kind.Text when value.ValueKind == JsonValueKind.String => Cut(value.GetString()!),
        Kind.Count when value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var count) && count >= 0 => count,
        Kind.Flag when value.ValueKind is JsonValueKind.True or JsonValueKind.False => value.GetBoolean(),
        _ => null,
    };

    private static string Cut(string text) => text.Length <= MaxText ? text : $"{text[..MaxText]}…";

    /// <summary>
    /// The recent lines, newest first, at most the limit; with the folder they are read from, how many
    /// matched, what each level counts, the events the period holds, and how many could not be read.
    /// </summary>
    /// <remarks>
    /// The counts are taken before the level filter and the events before the event filter, so a person
    /// narrowed to errors still sees how many warnings there were, and can choose another event.
    /// </remarks>
    private object Lines(JsonElement? payload)
    {
        var since = Filter(payload, "since", text => MachineLogReader.ParseSince(text) is not null,
            "a span such as 30m, 2h or 3d");
        var source = Filter(payload, "source", MachineLogReader.Sources.Contains,
            $"one of {string.Join(", ", MachineLogReader.Sources)}");
        var level = Filter(payload, "level", MachineLogReader.Floors.Contains, "warn or error");
        var named = PayloadHelper.GetOptionalValue<string>(payload, "event") is { Length: > 0 } wanted ? wanted : null;
        var limit = PayloadHelper.GetOptionalValue<int>(payload, "limit") is > 0 and var asked
            ? Math.Min(asked, MaxLines)
            : DefaultLines;

        var folder = log.Location;
        if (folder is null)
        {
            return new
            {
                Folder = (string?)null, Lines = Array.Empty<object>(), Total = 0, Counts = Count([]),
                Events = Array.Empty<string>(), Skipped = 0,
            };
        }

        var now = (clock ?? (() => DateTimeOffset.UtcNow))();
        var read = MachineLogReader.Read(folder, new LogFilter(
            Since: since is null ? null : now - MachineLogReader.ParseSince(since)!.Value,
            Source: source));
        var events = read.Lines.Select(line => line.Event).Distinct().Order(StringComparer.Ordinal).ToArray();
        var ofEvent = named is null ? read.Lines : read.Lines.Where(line => line.Event == named).ToList();
        var shown = level is null
            ? ofEvent
            : ofEvent.Where(line => MachineLogReader.Rank(line.Level) >= MachineLogReader.Rank(level)).ToList();

        return new
        {
            Folder = folder,
            Lines = Enumerable.Reverse(shown).Take(limit)
                .Select(line => new { Time = line.Stamp, line.Source, line.Level, line.Event, line.Data })
                .ToArray(),
            Total = shown.Count,
            Counts = Count(ofEvent),
            Events = events,
            read.Skipped,
        };
    }

    private static object Count(IReadOnlyList<LogLine> lines) => new
    {
        Info = lines.Count(line => MachineLogReader.Rank(line.Level) == 0),
        Warn = lines.Count(line => line.Level == "warn"),
        Error = lines.Count(line => line.Level == "error"),
    };

    /// <summary>A filter's value, null when the page set none, refused by name when the reader cannot use it.</summary>
    private static string? Filter(JsonElement? payload, string name, Func<string, bool> fits, string takes)
    {
        var value = PayloadHelper.GetOptionalValue<string>(payload, name);
        if (string.IsNullOrEmpty(value)) return null;
        return fits(value)
            ? value
            : throw Refusals.Because(
                Refusals.LogFilterUnknown,
                $"`{value}` is not a {name} the log reads; it takes {takes}.",
                ("filter", name), ("value", value));
    }

    /// <summary>The log's own folder in the file manager, made first if nothing has written to it yet.</summary>
    private object Open()
    {
        var folder = log.Location;
        if (folder is null || openFolder is null) return new { Opened = false, Folder = folder };

        try
        {
            Directory.CreateDirectory(folder);
            openFolder(folder);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException
            or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            throw Refusals.Because(
                Refusals.LogFolderNotOpened,
                $"the log's folder would not open: {error.Message}",
                ("folder", folder), ("problem", error.Message));
        }

        return new { Opened = true, Folder = folder };
    }
}
