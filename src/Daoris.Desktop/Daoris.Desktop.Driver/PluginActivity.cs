using System.Text.Json;

namespace Daoris.Driver;

/// <summary>One point's answers in a period: counted by word and by failure kind, with the median and slowest answer time.</summary>
/// <param name="MedianMs">The median answer time (the two middles' mean for an even count), or null with no answer timed.</param>
public sealed record PointActivity(
    string Point, IReadOnlyDictionary<string, int> Answers, IReadOnlyDictionary<string, int> Failures, long? MedianMs, long? SlowestMs);

/// <summary>A plugin's processes in a period: every start (the loop's, a landing's, a hand-off's), every exit, and every start that failed.</summary>
public sealed record ProcessActivity(int Starts, int Exits, int FailedStarts);

/// <summary>A name and how many: the sessions started on one of the plugin's agents.</summary>
public sealed record CountedName(string Name, int Count);

/// <summary>How many sessions one of the plugin's servers was handed to, and withheld from.</summary>
public sealed record ServerActivity(string Server, int Handed, int Withheld);

/// <summary>The plugin's trials in a period, by verdict.</summary>
public sealed record TrialActivity(int Passed, int Failed);

/// <summary>A branch the plugin answered that it pushed, from the landing record: its pull request, and when the branch landed.</summary>
public sealed record PluginPush(string Repository, string Branch, string? PullRequest, DateTimeOffset At);

/// <summary>
/// One line of a plugin's recent events: when, the event's word (<c>started</c>, <c>called</c>, <c>failed</c>…), its
/// point (or where it failed, or the server), its answer or failure kind (or why it stopped, who started it,
/// handed or withheld, passed or failed), and how long it took.
/// </summary>
public sealed record ActivityEvent(DateTimeOffset At, string Event, string? Point, string? Word, long? Ms);

/// <summary>What <see cref="PluginActivity.Read"/> found for one plugin since <paramref name="Since"/>.</summary>
/// <param name="Logged">Whether this machine has a machine log at all: with none, everything is empty, and the reader says so.</param>
/// <param name="Recent">The newest events first, at most <see cref="PluginActivity.RecentLines"/>.</param>
/// <param name="More">How many older events of the period the recent list leaves out.</param>
/// <param name="Skipped">How many lines of the period's files could not be read.</param>
public sealed record PluginActivityRead(
    string Plugin, DateTimeOffset Since, bool Logged, IReadOnlyList<PointActivity> Points, ProcessActivity Process,
    IReadOnlyList<CountedName> Agents, IReadOnlyList<ServerActivity> Servers, TrialActivity Trials,
    IReadOnlyList<PluginPush> Pushes, IReadOnlyList<ActivityEvent> Recent, int More, int Skipped);

/// <summary>
/// What a plugin did (PLUGUI1d, D119 §3.2 and §4.1): the machine log's <c>plugin.*</c> lines for one plugin over a
/// period, summarised, with the sessions started on its agents (<c>session.started</c>'s <c>adapter</c>) and the
/// branches it pushed (the landing record, D102). One reader for both doors: the page's <c>PLUGIN_ACTIVITY</c> and
/// <c>daoris-driver plugins activity</c>.
/// </summary>
/// <remarks>
/// <para><b>Never the plugin's words.</b> The log holds none (D94 §5), and the landing record's pull request is an
/// address the plugin answered, shown as the link it is. What the plugin said this run is the console's ring, which
/// the page reads apart (<c>TAIL_SESSION</c>).</para>
///
/// <para>A line that cannot be read is skipped and counted, as <see cref="MachineLogReader"/> does; a day's file
/// older than the period is not opened.</para>
/// </remarks>
public static class PluginActivity
{
    /// <summary>How many recent events are answered.</summary>
    public const int RecentLines = 50;

    /// <summary>The period a page opens on, and the terminal's without <c>--since</c>: the last 7 days.</summary>
    public static readonly TimeSpan DefaultPeriod = TimeSpan.FromDays(7);

