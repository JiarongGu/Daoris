using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The MinGit the built-in resource list pins: fetched once into a folder of the repository's
/// <c>_fixtures/</c> by the toolchain's own download, which holds the archive to the list's size and SHA-256 and refuses it otherwise, and
/// named as a test home's git (<see cref="Lay"/>) so the doors' classes run with it beside the runner's git.
/// </summary>
/// <remarks>
/// The fetch is the one a person's <c>daoris tool download git</c> makes, never a second reader of the list, and a version already there
/// is not fetched again. Nothing of the person's home or <c>PATH</c> is read or written; a Windows machine alone has the pin.
/// </remarks>
internal static class PinnedMinGit
{
    private static readonly Lazy<string> Fetched = new(Fetch);

    private static string ToolsHome => Path.Combine(WorkspaceRoot.Folder, "_fixtures", "pinned-mingit", "home");

    /// <summary>The pinned git's launcher (<c>cmd/git.exe</c>), fetched and verified on first use.</summary>
    public static string Exe => Fetched.Value;

    /// <summary>Name the pinned git as <paramref name="home"/>'s git, as the person's Tools page does a file they choose.</summary>
    public static void Lay(string home)
    {
        Directory.CreateDirectory(home);
        Tools.UseFile(home, "git", Exe);
    }

    private static string Fetch()
    {
        var list = ToolResources.ReadFile(ToolResources.BesideApplication(AppContext.BaseDirectory), ToolResources.BuiltIn);
        var platform = ToolResources.Current ?? throw new InvalidOperationException("this machine's platform has no pinned MinGit");
        var merged = ToolResources.Merge([list], platform);
        var offered = merged.Tools.First(tool => tool.Tool == "git").Versions.FirstOrDefault(version => version.Archive == "zip")
            ?? throw new InvalidOperationException("the built-in list pins no MinGit for " + platform);
        return ToolInstall.DownloadAsync(ToolsHome, "git", offered, platform, _ => { }).GetAwaiter().GetResult();
    }
}
