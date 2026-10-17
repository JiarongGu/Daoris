using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// What a plugin's step creates that a unit's cleanup must account for (FABRIC-UNIT1d; the fabric events design §D3): the manifest's
/// <c>resources: [{ schema, role?, cleanup }]</c>. A step's receipt names one by its schema; Daoris never removes it itself.
/// </summary>
/// <param name="Schema">What it is, like <c>acme.deploy/preview@1</c>: a receipt's resource is of a declared schema.</param>
/// <param name="Role">One of <see cref="PluginResourceRoles"/>: <c>temporary</c> where the manifest says none.</param>
/// <param name="Cleanup">Who removes it: <see cref="PluginSteps.CleanupPoint"/>, the plugin through its own cleanup adapter.</param>
public sealed record PluginResource(string Schema, string Role, string Cleanup);

/// <summary>A declared resource's role (§D3's ownership entry, as far as a manifest declares it). Twin: the CLI's <c>RESOURCE_ROLES</c>.</summary>
public static class PluginResourceRoles
{
    /// <summary>Removed at the unit's true end, through the plugin's cleanup adapter.</summary>
    public const string Temporary = "temporary";

    /// <summary>Kept as the work's result; a superseded attempt's is temporary whatever it declared (§D3).</summary>
    public const string Result = "result";

    public static readonly IReadOnlyList<string> All = [Temporary, Result];
}

/// <summary>A manifest's <c>steps</c> and <c>resources</c> as read: each list, or the first problem's code and words, which refuse the plugin.</summary>
/// <param name="Code">The problem's code, one of <see cref="PluginSteps"/>' codes, or null.</param>
/// <param name="Problem">Its sentence, which is the plugin's problem, or null.</param>
public sealed record PluginStepsRead(IReadOnlyList<NodeDeclaration> Steps, IReadOnlyList<PluginResource> Resources, string? Code, string? Problem)
{
    public static readonly PluginStepsRead None = new([], [], null, null);
}

/// <summary>
/// A plugin's steps and resources (FABRIC-UNIT1d; the fabric events design §D4, §D6, §D9, §E2): the manifest declares a step once, and it
/// is read into the one <see cref="NodeDeclaration"/> every built-in is, of the plugin's own kind <c>&lt;plugin&gt;/&lt;step&gt;</c>, and
/// judged by <see cref="NodeDeclarations.Problem"/>: never a second model.
/// </summary>
/// <remarks>
/// <para><b>A problem refuses the plugin</b>, as a server's or a skill's does (§C4), with its code and its words. The rules are checked
/// in one order, and the first broken one is said.</para>
///
/// <para>Reading starts nothing: no transport, no dispatch, no supervision (PERFORM1a onward). The CLI's <c>steps.ts</c> is this
/// class's twin: <c>plugin-steps.test.ts</c> parses <c>PluginStepsTests</c>' theory and holds its own table to it, cell for cell.</para>
/// </remarks>
public static class PluginSteps
{
    /// <summary>The point a performed step is asked on (§D6), and the only one its <c>perform</c> may name.</summary>
    public const string PerformPoint = "work/perform";

    /// <summary>The point that answers what became of an attempt (§D6): a <c>perform</c>'s <c>reconcile</c> names it.</summary>
    public const string ReconcilePoint = "work/reconcile";

    /// <summary>The point a plugin removes what it created on (§D3): a resource's <c>cleanup</c> names it.</summary>
    public const string CleanupPoint = "work/cleanup";

    /// <summary>A step's or a port's name: lowercase letters, digits and dashes, at most 40 (§C4). Twin: the CLI's <c>STEP_NAME</c>.</summary>
    public static readonly Regex NameShape = new(@"^[a-z0-9][a-z0-9-]{0,39}\z", RegexOptions.CultureInvariant);

    /// <summary>
    /// A schema's name, <c>&lt;owner&gt;/&lt;name&gt;@&lt;version&gt;</c> (<c>acme.deploy/receipt@1</c>, <c>daoris/quest.close@1</c>): its
    /// owner a plugin id by <see cref="PluginCatalog.IsId"/>, the one owner of that shape (REFAC1); its name words of lowercase letters
    /// and digits parted by single dots or dashes; its version a whole number from 1. Compared ordinally (WIRE1a's choice 2), and with
    /// no <c>:</c>, since a reference is <c>&lt;schema&gt;:&lt;id&gt;</c> (§D6). Twin: the CLI's <c>isSchema</c>.
    /// </summary>
    public static bool IsSchema(string text) =>
        SchemaParts.Match(text) is { Success: true } match && PluginCatalog.IsId(match.Groups["owner"].Value);

