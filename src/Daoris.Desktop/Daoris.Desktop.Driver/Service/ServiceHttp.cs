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

    /// <summary>The refusal for no service at all, where nothing else was looked for.</summary>
    public const string NoService =
        $"no {ServiceClient.UrlVariable} — the driver watches a service, and needs to know where one is. "
        + "A local host is usually http://localhost:5177.";

    /// <summary>URL and key from the environment — the same two names `connect` reads (design §5b) — for this process.</summary>
    public static ServiceHttp FromEnvironment() =>
        FromEnvironment(Environment.GetEnvironmentVariable, AppContext.BaseDirectory, url => RunningService.Status(url));

    /// <summary>
    /// The service, in the order a terminal finds it (FINDDAORIS1, D171 point 1): the variable, which still wins; else, for a
    /// program the install carries, the address the running desktop left in the home (<see cref="RunningService"/>); else the
    /// refusal, which then says Daoris is not running there and how to start it.
    /// </summary>
    /// <remarks>
    /// 🔴 A workspace build reads no address file. The account's <c>DAORIS_HOME</c> names a live install's home on a machine
    /// that has one, and a gate, a rehearsal or a test that cleared the variable to see this refusal would otherwise reach that
    /// install's running service and act on it. The dev loop and every gate name the service themselves.
    /// </remarks>
    /// <param name="baseDirectory">Where this program runs from, which says whether an install carries it.</param>
    /// <param name="answers">What an address's status door answers as a Daoris host, or null (HOMEID1: its home is checked).</param>
    public static ServiceHttp FromEnvironment(Func<string, string?> environment, string baseDirectory, Func<string, ServiceStatus?> answers)
    {
        var url = environment(ServiceClient.UrlVariable);
        if (string.IsNullOrWhiteSpace(url))
        {
            if (DaorisHome.InstallOf(baseDirectory) is not { } install
                || DaorisHome.Resolve(environment, baseDirectory) is not { } home)
            {
                throw new DriverException(NoService);
            }

            url = RunningService.Find(home, install, answers, out var why) ?? throw new DriverException($"{NoService} {why}");
        }

        return new ServiceHttp(url, environment(ServiceClient.KeyVariable));
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
