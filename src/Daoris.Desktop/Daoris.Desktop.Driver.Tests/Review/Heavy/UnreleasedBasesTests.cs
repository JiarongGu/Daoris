using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A heavy run's admission names the bases its tree holds unreleased, and refuses nothing; a tree with none is not named.</summary>
public sealed class UnreleasedBasesTests : IDisposable
{
    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "unreleased-bases", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose() => TreeSetAside.DeleteNeverFollowing(_root);

    [Fact]
    public void The_admission_line_names_each_unreleased_base_and_says_the_run_refuses_nothing()
    {
        var home = Path.Combine(_root, "home");
        var tree = Path.Combine(_root, "engine");
        Directory.CreateDirectory(home);
        Assert.Null(UnreleasedBases.Line(home, tree));

        TreeFilesRecord.Change(home, tree, state => state with
        {
            Bases = [new TreeBase("bin/Debug/", "s-aaaa0001", "abc", DateTimeOffset.UtcNow, "Engine.csproj", "Debug"), new TreeBase("bin/Release/", "s-aaaa0001", "abc", DateTimeOffset.UtcNow, "Engine.csproj", "Release")],
        });

        var line = UnreleasedBases.Line(home, tree + Path.DirectorySeparatorChar);
        Assert.StartsWith("run-heavy: `bin/Debug/`, `bin/Release/` are the previous line's build, unreleased", line);
        Assert.Contains("refuses nothing", line);
        Assert.Contains("daoris tree build <path>", line);
    }
}
