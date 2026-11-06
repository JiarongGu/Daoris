using System.Text.Json.Serialization;

namespace Daoris.Driver;

/// <summary>
/// One rule a person sets for a place (CONTEXTDRIVE1c; the context counters design §4.1): when <see cref="Count"/> stands at
/// <see cref="At"/>, <see cref="Do"/> happens.
/// </summary>
/// <param name="Id">The rule's name within its place's set.</param>
/// <param name="Count">The ruled count's code.</param>
/// <param name="Window">The count's window.</param>
/// <param name="At">The threshold.</param>
/// <param name="Do">The action.</param>
/// <param name="UpTo">For <c>wait</c>, the most minutes it waits; none for any other action.</param>
/// <param name="Place"><c>machine</c>, <c>workspace:id</c>, <c>repository:id</c> or <c>line:key</c>.</param>
/// <param name="Roles">The working roles whose lines it watches; none is every role.</param>
public sealed record SupervisorRule(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("count")] string Count,
    [property: JsonPropertyName("window")] string Window,
    [property: JsonPropertyName("at")] int At,
    [property: JsonPropertyName("do")] string Do,
    [property: JsonPropertyName("upTo")] int? UpTo,
    [property: JsonPropertyName("place")] string Place,
    [property: JsonPropertyName("roles")] IReadOnlyList<string>? Roles);

/// <summary>A place's rules, written whole, with the revision they were written in (0 for a place that holds none).</summary>
public sealed record SupervisorRuleSet(string Place, long Revision, IReadOnlyList<SupervisorRule> Rules);

/// <summary>A write refused: the closed code (<see cref="SupervisorRules.Codes"/>), why, and the place and role it stands at where it names one.</summary>
public sealed record RuleRefusal(string Code, string Message, string? Place = null, string? Role = null);

/// <summary>What <see cref="SupervisorRules.Judge"/> weighs a write against besides the sets.</summary>
/// <param name="Roles">The catalogue's working role ids.</param>
/// <param name="ReferOffered">The agent supervisor's <c>refer</c> is offered.</param>
/// <param name="Known">Whether a place is one Daoris drives (a line on this machine); none accepts every well-formed place.</param>
/// <param name="Wider">The places a place's lines also read, narrow to wide and before the machine; none names only the place itself.</param>
public sealed record SupervisorRuleWorld(
    IReadOnlyList<string> Roles,
    bool ReferOffered = false,
    Func<string, bool>? Known = null,
    Func<string, IReadOnlyList<string>>? Wider = null);

/// <summary>
/// The rule supervisor's pure half (the context counters design §4.4, §4.5): the judge of a write, the effective rules of a line and the act
/// at a point. Nothing here reads a model or a store.
/// </summary>
public static class SupervisorRules
{
    public const string Machine = "machine";

    public const string Hold = "hold";
    public const string Ask = "ask";
    public const string Wait = "wait";
    public const string Next = "next";
    public const string Weighted = "weighted";
    public const string Refer = "refer";
    public const string Off = "off";

    public const string AnswersAlone = "answers-alone";
    public const string Failures = "failures";
    public const string FailuresOnChoice = "failures-on-choice";
    public const string Tries = "tries";
    public const string Quiet = "quiet";
    public const string Limits = "limits";
    public const string Moves = "moves";

    public const string EpisodeWindow = "episode";

    /// <summary>The ruled counts (§2.2) and the window each offers.</summary>
    public static IReadOnlyDictionary<string, string> Windows { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [AnswersAlone] = "in-a-row",
        [Failures] = "since-try-again",
        [FailuresOnChoice] = "in-a-row-one-choice",
        [Tries] = EpisodeWindow,
        [Quiet] = "this-end",
        [Limits] = "this-end",
        [Moves] = "24-hours",
    };

    /// <summary>The refusals, a closed set.</summary>
    public static IReadOnlyList<string> Codes { get; } =
        ["count", "window", "at", "do", "upTo", "place", "roles", "ladder", "moves-unbounded", "tries-unbounded", "refer-not-offered", "revision"];