    private static readonly Regex SchemaParts =
        new(@"^(?<owner>[^/]+)/[a-z0-9]+(?:[.-][a-z0-9]+)*@[1-9][0-9]*\z", RegexOptions.CultureInvariant);

    /// <summary>A plugin's step's kind and id: <c>&lt;plugin&gt;/&lt;step&gt;</c>, a plugin's own node (<see cref="NodeKinds.IsPlugin"/>).</summary>
    public static string KindOf(string plugin, string step) => $"{plugin}/{step}";

    /// <summary>
    /// The manifest's <c>steps</c> and <c>resources</c>, read from its <paramref name="root"/> object for plugin <paramref name="plugin"/>:
    /// absent is none, so a manifest written before reads as it did. Each step's shape is read here, field by field in the manifest's
    /// order, and then its declaration is judged by <see cref="NodeDeclarations.Problem"/>, whose code names the field the words say.
    /// </summary>
    public static PluginStepsRead Read(string plugin, JsonElement root)
    {
        var steps = new List<NodeDeclaration>();
        if (root.TryGetProperty("steps", out var declared))
        {
            if (declared.ValueKind != JsonValueKind.Array) return Refused(PluginStepCodes.Steps, null);
            foreach (var row in declared.EnumerateArray())
            {
                var (step, code, name) = Step(plugin, row, steps);
                if (code is not null) return Refused(code, name);
                steps.Add(step!);
            }
        }

        var resources = new List<PluginResource>();
        if (root.TryGetProperty("resources", out var made))
        {
            if (made.ValueKind != JsonValueKind.Array) return Refused(PluginStepCodes.Resources, null);
            foreach (var row in made.EnumerateArray())
            {
                var schema = row.ValueKind == JsonValueKind.Object && row.TryGetProperty("schema", out var named) ? Schema(named) : null;
                if (schema is null) return Refused(PluginStepCodes.Schema, null);
                if (resources.Any(resource => resource.Schema == schema)) return Refused(PluginStepCodes.ResourceTwice, schema);
                var role = PluginResourceRoles.Temporary;
                if (row.TryGetProperty("role", out var declaredRole))
                {
                    if (declaredRole.ValueKind != JsonValueKind.String || !PluginResourceRoles.All.Contains(declaredRole.GetString(), StringComparer.Ordinal))
                    {
                        return Refused(PluginStepCodes.Role, schema);
                    }

                    role = declaredRole.GetString()!;
                }

                // §D3: who cleans it is said, never guessed; an unknown owner would block the unit's cleanup.
                if (!row.TryGetProperty("cleanup", out var cleanup) || !Is(cleanup, CleanupPoint)) return Refused(PluginStepCodes.Cleanup, schema);
                resources.Add(new PluginResource(schema, role, CleanupPoint));
            }
        }

        return new PluginStepsRead(steps, resources, null, null);
    }

