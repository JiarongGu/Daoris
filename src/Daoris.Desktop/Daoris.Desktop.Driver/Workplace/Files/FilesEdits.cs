namespace Daoris.Driver;

/// <summary>
/// The person's edits to their layer of the rule file, as the terminal's verb and the window's presses both make them: one
/// validation, one writer. A file that stands and does not read is never written over, and an allow made while it does not read
/// is refused with it. Each refusal is a <see cref="DriverException"/> whose sentence names no machine path.
/// </summary>
public static class FilesEdits
{
    /// <summary>Why the person's file stands and does not read, or null; nothing is written, and no allow made, while it is said.</summary>
    public static string? Unread(string home) => PersonFiles.Read(home).Unread;

    /// <summary>
    /// An entry of <paramref name="kind"/> over <paramref name="paths"/>, each a top of the repository written as the pattern that names
    /// it alone (<see cref="GitPattern.Anchored"/>), added at the end of the machine's rules or the repository's, so it wins there.
    /// </summary>
    /// <param name="copy">Only a <c>local</c> rule takes it: <c>reach</c> <c>copy</c>, or <c>tree</c>.</param>
    /// <returns>The kind's word.</returns>
    public static string Set(string home, string repository, bool machine, string kind, IReadOnlyList<string> paths, bool? copy)
    {
        var word = FileKinds.Of(kind) ?? throw new DriverException($"`{kind}` is no kind of file: one of {string.Join(", ", Enum.GetValues<FileKind>().Select(each => each.Word()))}.");
        if (paths.Count == 0) throw new DriverException("`set` takes a kind and at least one path.");
        if (paths.Select(RoomPaths.Unfit).FirstOrDefault(why => why is not null) is { } unfit) throw new DriverException($"a path {unfit}.");
        if (copy is not null && word != FileKind.Local) throw new DriverException("only a `local` rule takes --copy or --no-copy.");
        var rule = new FileRule(word, [.. paths.Select(GitPattern.Anchored)]) { Reach = copy is { } copied ? copied ? "copy" : "tree" : null };
        PersonFiles.Set(home, machine ? null : repository, rule);
        return word.Word();
    }

    /// <summary>Each of the scope's rules naming one of <paramref name="paths"/>, as written or as the top it names, loses it; how many rules named any.</summary>
    public static int Remove(string home, string repository, bool machine, IReadOnlyList<string> paths) =>
        paths.Sum(path => PersonFiles.Remove(home, machine ? null : repository, path) + (path.StartsWith('/') ? 0 : PersonFiles.Remove(home, machine ? null : repository, GitPattern.Anchored(path))));

    /// <summary>
    /// The person's file rewritten in git's syntax with each path it reads as written worded as the person chose, every such path
    /// in one write.
    /// </summary>
    public static void Settle(string home, IReadOnlyList<FilesWording> wordings) => PersonFilesForm.Settle(home, wordings);

    /// <summary>
    /// A permission added to or taken from a list of the machine's scope or the repository's; an allow is written with
    /// <paramref name="by"/>, who gave it. False where nothing changed.
    /// </summary>
    public static bool Permit(string home, string repository, bool machine, bool add, string list, string rule, string by)
    {
        var scope = machine ? null : repository;
        return add ? PersonPermissions.Add(home, scope, list, rule, by) : PersonPermissions.Remove(home, scope, list, rule);
    }
}
