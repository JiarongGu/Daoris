using System.Text.RegularExpressions;

namespace Daoris.Knowledge;

/// <summary>
/// A session's note as it may leave this machine (D47 §4, D49 §4, D51): the sentence, without what is
/// machine-local in it.
/// </summary>
/// <remarks>
/// <para>The wire has no FIELD for a tree, a transcript or a profile — and the note is free text the
/// driver writes: "opened a session tree at &lt;path&gt;", "its provider refused the `claude-code`
/// account `&lt;profile&gt;`", an exception's own words naming a file under the home. A field that does
/// not exist cannot be filled by accident; a sentence can, so it is cleaned wherever it crosses (REV3).</para>
///
/// <para>The record's own tree, transcript and profile go by name, whatever spelling of slashes the
/// sentence used; any other absolute path goes by shape. What remains is what a teammate needs: what
/// happened, the branch, the exit, the refusal's code.</para>
/// </remarks>
public static partial class SessionNote
{
    /// <summary>What stands in for a machine-local fact that was cut.</summary>
    public const string Elided = "…";

    public static string? ForAnotherMachine(string? note, Session session)
    {
        if (string.IsNullOrEmpty(note)) return note;

        var text = note;
        foreach (var local in new[] { session.Tree, session.Transcript })
        {
            if (string.IsNullOrWhiteSpace(local)) continue;
            foreach (var spelling in Spellings(local))
            {
                text = text.Replace(spelling, Elided, StringComparison.OrdinalIgnoreCase);
            }
        }

        if (!string.IsNullOrWhiteSpace(session.Profile))
        {
            text = Regex.Replace(
                text, $@"(?<![\w.-]){Regex.Escape(session.Profile)}(?![\w.-])", "a named account",
                RegexOptions.IgnoreCase);
        }

        text = WindowsPath().Replace(text, Elided);
        return UnixPath().Replace(text, Elided);
    }

    private static IEnumerable<string> Spellings(string path)
    {
        var trimmed = path.TrimEnd('/', '\\');
        yield return trimmed;
        yield return trimmed.Replace('\\', '/');
        yield return trimmed.Replace('/', '\\');
    }

    /// <summary>A drive-rooted or UNC path, to the end of the token it sits in.</summary>
    [GeneratedRegex(@"(?:\b[A-Za-z]:[\\/]|\\\\[\w.$-]+\\)[^\s'""`<>|]*")]
    private static partial Regex WindowsPath();

    /// <summary>A path under a home or a machine's own trees — never a branch like `daoris/s1`.</summary>
    [GeneratedRegex(@"(?<![\w.~-])/(?:home|Users|root|tmp|var|opt|mnt|srv|private)/[^\s'""`<>|]*")]
    private static partial Regex UnixPath();
}
