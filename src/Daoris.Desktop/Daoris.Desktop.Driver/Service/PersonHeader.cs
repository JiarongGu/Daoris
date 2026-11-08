namespace Daoris.Driver;

/// <summary>
/// Presents the person key the shell minted for its host on a client's requests. The header and the rule are the host's: a twin of its
/// <c>PersonKey.Header</c>, spelled again on purpose since the artefacts share no code.
/// </summary>
public static class PersonHeader
{
    /// <summary>The header the host reads the key in.</summary>
    public const string Name = "Daoris-Person";

    /// <summary>
    /// Sets the key on every request of <paramref name="http"/> when <paramref name="baseUrl"/> is this machine's own and a key is
    /// held. An address that names a remote never carries it: a service address is configuration, and the key is worth only to the
    /// host this window started.
    /// </summary>
    public static void Present(HttpClient http, string baseUrl, string? key)
    {
        if (string.IsNullOrWhiteSpace(key) || !RunningService.IsLoopback(baseUrl)) return;
        http.DefaultRequestHeaders.Remove(Name);
        http.DefaultRequestHeaders.Add(Name, key.Trim());
    }
}
