using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>The machine's free memory as the admission reads it on Linux: <c>/proc/meminfo</c>, its commit only under strict overcommit.</summary>
public sealed class MachineMemoryTests
{
    private const string Meminfo = "MemTotal:       32000000 kB\nMemAvailable:    8000000 kB\nCommitLimit:    20000000 kB\nCommitted_AS:   18000000 kB\n";

    [Fact]
    public void Available_memory_is_read_and_the_commit_only_under_strict_overcommit()
    {
        Assert.Equal(new MemoryReading(8000000L * 1024, null, 32000000L * 1024), MachineMemory.Meminfo(Meminfo, strict: false));
        Assert.Equal(new MemoryReading(8000000L * 1024, 2000000L * 1024, 32000000L * 1024), MachineMemory.Meminfo(Meminfo, strict: true));
    }

    /// <summary>🔴 An overcommit mode that cannot be read is not strict, and never loses the available memory read beside it.</summary>
    [Fact]
    public void An_overcommit_mode_that_cannot_be_read_is_not_strict()
    {
        Assert.False(MachineMemory.Strict(() => throw new UnauthorizedAccessException("denied")));
        Assert.False(MachineMemory.Strict(() => throw new IOException("gone")));
        Assert.True(MachineMemory.Strict(() => "2\n"));
        Assert.False(MachineMemory.Strict(() => "0\n"));
    }
}
