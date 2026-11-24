using System.Globalization;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// <c>daoris settings checks &lt;repository&gt; …</c>: a repository's check budgets on this machine, said or changed through
/// <see cref="CheckBudgetsDoor"/>, the judge the window's <c>SET_CHECK_BUDGETS</c> calls, so the two refuse alike. The word <c>checks</c> with a
/// whole number or <c>auto</c> (or alone) stays the machine's own setting (<see cref="ChecksVerb"/>); any other first word is a repository.
/// </summary>
public static class CheckBudgetsCommand
{
    public const string Usage =
        """
        daoris settings checks <repository>  (says its gates' timeouts, with where each comes from, and the rest of what this machine keeps for it)
        daoris settings checks <repository> pin <gate> <minutes> --why "…"  ·  pin <gate> --clear  (a timeout over what the green times teach)
        daoris settings checks <repository> memoryGb <gate> <gb>|--clear  ·  alone <gate> on|off  ·  timeoutMs <gate>/<set> <ms>|--clear
        daoris settings checks <repository> treeBaseline <command>|--clear  ·  devkit <version>|--clear
        daoris settings checks <repository> localInputs <path>…|--clear  ·  patternsFile <path>|--clear  (files under local/)
        """;

    /// <summary>Whether <paramref name="args"/> (the verb's, <c>checks</c> first) ask for a repository's budgets, not the machine's setting.</summary>
    public static bool Asks(IReadOnlyList<string> args) =>
        args.Count > 1 && args[0] == ChecksSetting.File && args[1] != ChecksSetting.Auto && !args[1].StartsWith("--", StringComparison.Ordinal)
        && !args[1].All(char.IsAsciiDigit);

    /// <summary>Whether the words change the file: they name a member.</summary>
    public static bool Changes(IReadOnlyList<string> args) => args.Count > 2;

    /// <summary>What the words ask, or a <see cref="ChoiceWords"/> saying what is wrong with them.</summary>
    /// <exception cref="ChoiceWords">The words are no member, name or value the door takes.</exception>
    public static CheckBudgetsAsk Read(IReadOnlyList<string> args)
    {
        var repository = args[1];
        var rest = args.Skip(2).ToList();
        var member = rest.Count > 0 ? rest[0] : null;
        if (member is not null && !CheckBudgetsDoor.Members.Contains(member, StringComparer.Ordinal))
        {
            throw new ChoiceWords($"`{member}` is no member of a repository's check budgets: {string.Join(", ", CheckBudgetsDoor.Members)}.\n{Usage}");
        }

        string? why = null;
        var clear = false;
        var words = new List<string>();
        for (var at = 1; at < rest.Count; at++)
        {
            if (rest[at] == "--clear") clear = true;
            else if (rest[at] == "--why" && at + 1 < rest.Count) why = rest[++at];
            else if (rest[at].StartsWith("--", StringComparison.Ordinal)) throw new ChoiceWords($"`{rest[at]}` is no flag of this verb: --clear, or --why <reason>.\n{Usage}");
            else words.Add(rest[at]);
        }

        // The gate or set a member is on comes first among its words; the value is what follows.
        var named = member is CheckBudgetsDoor.Pin or CheckBudgetsDoor.MemoryGb or CheckBudgetsDoor.Alone or CheckBudgetsDoor.TimeoutMs;
        var name = named && words.Count > 0 ? words[0] : null;
        var values = named ? words.Skip(1).ToList() : words;
        if (named && name is null) throw new ChoiceWords($"`{member}` is set on a {(member == CheckBudgetsDoor.TimeoutMs ? "set, as <gate>/<set>" : "gate")}: name it.\n{Usage}");
        return new CheckBudgetsAsk(repository, member, name, clear ? null : Value(member, values), clear, why);
    }

