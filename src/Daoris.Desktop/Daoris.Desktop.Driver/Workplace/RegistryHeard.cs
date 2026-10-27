namespace Daoris.Driver;

/// <summary>
/// What the ref watch's hearing of a repository's refs makes of the registry's following (<see cref="RegistryFollowing"/>, SETUPSTATE1): the
/// workplace's own member of it, which reads git's own files for the line's commit, so the following names no ref, and every caller keeps
/// <c>RegistryFollowing.Heard</c> as spelled.
/// </summary>
public static class RegistryHeard
{
    extension(RegistryFollowing)
    {
            /// <summary>
            /// The ref watch heard <paramref name="repository"/>'s refs move (SETUPSTATE1): where git's own files say its line, as it was last
            /// followed, now stands at another commit than the one followed, it is recorded moved, so the next look follows it. A line moved
            /// outside Daoris (a pull request merged on the remote, then pulled) is how a set-up landed on a branch for review reaches its line,
            /// and no Daoris landing says so. Read with no process: one never followed is the start's to follow, and a line the files cannot
            /// say (the reftable format, a ref being replaced) waits for the next start. Never throws.
            /// </summary>
            /// <returns>Whether it was recorded moved now.</returns>
            public static bool Heard(string home, RefRepository repository)
            {
                try
                {
                    if (!RegistryFollowing.Read(home).TryGetValue(repository.Repository, out var last) || last.Line is not { Length: > 0 } line
                        || last.Commit is not { Length: > 0 } commit
                        || RegistryFollowing.ReadMoved(home).TryGetValue(repository.Repository, out var moved) && moved > last.At)
                    {
                        return false;
                    }

                    var common = TreeTips.CommonDirectory(repository.Root);
                    if (common is null || TreeTips.Named(common, $"refs/heads/{line}") is not { } now
                        || string.Equals(now, commit, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }

                    RegistryFollowing.Moved(home, repository.Repository, DateTimeOffset.UtcNow);
                    return true;
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
                {
                    return false;
                }
            }
    }
}
