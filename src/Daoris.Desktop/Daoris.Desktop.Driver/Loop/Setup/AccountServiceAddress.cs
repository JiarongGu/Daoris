namespace Daoris.Driver;

/// <summary>
/// *Change the service's address* (WIRING1e, the this-Daoris design §4): <b>the account's</b> <c>DAORIS_SERVICE_URL</c> set to an
/// address of this machine, or cleared for the default. It takes effect when Daoris starts again, and a terminal reads it too.
/// </summary>
/// <remarks>
/// Only this machine's own address is taken: the local service is sent this machine's paths, which no other host is (D46, D171's note).
/// A refusal writes nothing.
/// </remarks>
public static class AccountServiceAddress
{
    /// <summary>The address as it would be stored, or why it is refused: <c>SERVICE_MALFORMED</c> or <c>SERVICE_NOT_LOOPBACK</c>.</summary>
    public static (string? Url, SetupResult? Refusal) Judge(string? asked)
    {
        if (asked is null) return (null, null);
        var url = asked.Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var address)
            || address.Scheme is not ("http" or "https")
            || string.IsNullOrEmpty(address.Host)
            || address.UserInfo.Length > 0
            || address.Query.Length > 0
            || address.Fragment.Length > 0)
        {
            return (null, SetupResult.Refused("SERVICE_MALFORMED", "The service's address must be http:// or https:// with a host and a port, and nothing else."));
        }

        return RunningService.IsLoopback(url)
            ? (url.TrimEnd('/'), null)
            : (null, SetupResult.Refused("SERVICE_NOT_LOOPBACK", "The local service is sent this machine's paths, so its address must be this machine's own: localhost, 127.0.0.1 or [::1]."));
    }

    /// <param name="account">The account's variables; null where the machine keeps none (off Windows).</param>
    /// <param name="asked">The address, or null for the default again.</param>
    public static SetupResult Set(IAccountEnvironment? account, string? asked)
    {
        var (url, refusal) = Judge(asked);
        if (refusal is not null) return refusal;
        if (account is null) return SetupResult.Refused("SERVICE_NOT_WINDOWS","This machine keeps no account variables to change: only Windows does.");

        string? before;
        try
        {
            before = account.Read(ServiceClient.UrlVariable);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            return SetupResult.Refused("SERVICE_UNREADABLE", "Your account's variables could not be read, so nothing was written.");
        }

        if (string.Equals(before, url, StringComparison.Ordinal)) return SetupResult.Did(changed: false);
        try
        {
            account.Write(ServiceClient.UrlVariable, url);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            return SetupResult.Refused("SERVICE_NOT_WRITTEN", "Your account's variables could not be written, so they are as they were.");
        }

        return SetupResult.Did(changed: true);
    }
}
