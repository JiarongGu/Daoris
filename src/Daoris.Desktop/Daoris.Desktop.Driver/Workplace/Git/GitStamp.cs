using System.Security.Cryptography;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// The one stamp of what a git answer rests on, for the folder that asks: the git in use, every configuration file git would read there
/// (present or absent), the replacements, grafts and <c>shallow</c>, and the variables that change an answer. A digest of their contents,
/// never their write times, so it is the same after a restart where nothing moved and differs where anything did.</summary>
/// <remarks>
/// <para><b>The candidates are git's levels and every include named.</b> The system file (<c>GIT_CONFIG_SYSTEM</c>'s, else the one the git
/// in use names, asked of it once a git in use; none under <c>GIT_CONFIG_NOSYSTEM</c>), on Windows the <c>ProgramData</c> file before it,
/// Daoris's global file and the person's own and XDG files, the repository's <c>config</c>, the asking tree's <c>config.worktree</c>,
/// and each file an <c>include.path</c> or <c>includeIf.*.path</c> names in a present candidate, resolved as git resolves it and followed
/// into its own includes. A conditional's inputs are stamped whether it matches now or not: the asking folder for <c>gitdir:</c>, the
/// tree's <c>HEAD</c> for <c>onbranch:</c>.</para>
/// <para><b>Each file is a <see cref="FileStamp"/></b>, so a warm stamp is stats.</para>
/// <para><b>The context</b> is the asking tree's own part: the repository, the tree's files and the folder where a <c>gitdir:</c>
/// include stands. Two trees of one repository share a flow memo's answer only where their contexts are equal, so a tree with
/// <c>core.useReplaceRefs=false</c> in its <c>config.worktree</c> is never answered from another's replaced commit. It starts nothing.</para>
/// <para><b>The fence</b> (<see cref="StatFence"/>) holds every input's stat, taken with the stamp.</para>
/// </remarks>
internal sealed class GitStamp
{
    private const int Depth = 10;

    private static readonly string[] Variables =
    [
        "GIT_NO_REPLACE_OBJECTS", "GIT_REPLACE_REF_BASE", "GIT_DEFAULT_HASH", "GIT_DEFAULT_REF_FORMAT", "GIT_CONFIG_NOSYSTEM", "GIT_CONFIG_SYSTEM",
        "GIT_CONFIG_GLOBAL", "GIT_CONFIG_PARAMETERS", "GIT_CONFIG_COUNT", "HOME", "XDG_CONFIG_HOME",
    ];

    private readonly StatFence _fence;

    private GitStamp(string digest, string context, IReadOnlyList<string> files, StatFence fence, bool settled)
    {
        Digest = digest;
        Context = context;
        Files = files;
        _fence = fence;
        Settled = settled;
    }

    /// <summary>The digest of every input's content.</summary>
    public string Digest { get; }

    /// <summary>The asking tree's part (<see cref="ContextOf"/>).</summary>
    public string Context { get; }

    /// <summary>Every configuration file stamped, present or absent, as full paths.</summary>
    public IReadOnlyList<string> Files { get; }

    /// <summary>Whether every write time the fence holds is settled on its volume's clock as the fence is read.</summary>
    public bool Settled { get; }

    /// <summary>Whether the fence read again now is the settled one taken with the stamp: an answer read between the two may be kept.</summary>
    public bool Holds() => Settled && _fence.Holds();

    /// <summary>
    /// The stamp of <paramref name="folder"/>; null where it is no repository's top. The system file's path is asked of the git in use the
    /// first time that git is seen.
    /// </summary>
    /// <param name="variable">The variables a start inherits; the process's by default.</param>
    internal static async Task<GitStamp?> OfAsync(string folder, CancellationToken ct, Func<string, string?>? variable = null)
    {
        variable ??= Environment.GetEnvironmentVariable;
        if (RefFiles.GitDirOf(folder) is null || RefFiles.CommonDirOf(folder) is null) return null;
        var git = GitRuns.GitInUse(ProgramHome.Named) ?? "";
        if (Nosystem(variable) is false && variable("GIT_CONFIG_SYSTEM") is not { Length: > 0 })
        {
            await GitSystemFiles.Shared.LearnAsync(git, folder, ct).ConfigureAwait(false);
        }

        return Read(folder, variable, git);
    }

