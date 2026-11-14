using Daoris.Ports.Repositories;

namespace Daoris.Testing.Tests;

/// <summary>The repository port's contract on the memory fake, the twin of the driver's real-git class that runs the same cases.</summary>
public sealed class MemoryRepositoryContractTests : RepositoryContract
{
    private MemoryRepository _repository = null!;

    private readonly MemoryRepositories _host = MemoryRepositories.New();

    protected override IGitRepository Backend => _host.Port;

    protected override bool CaseIgnored => false;

    protected override Task<string> NewAsync()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "memory", Guid.NewGuid().ToString("N")[..8]));
        _repository = _host.Add(new MemoryRepository(root));
        return Task.FromResult(root);
    }

    protected override Task WriteAsync(string folder, string path, string content)
    {
        _repository.Write(folder, path, content);
        return Task.CompletedTask;
    }

    protected override Task RemoveAsync(string folder, string path)
    {
        _repository.Remove(folder, path);
        return Task.CompletedTask;
    }

    protected override Task HideAsync(string folder, string path)
    {
        _repository.Hide(folder, path);
        return Task.CompletedTask;
    }

    protected override Task VanishAsync(string folder)
    {
        _repository.Vanish(folder);
        return Task.CompletedTask;
    }

    protected override string Beside(string root, string name) => Path.Combine(Path.GetDirectoryName(root)!, Path.GetFileName(root) + "-" + name);
}
