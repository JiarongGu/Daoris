using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// `daoris-driver plugins`, the host's one door for plugins (D50): the kit's <c>new</c> and <c>try</c>
/// (<see cref="PluginKitCommand"/>), and a plugin's page and activity, <c>show</c> and <c>activity</c> (PLUGUI1d,
/// D119 §4.4), the terminal twins of the Plugins view's page and its Activity.
/// </summary>
/// <remarks>
/// <para><b>Why the driver's.</b> <c>show</c> reads what only the driver knows (health, whether a process listens,
/// whether a plugin can land work here), and <c>activity</c> reads the machine log, whose one reader is the driver's.
/// Each reads through the same reader the page's route calls: <see cref="PluginPage.Read"/> and
/// <see cref="PluginActivity.Read"/>.</para>
///
/// <para><b>A terminal is another process</b>, so a plugin's health here is the machine log's last word, and
/// <c>show</c> says so and when (D119 §2).</para>
///
/// <para>The printing lives in the library so a test runs the whole door in-process. Exit codes keep the family
/// contract: 0 shown · 1 the plugin failed a check (<c>try</c>) · 2 it could not do what was asked.</para>
/// </remarks>
public static class PluginsCommand
{
    public const string Usage =
        PluginKitCommand.Usage + "\n"
        + "       daoris-driver plugins show <id> [--json]\n"
        + "       daoris-driver plugins activity <id> [--since <30m|2h|3d>] [--json]";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        // 中文 stays readable, as it does in the log.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <param name="log">This host's machine log, where the kit's trial of an installed plugin is written.</param>
    public static async Task<int> RunAsync(
        string[] args, TextWriter output, string? home, MachineLog? log = null, CancellationToken ct = default)
    {
        try
        {
            switch (args)
            {
                case ["new" or "try", ..]:
                    return await PluginKitCommand.RunAsync(args, output, home, ct, log).ConfigureAwait(false);
                case ["show", .. var rest]:
                    return ShowVerb(rest, output, home);
                case ["activity", .. var rest]:
                    return ActivityVerb(rest, output, home);
                default:
                    output.WriteLine(Usage);
                    output.WriteLine("  new and try are the plugin kit's; show is a plugin's page, its health read from the machine");
                    output.WriteLine("  log's last word; activity is what it did, from the machine log, the last 7 days unless --since.");
                    return 2;
            }
        }
        catch (DriverException error)
        {
            output.WriteLine($"plugins: {error.Message}");
            return 2;
        }
    }

    private static int ShowVerb(string[] args, TextWriter output, string? home)
    {
        var (id, json, _) = Parse(args, "show", takesSince: false);
        var page = PluginPage.Read(Home(home), id);
        output.Write(json ? JsonSerializer.Serialize(page, Json) + "\n" : Show(page));
        return 0;
    }

    private static int ActivityVerb(string[] args, TextWriter output, string? home)
    {
        var (id, json, since) = Parse(args, "activity", takesSince: true);
        var read = PluginActivity.Read(Home(home), id, DateTimeOffset.UtcNow - (since ?? PluginActivity.DefaultPeriod));
        if (json)
        {
            output.WriteLine(JsonSerializer.Serialize(read, Json));
            return 0;
        }

        if (!read.Logged)
        {
            output.WriteLine($"plugins: no machine log on this machine — nothing has written under `{Path.Combine(Home(home), MachineLog.Folder)}` yet.");
            return 0;
        }

        output.Write(Activity(read));
        return 0;
    }

