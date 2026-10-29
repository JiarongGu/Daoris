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

    /// <summary>
    /// How this install integrates the repository's line (INTEGRATE1d; D215 point 2, the integration design §4.1): only on a branch
    /// rule with no plugin and no <see cref="Squash"/>, inherited whole with the rule (D164), so a build that knows no
    /// <c>integrate</c> reads the rule as the branch rule it is and merges nothing. Null is none: the branch is the result, as
    /// always. A door with no field for it keeps it (<see cref="DriverConfig.WithLanding"/>); only
    /// <see cref="DriverConfig.WithLandingIntegrate"/> sets or clears it. Nothing acts on it yet.
    /// </summary>
    public LandingIntegrate? Integrate { get; init; }
}

/// <summary>
/// The install's half of integration, a branch rule's <c>integrate</c> (INTEGRATE1d; the integration design §4.1's <i>install's
/// half</i>, §17.7): each key as written, null where absent, so its reader reads the design's <i>Absent</i> column
/// (<see cref="Resolved"/>). <c>request</c> and <c>conflicts</c> are the unit's process, read under Current from here and under a
/// named workflow from its <c>integrate</c> step; the rest say how the line is integrated, and stay the rule's always.
/// </summary>
/// <param name="Request">Who requests: <see cref="Owner"/>, <see cref="Done"/> (through the rule's own <c>autoAccept</c>), or <see cref="Steward"/>.</param>
/// <param name="Commit">How each member lands: <see cref="Merge"/>, <see cref="Rebase"/> or <see cref="Squash"/>.</param>
/// <param name="Batch">Members per candidate, 1 to <see cref="MaxBatch"/>.</param>
/// <param name="Order"><see cref="Arrival"/>, or <see cref="Apart"/>.</param>
/// <param name="Gates"><see cref="Declared"/> (the accepted declaration's <c>integration.gates</c>), or <see cref="NoGates"/>.</param>
/// <param name="Unreached">What an unconfirmed failure no member reaches does: <see cref="Split"/>, <see cref="Hold"/> or <see cref="Land"/>.</param>
/// <param name="Reached">What an unconfirmed failure a member reaches does, the same three.</param>
/// <param name="Conflicts">Where a conflict of meaning goes: the member's <see cref="Session"/>, or held (<see cref="Hold"/>).</param>
/// <param name="Overlap">A start whose declared paths meet an open quest's: <see cref="Warn"/>, or <see cref="Wait"/>.</param>
/// <param name="Line">The line by name; null for the repository's canonical line.</param>
public sealed record LandingIntegrate(
    string? Request = null, string? Commit = null, int? Batch = null, string? Order = null, string? Gates = null,
    string? Unreached = null, string? Reached = null, string? Conflicts = null, string? Overlap = null, string? Line = null)
{
    public const string Owner = "owner";
    public const string Done = "done";
    public const string Steward = "steward";
    public const string Merge = "merge";
    public const string Rebase = "rebase";
    public const string Squash = "squash";
    public const string Arrival = "arrival";
    public const string Apart = "apart";
    public const string Declared = "declared";
    public const string NoGates = "none";
    public const string Split = "split";
    public const string Hold = "hold";
    public const string Land = "land";
    public const string Session = "session";
    public const string Warn = "warn";
    public const string Wait = "wait";

    /// <summary>The most members one candidate holds.</summary>
    public const int MaxBatch = 8;

    public static IReadOnlyList<string> Requests { get; } = [Owner, Done, Steward];
    public static IReadOnlyList<string> Commits { get; } = [Merge, Rebase, Squash];
    public static IReadOnlyList<string> Orders { get; } = [Arrival, Apart];
    public static IReadOnlyList<string> GateChoices { get; } = [Declared, NoGates];
    public static IReadOnlyList<string> Unconfirmeds { get; } = [Split, Hold, Land];
    public static IReadOnlyList<string> ConflictChoices { get; } = [Session, Hold];
    public static IReadOnlyList<string> Overlaps { get; } = [Warn, Wait];

    /// <summary>
    /// Every absent key read as the design's <i>Absent</i> column: requested by the owner, each member merged, eight members where
    /// gates run and one where none, in arrival order, no gates, an unconfirmed failure split either way, a conflict to the member's
    /// session, an overlap warned of; the line stays null, the repository's canonical one.
    /// </summary>
    public LandingIntegrate Resolved()
    {
        var gates = Gates ?? NoGates;
        return new(Request ?? Owner, Commit ?? Merge, Batch ?? (gates == Declared ? MaxBatch : 1), Order ?? Arrival, gates,
            Unreached ?? Split, Reached ?? Split, Conflicts ?? Session, Overlap ?? Warn, Line);
    }

    /// <summary>
    /// What an unconfirmed failure does for a unit a member reaches or not (the design §4.5): <see cref="Split"/> where absent, and
    /// where <see cref="Land"/> is set on a line no whole-set verification runs on, since <c>land</c> is only ever the owner's choice
    /// backed by one.
    /// </summary>
    public string Unconfirmed(bool reached, bool wholeSet)
    {
        var said = (reached ? Reached : Unreached) ?? Split;
        return said == Land && !wholeSet ? Split : said;
    }
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
    /// is on this machine is <see cref="PluginProblem"/>'s question: a file stays readable wherever it is. A rule carrying
    /// <c>integrate</c> is judged as though no whole-set verification ran on its line (<see cref="Problem(LandingRule, bool)"/>).
    /// </summary>
    public static string? Problem(LandingRule rule) => Problem(rule, wholeSet: false);

    /// <summary>
    /// What is wrong with a rule's shape, its <c>integrate</c> with it (INTEGRATE1d, the integration design §11 row d), in a sentence,
    /// or null when it can land work; <paramref name="wholeSet"/> says whether a whole-set verification runs on the line it integrates,
    /// without which an unconfirmed failure is never landed.
    /// </summary>
    public static string? Problem(LandingRule rule, bool wholeSet) => FormProblem(rule) ?? IntegrateProblem(rule, wholeSet);

    private static string? FormProblem(LandingRule rule) => rule.Form switch
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

    // INTEGRATE1d: the sentences a rule's `integrate` is refused in (the integration design §4.1, §11 row d).

    internal const string IntegrateShape =
        "`integrate` is an object: `request`, `commit`, `batch`, `order`, `gates`, `unconfirmed`, `conflicts`, `overlap` and `line`.";

    internal const string UnconfirmedShape = "`integrate.unconfirmed` is an object: `unreached` and `reached`, each `split`, `hold` or `land`.";

    internal const string BatchShape = "`integrate.batch` is how many members a candidate holds, 1 to 8.";

    internal const string IntegrateBesidePlugin =
        "`integrate` is refused beside a plugin — a pull request is its platform's integration. Take the plugin off the rule to integrate "
        + "here, or `integrate` off to open pull requests.";

    internal const string IntegrateOnMerge =
        "`integrate` rides a branch rule, never a merge rule — a build that knows no `integrate` merges a merge rule on the press, ungated. "
        + "`branch <pattern>` is the form that carries it.";

    internal const string IntegrateBesideSquash =
        "the rule's own `squash` is refused beside `integrate` — `integrate`'s `commit: squash` makes one commit per member.";

    internal const string DoneWithoutAutoAccept =
        "`request: done` is the quest's done through the rule's `autoAccept`, which is off — turn it on (`--auto-accept`), or request as `owner`.";

    internal const string DoneWithoutGates =
        "`request: done` needs `gates: declared` — work merged into the line with neither a press nor a check is refused.";

    internal const string StewardWithoutGates = "`request: steward` needs `gates: declared` — a role's act never lands ungated work on the line.";

    internal const string LandWithoutWholeSet =
        "`unconfirmed: land` is refused where no whole-set verification runs on the line — here, a landed route naming a verification "
        + "pipeline whose gates you accepted. A failure that does not recur splits.";

    private static string Listed(IReadOnlyList<string> choices) =>
        string.Join(", ", choices.Take(choices.Count - 1).Select(choice => $"`{choice}`")) + $" or `{choices[^1]}`";

    private static string ChoiceShape(string key, IReadOnlyList<string> choices) => $"`integrate.{key}` is {Listed(choices)}.";

    private static string ChoiceProblem(string key, IReadOnlyList<string> choices, string value) =>
        $"`integrate.{key}` is {Listed(choices)}, and `{value}` is none.";

    /// <summary>
    /// What is wrong with a rule's <c>integrate</c>, in a sentence, or null where it has none or it can integrate (the integration design
    /// §4.1, §11 row d): refused on a merge rule, beside a plugin and beside the rule's own <c>squash</c>; each key one of its values;
    /// <c>request: done</c> without the rule's <c>autoAccept</c> or without <c>gates: declared</c>; <c>request: steward</c> without
    /// <c>gates: declared</c>; and <c>land</c> for an unconfirmed failure where no whole-set verification runs on the line. Whether the
    /// accepted declaration names a gate is the acceptance's question, asked where the request is made.
    /// </summary>
    private static string? IntegrateProblem(LandingRule rule, bool wholeSet)
    {
        if (rule.Integrate is not { } integrate) return null;
        // An older build merges a merge rule on the press, ungated, whatever it carries (D215 point 3).
        if (rule.Form == LandingForm.Merge) return IntegrateOnMerge;
        // A pull request is the platform's integration (F17).
        if (rule.Plugin is not null) return IntegrateBesidePlugin;
        if (rule.Squash) return IntegrateBesideSquash;
        var unknown = Unknown("request", LandingIntegrate.Requests, integrate.Request)
            ?? Unknown("commit", LandingIntegrate.Commits, integrate.Commit)
            ?? (integrate.Batch is < 1 or > LandingIntegrate.MaxBatch ? BatchShape : null)
            ?? Unknown("order", LandingIntegrate.Orders, integrate.Order)
            ?? Unknown("gates", LandingIntegrate.GateChoices, integrate.Gates)
            ?? Unknown("unconfirmed.unreached", LandingIntegrate.Unconfirmeds, integrate.Unreached)
            ?? Unknown("unconfirmed.reached", LandingIntegrate.Unconfirmeds, integrate.Reached)
            ?? Unknown("conflicts", LandingIntegrate.ConflictChoices, integrate.Conflicts)
            ?? Unknown("overlap", LandingIntegrate.Overlaps, integrate.Overlap)
            ?? (integrate.Line is { } line && !BranchName.IsValid(line)
                ? $"`integrate.line` is the line by name, and `{line}` is not a branch name git would take."
                : null);
        if (unknown is not null) return unknown;

        var gated = integrate.Gates == LandingIntegrate.Declared;
        // A merge with neither a press nor a check is what D145 point 1 guards; a role's act landing ungated work is a landing (D209).
        if (integrate.Request == LandingIntegrate.Done && !rule.AutoAccept) return DoneWithoutAutoAccept;
        if (integrate.Request == LandingIntegrate.Done && !gated) return DoneWithoutGates;
        if (integrate.Request == LandingIntegrate.Steward && !gated) return StewardWithoutGates;
        // The confirm run is evidence, not proof: `land` is the owner's choice only where a whole-set verification backs it (C6, F14).
        return !wholeSet && (integrate.Unreached == LandingIntegrate.Land || integrate.Reached == LandingIntegrate.Land) ? LandWithoutWholeSet : null;

        static string? Unknown(string key, IReadOnlyList<string> choices, string? value) =>
            value is null || choices.Contains(value, StringComparer.Ordinal) ? null : ChoiceProblem(key, choices, value);
    }

    /// <summary>
    /// A rule's <c>integrate</c> as the file holds it, each key as written, or null with <paramref name="problem"/> where it does not
    /// read: no object, a key's value not text (<c>batch</c> not a whole number, <c>unconfirmed</c> not an object). A key written null
    /// is absent, and one this build does not know is passed over, as every key a rule does not know is. Its values are judged by
    /// <see cref="Problem(LandingRule, bool)"/>.
    /// </summary>
    internal static LandingIntegrate? IntegrateOf(JsonElement value, out string? problem)
    {
        problem = null;
        if (value.ValueKind != JsonValueKind.Object)
        {
            problem = IntegrateShape;
            return null;
        }

        static string? Choice(JsonElement holder, string key, string named, IReadOnlyList<string> choices, ref string? said)
        {
            if (said is not null || !holder.TryGetProperty(key, out var field) || field.ValueKind == JsonValueKind.Null) return null;
            if (field.ValueKind == JsonValueKind.String) return field.GetString();
            said = ChoiceShape(named, choices);
            return null;
        }

        string? said = null;
        var request = Choice(value, "request", "request", LandingIntegrate.Requests, ref said);
        var commit = Choice(value, "commit", "commit", LandingIntegrate.Commits, ref said);
        int? batch = null;
        if (said is null && value.TryGetProperty("batch", out var members) && members.ValueKind != JsonValueKind.Null)
        {
            if (members.ValueKind == JsonValueKind.Number && members.TryGetInt32(out var count)) batch = count;
            else said = BatchShape;
        }

        var order = Choice(value, "order", "order", LandingIntegrate.Orders, ref said);
        var gates = Choice(value, "gates", "gates", LandingIntegrate.GateChoices, ref said);
        string? unreached = null, reached = null;
        if (said is null && value.TryGetProperty("unconfirmed", out var unconfirmed) && unconfirmed.ValueKind != JsonValueKind.Null)
        {
            if (unconfirmed.ValueKind != JsonValueKind.Object) said = UnconfirmedShape;
            else
            {
                unreached = Choice(unconfirmed, "unreached", "unconfirmed.unreached", LandingIntegrate.Unconfirmeds, ref said);
                reached = Choice(unconfirmed, "reached", "unconfirmed.reached", LandingIntegrate.Unconfirmeds, ref said);
            }
        }

        var conflicts = Choice(value, "conflicts", "conflicts", LandingIntegrate.ConflictChoices, ref said);
        var overlap = Choice(value, "overlap", "overlap", LandingIntegrate.Overlaps, ref said);
        string? line = null;
        if (said is null && value.TryGetProperty("line", out var lineValue) && lineValue.ValueKind != JsonValueKind.Null)
        {
            if (lineValue.ValueKind == JsonValueKind.String) line = lineValue.GetString();
            else said = "`integrate.line` is the line by name.";
        }

        if (said is not null)
        {
            problem = said;
            return null;
        }

        return new LandingIntegrate(request, commit, batch, order, gates, unreached, reached, conflicts, overlap, line);
    }

    /// <summary>
    /// Why a rule's <c>integrate</c>, as the file holds it, is passed over (INTEGRATE1d): it does not read, or the rule refuses it; null
    /// where the rule carries none, or it reads. The rule itself stands either way, as a build that knows no <c>integrate</c> reads it;
    /// Setup's row says this sentence.
    /// </summary>
    internal static string? IntegrateRefused(JsonElement value)
    {
        Read(value, out _, out var refused);
        return refused;
    }

    private static void WriteIntegrate(Utf8JsonWriter writer, LandingIntegrate integrate)
    {
        writer.WriteStartObject("integrate");
        // Each written only when set, in the design's order: absent reads the Absent column (LandingIntegrate.Resolved).
        if (integrate.Request is not null) writer.WriteString("request", integrate.Request);
        if (integrate.Commit is not null) writer.WriteString("commit", integrate.Commit);
        if (integrate.Batch is { } batch) writer.WriteNumber("batch", batch);
        if (integrate.Order is not null) writer.WriteString("order", integrate.Order);
        if (integrate.Gates is not null) writer.WriteString("gates", integrate.Gates);
        if (integrate.Unreached is not null || integrate.Reached is not null)
        {
            writer.WriteStartObject("unconfirmed");
            if (integrate.Unreached is not null) writer.WriteString("unreached", integrate.Unreached);
            if (integrate.Reached is not null) writer.WriteString("reached", integrate.Reached);
            writer.WriteEndObject();
        }

        if (integrate.Conflicts is not null) writer.WriteString("conflicts", integrate.Conflicts);
        if (integrate.Overlap is not null) writer.WriteString("overlap", integrate.Overlap);
        if (integrate.Line is not null) writer.WriteString("line", integrate.Line);
        writer.WriteEndObject();
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
            // Written only when set (INTEGRATE1d), after the rule's switches and before its cycle: absent is no integration, the branch the result.
            if (rule.Integrate is { } integrate) WriteIntegrate(writer, integrate);
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
    internal static LandingRule? RuleOf(JsonElement value, out string? unread) => Read(value, out unread, out _);

    /// <summary><see cref="RuleOf"/>, and why the rule's <c>integrate</c> was passed over where it was (<see cref="IntegrateRefused"/>).</summary>
    private static LandingRule? Read(JsonElement value, out string? unread, out string? integrateRefused)
    {
        unread = null;
        integrateRefused = null;
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
        // INTEGRATE1d: an `integrate` that does not read, or that the rule refuses, is passed over and the rest of the rule stands, as a
        // build that knows no `integrate` reads it: dropping the rule would land the work by its workspace's rule or a merge unasked.
        // `land` is kept as written, whatever runs on the line now: it is judged at the door that sets it and where integration reads
        // it (LandingIntegrate.Unconfirmed), never lost for a route set later.
        if (value.TryGetProperty("integrate", out var integrate) && integrate.ValueKind != JsonValueKind.Null)
        {
            var read = IntegrateOf(integrate, out integrateRefused);
            if (read is not null && IntegrateProblem(rule with { Integrate = read }, wholeSet: true) is { } refused) integrateRefused = refused;
            else if (read is not null) rule = rule with { Integrate = read };
        }

        if (Problem(rule, wholeSet: true) is not { } problem) return rule;
        unread = rule.Form.Length == 0 ? "it names no `form`, `merge` or `branch`." : problem;
        return null;
    }
}
