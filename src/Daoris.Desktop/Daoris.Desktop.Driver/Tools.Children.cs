using System.Diagnostics;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// One answer for Daoris and every child (TOOLS5, D121; <c>docs/2026-10-01-tools-design.md</c> §2.4): the file a
/// command's first word starts, and the environment every process the driver and the modules start is handed.
/// </summary>
/// <remarks>
/// <para>🔴 <b>A TWIN of the CLI's <c>tools.ts</c></b> (<c>toolFolders</c>, <c>childPath</c>,
/// <c>childEnvironment</c>, <c>resolveCommand</c>): <c>ToolsChildrenTests</c> here and
/// <c>tools-children.test.ts</c> there hold the same tables, row for row.</para>
/// <list type="bullet">
/// <item><b>A child's <c>PATH</c></b> is the folders of each tool that is managed or a named file, in the declared
/// order, then the <c>PATH</c> it inherited. A managed version's folders are its record's <c>paths</c> inside the
/// package (<c>.</c> is the package itself), or the folder its executable is in; a named file's is its folder. A way
/// that refuses puts nothing first.</item>
/// <item><b>With every tool the system's the environment is the inherited one exactly</b> (§2.3's absence): no
/// variable is set at all.</item>
/// <item><b>A command's first word</b> that a tool answers for is that tool's: its own name is the resolved file;
/// another it answers for (<c>npm</c>, <c>npx</c>) is found beside it, by PATHEXT, and never on <c>PATH</c> when the
/// tool is managed or a file. Any other name is the caller's own resolver's.</item>
/// <item><b>Read from the file at each start</b>, and never written onto this process's own environment: a variable
/// rewritten while sessions start is the trap the modules' serialized tests already know (§2.4).</item>
/// </list>
/// <para><c>GIT_CONFIG_GLOBAL</c>, the other variable §2.4 names, is TOOLS6's: it joins
/// <see cref="ChildEnvironment"/> with the git file it names.</para>
/// </remarks>
public static partial class Tools
{
    /// <summary>The variable a child's <c>PATH</c> is. Windows reads it in any case, and a start's environment there is case-blind.</summary>
    public const string PathVariable = "PATH";

    /// <summary>
    /// The folders one tool puts first on a child's <c>PATH</c> (§2.4): a managed version's, or a named file's; none
    /// for the system's, and none for a way that refuses.
    /// </summary>
    /// <exception cref="DriverException">A tool this build does not declare.</exception>
    public static IReadOnlyList<string> Folders(ToolsRead read, string home, string tool)
    {
        var declared = Find(tool) ?? throw new DriverException(Undeclared(tool));
        var entry = read.Entries[tool];
        if (entry.Problem is not null) return [];

        if (entry.Way == ToolWay.File) return File.Exists(entry.File) ? [Path.GetDirectoryName(entry.File)!] : [];
        if (entry.Way != ToolWay.Managed) return [];

        var (file, _) = ManagedFile(home, declared, entry.Version!);
        if (file is null) return [];

        var version = Path.Combine(home, Folder, tool, entry.Version!);
        var package = Path.Combine(version, Package);
        return [.. RecordPaths(Path.Combine(version, Record)).Select(path => path == "." ? package : Path.Combine([package, .. path.Split('/')]))];
    }

    /// <summary>
    /// The <c>PATH</c> a child of Daoris starts with (§2.4): every managed or named tool's folders, in the declared
    /// order, then <paramref name="inherited"/>. Null when no tool puts a folder there, so the child's is the inherited
    /// one exactly. An empty inherited <c>PATH</c> adds no empty folder.
    /// </summary>
    public static string? ChildPath(ToolsRead read, string home, string? inherited)
    {
        var folders = Declared.SelectMany(tool => Folders(read, home, tool.Id)).ToList();
        if (folders.Count == 0) return null;
        if (!string.IsNullOrEmpty(inherited)) folders.Add(inherited);
        return string.Join(Path.PathSeparator, folders);
    }

