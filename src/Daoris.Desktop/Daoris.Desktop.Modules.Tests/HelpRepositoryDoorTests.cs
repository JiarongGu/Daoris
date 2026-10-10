using System.Net;
using System.Text;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// ENTRY1d1: a move proposal's Apply is the Manage drawer's own door, the local host's
/// <c>POST /api/registry/{repository}/workspace</c>, which edits one field of the registry's row, and the host's answer comes
/// back as its sentence. A service stood in, and nothing starts a process: the fast half (MOD8), beside
/// <see cref="HelpDoorsTests"/>, whose update runs a script.
/// </summary>
public sealed class HelpRepositoryDoorTests : DriverModuleBridge
{
    [Fact]
    public async Task A_move_goes_through_the_drawers_own_door_on_the_local_host()
    {
        var host = new StandInHost();
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(host));
        var doors = Module().HelpDoors(service);

        var moved = await doors.WireRepositoryAsync("engine", "studio", CancellationToken.None);
        var refused = await doors.WireRepositoryAsync("atelier", "studio", CancellationToken.None);

        Assert.Equal((true, "`engine` is now in workspace `studio`."), moved);
        Assert.Equal((false, "`atelier` is not registered here — `daoris connect` from inside it, or add it from Repositories."), refused);
        Assert.Equal(
            [("POST", "/api/registry/engine/workspace", "studio"), ("POST", "/api/registry/atelier/workspace", "studio")],
            host.Heard);
    }

    [Fact]
    public async Task A_move_before_the_driver_is_up_is_the_cold_start_sentence()
    {
        var refused = await Assert.ThrowsAnyAsync<Exception>(
            () => Module().HelpDoors(null).WireRepositoryAsync("engine", "studio", CancellationToken.None));

        Assert.Contains("still coming up", refused.Message);
    }

    /// <summary>The local host's re-wiring door, standing in: every request kept as heard, its method, path and workspace.</summary>
    private sealed class StandInHost : HttpMessageHandler
    {
        public List<(string Method, string Path, string? Workspace)> Heard { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var workspace = body.RootElement.GetProperty("workspace").GetString();
            lock (Heard) Heard.Add((request.Method.Method, path, workspace));
            var (status, answer) = path == "/api/registry/engine/workspace"
                ? (HttpStatusCode.OK, JsonSerializer.Serialize(new { repository = "engine", at = "2026-10-11T10:00:00Z", workspace }))
                : (HttpStatusCode.NotFound, JsonSerializer.Serialize(new
                {
                    error = "`atelier` is not registered here — `daoris connect` from inside it, or add it from Repositories.",
                }));
            return new HttpResponseMessage(status) { Content = new StringContent(answer, Encoding.UTF8, "application/json") };
        }
    }
}
