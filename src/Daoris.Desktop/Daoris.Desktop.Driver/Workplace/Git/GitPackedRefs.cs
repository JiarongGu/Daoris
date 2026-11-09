using System.Text;

namespace Daoris.Driver;

/// <summary>
/// A repository's <c>packed-refs</c>, parsed once a content (<see cref="FileStamp.Parsed{T}"/>): the branches' tips, and every line that
/// names a replacement. The replacement gate (<see cref="GitReplacements"/>) and the stamp (<see cref="GitStamp"/>) read this one parse.
/// </summary>
internal sealed record GitPackedRefs(IReadOnlyDictionary<string, string> Heads, IReadOnlyList<string> Replacements)
{
    private static readonly GitPackedRefs None = new(new Dictionary<string, string>(StringComparer.Ordinal), []);

    /// <summary>The file's path in <paramref name="common"/>, the repository's common directory.</summary>
    internal static string PathIn(string common) => Path.Combine(common, "packed-refs");

    /// <summary>The parse of <paramref name="common"/>'s file with its stamp; an absent file parses as no ref.</summary>
    internal static (FileStamped Stamp, GitPackedRefs Refs) Of(string common)
    {
        var (stamp, parsed) = FileStamp.Parsed(PathIn(common), Parse);
        return (stamp, parsed ?? None);
    }

    /// <summary>Each <c>&lt;id&gt; &lt;ref&gt;</c> line; a comment and a peeled line (<c>^&lt;id&gt;</c>) name no ref.</summary>
    internal static GitPackedRefs Parse(byte[] bytes)
    {
        var heads = new Dictionary<string, string>(StringComparer.Ordinal);
        var replacements = new List<string>();
        foreach (var line in Encoding.UTF8.GetString(bytes).Split('\n'))
        {
            var text = line.TrimEnd('\r');
            if (text.Length == 0 || text[0] is '#' or '^' || text.IndexOf(' ') is not (> 0 and var space)) continue;
            var name = text[(space + 1)..];
            if (name.StartsWith("refs/heads/", StringComparison.Ordinal)) heads[name] = text[..space];
            else if (name.StartsWith("refs/replace/", StringComparison.Ordinal)) replacements.Add(text);
        }

        return new GitPackedRefs(heads, replacements);
    }
}
