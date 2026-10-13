using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// STRUCT3d: the service client over its port (D166's STRUCT3d note). <see cref="ServiceClient"/> writes each request and reads
/// each answer; <see cref="IServiceDoors"/> carries them, the HTTP's (<c>ServiceHttp</c>) or this memory's, so a use case is held
/// with no host and no HTTP. Runs no process: the fast half (MOD8).
/// </summary>
public sealed class ServiceDoorsTests
{
    [Fact]
    public async Task A_read_asks_its_door_by_its_path_under_the_host_and_reads_the_answer()
    {
        var doors = new Doors { Read = path => path == "/api/registry" ? """[{ "repository": "engine", "adopted": true }]""" : "[]" };
        using var service = new ServiceClient(doors);

        var registry = await service.RegistryAsync();

        Assert.Equal("engine", Assert.Single(registry).Repository);
        Assert.Equal(["GET /api/registry"], doors.Asked);
        Assert.Equal("http://desk:5177", service.BaseUrl);
    }

    [Fact]
    public async Task A_write_sends_its_body_and_a_door_the_host_lacks_is_said_with_the_host_it_asked()
    {
        var doors = new Doors { Reply = (_, _, _) => new ServiceReply(404, "Not Found") };
        using var service = new ServiceClient(doors);

        var (ok, message) = await service.PersonDoneAsync("#q7", "  mine  ");

        Assert.False(ok);
        Assert.Equal("the service at http://desk:5177 has no done door for a person (404) — is it older than this driver?", message);
        Assert.Equal(["POST /api/quests/q7/done {\"note\":\"mine\"}"], doors.Asked);
    }

    [Fact]
    public async Task An_ask_the_service_has_none_of_is_null_and_a_delete_is_a_delete()
    {
        var doors = new Doors
        {
            Reply = (verb, _, _) => verb == ServiceVerb.Get
                ? new ServiceReply(404, "")
                : new ServiceReply(409, """{ "error": "a quest stands on it" }"""),
        };
        using var service = new ServiceClient(doors);

        Assert.Null(await service.FindAskAsync("#a3"));
        Assert.Equal((false, "a quest stands on it"), await service.DeleteAskAsync("a3"));
        Assert.Equal(["GET /api/asks/a3", "DELETE /api/asks/a3"], doors.Asked);
    }

    [Fact]
    public async Task A_session_moved_is_raised_from_the_answer_and_the_client_disposes_its_doors()
    {
        var doors = new Doors { Reply = (_, _, _) => new ServiceReply(200, """{ "session": { "state": "completed" }, "message": "moved" }""") };
        var service = new ServiceClient(doors);
        SessionMoved? moved = null;
        service.Moved += each => moved = each;

        Assert.Equal("moved", await service.AdvanceAsync("s1", "completed"));
        Assert.Equal(new SessionMoved("s1", "completed"), moved);

        service.Dispose();
        Assert.True(doors.Disposed);
    }

    /// <summary>The doors in memory: what was asked, in order, and what each answers.</summary>
    private sealed class Doors : IServiceDoors
    {
        public List<string> Asked { get; } = [];

        public Func<string, string> Read { get; init; } = _ => "[]";

        public Func<ServiceVerb, string, string?, ServiceReply> Reply { get; init; } = (_, _, _) => new ServiceReply(200, "{}");

        public bool Disposed { get; private set; }

        public string BaseUrl => "http://desk:5177";

        public Task<string> GetAsync(string path, CancellationToken ct)
        {
            Asked.Add($"GET {path}");
            return Task.FromResult(Read(path));
        }

        public Task<string> PostAsync(string path, string json, CancellationToken ct)
        {
            Asked.Add($"POST {path} {json}");
            return Task.FromResult("{}");
        }

        public Task<ServiceReply> SendAsync(ServiceVerb verb, string path, string? json, CancellationToken ct)
        {
            Asked.Add(json is null ? $"{verb.ToString().ToUpperInvariant()} {path}" : $"{verb.ToString().ToUpperInvariant()} {path} {json}");
            return Task.FromResult(Reply(verb, path, json));
        }

        public void Dispose() => Disposed = true;
    }
}
