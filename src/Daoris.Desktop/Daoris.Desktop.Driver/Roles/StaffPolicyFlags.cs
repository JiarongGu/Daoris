using System.Globalization;

namespace Daoris.Driver;

/// <summary>
/// The words of <c>daoris role policy</c> as a policy press: a draft (the stored policy
/// or today's order, or a preset's fill) with each flag laid over it, in the order typed. It judges nothing; the role door's judge does.
/// </summary>
public static class StaffPolicyFlags
{
    /// <exception cref="ChoiceWords">A flag that is not one, lacks its value or names a choice the role does not have.</exception>
    public static RolePolicyPress Read(IReadOnlyList<string> words, IReadOnlyList<ActorChoice> choices, StaffPolicyRead existing, string grammar)
    {
        if (words.Contains("--clear", StringComparer.Ordinal))
        {
            return words.Count == 1 ? new RolePolicyPress(Clear: true, Read: existing.Revision) : throw new ChoiceWords($"`--clear` stands alone: {grammar}.");
        }

        var kept = existing.Policy?.Rekeyed(choices);
        var at = words.ToList().IndexOf("--preset");
        var preset = at < 0 ? null : Value(words, at, grammar);
        if (preset is not null && !StaffPresets.Names.Contains(preset, StringComparer.Ordinal))
        {
            throw new ChoiceWords($"`{preset}` is not a preset — one of {string.Join(", ", StaffPresets.Names)}.");
        }

        var policy = preset is null ? kept ?? StaffPolicy.Preset(StaffPresets.First, choices, null) : StaffCapabilities.Preset(preset, choices, kept);

        for (var each = 0; each < words.Count; each++)
        {
            var flag = words[each];
            if (flag == "--preset")
            {
                each++;
                continue;
            }

            if (!flag.StartsWith("--", StringComparison.Ordinal)) throw new ChoiceWords($"`{flag}` is not a flag of `driver role policy`: {grammar}.");
            var value = Value(words, each++, grammar);
            policy = flag switch
            {
                "--rule" => policy with { Rule = value },
                "--weight" => Changed(policy, choices, value, grammar, (choice, number) => choice with { Weight = Whole(number, flag, grammar) }),
                "--share" => Changed(policy, choices, value, grammar, (choice, number) => choice with { Share = Whole(number, flag, grammar) }),
                "--budget" => Changed(policy, choices, value, grammar, (choice, number) => choice with { Budget = Budget(number, grammar) }),
                "--takes" => Changed(policy, choices, value, grammar, (choice, number) => choice with
                {
                    Takes = [.. number.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)],
                }),
                _ => throw new ChoiceWords($"`{flag}` is not a flag of `driver role policy`: {grammar}."),
            };
        }

