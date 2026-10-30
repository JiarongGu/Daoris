using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// This suite's own copy of the knowledge server (<c>daoris-knowledge</c>), built on first use under this project's <c>obj/knowledge-host</c>,
/// so a process test never depends on the service gate having built it first and never starts it from the service tree's <c>bin</c>
/// (the same shape as <see cref="RealServiceHost.HostDllAsync"/>).
/// </summary>
internal static class RealKnowledgeServer
{
    private static readonly SemaphoreSlim Building = new(1, 1);
    private static string? _built;

    internal static string Executable()
    {
        Building.Wait();
        try
        {
            if (_built is not null && File.Exists(_built)) return _built;

            // This test's build is <desktop>/<tests>/bin/<configuration>/<framework>/.
            var build = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var configuration = build.Parent!.Name;
            var tests = build.Parent.Parent!.Parent!;
            var project = Path.Combine(tests.Parent!.Parent!.FullName, "Daoris.Service", "Daoris.Service.Mcp", "Daoris.Service.Mcp.csproj");
            var artifacts = Path.Combine(tests.FullName, "obj", "knowledge-host");
            RealServiceHost.BuildAsync(project, configuration, artifacts).GetAwaiter().GetResult();

            var bin = Path.Combine(artifacts, "bin");
            var found = Directory.Exists(bin)
                ? Directory.GetFiles(bin, KnowledgeConnector.ExecutableName, SearchOption.AllDirectories)
                : [];
            _built = found.Length == 1
                ? found[0]
                : throw new FileNotFoundException($"the build of the knowledge server under {artifacts} left {found.Length} copies of {KnowledgeConnector.ExecutableName}, not one");
            return _built;
        }
        finally
        {
            Building.Release();
        }
    }
}