    /// <exception cref="DriverException">This machine holds no such plugin (<see cref="PluginPage.Unknown"/>).</exception>
    public static PluginActivityRead Read(string home, string id, DateTimeOffset since)
    {
        var (entry, manifest, _) = PluginPage.Installed(home, id);
        var plugin = entry.Manifest.Id;
        var folder = Path.Combine(home, MachineLog.Folder);
        var read = MachineLogReader.Read(folder, new LogFilter(Since: since));

        var points = new List<string>(manifest.Hooks?.Points ?? []);
        var answers = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
        var failures = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
        var times = new Dictionary<string, List<long>>(StringComparer.Ordinal);
        var agents = manifest.Harnesses.Select(harness => harness.Name).ToList();
        var sessions = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var servers = manifest.Servers.Select(server => server.Name).ToList();
        var served = new Dictionary<string, (int Handed, int Withheld)>(StringComparer.Ordinal);
        int starts = 0, exits = 0, failedStarts = 0, passed = 0, failedTrials = 0;
        var events = new List<ActivityEvent>();

        void Point(string point)
        {
            if (!points.Contains(point, StringComparer.Ordinal)) points.Add(point);
        }

        foreach (var line in read.Lines)
        {
            if (line.Event == "session.started")
            {
                if (Text(line, "adapter") is { } adapter && agents.FirstOrDefault(agent => string.Equals(agent, adapter, StringComparison.OrdinalIgnoreCase)) is { } agent)
                {
                    sessions[agent] = sessions.GetValueOrDefault(agent) + 1;
                }

                continue;
            }

            if (!PluginEvents.All.Contains(line.Event, StringComparer.Ordinal)
                || !string.Equals(Text(line, "plugin"), plugin, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var word = line.Event["plugin.".Length..];
            var ms = Number(line, "ms");
            switch (line.Event)
            {
                case PluginEvents.Started:
                    starts++;
                    events.Add(new(line.Time, word, null, Text(line, "by"), ms));
                    break;

                case PluginEvents.Stopped:
                    events.Add(new(line.Time, word, null, Text(line, "why"), null));
                    break;

                case PluginEvents.Called when Text(line, "point") is { } point:
                    Point(point);
                    var answer = Text(line, "answer") ?? "(unsaid)";
                    Bump(answers, point, answer);
                    if (ms is { } took) (times.TryGetValue(point, out var list) ? list : times[point] = []).Add(took);
                    events.Add(new(line.Time, word, point, answer, ms));
                    break;

                case PluginEvents.Failed:
                    var where = Text(line, "where");
                    var kind = Text(line, "kind") ?? "(unsaid)";
                    if (where == PluginEvents.AtStart) failedStarts++;
                    if (kind == PluginEvents.Exited) exits++;
                    if (where is { } at && at is not (PluginEvents.AtStart or PluginEvents.AtProcess))
                    {
                        Point(at);
                        Bump(failures, at, kind);
                    }

                    events.Add(new(line.Time, word, where, kind, ms));
                    break;

                case PluginEvents.Served when Text(line, "server") is { } server:
                    var handed = Flag(line, "handed");
                    if (!servers.Contains(server, StringComparer.Ordinal)) servers.Add(server);
                    var (yes, no) = served.GetValueOrDefault(server);
                    served[server] = handed ? (yes + 1, no) : (yes, no + 1);
                    events.Add(new(line.Time, word, server, handed ? "handed" : "withheld", null));
                    break;

                case PluginEvents.Tried or PluginEvents.Tested:
                    var ok = Flag(line, "passed");
                    if (line.Event == PluginEvents.Tried && ok) passed++;
                    else if (line.Event == PluginEvents.Tried) failedTrials++;
                    events.Add(new(line.Time, word, null, ok ? "passed" : "failed", ms));
                    break;
            }
        }

        var pushes = new LandedBranches(home).PushedBy(plugin)
            .Where(entry => entry.LandedAt >= since)
            .Reverse()
            .Select(entry => new PluginPush(entry.Repository, entry.Branch, entry.PullRequest, entry.LandedAt))
            .ToList();

        events.Reverse();
        return new PluginActivityRead(
            plugin, since, Directory.Exists(folder),
            [.. points.Select(point => new PointActivity(
                point,
                answers.GetValueOrDefault(point) ?? [],
                failures.GetValueOrDefault(point) ?? [],
                Median(times.GetValueOrDefault(point)),
                times.GetValueOrDefault(point) is { Count: > 0 } all ? all.Max() : null))],
            new ProcessActivity(starts, exits, failedStarts),
            [.. agents.Select(agent => new CountedName(agent, sessions.GetValueOrDefault(agent)))],
            [.. servers.Select(server => new ServerActivity(server, served.GetValueOrDefault(server).Handed, served.GetValueOrDefault(server).Withheld))],
            new TrialActivity(passed, failedTrials),
            pushes,
            [.. events.Take(RecentLines)],
            Math.Max(0, events.Count - RecentLines),
            read.Skipped);
    }

    /// <summary>The middle answer time, or the two middles' mean; null with none.</summary>
    private static long? Median(List<long>? times)
    {
        if (times is not { Count: > 0 }) return null;
        var sorted = times.Order().ToList();
        var middle = sorted.Count / 2;
        return sorted.Count % 2 == 1
            ? sorted[middle]
            : (long)Math.Round((sorted[middle - 1] + sorted[middle]) / 2.0, MidpointRounding.AwayFromZero);
    }

    private static void Bump(Dictionary<string, Dictionary<string, int>> counts, string point, string word)
    {
        if (!counts.TryGetValue(point, out var words)) counts[point] = words = new(StringComparer.Ordinal);
        words[word] = words.GetValueOrDefault(word) + 1;
    }

    private static string? Text(LogLine line, string key) =>
        line.Data.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static long? Number(LogLine line, string key) =>
        line.Data.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number)
            ? number
            : null;

    private static bool Flag(LogLine line, string key) =>
        line.Data.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.True;
}
