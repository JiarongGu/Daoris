using System.Net;
using System.Security.Cryptography;

namespace Daoris.Driver;

/// <summary>
/// How the driver downloads a binary: one client, one fetch with its words, one streaming hash. The tool
/// installer and the Claude release download each carried a copy (DEDUP8); each still chooses its own
/// exception, so a refusal reads as that caller's kind.
/// </summary>
internal static class Downloads
{
    /// <summary>
    /// A client with the "daoris" user agent and no timeout: a 200 MB download outlives any fixed one, and
    /// the person's stop is the token. <paramref name="redirects"/> false follows none on its own.
    /// </summary>
    public static HttpClient Client(HttpMessageHandler? transport = null, bool redirects = true)
    {
        var http = transport is null
            ? new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = redirects }) { Timeout = Timeout.InfiniteTimeSpan }
            : new HttpClient(transport, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("daoris");
        return http;
    }

    /// <summary>
    /// The response at an address, or null for a 404 — "that is not there" is an answer the caller words.
    /// An unreachable host and any other non-success throw <paramref name="refuse"/>'s exception. With
    /// <paramref name="passRedirects"/>, a redirect with a location is returned for the caller to follow.
    /// </summary>
    public static async Task<HttpResponseMessage?> GetAsync(
        HttpClient http, string url, CancellationToken ct, Func<string, Exception> refuse, bool passRedirects = false)
    {
        HttpResponseMessage response;
        try
        {
            response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException error)
        {
            throw refuse($"could not reach {new Uri(url).Host} — {error.Message}. Nothing was installed.");
        }

        var status = (int)response.StatusCode;
        if (passRedirects && status is >= 300 and < 400 && response.Headers.Location is not null) return response;
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            response.Dispose();
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            response.Dispose();
            throw refuse($"{url} answered {status} — nothing was installed.");
        }

        return response;
    }

    /// <summary>Stream a download to disk, hashing on the way — never held whole. Null for a 404.</summary>
    public static async Task<(string Sha256, long Size)?> SaveAsync(
        HttpClient http, string url, string to, CancellationToken ct, Func<string, Exception> refuse)
    {
        using var response = await GetAsync(http, url, ct, refuse).ConfigureAwait(false);
        return response is null ? null : await SaveAsync(response, to, ct).ConfigureAwait(false);
    }

    /// <summary>A response's body streamed to disk, hashed on the way.</summary>
    public static async Task<(string Sha256, long Size)> SaveAsync(HttpResponseMessage response, string to, CancellationToken ct)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long size = 0;
        await using (var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
        await using (var target = File.Create(to))
        {
            var buffer = new byte[81920];
            int read;
            while ((read = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
            {
                hash.AppendData(buffer, 0, read);
                size += read;
                await target.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
            }
        }

        return (Convert.ToHexStringLower(hash.GetHashAndReset()), size);
    }
}
