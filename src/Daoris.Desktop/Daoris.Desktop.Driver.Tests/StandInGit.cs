using System.Security.Cryptography;
using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// git standing in at the review's seam (<see cref="WorkingTree.GitRead"/>), in process (EVID1b): a few commits, each a set of
/// files by path, answering the reads evidence makes as git answers them: <c>rev-parse --verify --quiet</c> of a commit and of
/// <c>commit:path</c> (a file's blob, a folder's tree, exit 1 for neither), <c>ls-tree -z</c> of a commit or a tree,
/// <c>rev-parse --show-toplevel</c>, and <c>merge-base --is-ancestor</c> over each commit's parent. Every call is kept, so a
/// test sees each argument as git was handed it. Anything else answers 129, as git does to a usage it does not know.
/// </summary>
/// <remarks>Ids are 40 hex characters derived from what they name, so the same commit or file is the same id on every run.</remarks>
internal sealed class StandInGit(string top)
{
    private readonly Dictionary<string, (string? Parent, IReadOnlyDictionary<string, string> Files)> _commits = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _names = new(StringComparer.Ordinal);

    /// <summary>The commit HEAD names, or null for a repository with none.</summary>
    public string? Head { get; private set; }

    /// <summary>Each call's arguments, in order.</summary>
    public List<IReadOnlyList<string>> Calls { get; } = [];

    /// <summary>A start that fails, as a git that cannot run does: every call answers -1.</summary>
    public bool Broken { get; init; }

    /// <summary>A commit holding <paramref name="files"/> (path → content), whose parent is HEAD; HEAD moves to it.</summary>
    public string Commit(string name, params (string Path, string Content)[] files)
    {
        var id = Id("commit " + name);
        _commits[id] = (Head, files.ToDictionary(file => file.Path, file => Id("blob " + file.Content), StringComparer.Ordinal));
        _names[id] = name;
        Head = id;
        return id;
    }

    /// <summary>A file's blob id, as <see cref="Commit"/> names it from its content.</summary>
    public static string Blob(string content) => Id("blob " + content);

    /// <summary>The seam itself.</summary>
    public Task<int> Read(string root, IReadOnlyList<string> arguments, Func<ReadOnlyMemory<char>, bool> take, CancellationToken ct)
    {
        Calls.Add([.. arguments]);
        if (Broken) return Task.FromResult(-1);
        var (code, output) = Answer(arguments);
        if (output.Length > 0) take(output.AsMemory());
        return Task.FromResult(code);
    }

    /// <summary>The calls whose first arguments are <paramref name="start"/>.</summary>
    public IReadOnlyList<IReadOnlyList<string>> CallsTo(params string[] start) =>
        [.. Calls.Where(call => call.Count >= start.Length && call.Take(start.Length).SequenceEqual(start))];

    private (int Code, string Output) Answer(IReadOnlyList<string> arguments)
    {
        switch (arguments)
        {
            case ["rev-parse", "--show-toplevel"]:
                return (0, top.Replace('\\', '/') + "\n");
            case ["rev-parse", "--verify", "--quiet", var name] when name.EndsWith("^{commit}", StringComparison.Ordinal):
                return Resolve(name[..^"^{commit}".Length]) is { } named ? (0, named + "\n") : (1, "");
            case ["rev-parse", "--verify", "--quiet", var spec] when spec.Contains(':'):
            {
                var colon = spec.IndexOf(':');
                if (Resolve(spec[..colon]) is not { } commit) return (128, "");
                var path = spec[(colon + 1)..];
                var files = _commits[commit].Files;
                if (files.TryGetValue(path, out var blob)) return (0, blob + "\n");
                return files.Keys.Any(file => file.StartsWith(path + "/", StringComparison.Ordinal))
                    ? (0, Tree(commit, path) + "\n")
                    : (1, "");
            }

            case ["log", "--oneline", var range]:
            {
                // `X..HEAD`, or `HEAD` alone: each commit after X, newest first, as its abbreviation and its name.
                var stop = range.Contains("..", StringComparison.Ordinal) ? Resolve(range[..range.IndexOf("..", StringComparison.Ordinal)]) : null;
                if (range.Contains("..", StringComparison.Ordinal) && stop is null) return (128, "");
                var lines = new StringBuilder();
                for (var at = Head; at is not null && at != stop; at = _commits[at].Parent)
                {
                    lines.Append($"{at[..7]} {_names[at]}\n");
                }

                return (0, lines.ToString());
            }

            case ["ls-tree", "-z", var treeish]:
                return List(treeish) is { } listed ? (0, listed) : (128, "");
            case ["merge-base", "--is-ancestor", var ancestor, var descendant]:
            {
                if (Resolve(ancestor) is not { } from || Resolve(descendant) is not { } to) return (128, "");
                for (string? at = to; at is not null; at = _commits[at].Parent)
                {
                    if (at == from) return (0, "");
                }

                return (1, "");
            }

            default:
                return (129, "");
        }
    }

    /// <summary>A commit by HEAD, its whole id or a prefix of it, or null.</summary>
    private string? Resolve(string name) =>
        name == "HEAD" ? Head
        : name.Length >= 7 ? _commits.Keys.SingleOrDefault(id => id.StartsWith(name.ToLowerInvariant(), StringComparison.Ordinal))
        : null;

    /// <summary>A folder's tree id, from what it holds, as git's is: the same files make the same tree in every commit.</summary>
    private string Tree(string commit, string folder)
    {
        var prefix = folder + "/";
        var files = _commits[commit].Files;
        return Id("tree " + string.Join("|", files.Keys
            .Where(file => file.StartsWith(prefix, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(file => $"{file[prefix.Length..]}={files[file]}")));
    }

    /// <summary><c>ls-tree -z</c> of a commit (its root) or of a folder's tree id: <c>mode type id TAB name NUL</c> each.</summary>
    private string? List(string treeish)
    {
        foreach (var (commit, (_, files)) in _commits)
        {
            var folders = files.Keys
                .SelectMany(file => Enumerable.Range(1, file.Count(c => c == '/')).Select(depth => string.Join('/', file.Split('/').Take(depth))))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            string? folder = Resolve(treeish) == commit ? ""
                : folders.FirstOrDefault(each => Tree(commit, each) == treeish);
            if (folder is null) continue;

            var prefix = folder.Length == 0 ? "" : folder + "/";
            var listed = new StringBuilder();
            var names = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var file in files.Keys.Where(file => file.StartsWith(prefix, StringComparison.Ordinal)))
            {
                var rest = file[prefix.Length..];
                var slash = rest.IndexOf('/');
                names.Add(slash < 0 ? rest : rest[..slash]);
            }

            foreach (var name in names)
            {
                var path = prefix + name;
                listed.Append(files.TryGetValue(path, out var blob)
                    ? $"100644 blob {blob}\t{name}\0"
                    : $"040000 tree {Tree(commit, path)}\t{name}\0");
            }

            return listed.ToString();
        }

        return null;
    }

    private static string Id(string what) => Convert.ToHexStringLower(SHA1.HashData(Encoding.UTF8.GetBytes(what)));
}
