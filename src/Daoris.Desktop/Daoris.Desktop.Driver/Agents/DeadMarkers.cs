namespace Daoris.Driver;

/// <summary>
/// The session process markers (<see cref="HomeEntry.SessionPids"/>) a process left when it ended without its registry's removal: a marker
/// whose process no longer runs, of a session the service no longer lists as active. An active session's marker stays, since an
/// opener's mark names a process that is not the session's yet, and so does a mark whose opener still runs.
/// </summary>
internal static class DeadMarkers
{
    /// <returns>How many markers were removed.</returns>
    internal static int Sweep(string markers, IReadOnlySet<string> active)
    {
        if (!Directory.Exists(markers)) return 0;
        var removed = 0;
        try
        {
            foreach (var marker in Directory.EnumerateFiles(markers, "*.pid"))
            {
                if (active.Contains(Path.GetFileNameWithoutExtension(marker)) || SessionProcesses.MarkerAlive(marker)
                    || SessionProcesses.OpenerAlive(marker)) continue;
                try
                {
                    File.Delete(marker);
                    removed++;
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    // A marker held open stays for the next sweep.
                }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A folder that cannot be listed has nothing to sweep this time.
        }

        return removed;
    }
}
