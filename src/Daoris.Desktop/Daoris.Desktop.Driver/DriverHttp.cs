using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The driver's one way of speaking HTTP — client construction, the two verbs, and how a refusal is
/// read. It existed three times with three error conventions, and the DRV2-era copy threw the
/// service's refusal sentence away with a bare EnsureSuccessStatusCode — the exact loss its own
/// comment called unacceptable. The refusal sentence is the contract; every caller keeps it.
/// </summary>
internal static class DriverHttp
{
    /// <summary>A client for one host, carrying its key when there is one. The handler parameter is
    /// the test seam — production callers pass none and get the real transport.</summary>
    public static HttpClient Client(string? key, HttpMessageHandler? handler = null)
    {
        var http = handler is null
            ? new HttpClient { Timeout = TimeSpan.FromSeconds(30) }
            : new HttpClient(handler, disposeHandler: false) { Timeout = TimeSpan.FromSeconds(30) };
        if (!string.IsNullOrWhiteSpace(key))
        {
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        }

        return http;
    }

    public static async Task<string> GetAsync(HttpClient http, string url, CancellationToken ct)
    {
        using var response = await http.GetAsync(url, ct).ConfigureAwait(false);
        var payload = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new DriverException($"{url} answered {(int)response.StatusCode}: {ErrorOf(payload)}");
        }

        return payload;
    }

    public static async Task PostAsync(HttpClient http, string url, string json, CancellationToken ct)
    {
        using var response = await http.PostAsync(
            url, new StringContent(json, Encoding.UTF8, "application/json"), ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var payload = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            throw new DriverException($"{url} answered {(int)response.StatusCode}: {ErrorOf(payload)}");
        }
    }

    /// <summary>The service's own sentence out of an error payload — verbatim, truncated only when it
    /// is not JSON at all (a proxy's HTML page must become a sentence, not a wall).</summary>
    public static string ErrorOf(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            return document.RootElement.TryGetProperty("error", out var error)
                && error.ValueKind == JsonValueKind.String
                ? error.GetString() ?? payload
                : payload;
        }
        catch (JsonException)
        {
            return payload.Length <= 200 ? payload : payload[..200] + "…";
        }
    }
}
