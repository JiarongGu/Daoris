using System.Text;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// The files a folder's manifest passes by: <see cref="FolderExcludes"/>' defaults, and the person's own globs kept on the
/// registration. A glob with no <c>/</c> names a file wherever it stands; one with a <c>/</c> is read from the folder's root, where
/// <c>*</c> stays within a name and <c>**</c> crosses folders, so <c>build/**</c> passes the top <c>build</c> folder alone.
/// </summary>
public sealed class PlaceExcludes
{
    private readonly List<(Regex Pattern, bool Whole)> _own;

    private PlaceExcludes(IReadOnlyList<string> own)
    {
        Own = own;
        _own = [.. own.Select(glob => glob.Trim().Replace('\\', '/').TrimStart('/')).Where(glob => glob.Length > 0)
            .Select(glob => (Compile(glob), glob.Contains('/')))];
    }

    /// <summary>The defaults alone.</summary>
    public static PlaceExcludes Default { get; } = new([]);

    /// <summary>The globs the person gave, as they were given.</summary>
    public IReadOnlyList<string> Own { get; }

    /// <summary>The excludes a registration's globs name; the defaults alone where there are none.</summary>
    public static PlaceExcludes Of(IReadOnlyList<string>? own) => own is { Count: > 0 } ? new([.. own]) : Default;

    /// <summary>Whether the file at <paramref name="path"/>, relative to the root with <c>/</c>, is passed by.</summary>
    public bool Passes(string path)
    {
        var name = path[(path.LastIndexOf('/') + 1)..];
        return FolderExcludes.Passes(name) || _own.Any(each => each.Pattern.IsMatch(each.Whole ? path : name));
    }

    private static Regex Compile(string glob)
    {
        var text = new StringBuilder("^");
        for (var at = 0; at < glob.Length; at++)
        {
            switch (glob[at])
            {
                case '*' when at + 1 < glob.Length && glob[at + 1] == '*':
                    text.Append(".*");
                    at++;
                    break;
                case '*':
                    text.Append("[^/]*");
                    break;
                case '?':
                    text.Append("[^/]");
                    break;
                default:
                    text.Append(Regex.Escape(glob[at].ToString()));
                    break;
            }
        }

        return new Regex(text.Append('$').ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
