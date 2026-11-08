using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// A kept output standing in a taken slot as the previous line's build base (the tree-pool design §35.3, §38.2): one declared
/// configuration's folder under a <c>take: base</c> top, repository-relative and ending <c>/</c>; the previous line whose build it is;
/// the commit that line stood at when taken; the moment of the take; and its producer, the one project file beside the top and the
/// configuration the door builds. A release removes the entry.
/// </summary>
internal sealed record TreeBase(string Path, string From, string Commit, DateTimeOffset At, string Project, string Configuration)
{
    /// <summary>The claim's job that wrote the entry, so that claim's undo takes back exactly its own.</summary>
    internal string Job { get; init; } = "";

    /// <summary>The entry as it stood before that claim stamped it again; null where the claim added it.</summary>
    internal TreeBase? Was { get; init; }

    /// <summary>The set-aside folder of the take that kept it, which the entries its release trims join.</summary>
    internal string Aside { get; init; } = "";

    internal string Builds { get; init; } = "";

    internal static IReadOnlyList<TreeBase> FromJson(JsonElement root) =>
        root.TryGetProperty("bases", out var bases) ? [.. bases.EnumerateArray().Select(One)] : [];

    internal static JsonArray ToJson(IReadOnlyList<TreeBase> bases) => new([.. bases.Select(each => (JsonNode)Written(each))]);

    private static TreeBase One(JsonElement each) => new(
        each.GetProperty("path").GetString()!, each.GetProperty("from").GetString()!, each.GetProperty("commit").GetString()!,
        each.GetProperty("at").GetDateTimeOffset(), each.GetProperty("project").GetString()!, each.GetProperty("configuration").GetString()!)
    {
        Job = each.TryGetProperty("job", out var job) ? job.GetString()! : "",
        Was = each.TryGetProperty("was", out var was) ? One(was) : null,
        Aside = each.TryGetProperty("aside", out var aside) ? aside.GetString()! : "",
        Builds = each.TryGetProperty("builds", out var builds) ? builds.GetString()! : "",
    };

    private static JsonObject Written(TreeBase each)
    {
        var written = new JsonObject
        {
            ["path"] = each.Path, ["from"] = each.From, ["commit"] = each.Commit, ["at"] = each.At.ToString("O"),
            ["project"] = each.Project, ["configuration"] = each.Configuration,
        };
        if (each.Job.Length > 0) written["job"] = each.Job;
        if (each.Aside.Length > 0) written["aside"] = each.Aside;
        if (each.Builds.Length > 0) written["builds"] = each.Builds;
        if (each.Was is { } was) written["was"] = Written(was with { Was = null, Job = "" });
        return written;
    }
}
