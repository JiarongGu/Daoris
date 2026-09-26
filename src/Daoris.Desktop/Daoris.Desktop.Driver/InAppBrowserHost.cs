namespace Daoris.Driver;

/// <summary>
/// Daoris's own browser, as the driver sees it (D78): a window of the shell that sessions drive over
/// the Chrome DevTools Protocol. The shell implements it; a host with no shell has none.
/// </summary>
public interface IInAppBrowser
{
    /// <summary>
    /// Bring the browser up without taking the person's focus, and answer the loopback CDP endpoint a
    /// server attaches to — null where there is no browser to bring up.
    /// </summary>
    Task<string?> EnsureAsync(CancellationToken ct = default);

    /// <summary>Open it for the person, in front: the address bar, their sign-in, and what an agent does there.</summary>
    void Show();
}
