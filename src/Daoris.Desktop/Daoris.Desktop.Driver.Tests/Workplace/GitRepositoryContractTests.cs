using Daoris.Driver;
using Daoris.Ports.Repositories;
using Daoris.Testing;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The repository port's contract on real git, the side <see cref="MemoryRepository"/>'s twin is held to: the driver's own
/// <see cref="GitRepository"/> over its git funnel, on a scratch repository of each case's own. Real git runs here, so the class is a Process class.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class GitRepositoryContractTests : RepositoryContract, IDisposable
{
    private readonly string _scratch = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "repository-contract", Guid.NewGuid().ToString("N")[..8]);

    protected override IGitRepository Backend => GitRepository.Shared;

    protected override async Task<string> NewAsync()
    {
        var root = Path.Combine(_scratch, "repository");
        Directory.CreateDirectory(root);
        await GitSeed.GitAsync(root, "init", "--quiet", "-b", "main");
        await GitSeed.GitAsync(root, "config", "user.email", "fixture@example.test");
        await GitSeed.GitAsync(root, "config", "user.name", "Fixture");
        await GitSeed.NoMaintenanceAsync(root);
        return root;
    }

    protected override async Task WriteAsync(string folder, string path, string content)
    {
        var file = Path.Combine(folder, path);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        await File.WriteAllTextAsync(file, content);
    }

    protected override Task RemoveAsync(string folder, string path)
    {
        File.Delete(Path.Combine(folder, path));
        return Task.CompletedTask;
    }

    protected override async Task HideAsync(string folder, string path) => await GitSeed.GitAsync(folder, "update-index", "--assume-unchanged", path);

    protected override Task VanishAsync(string folder)
    {
        GitSeed.Remove(folder);
        return Task.CompletedTask;
    }

    protected override string Beside(string root, string name) => Path.Combine(Path.GetDirectoryName(root)!, name);

    public void Dispose() => GitSeed.Discard(_scratch);
}
