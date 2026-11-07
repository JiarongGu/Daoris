using System.Text.Json;

namespace Daoris.Driver;

/// <summary>What a service's status door last said, as This Daoris shows it.</summary>
/// <param name="Answers">Whether a Daoris host answered.</param>
/// <param name="Asked">When it is asked.</param>
/// <param name="Why">Why it did not answer, a code: <c>no-answer</c>, <c>foreign</c>, <c>no-host</c>, <c>unstartable</c>, <c>exited</c>, <c>never-answered</c>.</param>
/// <param name="Tier">Its search tier, as it says it.</param>
/// <param name="Version">Its version, as it says it (<c>/api/status</c>'s <c>version</c>); null from a host older than that.</param>
/// <param name="Home">The id of the home it serves, as <see cref="RunningService.ReadStatus"/> reads it; null where it names none.</param>
/// <remarks>Discovery's own reading is <see cref="ServiceStatus"/>: this one keeps when it is asked, why not, and the version.</remarks>
public sealed record ServiceProbe(bool Answers, DateTimeOffset Asked, string? Why, string? Tier, string? Version, string? Home = null)
{
    /// <summary>The longest tier or version kept: a status is the service's words, and a host is not trusted to keep them short.</summary>
    private const int Longest = 200;

    /// <summary>A status body read as discovery reads it (<see cref="RunningService.ReadStatus"/>): a Daoris host's names its tier, any other is foreign.</summary>
    public static ServiceProbe Of(string body, DateTimeOffset asked)
    {
        if (RunningService.ReadStatus(body) is not { } read) return new(false, asked, "foreign", null, null);
        using var status = JsonDocument.Parse(body);
        return new(true, asked, null, Short(status.RootElement, "tier"), Short(status.RootElement, "version"), read.Home);
    }

    private static string? Short(JsonElement status, string name) =>
        status.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { } text
            ? text.Length > Longest ? text[..Longest] : text
            : null;

    /// <summary>
    /// Ask <paramref name="url"/>'s status door within two seconds and with redirects off, as a terminal finding the running
    /// service does (where an address sends its asker is no answer).
    /// </summary>
    /// <param name="handler">A test's host; null asks over the network.</param>
    public static ServiceProbe Ask(string url, HttpMessageHandler? handler = null)
    {
        var asked = DateTimeOffset.UtcNow;
        using var http = handler is null
            ? new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false })
            : new HttpClient(handler, disposeHandler: false);
        http.Timeout = TimeSpan.FromSeconds(2);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{url.TrimEnd('/')}/api/status");
            using var response = http.Send(request);
            if (!response.IsSuccessStatusCode) return new(false, asked, "foreign", null, null);
            using var reader = new StreamReader(response.Content.ReadAsStream());
            return Of(reader.ReadToEnd(), asked);
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or IOException
                                          or InvalidOperationException or NotSupportedException or UriFormatException)
        {
            return new(false, asked, "no-answer", null, null);
        }
    }
}