    /// <summary>The asking tree's part of <paramref name="folder"/>'s stamp; null where it is no repository's top. Nothing is started.</summary>
    internal static string? ContextOf(string folder) =>
        RefFiles.GitDirOf(folder) is null || RefFiles.CommonDirOf(folder) is null
            ? null
            : Read(folder, Environment.GetEnvironmentVariable, GitRuns.GitInUse(ProgramHome.Named) ?? "", hashShared: false)?.Context;

    /// <summary>Whether a git asked in <paramref name="one"/> and one asked in <paramref name="other"/> read the same configuration.</summary>
    internal static bool SameContext(string one, string other) => ContextOf(one) is { } context && string.Equals(context, ContextOf(other), StringComparison.Ordinal);

    private static bool Nosystem(Func<string, string?> variable) =>
        variable("GIT_CONFIG_NOSYSTEM") is { Length: > 0 } value && !value.Equals("false", StringComparison.OrdinalIgnoreCase) && value != "0";

    private static GitStamp? Read(string folder, Func<string, string?> variable, string git, bool hashShared = true)
    {
        if (RefFiles.GitDirOf(folder) is not { } gitDir || RefFiles.CommonDirOf(folder) is not { } common) return null;
        var walk = new Walk(variable);

        if (!Nosystem(variable))
        {
            if (OperatingSystem.IsWindows())
            {
                var data = variable("PROGRAMDATA") ?? Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                if (data.Length > 0) walk.Config("programdata", Path.Combine(data, "Git", "config"), tree: false);
            }

            var system = variable("GIT_CONFIG_SYSTEM") is { Length: > 0 } named ? named : GitSystemFiles.Shared.Of(git);
            if (system is { Length: > 0 }) walk.Config("system", system, tree: false);
        }

        if (ProgramHome.Named is { } home) walk.Config("daoris-global", GitGlobalFile.PathOf(home), tree: false);
        if (variable(GitGlobalFile.Variable) is { Length: > 0 } global) walk.Config("global", global, tree: false);
        else
        {
            var user = GitGlobalFile.UserHome(variable);
            if (variable("XDG_CONFIG_HOME") is { Length: > 0 } xdg) walk.Config("xdg", Path.Combine(xdg, "git", "config"), tree: false);
            else if (user is not null) walk.Config("xdg", Path.Combine(user, ".config", "git", "config"), tree: false);
            if (user is not null) walk.Config("global", Path.Combine(user, ".gitconfig"), tree: false);
        }

        walk.Config("config", Path.Combine(common, "config"), tree: false);
        walk.Config("config.worktree", Path.Combine(gitDir, "config.worktree"), tree: true);
        if (walk.OnBranch) walk.File("HEAD", Path.Combine(gitDir, "HEAD"), tree: true);
        if (walk.GitDir) walk.Line("folder", GitFacts.Folder(folder), tree: true);

        if (hashShared)
        {
            walk.Line("git", git, tree: false);
            foreach (var name in Variables.Concat(Counted(variable))) if (variable(name) is { } value) walk.Line("variable", $"{name}={value}", tree: false);
            var (packed, refs) = GitPackedRefs.Of(common);
            walk.Fence.Add(packed.Path);
            if (packed.Unread) walk.Line("packed-unread", packed.Content!, tree: false);
            foreach (var line in refs.Replacements) walk.Line("packed-replace", line, tree: false);
            walk.Folder("replace", Path.Combine(common, "refs", "replace"));
            walk.File("grafts", Path.Combine(common, "info", "grafts"), tree: false);
            walk.File("shallow", Path.Combine(common, "shallow"), tree: false);
            walk.File("reftable", Path.Combine(common, "reftable", "tables.list"), tree: false);
        }

        var context = $"{GitFacts.Folder(common)}\n{walk.TreeText}";
        var digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{context}\n{walk.SharedText}")));
        return new GitStamp(digest, context, walk.Files, walk.Fence, hashShared && walk.Fence.Settled());
    }

