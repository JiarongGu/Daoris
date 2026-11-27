namespace Daoris.Driver;

/// <summary>
/// A refusal's sentence as the machine log keeps it beside <c>update.refused</c>, so a refusal reads again from the log alone, its code
/// saying too little to act on. A log line names no path: the install's and the home's own are said as <c>&lt;install&gt;</c> and
/// <c>&lt;home&gt;</c>, and what lies under them keeps its relative name.
/// </summary>
public static class UpdateRefusalWords
{
    /// <summary><paramref name="sentence"/> on one line, the paths of <paramref name="install"/> and <paramref name="home"/> named, not written.</summary>
    public static string Logged(string sentence, string? install, string home)
    {
        var said = string.Join(' ', sentence.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        // The longer first, so a home inside the install is named as the home.
        foreach (var (path, name) in new[] { (home, "<home>"), (install, "<install>") }
                     .Where(each => each.Item1 is { Length: > 0 })
                     .Select(each => (Path.GetFullPath(each.Item1!).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), each.Item2))
                     .OrderByDescending(each => each.Item1.Length))
        {
            said = said.Replace(path, name, StringComparison.OrdinalIgnoreCase);
        }

        return said;
    }
}
