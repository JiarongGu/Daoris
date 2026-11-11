using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <see cref="SessionChanges"/> for a stand-in that is the client's doors rather than an HTTP handler: the change door is answered
/// from what the stand-in answers the whole list, and every other request passes through.
/// </summary>
internal sealed class SessionChangesDoors(IServiceDoors inner) : IServiceDoors
{
    public string BaseUrl => inner.BaseUrl;

    public Task<string> GetAsync(string path, CancellationToken ct) => DoorOf(path) is { } door ? WholeAsync(door, ct) : inner.GetAsync(path, ct);

    public Task<string> PostAsync(string path, string json, CancellationToken ct) => inner.PostAsync(path, json, ct);

    public async Task<ServiceReply> SendAsync(ServiceVerb verb, string path, string? json, CancellationToken ct)
    {
        if (verb != ServiceVerb.Get || DoorOf(path) is not { } door) return await inner.SendAsync(verb, path, json, ct).ConfigureAwait(false);
        var listed = await inner.SendAsync(ServiceVerb.Get, door, null, ct).ConfigureAwait(false);
        return listed.Ok ? listed with { Payload = SessionChangeShape.Whole(listed.Payload) } : listed;
    }

    public void Dispose() => inner.Dispose();

    private static string? DoorOf(string path) => SessionChangeShape.ListDoorOf(path.Split('?')[0]);

    private async Task<string> WholeAsync(string door, CancellationToken ct) =>
        SessionChangeShape.Whole(await inner.GetAsync(door, ct).ConfigureAwait(false));
}
