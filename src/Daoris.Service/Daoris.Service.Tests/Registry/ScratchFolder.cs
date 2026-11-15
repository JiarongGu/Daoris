namespace Daoris.Service.Tests;

/// <summary>A scratch folder of its own for one test, written file by file and deleted whatever git made read-only.</summary>
public abstract class ScratchFolder(string prefix) : IDisposable
{
    protected readonly string _root = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose() => GitCheckouts.Delete(_root);

    protected string Write(string relative, string content)
    {
        var file = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, content);
        return file;
    }
}
