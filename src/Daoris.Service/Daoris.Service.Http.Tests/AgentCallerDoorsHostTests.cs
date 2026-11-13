using Daoris.Knowledge.Http;

namespace Daoris.Service.Http.Tests;

/// <summary>
/// D284 point 2: no person door. On a host holding the person key, the doors no workflow step waits on answer a keyless call (a
/// session's or Ask Daoris's terminal) as they answer the person's: a brainstorm asked, sent, stopped and cleared, finished history
/// cleared, a parked line's place let go, the registry's verbs and a machine's retirement. A refusal here is the door's own (no
/// such brainstorm, a body it does not read), never <c>person-only</c>.
/// </summary>
public sealed class AgentCallerDoorsHostTests(KeyedHost host) : IClassFixture<KeyedHost>
{
    public static TheoryData<string, string, string?> Doors => new()
    {
        { "POST", "/api/brainstorms", """{"question":"Which of the two caches should the report read?"}""" },
        { "POST", "/api/brainstorms/ffffffff/send", "{}" },
        { "POST", "/api/brainstorms/ffffffff/stop", "{}" },
        { "DELETE", "/api/brainstorms/ffffffff", null },
        { "POST", "/api/history/clear", "{}" },
        { "POST", "/api/sessions/no-such-session/release-place", "{}" },
        { "POST", "/api/sync/machines/no-such-machine/retire", "{}" },
        { "DELETE", "/api/registry/no-such-repository", null },
        { "POST", "/api/registry/no-such-repository/workspace", "{}" },
        { "POST", "/api/registry/no-such-repository/excludes", "{}" },
        { "POST", "/api/registry/import", "{}" },
    };

    [Theory]
    [MemberData(nameof(Doors))]
    public async Task A_keyless_call_is_answered_as_the_persons_is(string method, string path, string? body)
    {
        var keyless = await host.SendAsync(method, path, DaorisHost.Loopback, json: body);
        var keyed = await host.SendAsync(method, path, DaorisHost.Loopback, json: body, person: KeyedHost.Key);

        Assert.True(keyless.Status != 403, $"keyless {method} {path}: {keyless.Status} {keyless.Body}");
        Assert.True(keyless.Status < 500, $"keyless {method} {path}: {keyless.Status} {keyless.Body}");
        Assert.Equal(DoorClass.Agent, PersonDoors.Resolve(method, path)!.Value.Door.Class);
        // The same answer the person's call gets but for what the first call changed: a brainstorm asked twice is two brainstorms.
        if (method != "POST" || path != "/api/brainstorms") Assert.Equal(keyed.Status, keyless.Status);
    }
}
