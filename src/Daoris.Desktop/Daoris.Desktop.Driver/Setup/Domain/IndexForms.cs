namespace Daoris.Driver;

/// <summary>
/// The generator an index quest names (ORIENT2c, D151 point 5): its path, the toolchain it is written in, and its two
/// commands, the one that writes the index and the one that checks it.
/// </summary>
/// <param name="Stack">The toolchain, in the words the quest says it in: <c>Node</c>, <c>.NET</c> or <c>Python</c>.</param>
/// <param name="Generator">Its path, relative to the repository's root.</param>
/// <param name="Write">The command that writes the index's folder.</param>
/// <param name="Check">The command that builds it in memory and fails where the folder is stale.</param>
public sealed record IndexForm(string Stack, string Generator, string Write, string Check);

/// <summary>
/// Which generator the index quest names, from what a repository is built with (ORIENT2c; the orientation design §2.2):
/// a Node, .NET or Python repository each has one obvious form, and a stack the press does not know gets no quest.
/// </summary>
/// <remarks>
/// <para><b>The first stack the reading names</b> (<see cref="SelfDescription"/>'s order, the Node family first): a
/// repository built with two is indexed in the first, since one generator writes the whole folder.</para>
///
/// <para><b>Never over a file already there</b> (the design §2.3): <c>tools/index.*</c>, then
/// <c>tools/orient-index.*</c>; with both on the line the press names neither and says so.</para>
///
/// <para><b>.NET is a file-based program</b> (<c>dotnet run tools/index.cs</c>), which needs no project file, so the
/// quest changes no build file; an SDK that cannot run one is the session's to say, and it declines.</para>
/// </remarks>
public static class IndexForms
{
    /// <summary>The stack words <see cref="SelfDescription"/> reads that a <c>package.json</c> stands behind.</summary>
    private static readonly string[] NodeFamily = ["Angular", "Next.js", "React", "Vue", "Node"];

    /// <summary>The generator's names, in the order the press tries them.</summary>
    private static readonly string[] Names = ["tools/index", "tools/orient-index"];

    /// <summary>The form for <paramref name="stack"/>, or why there is none.</summary>
    /// <param name="stack">What the repository is built with, in <see cref="SelfDescription"/>'s order.</param>
    /// <param name="onLine">Whether a path is on the repository's line already.</param>
    public static (IndexForm? Form, string? Problem) For(IReadOnlyList<string> stack, Func<string, bool> onLine)
    {
        var known = stack.Select(Kind).OfType<string>().FirstOrDefault();
        if (known is null)
        {
            return (null, stack.Count == 0
                ? "nothing it is built with could be read at its top"
                : $"it is built with {string.Join(", ", stack)}, and the press knows a generator for Node, .NET and Python only");
        }

        var extension = known switch { "Node" => "mjs", ".NET" => "cs", _ => "py" };
        var path = Names.Select(name => $"{name}.{extension}").FirstOrDefault(candidate => !onLine(candidate));
        if (path is null)
        {
            return (null, $"{string.Join(" and ", Names.Select(name => $"`{name}.{extension}`"))} are already on its line, "
                + "and the press never names a generator over a file");
        }

        var (write, check) = known switch
        {
            "Node" => ($"node {path}", $"node {path} --check"),
            ".NET" => ($"dotnet run {path}", $"dotnet run {path} -- --check"),
            _ => ($"python {path}", $"python {path} --check"),
        };
        return (new IndexForm(known, path, write, check), null);
    }

    private static string? Kind(string word) =>
        NodeFamily.Contains(word, StringComparer.Ordinal) ? "Node"
        : word is ".NET" or "Python" ? word
        : null;
}
