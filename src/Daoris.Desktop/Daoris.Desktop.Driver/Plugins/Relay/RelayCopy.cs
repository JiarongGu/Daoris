using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>The relay could not go on, by its closed code (<see cref="RelaySaid.StopCodes"/>): said as <see cref="RelaySaid.Stopped"/>, exit 2.</summary>
public sealed class RelayStop(string code) : Exception(code)
{
    public string Code { get; } = code;
}

/// <summary>
/// The relay's copy of its build: laid out by <c>--where</c> under the home's
/// <c>bin</c> entry, named by <see cref="RelayBuild.Of"/> of the running build's folder, staged beside and moved whole, so a copy appears
/// whole or not at all; each whole copy holds its <see cref="InUse"/> file, which every relay running from it holds open for its life and
/// which keeps it. A build none holds is removed by a rename first, so no relay runs from one being removed and no half-removed folder is
/// ever taken for a build. No junction or symbolic link is followed or removed through.
/// </summary>
public static class RelayCopy
{
    /// <summary>The folder under the home's <c>bin</c> entry the builds are laid out in.</summary>
    public const string Folder = "events-relay";

    /// <summary>The file a whole copy holds, and a relay holds open while it runs.</summary>
    public const string InUse = "in-use";

    private const string Staging = ".staging-";
    private const string Removing = ".removing-";

    private static readonly Regex BuildName = new(@"\A[0-9a-f]{64}\z", RegexOptions.CultureInvariant);

    /// <summary>
    /// Lay the build in <paramref name="source"/> out for <paramref name="home"/>, and answer its folder: an existing whole copy of the same
    /// build is used, one without its <see cref="InUse"/> file laid out again, every other build none holds removed, and every removal a
    /// stopped one left deleted. A link met is said once by <paramref name="said"/> and left.
    /// </summary>
    /// <param name="renamed">Told the removal name of a build just renamed for removal; a test hands in one that holds it.</param>
    /// <exception cref="RelayStop">The copy could not be laid out whole (<c>copy</c>).</exception>
    public static string LayOut(string home, string source, Action<string> said, Action<string>? renamed = null)
    {
        var bin = DaorisHome.Entry(home, HomeEntry.Bin);
        var builds = Path.Combine(bin, Folder);
        var linked = false;
        void Linked()
        {
            if (!linked) said(RelaySaid.Linked);
            linked = true;
        }

        if (!Plain(bin) || !Plain(builds))
        {
            Linked();
            throw new RelayStop("copy");
        }

        Directory.CreateDirectory(builds);
        var build = RelayBuild.Of(source);
        var copy = Path.Combine(builds, build);
        foreach (var entry in Directory.EnumerateFileSystemEntries(builds))
        {
            var name = Path.GetFileName(entry);
            if (name.StartsWith(Staging, StringComparison.Ordinal)) continue;
            if (!Whole(entry))
            {
                Linked();
                continue;
            }

            if (name.StartsWith(Removing, StringComparison.Ordinal))
            {
                if (Unheld(entry)) Delete(entry);
            }
            else if (name == build && !File.Exists(Path.Combine(entry, InUse))) Remove(entry, builds, renamed);
            else if (name != build && BuildName.IsMatch(name) && Directory.Exists(entry)) Remove(entry, builds, renamed);
        }

        if (!File.Exists(Path.Combine(copy, InUse))) Stage(source, builds, build);
        return File.Exists(Path.Combine(copy, InUse)) ? copy : throw new RelayStop("copy");
    }

    /// <summary>
    /// The copy this process runs from, held in use for its life: null where <paramref name="baseDirectory"/> is not a whole copy of a build
    /// under <paramref name="home"/>'s <c>bin</c> entry, a folder on its way is a link, or its <see cref="InUse"/> file will not open (a
    /// build being removed).
    /// </summary>
    public static FileStream? Hold(string baseDirectory, string home, out string build)
    {
        var folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(baseDirectory));
        build = Path.GetFileName(folder);
        var bin = DaorisHome.Entry(home, HomeEntry.Bin);
        var builds = Path.Combine(bin, Folder);
        if (!BuildName.IsMatch(build) || !string.Equals(Path.GetDirectoryName(folder), Path.GetFullPath(builds), StringComparison.OrdinalIgnoreCase)) return null;
        if (!Plain(bin) || !Plain(builds) || !Plain(folder)) return null;
        try
        {
            // Shared for reading alone, never for deletion: a removal's exclusive open fails, and so does its rename of the folder.
            return new FileStream(Path.Combine(folder, InUse), FileMode.Open, FileAccess.Read, FileShare.Read);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Whether <paramref name="path"/> is absent or a plain folder or file, never a junction or a symbolic link.</summary>
    /// <remarks>
    /// Its attributes are read once: a folder made between two reads would otherwise be absent to one and there to the other. One that
    /// cannot be read is left to the act that touches it, which fails there.
    /// </remarks>
    public static bool Plain(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0;
        }
        catch (Exception unread) when (unread is IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }

    // An entry and everything under it plain: what is removed through is never a link.
    private static bool Whole(string entry)
    {
        if (!Plain(entry)) return false;
        if (!Directory.Exists(entry)) return true;
        return Directory.EnumerateFileSystemEntries(entry).All(Whole);
    }

    private static void Stage(string source, string builds, string build)
    {
        var staging = Path.Combine(builds, Staging + Guid.NewGuid().ToString("N"));
        try
        {
            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                var to = Path.Combine(staging, Path.GetRelativePath(source, file));
                Directory.CreateDirectory(Path.GetDirectoryName(to)!);
                File.Copy(file, to);
            }

            // Every file's bytes checked against the source's at once: the copy's own build is the source's.
            if (RelayBuild.Of(staging) != build) throw new RelayStop("copy");
            File.WriteAllBytes(Path.Combine(staging, InUse), []);
            Directory.Move(staging, Path.Combine(builds, build));
        }
        catch (IOException) when (File.Exists(Path.Combine(builds, build, InUse)))
        {
            // Another --where's move came first: its copy is the same bytes, and is used.
        }
        finally
        {
            if (Directory.Exists(staging)) Delete(staging);
        }
    }

    // A build none holds in use: opened alone and closed, then renamed to a removal name of its own, opened alone again, and only then
    // deleted. A relay that opened its in-use file between the first open and the rename makes the rename fail on Windows; off it a rename
    // ignores an open file, so the second open finds the relay, and the build goes back to its name.
    private static void Remove(string folder, string builds, Action<string>? renamed)
    {
        try
        {
            if (!Unheld(folder)) return;
            var removing = Path.Combine(builds, Removing + Guid.NewGuid().ToString("N"));
            Directory.Move(folder, removing);
            renamed?.Invoke(removing);
            if (Unheld(removing)) Delete(removing);
            else Directory.Move(removing, folder);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Held in use, or held by a scanner: kept, or left under its removal name for the next --where.
        }
    }

    /// <summary>Whether no relay holds the folder's <see cref="InUse"/> file: it opens alone, or is not there.</summary>
    private static bool Unheld(string folder)
    {
        var inUse = Path.Combine(folder, InUse);
        if (!File.Exists(inUse)) return true;
        try
        {
            using var alone = new FileStream(inUse, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void Delete(string folder)
    {
        try
        {
            Directory.Delete(folder, recursive: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A file held open: the next --where deletes what is left.
        }
    }
}
