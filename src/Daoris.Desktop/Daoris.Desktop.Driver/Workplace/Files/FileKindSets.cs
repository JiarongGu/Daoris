namespace Daoris.Driver;

/// <summary>
/// The rule set each kind names (the tree-pool design §35.1): one rule per act Daoris makes with an entry's paths, a field an entry
/// leaves out taken from its toolchain signature and then from its kind. <c>reach</c> is how a path reaches a tree; <c>take</c> what
/// a take does with it; <c>finish</c> what a finish does with it; <c>across</c> whether a dependency's store entry is shared across
/// repositories.
/// </summary>
internal static class FileKindSets
{
    internal static readonly string[] Reaches = ["tree", "copy", "read", "write"];

    internal static readonly string[] Takes = ["keep", "base", "renew", "aside", "own"];

    internal static readonly string[] Finishes = ["keep", "aside"];

    private const string SkipsBuild = "--no-build";

    /// <summary>The fields every kind takes in git's form, beside <c>kind</c> and <c>paths</c>.</summary>
    private static readonly string[] Acts = ["reach", "take", "finish"];

    /// <summary>A kind's fields in git's form; every kind also takes the found layer's <c>evidence</c>, <c>other</c> and <c>size</c>.</summary>
    internal static IReadOnlyList<string> Fields(FileKind kind) => kind switch
    {
        FileKind.Dependency => [.. Acts, "install", "setup", "per", "key", "toolchain", "env", "secret", "caches", "bound", "across"],
        FileKind.Intermediate or FileKind.Cache => [.. Acts, "toolchain"],
        FileKind.Output => [.. Acts, "builds", "refuses"],
        _ => Acts,
    };

    /// <summary>Why an output's <c>builds</c> cannot be the door's one command, a build of each configuration it names; null where it can.</summary>
    internal static string? Unbuildable(string builds) =>
        builds.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(SkipsBuild, StringComparer.Ordinal)
            ? $"`builds` holds `{SkipsBuild}`, which skips the build the door's release stands on"
            : !builds.Contains("{configuration}", StringComparison.Ordinal)
                ? "`builds` names no `{configuration}`, which the door fills with each configuration it builds"
                : null;

    /// <summary>
    /// What a take does with a kind's paths where no rule says: a dependency kept by its key and record, an intermediate or a cache
    /// renewed when its toolchain moves, an output set aside, and every other kind the line's own.
    /// </summary>
    internal static string Take(FileKind kind) => kind switch
    {
        FileKind.Dependency => "keep",
        FileKind.Intermediate or FileKind.Cache => "renew",
        FileKind.Output => "aside",
        _ => "own",
    };

    /// <summary>A rule's take: its own, else its kind's.</summary>
    internal static string TakeOf(FileRule rule) => rule.Take ?? Take(rule.Kind);

    /// <summary>A rule's finish: its own, else kept where its take keeps it, else set aside.</summary>
    internal static string FinishOf(FileRule rule) => rule.Finish ?? (TakeOf(rule) is "keep" or "base" or "renew" ? "keep" : "aside");

    /// <summary>Whether a rule copies its paths into each tree from the checkout.</summary>
    internal static bool Copies(FileRule rule) => rule.Reach == "copy";

    internal static bool Shares(FileRule rule) => rule.Reach is "read" or "write";

    /// <summary>Why a kind may not share its paths, or null: what its producer rewrites would write the checkout's one place.</summary>
    internal static string? Unshared(FileKind kind, string? reach) =>
        reach is "read" or "write" && kind is FileKind.Dependency or FileKind.Intermediate or FileKind.Cache or FileKind.Output
            ? $"`reach: {reach}` shares one place every tree uses, and {FileKinds.Article(kind)} `{kind.Word()}` is rewritten by what makes it"
            : null;
}