        // A preset is carried by name too, so its limit move and bound are written as rules.
        return new RolePolicyPress(policy, preset, Read: existing.Revision);
    }

    /// <summary>The flags that make <paramref name="policy"/> from a blank draft (<c>--preset first</c>), as the verb reads them back.</summary>
    public static string Terminal(StaffPolicy policy, IReadOnlyList<ActorChoice> choices)
    {
        var flags = new List<string> { "--preset first", $"--rule {policy.Rule}" };
        for (var at = 0; at < choices.Count; at++)
        {
            if (policy.Of(StaffKey.Of(choices[at])) is not { } choice) continue;
            var n = at + 1;
            if (choice.Weight != 1) flags.Add($"--weight {n}={choice.Weight}");
            if (choice.Share is { } share) flags.Add($"--share {n}={share}");
            if (choice.Budget?.Starts is { } starts) flags.Add($"--budget {n}={starts}/day");
            if (choice.Takes is { } takes) flags.Add($"--takes {n}={string.Join(",", takes)}");
        }

        return string.Join(" ", flags);
    }

    /// <summary>The policy in a sentence: the rule, the weights as a proportion, and the limits set.</summary>
    public static string Said(StaffPolicy policy, IReadOnlyList<ActorChoice> choices)
    {
        var rule = policy.Rule == StaffRules.Weight
            ? "spread by weight " + string.Join(":", choices.Select(choice => policy.Of(StaffKey.Of(choice))?.Weight ?? 1))
            : "first that can run";
        var limits = policy.Choices.Select((choice, at) => (choice, at))
            .Where(each => each.choice.Share is not null || each.choice.Budget is not null || each.choice.Takes is not null)
            .Select(each => $"choice {each.at + 1}: " + string.Join(", ", new[]
            {
                each.choice.Share is { } share ? $"at most {share}%" : null,
                each.choice.Budget?.Starts is { } starts ? $"{starts} starts a day" : null,
                each.choice.Budget?.Tokens is { } tokens ? $"{tokens} reported tokens a day" : null,
                each.choice.Takes is { } takes ? "takes " + string.Join("/", takes) : null,
            }.OfType<string>()));
        return string.Join("; ", new[] { rule }.Concat(limits));
    }

    /// <summary>
    /// The policy in the fold's words, as a card and the room say it: <i>Spread by weight (3:1);
    /// choice 2: at most 40%</i>. No policy, or one in use by order, is <i>First that can run</i>.
    /// </summary>
    public static string Fold(StaffPolicy? policy, IReadOnlyList<ActorChoice> choices)
    {
        if (policy is null) return "First that can run";
        var parts = new List<string>
        {
            policy.Rule == StaffRules.Weight
                ? $"Spread by weight ({string.Join(":", choices.Select(choice => policy.Of(StaffKey.Of(choice))?.Weight ?? 1))})"
                : "First that can run",
        };
        parts.AddRange(policy.Choices.Select((choice, at) => (choice, at))
            .Where(each => each.choice.Share is not null || each.choice.Budget is not null || each.choice.Takes is not null)
            .Select(each => $"choice {each.at + 1}: " + string.Join(", ", new[]
            {
                each.choice.Share is { } share ? $"at most {share}%" : null,
                each.choice.Budget?.Starts is { } starts ? $"{starts} starts a day" : null,
                each.choice.Budget?.Tokens is { } tokens ? $"{tokens} reported tokens a day" : null,
                each.choice.Takes is { } takes ? "takes " + string.Join("/", takes) : null,
            }.OfType<string>())));
        return string.Join("; ", parts);
    }

    private static string Value(IReadOnlyList<string> words, int at, string grammar) =>
        at + 1 < words.Count ? words[at + 1] : throw new ChoiceWords($"`{words[at]}` needs its value: {grammar}.");

    private static int Whole(string text, string flag, string grammar) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : throw new ChoiceWords($"`{flag}` takes a whole number, not `{text}`: {grammar}.");

    private static StaffBudget Budget(string text, string grammar)
    {
        var count = text.EndsWith("/day", StringComparison.Ordinal) ? text[..^4] : text;
        return new StaffBudget(Starts: Whole(count, "--budget", grammar));
    }

    /// <summary>A flag's <c>&lt;n&gt;=&lt;value&gt;</c> laid on choice <c>n</c> (counted from 1) of the role's staffing.</summary>
    private static StaffPolicy Changed(StaffPolicy policy, IReadOnlyList<ActorChoice> choices, string given, string grammar, Func<StaffChoice, string, StaffChoice> change)
    {
        var split = given.IndexOf('=');
        if (split < 1 || split == given.Length - 1) throw new ChoiceWords($"`{given}` is not `<choice>=<value>`: {grammar}.");
        var number = Whole(given[..split], "the choice's number", grammar);
        if (number < 1 || number > choices.Count) throw new ChoiceWords($"the role has {choices.Count} choice{(choices.Count == 1 ? "" : "s")}; `{number}` is not one of them.");
        var key = StaffKey.Of(choices[number - 1]);
        return policy with { Choices = [.. policy.Choices.Select(choice => choice.Key == key ? change(choice, given[(split + 1)..]) : choice)] };
    }

}
