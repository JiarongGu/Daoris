using System.Diagnostics;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// What Daoris's git carries: <c>profiles/git/global.gitconfig</c>,
/// handed as <c>GIT_CONFIG_GLOBAL</c> to every start that takes the tools' environment (<see cref="Tools.Hand(ProcessStartInfo, ToolsRead, string)"/>),
/// never to the terminal's shells, which are the person's own. The person's own global files are included first by their paths, read
/// and never written; the allow-listed keys of <c>tools.json</c>'s <c>git</c> follow between Daoris's markers, so the person's choice
/// in Settings wins over their own file and a repository's own configuration still wins over both. It holds no identity and no address.
/// </summary>
/// <remarks>
/// <para><b>Handed only where git carries a key</b>, so with none an environment is the inherited one; and not to a git
/// known older than <see cref="GitVersions.Setting"/>, which would read none of it.</para>
/// <para><b>Where Daoris's ssh door is laid out it carries no <c>core.sshCommand</c></b>: the door hands the choice to an identity's ssh
/// itself, and where the door is named by <c>GIT_SSH</c> a global <c>core.sshCommand</c> would outrank it and pass it by.</para>
/// <para><b>A child's <c>git config --global</c> writes here</b>, not into the person's file. Everything after the end marker is kept;
/// a key git adds inside Daoris's part (its <c>[core]</c>, the section git finds first) is moved after the marker at the next rewrite.
/// Written by git's own lock (<c>global.gitconfig.lock</c>), so a rewrite and a child's write never cross.</para>
/// </remarks>
public static class GitGlobalFile
{
    /// <summary>The variable git reads the global file's path from.</summary>
    public const string Variable = "GIT_CONFIG_GLOBAL";

    /// <summary>The line before Daoris's keys.</summary>
    public const string Begin = "# daoris: begin";

    /// <summary>The line after them; what follows it is kept.</summary>
    public const string End = "# daoris: end";

    /// <summary>The ssh door's file in an install's <c>app/bin/</c>.</summary>
    public const string SshDoor = "daoris-git-ssh.exe";

    private const string Header =
        "# Written by Daoris (D121, D198): your own global git configuration is included first, unchanged, and the lines\n"
        + "# between the two markers are what Daoris's git carries, rewritten from tools.json. What follows them is kept.\n";

    /// <summary>The file's path under <paramref name="home"/>.</summary>
    public static string PathOf(string home) => DaorisHome.Entry(home, HomeEntry.GitConfig);

    /// <summary>
    /// The person's own global files, in the order git reads them, each only where it exists: the file their own
    /// <c>GIT_CONFIG_GLOBAL</c> names, where it names one other than Daoris's; else the XDG file, then <c>.gitconfig</c> in git's home.
    /// </summary>
    /// <param name="variable">A variable's value in the environment the start inherited; null where unset.</param>
    public static IReadOnlyList<string> Includes(string home, Func<string, string?> variable)
    {
        if (variable(Variable) is { Length: > 0 } named && !Same(named, PathOf(home))) return File.Exists(named) ? [Path.GetFullPath(named)] : [];
        var user = UserHome(variable);
        var xdg = variable("XDG_CONFIG_HOME") is { Length: > 0 } config
            ? Path.Combine(config, "git", "config")
            : user is null ? null : Path.Combine(user, ".config", "git", "config");
        var dot = user is null ? null : Path.Combine(user, ".gitconfig");
        return [.. new[] { xdg, dot }.OfType<string>().Where(File.Exists).Select(Path.GetFullPath)];
    }

    /// <summary>
    /// The keys the file carries: <c>tools.json</c>'s allow-listed git settings, without <c>core.sshCommand</c> where Daoris's ssh door
    /// is laid out beside <paramref name="home"/>.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Carried(ToolsRead read, string home)
    {
        var door = Tools.InstallBin(home) is { } bin && File.Exists(Path.Combine(bin, SshDoor));
        return read.Git
            .Where(each => !(door && string.Equals(each.Key, "core.sshCommand", StringComparison.OrdinalIgnoreCase)))
            .ToDictionary(each => each.Key, each => each.Value, StringComparer.Ordinal);
    }

    /// <summary>
    /// The file's text: the header, the includes, Daoris's keys between the markers, then what <paramref name="existing"/> held after
    /// its end marker, any key a child wrote inside Daoris's part moved there under its section first.
    /// </summary>
    public static string Text(IReadOnlyList<string> includes, IReadOnlyDictionary<string, string> keys, string? existing)
    {
        var text = new StringBuilder(Header);
        if (includes.Count > 0)
        {
            text.Append("[include]\n");
            foreach (var include in includes) text.Append("\tpath = ").Append(Quoted(include.Replace('\\', '/'))).Append('\n');
        }

        text.Append(Begin).Append('\n');
        foreach (var section in keys.GroupBy(each => Section(each.Key), StringComparer.OrdinalIgnoreCase))
        {
            text.Append(section.Key).Append('\n');
            foreach (var (key, value) in section) text.Append('\t').Append(key[(key.LastIndexOf('.') + 1)..]).Append(" = ").Append(Quoted(value)).Append('\n');
        }

        text.Append(End).Append('\n');
        return text.Append(Kept(existing)).ToString();
    }

