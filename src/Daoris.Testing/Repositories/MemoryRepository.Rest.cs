using Daoris.Ports.Repositories;

namespace Daoris.Testing;

public sealed partial class MemoryRepository
{
    /// <summary>The empty commits made, as (folder, name, email, message), in order.</summary>
    public List<(string Folder, string Name, string Email, string Message)> Empties { get; } = [];

    /// <summary>Words that refuse the next empty commit, as a hook's refusal does; null for none.</summary>
    public string? RefuseCommit { get; set; }

    /// <summary>Whether <paramref name="folder"/> is a checkout of this repository or lies within one.</summary>
    public bool Holds(string folder)
    {
        var full = Full(folder);
        return _trees.Any(each => string.Equals(each.Path, full, StringComparison.OrdinalIgnoreCase)
            || full.StartsWith(each.Path + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
    }

    public Task<bool> AvailableAsync(CancellationToken ct) => Task.FromResult(true);

    public Task<GitDone> InitAsync(string folder, string line, CancellationToken ct)
    {
        if (Find(folder) is not { } tree) return Task.FromResult(GitDone.Not($"{folder} is not a folder of this repository"));
        if (HeadOf(tree) is null) tree.Branch = line;
        return Task.FromResult(GitDone.Done);
    }

    public Task<GitDone> CommitEmptyAsync(string folder, string name, string email, string message, CancellationToken ct)
    {
        if (Find(folder) is not { } tree) return Task.FromResult(GitDone.Not($"{folder} is not a git repository"));
        if (RefuseCommit is { } refused) return Task.FromResult(GitDone.Not(refused));
        var head = HeadOf(tree);
        var made = _history.Add(head is null ? [] : [head.Id], head?.Tree ?? new Dictionary<string, MemoryBlob>(), message);
        if (tree.Branch is { } branch) _history.Refs[$"refs/heads/{branch}"] = made.Id;
        else tree.Detached = made.Id;
        Empties.Add((folder, name, email, message));
        return Task.FromResult(GitDone.Done);
    }
}
