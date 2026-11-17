using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// A saved version's <c>human</c> step, a kind that told a person from an agent though a step's kind says only what settles it, is
/// read as a <c>task</c> under its role: the role it names, else the kind's default <c>owner</c>, which the person
/// holds by the owner's cast. The file stays as written, since a version is never edited, and the version keeps the digest of its saved
/// graph (<see cref="NodeGraphs.ReadSaved"/>), so a run pinned to it reads it at the identity its binding holds, as a renamed plugin's step is
/// read (<see cref="PluginFormerKinds"/>). A version the person saves next names the step as the editor shows it, a <c>task</c>.
/// </summary>
/// <remarks>The condition that ends it: no saved version under a home on a supported update path spells <c>human</c>, which
/// <see cref="Census"/> counts; a save never writes it, since the kind is no kit's.</remarks>
public static class HumanFormerKind
{
    /// <summary>The former kind's id as saved versions spell it.</summary>
    public const string Former = "human";

    /// <summary>A step's kind as this build reads it: the former kind a <c>task</c>, any other as it is.</summary>
    public static string KindOf(string kind) => kind == Former ? NodeKinds.Task : kind;

    /// <summary>A saved graph read with its former kind's steps as tasks; the graph itself where none is.</summary>
    public static NodeGraph Read(NodeGraph saved) =>
        saved.Nodes.Any(node => node.Kind == Former) ? saved with { Nodes = [.. saved.Nodes.Select(node => node with { Kind = KindOf(node.Kind) })] } : saved;

    /// <summary>
    /// The census that ends the reader: each saved workflow under <paramref name="home"/> and each of its versions whose graph's file spells the
    /// former kind, as <c>&lt;id&gt; v&lt;n&gt;</c>.
    /// </summary>
    public static IReadOnlyList<string> Census(string home) =>
    [
        .. new WorkflowFiles(home).List()
            .Where(found => found.File?["versions"] is JsonArray)
            .SelectMany(found => found.File!["versions"]!.AsArray().OfType<JsonObject>()
                .Where(version => version["graph"]?["nodes"] is JsonArray nodes
                    && nodes.OfType<JsonObject>().Any(node => node["kind"] is JsonValue kind && kind.TryGetValue<string>(out var named) && named == Former))
                .Select(version => $"{found.Id} v{version["version"]}")),
    ];
}
