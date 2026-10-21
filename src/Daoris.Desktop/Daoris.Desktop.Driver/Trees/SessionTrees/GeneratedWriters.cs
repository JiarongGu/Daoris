using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The commands a repository declares write its generated folders (GENREGEN1, D87's note): <c>daoris.gates.json</c>'s <c>generated</c>,
/// each <c>{ "paths": folder, "run": command line }</c>, the declaration this repository's merge tool reads too. A landing's replay that
/// took the line's copy of a generated file (<see cref="GeneratedPaths"/>) runs the writers of those files' folders in its own tree, so
/// what it lands reads as its tree, rather than leaving them for whoever merges the work.
/// </summary>
/// <remarks>
/// <para><b>Only under the person's acceptance.</b> A writer is a command read from the repository, which runs unasked only as the
/// person accepted it (the pipeline design §4.2): the landed route's accepted blob of <c>daoris.gates.json</c> must be the declaration at
/// the commit the replay builds on, and the command is read from that blob, never from the session's copy, so a session cannot declare
/// a command and have its own landing run it. Without that, the replay leaves the line's copy, as before.</para>
/// <para>Which files are generated stays <c>linguist-generated</c> (LANDREBASE1): <c>paths</c> names only which folder a command
/// writes.</para>
/// </remarks>
internal static class GeneratedWriters
{
    /// <summary>The declaration's key in <c>daoris.gates.json</c>.</summary>
    internal const string Key = "generated";

    /// <summary>One declared writer: the folder it writes, relative to the repository's top with no trailing slash, and its command line.</summary>
    internal sealed record Writer(string Folder, string Run)
    {
        /// <summary>Whether <paramref name="path"/> lies in the folder it writes.</summary>
        public bool Holds(string path) => path.StartsWith(Folder + "/", StringComparison.Ordinal);
    }

    /// <summary>What writing them again made: the files its commit changed and the commands run, or why it was not written.</summary>
    internal sealed record Written(IReadOnlyList<string> Paths, IReadOnlyList<string> Runs, string? Failed)
    {
        /// <summary>Nothing run: no writer accepted, or none holds a file taken.</summary>
        public static readonly Written None = new([], [], null);
    }

    /// <summary>
    /// The writers <paramref name="repository"/>'s landed route accepted, as declared at <paramref name="onto"/>: none where the route
    /// accepted no declaration, or the one at <paramref name="onto"/> is another (an edit asks again), or it declares none.
    /// </summary>
    internal static async Task<IReadOnlyList<Writer>> AcceptedAsync(
        DriverConfig config, string root, string repository, string workspace, string onto, CancellationToken ct)
    {
        if (WorkflowSelection.ResolveLanded(config, repository, workspace).Gates is not { } accepted) return [];
        var (found, sha, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", $"{onto}:{LandedGates.File}"], ct)
            .ConfigureAwait(false);
        var blob = sha.Trim();
        if (found != 0 || blob != accepted) return [];
        return await LandedGates.TextAsync(root, blob, ct).ConfigureAwait(false) is { } text ? Parse(text) : [];
    }

    /// <summary>The <c>generated</c> entries of a declaration's text; an entry without both a folder and a command is passed over.</summary>
    internal static IReadOnlyList<Writer> Parse(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty(Key, out var entries) || entries.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var writers = new List<Writer>();
            foreach (var entry in entries.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object) continue;
                var folder = entry.TryGetProperty("paths", out var paths) && paths.ValueKind == JsonValueKind.String ? paths.GetString()!.Trim().TrimEnd('/') : "";
                var run = entry.TryGetProperty("run", out var command) && command.ValueKind == JsonValueKind.String ? command.GetString()!.Trim() : "";
                if (folder.Length > 0 && run.Length > 0) writers.Add(new Writer(folder, run));
            }

            return writers;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// The writers among <paramref name="writers"/> whose folder holds a file of <paramref name="taken"/>, run in <paramref name="tree"/>
    /// (a replay's own, standing at its new tip), each folder staged, and what they changed committed on top as one commit of Daoris's,
    /// so the session's commits stay as replayed. A writer that fails or outlasts the replay's bound commits nothing, and says why.
    /// </summary>
    internal static async Task<Written> WriteAsync(string tree, IReadOnlyList<Writer> writers, IReadOnlyList<string> taken, CancellationToken ct)
    {
        var running = writers.Where(writer => taken.Any(writer.Holds)).ToList();
        if (running.Count == 0) return Written.None;
        var runs = running.Select(writer => writer.Run).Distinct(StringComparer.Ordinal).ToList();
        foreach (var run in runs)
        {
            var ran = await ChildProcess.RunAsync(Shell(run, tree), SyncBounds.Replay, ct, closeInput: true).ConfigureAwait(false);
            if (ran.TimedOut) return new([], runs, $"`{run}` was still running after {SyncBounds.Replay.TotalMinutes:0} minutes");
            if (!ran.Started) return new([], runs, $"`{run}` would not start{(ran.Error is { } error ? $" ({error.Message})" : "")}");
            if (ran.ExitCode != 0) return new([], runs, $"`{run}` ended {ran.ExitCode}: {TreeGit.Failure(ran.Stderr.Length > 0 ? ran.Stderr : ran.Stdout)}");
        }

        var folders = running.Select(writer => writer.Folder).Distinct(StringComparer.Ordinal).ToList();
        var (added, _, addErr) = await WorkingTree.GitAsync(tree, ["add", "-A", "--", .. folders], ct).ConfigureAwait(false);
        if (added != 0) return new([], runs, $"git would not stage what it wrote: {TreeGit.Failure(addErr)}");
        var (same, _, _) = await WorkingTree.GitAsync(tree, ["diff", "--cached", "--quiet", "HEAD"], ct).ConfigureAwait(false);
        if (same == 0) return new([], runs, null);

        var (made, _, madeErr) = await WorkingTree.GitAsync(
            tree, ["commit", "--quiet", "-m", $"Write {string.Join(", ", folders)} again from the replayed tree"], environment: null, SyncBounds.Replay, ct)
            .ConfigureAwait(false);
        if (made != 0) return new([], runs, $"git would not commit what it wrote: {TreeGit.Failure(madeErr)}");
        var (_, names, _) = await WorkingTree.GitAsync(tree, ["-c", "core.quotePath=false", "diff", "--name-only", "HEAD~1", "HEAD"], ct)
            .ConfigureAwait(false);
        return new(names.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), runs, null);
    }

    /// <summary>
    /// <paramref name="run"/> through the system's shell, as a gate's command line runs (<see cref="HeavyStep.CommandLine"/>): <c>cmd.exe</c>
    /// on Windows, <c>/bin/sh</c> elsewhere, in <paramref name="directory"/>, both streams read and its input closed.
    /// </summary>
    internal static ProcessStartInfo Shell(string run, string directory)
    {
        ProcessStartInfo info;
        if (OperatingSystem.IsWindows())
        {
            // `/s` takes what is between the first and the last quote as it is, so the command's own quotes reach the shell untouched.
            info = new ProcessStartInfo(HeavyStep.SystemShell()) { Arguments = $"/d /s /c \"{run}\"", CreateNoWindow = true };
        }
        else
        {
            info = new ProcessStartInfo("/bin/sh") { CreateNoWindow = true };
            info.ArgumentList.Add("-c");
            info.ArgumentList.Add(run);
        }

        info.WorkingDirectory = directory;
        info.UseShellExecute = false;
        info.RedirectStandardInput = true;
        info.RedirectStandardOutput = true;
        info.RedirectStandardError = true;
        info.StandardOutputEncoding = Encoding.UTF8;
        info.StandardErrorEncoding = Encoding.UTF8;
        // A writer is the repository's own command (node and its scripts), found as every child of the driver finds its tools (TOOLS5).
        Tools.Hand(info);
        return info;
    }
}