    /// <summary>
    /// Hand <paramref name="info"/> the file where git carries a key and its git is not known too old for it, written first where it is
    /// not as Daoris writes it; the path handed, or null where none was. Never a reason a start fails: a file that cannot be written is
    /// not handed.
    /// </summary>
    public static string? Hand(ProcessStartInfo info, ToolsRead read, string home)
    {
        var keys = Carried(read, home);
        if (keys.Count == 0) return null;
        var path = info.Environment.TryGetValue(Tools.PathVariable, out var held) ? held : null;
        if (GitVersions.Known(Tools.Resolve(read, home, "git", path).File) is { } version && version < GitVersions.Setting) return null;
        if (!Ensure(home, Includes(home, name => info.Environment.TryGetValue(name, out var value) ? value : null), keys)) return null;
        info.Environment[Variable] = PathOf(home);
        return PathOf(home);
    }

    /// <summary>Write the file where its text is not <see cref="Text"/>'s; whether it now is.</summary>
    public static bool Ensure(string home, IReadOnlyList<string> includes, IReadOnlyDictionary<string, string> keys)
    {
        var path = PathOf(home);
        try
        {
            var existing = File.Exists(path) ? AtomicFile.ReadText(path) : null;
            var text = Text(includes, keys, existing);
            if (existing == text) return true;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            // git's own lock: while a child's `git config --global` holds it, the file stands as it is and is rewritten at a later start.
            var locked = path + ".lock";
            FileStream stream;
            try
            {
                stream = new FileStream(locked, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            }
            catch (IOException)
            {
                return existing is not null;
            }

            try
            {
                using (stream) stream.Write(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(text));
                File.Move(locked, path, overwrite: true);
                return true;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                TryDelete(locked);
                return existing is not null;
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// What a rewrite keeps: the lines after the end marker, and a key inside Daoris's part that Daoris does not write (an include and
    /// an allow-listed key are Daoris's); a file with no end marker is kept whole after Daoris's part.
    /// </summary>
    private static string Kept(string? existing)
    {
        if (existing is null) return "";
        var lines = existing.Replace("\r\n", "\n").Split('\n');
        var end = Array.IndexOf(lines, End);
        if (end < 0) return existing.EndsWith('\n') || existing.Length == 0 ? existing : existing + "\n";

        var moved = new StringBuilder();
        string? header = null;
        var headerSaid = false;
        foreach (var line in lines[..end])
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith('['))
            {
                header = trimmed;
                headerSaid = false;
                continue;
            }

            if (trimmed.Length == 0 || trimmed[0] is '#' or ';' || header is null) continue;
            var name = trimmed.Split('=', 2)[0].Trim();
            var key = Key(header, name);
            if (key is null || key.Equals("include.path", StringComparison.OrdinalIgnoreCase)) continue;
            if (Tools.GitSettings.Any(each => each.Equals(key, StringComparison.OrdinalIgnoreCase))) continue;
            if (!headerSaid) moved.Append(header).Append('\n');
            headerSaid = true;
            moved.Append('\t').Append(trimmed).Append('\n');
        }

        var after = string.Join('\n', lines[(end + 1)..]);
        return moved.Append(after).ToString();
    }

    /// <summary>A section header as git writes it: <c>[core]</c>, or <c>[section "sub"]</c> for a key with a subsection.</summary>
    private static string Section(string key)
    {
        var first = key.IndexOf('.');
        var last = key.LastIndexOf('.');
        return first == last ? $"[{key[..first]}]" : $"[{key[..first]} {Quoted(key[(first + 1)..last])}]";
    }

    /// <summary>A key's whole name from its section header and its name; null for a header this reads no section from.</summary>
    private static string? Key(string header, string name)
    {
        var inside = header.TrimStart('[').Split(']', 2)[0].Trim();
        if (inside.Length == 0) return null;
        var space = inside.IndexOf(' ');
        if (space < 0) return $"{inside}.{name}";
        return $"{inside[..space]}.{inside[(space + 1)..].Trim().Trim('"')}.{name}";
    }

    /// <summary>A value in git's quoted form: a backslash and a quote escaped.</summary>
    private static string Quoted(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    /// <summary>Git's home on this machine, as Git for Windows finds it: <c>HOME</c>, else <c>HOMEDRIVE</c> and <c>HOMEPATH</c> where a folder, else <c>USERPROFILE</c>.</summary>
    private static string? UserHome(Func<string, string?> variable)
    {
        if (variable("HOME") is { Length: > 0 } home) return home;
        if (variable("HOMEPATH") is { Length: > 0 } homePath && (variable("HOMEDRIVE") ?? "") + homePath is var candidate && Directory.Exists(candidate))
            return candidate;
        return variable("USERPROFILE") is { Length: > 0 } profile ? profile : null;
    }

    private static bool Same(string one, string other)
    {
        try
        {
            return string.Equals(Path.GetFullPath(one), Path.GetFullPath(other), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static void TryDelete(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
        }
    }
}