    /// <summary>
    /// What a child's environment takes over the one it inherited (§2.4): <see cref="PathVariable"/> when a tool puts
    /// a folder first, and nothing when every tool is the system's.
    /// </summary>
    public static IReadOnlyDictionary<string, string> ChildEnvironment(ToolsRead read, string home, string? inheritedPath)
    {
        var variables = new Dictionary<string, string>(StringComparer.Ordinal);
        if (ChildPath(read, home, inheritedPath) is { } path) variables[PathVariable] = path;
        return variables;
    }

    /// <summary>
    /// Hand a start the tools' environment, read now from <c>$DAORIS_HOME</c>'s file (D63's one seam), over the
    /// <c>PATH</c> the start holds. A process with no home changes nothing.
    /// </summary>
    public static void Hand(ProcessStartInfo info) => Hand(info, DaorisHome.Resolve());

    /// <summary>Hand a start the tools' environment, read now from <paramref name="home"/>'s file; null changes nothing.</summary>
    public static void Hand(ProcessStartInfo info, string? home)
    {
        if (home is null) return;
        Hand(info, Read(home), home);
    }

    /// <summary>Hand a start the tools' environment from a read the caller already holds.</summary>
    public static void Hand(ProcessStartInfo info, ToolsRead read, string home)
    {
        var inherited = info.Environment.TryGetValue(PathVariable, out var path) ? path : null;
        foreach (var (name, value) in ChildEnvironment(read, home, inherited)) info.Environment[name] = value;
    }

    /// <summary>
    /// Which file starts for a command's first word (§2.4), or null when no tool answers for it and the caller's own
    /// resolver decides. The tool's own name is <see cref="Resolve(ToolsRead, string, string, string?)"/>'s answer;
    /// another name it answers for is found beside the tool's file, by PATHEXT, and a managed or named tool never
    /// lends <c>PATH</c>'s.
    /// </summary>
    /// <param name="path">The <c>PATH</c> the system's tool is found on; null for this process's own.</param>
    public static ToolResolution? ResolveCommand(ToolsRead read, string home, string name, string? path = null)
    {
        var declared = Declared.FirstOrDefault(tool => tool.Answers.Contains(name, StringComparer.Ordinal));
        if (declared is null) return null;

        var resolution = Resolve(read, home, declared.Id, path);
        if (name == declared.Answers[0] || resolution.Refused) return resolution;

        if (resolution.Way == ToolWay.System)
        {
            var found = CommandPresence.Resolve(name, path, startable: true);
            return found is not null
                ? resolution with { File = found, Problem = null }
                : resolution with
                {
                    File = null,
                    Problem = $"`{name}` is not on this machine's PATH. {declared.Name} is run as the system's, managed, or from a "
                        + $"file you name: `daoris tool use {declared.Id} file <path>` names one",
                };
        }

        var folders = Folders(read, home, declared.Id);
        var beside = CommandPresence.Resolve(name, string.Join(Path.PathSeparator, folders), startable: true);
        return beside is not null
            ? resolution with { File = beside }
            : resolution with
            {
                File = null,
                Refused = true,
                Problem = $"`{name}` is not beside {declared.Name}'s own file ({resolution.File}) — {Never}. "
                    + $"`daoris tool use {declared.Id} system` runs the one on PATH",
            };
    }

    /// <summary>
    /// A downloaded version's folders for <c>PATH</c>, from its record: its <c>paths</c> when they are a list of
    /// folders inside the package, else the folder its <c>exe</c> is in (§3.2's default, which TOOLS4 writes out).
    /// </summary>
    private static IReadOnlyList<string> RecordPaths(string record)
    {
        var (value, _) = Load(record);
        if (value?["paths"] is JsonArray named && named.Count > 0
            && named.Select(Text).All(path => path is not null && (path == "." || IsInside(path))))
        {
            return [.. named.Select(path => Text(path)!)];
        }

        var exe = Text(value?["exe"]) ?? "";
        var at = exe.LastIndexOf('/');
        return [at < 0 ? "." : exe[..at]];
    }
}