    /// <summary>One declared step, or the code of its first problem and the name it is said by.</summary>
    private static (NodeDeclaration? Step, string? Code, string? Name) Step(string plugin, JsonElement row, List<NodeDeclaration> read)
    {
        var name = row.ValueKind == JsonValueKind.Object && row.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String
            ? id.GetString()!
            : null;
        if (name is null || !NameShape.IsMatch(name)) return (null, PluginStepCodes.Id, null);
        var kind = KindOf(plugin, name);
        if (read.Any(step => step.Kind == kind)) return (null, PluginStepCodes.StepTwice, name);

        (NodeDeclaration?, string?, string?) Broken(string code) => (null, code, name);

        var version = 1;
        if (row.TryGetProperty("version", out var declaredVersion))
        {
            if (Whole(declaredVersion) is not { } whole) return Broken("version");
            version = whole;
        }

        var reach = "";
        if (row.TryGetProperty("reach", out var declaredReach))
        {
            if (declaredReach.ValueKind != JsonValueKind.String) return Broken("reach");
            reach = declaredReach.GetString()!;
        }

        if (Ports(row, "inputs") is not { } inputs) return Broken("inputs");
        if (Ports(row, "outputs") is not { } outputs) return Broken("outputs");

        string? evidence = null;
        if (row.TryGetProperty("evidence", out var declaredEvidence))
        {
            evidence = declaredEvidence.ValueKind == JsonValueKind.Object && declaredEvidence.TryGetProperty("schema", out var schema) ? Schema(schema) : null;
            if (evidence is null) return Broken("evidence");
        }

        NodeRetry? retry = null;
        if (row.TryGetProperty("retry", out var declaredRetry))
        {
            if (declaredRetry.ValueKind != JsonValueKind.Object || !declaredRetry.TryGetProperty("maxAttempts", out var attempts) || Whole(attempts) is not { } most)
            {
                return Broken("retry");
            }

            var delays = new List<int>();
            if (declaredRetry.TryGetProperty("delaysSeconds", out var declaredDelays))
            {
                if (declaredDelays.ValueKind != JsonValueKind.Array) return Broken("retry");
                foreach (var delay in declaredDelays.EnumerateArray())
                {
                    if (Whole(delay) is not { } seconds) return Broken("retry");
                    delays.Add(seconds);
                }
            }

            retry = new NodeRetry(most, delays);
        }

        int? timeout = null;
        if (row.TryGetProperty("timeoutSeconds", out var declaredTimeout))
        {
            if (Whole(declaredTimeout) is not { } seconds) return Broken("timeoutSeconds");
            timeout = seconds;
        }

        if (Flag(row, "skippable", true) is not { } skippable) return Broken("skippable");
        if (Flag(row, "rejectable", false) is not { } rejectable) return Broken("rejectable");

        NodePerform? perform = null;
        if (row.TryGetProperty("perform", out var declaredPerform))
        {
            if (declaredPerform.ValueKind != JsonValueKind.Object
                || (declaredPerform.TryGetProperty("point", out var point) && !Is(point, PerformPoint)))
            {
                return Broken("perform");
            }

            if (Flag(declaredPerform, "idempotent", false) is not { } idempotent) return Broken(PluginStepCodes.Idempotent);
            if (declaredPerform.TryGetProperty("reconcile", out var reconcile) && !Is(reconcile, ReconcilePoint)) return Broken(PluginStepCodes.Reconcile);

            var transient = new List<string>();
            if (declaredPerform.TryGetProperty("transient", out var codes))
            {
                if (codes.ValueKind != JsonValueKind.Array) return Broken("transient");
                foreach (var code in codes.EnumerateArray())
                {
                    if (code.ValueKind != JsonValueKind.String) return Broken("transient");
                    transient.Add(code.GetString()!);
                }
            }

            perform = new NodePerform(idempotent, reconcile.ValueKind == JsonValueKind.String, transient);
        }

        var step = new NodeDeclaration(kind, kind, version, reach)
        {
            Inputs = inputs,
            Outputs = outputs,
            Evidence = evidence,
            Retry = retry,
            TimeoutSeconds = timeout,
            Skippable = skippable,
            Rejectable = rejectable,
            Perform = perform,
        };

        // The one model's own checks, whose code is the field the words name.
        return NodeDeclarations.Problem(step) is { } problem ? Broken(problem) : (step, null, name);
    }

    /// <summary>A port map: absent is none; otherwise each name of <see cref="NameShape"/> with its schema, the last of a name written twice.</summary>
    private static Dictionary<string, string>? Ports(JsonElement row, string field)
    {
        var ports = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!row.TryGetProperty(field, out var declared)) return ports;
        if (declared.ValueKind != JsonValueKind.Object) return null;

