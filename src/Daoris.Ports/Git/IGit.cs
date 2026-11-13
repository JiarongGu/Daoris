namespace Daoris.Ports.Git;

/// <summary>Git at the level of its arguments; a later ring's typed members answer a repository's known questions.</summary>
public interface IGit
{
    Task<GitAnswer> RunAsync(GitAsk ask, CancellationToken ct);

    /// <summary>The first <paramref name="limit"/> bytes of its output, git stopped once they are read: an object that is not text.</summary>
    Task<GitBytes> ReadBytesAsync(GitAsk ask, int limit, CancellationToken ct);

    /// <summary>
    /// Hands each piece of its output to <paramref name="take"/> as git writes it, and stops git once <paramref name="take"/> answers
    /// false: 0 then, else git's exit, and null where git did not start, outlived its bound or could not be read.
    /// </summary>
    Task<int?> ReadAsync(GitAsk ask, Func<ReadOnlyMemory<char>, bool> take, CancellationToken ct);

    /// <summary>The program git runs as for a home, with the moment its file changed, or null where none resolves.</summary>
    string? InUse(string? home);
}
