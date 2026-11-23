using System.Text;
using LibGit2Sharp;

namespace Daoris.Driver;

/// <summary>
/// A tree's <c>status --porcelain</c> (v1, with or without <c>-z</c>) answered in process as git prints it: each changed path's two
/// columns, the head against the index (<see cref="GitIndexFile"/>) and the index against the working tree, sorted by its bytes, then
/// its untracked paths (<c>--untracked-files</c> <c>all</c>, <c>normal</c> or <c>no</c>), each <c>?? </c>. Null where git must answer. The
/// same walk is printed as <c>--porcelain=v2</c> (each entry with its modes and blob ids, an untracked path as <c>? name</c>), and with
/// <c>--branch</c> its two headers where the branch has no upstream configured.
/// </summary>
/// <remarks>
/// <para><b>The working tree as git reads it.</b> An entry whose size moved is changed; one whose size and write time are what the index
/// holds, and that is not racy (written in or after the index's second), is unchanged; any other is hashed and compared. A hash rests
/// on git's conversion, which reads configuration the library does not see, so a content holding a carriage return, or one an attribute
/// file may name a filter, <c>ident</c> or an encoding for, is git's to compare.</para>
/// <para><b>Settings from every file git reads</b> (<see cref="GitStamp.ConfigurationFilesOf"/>, its includes followed, and the
/// variables' keys): where two files disagree on one that changes the answer, git answers. <c>core.excludesFile</c> and
/// <c>core.attributesFile</c> set anywhere are git's; the default personal ignore file is handed to the library's ignore rules, below a
/// tree's own and <c>info/exclude</c>, as git ranks it.</para>
/// <para><b>Git's where it could differ:</b> a rename git may pair (a staged addition beside a staged deletion, or beside a staged
/// change where copies are asked) unless <c>--no-renames</c>; whatever the index reader refuses (a submodule, a sparse or intent-to-add
/// entry, a conflict); a gitlink or a link in the head; a link or junction in the tree, a folder standing at an entry's path; a subfolder, a
/// bare repository or <c>core.worktree</c> (<see cref="GitLibraryLayout.Top"/>); <c>--ignored</c>, a path given, any other option, a branch with an upstream under <c>--branch</c>; and
/// in the newline form a path git would quote (a space, a quote, a backslash, a control character or a byte past ASCII).</para>
/// </remarks>
internal static class GitLibraryStatus
{
    private const string GitEntry = ".git";

    // An index or an object store the process names elsewhere, which git reads and the library does not.
    private static readonly string[] Moved = ["GIT_INDEX_FILE", "GIT_OBJECT_DIRECTORY", "GIT_ALTERNATE_OBJECT_DIRECTORIES"];
    private static readonly string[] Converting = ["filter", "ident", "encoding"];

    private sealed record Shape(bool Nul, string? Untracked, bool Renames, bool V2, bool Branch);

    private sealed record Cell(char X, char Y, int HeadMode, string HeadId, int IndexMode, string IndexId, int TreeMode);

    private const string NoId = "0000000000000000000000000000000000000000";

    /// <summary>A refusal deep in a walk: the whole read is git's.</summary>
    private sealed class StatusRefusal : Exception;

    internal static (GitLibraryQuestion, Func<GitLibraryCall, GitLibraryAnswer?>)? Asked(List<string> rest)
    {
        var (porcelain, nul, untracked, renames, v1, v2, branch) = (false, false, (string?)null, true, false, false, false);
        foreach (var each in rest)
        {
            switch (each)
            {
                case "--porcelain" or "--porcelain=v1": porcelain = v1 = true; break;
                case "--porcelain=v2": porcelain = v2 = true; break;
                case "--branch": branch = true; break;
                case "-z": nul = true; break;
                case "--untracked-files=all" or "-uall": untracked = "all"; break;
                case "--untracked-files=normal" or "-unormal": untracked = "normal"; break;
                case "--untracked-files=no" or "-uno": untracked = "no"; break;
                case "--no-renames": renames = false; break;
                case "--ignore-submodules=none": break;
                default: return null;
            }
        }

        if (!porcelain || (v1 && v2) || (branch && !v2)) return null;
        return (GitLibraryQuestion.Status, call => Answer(call, new Shape(nul, untracked, renames, v2, branch)));
    }

