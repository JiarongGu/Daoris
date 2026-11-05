using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>The doors' build beside the tests' own, laid out as an install's <c>app/bin/</c>: one program, two names.</summary>
internal static class GitDoorsLaid
{
    /// <summary>Copy the built doors under <paramref name="install"/>, which holds the install's marker.</summary>
    public static void Lay(string install)
    {
        var build = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var desktop = build.Parent!.Parent!.Parent!.Parent!.FullName;
        var built = Path.Combine(desktop, "Daoris.Desktop.GitDoor", "bin", build.Parent.Name, build.Name);
        Assert.True(File.Exists(Path.Combine(built, GitWiring.SshDoor)), "the doors are built before these tests: " + built);
        var bin = Path.Combine([install, .. Tools.InstallBinLayout]);
        Directory.CreateDirectory(bin);
        foreach (var file in Directory.EnumerateFiles(built)) File.Copy(file, Path.Combine(bin, Path.GetFileName(file)), overwrite: true);
        File.Copy(Path.Combine(built, GitWiring.SshDoor), Path.Combine(bin, GitWiring.RemoteHelper), overwrite: true);
    }
}
