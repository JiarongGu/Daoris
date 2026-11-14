using System.Globalization;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// A reached runner's verdict as a check's: its exit and the last outcome line of its log, never a guess. A clean exit is reached, a red class
/// fails the check naming the class, and any other exit is unknown. The why always says the classes run, replayed and red, and names a flake
/// (it passed when run alone again) or a class main already stands red, so neither is silent.
/// </summary>
internal static class ReachedVerdict
{
    /// <summary>The one gate a reached run is shown as.</summary>
    internal const string Gate = "reached";

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
        return exit == 0
            ? new VerifyResult(invocation, commit, VerifyVerdicts.Reached, said, [new VerifyGate(Gate, RunnerVerdicts.Pass, 0) { Log = logPath }])
            : new VerifyResult(invocation, commit, VerifyVerdicts.Failed, said, [new VerifyGate(Gate, RunnerVerdicts.Fail, exit) { Log = logPath }]) { Failed = Gate };
    }

    private sealed record Outcome(int Ran, int Replayed, IReadOnlyList<string> Flaked, IReadOnlyList<string> MainRed, IReadOnlyList<string> Red);

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
            return root.ValueKind == JsonValueKind.Object ? new Outcome(Count("ran"), Count("replayed"), Names("flaked"), Names("mainRed"), Names("red")) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
