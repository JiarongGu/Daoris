using Daoris.Ports.Memory;

namespace Daoris.Testing.Tests;

/// <summary>The memory's contract on the stand-in, in the unit tier.</summary>
public sealed class StandInMemoryContractTests : MemoryContract
{
    protected override IMemoryEngines Memory(string repository)
    {
        var memory = new StandInMemory();
        memory.Engine(repository);
        return memory;
    }
}
