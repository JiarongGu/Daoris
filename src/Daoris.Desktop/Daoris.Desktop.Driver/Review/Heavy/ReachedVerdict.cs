using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// A reached runner's verdict as a check's: its exit and the last outcome line of its log, never a guess. A clean exit is reached, a red class
/// fails the check naming the class, and any other exit is unknown. The why always says the classes run, replayed and red, and names a flake
/// (it passed when run alone again) or a class main already stands red, so neither is silent; a red class first, with its log's lines.
/// </summary>
internal static class ReachedVerdict
{
    /// <summary>The one gate a reached run is shown as.</summary>
    internal const string Gate = "reached";

    private const int RedLines = 12;

    private const int LineChars = 400;

    private const string Gap = "…";

    private static readonly string[] RunnerLines = ["reached:", "reached-step:", "reached-classes:"];

    private static readonly string[] FailureWords = ["FAIL", "Failed", "failed", "✖", "×", "not ok", "Error"];

    internal static VerifyResult Judge(string invocation, string commit, int exit, string log, string logPath, string marker)
    {
        var outcome = OutcomeOf(log, marker);
        if (exit != 0 && outcome is not { Red.Count: > 0 } && exit != 1)
        {
            return new VerifyResult(invocation, commit, VerifyVerdicts.Unknown, $"the reached runner exited {exit.ToString(CultureInfo.InvariantCulture)}, which is neither a pass nor a failure",
                [new VerifyGate(Gate, RunnerVerdicts.Ended, exit) { Log = logPath }]);
        }

        var seen = outcome ?? new Outcome(0, 0, [], [], []);
        var said = Summary(seen);
        if (exit != 0 && seen.Red.Count > 0) said += "\n" + Failures(log, seen.Red, logPath);
        return exit == 0
            ? new VerifyResult(invocation, commit, VerifyVerdicts.Reached, said, [new VerifyGate(Gate, RunnerVerdicts.Pass, 0) { Log = logPath }])
            : new VerifyResult(invocation, commit, VerifyVerdicts.Failed, said, [new VerifyGate(Gate, RunnerVerdicts.Fail, exit) { Log = logPath }]) { Failed = Gate, Red = seen.Red };
    }

    /// <summary>A reached command's outcome, null with no outcome line: each red class first, its failure lines where <paramref name="path"/> is given.</summary>
    internal static string? Told(string log, string marker, string? path = null)
    {
        if (OutcomeOf(log, marker) is not { } outcome) return null;
        var said = new StringBuilder("the reached classes: ");
        if (outcome.Red.Count > 0) said.Append(outcome.Red.Count).Append(" red, failed again alone: ").Append(string.Join(", ", outcome.Red)).Append("; ");
        said.Append(CultureInfo.InvariantCulture, $"{outcome.Ran} run, {outcome.Replayed} replayed from passes of the same inputs");
        if (outcome.Red.Count == 0) said.Append(", 0 red");
        if (outcome.Flaked.Count > 0) said.Append("; passed on a second run alone: ").Append(string.Join(", ", outcome.Flaked));
        if (outcome.MainRed.Count > 0) said.Append("; red on main already: ").Append(string.Join(", ", outcome.MainRed));
        if (outcome.Deferred.Count > 0)
        {
            var bySuite = outcome.Deferred.GroupBy(id => id.Split('|')[0], StringComparer.Ordinal).Select(group => $"{group.Key} {group.Count()}");
            said.Append(CultureInfo.InvariantCulture, $"; {outcome.Deferred.Count} heavy-tier class{(outcome.Deferred.Count == 1 ? "" : "es")} owed after the landing ({string.Join(", ", bySuite)})");
        }

        return outcome.Red.Count > 0 && path is not null ? $"{said}\n{Failures(log, outcome.Red, path)}" : said.ToString();
    }

    /// <summary>Each red class's last failure line in the log (the runner's own lines aside) and those under it to a blank line, within the
    /// excerpt's bounds (<see cref="GateLogExcerpt"/>).</summary>
    private static string Failures(string log, IReadOnlyList<string> red, string path)
    {
        var lines = log.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Select(line => line.TrimEnd()).ToList();
        var said = new StringBuilder($"its log, {path}, fails them on:");
        var kept = 0;
        foreach (var id in red)
        {
            var name = NameOf(id);
            var named = Enumerable.Range(0, lines.Count).Where(at => !Runner(lines[at]) && lines[at].Contains(name, StringComparison.Ordinal)).ToList();
            var from = named.LastOrDefault(at => FailureWords.Any(word => lines[at].Contains(word, StringComparison.Ordinal)), named.Count > 0 ? named[^1] : -1);
            said.Append('\n').Append(id).Append(':');
            if (from < 0)
            {
                said.Append(" its log names no failure of it");
                continue;
            }

            for (var at = from; at < lines.Count && at < from + RedLines && (at == from || lines[at].Length > 0) && !Runner(lines[at]); at++)
            {
                var line = lines[at].Length > LineChars ? lines[at][..LineChars] + Gap : lines[at];
                if (kept >= GateLogExcerpt.Lines || Encoding.UTF8.GetByteCount(said.ToString()) + Encoding.UTF8.GetByteCount(line) > GateLogExcerpt.Bytes)
                {
                    return said.Append('\n').Append(Gap).ToString();
                }

                said.Append('\n').Append(line);
                kept++;
            }
        }

        return said.ToString();
    }

    private static string NameOf(string id)
    {
        var unit = id.Contains('|', StringComparison.Ordinal) ? id[(id.IndexOf('|', StringComparison.Ordinal) + 1)..] : id;
        var slash = unit.LastIndexOfAny(['/', '\\']);
        return slash >= 0 ? unit[(slash + 1)..] : unit;
    }

    private static bool Runner(string line) => RunnerLines.Any(prefix => line.StartsWith(prefix, StringComparison.Ordinal));

    private sealed record Outcome(int Ran, int Replayed, IReadOnlyList<string> Flaked, IReadOnlyList<string> MainRed, IReadOnlyList<string> Red)
    {
        public IReadOnlyList<string> Deferred { get; init; } = [];
    }

    private static string Summary(Outcome outcome)
    {
        var said = $"{outcome.Ran} classes run, {outcome.Replayed} replayed, {outcome.Red.Count} red";
        if (outcome.Red.Count > 0) said += $": {string.Join(", ", outcome.Red)}";
        if (outcome.Flaked.Count > 0) said += $"; passed on a second run alone: {string.Join(", ", outcome.Flaked)}";
        if (outcome.MainRed.Count > 0) said += $"; red on main already: {string.Join(", ", outcome.MainRed)}";
        return said;
    }

    private static Outcome? OutcomeOf(string log, string marker)
    {
        var line = log.Split('\n').Select(each => each.TrimEnd('\r')).LastOrDefault(each => each.StartsWith(marker, StringComparison.Ordinal));
        if (line is null) return null;
        try
        {
            using var document = JsonDocument.Parse(line[marker.Length..]);
            var root = document.RootElement;
            int Count(string name) => root.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : 0;
            IReadOnlyList<string> Names(string name) => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
                ? [.. value.EnumerateArray().Where(each => each.ValueKind == JsonValueKind.String).Select(each => each.GetString()!)]
                : [];
            return root.ValueKind == JsonValueKind.Object
                ? new Outcome(Count("ran"), Count("replayed"), Names("flaked"), Names("mainRed"), Names("red")) { Deferred = Names("deferred") }
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
