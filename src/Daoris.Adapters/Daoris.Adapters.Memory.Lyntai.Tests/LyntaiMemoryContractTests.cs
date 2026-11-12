using Daoris.Ports.Memory;
using Daoris.Testing;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Daoris.Adapters.Memory.Lyntai.Tests;

/// <summary>The memory's contract on Lyntai's engines, each case over a file of its own under the build's output.</summary>
[Trait("Category", "Needs")]
[Trait("Needs", "filesystem")]
public sealed class LyntaiMemoryContractTests : MemoryContract, IDisposable
{
    private readonly string _folder = Path.Combine(AppContext.BaseDirectory, "memory-contract", Guid.NewGuid().ToString("N")[..8]);

    protected override IMemoryEngines Memory(string repository)
    {
        Directory.CreateDirectory(_folder);
        return new LyntaiMemory(Path.Combine(_folder, LyntaiMemory.FileName));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
            // A file still open for a moment: the next build's clean takes it.
        }
    }
}
