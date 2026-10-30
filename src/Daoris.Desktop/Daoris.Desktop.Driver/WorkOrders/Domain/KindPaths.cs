using System.Text;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// A kind's paths (WORKFLOW1f, the workflow design §4.2, §4.4): the globs work of that kind keeps to, matched against the paths a
/// chain changed. <c>*</c> stays inside one folder, <c>**</c> crosses folders (<c>**/</c> matching none too), <c>?</c> is one
/// character of a name, <c>{a,b}</c> is either; the rest is itself, compared exactly, as git names a path.
/// </summary>
public static class KindPaths
{
    /// <summary>Whether <paramref name="path"/>, as git names it with <c>/</c> between its parts, keeps to <paramref name="glob"/>.</summary>
    public static bool Matches(string glob, string path) =>
        Regex.IsMatch(path, Pattern(glob), RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <summary>The paths that keep to none of <paramref name="globs"/>, in the order given.</summary>
    public static IReadOnlyList<string> Outside(IReadOnlyList<string> globs, IReadOnlyList<string> paths) =>
        [.. paths.Where(path => !globs.Any(glob => Matches(glob, path)))];

    /// <summary>A glob as a pattern a whole path is held to.</summary>
    internal static string Pattern(string glob)
    {
        var pattern = new StringBuilder("^");
        var braces = 0;
        for (var at = 0; at < glob.Length; at++)
        {
            var c = glob[at];
            switch (c)
            {
                case '*' when at + 1 < glob.Length && glob[at + 1] == '*':
                    // `**/` is any folders, none among them; `**` elsewhere is anything at all.
                    if (at + 2 < glob.Length && glob[at + 2] == '/')
                    {
                        pattern.Append("(?:.*/)?");
                        at += 2;
                    }
                    else
                    {
                        pattern.Append(".*");
                        at += 1;
                    }

                    break;
                case '*':
                    pattern.Append("[^/]*");
                    break;
                case '?':
                    pattern.Append("[^/]");
                    break;
                case '{':
                    braces++;
                    pattern.Append("(?:");
                    break;
                case '}' when braces > 0:
                    braces--;
                    pattern.Append(')');
                    break;
                case ',' when braces > 0:
                    pattern.Append('|');
                    break;
                default:
                    pattern.Append(Regex.Escape(c.ToString()));
                    break;
            }
        }

        // An unclosed brace is the glob's own words, not a group: its text is taken as written.
        return braces > 0 ? "^" + Regex.Escape(glob) + "$" : pattern.Append('$').ToString();
    }
}
