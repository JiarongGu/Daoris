using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// Whose a declared tool is (the UX6 design §7.2): one Daoris runs itself, whose way is set in Settings → Tools; one
/// Daoris's code names and runs for no one but a plugin; or one Daoris does not know, found and never downloaded. The
/// CLI's <c>PluginToolKind</c> spells them <c>own</c>, <c>known</c> and <c>other</c>.
/// </summary>
public enum PluginToolKind
{
    Own,
    Known,
    Other,
}

/// <summary>One readiness check (§7.2): a command whose exit 0 means ready, what that means, and what a person runs when it is not.</summary>
/// <param name="Fix">Shown for the person to run. 🔴 Daoris never runs it.</param>
public sealed record ReadyCheck(IReadOnlyList<string> Run, string Says, string? Fix);

/// <summary>
/// A tool a plugin declares (PLUGTOOL1a, D150 point 7), as its manifest writes it. A known id takes its name from Daoris
/// and its file from its way, so <see cref="Command"/> is null for one; any other is found by <see cref="Command"/>, the
/// id when absent. An entry that breaks a rule keeps only its id, where it has one, and says its first problem.
/// </summary>
/// <param name="Versions">The range as written: <c>&gt;=2.60</c>, <c>&gt;=2.60 &lt;3</c>, or one exact version; null for any.</param>
/// <param name="Problem">Why the entry is not read, naming it; never a reason to refuse the plugin.</param>
public sealed record PluginTool(
    string? Id,
    PluginToolKind? Kind,
    string? Name,
    string? Command,
    IReadOnlyList<string>? VersionArguments,
    string? Versions,
    string? For,
    IReadOnlyList<ReadyCheck> Ready,
    string? Problem)
{
    internal static PluginTool NotRead(string? id, string problem) => new(id, null, null, null, null, null, null, [], problem);
}

/// <summary>
/// A plugin's tools (PLUGTOOL1a, D150 point 7; the UX6 design §7.2–§7.3): the manifest's <c>tools</c>, the programs its
/// process runs, read by the catalogue by one table.
/// </summary>
/// <remarks>
/// <para><b>The CLI's <c>plugins.ts</c> is the twin</b> (<c>toolsDeclared</c>): the same rules in the same order, the same
/// sentences, held by one table (<c>PluginToolsTests</c>, which <c>plugin-tools.test.ts</c> parses). They share no code.</para>
///
/// <para>🔴 <b>A problem in <c>tools</c> never refuses the plugin.</b> A tool is something the plugin needs, never what it
/// is; the problem is said beside the tool. 🔴 <b>Reading starts nothing</b>: a tool is found, asked its version and
/// checked only at a trial or on the person's press (<see cref="PluginToolChecks"/>), never at a load, a look or on a
/// timer, since a check may reach the platform or read another tool's sign-in.</para>
///
/// <para><b>A plugin never carries a download</b> (§7.6): an entry's other fields, an address among them, are passed
/// over, and a tool Daoris does not know is found where the system has it.</para>
/// </remarks>
public static class PluginTools
{
    /// <summary>The tools Daoris runs itself (D150 point 7): their way stays Settings → Tools'. The CLI's <c>DAORIS_OWN_TOOLS</c>.</summary>
    public static readonly IReadOnlyList<string> DaorisOwn = ["git", "node", "pwsh"];

    /// <summary>The most checks a tool may carry (§7.2). The CLI's <c>MAX_TOOL_CHECKS</c>.</summary>
    public const int MaxChecks = 4;

    /// <summary>How long one check, or one version question, may take before it counts as no answer (§7.2).</summary>
    public static readonly TimeSpan CheckPatience = TimeSpan.FromSeconds(10);

    /// <summary>A plugin id's shape (<see cref="PluginCatalog.IsId"/>'s); <c>\z</c>, since .NET's <c>$</c> passes a final newline the CLI's refuses.</summary>
    private static readonly Regex IdShape = new(@"^[a-z0-9][a-z0-9.-]*\z", RegexOptions.CultureInvariant);

    private static readonly string[] Daoris = ["name", "command", "versionArguments"];

    /// <summary>The manifest's <c>tools</c>, by the table: none for <c>null</c>; an array, each entry read on its own.</summary>
    public static (IReadOnlyList<PluginTool> Tools, string? Problem) Read(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Null) return ([], null);
        if (value.ValueKind != JsonValueKind.Array)
        {
            return ([], "`tools` must be an array of the tools the plugin runs, each an object with an `id`.");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        return ([.. value.EnumerateArray().Select((row, at) => One(row, at + 1, seen))], null);
    }