    /// <summary>A plugin's page, as a terminal says it: the page's sections, one field a line.</summary>
    public static string Show(PluginPageRead page)
    {
        var text = new StringBuilder();
        text.Append($"plugins: `{page.Id}` — {page.Name}{(page.Version.Length > 0 ? " " + page.Version : "")}\n");
        if (page.Description.Length > 0) text.Append($"  {page.Description}\n");
        Field(text, "health", HealthLine(page));
        if (page.HealthFrom == "log")
        {
            Field(text, "", page.Health.Word is { } word
                ? $"from the machine log's last word for it, {Stamp(word)} ({page.Health.Source})"
                : "from the machine log, which has no word of it");
        }

        Field(text, "folder", page.Folder);
        Field(text, "installed", page.InstalledAt is { } at ? Stamp(at) : "unknown");
        Field(text, "source", SourceLine(page.Source));
        Field(text, "data", page.Data.Exists
            ? $"{page.Data.Folder} — {(page.Data.More ? "more than " : "")}{page.Data.Files} file(s), "
              + $"{page.Data.Bytes.ToString("N0", CultureInfo.InvariantCulture)} bytes"
              + (page.Data.Changed is { } changed ? $", newest {Stamp(changed)}" : "")
            : $"{page.Data.Folder} — it keeps nothing yet");

        var taken = page.Taken ? "" : " (not taken)";
        foreach (var (point, index) in page.Points.Select((point, index) => (point, index)))
        {
            var about = point.Kind is { } kind
                ? $"{Article(kind)} {kind}, waits {Span(point.WaitMs!.Value)}"
                : "not a point this build has";
            if (point.Listening is { } listening) about += listening ? ", listening" : ", not listening: the driver asks it nothing there";
            Field(text, index == 0 ? "points" : "", $"{point.Name,-15} {about}{taken}");
            if (point.Name == HookPoints.Land && page.Landing is { } landing)
            {
                Field(text, "", "  " + (landing.Rules.Count > 0
                    ? "named by " + string.Join("; ", landing.Rules.Select(rule => $"{rule.Scope} `{rule.Name}` ({rule.Pattern})"))
                    : "no landing rule names it — `daoris driver landing` names one"));
                Field(text, "", "  " + (landing.Problem ?? "it can land work here"));
            }
        }

        foreach (var (agent, index) in page.Agents.Select((agent, index) => (agent, index)))
        {
            var parts = new List<string>
            {
                $"`{string.Join(" ", agent.Command)}`",
                $"way in: {agent.WayIn}",
                $"permission mode: {agent.Posture ?? "its wire names none"}",
            };
            if (agent.ProfileVariable is { } variable) parts.Add($"account variable {variable}");
            if (agent.Package is { } package) parts.Add($"package {package}");
            if (agent.Install is { Count: > 0 } install) parts.Add($"installed by `{string.Join(" ", install)}`");
            Field(text, index == 0 ? "agents" : "", $"{agent.Name}{taken}  {string.Join(", ", parts)}");
        }

        foreach (var (server, index) in page.Servers.Select((server, index) => (server, index)))
        {
            var environment = server.Environment.Count > 0 ? $", environment {string.Join(", ", server.Environment)}" : "";
            var handed = server.DrivesBrowser
                ? "drives Daoris's browser, and is withheld where no shell answers"
                : "handed to every session beside Daoris's own knowledge host";
            Field(text, index == 0 ? "servers" : "", $"{server.Name}{taken}  `{string.Join(" ", server.Command)}`{environment}; {handed}");
        }

        Field(text, "tests", page.Tests.Count > 0 ? string.Join(", ", page.Tests) : "its folder carries none");
        return text.ToString();
    }

    /// <summary>A plugin's activity, as a terminal says it: the summary, then the newest events.</summary>
    public static string Activity(PluginActivityRead read)
    {
        var text = new StringBuilder();
        text.Append($"plugins: `{read.Plugin}`'s activity since {Stamp(read.Since)}\n");
        foreach (var point in read.Points)
        {
            var answered = point.Answers.Values.Sum();
            var failed = point.Failures.Values.Sum();
            var parts = new List<string>();
            if (answered > 0) parts.Add($"{answered} answered ({Counts(point.Answers)})");
            if (failed > 0) parts.Add($"{failed} failure{(failed == 1 ? "" : "s")} ({Counts(point.Failures)})");
            if (point.MedianMs is { } median) parts.Add($"median {median} ms, slowest {point.SlowestMs} ms");
            Field(text, point.Point, parts.Count > 0 ? string.Join(" · ", parts) : "nothing in the period", width: 16);
        }

        Field(text, "process", $"{read.Process.Starts} start(s), {read.Process.Exits} exit(s), {read.Process.FailedStarts} failed start(s)", width: 16);
        Field(text, "trials", $"{read.Trials.Passed} passed, {read.Trials.Failed} failed", width: 16);
        foreach (var (agent, index) in read.Agents.Select((agent, index) => (agent, index)))
        {
            Field(text, index == 0 ? "agents" : "", $"{agent.Name}: {agent.Count} session(s)", width: 16);
        }

        foreach (var (server, index) in read.Servers.Select((server, index) => (server, index)))
        {
            Field(text, index == 0 ? "servers" : "", $"{server.Server}: handed to {server.Handed} session(s), withheld from {server.Withheld}", width: 16);
        }

        foreach (var (push, index) in read.Pushes.Select((push, index) => (push, index)))
        {
            Field(text, index == 0 ? "pushed" : "", $"{push.Repository}  {push.Branch}  {push.PullRequest ?? "(no pull request)"}  {Stamp(push.At)}", width: 16);
        }

        if (read.Recent.Count == 0)
        {
            Field(text, "recent", "nothing in the machine log for this plugin in the period", width: 16);
        }
        else
        {
            Field(text, "recent", $"the newest {read.Recent.Count} of {read.Recent.Count + read.More}:", width: 16);
            foreach (var e in read.Recent)
            {
                text.Append($"    {Stamp(e.At)}  {e.Event,-8} {e.Point ?? "",-15} {e.Word ?? "",-11}{(e.Ms is { } ms ? $" {ms} ms" : "")}".TrimEnd()).Append('\n');
            }
        }

        if (read.Skipped > 0) text.Append($"plugins: {read.Skipped} line(s) could not be read and were skipped.\n");
        return text.ToString();
    }

