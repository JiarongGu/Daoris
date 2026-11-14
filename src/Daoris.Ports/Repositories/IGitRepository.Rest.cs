namespace Daoris.Ports.Repositories;

/// <summary>One path a checkout's status names.</summary>
/// <param name="Path">The path from the tree's top, with <c>/</c>.</param>
/// <param name="State">Its two-letter state, <c>??</c> for an untracked one.</param>
/// <param name="From">The path a rename or copy came from; null otherwise.</param>
public sealed record GitStatusEntry(string Path, string State, string? From);

public partial interface IGitRepository
{
    /// <summary>Every path status names, untracked files one by one, each with where a rename or copy came from; thrown where git could not read it.</summary>
    Task<IReadOnlyList<GitStatusEntry>> StatusEntriesAsync(string folder, CancellationToken ct);

    /// <summary>Whether git answers on this machine.</summary>
    Task<bool> AvailableAsync(CancellationToken ct);

    /// <summary>Makes <paramref name="folder"/> a repository whose HEAD names the unborn branch <paramref name="line"/>; the folder is made where it is not there.</summary>
    Task<GitDone> InitAsync(string folder, string line, CancellationToken ct);

    /// <summary>Commits an empty change on the checkout's branch as <paramref name="name"/> and <paramref name="email"/>, hooks and signing left out.</summary>
    Task<GitDone> CommitEmptyAsync(string folder, string name, string email, string message, CancellationToken ct);
}
