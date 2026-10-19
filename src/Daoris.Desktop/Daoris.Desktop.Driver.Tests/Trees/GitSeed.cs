using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A repository shape built once per test run and copied per test (PROC1h): making a repository (<c>init</c>, <c>config</c>,
/// the first commit, <c>remote add</c>, the first push) was about half of the fixtures' own git, and every test made the same one.
/// </summary>
/// <remarks>
/// A seed is built by the fixture's own steps in a folder under the run's own scratch (<c>_fixtures/seeds/&lt;run&gt;</c>,
/// removed when the host exits), at the first test that asks for the shape; the git half's classes run four at once in one host, so
/// the build is a lazy task per shape and the others wait for it. A copy is a plain file copy of the folders, so each test has its
/// own writable repositories and none sees another's; what git keeps in them is copied too, hooks and config included. Two things
/// are mended: the origin's url, which names the seed's bare repository and now names the copy's own, and <c>worktrees/</c>, which a
/// seed never has but a copy would not carry if it did (a linked worktree's metadata names a path outside the copy).
/// 🔴 A test that reads a fresh repository's exact history (commit ids, times) keeps its own build: every copy of a shape holds the
/// seed's commits, the same ids at the seed's time.
/// </remarks>
internal static class GitSeed
{
    private static readonly string Run = $"{Environment.ProcessId}-{Guid.NewGuid():N}"[..16];
    private static readonly ConcurrentDictionary<string, Lazy<Task<string>>> Seeds = new();
    private static readonly Regex OriginUrl = new(@"^(?<lead>[ \t]*url[ \t]*=[ \t]*).*$", RegexOptions.Multiline | RegexOptions.CultureInvariant);

    static GitSeed()
    {
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            try
            {
                Remove(Folder());
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Scratch the host leaves is under the ignored fixtures folder; the next run's own folder never meets it.
            }
        };
    }

    /// <summary>The seed of <paramref name="shape"/>: <paramref name="build"/> runs once per run, in a folder of its own.</summary>
    public static Task<string> SeedAsync(string shape, Func<string, Task> build) =>
        Seeds.GetOrAdd(shape, key => new Lazy<Task<string>>(async () =>
        {
            var folder = Path.Combine(Folder(), Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(folder);
            await build(folder).ConfigureAwait(false);
            return folder;
        })).Value;

    /// <summary>A plain copy of <paramref name="from"/> as <paramref name="to"/>, files with their times, and no linked-worktree metadata.</summary>
    public static void Copy(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var file in Directory.EnumerateFiles(from)) File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
        foreach (var directory in Directory.EnumerateDirectories(from))
        {
            // A linked worktree's folder names a path outside this copy; a copy has none of its seed's.
            if (Path.GetFileName(directory) == "worktrees" && Path.GetFileName(from) == ".git") continue;
            Copy(directory, Path.Combine(to, Path.GetFileName(directory)));
        }
    }

    /// <summary>
    /// A scratch folder of repositories removed whole (PROCSPEED1): git writes its objects read-only, a copy keeps that, and
    /// <c>Directory.Delete</c> refuses a read-only file on Windows, so each file is made writable first, as <c>GitTree</c>'s cleanup
    /// does. A link is never walked into, so nothing it points at outside the folder changes. A file something still holds (a
    /// process a test started, a scanner) is waited for, 12 tries a quarter second apart (SCRATCHLEAK1, as the landing fixtures
    /// already waited); the last refusal is thrown, as <c>Directory.Delete</c> throws it.
    /// </summary>
    public static void Remove(string folder)
    {
        for (var attempt = 1; Directory.Exists(folder); attempt++)
        {
            try
            {
                var walk = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint };
                foreach (var file in Directory.EnumerateFiles(folder, "*", walk))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }

                Directory.Delete(folder, recursive: true);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                if (attempt >= 12) throw;
                Thread.Sleep(250);
            }
        }
    }

    /// <summary>
    /// <paramref name="repository"/>'s <c>origin</c> made to name <paramref name="origin"/>: the one <c>url</c> in its config is
    /// written again as git writes a path, backslashes doubled, so nothing starts to do it.
    /// </summary>
    public static void RepairOrigin(string repository, string origin)
    {
        var config = Path.Combine(repository, ".git", "config");
        var text = File.ReadAllText(config);
        var repaired = OriginUrl.Replace(text, match => match.Groups["lead"].Value + origin.Replace("\\", "\\\\", StringComparison.Ordinal), 1);
        if (repaired == text) throw new InvalidOperationException($"{config} named no origin url to repair");
        File.WriteAllText(config, repaired, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    /// <summary>
    /// A <see cref="Remove"/> whose last refusal is let go (SCRATCHLEAK1): what a test's <c>Dispose</c> calls, so a straggler's hold
    /// on a file leaves the folder for a later clear and never fails the test that has already passed.
    /// </summary>
    public static void Discard(string folder)
    {
        try
        {
            Remove(folder);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Scratch left here is under the ignored fixtures folder; the retries in Remove have already waited.
        }
    }

    /// <summary>The checkout a test run is in: the scratch's root, <c>_fixtures</c> beside it.</summary>
    public static string RepoRoot() => RootFrom(AppContext.BaseDirectory);

    /// <summary>
    /// The nearest folder at or above <paramref name="start"/> with a <c>.git</c>, a directory or a file (SCRATCHLEAK1): a linked
    /// worktree's <c>.git</c> is a file, and a walk that wanted a directory passed it into the main checkout's folder.
    /// </summary>
    public static string RootFrom(string start)
    {
        var directory = new DirectoryInfo(start);
        while (directory is not null && !Path.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? start;
    }

    private static string Folder() => Path.Combine(RepoRoot(), "_fixtures", "seeds", Run);
}
