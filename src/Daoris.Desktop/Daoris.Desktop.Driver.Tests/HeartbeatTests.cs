namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The heartbeat a real child rewrites is read beside its writer's open handle (<see cref="Heartbeat"/>; TEST4, PROCPAR1). No
/// process here: the writer is a handle this test holds.
/// </summary>
public sealed class HeartbeatTests : IDisposable
{
    // Scratch in the repository's own gitignored `_fixtures/`, never OS temp.
    private readonly string _folder = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "heartbeat", Guid.NewGuid().ToString("N")[..8]);

    public HeartbeatTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public async Task A_heartbeat_is_read_while_its_writer_holds_it_open()
    {
        var heartbeat = Path.Combine(_folder, "heartbeat.txt");
        File.WriteAllText(heartbeat, "1760000000000");

        using (new FileStream(heartbeat, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
        {
            Assert.Equal("1760000000000", await Heartbeat.ReadAsync(heartbeat));
        }
    }

    [Fact]
    public async Task No_heartbeat_yet_is_none_and_does_not_beat()
    {
        var missing = Path.Combine(_folder, "missing.txt");

        Assert.Null(await Heartbeat.ReadAsync(missing));
        Assert.False(await Heartbeat.Beating(missing));
    }
}
