using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The tree guard imports the module that finds a session's own subagent's worktree, so the module is written beside the guard,
/// before it and as shipped, wherever the guard is installed.
/// </summary>
public sealed class SubagentWorktreeShippedTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-subagent-worktree-" + Guid.NewGuid().ToString("N")[..8]);

    public SubagentWorktreeShippedTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public void The_module_is_written_beside_the_guard_as_shipped_and_put_back_if_changed()
    {
        var guard = TreeGuard.Install(_home);
        var module = Path.Combine(Path.GetDirectoryName(guard)!, TreeGuard.WorktreeScriptName);

        Assert.Equal(TreeGuard.WorktreeSource, File.ReadAllText(module));
        Assert.Contains($"from './{TreeGuard.WorktreeScriptName}'", TreeGuard.Source, StringComparison.Ordinal);

        File.WriteAllText(module, "export const subagentWorktree = () => '/';\n");
        TreeGuard.Install(_home);

        Assert.Equal(TreeGuard.WorktreeSource, File.ReadAllText(module));
        Assert.DoesNotContain("\r", TreeGuard.WorktreeSource);
    }
}
