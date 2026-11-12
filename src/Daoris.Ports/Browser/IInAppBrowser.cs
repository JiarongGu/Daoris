namespace Daoris.Ports.Browser;

/// <summary>
/// Daoris's own browser, as the application sees it: a window of the shell that sessions drive over
/// the Chrome DevTools Protocol. The shell implements it; a host with no shell has none.
/// </summary>
public interface IInAppBrowser
{
    /// <summary>
    /// Bring the browser up without taking the person's focus, and answer the loopback CDP endpoint a
    /// server attaches to — null where there is no browser to bring up.
    /// </summary>
    Task<string?> EnsureAsync(CancellationToken ct = default);

    /// <summary>
    /// The endpoint a session's servers are handed, answered without bringing the browser up where the host can
    /// hold it: the browser then starts at the first connection to the endpoint. A host that cannot defer brings it up, as
    /// <see cref="EnsureAsync"/> does.
    /// </summary>
    Task<string?> EndpointAsync(CancellationToken ct = default) => EnsureAsync(ct);

    /// <summary>Open it for the person, in front: the address bar, their sign-in, and what an agent does there.</summary>
    void Show();

    /// <summary>
    /// Open a page in it for the person, in front, as a tab of its own: a link on Daoris's page,
    /// where the person chose that links open here. <paramref name="address"/> is already a web page's.
    /// </summary>
    void Open(string address);
}

/// <summary>
/// The shell's word on Daoris's own browser for clearing its caches: the shell's browser host, handed to the application as it hands
/// the in-app browser (<see cref="IInAppBrowser"/>). Only the shell can say whether its browser runs, and only it can hold the
/// browser's start, so a terminal acting alone, with no shell to ask, keeps the caches.
/// </summary>
public interface IBrowserCaches
{
    /// <summary>
    /// Run <paramref name="clear"/> while Daoris's own browser is not running, holding the host's start gate through it so none starts
    /// meanwhile; null, with nothing run, where the browser runs.
    /// </summary>
    Task<T?> WhileStoppedAsync<T>(Func<T> clear, CancellationToken ct) where T : class;
}