    private static JsonElement? Value(string? member, IReadOnlyList<string> words)
    {
        if (member is null) return null;
        if (words.Count == 0) throw new ChoiceWords($"`{member}` takes a value, or --clear to take it away.\n{Usage}");
        switch (member)
        {
            case CheckBudgetsDoor.Alone when words is [var flag]:
                return flag switch
                {
                    "on" or "true" => JsonSerializer.SerializeToElement(true),
                    "off" or "false" => JsonSerializer.SerializeToElement(false),
                    _ => throw new ChoiceWords("`alone` takes on or off."),
                };
            case CheckBudgetsDoor.Pin or CheckBudgetsDoor.MemoryGb or CheckBudgetsDoor.TimeoutMs when words is [var number]:
                return double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                    ? JsonSerializer.SerializeToElement(parsed)
                    : throw new ChoiceWords($"`{number}` is no number.");
            case CheckBudgetsDoor.LocalInputs:
                return JsonSerializer.SerializeToElement(words);
            case CheckBudgetsDoor.TreeBaseline:
                return JsonSerializer.SerializeToElement(string.Join(' ', words));
            case CheckBudgetsDoor.Devkit or CheckBudgetsDoor.PatternsFile when words is [var one]:
                return JsonSerializer.SerializeToElement(one);
            default:
                throw new ChoiceWords($"`{member}` takes one value.\n{Usage}");
        }
    }

    /// <summary>The change as the terminal's press: acts alone on the home, which the window's route writes through the same door.</summary>
    public static ChoicePress Plan(IReadOnlyList<string> args, DriverChoicesWorld world)
    {
        var ask = Read(args);
        var home = DriverConfig.HomeOf(world.ConfigPath);
        return new(ChecksSetting.File, null, null, _ => Task.FromResult<object?>(CheckBudgetsDoor.Apply(home, ask)), _ => null,
            view => Say((CheckBudgetsView)view!, world.Instance.Out));
    }

    /// <summary>The record said, where the words only read; the exit code.</summary>
    public static int Said(IReadOnlyList<string> args, DriverChoicesWorld world)
    {
        try
        {
            var view = CheckBudgetsDoor.View(DriverConfig.HomeOf(world.ConfigPath), args[1], null, null);
            return Say(view, world.Instance.Out);
        }
        catch (DriverException refused)
        {
            world.Instance.Error.WriteLine($"daoris: {refused.Message}");
            return 2;
        }
    }

    /// <summary>A repository's record in words: each gate's timeout and where it comes from, then what else the machine keeps.</summary>
    private static int Say(CheckBudgetsView view, TextWriter output)
    {
        output.WriteLine($"daoris: the check budgets this machine keeps for `{view.Repository}`:");
        if (view.Gates.Count == 0 && view.SetTimeoutMs.Count == 0 && view.TreeBaseline is null && view.Devkit is null && view.LocalInputs is null && view.PatternsFile is null)
        {
            output.WriteLine("  nothing: each gate runs under its lane's default timeout, learned from its green runs as they come.");
            return 0;
        }

        foreach (var (gate, each) in view.Gates)
        {
            var source = each.Budget.Source switch
            {
                "pin" => $"pinned: {each.Budget.Why}",
                "learned" => $"learned from {each.Greens} green run(s)",
                _ => "the lane's default",
            };
            var more = new[] { each.MemoryGb is { } gb ? $"{CheckBudgets.Said(gb)} GB" : null, each.Alone ? "runs alone" : null }.OfType<string>().ToList();
            output.WriteLine($"  gate {gate}: {CheckBudgets.Said(each.Budget.Minutes)} min ({source}){(more.Count > 0 ? "; " + string.Join(", ", more) : "")}");
        }

        foreach (var (set, timeout) in view.SetTimeoutMs) output.WriteLine($"  set {set}: {timeout} ms per test");
        if (view.TreeBaseline is not null) output.WriteLine($"  treeBaseline: {view.TreeBaseline}");
        if (view.Devkit is not null) output.WriteLine($"  devkit: {view.Devkit}");
        if (view.LocalInputs is not null) output.WriteLine($"  localInputs: {(view.LocalInputs.Count == 0 ? "none" : string.Join(", ", view.LocalInputs))}");
        if (view.PatternsFile is not null) output.WriteLine($"  patternsFile: {view.PatternsFile}");
        return 0;
    }
}