    /// <summary>The health line: the state and since when, and what it costs while it fails (D119 §2).</summary>
    private static string HealthLine(PluginPageRead page)
    {
        var health = page.Health;
        var points = page.Hook?.Points ?? [];
        return health.State switch
        {
            PluginHealth.Running => $"running since {Stamp(health.Since!.Value)}, listening on {string.Join(", ", health.Listening)}",
            PluginHealth.Failing when health.Failure is { } failure =>
                $"failing since {Stamp(failure.At)}: {failure.Kind} at {failure.Where}. {Cost(failure.Where, points)}",
            PluginHealth.Refused => $"refused: {page.Problem}",
            PluginHealth.Off => $"off: the driver asks it nothing, and hands sessions nothing it declares. `daoris plugin enable {page.Id}` turns it on.",
            _ => points.Any(point => HookPoints.Loop.Contains(point, StringComparer.Ordinal))
                ? "ready: on and sound, and no process of it is up now; the driver loop starts it"
                : points.Contains(HookPoints.Land, StringComparer.Ordinal)
                    ? "ready: a landing starts it for each branch it lands"
                    : points.Contains(HookPoints.State, StringComparer.Ordinal)
                        ? "ready: a look that may remove a landed branch starts it to ask about its pull request"
                        : "ready: it declares agents and servers, and runs nothing itself",
        };
    }

    /// <summary>What a failure costs, by where it failed (D119 §2): a decision fails closed, an observation is contained.</summary>
    private static string Cost(string where, IReadOnlyList<string> points)
    {
        var at = where is PluginEvents.AtStart or PluginEvents.AtProcess
            ? points.Contains(HookPoints.QuestConsider, StringComparer.Ordinal) ? HookPoints.QuestConsider
              : points.Contains(HookPoints.SessionEnded, StringComparer.Ordinal) ? HookPoints.SessionEnded
              : points.Contains(HookPoints.Land, StringComparer.Ordinal) ? HookPoints.Land
              : HookPoints.State
            : where;
        return at switch
        {
            HookPoints.QuestConsider => "Every quest it is asked about sits until it answers, or until you turn it off.",
            HookPoints.Land => "A landing keeps its branch; push it by hand.",
            // PLUGHOOK1a (design §2.4): a query fails toward keeping.
            HookPoints.State => "Nothing is removed on its word until it answers.",
            _ => "Nothing waits on it; the driver goes on.",
        };
    }

    private static string SourceLine(PageSource source)
    {
        var from = source.Kind switch
        {
            "folder" => $"a folder: {source.Folder}",
            "offer" => $"Daoris's own plugins (`{source.Offer}`)",
            "unread" => $"its record does not read: {source.Problem}",
            _ => "no record of where it came from — `daoris plugin add <folder>` replaces it and records one",
        };
        var update = source.Update switch
        {
            "waits" => " · an update waits: " + string.Join("; ", source.Changes.Select(change => $"{change.What} {Or(change.Was)} → {Or(change.Now)}"))
                + $" — `daoris plugin update <id>`",
            "current" => " · its source declares the same",
            _ => source.Refusal is { } refusal ? $" · {refusal}" : "",
        };
        return from + update;
    }

    private static string Or(string said) => said.Length > 0 ? said : "(none)";

    private static string Counts(IReadOnlyDictionary<string, int> counts) =>
        string.Join(", ", counts.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key} {pair.Value}"));

    private static string Article(string kind) => kind is "observation" or "act" ? "an" : "a";

    private static string Span(long ms) => PluginKit.Seconds(TimeSpan.FromMilliseconds(ms));

    private static string Stamp(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static void Field(StringBuilder text, string name, string value, int width = 10) =>
        text.Append("  ").Append(name.PadRight(width)).Append(' ').Append(value).Append('\n');

    private static string Home(string? home) =>
        home ?? throw new DriverException(
            $"no Daoris home: a plugin lives under one. Set {DaorisHome.Variable}, or run the installed desktop once, which sets it.");

    /// <summary>The verb's one id, <c>--json</c>, and <c>--since</c> where it takes one; anything else refused by name.</summary>
    private static (string Id, bool Json, TimeSpan? Since) Parse(string[] args, string verb, bool takesSince)
    {
        string? id = null;
        var json = false;
        TimeSpan? since = null;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--json":
                    json = true;
                    break;
                case "--since" when takesSince:
                    if (i + 1 >= args.Length || MachineLogReader.ParseSince(args[i + 1]) is not { } span)
                    {
                        throw new DriverException("`--since` takes a span such as 30m, 2h or 3d.");
                    }

                    since = span;
                    i++;
                    break;
                default:
                    if (args[i].StartsWith("--", StringComparison.Ordinal) || id is not null)
                    {
                        throw new DriverException($"`{args[i]}` is not an option of `plugins {verb}`.\n{Usage}");
                    }

                    id = args[i];
                    break;
            }
        }

        return (id ?? throw new DriverException($"`plugins {verb}` needs a plugin's id — `daoris plugin list` shows what there is."), json, since);
    }
}
