using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The service the brainstorm and meeting verbs press (ONEWAY1t), in memory: each request kept, each answered as the test says,
/// by default as the desk's success. The desk's own rules are the service suite's; here the verb's line, its path, its body and
/// what it prints are held.
/// </summary>
internal sealed class BrainstormDoorStandIn : IServiceDoors
{
    public List<(ServiceVerb Verb, string Path, string? Json)> Sent { get; } = [];

    /// <summary>What the service answers; the default is a success naming the desk's sentence.</summary>
    public Func<ServiceVerb, string, string?, ServiceReply> Answer { get; set; } =
        (_, _, _) => new ServiceReply(200, """{ "message": "Brainstorm `ab12cd34` asked." }""");

    /// <summary>Thrown by every press, as a service that is not there is.</summary>
    public Exception? Down { get; set; }

    public string BaseUrl => "http://stand-in";

    public Task<string> GetAsync(string path, CancellationToken ct) => throw new NotSupportedException();

    public Task<string> PostAsync(string path, string json, CancellationToken ct) => throw new NotSupportedException();

    public Task<ServiceReply> SendAsync(ServiceVerb verb, string path, string? json, CancellationToken ct)
    {
        if (Down is not null) throw Down;
        Sent.Add((verb, path, json));
        return Task.FromResult(Answer(verb, path, json));
    }

    public void Dispose()
    {
    }
}
