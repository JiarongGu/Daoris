namespace Daoris.Ports.Git;

/// <summary>
/// Git at the level of its arguments: one run of the program, the bytes it wrote, or its lines as they come. Where a repository's
/// questions are known, a typed member of a later ring answers them; this ring carries the rest.
/// </summary>
public interface IGit
{
    /// <summary>Runs git once and answers its exit, its output and its error.</summary>
    Task<GitAnswer> RunAsync(GitAsk ask, CancellationToken ct);

    /// <summary>Runs git once and answers its standard output as bytes, for an object's content that is not text.</summary>
    Task<GitBytes> ReadBytesAsync(GitAsk ask, CancellationToken ct);

    /// <summary>Runs git and yields each line of its output as it is written.</summary>
    IAsyncEnumerable<string> ReadLinesAsync(GitAsk ask, CancellationToken ct);

    /// <summary>The program git is run as for a home, or null where the one the machine has is used.</summary>
    string? InUse(string? home);
}
