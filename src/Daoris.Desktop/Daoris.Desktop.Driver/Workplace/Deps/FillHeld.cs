namespace Daoris.Driver;

/// <summary>
/// What a tree held ignored as a fill's install began, and the seal answers for: a fill seals in a tree holding nothing ignored but the
/// folders of the dependencies placed before it and the build outputs the rule file declares (an <c>output</c> or an <c>intermediate</c>),
/// which a kept slot holds after its first build. An install reads no product of a build, but it may read any other ignored file
/// (a local copy, a cache, a scratch path), whose bytes then reach the store under a key that does not name them.
/// </summary>
internal static class FillHeld
{
    /// <summary>The first ignored top the seal does not answer for, or null where the tree held only what it allows.</summary>
    internal static string? Of(IReadOnlyDictionary<string, string> before, StoreDependency dependency, TreeFilesState record, FileRules rules, FileFloorFacts floor) =>
        before.Where(each => each.Value == "!!" && each.Key != dependency.Path && record.Of(each.Key) is null && !BuildOutput(rules, floor, each.Key))
            .Select(each => each.Key).FirstOrDefault();

    private static bool BuildOutput(FileRules rules, FileFloorFacts floor, string path) =>
        rules.For(path, floor) is { Rule: not null, Floor: null, Kind: FileKind.Output or FileKind.Intermediate };
}
