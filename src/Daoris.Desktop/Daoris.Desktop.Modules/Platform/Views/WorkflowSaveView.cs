using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// A workflow's version saved (<c>WORKFLOW_SAVE</c>): a draft applied onto a saved workflow (<c>daoris workflow apply &lt;id&gt; &lt;file&gt;
/// --base &lt;v&gt;</c>), a new one imported or made from a default, as the card shows it. The route's own plan judges the press at the ask
/// (<c>plan: true</c>, which writes nothing), so a save it would refuse (stale, a graph that does not hold, a reach that cannot start) is
/// answered to the terminal in its words and never shown; one it takes draws its graph as the editor draws it, with its changes against
/// the version it replaces.
/// </summary>
public sealed class WorkflowSaveView : IPressView
{
    public string Module => DriverModule.Name;

    public string Route => "WORKFLOW_SAVE";

    public async Task<PressViewed> ViewAsync(IPipeGroundWorld world, JsonElement? payload, CancellationToken cancellationToken)
    {
        var id = PressPayload.Text(payload, "id").Trim();
        if (id.Length == 0 || payload is not { ValueKind: JsonValueKind.Object } body) return PressViewed.Refused("a workflow is saved under an `id` — name it.");

        var asked = JsonNode.Parse(body.GetRawText())!.AsObject();
        asked["plan"] = true;
        var planned = await world.ReadAsync(Module, Route, asked, cancellationToken).ConfigureAwait(false);
        if (PressPayload.Field(planned, "refused").Length > 0)
        {
            var problems = PressPayload.Rows(planned, "problems").Select(row => PressPayload.Field(row, "words")).Where(words => words.Length > 0);
            return PressViewed.Refused(string.Join(' ', new[] { PressPayload.Field(planned, "message") }.Concat(problems).Where(words => words.Length > 0)));
        }

        var version = Whole(planned, "version");
        var @base = Whole(body, "base");
        var graph = body.TryGetProperty("graph", out var drawn) && drawn.ValueKind == JsonValueKind.Object ? drawn.Clone() : (JsonElement?)null;
        var word = ShellWord.Of(id, ShellWord.Name);
        if (PressPayload.Flag(planned, "appended") && @base is { } replaced)
        {
            var shown = await world.ReadAsync(Module, "WORKFLOW_SHOW", new { id, version = replaced }, cancellationToken).ConfigureAwait(false);
            var facts = Facts(("id", id), ("name", PressPayload.Field(shown, "name") is { Length: > 0 } kept ? kept : id), ("base", Number(replaced)),
                ("version", Number(version ?? replaced)));
            var command = $"daoris workflow apply {word} <file> --base {Number(replaced)}";
            if (version == replaced) return PressViewed.Shown(new PressView("workflow-save-same", facts, command));
            var before = shown.ValueKind == JsonValueKind.Object && shown.TryGetProperty("body", out var was) ? Read(was) : null;
            var after = graph is { } now ? Read(now) : null;
            return PressViewed.Shown(new PressView("workflow-save-edit", facts, command)
            {
                Workflow = graph is { } whole ? new PressWorkflow(whole, before is not null && after is not null ? WorkflowGraphDiff.Of(before, after) : null) : null,
            });
        }

        var from = PressPayload.Text(payload, "from");
        var made = Facts(("id", id), ("name", PressPayload.Text(payload, "name") is { Length: > 0 } name ? name : id), ("version", Number(version ?? 1)));
        return PressViewed.Shown(new PressView("workflow-save-new", made,
            from.Length > 0 ? $"daoris workflow new {word} --from {ShellWord.Of(from, ShellWord.Name)}" : "daoris workflow import <file>")
        {
            Workflow = graph is { } fresh ? new PressWorkflow(fresh, null) : null,
        });
    }

    private static NodeGraph? Read(JsonElement graph) =>
        graph.ValueKind == JsonValueKind.Object ? NodeGraphs.Read(graph.GetRawText()).Graph : null;

    private static int? Whole(JsonElement read, string name) =>
        read.ValueKind == JsonValueKind.Object && read.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out var number) ? number : null;

    private static Dictionary<string, string> Facts(params (string Name, string Value)[] facts) =>
        facts.ToDictionary(fact => fact.Name, fact => fact.Value, StringComparer.Ordinal);

    private static string Number(int number) => number.ToString(CultureInfo.InvariantCulture);
}
