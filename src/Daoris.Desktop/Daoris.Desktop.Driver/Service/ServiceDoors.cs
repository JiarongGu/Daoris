namespace Daoris.Driver;

/// <summary>
/// The service's doors as <see cref="ServiceClient"/> knocks on them: a path under the host, a body,
/// and what came back. The client writes each request and reads each answer; the HTTP is
/// <see cref="ServiceHttp"/>'s, which the client's own constructors compose, and a test may hand its own.
/// </summary>
public interface IServiceDoors : IDisposable
{
    /// <summary>Where the service is, without a trailing slash: what every path is under, and what a sentence names.</summary>
    string BaseUrl { get; }

    /// <summary>
    /// A read's text. A refusal throws <see cref="DriverException"/> carrying the service's own sentence, so the driver's log
    /// never says less than the service said.
    /// </summary>
    Task<string> GetAsync(string path, CancellationToken ct);

    /// <summary>A JSON write's answer, its refusal thrown as a read's is.</summary>
    Task<string> PostAsync(string path, string json, CancellationToken ct);

    /// <summary>A request whose every answer comes back, a refusal's included: its status and its text.</summary>
    /// <param name="json">A POST's JSON body; null sends none.</param>
    Task<ServiceReply> SendAsync(ServiceVerb verb, string path, string? json, CancellationToken ct);
}

/// <summary>The verbs the client's doors take.</summary>
public enum ServiceVerb
{
    Get,
    Post,
    Delete,
}

/// <summary>An answer as a door gave it: its status and its text, whatever either says.</summary>
public sealed record ServiceReply(int Status, string Payload)
{
    /// <summary>A success, 200 to 299, as an HTTP answer's <c>IsSuccessStatusCode</c> reads one.</summary>
    public bool Ok => Status is >= 200 and <= 299;

    /// <summary>
    /// The tier the search answered with, as its <c>x-daoris-tier</c> header says it (TIER1, D24): read for a brainstorm's packet
    /// (BRAINSTORM1a), which says which tier found its sources; null where the door carries no such header.
    /// </summary>
    public string? Tier { get; init; }
}
