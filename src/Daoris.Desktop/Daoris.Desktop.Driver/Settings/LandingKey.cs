using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// The forms a landing rule takes (WSR1, D87). Pushing and opening a pull request is not a form of its
/// own: a branch rule names the plugin that does it (WSR4, D100).
/// </summary>
public static class LandingForm
{
    /// <summary>Into the repository's line (D86), in its own checkout — the merge door as it always was.</summary>
    public const string Merge = "merge";

    /// <summary>Onto a new branch named by a pattern, from the session's branch, for the person — or the rule's plugin — to push.</summary>
    public const string Branch = "branch";
}

/// <summary>
/// How a session's work lands: a form, for <see cref="LandingForm.Branch"/> the pattern that names the
/// branch, and whether the tree and its branch go once a press lands the work (<paramref name="Tidy"/>,
/// D88) — only where git proves the work is on a branch of the person's.
/// </summary>
/// <param name="Plugin">
/// The plugin a branch rule hands the new branch to (WSR4, D100): spoken to on <see cref="HookPoints.Land"/>
/// once the branch exists, it pushes and opens the pull request for its platform. Null — the default — is
/// the branch left for the person to push, as it always was.
/// </param>
/// <param name="AutoAccept">
/// <i>Accept automatically</i> (LAND2a, D145): a quest's done lands its work as the person's Accept would, and the rule's
/// plugin pushes it and opens the pull request with no press, the pull request being the last human step. Only a branch
/// rule takes it; off, the default, is the press as it always was. It rides the rule, so a repository's own rule replaces
/// its workspace's switch with the rest of it. LAND2b acts on it: a session concluding on a done quest becomes due
/// (<see cref="AutoLandings"/>), and the next look lands it (<see cref="AutoLander"/>).
/// </param>
/// <param name="Key">
/// The shape of the repository's ticket keys (PROCESS1b-i, D164's amendment point 2): a regular expression the pattern's
/// <c>{ticket}</c> is found by, in the title, then the request, then its links (<see cref="LandingRules.Ticket"/>). Null is
/// <see cref="LandingRules.DefaultKey"/>. Only a branch rule whose pattern says <c>{ticket}</c> takes one.
/// </param>
/// <param name="Squash">
/// The pull request's branch as one commit (PROCESS1b-ii, D164's amendment point 2 and its PROCESS1b-ii note): Accept makes it
/// from the session's tree, its parent where the session left the line, its message the quest's title and the session's commits'
/// subjects (<see cref="SquashMessage"/>), with git's own identity in the repository. Only a branch rule takes it; off, the
/// default, is the branch at the session's own commits, as it always was. A tree on a branch of its own is never squashed.
/// </param>
public sealed record LandingRule(
    string Form, string? Pattern = null, bool Tidy = false, string? Plugin = null, bool AutoAccept = false, string? Key = null,
    bool Squash = false)
{
    public static readonly LandingRule Merge = new(LandingForm.Merge);

    /// <summary>
    /// The work's cycle after the landing (FABRIC-GRAPH1; the fabric events design §C2, §E7): a graph fragment <c>{ nodes, wires, entries,
    /// end, cleanup }</c>, its JSON text as the file holds it, compact. Null is the default, today's: ended when the line holds the work,
    /// the cleanup at <i>held</i>. It rides the rule (D164's amendment), so a repository's own rule replaces its workspace's cycle with the
    /// rest of it; a door with no field for it keeps it (<see cref="DriverConfig.WithLanding"/>). <see cref="NodeGraphHomes"/> reads it.
    /// </summary>
    public string? Cycle { get; init; }
}

/// <summary>
/// What a pattern is expanded from: the session's quest (or the session itself), its title's words, the repository, and the
/// ticket its rule's key found (PROCESS1b-i), the session standing in where it found none.
/// </summary>
public sealed record LandingNames(string Quest, string Slug, string Repository, string Session, string? Ticket = null);

