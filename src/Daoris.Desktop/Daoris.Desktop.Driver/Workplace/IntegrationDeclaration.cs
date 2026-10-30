using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>A rule of <c>integration.merge</c> (the integration design §4.1): a file settled by rows, or a line pattern with the writer that recomputes it.</summary>
/// <param name="Paths">The file, trimmed, each backslash a slash.</param>
/// <param name="Line">The line's pattern, as written; null for a file settled by rows.</param>
/// <param name="Run">The writer's command, as written; null for a file settled by rows.</param>
internal sealed record IntegrationMergeRule(string Paths, string? Line = null, string? Run = null)
{
    /// <summary>Whether the file is settled by its rows (<c>by: rows</c>), as the backlog is.</summary>
    public bool ByRows => Line is null;
}

/// <summary>
/// A repository's integration facts as its <c>daoris.gates.json</c> declares them (INTEGRATE1d; D215 point 2, the integration design
/// §4.1), each key's absence kept as absence, so its reader reads the design's <i>Absent</i> column.
/// </summary>
/// <param name="By">Who integrates the line, <c>daoris</c> or <c>tool</c>; null where absent (<c>daoris</c> where the rule integrates).</param>
/// <param name="Gates">The declared gates run at integration, in the order written; null where absent (every declared gate) or <paramref name="Reached"/>.</param>
/// <param name="Reached">Whether the gates at integration are <c>reached</c>, the runner's plan choosing among the accepted ones.</param>
/// <param name="Commits">The commit check, <c>runner</c> or a declared gate's name; null for none.</param>
/// <param name="Scratch">The globs of ignored output a landing's tidy may take with a tree, each trimmed with each backslash a slash.</param>
/// <param name="Merge">The files whose conflicts settle by a rule, in the order written.</param>
/// <param name="Message">The merge commit's subject; null for the merge form's own.</param>
/// <param name="Pass">The variables' exact names a gate needs from the install's environment beyond the composed base.</param>
internal sealed record IntegrationFacts(
    string? By, IReadOnlyList<string>? Gates, bool Reached, string? Commits, IReadOnlyList<string> Scratch,
    IReadOnlyList<IntegrationMergeRule> Merge, string? Message, IReadOnlyList<string> Pass)
{
    /// <summary>The files settled by rows (INTEGRATE1c), each once, in the order first declared: no line rule, whose writer waits for the acceptance.</summary>
    public IReadOnlyList<string> RowTables => [.. Merge.Where(rule => rule.ByRows).Select(rule => rule.Paths).Distinct(StringComparer.Ordinal)];
}

/// <summary>What a declaration's text reads as: its facts, or the one problem that stops them, never both.</summary>
internal sealed record IntegrationRead(IntegrationFacts? Facts, string? Problem);

/// <summary>
/// A repository's <c>integration</c>, read whole in one place (INTEGRATE1d; D215 point 2, the integration design §4.1): <c>by</c>,
/// <c>gates</c>, <c>commits</c>, <c>scratch</c>, <c>merge</c>, <c>message</c> and <c>env.pass</c>, each key's shape checked in that
/// order, whatever order it is written in. A declaration that breaks a shape is one problem, named in a sentence, and reads no
/// facts at all: never a partial read. A key written null is absent, and one this build does not know is passed over, as the
/// devkit's reader passes over keys it does not know. Nothing acts on it yet beyond row c's row tables (<see cref="BacklogMerge.RowTablesOf"/>,
/// read unaccepted from the line, since no command runs): the line rules' writers, the scratch and the gates wait for the
/// owner's acceptance (<see cref="IntegrationAcceptance"/>; C6).
/// </summary>
/// <remarks>
/// <b>A twin</b> of <c>tools/merge-branch.mjs</c>'s <c>integrationOf</c>: no code is shared, so both hold one table, the driver's
/// tests' <c>fixtures/integration-declarations.json</c>, row for row and sentence for sentence (<c>IntegrationDeclarationTests</c>,
/// <c>merge-branch.test.ts</c>), and a change to one is a change to both.
/// </remarks>
internal static class IntegrationDeclaration
{
    /// <summary>The declaration's key that holds the repository's integration facts.</summary>
    internal const string Key = "integration";

