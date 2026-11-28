using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// A refusal's sentence as the machine log keeps it in the <c>update.state</c> line into refused, so a refusal reads again from the log alone, its code
/// saying too little to act on. The install's and the home's paths are said as <c>&lt;install&gt;</c> and <c>&lt;home&gt;</c>, in either
/// slash and in the long form, where a separator or the path's end follows them, and what lies under them keeps its relative name; an
/// email is said as <c>&lt;email&gt;</c>; the rest is kept as the build said it, on one line of at most <see cref="Longest"/> characters.
/// </summary>
public static class UpdateRefusalWords
{
    /// <summary>How long a logged sentence is at most, its end cut.</summary>
    public const int Longest = 800;

    /// <summary><paramref name="sentence"/> on one line, the paths of <paramref name="install"/> and <paramref name="home"/> named, not written.</summary>
    public static string Logged(string sentence, string? install, string home)
    {
        var said = string.Join(' ', sentence.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        // The longer first, so a home inside the install is named as the home.
        foreach (var (path, name) in new[] { (home, "<home>"), (install, "<install>") }
                     .Select(each => (Path: Full(each.Item1), Name: each.Item2))
                     .Where(each => each.Path is { Length: > 0 })
                     .OrderByDescending(each => each.Path!.Length))
        {
            said = Pattern(path!).Replace(said, name);
        }

        said = Email.Replace(said, "<email>");
        return said.Length <= Longest ? said : said[..(Longest - 1)] + "…";
    }

    /// <summary>The path in full, with no separator at its end; null for one that is no path, so a refusal is never a crash.</summary>
    private static string? Full(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException or System.Security.SecurityException)
        {
            return null;
        }
    }

    /// <summary><paramref name="path"/> in either slash, its long form's prefix taken with it, ending where a separator, a stop or the text's end follows.</summary>
    private static Regex Pattern(string path)
    {
        var parts = path.Split(['\\', '/']).Select(Regex.Escape);
        return new Regex(@"(?:\\\\\?\\|//\?/)?" + string.Join(@"[\\/]", parts) + @"(?=[\\/]|[\s""'`,;:)\]>]|\.(?:\s|$)|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static readonly Regex Email = new(@"[\w.+-]+@[\w-]+(?:\.[\w-]+)+", RegexOptions.CultureInvariant);
}
