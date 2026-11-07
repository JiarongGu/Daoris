using System.Security.Cryptography;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// The groups an install forms where it changed an earlier placement (R5.6's round-11 note): the two, with any already grouped with
/// either, are one group under one key of every member's path and key in rule order. A group stands only while every member's rule
/// stands and its whole key, each member's record and companions, and after the hook each member's other writes hold.
/// </summary>
internal static class DependencyGroups
{
    internal static string KeyOf(IEnumerable<string> members, IReadOnlyDictionary<string, string> keys)
    {
        var text = string.Join("\n", members.Select(member => member + "\t" + (keys.TryGetValue(member, out var key) ? key : "absent")));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("group\n" + text)));
    }

    /// <summary>Every earlier placement <paramref name="installed"/>'s install changed, read against its record with its rule's caches.</summary>
    internal static IReadOnlyList<string> Changed(string tree, TreeFilesState record, string installed, IReadOnlyList<StoreDependency> order) =>
        [.. record.Placements
            .Where(each => each.Path != installed && each.Manifest is not null && !each.Installing)
            .Where(each => each.Manifest!.Differs(tree, each.Path.TrimEnd('/'), CachesOf(each.Path, order)) is not null)
            .Select(each => each.Path)];

    /// <summary>The group <paramref name="installed"/> forms with what it changed and every member already grouped with either, in rule order.</summary>
    internal static IReadOnlyList<string> Members(TreeFilesState record, string installed, IReadOnlyList<string> changed, IReadOnlyList<StoreDependency> order)
    {
        var members = new HashSet<string>(StringComparer.Ordinal) { installed };
        foreach (var path in changed.Append(installed))
        {
            members.Add(path);
            if (record.GroupOf(path) is { } group) members.UnionWith(group.Members);
        }

        return [.. members.OrderBy(path => order.ToList().FindIndex(each => each.Path == path) is var at && at < 0 ? int.MaxValue : at).ThenBy(path => path, StringComparer.Ordinal)];
    }

    /// <summary>The members' placements read again as they stand now, so the group's record is of what its install left.</summary>
    internal static TreeFilesState Formed(string tree, TreeFilesState record, IReadOnlyList<string> members, IReadOnlyDictionary<string, string> keys, IReadOnlyList<StoreDependency> order)
    {
        var state = record;
        foreach (var member in members)
        {
            if (state.Of(member) is not { } placement) continue;
            var manifest = DependencyManifest.Read(tree, member.TrimEnd('/'), CachesOf(member, order), out _);
            state = state.With(placement with { How = "grouped", Manifest = manifest });
        }

        return state.With(new DependencyGroup(members, KeyOf(members, keys)));
    }

    /// <summary>
    /// Why <paramref name="group"/> no longer stands as one, or null where it does: a member's rule gone, its whole key moved, or a
    /// member's record, companions or, where <paramref name="others"/> asks, its other writes failing.
    /// </summary>
    internal static string? Fails(string tree, TreeFilesState record, DependencyGroup group, IReadOnlyList<StoreDependency> order, IReadOnlyDictionary<string, string> keys, bool others)
    {
        if (group.Members.FirstOrDefault(member => order.All(each => each.Path != member)) is { } ruleless) return $"the rule of `{ruleless}` no longer stands";
        if (KeyOf(group.Members, keys) != group.Key) return "the files that key it moved";
        foreach (var member in group.Members)
        {
            if (record.Of(member) is not { Installing: false, Manifest: { } manifest } placement) return $"`{member}` has no record";
            if (manifest.Differs(tree, member.TrimEnd('/'), CachesOf(member, order)) is { } changed) return $"`{member}`: {changed}";
            if (InstallWrites.CompanionsDiffer(tree, placement) is { } companion) return companion;
            if (others && InstallWrites.OtherGone(tree, placement) is { } gone) return $"`{gone}` is gone";
        }

        return null;
    }

    /// <summary>The other members of the group naming <paramref name="path"/>, for a line that names them.</summary>
    internal static string Others(DependencyGroup group, string path) =>
        string.Join(", ", group.Members.Where(member => member != path).Select(member => member.TrimEnd('/')));

    private static IReadOnlyList<string> CachesOf(string path, IReadOnlyList<StoreDependency> order) =>
        order.FirstOrDefault(each => each.Path == path)?.Caches ?? [];
}