    // The sentences, the merge tool's INTEGRATION_SAYS sentence for sentence (a deliberate copy).
    internal const string NotJson = "`daoris.gates.json` does not read as JSON, so it declares no integration.";
    internal const string NotObject = "`daoris.gates.json` holds no object, so it declares no integration.";
    internal const string IntegrationShape = "`integration` is an object: `by`, `gates`, `commits`, `scratch`, `merge`, `message` and `env`.";
    internal const string ByShape = "`integration.by` is `daoris` or `tool`.";
    internal const string GatesShape = "`integration.gates` is `reached`, or a list of one or more declared gates' names.";
    internal const string CommitsShape = "`integration.commits` is `runner`, or a declared gate's name.";
    internal const string ScratchShape = "`integration.scratch` is a list of globs, each text.";
    internal const string MergeShape = "`integration.merge` is a list of rules.";
    internal const string MessageShape = "`integration.message` is the merge commit's subject, as text.";
    internal const string EnvShape = "`integration.env` is an object, its `pass` a list of variables' names.";

    internal static string GateUndeclared(string name) => $"`integration.gates` names `{name}`, which `gates` does not declare.";

    internal static string GateTwice(string name) => $"`integration.gates` names `{name}` twice.";

    internal static string CommitsUndeclared(string name) => $"`integration.commits` names `{name}`, which `gates` does not declare.";

    internal static string ScratchGlob(string glob) =>
        $"`integration.scratch` holds `{glob}`: a glob is a path from the repository's root, never absolute, never leaving it by `..`, never opening with a wildcard.";

    internal static string MergeRule(int at) =>
        $"`integration.merge`'s rule {at} is neither `{{ paths, by: rows }}` nor `{{ paths, line, run }}`: no rule keeps one side's copy.";

    internal static string MessageSays(string said) =>
        $"`integration.message` says `{said}`, and a subject says only `{{name}}`, `{{title}}`, `{{quest}}`, `{{repository}}` and `{{branch}}`.";

    internal static string PassName(string name) => $"`integration.env.pass` names `{name}`, which is no variable's name.";

    internal static string PassDaoris(string name) =>
        $"`integration.env.pass` names `{name}`: a `DAORIS_` variable is the install's, and never passes to a gate.";

    /// <summary>What a merge message may say (the design §4.1's <c>message</c>).</summary>
    internal static readonly IReadOnlyList<string> MessageNames = ["name", "title", "quest", "repository", "branch"];

    private static readonly IntegrationRead None = new(null, null);

    private static readonly Regex Placeholder = new(@"\{([^{}]*)\}", RegexOptions.CultureInvariant);