    private static GitLibraryAnswer? Answer(GitLibraryCall call, Shape shape)
    {
        if (Moved.Any(name => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name)))) return null;
        if (GitLibraryLayout.Top(call) is null || call.Repository.Info.WorkingDirectory is not { } work) return null;
        var top = Path.TrimEndingDirectorySeparator(Path.GetFullPath(work));
        if (StatusSettings.Of(top, call) is not { } settings) return null;
        var untracked = shape.Untracked ?? (settings.UntrackedSet ? null : "normal");
        if (untracked is null || GitIndexFile.Read(Path.Combine(call.GitDir, "index"), out _) is not { } index) return null;
        try
        {
            var text = new StringBuilder();
            if (shape.Branch && !Branch(call, settings, text, shape.Nul)) return null;
            var columns = Staged(call, index.Entries, shape.Renames, settings.Copies);
            if (columns is null) return null;
            var reader = new WorkingFiles(call, top, index.Entries, settings);
            foreach (var entry in index.Entries)
            {
                if (reader.Worktree(entry, index.Written) is not { } y) continue;
                var staged = columns.TryGetValue(entry.Path, out var held) ? held : new Cell(' ', ' ', entry.Mode, entry.Id, entry.Mode, entry.Id, entry.Mode);
                columns[entry.Path] = staged with { Y = y, TreeMode = y == 'D' ? 0 : reader.ModeOf(entry) };
            }

            var listed = untracked == "no" ? [] : reader.Untracked(all: untracked == "all");
            foreach (var (path, cell) in Sorted(columns.Select(each => (each.Key, each.Value)))) Line(text, shape, cell, path);
            foreach (var (path, _) in Sorted(listed.Select(each => (each, 0)))) Line(text, shape, null, path);
            return new GitLibraryAnswer(0, text.ToString());
        }
        catch (StatusRefusal)
        {
            return null;
        }
    }

    private static void Line(StringBuilder text, Shape shape, Cell? cell, string path)
    {
        if (!shape.Nul && path.Any(Quoted)) throw new StatusRefusal();
        if (!shape.V2) text.Append(cell is null ? "??" : $"{cell.X}{cell.Y}").Append(' ');
        else if (cell is null) text.Append("? ");
        else
        {
            text.Append($"1 {Dot(cell.X)}{Dot(cell.Y)} N... {Octal(cell.HeadMode)} {Octal(cell.IndexMode)} {Octal(cell.TreeMode)} {cell.HeadId} {cell.IndexId} ");
        }

        text.Append(path).Append(shape.Nul ? '\0' : '\n');

        static char Dot(char each) => each == ' ' ? '.' : each;
        static string Octal(int mode) => Convert.ToString(mode, 8).PadLeft(6, '0');

        static bool Quoted(char each) => each <= ' ' || each > '~' || each is '"' or '\\';
    }

    private static bool Branch(GitLibraryCall call, StatusSettings settings, StringBuilder text, bool nul)
    {
        if (settings.ForeignBranches) return false;
        var repository = call.Repository;
        var detached = repository.Info.IsHeadDetached;
        var head = repository.Head.CanonicalName;
        if (!detached && !head.StartsWith("refs/heads/", StringComparison.Ordinal)) return false;
        var name = detached ? "(detached)" : head["refs/heads/".Length..];
        if (!detached && !GitLibraryUpstream.NoneConfigured(call, name)) return false;
        var oid = repository.Info.IsHeadUnborn ? "(initial)" : repository.Head.Tip?.Sha;
        if (oid is null) return false;
        var end = nul ? '\0' : '\n';
        text.Append("# branch.oid ").Append(oid).Append(end).Append("# branch.head ").Append(name).Append(end);
        return true;
    }

    private static IEnumerable<(string Path, T Value)> Sorted<T>(IEnumerable<(string Path, T Value)> paths)
    {
        var keyed = paths.Select(each => (each.Path, each.Value, Bytes: Encoding.UTF8.GetBytes(each.Path))).ToList();
        keyed.Sort((one, other) => one.Bytes.AsSpan().SequenceCompareTo(other.Bytes));
        return keyed.Select(each => (each.Path, each.Value));
    }

    /// <summary>The head against the index: each path's first column; null where a rename could pair or the head holds what is not a file.</summary>
    private static Dictionary<string, Cell>? Staged(GitLibraryCall call, IReadOnlyList<GitIndexEntry> entries, bool renames, bool copies)
    {
        var head = new Dictionary<string, (string Id, int Mode)>(StringComparer.Ordinal);
        if (!call.Repository.Info.IsHeadUnborn)
        {
            if (call.Repository.Head.Tip is not { } tip || !Flatten(tip.Tree, "", head)) return null;
        }

        var columns = new Dictionary<string, Cell>(StringComparer.Ordinal);
        var indexed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            indexed.Add(entry.Path);
            if (!head.TryGetValue(entry.Path, out var held)) columns[entry.Path] = new Cell('A', ' ', 0, NoId, entry.Mode, entry.Id, entry.Mode);
            else if (held.Id != entry.Id || held.Mode != entry.Mode) columns[entry.Path] = new Cell('M', ' ', held.Mode, held.Id, entry.Mode, entry.Id, entry.Mode);
        }

        foreach (var path in head.Keys.Where(path => !indexed.Contains(path))) columns[path] = new Cell('D', ' ', head[path].Mode, head[path].Id, 0, NoId, 0);
        var added = columns.Values.Any(each => each.X == 'A');
        if (renames && added && columns.Values.Any(each => each.X == 'D' || (copies && each.X == 'M'))) return null;
        return columns;
    }

    private static bool Flatten(Tree tree, string prefix, Dictionary<string, (string Id, int Mode)> into)
    {
        foreach (var entry in tree)
        {
            var path = prefix + entry.Name;
            switch (entry.Mode)
            {
                case Mode.Directory:
                    if (!Flatten((Tree)entry.Target, path + "/", into)) return false;
                    break;
                case Mode.NonExecutableFile or Mode.NonExecutableGroupWritableFile:
                    into[path] = (entry.Target.Sha, GitIndexFile.Regular);
                    break;
                case Mode.ExecutableFile:
                    into[path] = (entry.Target.Sha, GitIndexFile.Executable);
                    break;
                default:
                    return false;
            }
        }

        return true;
    }

    /// <summary>The working tree read against the index: an entry's second column, and the paths nobody tracks.</summary>
    private sealed class WorkingFiles
    {
        private static readonly EnumerationOptions Listing = new()
        {
            AttributesToSkip = 0, IgnoreInaccessible = false, RecurseSubdirectories = false, ReturnSpecialDirectories = false,
        };

        private readonly GitLibraryCall _call;
        private readonly string _top;
        private readonly StatusSettings _settings;
        private readonly HashSet<string> _tracked;
        private readonly HashSet<string> _folders;
        private readonly Dictionary<string, string> _ids;
        private readonly Dictionary<string, bool> _converts = new(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> _negates = new(StringComparer.Ordinal);

        public WorkingFiles(GitLibraryCall call, string top, IReadOnlyList<GitIndexEntry> entries, StatusSettings settings)
        {
            (_call, _top, _settings) = (call, top, settings);
            var names = settings.IgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            _tracked = new HashSet<string>(entries.Select(entry => entry.Path), names);
            _ids = entries.ToDictionary(entry => entry.Path, entry => entry.Id, StringComparer.Ordinal);
            _folders = new HashSet<string>(names);
            foreach (var entry in entries)
            {
                for (var slash = entry.Path.IndexOf('/'); slash > 0; slash = entry.Path.IndexOf('/', slash + 1)) _folders.Add(entry.Path[..slash]);
            }
        }

        /// <summary>The mode the tree's file holds as git reads it: the index's where the executable bit is not trusted.</summary>
        public int ModeOf(GitIndexEntry entry) => !_settings.FileMode ? entry.Mode : Executable(Full(entry.Path)) ? GitIndexFile.Executable : GitIndexFile.Regular;

        /// <summary>The entry's second column where the tree changed it; null where it did not.</summary>
        public char? Worktree(GitIndexEntry entry, DateTime indexWritten)
        {
            var full = Full(entry.Path);
            var file = new FileInfo(full);
            if (!file.Exists)
            {
                if (Directory.Exists(full)) throw new StatusRefusal();
                return 'D';
            }

            if (Linked(file)) throw new StatusRefusal();
            if (_settings.FileMode && Executable(full) != (entry.Mode == GitIndexFile.Executable)) return 'M';
            var size = unchecked((uint)file.Length);
            if (size != entry.Size && entry.Size != 0) return 'M';
            return Unchanged(entry, file, size, indexWritten) || Same(entry, full) ? null : 'M';
        }

        // Whether git trusts the entry's stat as it stands: on Windows its size, write time and creation time (git's ctime there) as
        // recorded, and the index written after them. Elsewhere git also holds the inode and a ctime .NET cannot read, so the bytes decide.
        private static bool Unchanged(GitIndexEntry entry, FileInfo file, uint size, DateTime indexWritten)
        {
            if (!OperatingSystem.IsWindows() || size != entry.Size || entry.Seconds >= Unix(indexWritten).Seconds) return false;
            return Unix(file.LastWriteTimeUtc) == (entry.Seconds, entry.Nanoseconds) && Unix(file.CreationTimeUtc) == (entry.ChangedSeconds, entry.ChangedNanoseconds);
        }

        /// <summary>The untracked paths, each a file, or a folder git names whole (a nested repository; every untracked folder unless <paramref name="all"/>).</summary>
        public List<string> Untracked(bool all)
        {
            var found = new List<string>();
            Walk("", all, found);
            return found;
        }

        private void Walk(string folder, bool all, List<string> found)
        {
            foreach (var child in Children(folder))
            {
                var path = folder.Length == 0 ? child.Name : $"{folder}/{child.Name}";
                if (folder.Length == 0 && child.Name == GitEntry) continue;
                if (child is DirectoryInfo)
                {
                    if (_tracked.Contains(path)) throw new StatusRefusal();
                    if (_folders.Contains(path)) Walk(path, all, found);
                    else if (Ignored(path)) continue;
                    else if (Nested(child.FullName)) found.Add(path + "/");
                    else if (all) Walk(path, all, found);
                    else if (Holds(path)) found.Add(path + "/");
                }
                else if (!_tracked.Contains(path) && !Ignored(path)) found.Add(path);
            }
        }

        // Whether an untracked folder holds anything git would list.
        private bool Holds(string folder)
        {
            foreach (var child in Children(folder))
            {
                var path = $"{folder}/{child.Name}";
                if (Ignored(path)) continue;
                if (child is not DirectoryInfo || Nested(child.FullName) || Holds(path)) return true;
            }

            return false;
        }

        private IEnumerable<FileSystemInfo> Children(string folder)
        {
            List<FileSystemInfo> children;
            try
            {
                children = [.. new DirectoryInfo(Full(folder)).EnumerateFileSystemInfos("*", Listing)];
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                throw new StatusRefusal();
            }

            foreach (var child in children)
            {
                if (Linked(child) || child.Name.Any(char.IsSurrogate) && !IsWellFormed(child.Name)) throw new StatusRefusal();
            }

            return children;
        }

        // The repository's rules first, as git ranks them; the personal file's decide only a path no repository rule matched. The
        // library ranks a rule added to it above every file, so where it alone ignores a path a repository negation may re-include, git answers.
        private bool Ignored(string path)
        {
            var ignore = _call.Repository.Ignore;
            if (_settings.PersonalIgnore is not { } rules) return ignore.IsPathIgnored(path);
            ignore.ResetAllTemporaryRules();
            if (ignore.IsPathIgnored(path)) return true;
            ignore.AddTemporaryRules(rules);
            if (!ignore.IsPathIgnored(path)) return false;
            return Negates(path) ? throw new StatusRefusal() : true;
        }

        // Whether a rule file git reads for the path above the personal one holds a negation: info/exclude and each folder's .gitignore to it.
        private bool Negates(string path)
        {
            IEnumerable<string> folders = [.. path.Split('/')[..^1].Select((_, at) => string.Join('/', path.Split('/')[..(at + 1)]))];
            foreach (var folder in folders.Prepend("").Prepend("/"))
            {
                if (!_negates.TryGetValue(folder, out var negates))
                {
                    var file = folder == "/" ? Path.Combine(_call.CommonDir, "info", "exclude") : Full(folder.Length == 0 ? ".gitignore" : $"{folder}/.gitignore");
                    try
                    {
                        negates = File.Exists(file) && File.ReadLines(file).Any(line => line.TrimStart().StartsWith('!'));
                    }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                    {
                        throw new StatusRefusal();
                    }

                    _negates[folder] = negates;
                }

                if (negates) return true;
            }

            return false;
        }

        private static bool Nested(string folder) => Path.Exists(Path.Combine(folder, GitEntry));

        // Whether the file's bytes, as git would hash them, are the entry's blob: only where git's conversion is sure to leave them as they are.
        private bool Same(GitIndexEntry entry, string full)
        {
            if (Converts(entry.Path)) throw new StatusRefusal();
            byte[] bytes;
            try
            {
                using var stream = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                bytes = new byte[stream.Length];
                stream.ReadExactly(bytes);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                throw new StatusRefusal();
            }

            if (bytes.AsSpan().IndexOf((byte)'\r') >= 0) throw new StatusRefusal();
            return GitObjectHash.Of("blob", bytes, sha256: false) == entry.Id;
        }

        // Whether any attribute file git reads for the path may name a filter, ident or an encoding.
        private bool Converts(string path)
        {
            if (_settings.Attributes.Any(Names)) return true;
            var folder = "";
            foreach (var part in path.Split('/')[..^1].Prepend(""))
            {
                folder = part.Length == 0 ? "" : folder.Length == 0 ? part : $"{folder}/{part}";
                if (!_converts.TryGetValue(folder, out var converts)) _converts[folder] = converts = Names(Attributes(folder));
                if (converts) return true;
            }

            return false;
        }

        // A folder's own .gitattributes: the tree's file, else the index's, as git reads it for a check-in.
        private string Attributes(string folder)
        {
            var name = folder.Length == 0 ? ".gitattributes" : $"{folder}/.gitattributes";
            var full = Full(name);
            try
            {
                if (File.Exists(full)) return File.ReadAllText(full);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                throw new StatusRefusal();
            }

            return _ids.TryGetValue(name, out var id) && _call.Repository.Lookup<Blob>(id) is { } blob ? blob.GetContentText() : "";
        }

        private static bool Names(string text) => Converting.Any(word => text.Contains(word, StringComparison.OrdinalIgnoreCase));

        private string Full(string path) => path.Length == 0 ? _top : Path.Combine(_top, path.Replace('/', Path.DirectorySeparatorChar));

        private static bool Linked(FileSystemInfo info) => info.Attributes.HasFlag(FileAttributes.ReparsePoint) || info.LinkTarget is not null;

        private static bool Executable(string full) =>
            !OperatingSystem.IsWindows() ? (File.GetUnixFileMode(full) & UnixFileMode.UserExecute) != 0 : throw new StatusRefusal();

        private static bool IsWellFormed(string name)
        {
            for (var at = 0; at < name.Length; at++)
            {
                if (char.IsHighSurrogate(name[at]) && at + 1 < name.Length && char.IsLowSurrogate(name[at + 1])) at++;
                else if (char.IsSurrogate(name[at])) return false;
            }

            return true;
        }

        private static (uint Seconds, uint Nanoseconds) Unix(DateTime utc)
        {
            var ticks = utc.Ticks - DateTime.UnixEpoch.Ticks;
            return ticks < 0 ? (0, 0) : (unchecked((uint)(ticks / TimeSpan.TicksPerSecond)), (uint)(ticks % TimeSpan.TicksPerSecond * 100));
        }
    }

    /// <summary>What git's configuration says that changes a status, read from every file git reads; null where they disagree or say what is git's.</summary>
    private sealed record StatusSettings(bool FileMode, bool IgnoreCase, bool UntrackedSet, bool Copies, IReadOnlyList<string>? PersonalIgnore, IReadOnlyList<string> Attributes, bool ForeignBranches)
    {
        public static StatusSettings? Of(string top, GitLibraryCall call)
        {
            if (GitStamp.ConfigurationFilesOf(top) is not { } files || GitSystemFiles.Shared.Of(GitRuns.GitInUse(ProgramHome.Named) ?? "") is not { } system) return null;
            var values = new List<(string Name, string Value)>();
            var own = Path.GetFullPath(Path.Combine(call.CommonDir, "config"));
            var foreign = Environment.GetEnvironmentVariable("GIT_CONFIG_PARAMETERS") is { } given && given.Contains("branch.", StringComparison.OrdinalIgnoreCase);
            foreach (var file in files.Where(File.Exists))
            {
                var lines = File.ReadAllLines(file);
                var read = GitConfigFile.Parse(lines);
                foreach (var name in Names) values.AddRange(read.All(name).Select(value => (name, value)));
                foreign |= !Path.GetFullPath(file).Equals(own, StringComparison.OrdinalIgnoreCase) && lines.Any(line => line.TrimStart().StartsWith("[branch", StringComparison.OrdinalIgnoreCase));
            }

            if (int.TryParse(Environment.GetEnvironmentVariable("GIT_CONFIG_COUNT"), out var count))
            {
                for (var at = 0; at < count; at++)
                {
                    var key = Environment.GetEnvironmentVariable($"GIT_CONFIG_KEY_{at}") ?? "";
                    foreign |= key.StartsWith("branch.", StringComparison.OrdinalIgnoreCase);
                    if (Names.FirstOrDefault(name => name.Equals(key, StringComparison.OrdinalIgnoreCase)) is { } named) values.Add((named, Environment.GetEnvironmentVariable($"GIT_CONFIG_VALUE_{at}") ?? ""));
                }
            }

            if (Environment.GetEnvironmentVariable("GIT_CONFIG_PARAMETERS") is { Length: > 0 } parameters && Names.Any(name => parameters.Contains(name, StringComparison.OrdinalIgnoreCase))) return null;
            List<string> Of(string name) => [.. values.Where(each => each.Name == name).Select(each => each.Value)];
            if (Of("core.excludesFile").Count > 0 || Of("core.attributesFile").Count > 0 || Of("core.worktree").Count > 0) return null;
            if (Of("core.checkStat").Count > 0 || Agreed(Of("core.trustCtime"), true) is not true) return null;
            if (Agreed(Of("core.fileMode"), true) is not { } fileMode || Agreed(Of("core.ignoreCase"), false) is not { } ignoreCase) return null;
            // The library reads the repository's own files alone: a case rule only another file holds would be read otherwise.
            if (ignoreCase != (GitLibraryLayout.IgnoreCase(call) ?? false)) return null;
            var copies = Of("status.renames").Concat(Of("diff.renames")).Any(value => value.Trim().ToLowerInvariant() is "copies" or "copy");

            Func<string, string?> variable = Environment.GetEnvironmentVariable;
            var config = variable("XDG_CONFIG_HOME") is { Length: > 0 } xdg ? Path.Combine(xdg, "git")
                : GitGlobalFile.UserHome(variable) is { } user ? Path.Combine(user, ".config", "git") : null;
            var attributes = new List<string>();
            IReadOnlyList<string>? ignore = null;
            try
            {
                if (config is not null && File.Exists(Path.Combine(config, "ignore"))) ignore = File.ReadAllLines(Path.Combine(config, "ignore"));
                IEnumerable<string?> sources =
                [
                    system.Length > 0 ? Path.Combine(Path.GetDirectoryName(system)!, "gitattributes") : null,
                    config is null ? null : Path.Combine(config, "attributes"),
                    Path.Combine(call.CommonDir, "info", "attributes"),
                ];
                attributes.AddRange(sources.OfType<string>().Where(File.Exists).Select(File.ReadAllText));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                return null;
            }

            return new StatusSettings(fileMode, ignoreCase, Of("status.showUntrackedFiles").Count > 0, copies, ignore, attributes, foreign);
        }

        private static readonly string[] Names =
        [
            "core.excludesFile", "core.attributesFile", "core.worktree", "core.fileMode", "core.ignoreCase", "core.checkStat", "core.trustCtime",
            "status.showUntrackedFiles",
            "status.renames", "diff.renames",
        ];

        // Every file's value of a boolean agreeing, or its default where none sets it; null where two disagree or one is not a boolean.
        private static bool? Agreed(List<string> values, bool unset)
        {
            var read = values.Select(value => value.Trim().ToLowerInvariant() switch
            {
                "true" or "yes" or "on" or "1" or "" => (bool?)true,
                "false" or "no" or "off" or "0" => false,
                _ => null,
            }).ToList();
            if (read.Any(each => each is null) || read.Distinct().Count() > 1) return null;
            return read.Count == 0 ? unset : read[0];
        }
    }
}