/// <summary>
/// The landing rule as <c>driver.json</c> keeps it, <c>landings</c> and <c>workspaceLandings</c> (WSR1, D87; FOUNDATION1 mod-iii): the
/// key's shape, its limits and the sentences that say what does not read, what a pattern may say and what it names, read and written by
/// the settings' own parser. What a rule means, its choice, press and the plugin that lands by it, stays with the landing
/// (<see cref="LandingRules"/>), which answers through this for what it shares.
/// </summary>
public static class LandingKey
{
    /// <summary>
    /// What a pattern may say. One of the first three is required, or every session's work would go to one branch: a ticket names
    /// a branch as the quest does (PROCESS1b-i), two sessions on one ticket meeting the second's <c>exists</c> refusal.
    /// </summary>
    public static readonly IReadOnlyList<string> Placeholders = ["quest", "session", "ticket", "slug", "repository"];

    /// <summary>What a pattern is tried against before it is kept — the CLI's twin tries the same names.</summary>
    private static readonly LandingNames Sample = new("0fda18", "sample-work", "engine", "s1a2b3c4", "ABC-123");

    private static readonly Regex Placeholder = new(@"\{([^{}]*)\}", RegexOptions.CultureInvariant);

    /// <summary>
    /// The shape of a ticket's key where the rule names none (PROCESS1b-i, D164's amendment point 2): a project's capitals, a dash
    /// and its number, as <c>TK-2202</c>.
    /// </summary>
    public const string DefaultKey = @"[A-Z][A-Z0-9]+-\d+";

    /// <summary>How long a key may be: a shape, not a program. The CLI's twin holds the same bound.</summary>
    public const int KeyLimit = 64;

    /// <summary>
    /// The letters a key may escape (PROCESS1b-i): the classes and controls .NET and JavaScript read alike. <c>\x</c>, <c>\u</c>,
    /// <c>\c</c>, <c>\p</c> and the rest differ, or are refused by one engine. The CLI's <c>SHARED_ESCAPES</c> is the same list.
    /// </summary>
    public const string SharedEscapes = "dDwWsSbBnrtfv";

    /// <summary>How long one search for a key may run before its words are passed over: a key is the person's, and need not be quick.</summary>
    internal static readonly TimeSpan KeyTimeout = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// What is wrong with a rule's shape, in a sentence, or null when it can land work. Whether its plugin
    /// is on this machine is <see cref="PluginProblem"/>'s question: a file stays readable wherever it is.
    /// </summary>
    public static string? Problem(LandingRule rule) => rule.Form switch
    {
        // A merge makes no branch, and the plugin starts from the branch Daoris made (D100).
        LandingForm.Merge when rule.Plugin is not null =>
            "only a branch rule hands its work to a plugin — the plugin pushes the branch Daoris made, and a merge "
            + "makes none. `branch <pattern> --plugin <id>` is the form that does.",
        // A merge writes into the person's checkout, and with no press nothing would stand between the work and the
        // line (D145 point 1, D51 rule 6).
        LandingForm.Merge when rule.AutoAccept =>
            "only a branch rule accepts automatically — a merge writes into your checkout, and with no press nothing would "
            + "stand between the work and the line. `branch <pattern> --auto-accept` is the form that does.",
        // A ticket's key names a branch (PROCESS1b-i), and a merge makes none.
        LandingForm.Merge when rule.Key is not null =>
            "only a branch rule reads a ticket's key — a merge makes no branch to name. `branch <pattern> --key <shape>` is the form that does.",
        // A squash makes the pull request's branch one commit (PROCESS1b-ii), and a merge makes no branch.
        LandingForm.Merge when rule.Squash =>
            "only a branch rule squashes — a merge makes no branch to squash the work onto. `branch <pattern> --squash` is the form that does.",
        LandingForm.Merge => null,
        // A plugin's id is the catalogue's own shape, asked of its one owner (REFAC1), so a rule never names a path.
        LandingForm.Branch => Problem(rule.Pattern ?? "") ?? (rule.Plugin is { } plugin && !PluginIds.IsId(plugin)
            ? $"`{plugin}` is not a plugin id — one is lowercase letters, digits, dots and dashes, like `example.github-pull-request`."
            : null) ?? KeyProblem(rule),
        _ => $"`{rule.Form}` is not a way work lands here — `{LandingForm.Merge}` or `{LandingForm.Branch}`. "
             + "Pushing and opening a pull request is a plugin's to do, named on the branch form: "
             + "`branch <pattern> --plugin <id>`.",
    };

