using System.Diagnostics;
using System.Text;

namespace Daoris.Driver;

/// <summary>The proof asked of a staged build before it is swapped in: the refusal's code and sentence for the build staged beside <paramref name="install"/> over <paramref name="home"/>; null where it may be installed.</summary>
public delegate (string Code, string Sentence)? StagedProof(string install, string home, string? staged);

/// <summary>
/// A staged build starts clean on a copy of the real home before it is swapped in. The running build reads the home
/// (<see cref="HomeProof.Read"/>), copies it into a scratch beside the staged build, and has the staged build's own driver read the copy
/// (<c>daoris home proof</c>); the swap is refused, the old build keeps running and the sentence says why, unless every account the
/// running build lists is still listed and the staged build finds nothing amiss in the copy (<see cref="HomeProofReport.Problems"/>).
/// </summary>
/// <remarks>
/// <para>The source is read and never written. The copy holds the home's own folder whole (its files above
/// <see cref="HomeCopy.MaxFile"/> as empty files) and, of every other folder of the Daoris folder, its names to
/// <see cref="HomeCopy.Depth"/> levels as empty files and folders, which is all a reading of accounts and of paths no entry names
/// needs. It is removed when the proof ends.</para>
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
        var running = HomeProof.Read(home);
        var scratch = Path.Combine(install, StagedBuild.Folder, Scratch);
        HomeCopy.Sweep(scratch);
        var made = Path.Combine(scratch, Guid.NewGuid().ToString("N")[..12]);
        using var held = HomeCopy.Hold(made);
        try
        {
            var copy = HomeCopy.Make(home, made);
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

            reasons.AddRange(proof.Problems);
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

    private const int Said = 4;

    private static (string Code, string Sentence) Refuse(string why) =>
        (Code, $"the staged build did not start clean on a copy of this home: {why}. Nothing was replaced and this build keeps running.");

    private static string First(string text) => text.Split('\n')[0].Trim();

    /// <summary>The staged build's driver run on the copy, as <c>daoris home proof</c>, with nothing of this process's own Daoris environment.</summary>
    private static (int Exit, string Output) StartStaged(string staged, string copy)
    {
        var driver = Path.Combine(staged, StagedBuild.AppFolder, "daoris-driver", OperatingSystem.IsWindows() ? "daoris-driver.exe" : "daoris-driver");
        if (!File.Exists(driver)) return (-1, "the build carries no daoris-driver to read the copy with");
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

/// <summary>The copy of a home a proof is made on, and its removal.</summary>
internal static class HomeCopy
{
    /// <summary>A file above this is copied as an empty one: a record that large is read in no start.</summary>
    internal const long MaxFile = 64L * 1024 * 1024;

    /// <summary>How many folders deep a folder outside the home's own is named in the copy.</summary>
    internal const int Depth = 5;

    /// <summary>What the copy names of folders outside the home's own, at most.</summary>
    private const int Names = 200_000;

    /// <summary>The copy of <paramref name="home"/> in <paramref name="into"/>, laid out as the source is (an install, or a home that is its own folder); the copy's home.</summary>
    internal static string Make(string home, string into)
    {
        var folder = DaorisHome.FolderOf(home);
        var own = Path.GetFullPath(home).TrimEnd(Path.DirectorySeparatorChar);
        var root = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar);
        Directory.CreateDirectory(into);
        File.WriteAllText(Path.Combine(into, DaorisHome.InstallMarker), "# Daoris — installed desktop\n");
        var inside = !string.Equals(own, root, StringComparison.OrdinalIgnoreCase) && own.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        var copyHome = inside ? Path.Combine(into, Path.GetRelativePath(root, own)) : into;
        if (inside) Copy(own, copyHome);
        var left = Names;
        foreach (var child in Directory.EnumerateFileSystemEntries(root))
        {
            var name = Path.GetFileName(child);
            if (IsTheInstalls(name) || string.Equals(Path.GetFullPath(child), own, StringComparison.OrdinalIgnoreCase)) continue;
            Mirror(child, Path.Combine(into, name), 1, ref left);
        }

        if (!inside) Copy(own, copyHome);
        return copyHome;
    }

    /// <summary>The folders of an install that are its own, not the home's (<see cref="HomeStrays"/> passes them too).</summary>
    private static bool IsTheInstalls(string name) =>
        new[] { DaorisHome.InstallApp, DaorisHome.InstallMarker, "purged", DaorisHome.InstallUpdate, DaorisHome.InstallLauncher }
            .Any(own => string.Equals(name, own, StringComparison.OrdinalIgnoreCase));

    /// <summary>The home's own folder, whole: each file as it is, one above <see cref="MaxFile"/> as an empty one. A link is not followed.</summary>
    private static void Copy(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var entry in Directory.EnumerateFileSystemEntries(from))
        {
            var target = Path.Combine(to, Path.GetFileName(entry));
            if (Directory.Exists(entry))
            {
                if (IsLink(entry)) Directory.CreateDirectory(target);
                else Copy(entry, target);
            }
            else if (!Skipped(from, entry))
            {
                if (new FileInfo(entry).Length <= MaxFile) CopyShared(entry, target);
                else File.WriteAllBytes(target, []);
            }
        }
    }

    /// <summary>A file the copy leaves out: what only a running application holds, so a copy of it would hold nothing true.</summary>
    private static bool Skipped(string folder, string file) =>
        string.Equals(Path.GetFileName(file), "home.lease", StringComparison.OrdinalIgnoreCase);

    /// <summary>A file copied reading beside its writers: a store the running application holds open is read as it stands.</summary>
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

    /// <summary>Removes a copy this proof made.</summary>
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

    /// <summary>Marks <paramref name="made"/> as a proof's in progress: a file beside it, held open for as long as the proof lasts.</summary>
    internal static IDisposable Hold(string made)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(made)!);
        return new FileStream(made + Live, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
    }

    /// <summary>Removes the copies an earlier proof left: those whose mark no proof holds any more (one that ended, or died).</summary>
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
