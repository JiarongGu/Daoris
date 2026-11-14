namespace Daoris.Driver;

/// <summary>
/// <i>Done and failed</i>'s plugin commands: a round of a plugin's command that failed wherever it ran, one notice by plugin, command and moment, read from the machine log's
/// <c>plugin.command</c> lines, which hold names alone. Its fields: <see cref="Notice.Code"/> the plugin, <see cref="Notice.Act"/> the
/// command, <see cref="Notice.Route"/> who asked (<see cref="CommandWire"/>), <see cref="Notice.State"/> <c>failed</c>,
/// <see cref="Notice.Session"/> the session an agent called from. A round that ended well is no notice.
/// </summary>
public static class PluginCommandNotices
{
    /// <summary>The notice's kind.</summary>
    public const string Kind = "plugin-command";

    /// <summary>The news teller whose occurrences these are.</summary>
    public const string Teller = "plugin-commands";

    /// <summary>How far back a look reads: a day, since the log is read at every look and a claimed key is never told twice.</summary>
    public static readonly TimeSpan Reach = TimeSpan.FromDays(1);

    public static IReadOnlyList<NewsOccurrence> Occurrences(string home, DateTimeOffset now)
    {
        var read = MachineLogReader.Read(DaorisHome.Entry(home, HomeEntry.Logs), new LogFilter(Since: now - Reach, Event: PluginEvents.Command));
        var occurrences = new List<NewsOccurrence>();
        foreach (var line in read.Lines)
        {
            if (Of(line) is not { } notice) continue;
            occurrences.Add(new NewsOccurrence(notice.Id, line.Time, notice));
        }

        return occurrences;
    }

    /// <summary>The notice a line makes, or null for one that is no news or names no plugin and command.</summary>
    public static Notice? Of(LogLine line)
    {
        var plugin = Text(line, "plugin");
        var command = Text(line, "command");
        var from = Text(line, "from");
        if (plugin is null || command is null || from is null) return null;
        int? exit = line.Data.TryGetProperty("exit", out var code) && code.ValueKind == System.Text.Json.JsonValueKind.Number ? code.GetInt32() : null;
        var failed = exit is not null and not 0 || (exit is null && from == CommandWire.Agent);
        if (!failed) return null;
        var id = $"{Kind}:{plugin}/{command}/{line.Stamp}";
        return new Notice(id, Kind, line.Time, State: failed ? "failed" : "reached", Route: from, Act: command, Code: plugin, Session: Text(line, "session"));
    }

    private static string? Text(LogLine line, string name) =>
        line.Data.TryGetProperty(name, out var value) && value.ValueKind == System.Text.Json.JsonValueKind.String ? value.GetString() : null;
}
