using System.Net.Http.Headers;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// The service's doors over HTTP:<see cref="IServiceDoors"/> as the driver speaks it to a host, the key its bearer,
/// through <see cref="DriverHttp"/>'s client and reads. The HTTP <see cref="ServiceClient"/> held before its doors had a port,
/// moved as it was.
/// </summary>
public sealed class ServiceHttp : IServiceDoors
{
    private readonly HttpClient _http;

    /// <param name="http">A test's client, the key set on it as the bearer; null makes the driver's own.</param>
    public ServiceHttp(string baseUrl, string? key, HttpClient? http = null)
    {
        BaseUrl = baseUrl.TrimEnd('/');
        _http = http ?? DriverHttp.Client(key);
        if (http is not null && !string.IsNullOrWhiteSpace(key))
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        }
    }

    /// <summary>URL and key from the environment — the same two names `connect` reads (design §5b).</summary>
    public static ServiceHttp FromEnvironment()
    {
        var url = Environment.GetEnvironmentVariable(ServiceClient.UrlVariable);
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new DriverException(
                $"no {ServiceClient.UrlVariable} — the driver watches a service, and needs to know where one is. "
                + "A local host is usually http://localhost:5177.");
        }

        return new ServiceHttp(url, Environment.GetEnvironmentVariable(ServiceClient.KeyVariable));
    }

    public string BaseUrl { get; }

    // Through DriverHttp, so a refused read carries the service's own sentence — a bare
    // EnsureSuccessStatusCode threw it away, and the driver's log said less than the service said.
    public Task<string> GetAsync(string path, CancellationToken ct) => DriverHttp.GetAsync(_http, $"{BaseUrl}{path}", ct);

    public Task<string> PostAsync(string path, string json, CancellationToken ct) => DriverHttp.PostAsync(_http, $"{BaseUrl}{path}", json, ct);

    public async Task<ServiceReply> SendAsync(ServiceVerb verb, string path, string? json, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(Method(verb), $"{BaseUrl}{path}");
        if (json is not null) request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        return new ServiceReply((int)response.StatusCode, await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
    }

    public void Dispose() => _http.Dispose();

    private static HttpMethod Method(ServiceVerb verb) => verb switch
    {
        ServiceVerb.Post => HttpMethod.Post,
        ServiceVerb.Delete => HttpMethod.Delete,
        _ => HttpMethod.Get,
    };
}
