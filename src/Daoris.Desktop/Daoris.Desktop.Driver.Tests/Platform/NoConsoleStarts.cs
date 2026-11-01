using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The scan behind both test projects' <c>ProcessStartsCensusTests</c> (CONSOLEWINDOWS1): every <c>new ProcessStartInfo</c> that says
/// <c>UseShellExecute = false</c> runs a console program, and a console program started from a parent with no console of its own
/// (the merge queue's detached runner, a gate under it) is given a visible window unless the start says <c>CreateNoWindow = true</c>.
/// A start with <c>UseShellExecute = true</c> opens a window on purpose and is named in <see cref="Shell"/>; one that keeps a
/// console or a window is named in <see cref="Windows"/>. A start that says neither is a bag of properties (a test of a wiring that
/// is handed one) and is no start. Compiled into the modules' tests as a link, as <c>TestGit.cs</c> is.
/// </summary>
internal static class NoConsoleStarts
{
    /// <summary>The starts through the shell, each file with its reason: the window it opens is the point.</summary>
    public static readonly IReadOnlyDictionary<string, string> Shell = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Daoris.Desktop/Daoris.Desktop.Launcher/Program.cs"] = "the launcher starts the application, a windowed program, and exits",
        ["Daoris.Desktop/Daoris.Desktop.Modules/Update/UpdateLauncher.cs"] =
            "the update launcher hands over to the launcher, a windowed program with no console to show",
    };

    /// <summary>The console-program starts that keep a console or a window, each file with its reason; each marks its start.</summary>
    public static readonly IReadOnlyDictionary<string, string> Windows = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Daoris.Desktop/Daoris.Desktop.Driver/Workplace/GitDoors.cs"] =
            "the door is git's console child, and its ssh speaks git's protocol over the stdio git handed it",
        ["Daoris.Desktop/Daoris.Desktop.Driver/Platform/PieceLauncher.cs"] =
            "the harness speaks its protocol over the stdio the launcher was handed, on the console the launcher made with no window",
        ["Daoris.Desktop/Daoris.Desktop.Driver/Agents/HarnessActions.cs"] =
            "a terminal action: the person signs in at the harness's own console, which is the window",
        ["Daoris.Devkit/Daoris.Devkit.Core/Process.cs"] =
            "a gate's output is the person's, shown where the verb runs: the child takes the verb's console",
    };

    /// <summary>The marks a start in <see cref="Windows"/> carries on its line or the line above.</summary>
    private static readonly string[] Marks = ["// inherits its console:", "// console-window:"];

    /// <summary>The problems of the starts in <paramref name="sources"/> (file under <c>src</c>, text), read with their comments.</summary>
    public static (List<string> Problems, int Starts) Scan(IEnumerable<(string File, string Source)> sources)
    {
        var problems = new List<string>();
        var starts = 0;
        var shell = new HashSet<string>();
        var windows = new HashSet<string>();
        foreach (var (file, source) in sources)
        {
            foreach (Match spawn in Regex.Matches(source, @"new ProcessStartInfo\b"))
            {
                var (initializer, after) = Parts(source, spawn.Index + spawn.Length);
                var line = source[..spawn.Index].Count(c => c == '\n') + 1;
                if (Regex.IsMatch(initializer, @"UseShellExecute\s*=\s*true"))
                {
                    shell.Add(file);
                    if (!Shell.ContainsKey(file)) problems.Add($"{file}:{line}: UseShellExecute = true and the file is not named as opening a window on purpose");
                    continue;
                }

                if (!Regex.IsMatch(initializer, @"UseShellExecute\s*=\s*false")) continue;
                starts++;
                if (initializer.Contains("CreateNoWindow = true", StringComparison.Ordinal)
                    || after.Contains(".CreateNoWindow = true", StringComparison.Ordinal)
                    || Regex.IsMatch(after, @"TestGit\.(?:Past|Ceiling)\("))
                {
                    continue;
                }

                var lines = source.Split('\n');
                var near = string.Join('\n', lines.Skip(Math.Max(0, line - 3)).Take(3)) + initializer;
                if (Marks.Any(near.Contains) && near.Contains("CreateNoWindow = false", StringComparison.Ordinal))
                {
                    windows.Add(file);
                    if (!Windows.ContainsKey(file)) problems.Add($"{file}:{line}: keeps a console or a window and the file is not named");
                    continue;
                }

                problems.Add($"{file}:{line}: UseShellExecute = false and no CreateNoWindow = true, so a console program opens a window from a parent with no console");
            }
        }

        return (problems, starts);
    }

    /// <summary>The named files among <paramref name="sources"/> that no longer carry what they are named for.</summary>
    public static IEnumerable<string> Stale(IEnumerable<(string File, string Source)> sources)
    {
        var all = sources.ToList();
        foreach (var (file, source) in all)
        {
            if (Shell.ContainsKey(file) && !Regex.IsMatch(source, @"UseShellExecute\s*=\s*true")) yield return $"{file} opens no window by the shell now: take it off the list";
            if (Windows.ContainsKey(file) && !Marks.Any(source.Contains)) yield return $"{file} marks no kept console now: take it off the list";
        }
    }

    /// <summary>The initializer's braces, and the text after the start's statement begins, of the start whose type name ends at <paramref name="at"/>.</summary>
    private static (string Initializer, string After) Parts(string source, int at)
    {
        var i = at;
        while (i < source.Length && char.IsWhiteSpace(source[i])) i++;
        if (i < source.Length && source[i] == '(') i = Close(source, i, '(', ')') + 1;
        while (i < source.Length && char.IsWhiteSpace(source[i])) i++;
        var initializer = "";
        if (i < source.Length && source[i] == '{')
        {
            var end = Close(source, i, '{', '}');
            initializer = source[i..end];
            i = end;
        }

        return (initializer, source.Substring(i, Math.Min(600, source.Length - i)));
    }

    private static int Close(string source, int open, char up, char down)
    {
        var depth = 0;
        for (var i = open; i < source.Length; i++)
        {
            if (source[i] == up) depth++;
            else if (source[i] == down && --depth == 0) return i;
        }

        return source.Length;
    }

    /// <summary>Each source of the named top folders of <c>src</c>, by its path under it; the build's output is no source.</summary>
    public static IEnumerable<(string File, string Source)> Read(string src, params string[] folders)
    {
        foreach (var folder in folders)
        {
            var root = Path.Combine(src, folder);
            if (!Directory.Exists(root)) continue;
            foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                var sep = Path.DirectorySeparatorChar;
                if (file.Contains($"{sep}obj{sep}") || file.Contains($"{sep}bin{sep}")) continue;
                // The census's own cases hold the starts it refuses, as text.
                if (Path.GetFileName(file) is "NoConsoleStarts.cs" or "ProcessStartsCensusTests.cs") continue;
                yield return (Path.GetRelativePath(src, file).Replace('\\', '/'), File.ReadAllText(file).Replace("\r\n", "\n"));
            }
        }
    }

    /// <summary>The repository's <c>src</c> folder, found above the test binary.</summary>
    public static string Src()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !Directory.Exists(Path.Combine(folder.FullName, "Daoris.Desktop", "Daoris.Desktop.Driver")))
        {
            folder = folder.Parent;
        }

        return folder?.FullName ?? throw new InvalidOperationException("the source tree was not found above the test binary");
    }
}
