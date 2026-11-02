namespace Daoris.Desktop.Driver.Tests;

/// <summary>A Daoris home of a test's own, under the workspace's <c>_fixtures</c> and gone with the test.</summary>
public abstract class TestHome : IDisposable
{
    protected string Home { get; } = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "test-homes", Guid.NewGuid().ToString("N")[..8]);

    protected TestHome() => Directory.CreateDirectory(Home);

    public void Dispose()
    {
        GitSeed.Discard(Home);
        GC.SuppressFinalize(this);
    }
}
