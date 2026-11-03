namespace Daoris.Driver;

/// <summary>What wiring a workspace did: the workspace and the address as stored, and the address it replaced where it was wired already.</summary>
public sealed record RemoteSet(string Workspace, string Url, string Key, string? Replacing);

/// <summary>
/// The machine's wiring, edited (ONEWAY1u, D194 point 4): the one operation per change that the window's Remotes screen and the
/// terminal's <c>remote</c> verb both call, over the home's <c>remotes.json</c> as <see cref="RemoteFile"/> reads it. It talks to
/// nothing: validating an address by calling it would need the deployment up before a machine could be wired for it.
/// </summary>
public static partial class RemoteOperations
{
    [System.Text.RegularExpressions.GeneratedRegex("^https?://[^/]", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex Scheme();

    /// <summary>
    /// <paramref name="workspace"/> wired to <paramref name="url"/> with <paramref name="key"/>, made onto the file as it stands
    /// (KEEPALL1e): what the operation does not own stays. A workspace is one however its case is spelled.
    /// </summary>
    /// <param name="afterRead">Called between the read and the write: a test's place to make another writer's change.</param>
    /// <exception cref="HomeFileRefusal">An address or a key missing, or an address that is not http or https; nothing was written.</exception>
    /// <exception cref="DriverException">The file there now does not read or holds a key twice; nothing was written.</exception>
    public static RemoteSet Set(string path, string workspace, string url, string key, Action? afterRead = null)
    {
        var name = RemoteTarget.Workspace(workspace);
        var address = url.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(address) || string.IsNullOrWhiteSpace(key))
        {
            // Half a pair is no remote in every loader (D48 §5): refused rather than written, so the file never holds an entry that does nothing.
            throw new HomeFileRefusal(
                HomeFileRefusal.HalfDeclared,
                $"`{name}` needs both an address and a key: a half-declared remote is no remote, and one written into the map would simply be skipped.",
                ("workspace", name));
        }

        // An address with no scheme parses as a scheme of its own, and every sync with it failed at the service's door (REV3).
        if (!Scheme().IsMatch(url))
        {
            throw new HomeFileRefusal(
                HomeFileRefusal.NotAnAddress,
                $"`{url}` is not an address a deployment answers at — one starts http:// or https://, e.g. `--url https://team.example.com`.",
                ("url", url));
        }

        var file = RemoteFile.Load(path);
        var replacing = file.Remotes.TryGetValue(name, out var held) ? held.Url : null;
        afterRead?.Invoke();
        file.With(name, new RemoteTarget(address, key)).Save(path);
        return new RemoteSet(name, address, key, replacing);
    }

    /// <summary>
    /// <paramref name="workspace"/> no longer wired: false where it was not, which is an answer and not an error, and writes nothing.
    /// </summary>
    /// <exception cref="DriverException">The file there now does not read or holds a key twice; nothing was written.</exception>
    public static bool Remove(string path, string workspace, Action? afterRead = null)
    {
        var name = RemoteTarget.Workspace(workspace);
        var file = RemoteFile.Load(path);
        var held = file.Remotes.ContainsKey(name);
        afterRead?.Invoke();
        if (!held) return false;
        file.Without(name).Save(path);
        return true;
    }
}
