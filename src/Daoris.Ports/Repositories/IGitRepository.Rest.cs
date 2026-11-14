namespace Daoris.Ports.Repositories;

public partial interface IGitRepository
{
    /// <summary>Whether git answers on this machine.</summary>
    Task<bool> AvailableAsync(CancellationToken ct);

    /// <summary>Makes <paramref name="folder"/> a repository whose HEAD names the unborn branch <paramref name="line"/>; the folder is made where it is not there.</summary>
    Task<GitDone> InitAsync(string folder, string line, CancellationToken ct);

    /// <summary>Commits an empty change on the checkout's branch as <paramref name="name"/> and <paramref name="email"/>, hooks and signing left out.</summary>
    Task<GitDone> CommitEmptyAsync(string folder, string name, string email, string message, CancellationToken ct);
}
