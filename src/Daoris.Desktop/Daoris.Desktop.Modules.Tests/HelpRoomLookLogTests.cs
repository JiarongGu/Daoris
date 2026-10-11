using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>The room's session look timed into the machine log (SESSUX1h3b, D161's note, LOG1a/D94): a duration, never words.</summary>
public sealed class HelpRoomLookLogTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-roomlook-" + Guid.NewGuid().ToString("N")[..8]);

    public HelpRoomLookLogTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string[] Lines()
    {
        var folder = Path.Combine(_home, MachineLog.Folder);
        if (!Directory.Exists(folder)) return [];
        return Directory.GetFiles(folder).SelectMany(path =>
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        }).ToArray();
    }

    [Fact]
    public void A_look_within_the_bound_is_an_info_line_with_its_milliseconds()
    {
        using var log = new MachineLog(_home, "driver");
        HelpRoomLookLog.Said(log, TimeSpan.FromMilliseconds(120), read: true);

        var line = Assert.Single(Lines());
        Assert.Contains("\"level\":\"info\",\"event\":\"help.room.look\"", line);
        Assert.Contains("\"ms\":120", line);
        Assert.Contains("\"slow\":false", line);
        Assert.Contains("\"read\":true", line);
    }

    [Fact]
    public void A_look_over_the_bound_is_a_warning_that_says_slow_and_the_bound()
    {
        using var log = new MachineLog(_home, "driver");
        HelpRoomLookLog.Said(log, TimeSpan.FromMilliseconds(HelpRoomLookLog.SlowMs + 1), read: false);

        var line = Assert.Single(Lines());
        Assert.Contains("\"level\":\"warn\",\"event\":\"help.room.look\"", line);
        Assert.Contains("\"slow\":true", line);
        Assert.Contains($"\"boundMs\":{HelpRoomLookLog.SlowMs}", line);
        Assert.Contains("\"read\":false", line);
    }
}
