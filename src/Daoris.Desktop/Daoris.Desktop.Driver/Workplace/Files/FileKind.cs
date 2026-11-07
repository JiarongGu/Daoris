namespace Daoris.Driver;

/// <summary>
/// What a rule says Daoris's trees do with a path git does not manage (the tree-pool design §29.2): a dependency a package manager
/// installs, an intermediate its build tool checks, a runnable output, a tool's own cache, a machine-local file, scratch, another
/// repository or tree, or unknown. The session's own work, untracked and not ignored, is no rule's.
/// </summary>
internal enum FileKind
{
    Dependency,
    Intermediate,
    Output,
    Cache,
    Local,
    Scratch,
    Repository,
    Unknown,
}

/// <summary>The kinds' words in the rule file, and the fields each takes beside <c>kind</c> and <c>paths</c>.</summary>
internal static class FileKinds
{
    internal static string Word(this FileKind kind) => kind.ToString().ToLowerInvariant();

    internal static FileKind? Of(string? word) =>
        Enum.GetValues<FileKind>().Cast<FileKind?>().FirstOrDefault(kind => kind!.Value.Word() == word);

    /// <summary>A kind's own fields; every kind also takes the found layer's <c>evidence</c>, <c>other</c> and <c>size</c>.</summary>
    internal static IReadOnlyList<string> Fields(FileKind kind) => kind switch
    {
        FileKind.Dependency => ["install", "key", "toolchain", "env", "secret", "caches", "bound", "share"],
        FileKind.Intermediate or FileKind.Cache => ["toolchain"],
        FileKind.Local => ["copy"],
        _ => [],
    };

    internal static string Article(FileKind kind) => kind is FileKind.Intermediate or FileKind.Output or FileKind.Unknown ? "an" : "a";
}
