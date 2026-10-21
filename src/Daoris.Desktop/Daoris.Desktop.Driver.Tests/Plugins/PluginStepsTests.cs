using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A plugin's steps and resources (FABRIC-UNIT1d; the fabric events design §D4, §D6, §D9, §E2): the manifest declares a step once, and
/// it is read into the one <see cref="NodeDeclaration"/> a built-in is, judged by <see cref="NodeDeclarations.Problem"/>. The rules' one
/// table: the CLI's <c>steps.ts</c> held its twin to <see cref="The_steps_read_as_the_cli_reads_them"/> until it left with
/// <c>daoris plugin</c>'s TypeScript (PUSH1f2f).
/// </summary>
/// <remarks>🔴 <b>A problem refuses the plugin</b>, as a skill's does (§C4): its code and words, and nothing of the plugin is taken.</remarks>
public sealed class PluginStepsTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-plugin-steps-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>
    /// The hooks a row's manifest speaks with where it names none (FABRIC-PERFORM1a): a performed step is asked at `work/perform`, so
    /// the hooks must name that point, and a row about anything else is not refused for it. Twin: <c>HOOKS</c> in the CLI's test.
    /// </summary>
    private const string Hooks = """, "hooks": { "command": ["node", "deploy.mjs"], "points": ["work/perform"] }""";

    /// <summary>The manifest a row declares: its id, the default hooks unless it names its own, and what it declares. Twin: <c>manifest</c>.</summary>
    private static string Manifest(string declared) =>
        $"{{ \"id\": \"acme.deploy\"{(declared.Contains("\"hooks\"", StringComparison.Ordinal) ? "" : Hooks)}{declared} }}";

    private void Plugin(string declared)
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.Plugins, "acme.deploy");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, PluginCatalog.ManifestName), Manifest(declared));
    }

    /// <summary>One step as the table says it: its kind and version, its reach, then each declared part. Twin: <c>said</c> in the CLI's test.</summary>
    private static string Said(NodeDeclaration step)
    {
        var parts = new List<string> { $"{step.Kind}@{step.Version} {step.Reach}" };
        if (step.Inputs.Count > 0) parts.Add("in " + string.Join(",", step.Inputs.Select(port => $"{port.Key}={port.Value}")));
        if (step.Outputs.Count > 0) parts.Add("out " + string.Join(",", step.Outputs.Select(port => $"{port.Key}={port.Value}")));
        if (step.Evidence is { } evidence) parts.Add($"evidence {evidence}");
        if (step.Retry is { } retry) parts.Add($"retry {retry.MaxAttempts}/{string.Join("+", retry.DelaysSeconds)}");
        if (step.TimeoutSeconds is { } timeout) parts.Add($"timeout {timeout}");
        if (!step.Skippable) parts.Add("no-skip");
        if (step.Rejectable) parts.Add("reject");
        if (step.Perform is { } perform)
        {
            parts.Add("perform");
            if (perform.Idempotent) parts.Add("idempotent");
            if (perform.Reconcile) parts.Add("reconcile");
            if (perform.Transient.Count > 0) parts.Add("transient=" + string.Join("+", perform.Transient));
        }

        return string.Join(" ", parts);
    }

    private static string Said(PluginResource resource) => $"{resource.Schema} {resource.Role} {resource.Cleanup}";

    /// <summary>
    /// The steps' and resources' table: [case, what the manifest declares after its id, the problem's code and its words, or null and
    /// null, then the steps read and the resources read, each as <see cref="Said(NodeDeclaration)"/> says it, joined by <c>; </c>]. The
    /// rules are checked in this order and the first broken one is said. The twin is <c>plugin-steps.test.ts</c>'s <c>STEP_ROWS</c>.
    /// </summary>
    [Theory]
    [InlineData("no steps", "", null, null, "", "")]
    [InlineData("none declared", """, "steps": [], "resources": [] """, null, null, "", "")]
    [InlineData("an observed step, the least", """, "steps": [{ "id": "deployed", "reach": "observed" }] """, null, null, "acme.deploy/deployed@1 observed", "")]
    [InlineData("a performed step, the least", """, "steps": [{ "id": "deploy", "reach": "performed" }] """, null, null, "acme.deploy/deploy@1 performed", "")]
    [InlineData("a whole performed step", """, "steps": [{ "id": "deploy", "version": 2, "reach": "performed", "inputs": { "release": "acme.build/artifact@1" }, "outputs": { "preview": "acme.deploy/preview@1", "log": "acme.deploy/log@1" }, "evidence": { "schema": "acme.deploy/receipt@1" }, "retry": { "maxAttempts": 3, "delaysSeconds": [5, 30] }, "timeoutSeconds": 600, "skippable": false, "rejectable": true, "perform": { "point": "work/perform", "idempotent": true, "reconcile": "work/reconcile", "transient": ["unavailable", "rate-limited", "timeout"] } }] """, null, null, "acme.deploy/deploy@2 performed in release=acme.build/artifact@1 out log=acme.deploy/log@1,preview=acme.deploy/preview@1 evidence acme.deploy/receipt@1 retry 3/5+30 timeout 600 no-skip reject perform idempotent reconcile transient=unavailable+rate-limited+timeout", "")]
    [InlineData("a retry with no delays, reconciled", """, "steps": [{ "id": "deploy", "reach": "performed", "retry": { "maxAttempts": 2 }, "perform": { "reconcile": "work/reconcile", "transient": ["unavailable"] } }] """, null, null, "acme.deploy/deploy@1 performed retry 2/ perform reconcile transient=unavailable", "")]
    [InlineData("a retry of one attempt is none", """, "steps": [{ "id": "deploy", "reach": "performed", "retry": { "maxAttempts": 1 } }] """, null, null, "acme.deploy/deploy@1 performed retry 1/", "")]
    [InlineData("perform naming no point", """, "steps": [{ "id": "deploy", "reach": "performed", "perform": {} }] """, null, null, "acme.deploy/deploy@1 performed perform", "")]
    [InlineData("two steps, in their order", """, "steps": [{ "id": "build", "reach": "performed" }, { "id": "deployed", "reach": "observed", "skippable": true, "rejectable": false }] """, null, null, "acme.deploy/build@1 performed; acme.deploy/deployed@1 observed", "")]
    [InlineData("a version written 1.0", """, "steps": [{ "id": "deploy", "version": 1.0, "reach": "performed" }] """, null, null, "acme.deploy/deploy@1 performed", "")]
    [InlineData("an id of 40", """, "steps": [{ "id": "abcdefghijabcdefghijabcdefghijabcdefghij", "reach": "observed" }] """, null, null, "acme.deploy/abcdefghijabcdefghijabcdefghijabcdefghij@1 observed", "")]
    [InlineData("a field it does not know is passed over", """, "steps": [{ "id": "deploy", "reach": "performed", "label": "Deploy the preview" }] """, null, null, "acme.deploy/deploy@1 performed", "")]
    [InlineData("resources", """, "resources": [{ "schema": "acme.deploy/preview@1", "cleanup": "work/cleanup" }, { "schema": "acme.deploy/site@1", "role": "result", "cleanup": "work/cleanup" }] """, null, null, "", "acme.deploy/preview@1 temporary work/cleanup; acme.deploy/site@1 result work/cleanup")]
    [InlineData("steps not an array", """, "steps": { "id": "deploy" } """, "steps", "`steps` must be an array of the steps the plugin declares.", "", "")]
    [InlineData("steps that are null", """, "steps": null """, "steps", "`steps` must be an array of the steps the plugin declares.", "", "")]
    [InlineData("a step that is not an object", """, "steps": ["deploy"] """, "id", "a declared step needs an `id` — lowercase letters, digits and dashes, at most 40; its kind is `<plugin>/<id>`.", "", "")]
    [InlineData("a step with no id", """, "steps": [{ "reach": "performed" }] """, "id", "a declared step needs an `id` — lowercase letters, digits and dashes, at most 40; its kind is `<plugin>/<id>`.", "", "")]
    [InlineData("an id in capitals", """, "steps": [{ "id": "Deploy", "reach": "performed" }] """, "id", "a declared step needs an `id` — lowercase letters, digits and dashes, at most 40; its kind is `<plugin>/<id>`.", "", "")]
    [InlineData("an id with a slash", """, "steps": [{ "id": "acme/deploy", "reach": "performed" }] """, "id", "a declared step needs an `id` — lowercase letters, digits and dashes, at most 40; its kind is `<plugin>/<id>`.", "", "")]
    [InlineData("an id of 41", """, "steps": [{ "id": "abcdefghijabcdefghijabcdefghijabcdefghijk", "reach": "observed" }] """, "id", "a declared step needs an `id` — lowercase letters, digits and dashes, at most 40; its kind is `<plugin>/<id>`.", "", "")]
    [InlineData("a step twice", """, "steps": [{ "id": "deploy", "reach": "performed" }, { "id": "deploy", "reach": "observed" }] """, "step-twice", "step `deploy` is declared twice.", "", "")]
    [InlineData("a version that is text", """, "steps": [{ "id": "deploy", "version": "2", "reach": "performed" }] """, "version", "step `deploy`'s `version` must be a whole number, 1 or more: an acceptance binds it, so a new version lapses the old ones.", "", "")]
    [InlineData("a version of 0", """, "steps": [{ "id": "deploy", "version": 0, "reach": "performed" }] """, "version", "step `deploy`'s `version` must be a whole number, 1 or more: an acceptance binds it, so a new version lapses the old ones.", "", "")]
    [InlineData("a version with a fraction", """, "steps": [{ "id": "deploy", "version": 1.5, "reach": "performed" }] """, "version", "step `deploy`'s `version` must be a whole number, 1 or more: an acceptance binds it, so a new version lapses the old ones.", "", "")]
    [InlineData("no reach", """, "steps": [{ "id": "deploy" }] """, "reach", "step `deploy`'s `reach` must be `observed`, which the plugin reports, or `performed`, which Daoris asks the plugin to do.", "", "")]
    [InlineData("a reach a person presses", """, "steps": [{ "id": "deploy", "reach": "pressed" }] """, "reach", "step `deploy`'s `reach` must be `observed`, which the plugin reports, or `performed`, which Daoris asks the plugin to do.", "", "")]
    [InlineData("a reach that is not text", """, "steps": [{ "id": "deploy", "reach": 1 }] """, "reach", "step `deploy`'s `reach` must be `observed`, which the plugin reports, or `performed`, which Daoris asks the plugin to do.", "", "")]
    [InlineData("inputs that are a list", """, "steps": [{ "id": "deploy", "reach": "performed", "inputs": ["release"] }] """, "inputs", "step `deploy`'s `inputs` must name each input, in lowercase letters, digits and dashes, with the schema it takes, like `acme.build/artifact@1`.", "", "")]
    [InlineData("an input with no schema", """, "steps": [{ "id": "deploy", "reach": "performed", "inputs": { "release": "artifact" } }] """, "inputs", "step `deploy`'s `inputs` must name each input, in lowercase letters, digits and dashes, with the schema it takes, like `acme.build/artifact@1`.", "", "")]
    [InlineData("an input named in capitals", """, "steps": [{ "id": "deploy", "reach": "performed", "inputs": { "Release": "acme.build/artifact@1" } }] """, "inputs", "step `deploy`'s `inputs` must name each input, in lowercase letters, digits and dashes, with the schema it takes, like `acme.build/artifact@1`.", "", "")]
    [InlineData("an output named evidence", """, "steps": [{ "id": "deploy", "reach": "performed", "outputs": { "evidence": "acme.deploy/receipt@1" } }] """, "outputs", "step `deploy`'s `outputs` must name each output, in lowercase letters, digits and dashes and never `evidence`, with its schema, like `acme.deploy/preview@1`.", "", "")]
    [InlineData("an output schema with a colon", """, "steps": [{ "id": "deploy", "reach": "performed", "outputs": { "preview": "acme.deploy/preview@1:p1" } }] """, "outputs", "step `deploy`'s `outputs` must name each output, in lowercase letters, digits and dashes and never `evidence`, with its schema, like `acme.deploy/preview@1`.", "", "")]
    [InlineData("evidence as text", """, "steps": [{ "id": "deploy", "reach": "performed", "evidence": "acme.deploy/receipt@1" }] """, "evidence", "step `deploy`'s `evidence` must be an object whose `schema` names what it is reached on, like `acme.deploy/receipt@1`.", "", "")]
    [InlineData("evidence with no version", """, "steps": [{ "id": "deploy", "reach": "performed", "evidence": { "schema": "acme.deploy/receipt" } }] """, "evidence", "step `deploy`'s `evidence` must be an object whose `schema` names what it is reached on, like `acme.deploy/receipt@1`.", "", "")]
    [InlineData("a retry of no attempts", """, "steps": [{ "id": "deploy", "reach": "performed", "retry": { "maxAttempts": 0 } }] """, "retry", "step `deploy`'s `retry` must be `maxAttempts`, 1 or more, and `delaysSeconds`, each 0 or more; more than one attempt only where its `perform` lists `transient` codes and is `idempotent` or has `reconcile`.", "", "")]
    [InlineData("a negative delay", """, "steps": [{ "id": "deploy", "reach": "performed", "retry": { "maxAttempts": 1, "delaysSeconds": [-5] } }] """, "retry", "step `deploy`'s `retry` must be `maxAttempts`, 1 or more, and `delaysSeconds`, each 0 or more; more than one attempt only where its `perform` lists `transient` codes and is `idempotent` or has `reconcile`.", "", "")]
    [InlineData("a retry with no maxAttempts", """, "steps": [{ "id": "deploy", "reach": "performed", "retry": { "delaysSeconds": [5] } }] """, "retry", "step `deploy`'s `retry` must be `maxAttempts`, 1 or more, and `delaysSeconds`, each 0 or more; more than one attempt only where its `perform` lists `transient` codes and is `idempotent` or has `reconcile`.", "", "")]
    [InlineData("a retry with no transient code", """, "steps": [{ "id": "deploy", "reach": "performed", "retry": { "maxAttempts": 3 }, "perform": { "idempotent": true } }] """, "retry", "step `deploy`'s `retry` must be `maxAttempts`, 1 or more, and `delaysSeconds`, each 0 or more; more than one attempt only where its `perform` lists `transient` codes and is `idempotent` or has `reconcile`.", "", "")]
    [InlineData("a retry neither idempotent nor reconciled", """, "steps": [{ "id": "deploy", "reach": "performed", "retry": { "maxAttempts": 3 }, "perform": { "transient": ["unavailable"] } }] """, "retry", "step `deploy`'s `retry` must be `maxAttempts`, 1 or more, and `delaysSeconds`, each 0 or more; more than one attempt only where its `perform` lists `transient` codes and is `idempotent` or has `reconcile`.", "", "")]
    [InlineData("a retry on an observed step", """, "steps": [{ "id": "deployed", "reach": "observed", "retry": { "maxAttempts": 2 } }] """, "retry", "step `deployed`'s `retry` must be `maxAttempts`, 1 or more, and `delaysSeconds`, each 0 or more; more than one attempt only where its `perform` lists `transient` codes and is `idempotent` or has `reconcile`.", "", "")]
    [InlineData("a timeout of 0", """, "steps": [{ "id": "deploy", "reach": "performed", "timeoutSeconds": 0 }] """, "timeoutSeconds", "step `deploy`'s `timeoutSeconds` must be a whole number of seconds, above 0.", "", "")]
    [InlineData("a timeout that is text", """, "steps": [{ "id": "deploy", "reach": "performed", "timeoutSeconds": "600" }] """, "timeoutSeconds", "step `deploy`'s `timeoutSeconds` must be a whole number of seconds, above 0.", "", "")]
    [InlineData("skippable as text", """, "steps": [{ "id": "deploy", "reach": "performed", "skippable": "no" }] """, "skippable", "step `deploy`'s `skippable` must be `true` or `false`: whether the person may pass it over.", "", "")]
    [InlineData("rejectable as a number", """, "steps": [{ "id": "deploy", "reach": "performed", "rejectable": 1 }] """, "rejectable", "step `deploy`'s `rejectable` must be `true` or `false`: whether the person may reject it.", "", "")]
    [InlineData("perform on an observed step", """, "steps": [{ "id": "deployed", "reach": "observed", "perform": {} }] """, "perform", "step `deployed`'s `perform` is a `performed` step's alone: an object whose `point`, where it names one, is `work/perform`.", "", "")]
    [InlineData("perform as true", """, "steps": [{ "id": "deploy", "reach": "performed", "perform": true }] """, "perform", "step `deploy`'s `perform` is a `performed` step's alone: an object whose `point`, where it names one, is `work/perform`.", "", "")]
    [InlineData("perform at another point", """, "steps": [{ "id": "deploy", "reach": "performed", "perform": { "point": "work/land" } }] """, "perform", "step `deploy`'s `perform` is a `performed` step's alone: an object whose `point`, where it names one, is `work/perform`.", "", "")]
    [InlineData("idempotent as text", """, "steps": [{ "id": "deploy", "reach": "performed", "perform": { "idempotent": "yes" } }] """, "idempotent", "step `deploy`'s `idempotent` must be `true` or `false`: whether the plugin deduplicates a redelivered key.", "", "")]
    [InlineData("reconcile as true", """, "steps": [{ "id": "deploy", "reach": "performed", "perform": { "reconcile": true } }] """, "reconcile", "step `deploy`'s `reconcile` must be `work/reconcile`, the point that answers what became of an attempt.", "", "")]
    [InlineData("reconcile at another point", """, "steps": [{ "id": "deploy", "reach": "performed", "perform": { "reconcile": "work/perform" } }] """, "reconcile", "step `deploy`'s `reconcile` must be `work/reconcile`, the point that answers what became of an attempt.", "", "")]
    [InlineData("transient as text", """, "steps": [{ "id": "deploy", "reach": "performed", "retry": { "maxAttempts": 3 }, "perform": { "idempotent": true, "transient": "unavailable" } }] """, "transient", "step `deploy`'s `transient` must list the failure codes worth retrying, once each, in lowercase letters, digits and dashes, and only with a `retry` of more than one attempt.", "", "")]
    [InlineData("a transient code in capitals", """, "steps": [{ "id": "deploy", "reach": "performed", "retry": { "maxAttempts": 3 }, "perform": { "idempotent": true, "transient": ["Unavailable"] } }] """, "transient", "step `deploy`'s `transient` must list the failure codes worth retrying, once each, in lowercase letters, digits and dashes, and only with a `retry` of more than one attempt.", "", "")]
    [InlineData("a transient code twice", """, "steps": [{ "id": "deploy", "reach": "performed", "retry": { "maxAttempts": 3 }, "perform": { "idempotent": true, "transient": ["unavailable", "unavailable"] } }] """, "transient", "step `deploy`'s `transient` must list the failure codes worth retrying, once each, in lowercase letters, digits and dashes, and only with a `retry` of more than one attempt.", "", "")]
    [InlineData("transient codes with no retry", """, "steps": [{ "id": "deploy", "reach": "performed", "perform": { "idempotent": true, "transient": ["unavailable"] } }] """, "transient", "step `deploy`'s `transient` must list the failure codes worth retrying, once each, in lowercase letters, digits and dashes, and only with a `retry` of more than one attempt.", "", "")]
    [InlineData("the first broken step is said", """, "steps": [{ "id": "deploy", "reach": "performed" }, { "id": "Smoke", "reach": "observed" }] """, "id", "a declared step needs an `id` — lowercase letters, digits and dashes, at most 40; its kind is `<plugin>/<id>`.", "", "")]
    [InlineData("resources not an array", """, "resources": { "schema": "acme.deploy/preview@1" } """, "resources", "`resources` must be an array of what the plugin's steps create.", "", "")]
    [InlineData("a resource with no schema", """, "resources": [{ "cleanup": "work/cleanup" }] """, "schema", "a declared resource needs a `schema` — what it is, like `acme.deploy/preview@1`.", "", "")]
    [InlineData("a resource schema with no owner", """, "resources": [{ "schema": "preview@1", "cleanup": "work/cleanup" }] """, "schema", "a declared resource needs a `schema` — what it is, like `acme.deploy/preview@1`.", "", "")]
    [InlineData("a resource twice", """, "resources": [{ "schema": "acme.deploy/preview@1", "cleanup": "work/cleanup" }, { "schema": "acme.deploy/preview@1", "role": "result", "cleanup": "work/cleanup" }] """, "resource-twice", "resource `acme.deploy/preview@1` is declared twice.", "", "")]
    [InlineData("a role it does not know", """, "resources": [{ "schema": "acme.deploy/preview@1", "role": "evidence", "cleanup": "work/cleanup" }] """, "role", "resource `acme.deploy/preview@1`'s `role` must be `temporary`, removed at the end, or `result`, kept as the work's result.", "", "")]
    [InlineData("no cleanup", """, "resources": [{ "schema": "acme.deploy/preview@1" }] """, "cleanup", "resource `acme.deploy/preview@1`'s `cleanup` must be `work/cleanup`: the plugin removes what it made through its own cleanup point, and Daoris never does.", "", "")]
    [InlineData("cleanup at another point", """, "resources": [{ "schema": "acme.deploy/preview@1", "cleanup": "work/perform" }] """, "cleanup", "resource `acme.deploy/preview@1`'s `cleanup` must be `work/cleanup`: the plugin removes what it made through its own cleanup point, and Daoris never does.", "", "")]
    [InlineData("a step's problem before a resource's", """, "steps": [{ "id": "deploy" }], "resources": [{ "schema": "preview" }] """, "reach", "step `deploy`'s `reach` must be `observed`, which the plugin reports, or `performed`, which Daoris asks the plugin to do.", "", "")]
    [InlineData("a performed step asked nowhere", """, "hooks": { "command": ["node", "deploy.mjs"], "points": ["work/watch", "work/step"] }, "steps": [{ "id": "deploy", "reach": "performed" }] """, "perform-point", "step `deploy` is performed, so `hooks.points` must name `work/perform`: the point Daoris asks it at.", "", "")]
    [InlineData("an observed step asked nowhere", """, "hooks": { "command": ["node", "deploy.mjs"], "points": ["work/watch", "work/step"] }, "steps": [{ "id": "deployed", "reach": "observed" }] """, null, null, "acme.deploy/deployed@1 observed", "")]
    [InlineData("a step's own problem before its point", """, "hooks": { "command": ["node", "deploy.mjs"], "points": ["work/watch"] }, "steps": [{ "id": "deploy", "reach": "performed", "timeoutSeconds": 0 }] """, "timeoutSeconds", "step `deploy`'s `timeoutSeconds` must be a whole number of seconds, above 0.", "", "")]
    [InlineData("the second step asked nowhere", """, "hooks": { "command": ["node", "deploy.mjs"], "points": ["quest/consider"] }, "steps": [{ "id": "deployed", "reach": "observed" }, { "id": "deploy", "reach": "performed" }] """, "perform-point", "step `deploy` is performed, so `hooks.points` must name `work/perform`: the point Daoris asks it at.", "", "")]
    public void The_steps_read_as_the_cli_reads_them(string name, string declared, string? code, string? problem, string steps, string resources)
    {
        Plugin(declared);

        var entry = Assert.Single(PluginCatalog.Load(_home).Plugins);
        Assert.True(entry.Problem == problem, $"{name}: {entry.Problem}");
        Assert.Equal(steps, string.Join("; ", entry.Manifest.Steps.Select(Said)));
        Assert.Equal(resources, string.Join("; ", entry.Manifest.Resources.Select(Said)));
        Assert.All(entry.Manifest.Steps, step => Assert.Equal(step.Kind, step.Id));

        // The reader's own answer names the code the sentence says.
        using var root = JsonDocument.Parse(Manifest(declared));
        var read = PluginSteps.Read("acme.deploy", root.RootElement);
        Assert.Equal((code, problem), (read.Code, read.Problem));
    }

    /// <summary>
    /// A performed step is asked at `work/perform` on the plugin's process (FABRIC-PERFORM1a, UNIT1d's choice 8): a manifest whose hooks
    /// do not name that point, or that has no hooks at all, is refused when it is read, and an observed step needs no point.
    /// </summary>
    [Theory]
    [InlineData("""{ "id": "acme.deploy", "steps": [{ "id": "deploy", "reach": "performed" }] }""", "perform-point")]
    [InlineData("""{ "id": "acme.deploy", "hooks": { "command": ["node", "deploy.mjs"] }, "steps": [{ "id": "deploy", "reach": "performed" }] }""", "perform-point")]
    [InlineData("""{ "id": "acme.deploy", "hooks": { "command": ["node", "deploy.mjs"], "points": "work/perform" }, "steps": [{ "id": "deploy", "reach": "performed" }] }""", "perform-point")]
    [InlineData("""{ "id": "acme.deploy", "steps": [{ "id": "deployed", "reach": "observed" }] }""", null)]
    [InlineData("""{ "id": "acme.deploy", "hooks": { "command": ["node", "deploy.mjs"], "points": ["work/perform"] }, "steps": [{ "id": "deploy", "reach": "performed" }] }""", null)]
    public void A_performed_step_needs_its_point_in_the_hooks(string manifest, string? code)
    {
        using var root = JsonDocument.Parse(manifest);

        var read = PluginSteps.Read("acme.deploy", root.RootElement);

        Assert.Equal(code, read.Code);
        if (code is not null) Assert.Equal("step `deploy` is performed, so `hooks.points` must name `work/perform`: the point Daoris asks it at.", read.Problem);
    }

    /// <summary>A manifest's step is the node declaration built by hand: one model, judged by its own checks.</summary>
    [Fact]
    public void A_manifest_s_step_is_the_node_declaration_built_by_hand()
    {
        Plugin("""
            , "hooks": { "command": ["node", "deploy.mjs"], "points": ["quest/consider", "work/perform"] },
            "steps": [{ "id": "deploy", "version": 2, "reach": "performed",
              "inputs": { "release": "acme.build/artifact@1" }, "outputs": { "preview": "acme.deploy/preview@1" },
              "evidence": { "schema": "acme.deploy/receipt@1" }, "retry": { "maxAttempts": 3, "delaysSeconds": [5, 30] },
              "timeoutSeconds": 600, "skippable": false, "rejectable": true,
              "perform": { "point": "work/perform", "idempotent": true, "reconcile": "work/reconcile", "transient": ["unavailable", "rate-limited"] } },
              { "id": "smoke", "reach": "observed" }],
            "resources": [{ "schema": "acme.deploy/preview@1", "role": "temporary", "cleanup": "work/cleanup" }]
            """);

        var entry = Assert.Single(PluginCatalog.Load(_home).Plugins);
        Assert.Null(entry.Problem);
        var byHand = new NodeDeclaration("acme.deploy/deploy", "acme.deploy/deploy", 2, NodeReach.Performed)
        {
            Inputs = new Dictionary<string, string> { ["release"] = "acme.build/artifact@1" },
            Outputs = new Dictionary<string, string> { ["preview"] = "acme.deploy/preview@1" },
            Evidence = "acme.deploy/receipt@1",
            Retry = new NodeRetry(3, [5, 30]),
            TimeoutSeconds = 600,
            Skippable = false,
            Rejectable = true,
            Perform = new NodePerform(Idempotent: true, Reconcile: true, ["unavailable", "rate-limited"]),
        };
        var observed = new NodeDeclaration("acme.deploy/smoke", "acme.deploy/smoke", 1, NodeReach.Observed);

        Assert.Equal([byHand, observed], entry.Manifest.Steps);
        Assert.Equal(byHand.GetHashCode(), entry.Manifest.Steps[0].GetHashCode());
        Assert.All(entry.Manifest.Steps, step => Assert.Null(NodeDeclarations.Problem(step)));
        Assert.Equal(new PluginResource("acme.deploy/preview@1", PluginResourceRoles.Temporary, PluginSteps.CleanupPoint), Assert.Single(entry.Manifest.Resources));

        // The authority is a plugin's own node's, from the declaration alone: what differs by kind and nothing else.
        var authority = NodeAuthorities.Of(entry.Manifest.Steps[0])!;
        Assert.Equal((NodeReach.Performed, NodeSource.Plugin, true, false, true), (authority.Reach, authority.ReachBy, authority.PersonJudges, authority.PersonSkips, authority.Dispatches));
    }

    /// <summary>A manifest written before steps reads exactly as it did: no steps, no resources, nothing else moved.</summary>
    [Fact]
    public void A_manifest_without_steps_reads_as_before()
    {
        const string before = """
            { "id": "acme.deploy", "name": "Acme deploys", "version": "1.0.0", "description": "Deploys.",
              "hooks": { "command": ["node", "deploy.mjs"], "points": ["work/land"] },
              "servers": [{ "name": "acme", "command": ["acme-mcp"] }], "skills": [{ "name": "triage" }] }
            """;
        var (manifest, problem) = PluginCatalog.Parse("acme.deploy", before);

        Assert.Null(problem);
        Assert.Empty(manifest.Steps);
        Assert.Empty(manifest.Resources);
        Assert.Equal(("Acme deploys", "1.0.0", "Deploys."), (manifest.Name, manifest.Version, manifest.Description));
        Assert.Equal(["work/land"], manifest.Hooks!.Points);
        Assert.Equal("acme", Assert.Single(manifest.Servers).Name);
        Assert.Equal("triage", Assert.Single(manifest.Skills).Name);
    }

    /// <summary>A step's problem refuses the plugin: it contributes nothing, its steps and resources included, and its sentence is said.</summary>
    [Fact]
    public void A_step_s_problem_refuses_the_plugin_and_nothing_of_it_is_taken()
    {
        Plugin("""
            , "hooks": { "command": ["node", "deploy.mjs"], "points": ["work/land"] },
            "steps": [{ "id": "deploy", "reach": "performed", "perform": { "reconcile": "yes" } }],
            "resources": [{ "schema": "acme.deploy/preview@1", "cleanup": "work/cleanup" }]
            """);

        var catalog = PluginCatalog.Load(_home);
        var entry = Assert.Single(catalog.Plugins);
        Assert.Equal("step `deploy`'s `reconcile` must be `work/reconcile`, the point that answers what became of an attempt.", entry.Problem);
        Assert.Empty(catalog.Contributing);
        Assert.Null(entry.Manifest.Hooks);
        Assert.Empty(entry.Manifest.Steps);
        Assert.Empty(entry.Manifest.Resources);
    }

    /// <summary>A sound plugin refused for another reason, a conflict, keeps none of its steps or resources either.</summary>
    [Fact]
    public void A_plugin_refused_by_the_catalogue_keeps_no_steps()
    {
        var steps = """, "steps": [{ "id": "deploy", "reach": "performed" }], "resources": [{ "schema": "acme.deploy/preview@1", "cleanup": "work/cleanup" }] """;
        var (manifest, problem) = PluginCatalog.Parse("acme.deploy", Manifest(steps));
        Assert.Null(problem);
        Assert.Single(manifest.Steps);

        var judged = PluginCatalog.Judge(_home, [("acme.deploy", manifest, null)], new HashSet<string>(), _ => "refused here");
        var entry = Assert.Single(judged.Plugins);
        Assert.Equal("refused here", entry.Problem);
        Assert.Empty(entry.Manifest.Steps);
        Assert.Empty(entry.Manifest.Resources);
    }

    /// <summary>A manifest a row declares, read as the catalogue reads one.</summary>
    private static PluginManifest Parsed(string declared)
    {
        var (manifest, problem) = PluginCatalog.Parse("acme.deploy", Manifest(declared));
        Assert.Null(problem);
        return manifest;
    }

    /// <summary>
    /// FABRIC-UNIT1e (§D9): what an update changes in a plugin's steps and resources, each said in <see cref="PluginSteps.Said(NodeDeclaration)"/>'s
    /// words: [case, what the installed manifest declares after its id, what the source's declares, then each change as
    /// <c>what: was → now</c>, <c>(none)</c> for an empty side, joined by <c> | </c>]. A step is said whole, so a declaration that
    /// changes without a new version is still a change. The twin is <c>plugin-steps.test.ts</c>'s <c>CHANGE_ROWS</c>.
    /// </summary>
    [Theory]
    [InlineData("the same steps", """, "steps": [{ "id": "deploy", "reach": "performed" }] """, """, "steps": [{ "id": "deploy", "reach": "performed" }] """, "")]
    [InlineData("a step added", "", """, "steps": [{ "id": "deploy", "reach": "performed" }] """, "steps: (none) → deploy (performed, version 1)")]
    [InlineData("a step taken out", """, "steps": [{ "id": "build", "reach": "performed" }, { "id": "deployed", "reach": "observed" }] """, """, "steps": [{ "id": "deployed", "reach": "observed" }] """, "steps: build (performed, version 1); deployed (observed, version 1) → deployed (observed, version 1)")]
    [InlineData("a new version", """, "steps": [{ "id": "deploy", "reach": "performed" }] """, """, "steps": [{ "id": "deploy", "version": 2, "reach": "performed" }] """, "steps: deploy (performed, version 1) → deploy (performed, version 2)")]
    [InlineData("a whole step", "", """, "steps": [{ "id": "deploy", "version": 2, "reach": "performed", "inputs": { "release": "acme.build/artifact@1" }, "outputs": { "preview": "acme.deploy/preview@1", "log": "acme.deploy/log@1" }, "evidence": { "schema": "acme.deploy/receipt@1" }, "retry": { "maxAttempts": 3, "delaysSeconds": [5, 30] }, "timeoutSeconds": 600, "skippable": false, "rejectable": true, "perform": { "idempotent": true, "reconcile": "work/reconcile", "transient": ["unavailable", "rate-limited"] } }] """, "steps: (none) → deploy (performed, version 2; inputs release acme.build/artifact@1; outputs log acme.deploy/log@1, preview acme.deploy/preview@1; evidence acme.deploy/receipt@1; at most 3 attempts, waiting 5, 30 seconds; timeout 600 seconds; not skippable; rejectable; idempotent; reconciled; transient unavailable, rate-limited)")]
    [InlineData("a retry widened in the same version", """, "steps": [{ "id": "deploy", "reach": "performed", "retry": { "maxAttempts": 2 }, "perform": { "reconcile": "work/reconcile", "transient": ["unavailable"] } }] """, """, "steps": [{ "id": "deploy", "reach": "performed", "retry": { "maxAttempts": 4, "delaysSeconds": [10] }, "perform": { "reconcile": "work/reconcile", "transient": ["unavailable"] } }] """, "steps: deploy (performed, version 1; at most 2 attempts; reconciled; transient unavailable) → deploy (performed, version 1; at most 4 attempts, waiting 10 seconds; reconciled; transient unavailable)")]
    [InlineData("a retry of one attempt", "", """, "steps": [{ "id": "deploy", "reach": "performed", "retry": { "maxAttempts": 1 } }] """, "steps: (none) → deploy (performed, version 1; at most 1 attempt)")]
    [InlineData("perform declared with nothing in it", """, "steps": [{ "id": "deploy", "reach": "performed" }] """, """, "steps": [{ "id": "deploy", "reach": "performed", "perform": {} }] """, "steps: deploy (performed, version 1) → deploy (performed, version 1; perform declared)")]
    [InlineData("an observed step made performed", """, "steps": [{ "id": "deploy", "reach": "observed" }] """, """, "steps": [{ "id": "deploy", "reach": "performed" }] """, "steps: deploy (observed, version 1) → deploy (performed, version 1)")]
    [InlineData("a resource kept as the result", """, "resources": [{ "schema": "acme.deploy/preview@1", "cleanup": "work/cleanup" }] """, """, "resources": [{ "schema": "acme.deploy/preview@1", "role": "result", "cleanup": "work/cleanup" }] """, "resources: acme.deploy/preview@1 (temporary) → acme.deploy/preview@1 (result)")]
    [InlineData("a step and a resource added", "", """, "steps": [{ "id": "deployed", "reach": "observed", "skippable": true, "rejectable": false }], "resources": [{ "schema": "acme.deploy/site@1", "role": "temporary", "cleanup": "work/cleanup" }] """, "steps: (none) → deployed (observed, version 1) | resources: (none) → acme.deploy/site@1 (temporary)")]
    public void An_update_says_its_step_changes_as_the_cli_says_them(string name, string was, string now, string changes)
    {
        static string Side(string said) => said.Length > 0 ? said : "(none)";
        var between = PluginChange.Between(Parsed(was), Parsed(now));
        Assert.True(
            changes == string.Join(" | ", between.Select(change => $"{change.What}: {Side(change.Was)} → {Side(change.Now)}")),
            $"{name}: {string.Join(" | ", between)}");
    }

    /// <summary>
    /// FABRIC-UNIT1e (§D9): a plugin described where its other declarations are (<c>plugins install</c>'s line; the CLI's
    /// <c>plugin list</c> and <c>plugin add</c>): [case, what the manifest declares after its id, the description]. The twin is
    /// <c>plugin-steps.test.ts</c>'s <c>DESCRIBE_ROWS</c>.
    /// </summary>
    [Theory]
    [InlineData("no steps", "", "speaks on work/perform")]
    [InlineData("one step", """, "steps": [{ "id": "deploy", "reach": "performed" }] """, "speaks on work/perform; declares step deploy (performed)")]
    [InlineData("steps and resources", """, "steps": [{ "id": "build", "reach": "performed" }, { "id": "deployed", "reach": "observed" }], "resources": [{ "schema": "acme.deploy/preview@1", "cleanup": "work/cleanup" }, { "schema": "acme.deploy/site@1", "role": "result", "cleanup": "work/cleanup" }] """, "speaks on work/perform; declares steps build (performed), deployed (observed); creates acme.deploy/preview@1 (temporary), acme.deploy/site@1 (result)")]
    [InlineData("after a skill", """, "skills": [{ "name": "triage" }], "steps": [{ "id": "deployed", "reach": "observed" }] """, "speaks on work/perform; hands sessions skill triage; declares step deployed (observed)")]
    public void A_plugin_is_described_with_its_steps_as_the_cli_describes_it(string name, string declared, string described)
    {
        Assert.True(described == PluginPackageCommand.Describe(Parsed(declared)), $"{name}: {PluginPackageCommand.Describe(Parsed(declared))}");
    }
}
