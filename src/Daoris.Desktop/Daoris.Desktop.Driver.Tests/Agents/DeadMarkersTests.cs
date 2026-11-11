using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A session's process marker outlives its process only until the sweep: a marker naming a process that is gone, of a session no longer
/// active, is removed; one whose process runs, one of a session still active (an opening's), and a file that is no marker stay.
/// </summary>
public sealed class DeadMarkersTests : IDisposable
{
    private readonly string _markers = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "dead-markers", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose() => GitSeed.Discard(_markers);

    private string Put(string name, string text)
    {
        Directory.CreateDirectory(_markers);
        var path = Path.Combine(_markers, name);
        File.WriteAllText(path, text);
        return path;
    }

    [Fact]
    public void A_marker_of_a_gone_process_of_a_finished_session_is_removed_and_no_other()
    {
        using var self = Process.GetCurrentProcess();
        var gone = Put("s-gone.pid", $"{int.MaxValue - 1} {DateTime.UtcNow.Ticks}");
        var alive = Put("s-alive.pid", $"{self.Id} {self.StartTime.ToUniversalTime().Ticks}");
        var active = Put("s-active.pid", $"{int.MaxValue - 1} {DateTime.UtcNow.Ticks}");
        var other = Put("notes.txt", "not a marker");

        var removed = DeadMarkers.Sweep(_markers, new HashSet<string>(["s-active"], StringComparer.Ordinal));

        Assert.Equal(1, removed);
        Assert.False(File.Exists(gone));
        Assert.True(File.Exists(alive));
        Assert.True(File.Exists(active));
        Assert.True(File.Exists(other));
    }

    [Fact]
    public void A_home_with_no_markers_is_swept_to_nothing()
    {
        Assert.Equal(0, DeadMarkers.Sweep(_markers, new HashSet<string>(StringComparer.Ordinal)));
    }
}
