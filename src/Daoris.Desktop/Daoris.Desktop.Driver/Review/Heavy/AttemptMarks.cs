namespace Daoris.Driver;

/// <summary>
/// The marks an executor leaves in an attempt's folder, one pair per send: <c>began-N</c> when send N holds its slots and starts, and
/// <c>closed-N</c> when the executor's own close ends its job. A check found with neither result nor job counts as lost only for a send that
/// began and did not close (<see cref="LostChecks"/>).
/// </summary>
public static class AttemptMarks
{
    public static string Began(string home, string run, string invocation, int send) => Path.Combine(VerifyFiles.AttemptFolder(home, run, invocation), $"began-{send}");

    public static string Closed(string home, string run, string invocation, int send) => Path.Combine(VerifyFiles.AttemptFolder(home, run, invocation), $"closed-{send}");

    /// <summary>Writes the mark at <paramref name="path"/>: the send's number and the time.</summary>
    public static void Write(string path, int send, DateTimeOffset at)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, $"{send} {at.ToUniversalTime():O}\n");
    }
}