    /// <summary>
    /// One entry, by the rules in order, the first broken said: an object; an <c>id</c> of a tool id's shape, once; a known
    /// id declaring none of the three fields that are Daoris's; another id's <c>name</c>, <c>command</c> (a name on the
    /// PATH, no folder) and <c>versionArguments</c>; <c>versions</c> a range that holds a version; <c>for</c> a sentence;
    /// at most four checks, each a <c>run</c> whose first word is not blank, its <c>says</c>, and a <c>fix</c> that is text
    /// where it is given. JSON <c>null</c> is no field.
    /// </summary>
    private static PluginTool One(JsonElement row, int at, HashSet<string> seen)
    {
        if (row.ValueKind != JsonValueKind.Object) return PluginTool.NotRead(null, $"tool {at} in `tools` is not an object with an `id`.");

        bool Given(string name) => row.TryGetProperty(name, out var field) && field.ValueKind != JsonValueKind.Null;
        JsonElement Field(string name) => row.GetProperty(name);

        if (Filled(row, "id") is not { } id)
        {
            return PluginTool.NotRead(null, $"tool {at} in `tools` needs an `id`: a tool's name in lowercase, like `az`.");
        }

        if (!IdShape.IsMatch(id))
        {
            return PluginTool.NotRead(null,
                $"tool {at} in `tools` has the `id` `{id}`, which is not one: lowercase letters, digits, dots and dashes, like `az`.");
        }

        if (!seen.Add(id)) return PluginTool.NotRead(id, $"tool `{id}` is declared twice in `tools`; the first is read.");
        var tool = $"tool `{id}`";

        var known = Tools.Find(id);
        if (known is not null)
        {
            if (Daoris.FirstOrDefault(Given) is { } field)
            {
                return PluginTool.NotRead(id, $"{tool} is {known.Name}, which Daoris knows: its name, its file and how its version is asked are "
                    + $"Daoris's, so `{field}` is not a plugin's to declare.");
            }
        }
        else
        {
            if (Given("name") && Filled(row, "name") is null) return PluginTool.NotRead(id, $"{tool}'s `name` must be text: what a person calls it.");
            if (Given("command") && (Filled(row, "command") is not { } named || named.IndexOfAny(['/', '\\', ':']) >= 0))
            {
                return PluginTool.NotRead(id, $"{tool}'s `command` must be the name of a program found on the PATH, with no folder in it.");
            }

            if (Given("versionArguments") && Texts(Field("versionArguments")) is null)
            {
                return PluginTool.NotRead(id, $"{tool}'s `versionArguments` must be an array of text: what prints its version.");
            }
        }

        string? versions = null;
        if (Given("versions"))
        {
            var written = Field("versions").ValueKind == JsonValueKind.String ? Field("versions").GetString()! : null;
            if (written is null || VersionRange.Parse(written) is not { } range)
            {
                return PluginTool.NotRead(id, $"{tool}'s `versions` must be a range: `>=2.60`, `>=2.60 <3`, or one exact version.");
            }

            if (range is { Lower: { } lower, Upper: { } upper } && VersionRange.Compare(upper, lower) <= 0)
            {
                return PluginTool.NotRead(id, $"{tool}'s `versions` `{written}` holds no version: `<{upper}` is not above `>={lower}`.");
            }

            versions = written;
        }

        if (Given("for") && Filled(row, "for") is null) return PluginTool.NotRead(id, $"{tool}'s `for` must be one sentence: why the plugin runs it.");

        var ready = new List<ReadyCheck>();
        if (Given("ready"))
        {
            var checks = Field("ready");
            if (checks.ValueKind != JsonValueKind.Array) return PluginTool.NotRead(id, $"{tool}'s `ready` must be an array of checks.");
            if (checks.GetArrayLength() > MaxChecks)
            {
                return PluginTool.NotRead(id, $"{tool} has {checks.GetArrayLength()} checks in `ready`, and a tool has at most four.");
            }

            var m = 0;
            foreach (var check in checks.EnumerateArray())
            {
                m++;
                var run = check.ValueKind == JsonValueKind.Object && check.TryGetProperty("run", out var words) ? Texts(words) : null;
                if (run is not { Count: > 0 } || string.IsNullOrWhiteSpace(run[0]))
                {
                    return PluginTool.NotRead(id, $"{tool}'s check {m} needs a `run`: the command whose exit 0 means ready, as an array.");
                }

                if (Filled(check, "says") is not { } says) return PluginTool.NotRead(id, $"{tool}'s check {m} needs `says`: what it means when it passes.");

                var fix = Filled(check, "fix");
                if (fix is null && check.TryGetProperty("fix", out var fixing) && fixing.ValueKind != JsonValueKind.Null)
                {
                    return PluginTool.NotRead(id, $"{tool}'s check {m} has a `fix` that is not text: the command a person runs when it does not pass.");
                }

                ready.Add(new ReadyCheck(run, says, fix));
            }
        }

        return new PluginTool(
            id,
            known is null ? PluginToolKind.Other : DaorisOwn.Contains(id, StringComparer.Ordinal) ? PluginToolKind.Own : PluginToolKind.Known,
            known?.Name ?? Filled(row, "name") ?? id,
            known is null ? Filled(row, "command") ?? id : null,
            known is null && Given("versionArguments") ? Texts(Field("versionArguments")) : null,
            versions,
            Filled(row, "for"),
            ready,
            null);
    }

