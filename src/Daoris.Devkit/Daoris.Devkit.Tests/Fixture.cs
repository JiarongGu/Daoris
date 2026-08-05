namespace Daoris.Devkit.Tests;

/// <summary>A scratch repository for one test, under the devkit's own gitignored `_fixtures/`.</summary>
/// <remarks>
/// <b>Not OS temp</b>, deliberately. `no-tmp-for-repo-files` is one of this project's own canonical
/// rules: scratch belongs in a gitignored directory inside the repository, where it is visible to
/// whoever is looking at the workspace and cannot be orphaned somewhere nobody thinks to look.
///
/// Following it here rather than copying the service's tests, which reach for `Path.GetTempPath()` and
/// predate anyone noticing.
/// </remarks>
public sealed class Fixture : IDisposable
{
    private static readonly string Root = FindFixtureRoot();

    public string Path { get; }

    public Fixture(string label)
    {
        Path = System.IO.Path.Combine(Root, $"{label}-{Guid.NewGuid().ToString("N")[..8]}");
        Directory.CreateDirectory(Path);
    }

    /// <summary>Write a file, creating whatever directories it needs. Repository-relative, '/'-separated.</summary>
    public void Write(string relative, string content)
    {
        var file = Absolute(relative);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file)!);
        File.WriteAllText(file, content);
    }

    public void WriteBytes(string relative, byte[] content)
    {
        var file = Absolute(relative);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file)!);
        File.WriteAllBytes(file, content);
    }

    public string Absolute(string relative) =>
        System.IO.Path.Combine(Path, relative.Replace('/', System.IO.Path.DirectorySeparatorChar));

    public void Dispose()
    {
        if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
    }

    /// <summary>
    /// Walk up to the test project, then place `_fixtures/` beside it — the same place the CLI's tests
    /// put theirs, so there is one answer to "where does scratch go" across both languages.
    /// </summary>
    private static string FindFixtureRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (directory.GetFiles("*.csproj").Length > 0)
            {
                var root = System.IO.Path.Combine(directory.FullName, "_fixtures");
                Directory.CreateDirectory(root);
                return root;
            }
        }

        throw new InvalidOperationException("could not locate the test project directory");
    }
}
