using System.Diagnostics;
using System.Text;

namespace Daoris.Driver;

/// <summary>The proof asked of a staged build before it is swapped in: the refusal's code and sentence for the build staged beside <paramref name="install"/> over <paramref name="home"/>; null where it may be installed.</summary>
public delegate (string Code, string Sentence)? StagedProof(string install, string home, string? staged);

/// <summary>
/// A staged build starts clean on a copy of the real home before it is swapped in. The running build copies the home into a scratch
/// beside the staged build (<see cref="HomeCopy"/>), reads the copy itself (<see cref="HomeProof.Read"/>) and has the staged build's own
/// driver read the same copy (<c>daoris home proof</c>); the swap is refused, the old build keeps running and the sentence says why, unless
/// every account the running build lists of the copy is still listed and the staged build finds nothing amiss in it the running build
/// does not (<see cref="Novel"/>).
/// </summary>
/// <remarks>
/// <para>Both builds read one copy, so a stand-in, a folder kept by its names or a file its holder let no one read is read alike by both
/// and is never the staged build's fault; what differs is the builds. The source is read and never written, and the copy is removed when
/// the proof ends.</para>
/// <para>A staged build that cannot read the proof (it carries no driver, its driver has no such verb, or it does not end within
/// <see cref="Bound"/>) is refused as one that found the home amiss: the swap is of builds that show they start.</para>
/// </remarks>
public static class HomeProbe
{
    /// <summary>The refusal's code.</summary>
    public const string Code = "home";

    /// <summary>The folder under <c>update/</c> the copies are made in.</summary>
    public const string Scratch = "probe";

    /// <summary>How long the staged build's reading may take.</summary>
    public static readonly TimeSpan Bound = TimeSpan.FromMinutes(2);

    /// <summary>Runs the staged build's reading of <paramref name="copy"/> (a home): its exit code and everything it said.</summary>
    public delegate (int Exit, string Output) ProofRun(string staged, string copy);

    /// <summary>The proof as the application asks it: the staged build's own driver reads the copy.</summary>
    public static (string Code, string Sentence)? Refused(string install, string home, string? staged) => Refused(install, home, staged, StartStaged);

    /// <param name="run">How the staged build reads the copy; the staged build's driver by <see cref="Refused(string, string, string?)"/>.</param>
    public static (string Code, string Sentence)? Refused(string install, string home, string? staged, ProofRun run)
    {
        var scratch = Path.Combine(install, StagedBuild.Folder, Scratch);
        HomeCopy.Sweep(scratch);
        var made = Path.Combine(scratch, Guid.NewGuid().ToString("N")[..12]);
        using var held = HomeCopy.Hold(made);
        try
        {
            var copy = HomeCopy.Make(home, made);
            var running = HomeProof.Read(copy);
            var (exit, output) = run(StagedBuild.StagedOf(install), copy);
            if (HomeProof.Parse(output) is not { } proof)
            {
                var said = output.Trim();
                return Refuse($"it gave no readable report of the copy (exit {exit}{(said.Length == 0 ? "" : $": {First(said)}")})");
            }

            var reasons = new List<string>();
            foreach (var (agent, accounts) in running.Accounts)
            {
                var listed = proof.Accounts.TryGetValue(agent, out var still) ? still : [];
                reasons.AddRange(accounts.Where(account => !listed.Contains(account, StringComparer.Ordinal)).Select(account => $"the account `{agent}/{account}` is no longer listed"));
            }

            reasons.AddRange(Novel(proof.Problems, running.Problems, copy));
            return reasons.Count == 0 ? null : Refuse(string.Join("; ", reasons.Take(Said)) + (reasons.Count > Said ? $"; and {reasons.Count - Said} more" : ""));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return Refuse($"the copy of the home could not be made: {error.Message}");
        }
        finally
        {
            HomeCopy.Remove(made);
        }
    }

    /// <summary>What the staged build finds amiss in <paramref name="copy"/> that the running build's reading of it does not, the copy's path in either however it is written.</summary>
    internal static IReadOnlyList<string> Novel(IEnumerable<string> staged, IEnumerable<string> running, string copy)
    {
        string Key(string problem) => problem.Replace(Path.GetFullPath(copy), "<home>", StringComparison.OrdinalIgnoreCase);
        var known = running.Select(Key).ToHashSet(StringComparer.Ordinal);
        return [.. staged.Where(problem => !known.Contains(Key(problem)))];
    }

    private const int Said = 4;

    private static (string Code, string Sentence) Refuse(string why) =>
        (Code, $"the staged build did not start clean on a copy of this home: {why}. Nothing was replaced and this build keeps running.");

    private static string First(string text) => text.Split('\n')[0].Trim();

    /// <summary>The staged build's driver run on the copy, as <c>daoris home proof</c>, with nothing of this process's own Daoris environment.</summary>
    private static (int Exit, string Output) StartStaged(string staged, string copy)
    {
        const string program = "daoris-driver";
        var driver = Path.Combine(staged, StagedBuild.AppFolder, program, OperatingSystem.IsWindows() ? program + ".exe" : program);
        if (!File.Exists(driver)) return (-1, "the build carries no program to read the copy with");
        // Not the tools' environment: the staged build's own program reads a copy and starts nothing the tools provide.
        var info = new ProcessStartInfo(driver)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = copy,
        };
        info.ArgumentList.Add("home");
        info.ArgumentList.Add("proof");
        foreach (var name in info.Environment.Keys.Where(key => key.StartsWith("DAORIS_", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            info.Environment.Remove(name);
        }

        info.Environment[DaorisHome.Variable] = copy;
        var run = ChildProcess.RunAsync(info, Bound, CancellationToken.None, closeInput: false).GetAwaiter().GetResult();
        if (!run.Started) return (-1, run.Error?.Message ?? "it did not start");
        if (run.TimedOut || run.ExitCode is not { } exit) return (-1, $"it did not end within {(int)Bound.TotalSeconds} seconds");
        return (exit, run.Stdout + run.Stderr);
    }
}
