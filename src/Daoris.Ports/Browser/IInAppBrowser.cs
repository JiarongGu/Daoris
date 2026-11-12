namespace Daoris.Ports.Browser;

/// <summary>
/// The browser a session drives: the engine's endpoint, and showing a page in the window's own browser. The host answers it from the
/// shell that owns the engine; where none is, the endpoint is null.
/// </summary>
public interface IInAppBrowser
{
    /// <summary>Starts the engine if it is not running and answers its debugging endpoint, or null where there is none.</summary>
    Task<string?> EnsureAsync(CancellationToken ct = default);

    /// <summary>The endpoint of the engine as it runs, started on first ask.</summary>
    Task<string?> EndpointAsync(CancellationToken ct = default) => EnsureAsync(ct);

    /// <summary>Brings the browser's pane to the front.</summary>
    void Show();

    /// <summary>Opens <paramref name="address"/> in the browser's pane.</summary>
    void Open(string address);
}

/// <summary>The browser's caches, which are cleared only while the browser is stopped.</summary>
public interface IBrowserCaches
{
    /// <summary>
    /// Runs <paramref name="clear"/> while the browser is stopped and answers what it answered, or null where the browser could not
    /// be stopped.
    /// </summary>
    Task<T?> WhileStoppedAsync<T>(Func<T> clear, CancellationToken ct) where T : class;
}
