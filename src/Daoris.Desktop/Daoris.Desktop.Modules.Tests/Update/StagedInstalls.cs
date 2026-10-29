using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A scratch install and a build staged beside it as the publish stages one (UPDATE1): the updater's tests', and the pipe's drain proof's
/// (DRAINHOLD1), which drains a real <see cref="InstallUpdater"/> as the shell wires it.
/// </summary>
internal static class StagedInstalls
{
    /// <summary>An install as the publish leaves one: its record, its launcher and its application.</summary>
    public static void Prepare(string install)
    {
        Write(install, "INSTALLED.md", "# Daoris — installed desktop\n");
        Write(install, "Daoris.exe", "old launcher");
        Write(install, "app/Daoris.Desktop.exe", "old application");
        Write(install, "app/Daoris.Desktop.App.dll", "old library");
    }

    /// <summary>A build staged as the publish stages one, consistent with its manifest.</summary>
    public static void Stage(string install, string id)
    {
        var staged = Path.Combine(install, "update", "staged");
        if (Directory.Exists(staged)) Directory.Delete(staged, recursive: true);
        Write(install, "update/staged/INSTALLED.md", "# Daoris — installed desktop\nnew\n");
        Write(install, "update/staged/Daoris.exe", "new launcher");
        Write(install, "update/staged/app/Daoris.Desktop.exe", "new application");
        Write(install, "update/staged/app/Daoris.Desktop.App.dll", "new library");
        var files = new JsonArray();
        foreach (var full in Directory.EnumerateFiles(staged, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            files.Add(new JsonObject
            {
                ["path"] = Path.GetRelativePath(staged, full).Replace(Path.DirectorySeparatorChar, '/'),
                ["size"] = new FileInfo(full).Length,
                ["sha256"] = StagedBuild.Sha256Of(full),
            });
        }

        File.WriteAllText(Path.Combine(staged, "build.json"), new JsonObject
        {
            ["schema"] = 1, ["id"] = id, ["version"] = "0.0.1", ["commit"] = "abc1234", ["at"] = "2026-10-03T12:00:00Z", ["files"] = files,
        }.ToJsonString());
    }

    /// <summary>A file of the install, by its install-relative path.</summary>
    public static void Write(string install, string relative, string text)
    {
        var path = Path.Combine(install, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }
}
