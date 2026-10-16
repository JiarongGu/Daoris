using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The change watch's fake source (FABRIC-EVENT2, the design §A4.1): what a <see cref="FileSystemWatcher"/> would say, raised
/// by a test, with overflows, losses, a folder absent or replaced, and nothing at all when silenced. Every handle it opened is
/// kept, so a test can raise from an old one and see the watch drop it.
/// </summary>
internal sealed class FakeChangeSource : IChangeSource
{
    private readonly object _gate = new();
    private readonly HashSet<string> _absent = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<FakeChangeHandle> _opened = [];

    /// <summary>Every handle opened, oldest first, disposed ones included.</summary>
    public IReadOnlyList<FakeChangeHandle> Opened
    {
        get
        {
            lock (_gate) return [.. _opened];
        }
    }

    /// <summary>How often an open was asked, absent folders included.</summary>
    public int Asked { get; private set; }

    /// <summary>The handle now open on <paramref name="folder"/>, or null.</summary>
    public FakeChangeHandle? On(string folder)
    {
        lock (_gate) return _opened.LastOrDefault(handle => handle.Folder == folder && !handle.Disposed);
    }

    /// <summary>Whether <paramref name="folder"/> is absent: an open of it answers null.</summary>
    public void Absent(string folder, bool absent = true)
    {
        lock (_gate)
        {
            if (absent) _absent.Add(folder);
            else _absent.Remove(folder);
        }
    }

    public IChangeHandle? Open(string folder, bool subfolders, IChangeSink sink)
    {
        lock (_gate)
        {
            Asked++;
            if (_absent.Contains(folder)) return null;
            var handle = new FakeChangeHandle(folder, sink);
            _opened.Add(handle);
            return handle;
        }
    }
}

/// <summary>One fake watcher on one folder: raise what moved, lose it, or replace the folder under it.</summary>
internal sealed class FakeChangeHandle(string folder, IChangeSink sink) : IChangeHandle
{
    public string Folder { get; } = folder;

    public bool Disposed { get; private set; }

    /// <summary>Whether the folder at the path is still the one opened: false once a test replaced it.</summary>
    public bool Same { get; set; } = true;

    /// <summary>What moved, by its name under the folder, raised whether or not this handle is still the watch's.</summary>
    public void Raise(string name, ChangeKind kind = ChangeKind.Created) => sink.Moved(name, kind);

    /// <summary>The watcher's <c>Error</c>: an overflow, or its handle lost.</summary>
    public void Lose(string why = "overflow") => sink.Lost(why);

    public void Dispose() => Disposed = true;
}
