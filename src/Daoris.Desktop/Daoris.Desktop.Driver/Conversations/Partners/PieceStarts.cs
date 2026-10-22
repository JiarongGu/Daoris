using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// The partner's strict start, the driver's half (COAGENT1b; D199's note on how a partner starts in its strict job; the co-agent design
/// §2.3 step 5): the harness's start, as <see cref="Driver.Prepare"/> built it, is made the launcher's (<see cref="PieceLauncher"/>), which
/// joins the piece's job before the harness runs. The environment, the folder, the streams and their encodings stay the harness's, so the
/// doors read the launcher as they read any session's process. Never the best effort an ordinary session's job is: with no job made, or no
/// launcher laid out beside the home, the piece is refused <see cref="PartnerWhy.NoJob"/> and nothing starts.
/// </summary>
public static class PieceStarts
{
    /// <summary>The launcher in the install beside <paramref name="home"/>, or null where it is not laid out (a test's home, a development run's).</summary>
    public static string? LauncherPath(string? home) =>
        home is not null && Tools.InstallBin(home) is { } bin && File.Exists(Path.Combine(bin, PieceLauncher.Program))
            ? Path.Combine(bin, PieceLauncher.Program)
            : null;

    /// <summary>
    /// Make <paramref name="harness"/>'s start the launcher's, in place: the launcher named as the program and handed the job, the harness and
    /// its words after it; everything else of the start kept. Null where it was made; otherwise why not, the start unchanged.
    /// </summary>
    public static string? Through(ProcessStartInfo harness, string? launcher, ProcessJob? job, string piece)
    {
        if (Missing(launcher, job, piece) is { } missing) return missing;
        if (!string.IsNullOrEmpty(harness.Arguments)) return "the harness's start carries its words as one string, which the launcher cannot hand on whole.";

        var file = harness.FileName;
        var words = harness.ArgumentList.ToList();
        harness.FileName = launcher;
        harness.ArgumentList.Clear();
        foreach (var word in PieceLauncher.Arguments(PieceJobs.NameOf(piece), file, words)) harness.ArgumentList.Add(word);
        return null;
    }

    /// <summary>Why a partner cannot be started through its launcher before any start is built: no job, or no launcher; null where both are.</summary>
    public static string? Missing(string? launcher, ProcessJob? job, string piece) =>
        job is null ? $"the piece's job could not be made ({PieceJobs.NameOf(piece)}), so the partner was not started."
        : launcher is null ? $"the partner's launcher ({PieceLauncher.Program}) is not laid out in this install, so the partner was not started."
        : null;
}
