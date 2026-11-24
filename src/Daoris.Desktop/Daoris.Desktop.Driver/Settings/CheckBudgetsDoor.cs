using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// One change the owner's door makes to a repository's check budgets: the member, the gate or set it is on, its value or its clearing,
/// and the reason where it is a pin. The window's <c>SET_CHECK_BUDGETS</c> payload and the words of <c>daoris settings checks</c> both
/// make this, and <see cref="CheckBudgetsDoor.Apply"/> judges it, so the two refuse alike.
/// </summary>
/// <param name="Member">One of <see cref="CheckBudgetsDoor.Members"/>.</param>
/// <param name="Name">The gate (<c>pin</c>, <c>memoryGb</c>, <c>alone</c>) or the set as <c>&lt;gate&gt;/&lt;set&gt;</c> (<c>timeoutMs</c>); none for the rest.</param>
/// <param name="Value">A number (<c>pin</c>'s minutes, <c>memoryGb</c>, <c>timeoutMs</c>), a boolean (<c>alone</c>), a string (<c>treeBaseline</c>, <c>devkit</c>, <c>patternsFile</c>) or a list of strings (<c>localInputs</c>).</param>
/// <param name="Clear">Takes the member away (a pin: its unpin; <c>alone</c>: false) instead of setting it.</param>
public sealed record CheckBudgetsAsk(string? Repository, string? Member, string? Name, JsonElement? Value, bool Clear, string? Why);

/// <summary>One gate as the door shows it: its budget and where that comes from, how many greens teach it, and what the machine set.</summary>
public sealed record GateBudgetView(GateTimeout Budget, int Greens, GatePin? Pin, double? MemoryGb, bool Alone);

/// <summary>A repository's record as the door shows it, each gate with its derived budget; a member the home does not hold is null.</summary>
public sealed record CheckBudgetsView(
    string Repository, IReadOnlyDictionary<string, GateBudgetView> Gates, IReadOnlyDictionary<string, long> SetTimeoutMs, string? TreeBaseline,
    string? Devkit, IReadOnlyList<string>? LocalInputs, string? PatternsFile);

/// <summary>
/// The one door that writes a repository's check budgets: the judge both the window's route and
/// <c>daoris settings checks</c> call before anything is written, and the record it answers with.
/// </summary>
public static class CheckBudgetsDoor
{
    public const string Pin = "pin";
    public const string MemoryGb = "memoryGb";
    public const string Alone = "alone";
    public const string TimeoutMs = "timeoutMs";
    public const string TreeBaseline = "treeBaseline";
    public const string Devkit = "devkit";
    public const string LocalInputs = "localInputs";
    public const string PatternsFile = "patternsFile";

    /// <summary>Every member the door sets, in the order it says them.</summary>
    public static IReadOnlyList<string> Members { get; } = [Pin, MemoryGb, Alone, TimeoutMs, TreeBaseline, Devkit, LocalInputs, PatternsFile];

    /// <summary>
    /// The change made, judged and written, answered as the repository's record stands after it.
    /// </summary>
    /// <exception cref="DriverException">The refusal, nothing written: no repository, a member the door does not set, a gate or set not named, a
    /// value of the wrong kind or out of its bounds, a pin with no reason, or a file that does not read.</exception>
    public static CheckBudgetsView Apply(string home, CheckBudgetsAsk ask, string? lane = null)
    {
        var repository = ask.Repository?.Trim() ?? "";
        if (repository.Length == 0) throw new DriverException("a repository's check budgets are set for a repository: name one.");
        var name = ask.Name?.Trim() ?? "";
        switch (ask.Member)
        {
            case Pin when ask.Clear:
                CheckBudgets.ClearPin(home, repository, name);
                break;
            case Pin:
                CheckBudgets.SetPin(home, repository, name, Number(ask), ask.Why);
                break;
            case MemoryGb:
                CheckBudgets.SetMemoryGb(home, repository, name, ask.Clear ? null : Number(ask));
                break;
            case Alone:
                CheckBudgets.SetAlone(home, repository, name, !ask.Clear && Flag(ask));
                break;
            case TimeoutMs:
                CheckBudgets.SetSetTimeout(home, repository, name, ask.Clear ? null : Whole(ask));
                break;
            case TreeBaseline:
                CheckBudgets.SetTreeBaseline(home, repository, ask.Clear ? null : Text(ask));
                break;
            case Devkit:
                CheckBudgets.SetDevkit(home, repository, ask.Clear ? null : Text(ask));
                break;
            case PatternsFile:
                CheckBudgets.SetPatternsFile(home, repository, ask.Clear ? null : Text(ask));
                break;
            case LocalInputs:
                CheckBudgets.SetLocalInputs(home, repository, ask.Clear ? null : Texts(ask));
                break;
            default:
                throw new DriverException($"`{ask.Member}` is no member of a repository's check budgets: {string.Join(", ", Members)}.");
        }

        return View(home, repository, null, lane);
    }

    /// <summary>
    /// The repository's record with each gate's derived budget: the gates the home keeps something for, and those named in
    /// <paramref name="gates"/> (the declaration's), each as the lane <paramref name="lane"/> derives it.
    /// </summary>
    public static CheckBudgetsView View(string home, string repository, IEnumerable<string>? gates, string? lane)
    {
        var kept = CheckBudgets.Of(home, repository);
        var each = new SortedDictionary<string, GateBudgetView>(StringComparer.Ordinal);
        foreach (var gate in kept.Gates.Keys.Concat(gates ?? []).Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name.Trim()))
        {
            var record = kept.Gate(gate);
            each[gate] = new GateBudgetView(CheckBudgets.TimeoutOf(record, lane), record.Green.Count, record.Pin, record.MemoryGb, record.Alone);
        }

        return new CheckBudgetsView(
            repository.Trim(), each, new SortedDictionary<string, long>(kept.SetTimeoutMs.ToDictionary(pair => pair.Key, pair => pair.Value), StringComparer.Ordinal),
            kept.TreeBaseline, kept.Devkit, kept.LocalInputs, kept.PatternsFile);
    }

    private static double Number(CheckBudgetsAsk ask) =>
        ask.Value is { ValueKind: JsonValueKind.Number } value && value.TryGetDouble(out var number) ? number : throw Takes(ask, "a number");

    private static long Whole(CheckBudgetsAsk ask) =>
        ask.Value is { ValueKind: JsonValueKind.Number } value && value.TryGetInt64(out var number) ? number : throw Takes(ask, "a whole number");

    private static bool Flag(CheckBudgetsAsk ask) => ask.Value switch
    {
        { ValueKind: JsonValueKind.True } => true,
        { ValueKind: JsonValueKind.False } => false,
        _ => throw Takes(ask, "true or false"),
    };

    private static string Text(CheckBudgetsAsk ask) =>
        ask.Value is { ValueKind: JsonValueKind.String } value && value.GetString() is { } text ? text : throw Takes(ask, "text");

    private static List<string> Texts(CheckBudgetsAsk ask) =>
        ask.Value is { ValueKind: JsonValueKind.Array } list && list.EnumerateArray().All(each => each.ValueKind == JsonValueKind.String)
            ? [.. list.EnumerateArray().Select(each => each.GetString()!)]
            : throw Takes(ask, "a list of paths");

    private static DriverException Takes(CheckBudgetsAsk ask, string kind) => new($"`{ask.Member}` takes {kind}, or --clear to take it away.");
}