    // The actions each count allows (§4.2); `off` is every ruled count's but tries'.
    private static readonly IReadOnlyDictionary<string, string[]> Allowed = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        [AnswersAlone] = [Hold, Refer, Off],
        [Failures] = [Hold, Refer, Off],
        [FailuresOnChoice] = [Ask, Next, Weighted, Refer, Off],
        [Tries] = [Hold, Ask, Next, Weighted, Refer],
        [Quiet] = [Next, Weighted, Refer, Off],
        [Limits] = [Ask, Wait, Next, Weighted, Refer, Off],
        [Moves] = [Ask, Refer, Off],
    };

    /// <summary>
    /// The places a line's rules come from, narrow to wide, ending at the machine: its repository's, its workspace's. A line has no place of its
    /// own: nothing reads one, and the judge refuses it.
    /// </summary>
    public static IReadOnlyList<string> Chain(string? repository, string? workspace)
    {
        var chain = new List<string>(3);
        if (repository is not null) chain.Add($"repository:{repository}");
        if (workspace is not null) chain.Add($"workspace:{workspace}");
        chain.Add(Machine);
        return chain;
    }

    /// <summary>
    /// A line's effective rules per count, for <paramref name="role"/> (none for a line with no working role). For each count the most
    /// specific place of <paramref name="chain"/> holding a rule on it that applies to the role decides, and its rules replace every wider
    /// place's, with no layering; within that place the rules naming the role replace those naming none. A place's <c>off</c> ends the
    /// inheritance: that count has no rule.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<SupervisorRule>> Effective(
        IReadOnlyList<SupervisorRuleSet> sets, IReadOnlyList<string> chain, string? role)
    {
        var effective = new Dictionary<string, IReadOnlyList<SupervisorRule>>(StringComparer.Ordinal);
        foreach (var count in Windows.Keys)
        {
            foreach (var place in chain)
            {
                var applying = sets.Where(set => set.Place == place).SelectMany(set => set.Rules)
                    .Where(rule => rule.Count == count && (rule.Roles is null || (role is not null && rule.Roles.Contains(role)))).ToList();
                if (applying.Count == 0) continue;
                var named = applying.Where(rule => rule.Roles is not null).ToList();
                var decided = named.Count > 0 ? named : applying;
                if (decided.All(rule => rule.Do != Off)) effective[count] = [.. decided.OrderBy(rule => rule.At)];
                break;
            }
        }

        return effective;
    }

    // The narrowest place is judged first, so a refusal names the place a wider write left unbounded.
    private static int Specificity(string place) =>
        place.StartsWith("repository:", StringComparison.Ordinal) ? 1 : place == Machine ? 3 : 2;

    /// <summary>
    /// Judge a place's rules, written whole, before anything is written: null where they stand, else the refusal. <paramref name="held"/>
    /// is every place's set as stored; the bounds are judged on the effective set of every place holding a rule and the machine, for each
    /// role and for none, as they would stand after the write, so a wider place's write that leaves a narrower place unbounded is refused,
    /// naming it.
    /// </summary>
    public static RuleRefusal? Judge(string place, long revision, IReadOnlyList<SupervisorRule> rules, IReadOnlyList<SupervisorRuleSet> held, SupervisorRuleWorld world)
    {
        var current = held.FirstOrDefault(set => set.Place == place)?.Revision ?? 0;
        if (revision != current) return new RuleRefusal("revision", $"{place}'s rules moved to revision {current}; read them again.", place);
        if (place.StartsWith("line:", StringComparison.Ordinal)) return new RuleRefusal("place", $"`{place}` is a line: its rules are its repository's, its workspace's or the machine's.", place);
        if (!PlaceReads(place) || world.Known?.Invoke(place) == false) return new RuleRefusal("place", $"`{place}` is not a place Daoris drives.", place);
        foreach (var rule in rules)
        {
            if (Shape(rule, place, world) is { } refusal) return refusal;
        }

        // Every executor acts on one rule per count, so a second rule a line would read beside the first (a ladder) is refused.
        var ladder = rules.Select((rule, at) => (rule, at)).FirstOrDefault(each => rules.Skip(each.at + 1).Any(other =>
            other.Count == each.rule.Count && SameTier(each.rule.Roles, other.Roles))).rule;
        if (ladder is not null)
        {
            return new RuleRefusal("ladder", $"{ladder.Count} takes one rule here for its roles: another rule there would also apply.", place);
        }

        var after = held.Where(set => set.Place != place).Append(new SupervisorRuleSet(place, current + 1, rules)).ToList();
        var places = after.Where(set => set.Rules.Count > 0).Select(set => set.Place).Append(Machine).Distinct(StringComparer.Ordinal)
            .OrderBy(Specificity).ThenBy(each => each, StringComparer.Ordinal);
        foreach (var each in places)
        {
            var chain = world.Wider?.Invoke(each) is { } wider ? [each, .. wider.Where(item => item != each), Machine] : each == Machine ? [Machine] : (IReadOnlyList<string>)[each, Machine];
            foreach (var role in world.Roles.Cast<string?>().Append(null))
            {
                var effective = Effective(after, chain, role);
                var within = role ?? "no role";
                if (effective.TryGetValue(FailuresOnChoice, out var choice)
                    && effective.TryGetValue(Failures, out var failures)
                    && choice.Any(rule => rule.Do != Off && rule.At >= failures.Min(item => item.At)))
                {
                    return new RuleRefusal("at", $"failures-on-choice acts before failures does at {each} for {within}.", each, role);
                }

                if (!effective.ContainsKey(Tries))
                {
                    return new RuleRefusal("tries-unbounded", $"{each} would leave {within}'s lines with no tries rule: a transient end would start again without end.", each, role);
                }

                if (effective.Values.SelectMany(item => item).Any(rule => rule.Do is Next or Weighted) && !effective.ContainsKey(Moves))
                {
                    return new RuleRefusal("moves-unbounded", $"{each} would leave {within}'s lines moving to another choice with no moves rule.", each, role);
                }
            }
        }

        return null;
    }

    // Two rules a line reads together: both for every role, or both naming one role; a rule naming the role beats one naming none.
    private static bool SameTier(IReadOnlyList<string>? left, IReadOnlyList<string>? right) =>
        left is null ? right is null : right is not null && left.Intersect(right).Any();

    private static bool PlaceReads(string place) =>
        place == Machine || ((place.StartsWith("workspace:", StringComparison.Ordinal) || place.StartsWith("repository:", StringComparison.Ordinal))
                             && place.IndexOf(':') < place.Length - 1);

    private static RuleRefusal? Shape(SupervisorRule rule, string place, SupervisorRuleWorld world)
    {
        RuleRefusal Refuse(string code, string why) => new(code, $"rule `{rule.Id}`: {why}", place);
        if (rule.Place != place) return Refuse("place", $"it names {rule.Place}, not the place it is written for.");
        if (!Windows.TryGetValue(rule.Count, out var window)) return Refuse("count", $"`{rule.Count}` is not a ruled count.");
        if (rule.Window != window) return Refuse("window", $"{rule.Count} offers `{window}`.");
        if (rule.Roles is { } roles && (roles.Count == 0 || roles.Distinct().Count() != roles.Count || roles.Any(role => !world.Roles.Contains(role))))
        {
            return Refuse("roles", "a role is not in the catalogue, named twice, or none is named.");
        }

        if (!Allowed[rule.Count].Contains(rule.Do)) return Refuse("do", $"{rule.Count} does not allow `{rule.Do}`.");
        if (rule.Do == Refer && !world.ReferOffered) return Refuse("refer-not-offered", "asking the supervisor is not offered yet.");
        if (rule.Do == Wait ? rule.UpTo is not (>= 5 and <= 600) : rule.UpTo is not null)
        {
            return Refuse("upTo", rule.Do == Wait ? "wait takes 5 to 600 minutes." : "only wait takes upTo.");
        }

        var (low, high) = Bounds(rule.Count);
        return rule.At < low || rule.At > high ? Refuse("at", $"{rule.Count} takes {low} to {high}.") : null;
    }

    /// <summary>The actions <paramref name="count"/> allows (§4.2), <c>refer</c> among them though it is offered only once the actor is.</summary>
    public static IReadOnlyList<string> Actions(string count) => Allowed.TryGetValue(count, out var actions) ? actions : [];

    /// <summary>The <c>at</c> a rule on <paramref name="count"/> takes (§4.1); failures in a row on one choice below the failures rule's, judged apart.</summary>
    public static (int Low, int High) Bounds(string count) => count switch
    {
        AnswersAlone or Failures => (1, 20),
        FailuresOnChoice => (1, 19),
        Tries => (1, 5),
        Moves => (1, 8),
        _ => (1, 1),
    };
}