    private static readonly Regex VariableName = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant);

    /// <summary>
    /// The facts <paramref name="text"/> declares, or the sentence naming the first key whose shape it breaks; neither where the text
    /// is null (no declaration) or declares no <c>integration</c>.
    /// </summary>
    internal static IntegrationRead Read(string? text)
    {
        if (text is null) return None;
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(text);
        }
        catch (JsonException)
        {
            return Refused(NotJson);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return Refused(NotObject);
            if (!Present(root, Key, out var integration)) return None;
            return integration.ValueKind == JsonValueKind.Object ? Facts(integration, DeclaredGates(root)) : Refused(IntegrationShape);
        }
    }

    private static IntegrationRead Facts(JsonElement integration, IReadOnlyList<string> declared)
    {
        string? by = null;
        if (Present(integration, "by", out var byValue))
        {
            by = byValue.ValueKind == JsonValueKind.String ? byValue.GetString() : null;
            if (by is not ("daoris" or "tool")) return Refused(ByShape);
        }

        List<string>? gates = null;
        var reached = false;
        if (Present(integration, "gates", out var gatesValue))
        {
            if (gatesValue.ValueKind == JsonValueKind.String && gatesValue.GetString() == "reached") reached = true;
            else if (gatesValue.ValueKind == JsonValueKind.Array && gatesValue.GetArrayLength() > 0 && gatesValue.EnumerateArray().All(IsText))
            {
                gates = [];
                foreach (var name in gatesValue.EnumerateArray().Select(each => each.GetString()!))
                {
                    if (!declared.Contains(name, StringComparer.Ordinal)) return Refused(GateUndeclared(name));
                    if (gates.Contains(name, StringComparer.Ordinal)) return Refused(GateTwice(name));
                    gates.Add(name);
                }
            }
            else return Refused(GatesShape);
        }

        string? commits = null;
        if (Present(integration, "commits", out var commitsValue))
        {
            if (!IsText(commitsValue)) return Refused(CommitsShape);
            commits = commitsValue.GetString()!;
            if (commits != "runner" && !declared.Contains(commits, StringComparer.Ordinal)) return Refused(CommitsUndeclared(commits));
        }

        var scratch = new List<string>();
        if (Present(integration, "scratch", out var scratchValue))
        {
            if (scratchValue.ValueKind != JsonValueKind.Array || !scratchValue.EnumerateArray().All(IsText)) return Refused(ScratchShape);
            foreach (var written in scratchValue.EnumerateArray())
            {
                var glob = KeptPath(written.GetString()!);
                if (!GlobHolds(glob)) return Refused(ScratchGlob(glob));
                scratch.Add(glob);
            }
        }

        var merge = new List<IntegrationMergeRule>();
        if (Present(integration, "merge", out var mergeValue))
        {
            if (mergeValue.ValueKind != JsonValueKind.Array) return Refused(MergeShape);
            var at = 0;
            foreach (var rule in mergeValue.EnumerateArray())
            {
                at++;
                var paths = rule.ValueKind == JsonValueKind.Object && rule.TryGetProperty("paths", out var named) && IsText(named)
                    ? KeptPath(named.GetString()!)
                    : null;
                if (paths is not null && Present(rule, "by", out var rows) && rows.ValueKind == JsonValueKind.String && rows.GetString() == "rows"
                    && !Present(rule, "line", out _) && !Present(rule, "run", out _))
                {
                    merge.Add(new IntegrationMergeRule(paths));
                }
                else if (paths is not null && !Present(rule, "by", out _)
                    && rule.TryGetProperty("line", out var line) && IsText(line) && rule.TryGetProperty("run", out var run) && IsText(run))
                {
                    merge.Add(new IntegrationMergeRule(paths, line.GetString(), run.GetString()));
                }
                else return Refused(MergeRule(at));
            }
        }

        string? message = null;
        if (Present(integration, "message", out var messageValue))
        {
            if (!IsText(messageValue)) return Refused(MessageShape);
            message = messageValue.GetString()!;
            foreach (Match said in Placeholder.Matches(message))
            {
                if (!MessageNames.Contains(said.Groups[1].Value, StringComparer.Ordinal)) return Refused(MessageSays(said.Value));
            }
        }

        var pass = new List<string>();
        if (Present(integration, "env", out var env))
        {
            if (env.ValueKind != JsonValueKind.Object) return Refused(EnvShape);
            if (Present(env, "pass", out var passValue))
            {
                if (passValue.ValueKind != JsonValueKind.Array || passValue.EnumerateArray().Any(each => each.ValueKind != JsonValueKind.String))
                {
                    return Refused(EnvShape);
                }

                foreach (var name in passValue.EnumerateArray().Select(each => each.GetString()!))
                {
                    if (!VariableName.IsMatch(name)) return Refused(PassName(name));
                    // INTEGRATE1a's composed base strips every Daoris name; a declaration never hands one back (the design §5.5).
                    if (name.StartsWith("DAORIS_", StringComparison.OrdinalIgnoreCase)) return Refused(PassDaoris(name));
                    pass.Add(name);
                }
            }
        }

        return new IntegrationRead(new IntegrationFacts(by, gates, reached, commits, scratch, merge, message, pass), null);
    }

    /// <summary>The names of the gates <paramref name="text"/>'s own <c>gates</c> declares, as the integration's gates read them; none where it does not read (INTEGRATE1e part e4a).</summary>
    internal static IReadOnlyList<string> Declared(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            return document.RootElement.ValueKind == JsonValueKind.Object ? DeclaredGates(document.RootElement) : [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>The names of the gates the declaration's own <c>gates</c> declares: each an object whose <c>name</c> is text.</summary>
    private static IReadOnlyList<string> DeclaredGates(JsonElement root) =>
        root.TryGetProperty("gates", out var gates) && gates.ValueKind == JsonValueKind.Array
            ? [.. gates.EnumerateArray()
                .Where(gate => gate.ValueKind == JsonValueKind.Object && gate.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String)
                .Select(gate => gate.GetProperty("name").GetString()!)]
            : [];

    /// <summary>
    /// Whether a scratch glob stays under the repository's root as <see cref="TreeScratch"/> takes one: not absolute (a leading
    /// <c>/</c> or any <c>:</c>), no <c>..</c> part, and no wildcard in its first part, which would reach folders the repository never named.
    /// </summary>
    private static bool GlobHolds(string glob)
    {
        if (glob.StartsWith('/') || glob.Contains(':')) return false;
        var segments = glob.Trim('/').Split('/');
        return segments[0].Length > 0 && !segments.Contains("..") && segments[0].AsSpan().IndexOfAny('*', '?') < 0;
    }

    private static IntegrationRead Refused(string problem) => new(null, problem);

    /// <summary>A key the object holds, written as something other than null: a key written null is absent.</summary>
    private static bool Present(JsonElement element, string key, out JsonElement value) =>
        element.TryGetProperty(key, out value) && value.ValueKind != JsonValueKind.Null;

    private static bool IsText(JsonElement value) => value.ValueKind == JsonValueKind.String && value.GetString()!.Trim().Length > 0;

    /// <summary>A path as both twins keep one: trimmed, each backslash a slash.</summary>
    private static string KeptPath(string text) => text.Trim().Replace('\\', '/');
}