    /// <summary>What is wrong with a branch pattern, in a sentence, or null when it names one branch per session.</summary>
    public static string? Problem(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return "a branch pattern needs a name — e.g. `feature/{quest}-{slug}`.";
        }

        foreach (Match used in Placeholder.Matches(pattern))
        {
            if (!Placeholders.Contains(used.Groups[1].Value))
            {
                return $"`{used.Value}` is not something a pattern can say — "
                    + string.Join(", ", Placeholders.Select(name => $"`{{{name}}}`")) + ".";
            }
        }

        // A ticket names a branch as a quest does (PROCESS1b-i); the sentence still offers the two that are one per session.
        if (!pattern.Contains("{quest}", StringComparison.Ordinal) && !pattern.Contains("{session}", StringComparison.Ordinal)
            && !pattern.Contains("{ticket}", StringComparison.Ordinal))
        {
            return "a pattern needs `{quest}` or `{session}`, or every session's work would be put on one branch.";
        }

        var sample = Expand(pattern, Sample);
        return sample.Contains('{') || sample.Contains('}') || !BranchName.IsValid(sample)
            ? $"`{pattern}` does not make a branch name git would take — it gives `{sample}`."
            : null;
    }

    /// <summary>
    /// What is wrong with a rule's ticket key, in a sentence, or null where it has none or can name a branch (PROCESS1b-i): read
    /// only for <c>{ticket}</c>, and held to <see cref="KeyProblem(string)"/>'s shape.
    /// </summary>
    private static string? KeyProblem(LandingRule rule) => rule.Key is not { } key ? null
        : !(rule.Pattern ?? "").Contains("{ticket}", StringComparison.Ordinal)
            ? $"a ticket's key is read only for `{{ticket}}`, and `{rule.Pattern}` does not say it — add `{{ticket}}` to the pattern, or take the key away."
            : KeyProblem(key);

    /// <summary>
    /// What is wrong with a ticket's key, in a sentence, or null where both readers of a rule take it the same way (PROCESS1b-i):
    /// the driver matches it in .NET and the CLI checks it in JavaScript, so it is held to what the two engines read alike — at
    /// most <see cref="KeyLimit"/> characters, no <c>(?</c> group (lookaround, named group, inline option), no back-reference,
    /// a letter escaped only as one of <see cref="SharedEscapes"/> (a punctuation mark may be escaped), and no empty class. Each
    /// engine's own compile is the last word. The CLI's <c>keyProblem</c> is the twin, sentence for sentence.
    /// </summary>
    /// <remarks>
    /// 🔴 KEYPARITY: a key one engine takes and the other refuses was written by the CLI and then not read by the driver
    /// (<c>TK\_\d+</c>, <c>\x</c> with no digits, <c>[^]</c>); <c>[]a]</c> compiles in both and means two things.
    /// </remarks>
    public static string? KeyProblem(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return $"a ticket's key needs a shape — e.g. `{DefaultKey}`, the one read where a rule names none.";
        }

        if (key.Length > KeyLimit) return $"a ticket's key is at most {KeyLimit} characters, and this one is {key.Length}.";

        var inClass = false;
        for (var at = 0; at < key.Length; at++)
        {
            var next = at + 1 < key.Length ? key[at + 1] : '\0';
            if (key[at] == '\\')
            {
                if (next is 'k' or (>= '1' and <= '9'))
                {
                    return "a ticket's key may not refer back to a group (`\\k` or `\\1` to `\\9`), which the driver and the CLI read differently.";
                }

                // A punctuation mark or symbol escaped is itself in both; `_`, a digit, a space and any other letter are not read alike.
                var punctuation = next is > ' ' and < '\x7f' && !char.IsAsciiLetterOrDigit(next) && next != '_';
                if (next != '\0' && !SharedEscapes.Contains(next) && !punctuation)
                {
                    return $"a ticket's key may escape only a punctuation mark or one of `{string.Join(' ', SharedEscapes.Select(c => $"\\{c}"))}`, "
                        + $"which the driver and the CLI read alike, and `\\{next}` is neither.";
                }

                at++; // the escaped character is the escape's own: `\\1` is a backslash and a one, `\(?` a bracket made optional
            }
            else if (inClass)
            {
                // .NET reads `[a-z-[aeiou]]` as a class minus a class, JavaScript as a class then `]` (KEYPARITY2).
                if (key[at] == '-' && next == '[')
                {
                    return "a ticket's key may not subtract a class (`-[` inside a class), which the driver and the CLI read differently.";
                }

                if (key[at] == ']') inClass = false;
            }
            else if (key[at] == '(' && next == '?')
            {
                return "a ticket's key may not use `(?` — a lookaround, a named group or an inline option, which the driver and the "
                    + "CLI read differently.";
            }
            else if (key[at] == '[')
            {
                // `[]` and `[^]` are an empty class in JavaScript, and a class holding `]` in .NET.
                if (next == ']' || (next == '^' && at + 2 < key.Length && key[at + 2] == ']'))
                {
                    return "a ticket's key may not hold an empty class, `[]` or `[^]`, which the driver and the CLI read differently.";
                }

                inClass = true;
            }
        }

        try
        {
            _ = new Regex(key, RegexOptions.CultureInvariant, KeyTimeout);
            return null;
        }
        catch (ArgumentException)
        {
            return $"a ticket's key `{key}` is not a regular expression — e.g. `{DefaultKey}`.";
        }
    }

    /// <summary>
    /// A pattern filled with one session's names. A name that is empty — a title that was only a web address leaves no slug —
    /// takes with it the separator (<c>-</c>, <c>_</c>, <c>.</c>, <c>/</c>) it would leave doubled, leading or trailing, keeping a
    /// <c>/</c> where the other side's is not one. A ticket the key did not find is the session's id.
    /// </summary>
    public static string Expand(string pattern, LandingNames names)
    {
        var text = new StringBuilder();
        var gaps = new List<int>();
        var from = 0;
        foreach (Match used in Placeholder.Matches(pattern))
        {
            text.Append(pattern, from, used.Index - from);
            from = used.Index + used.Length;
            var value = used.Groups[1].Value switch
            {
                "quest" => names.Quest,
                "session" => names.Session,
                "ticket" => names.Ticket ?? names.Session,
                "slug" => names.Slug,
                "repository" => names.Repository,
                _ => used.Value,
            };
            // Two empty names side by side leave one gap.
            if (value.Length > 0) text.Append(value);
            else if (gaps.Count == 0 || gaps[^1] != text.Length) gaps.Add(text.Length);
        }

        text.Append(pattern, from, pattern.Length - from);

        // From the last gap back, so a removal never moves a gap still to come.
        for (var at = gaps.Count - 1; at >= 0; at--)
        {
            var gap = gaps[at];
            var before = gap > 0 && IsSeparator(text[gap - 1]);
            var after = gap < text.Length && IsSeparator(text[gap]);
            if (before && after) text.Remove(text[gap - 1] == '/' && text[gap] != '/' ? gap : gap - 1, 1);
            else if (before && gap == text.Length) text.Remove(gap - 1, 1);
            else if (after && gap == 0) text.Remove(gap, 1);
        }

        return text.ToString();
    }

    private static bool IsSeparator(char c) => c is '-' or '_' or '.' or '/';

    /// <summary>A rule as it is kept: a merge carries no pattern, whatever it was handed.</summary>
    internal static LandingRule Kept(LandingRule rule) => rule.Form == LandingForm.Merge ? rule with { Pattern = null } : rule;

    internal static void WriteRules(Utf8JsonWriter writer, string name, IReadOnlyDictionary<string, LandingRule> map)
    {
        if (map.Count == 0) return;
        writer.WriteStartObject(name);
        foreach (var (key, rule) in map.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            writer.WriteStartObject(key);
            writer.WriteString("form", rule.Form);
            if (rule.Pattern is not null) writer.WriteString("pattern", rule.Pattern);
            // Written only when named, after the pattern it serves (PROCESS1b-i): absent is the default key. The CLI writes it here too.
            if (rule.Key is not null) writer.WriteString("key", rule.Key);
            // Written only when named (D100): absent is the branch left for the person to push.
            if (rule.Plugin is not null) writer.WriteString("plugin", rule.Plugin);
            // Written only when on: absent is the tree staying, as it always has.
            if (rule.Tidy) writer.WriteBoolean("tidy", true);
            // Written only when on (LAND2a): absent is the person's press, as it always was. The CLI writes it here too.
            if (rule.AutoAccept) writer.WriteBoolean("autoAccept", true);
            // Written only when on (PROCESS1b-ii): absent is the branch at the session's own commits. The CLI writes it here too.
            if (rule.Squash) writer.WriteBoolean("squash", true);
            // Written only when set (FABRIC-GRAPH1), last, as written: absent is the default cycle, today's. The CLI writes it here too.
            if (rule.Cycle is not null) CycleText.Write(writer, "cycle", rule.Cycle);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    /// <summary>
    /// A map of names to landing rules; one that could not land work — an unknown form, a pattern that
    /// names no branch, a merge naming a plugin or accepting automatically — is skipped. A ticket's key it refuses is ignored, the
    /// rest of the rule standing. A plugin not installed here is still read: the press says so, and the file stays what the
    /// person wrote. An empty key is none.
    /// </summary>
    internal static IReadOnlyDictionary<string, LandingRule> RuleMap(JsonElement root, string name)
    {
        var map = new Dictionary<string, LandingRule>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                // The first rule written in any case governs, as the CLI reads it, and a later spelling is said (the review's finding 3:
                // the driver kept the last and the terminal the first, so the two chose different graphs).
                if (RuleOf(property.Value, out _) is { } rule && !map.ContainsKey(property.Name)) map[property.Name] = Kept(rule);
            }
        }

        return map;
    }

    /// <summary>
    /// A landing rule as the file holds it, read as <see cref="RuleMap"/> reads it, or null where it is none or does not read: then
    /// <paramref name="unread"/> says why (FABRIC-GRAPH1d) — no object, no <c>form</c>, or a rule <see cref="LandingRules.Problem"/> refuses
    /// — and is null for a rule written null, which is none. Both readers skip such a rule, and <see cref="NodeGraphHomes.Strays"/> says it;
    /// <c>daoris-driver driver list</c> says it too.
    /// </summary>
    internal static LandingRule? RuleOf(JsonElement value, out string? unread)
    {
        unread = null;
        if (value.ValueKind == JsonValueKind.Null) return null;
        if (value.ValueKind != JsonValueKind.Object)
        {
            unread = "it holds no rule's object.";
            return null;
        }

        // Only JSON `true` switches the tidy or the automatic acceptance on (LAND2a): `"true"` or 1 is not the person's say-so for a
        // push with no press.
        var rule = new LandingRule(
            Json.NonEmpty(value, "form") ?? "", Json.NonEmpty(value, "pattern"),
            value.TryGetProperty("tidy", out var tidy) && tidy.ValueKind == JsonValueKind.True,
            Json.NonEmpty(value, "plugin"),
            value.TryGetProperty("autoAccept", out var auto) && auto.ValueKind == JsonValueKind.True,
            Json.NonEmpty(value, "key"),
            // Only JSON `true` squashes (PROCESS1b-ii), as only it accepts automatically.
            value.TryGetProperty("squash", out var squash) && squash.ValueKind == JsonValueKind.True);
        // A key refused here is ignored and the rest of the rule stands (PROCESS1b-i): a key another reader took never drops the rule,
        // which would land the repository's work by its workspace's rule or a merge unasked.
        if (rule.Key is not null && Problem(rule) is not null) rule = rule with { Key = null };
        // FABRIC-GRAPH1: the cycle is kept as written whether or not it is a graph that holds, said where it is read, so a mistyped end
        // never changes how the work lands; only null is none (the review's finding 2: `[]` was dropped).
        if (value.TryGetProperty("cycle", out var cycle) && cycle.ValueKind != JsonValueKind.Null) rule = rule with { Cycle = CycleText.Compact(cycle) };
        if (Problem(rule) is not { } problem) return rule;
        unread = rule.Form.Length == 0 ? "it names no `form`, `merge` or `branch`." : problem;
        return null;
    }
}
