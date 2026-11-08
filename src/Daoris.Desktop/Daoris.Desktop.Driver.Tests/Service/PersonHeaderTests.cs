using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The shell's own clients present the person key it minted to its host, on the doors that are the person's or the driver's, and only
/// to an address of this machine: a service address that names a remote never carries it off the machine. A client handed none sends no header.
/// </summary>
public sealed class PersonHeaderTests
{
    private const string Key = "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8";

    /// <summary>Records the person header each request carried, by its method and address.</summary>
    private sealed class Recorder : HttpMessageHandler
    {
        public List<(string Request, string? Header)> Seen { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Seen.Add(($"{request.Method} {request.RequestUri}",
                request.Headers.TryGetValues(PersonHeader.Name, out var values) ? values.Single() : null));
            var body = request.RequestUri!.AbsolutePath == "/api/sync"
                ? """{ "wired": true, "conflicts": [], "refused": [], "behind": [], "problem": null }"""
                : "[]";
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(body) });
        }
    }

    [Theory]
    [InlineData("http://localhost:5177", Key, Key)]
    [InlineData("http://127.0.0.1:5177", Key, Key)]
    [InlineData("http://[::1]:5177", Key, Key)]
    [InlineData("https://remote.example.com", Key, null)]
    [InlineData("http://localhost.example.com:5177", Key, null)]
    [InlineData("http://localhost:5177", null, null)]
    [InlineData("http://localhost:5177", " ", null)]
    public async Task The_service_client_presents_the_key_to_this_machine_alone(string url, string? key, string? sent)
    {
        var host = new Recorder();
        using var doors = new ServiceHttp(url, key: null, new HttpClient(host, disposeHandler: false), personKey: key);

        await doors.PostAsync("/api/sessions/s1/state", "{}", CancellationToken.None);
        await doors.SendAsync(ServiceVerb.Post, "/api/sessions/s1/taken", "{}", CancellationToken.None);

        Assert.All(host.Seen, call => Assert.Equal(sent, call.Header));
        Assert.Equal(2, host.Seen.Count);
    }

    [Fact]
    public async Task The_clients_the_loop_makes_of_its_host_both_carry_it()
    {
        var host = new Recorder();
        using var client = new ServiceClient("http://localhost:5177", null, new HttpClient(host, disposeHandler: false), personKey: Key);

        await client.RefreshAsync();

        Assert.Equal(Key, host.Seen.Single(call => call.Request.StartsWith("POST")).Header);
    }

    /// <summary>The sync's local client posts the host's own pass, a driver's door; the remote it feeds is another machine and never sees the key.</summary>
    [Fact]
    public async Task The_sync_presents_the_key_to_its_local_host_and_never_to_the_remote()
    {
        var host = new Recorder();
        using var sync = new RemoteSync(
            "http://localhost:5177", null, RemoteTarget.DefaultWorkspace, new RemoteTarget("https://remote.example.com", "dk_test"), host,
            personKey: Key);

        await sync.RunOnceAsync();

        Assert.Equal(Key, host.Seen.Single(call => call.Request == "POST http://localhost:5177/api/sync?workspace=default").Header);
        Assert.All(host.Seen.Where(call => call.Request.Contains("remote.example.com")), call => Assert.Null(call.Header));
    }
}
