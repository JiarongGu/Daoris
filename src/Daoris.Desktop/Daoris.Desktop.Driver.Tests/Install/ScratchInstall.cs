using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A scratch install holding the build before (its launcher, its application, its record and the person's data), with what
/// stages a build beside it as the publish does and reads what a swap left in it.
/// </summary>
public abstract class ScratchInstall : IDisposable
{
    protected readonly string _install = Path.Combine(TestHomes.Root, "daoris-staged-" + Guid.NewGuid().ToString("N")[..8]);

    protected ScratchInstall()
    {
        Write("INSTALLED.md", "# Daoris — installed desktop\nold\n");
        Write("Daoris.exe", "old launcher");
        Write("app/Daoris.Desktop.exe", "old application");
        Write("app/Daoris.Desktop.App.dll", "old library");
        Write("data/driver.json", "{ \"drivable\": [] }\n");
    }

    public void Dispose()
    {
        try { Directory.Delete(_install, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        GC.SuppressFinalize(this);
    }

    protected void AssertTheBuildBefore()
    {
        Assert.Equal("old launcher", Read("Daoris.exe"));
        Assert.Equal("old application", Read("app/Daoris.Desktop.exe"));
        Assert.Equal("old library", Read("app/Daoris.Desktop.App.dll"));
        Assert.Equal("# Daoris — installed desktop\nold\n", Read("INSTALLED.md"));
        Assert.Equal("{ \"drivable\": [] }\n", Read("data/driver.json"));
    }

    /// <summary>A build staged as the publish stages one: the four files every build carries, and its manifest.</summary>
    protected void Stage()
    {
        Write("update/staged/INSTALLED.md", "# Daoris — installed desktop\nnew\n");
        Write("update/staged/Daoris.exe", "new launcher");
        Write("update/staged/app/Daoris.Desktop.exe", "new application");
        Write("update/staged/app/Daoris.Desktop.App.dll", "new library");
        Manifest();
    }

    /// <summary>The manifest of whatever the staged folder holds now, as the publish writes it.</summary>
    protected void Manifest()
    {
        var staged = Path.Combine(_install, "update", "staged");
        var files = new JsonArray();
        foreach (var full in Directory.EnumerateFiles(staged, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var relative = Path.GetRelativePath(staged, full).Replace(Path.DirectorySeparatorChar, '/');
            if (relative == "build.json") continue;
            files.Add(new JsonObject
            {
                ["path"] = relative,
                ["size"] = new FileInfo(full).Length,
                ["sha256"] = StagedBuild.Sha256Of(full),
            });
        }

        File.WriteAllText(Path.Combine(staged, "build.json"), new JsonObject
        {
            ["schema"] = 1,
            ["id"] = "b1",
            ["version"] = "0.0.1",
            ["commit"] = "abc1234",
            ["at"] = "2026-10-03T12:00:00Z",
            ["files"] = files,
        }.ToJsonString());
    }

    protected string Staged(string relative) => Path.Combine(_install, "update", "staged", relative.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>Whether a full path a move is handed is this install-relative one.</summary>
    protected bool Is(string full, string relative) =>
        string.Equals(full, Path.Combine(_install, relative.Replace('/', Path.DirectorySeparatorChar)), StringComparison.OrdinalIgnoreCase);

    /// <summary>What Windows answers a move of a folder a scanner holds a file in: access denied.</summary>
    protected static UnauthorizedAccessException Refused() => new("Access to the path is denied.");

    protected string Read(string relative) => File.ReadAllText(Path.Combine(_install, relative.Replace('/', Path.DirectorySeparatorChar)));

    protected void Write(string relative, string text)
    {
        var path = Path.Combine(_install, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }
}

/// <summary>Thrown by a test's sleep to stop a swap where a launcher would have died: waiting for its application.</summary>
internal sealed class Stop : Exception;
