namespace Daoris.Driver;

/// <summary>Participants supplies one per-run stream watcher; Agents forwards each line before mapping it.</summary>
public interface ILineageStreams
{
    Action<string> Watch(string home, string sessionId);
}

/// <summary>The required watcher operation, installed by Participants when the driver assembly loads.</summary>
public static class LineageStreams
{
    private static ILineageStreams? _current;

    public static ILineageStreams Current
    {
        get => _current ?? throw new InvalidOperationException("no lineage streams are installed: Participants installs them when the driver loads");
        set => _current = value;
    }
}