        // JSON's last of a name written twice, as the CLI's parser keeps it: what is judged is what is kept.
        var last = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var port in declared.EnumerateObject()) last[port.Name] = port.Value;
        foreach (var (name, value) in last)
        {
            if (!NameShape.IsMatch(name) || Schema(value) is not { } schema) return null;
            ports[name] = schema;
        }

        return ports;
    }

    /// <summary>A schema's name (<see cref="IsSchema"/>), or null.</summary>
    private static string? Schema(JsonElement value) =>
        value.ValueKind == JsonValueKind.String && IsSchema(value.GetString()!) ? value.GetString() : null;

    /// <summary>A whole number a manifest writes, <c>2</c> or <c>2.0</c> alike, within an <see cref="int"/>; null for anything else.</summary>
    private static int? Whole(JsonElement value) =>
        value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) && number == Math.Floor(number)
        && number >= int.MinValue && number <= int.MaxValue
            ? (int)number
            : null;

    /// <summary>A JSON <c>true</c> or <c>false</c>, <paramref name="absent"/> where it is not written; null for anything else.</summary>
    private static bool? Flag(JsonElement row, string field, bool absent) =>
        !row.TryGetProperty(field, out var value) ? absent
        : value.ValueKind == JsonValueKind.True ? true
        : value.ValueKind == JsonValueKind.False ? false
        : null;

    private static bool Is(JsonElement value, string text) => value.ValueKind == JsonValueKind.String && value.GetString() == text;

    private static PluginStepsRead Refused(string code, string? name) => new([], [], code, Words(code, name));

    /// <summary>The words each code is said in, naming the step by its manifest's id or the resource by its schema. Twin: <c>steps.ts</c>.</summary>
    public static string Words(string code, string? name) => code switch
    {
        PluginStepCodes.Steps => "`steps` must be an array of the steps the plugin declares.",
        PluginStepCodes.Id => "a declared step needs an `id` — lowercase letters, digits and dashes, at most 40; its kind is `<plugin>/<id>`.",
        PluginStepCodes.StepTwice => $"step `{name}` is declared twice.",
        "version" => $"step `{name}`'s `version` must be a whole number, 1 or more: an acceptance binds it, so a new version lapses the old ones.",
        "reach" => $"step `{name}`'s `reach` must be `observed`, which the plugin reports, or `performed`, which Daoris asks the plugin to do.",
        "inputs" => $"step `{name}`'s `inputs` must name each input, in lowercase letters, digits and dashes, with the schema it takes, like `acme.build/artifact@1`.",
        "outputs" => $"step `{name}`'s `outputs` must name each output, in lowercase letters, digits and dashes and never `evidence`, with its schema, like `acme.deploy/preview@1`.",
        "evidence" => $"step `{name}`'s `evidence` must be an object whose `schema` names what it is reached on, like `acme.deploy/receipt@1`.",
        "retry" => $"step `{name}`'s `retry` must be `maxAttempts`, 1 or more, and `delaysSeconds`, each 0 or more; more than one attempt only where its `perform` lists `transient` codes and is `idempotent` or has `reconcile`.",
        "timeoutSeconds" => $"step `{name}`'s `timeoutSeconds` must be a whole number of seconds, above 0.",
        "skippable" => $"step `{name}`'s `skippable` must be `true` or `false`: whether the person may pass it over.",
        "rejectable" => $"step `{name}`'s `rejectable` must be `true` or `false`: whether the person may reject it.",
        "perform" => $"step `{name}`'s `perform` is a `performed` step's alone: an object whose `point`, where it names one, is `work/perform`.",
        PluginStepCodes.Idempotent => $"step `{name}`'s `idempotent` must be `true` or `false`: whether the plugin deduplicates a redelivered key.",
        PluginStepCodes.Reconcile => $"step `{name}`'s `reconcile` must be `work/reconcile`, the point that answers what became of an attempt.",
        "transient" => $"step `{name}`'s `transient` must list the failure codes worth retrying, once each, in lowercase letters, digits and dashes, and only with a `retry` of more than one attempt.",
        PluginStepCodes.Resources => "`resources` must be an array of what the plugin's steps create.",
        PluginStepCodes.Schema => "a declared resource needs a `schema` — what it is, like `acme.deploy/preview@1`.",
        PluginStepCodes.ResourceTwice => $"resource `{name}` is declared twice.",
        PluginStepCodes.Role => $"resource `{name}`'s `role` must be `temporary`, removed at the end, or `result`, kept as the work's result.",
        PluginStepCodes.Cleanup => $"resource `{name}`'s `cleanup` must be `work/cleanup`: the plugin removes what it made through its own cleanup point, and Daoris never does.",
        _ => $"step `{name}` is no node this build reads (`{code}`).",
    };
}

/// <summary>
/// The codes a manifest's steps and resources are refused by (FABRIC-UNIT1d), beside the field codes <see cref="NodeDeclarations.Problem"/>
/// returns (<c>version</c>, <c>reach</c>, <c>retry</c>, <c>timeoutSeconds</c>, <c>outputs</c>, <c>perform</c>, <c>transient</c>), which
/// a step's shape reuses for the same field. Twin: the CLI's <c>steps.ts</c>.
/// </summary>
public static class PluginStepCodes
{
    public const string Steps = "steps";
    public const string Id = "id";
    public const string StepTwice = "step-twice";
    public const string Idempotent = "idempotent";
    public const string Reconcile = "reconcile";
    public const string Resources = "resources";
    public const string Schema = "schema";
    public const string ResourceTwice = "resource-twice";
    public const string Role = "role";
    public const string Cleanup = "cleanup";
}
