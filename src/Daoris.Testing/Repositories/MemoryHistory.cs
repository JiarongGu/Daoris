using System.Security.Cryptography;
using System.Text;

namespace Daoris.Testing;

/// <summary>One commit of a <see cref="MemoryRepository"/>: its parents and the whole tree it holds, a path to its mode and content.</summary>
internal sealed record MemoryCommit(string Id, int Order, IReadOnlyList<string> Parents, IReadOnlyDictionary<string, MemoryBlob> Tree, string Message);

/// <summary>A tracked file as a commit holds it.</summary>
internal sealed record MemoryBlob(string Mode, string Content)
{
    internal const string File = "100644";

    internal const string Submodule = "160000";
}

/// <summary>The commits and refs of a memory repository, and the questions of history asked of them.</summary>
internal sealed class MemoryHistory
{
    private readonly Dictionary<string, MemoryCommit> _commits = new(StringComparer.Ordinal);

    internal Dictionary<string, string> Refs { get; } = new(StringComparer.Ordinal);

    internal MemoryCommit Add(IReadOnlyList<string> parents, IReadOnlyDictionary<string, MemoryBlob> tree, string message)
    {
        var order = _commits.Count + 1;
        var text = string.Join('\n', parents) + "\n" + order + "\n" + message + "\n" + string.Join('\n', tree.OrderBy(each => each.Key, StringComparer.Ordinal).Select(each => $"{each.Key}\0{each.Value.Mode}\0{each.Value.Content}"));
        var commit = new MemoryCommit(Convert.ToHexStringLower(SHA1.HashData(Encoding.UTF8.GetBytes(text))), order, parents, tree, message);
        _commits[commit.Id] = commit;
        return commit;
    }

    internal MemoryCommit? Commit(string? id) => id is not null && _commits.TryGetValue(id, out var commit) ? commit : null;

    /// <summary>The commit a revision names: a full id, a ref's name, <c>HEAD</c> as <paramref name="head"/>, then any run of <c>^</c>, <c>^n</c>, <c>~n</c> and <c>^{commit}</c>.</summary>
    internal MemoryCommit? Resolve(string revision, string? head)
    {
        var end = revision.IndexOfAny(['^', '~']);
        var name = end < 0 ? revision : revision[..end];
        var commit = name == "HEAD" ? Commit(head) : Commit(name) ?? NamedRef(name);
        var rest = end < 0 ? "" : revision[end..];
        while (commit is not null && rest.Length > 0)
        {
            if (rest.StartsWith("^{commit}", StringComparison.Ordinal)) rest = rest["^{commit}".Length..];
            else if (rest[0] is '^' or '~')
            {
                var count = 0;
                var at = 1;
                while (at < rest.Length && char.IsAsciiDigit(rest[at])) count = (count * 10) + (rest[at++] - '0');
                var steps = at == 1 ? 1 : count;
                var caret = rest[0] == '^';
                rest = rest[at..];
                // ^n names the nth parent (^0 the commit itself); ~n walks n first parents.
                if (caret) commit = steps == 0 ? commit : commit.Parents.Count >= steps ? Commit(commit.Parents[steps - 1]) : null;
                else for (var step = 0; step < steps && commit is not null; step++) commit = commit.Parents.Count > 0 ? Commit(commit.Parents[0]) : null;
            }
            else return null;
        }

        return commit;
    }

    private MemoryCommit? NamedRef(string name) =>
        new[] { name, $"refs/heads/{name}", $"refs/tags/{name}", $"refs/remotes/{name}" }
            .Select(each => Refs.TryGetValue(each, out var id) ? Commit(id) : null).FirstOrDefault(each => each is not null);

    /// <summary>The commit and all it reaches.</summary>
    internal HashSet<string> Reach(MemoryCommit from)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Stack<MemoryCommit>([from]);
        while (queue.Count > 0)
        {
            var next = queue.Pop();
            if (!seen.Add(next.Id)) continue;
            foreach (var parent in next.Parents) if (Commit(parent) is { } found) queue.Push(found);
        }

        return seen;
    }

    /// <summary>The commit both reach that no other common commit reaches past; where several, the latest made.</summary>
    internal MemoryCommit? Base(MemoryCommit first, MemoryCommit second)
    {
        var common = Reach(first);
        common.IntersectWith(Reach(second));
        var best = common.Select(id => Commit(id)!).Where(each => !common.Any(other => other != each.Id && Reach(Commit(other)!).Contains(each.Id))).ToList();
        return best.OrderByDescending(each => each.Order).FirstOrDefault();
    }
}
