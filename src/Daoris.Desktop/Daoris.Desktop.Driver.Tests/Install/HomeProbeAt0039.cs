using System.Diagnostics;
using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The home probe as 0.0.39 (e82e2e5116) runs it, its logic copied unchanged: the probe that judges an update is the running build's, so a
/// build must pass this one to reach an install that runs 0.0.39. It copies the home's own folder whole, reads the real home with the
/// running build's reading, and fails on the first file it cannot read. Delete it once no install runs a build older than the one this
/// file lands with.
/// </summary>
internal static class HomeProbeAt0039
{
    private static readonly TimeSpan Bound = TimeSpan.FromMinutes(2);

    public static (string Code, string Sentence)? Refused(string install, string home, string? staged)
    {
        var running = HomeProof.Read(home);
        var scratch = Path.Combine(install, StagedBuild.Folder, HomeProbe.Scratch);
        Copy.Sweep(scratch);
        var made = Path.Combine(scratch, Guid.NewGuid().ToString("N")[..12]);
        using var held = Copy.Hold(made);
        try
        {
            var copy = Copy.Make(home, made);
            var (exit, output) = StartStaged(StagedBuild.StagedOf(install), copy);
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

            reasons.AddRange(Novel(proof.Problems, running.Problems, copy, home));
            return reasons.Count == 0 ? null : Refuse(string.Join("; ", reasons.Take(Said)) + (reasons.Count > Said ? $"; and {reasons.Count - Said} more" : ""));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return Refuse($"the copy of the home could not be made: {error.Message}");
        }
        finally
        {
            Copy.Remove(made);
        }
    }

    private static IReadOnlyList<string> Novel(IEnumerable<string> staged, IEnumerable<string> running, string copy, string home)
    {
        string Key(string problem) =>
            problem.Replace(Path.GetFullPath(copy), "<home>", StringComparison.OrdinalIgnoreCase).Replace(Path.GetFullPath(home), "<home>", StringComparison.OrdinalIgnoreCase);
        var known = running.Select(Key).ToHashSet(StringComparer.Ordinal);
        return [.. staged.Where(problem => !known.Contains(Key(problem)))];
    }

    private const int Said = 4;

    private static (string Code, string Sentence) Refuse(string why) =>
        (HomeProbe.Code, $"the staged build did not start clean on a copy of this home: {why}. Nothing was replaced and this build keeps running.");

    private static string First(string text) => text.Split('\n')[0].Trim();

    private static (int Exit, string Output) StartStaged(string staged, string copy)
    {
        const string program = "daoris-driver";
        var driver = Path.Combine(staged, StagedBuild.AppFolder, program, OperatingSystem.IsWindows() ? program + ".exe" : program);
        if (!File.Exists(driver)) return (-1, "the build carries no program to read the copy with");
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

    /// <summary>0.0.39's copy: the home's own folder whole, every other folder by its names.</summary>
    private static class Copy
    {
        private const long MaxFile = 64L * 1024 * 1024;

        private const int Depth = 5;

        private const int Names = 200_000;

        internal static string Make(string home, string into)
        {
            var folder = DaorisHome.FolderOf(home);
            var own = Path.GetFullPath(home).TrimEnd(Path.DirectorySeparatorChar);
            var root = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar);
            Directory.CreateDirectory(into);
            File.WriteAllText(Path.Combine(into, DaorisHome.InstallMarker), "# Daoris — installed desktop\n");
            var inside = !string.Equals(own, root, StringComparison.OrdinalIgnoreCase) && own.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
            var copyHome = inside ? Path.Combine(into, Path.GetRelativePath(root, own)) : into;
            if (inside) CopyAll(own, copyHome);
            foreach (var child in Directory.EnumerateFileSystemEntries(root))
            {
                var name = Path.GetFileName(child);
                if (IsTheInstalls(name) || string.Equals(Path.GetFullPath(child), own, StringComparison.OrdinalIgnoreCase)) continue;
                var left = Names;
                Mirror(child, Path.Combine(into, name), 1, ref left);
            }

            if (!inside) CopyAll(own, copyHome);
            return copyHome;
        }

        private static bool IsTheInstalls(string name) =>
            new[] { DaorisHome.InstallApp, DaorisHome.InstallMarker, "purged", DaorisHome.InstallUpdate, DaorisHome.InstallLauncher }
                .Any(own => string.Equals(name, own, StringComparison.OrdinalIgnoreCase));

        private static void CopyAll(string from, string to)
        {
            Directory.CreateDirectory(to);
            foreach (var entry in Directory.EnumerateFileSystemEntries(from))
            {
                var target = Path.Combine(to, Path.GetFileName(entry));
                if (Directory.Exists(entry))
                {
                    if (IsLink(entry)) Directory.CreateDirectory(target);
                    else CopyAll(entry, target);
                }
                else if (!string.Equals(Path.GetFileName(entry), "home.lease", StringComparison.OrdinalIgnoreCase))
                {
                    if (new FileInfo(entry).Length <= MaxFile) CopyShared(entry, target);
                    else File.WriteAllBytes(target, []);
                }
            }
        }

        private static void CopyShared(string from, string to)
        {
            using var source = new FileStream(from, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var target = new FileStream(to, FileMode.Create, FileAccess.Write, FileShare.None);
            source.CopyTo(target);
        }

        private static void Mirror(string from, string to, int depth, ref int left)
        {
            if (left-- <= 0) return;
            if (!Directory.Exists(from))
            {
                File.WriteAllBytes(to, []);
                return;
            }

            Directory.CreateDirectory(to);
            if (depth >= Depth || IsLink(from)) return;
            IEnumerable<string> children;
            try
            {
                children = Directory.EnumerateFileSystemEntries(from).ToList();
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                return;
            }

            foreach (var child in children) Mirror(child, Path.Combine(to, Path.GetFileName(child)), depth + 1, ref left);
        }

        private static bool IsLink(string folder) => new DirectoryInfo(folder).Attributes.HasFlag(FileAttributes.ReparsePoint);

        internal static void Remove(string made)
        {
            try
            {
                if (Directory.Exists(made)) Directory.Delete(made, recursive: true);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Left for the next proof's sweep.
            }
        }

        private const string Live = ".live";

        internal static IDisposable Hold(string made)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(made)!);
            return new FileStream(made + Live, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
        }

        internal static void Sweep(string scratch)
        {
            if (!Directory.Exists(scratch)) return;
            foreach (var old in Directory.GetDirectories(scratch))
            {
                try
                {
                    using var _ = new FileStream(old + Live, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
                    Remove(old);
                }
                catch (IOException)
                {
                    // A proof under way holds it.
                }
            }
        }
    }
}
