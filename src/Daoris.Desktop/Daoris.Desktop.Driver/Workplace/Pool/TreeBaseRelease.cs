using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// What the door did with one base, kept in the tree's files record beside the bases (the tree-pool design §36.3, 2, §38.2, 1): the
/// base's folder, its producer's project file, configuration and the frameworks the SDK's evaluation read, each command the door ran,
/// the commit the tree stood at, the build's exit, the moment, and how many entries its build did not own were set aside. A later
/// release of the same folder replaces it. <see cref="Said"/> is whether a files stage has said it to the line.
/// </summary>
internal sealed record TreeBaseRelease(
    string Path, string Project, string Configuration, IReadOnlyList<string> Frameworks, IReadOnlyList<string> Commands, string Commit,
    int Exit, DateTimeOffset At, int Trimmed)
{
    internal bool Said { get; init; }

    internal static IReadOnlyList<TreeBaseRelease> FromJson(JsonElement root) =>
        root.TryGetProperty("released", out var released) ? [.. released.EnumerateArray().Select(One)] : [];

    internal static JsonArray ToJson(IReadOnlyList<TreeBaseRelease> released) => new([.. released.Select(each => (JsonNode)Written(each))]);

    private static TreeBaseRelease One(JsonElement each) => new(
        each.GetProperty("path").GetString()!, each.GetProperty("project").GetString()!, each.GetProperty("configuration").GetString()!,
        Strings(each, "frameworks"), Strings(each, "commands"), each.GetProperty("commit").GetString()!, each.GetProperty("exit").GetInt32(),
        each.GetProperty("at").GetDateTimeOffset(), each.GetProperty("trimmed").GetInt32())
    {
        Said = each.TryGetProperty("said", out var said) && said.GetBoolean(),
    };

    private static IReadOnlyList<string> Strings(JsonElement each, string name) => [.. each.GetProperty(name).EnumerateArray().Select(item => item.GetString()!)];

    private static JsonObject Written(TreeBaseRelease each) => new()
    {
        ["path"] = each.Path, ["project"] = each.Project, ["configuration"] = each.Configuration,
        ["frameworks"] = FileRuleFile.List(each.Frameworks), ["commands"] = FileRuleFile.List(each.Commands), ["commit"] = each.Commit,
        ["exit"] = each.Exit, ["at"] = each.At.ToString("O"), ["trimmed"] = each.Trimmed, ["said"] = each.Said,
    };
}