    /// <summary>A field that is text and not blank, as written; null for anything else.</summary>
    private static string? Filled(JsonElement row, string name) =>
        row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()
            : null;

    /// <summary>An array of text, as written; null for anything else.</summary>
    private static List<string>? Texts(JsonElement value) =>
        value.ValueKind == JsonValueKind.Array && value.EnumerateArray().All(item => item.ValueKind == JsonValueKind.String)
            ? [.. value.EnumerateArray().Select(item => item.GetString()!)]
            : null;
}

/// <summary>
/// A range a plugin's tool works with (§7.2): a floor (<c>&gt;=V</c>), a floor and a top (<c>&gt;=V &lt;V</c>), or one exact
/// version (<c>V</c>), each <c>V</c> one to four numbers. Compared number by number, a missing number 0.
/// </summary>
/// <param name="Lower">The version it starts at, held.</param>
/// <param name="Upper">The version it stays below.</param>
/// <param name="Exact">The one version it takes.</param>
public sealed record VersionRange(string? Lower, string? Upper, string? Exact)
{
    /// <summary>
    /// The range a manifest writes, or null where it is not one: words parted by spaces, <c>&gt;=V</c>, <c>&gt;=V &lt;V</c>,
    /// or <c>V</c> alone. Nothing else is a range, so no reader guesses at what one meant. The CLI's <c>parseRange</c>.
    /// </summary>
    public static VersionRange? Parse(string written)
    {
        var words = written.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        static string? Floor(string word) => word.StartsWith(">=", StringComparison.Ordinal) && Tools.IsExactVersion(word[2..]) ? word[2..] : null;
        static string? Top(string word) => word.StartsWith('<') && Tools.IsExactVersion(word[1..]) ? word[1..] : null;

        return words switch
        {
            [var one] when Tools.IsExactVersion(one) => new(null, null, one),
            [var one] => Floor(one) is { } lower ? new(lower, null, null) : null,
            [var first, var second] => Floor(first) is { } lower && Top(second) is { } upper ? new(lower, upper, null) : null,
            _ => null,
        };
    }

    /// <summary>
    /// Two exact versions, number by number, a missing number 0, so <c>2.60</c> and <c>2.60.0</c> are the same. The CLI's
    /// <c>compareNumbers</c>.
    /// </summary>
    public static int Compare(string a, string b)
    {
        var left = a.Split('.');
        var right = b.Split('.');
        for (var at = 0; at < Math.Max(left.Length, right.Length); at++)
        {
            var x = Number(at < left.Length ? left[at] : "0");
            var y = Number(at < right.Length ? right[at] : "0");
            if (x.Length != y.Length) return x.Length - y.Length;
            var order = string.CompareOrdinal(x, y);
            if (order != 0) return Math.Sign(order);
        }

        return 0;

        static string Number(string part) => part.TrimStart('0') is { Length: > 0 } trimmed ? trimmed : "0";
    }

    /// <summary>Whether a version a tool said is in the range. A version is its first dotted number, which a trial reads.</summary>
    public bool Holds(string version) =>
        Exact is { } exact
            ? Compare(version, exact) == 0
            : (Lower is null || Compare(version, Lower) >= 0) && (Upper is null || Compare(version, Upper) < 0);

    /// <summary>What it needs, as the plugin's page says it (§7.2): <c>2.60 or newer</c>, <c>2.60 or newer, below 3</c>, <c>exactly 22.11.0</c>.</summary>
    public string Needs => Exact is { } exact
        ? $"exactly {exact}"
        : Upper is { } upper ? $"{Lower} or newer, below {upper}" : $"{Lower} or newer";
}
