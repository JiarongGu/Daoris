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
    /// <param name="retryAfter">The wait before a status door that did not answer is asked again (SERVICESTALL1); null is <see cref="RunningService.RetryAfter"/>.</param>
    public static ServiceHttp FromEnvironment(
        Func<string, string?> environment, string baseDirectory, Func<string, ServiceStatus?> answers, TimeSpan? retryAfter = null)
    {
        var url = environment(ServiceClient.UrlVariable);
        if (string.IsNullOrWhiteSpace(url))
        {
            if (DaorisHome.InstallOf(baseDirectory) is not { } install
                || DaorisHome.Resolve(environment, baseDirectory) is not { } home)
            {
                throw new DriverException(NoService);
            }

            // An address the home left that nothing answered at is not a missing variable: its sentence says what was seen, alone.
            url = RunningService.Find(home, install, answers, out var why, out var unanswered, retryAfter ?? RunningService.RetryAfter)
                ?? throw new DriverException(unanswered ? why : $"{NoService} {why}");
        }

        return new ServiceHttp(url, environment(ServiceClient.KeyVariable));
    }

    public string BaseUrl { get; }

    // Through DriverHttp, so a refused read carries the service's own sentence — a bare
    // EnsureSuccessStatusCode threw it away, and the driver's log said less than the service said.
    /// <summary>The wait before a read the service did not answer is asked once more (SERVICESTALL1).</summary>
    public TimeSpan RetryAfter { get; init; } = TimeSpan.FromSeconds(1);

    public Task<string> GetAsync(string path, CancellationToken ct) =>
        Attempt(read: true, path, () => DriverHttp.GetAsync(_http, $"{BaseUrl}{path}", ct), ct);

    public Task<string> PostAsync(string path, string json, CancellationToken ct) =>
        Attempt(read: false, path, () => DriverHttp.PostAsync(_http, $"{BaseUrl}{path}", json, ct), ct);

    public Task<ServiceReply> SendAsync(ServiceVerb verb, string path, string? json, CancellationToken ct) =>
        Attempt(read: verb == ServiceVerb.Get, path, async () =>
        {
            using var request = new HttpRequestMessage(Method(verb), $"{BaseUrl}{path}");
            if (json is not null) request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
            return new ServiceReply((int)response.StatusCode, await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false))
            {
                Tier = response.Headers.TryGetValues("x-daoris-tier", out var tier) ? tier.FirstOrDefault() : null,
                Run = response.Headers.TryGetValues("x-daoris-run", out var run) ? run.FirstOrDefault() : null,
            };
        }, ct);

    /// <summary>
    /// One request, said as what it saw where the service did not answer (SERVICESTALL1): a read is asked once more after
    /// <see cref="RetryAfter"/>, since asking again changes nothing; a press that changes state is never asked again blindly, since
    /// the service may have done it before it stopped answering. A refusal is an answer and comes back as the service said it.
    /// </summary>
    private async Task<T> Attempt<T>(bool read, string path, Func<Task<T>> send, CancellationToken ct)
    {
        try
        {
            return await send().ConfigureAwait(false);
        }
        catch (Exception first) when (Unanswered(first, ct))
        {
            if (!read) throw Said(path, first, again: false);
            await Task.Delay(RetryAfter, ct).ConfigureAwait(false);
            try
            {
                return await send().ConfigureAwait(false);
            }
            catch (Exception second) when (Unanswered(second, ct))
            {
                throw Said(path, second, again: true);
            }
        }
    }

    /// <summary>Whether a request failed for want of an answer, not because its caller gave up.</summary>
    private static bool Unanswered(Exception error, CancellationToken ct) =>
        !ct.IsCancellationRequested && error is HttpRequestException or TaskCanceledException or TimeoutException;

    /// <summary>
    /// What the driver saw: the address, the bound where it ran out, whether it asked again, and where it did not what to look at. The
    /// type stays the one a caller already catches.
    /// </summary>
    private HttpRequestException Said(string path, Exception error, bool again)
    {
        var saw = error is HttpRequestException refused
            ? $"could not be reached ({refused.Message})"
            : $"did not answer within {RunningService.Seconds(_http.Timeout)}";
        var then = again
            ? $", and not again after a second try {RunningService.Seconds(RetryAfter)} later"
            : ". This asked for a change, so it was not asked again: the service may have done it before it stopped answering, so look at what it "
              + "changes (the session, quest or ask it names) before you press it again";
        return new HttpRequestException(
            $"the service at {BaseUrl} {saw} to {path}{then}. The address was found, so {ServiceClient.UrlVariable} is not what is "
            + "missing: the service may be busy, so run this again in a moment.", error);
    }

    public void Dispose() => _http.Dispose();

    private static HttpMethod Method(ServiceVerb verb) => verb switch
    {
        ServiceVerb.Post => HttpMethod.Post,
        ServiceVerb.Delete => HttpMethod.Delete,
        _ => HttpMethod.Get,
    };
}