    private static IEnumerable<string> Counted(Func<string, string?> variable)
    {
        if (!int.TryParse(variable("GIT_CONFIG_COUNT"), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var count)) yield break;
        for (var at = 0; at < Math.Min(count, 64); at++)
        {
            yield return $"GIT_CONFIG_KEY_{at}";
            yield return $"GIT_CONFIG_VALUE_{at}";
        }
    }

    /// <summary>One stamp's reading: each input's line, in the order read, and the fence beside it.</summary>
    private sealed class Walk(Func<string, string?> variable)
    {
        private readonly StringBuilder _tree = new();
        private readonly StringBuilder _shared = new();
        private readonly HashSet<string> _seen = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        private readonly List<string> _files = [];

        public bool OnBranch { get; private set; }

        public bool GitDir { get; private set; }

        public string TreeText => _tree.ToString();

        public string SharedText => _shared.ToString();

        public IReadOnlyList<string> Files => _files;

        public StatFence Fence { get; } = new();

        /// <summary>A configuration file and, where it is present, each file it includes, to git's depth.</summary>
        public void Config(string role, string path, bool tree, int depth = 0)
        {
            string full;
            try
            {
                full = Path.GetFullPath(path);
            }
            catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return;
            }

            if (depth > Depth || !_seen.Add(full)) return;
            _files.Add(full);
            var (stamp, includes) = FileStamp.Parsed(full, GitConfigIncludes.Parse);
            Note(role, full, stamp, tree);
            foreach (var include in includes ?? [])
            {
                if (include.Condition is { } condition)
                {
                    GitDir |= condition.StartsWith("gitdir", StringComparison.OrdinalIgnoreCase);
                    OnBranch |= condition.StartsWith("onbranch:", StringComparison.OrdinalIgnoreCase);
                }

                if (Resolve(include.Path, full) is { } target) Config("include", target, tree, depth + 1);
            }
        }

        public void File(string role, string path, bool tree)
        {
            var full = Path.GetFullPath(path);
            Note(role, full, FileStamp.Of(full), tree);
        }

        /// <summary>A folder's files at any depth by name and content, and the folder's and each subfolder's stat in the fence.</summary>
        public void Folder(string role, string path)
        {
            var full = Path.GetFullPath(path);
            try
            {
                Fence.AddFolders(full);
                if (!Directory.Exists(full)) return;
                foreach (var file in Directory.EnumerateFiles(full, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal)) Note(role, file, FileStamp.Of(file), tree: false);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                Line(role, "unreadable:" + Guid.NewGuid().ToString("N"), tree: false);
            }
        }

        public void Line(string role, string text, bool tree) => (tree ? _tree : _shared).Append(role).Append('\t').Append(text).Append('\n');

        // A tree's own file is said by its role alone, so two trees whose files hold the same bytes share a context; any other by its path.
        private void Note(string role, string path, FileStamped stamp, bool tree)
        {
            Line(role, $"{(tree && role != "include" ? "" : path)}\t{stamp.Content ?? "-"}", tree);
            Fence.Add(path);
        }

        private string? Resolve(string named, string including)
        {
            try
            {
                if (named.StartsWith("~/", StringComparison.Ordinal) || named.StartsWith(@"~\", StringComparison.Ordinal))
                {
                    return GitGlobalFile.UserHome(variable) is { } user ? Path.GetFullPath(Path.Combine(user, named[2..])) : null;
                }

                return Path.IsPathRooted(named) ? Path.GetFullPath(named) : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(including)!, named));
            }
            catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return null;
            }
        }
    }
}
