using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// 🔴 FINDDAORIS1 (D171 point 1): the owner ran <c>daoris-driver setup &lt;repository&gt; --plan</c> in a PowerShell of their
/// own and met <i>no DAORIS_SERVICE_URL</i>, with Daoris running. A program the install carries now finds the service in this
/// order: the variable; else the address the running desktop left in the home, trusted only while the process it names lives
/// and the address answers as this home's Daoris host on this machine; else the refusal, which says Daoris is not running and
/// how to start it. A workspace build reads no address file, so a gate, a rehearsal or a test never reaches a live install.
/// </summary>
/// <remarks>
/// 🔴 HOMEID1 (D171, the terminal RPC design §3.2): the status was asked with redirects followed and taken where it named a
/// <c>tier</c>, so another install's service, an older host, or a process id the system gave to another process since passed.
/// It is now taken only where it answers this home's id (<see cref="HomeIdentity"/>), with redirects off.
/// </remarks>
public sealed class RunningServiceTests : IDisposable
{
    private readonly string _install = DaorisHomeTests.Install();
    private readonly List<string> _asked = [];

    public void Dispose()
    {
        if (Directory.Exists(_install)) Directory.Delete(_install, recursive: true);
    }

    private string Data => Path.Combine(_install, "data");

    private string App => Path.Combine(_install, "app", "daoris-driver");

    private static Func<string, string?> Env(params (string Name, string? Value)[] pairs) =>
        name => pairs.FirstOrDefault(p => p.Name == name).Value;

    /// <summary>A status door answering as a Daoris host on <paramref name="home"/>, whose id it answers (HOMEID1).</summary>
    private Func<string, ServiceStatus?> Answering(string home) => Answering(new ServiceStatus("lexical only", HomeIdentity.Ensure(home)));

    /// <summary>A status door answering as the test says: null is nothing answering as a Daoris host.</summary>
    private Func<string, ServiceStatus?> Answering(ServiceStatus? status) => url =>
    {
        _asked.Add(url);
        return status;
    };

    private RunningAddress Running(string home, string url = "http://localhost:5181")
    {
        Directory.CreateDirectory(home);
        return RunningAddress.Write(home, url) ?? throw new InvalidOperationException("the address was not written");
    }

    /// <summary>The address file by hand, naming this process: the file is the contract.</summary>
    private static void Leave(string home, string url, long? started = null)
    {
        Directory.CreateDirectory(home);
        using var self = Process.GetCurrentProcess();
        File.WriteAllText(RunningAddress.PathIn(home), new JsonObject
        {
            ["url"] = url, ["pid"] = Environment.ProcessId, ["started"] = started ?? self.StartTime.ToUniversalTime().Ticks,
            ["since"] = "2026-10-16T08:00:00+00:00",
        }.ToJsonString());
    }

    [Fact]
    public void The_variable_still_wins()
    {
        using var running = Running(Data);
        using var service = ServiceHttp.FromEnvironment(
            Env(("DAORIS_SERVICE_URL", "http://localhost:5199/")), App, Answering(Data));

        Assert.Equal("http://localhost:5199", service.BaseUrl);
        Assert.Empty(_asked);
    }

    [Fact]
    public void A_program_the_install_carries_finds_the_service_the_running_desktop_holds()
    {
        using var running = Running(Data);
        using var service = ServiceHttp.FromEnvironment(Env(), App, Answering(Data));

        Assert.Equal("http://localhost:5181", service.BaseUrl);
        Assert.Equal(["http://localhost:5181"], _asked);
    }

    /// <summary>The home's variable still wins too: the address is the one in the home it names.</summary>
    [Fact]
    public void The_address_is_the_one_in_the_home_the_variable_names()
    {
        var other = Path.Combine(_install, "elsewhere");
        using var mine = Running(Data, "http://localhost:5181");
        using var named = Running(other, "http://localhost:5182");
        using var service = ServiceHttp.FromEnvironment(Env(("DAORIS_HOME", other)), App, Answering(other));

        Assert.Equal("http://localhost:5182", service.BaseUrl);
    }

    [Fact]
    public void With_nothing_running_the_refusal_says_so_and_how_to_start_it()
    {
        var error = Assert.Throws<DriverException>(() => ServiceHttp.FromEnvironment(Env(), App, Answering(Data)));

        Assert.StartsWith("no DAORIS_SERVICE_URL — the driver watches a service", error.Message);
        Assert.Contains($"Daoris is not running on {Data}", error.Message);
        Assert.Contains(Path.Combine(_install, "Daoris.exe"), error.Message);
        Assert.Empty(_asked);
    }

    /// <summary>A desktop that ended without its clean exit left its address: the process it names is gone, and nothing is asked.</summary>
    [Fact]
    public void An_address_whose_process_is_gone_is_not_trusted()
    {
        Leave(Data, "http://localhost:5181", started: 1L);

        var error = Assert.Throws<DriverException>(() => ServiceHttp.FromEnvironment(Env(), App, Answering(Data)));

        Assert.Contains($"Daoris is not running on {Data}", error.Message);
        Assert.Empty(_asked);
    }

    /// <summary>
    /// 🔴 HOMEID1: a process id the system gave to another process since. Its start is not the one the file names, so it is
    /// not taken and nothing is asked; and where it could not be told (the CLI's case), its status would answer another id.
    /// </summary>
    [Fact]
    public void A_process_id_used_again_is_not_taken()
    {
        HomeIdentity.Ensure(Data);
        Leave(Data, "http://localhost:5181", started: DateTime.UtcNow.AddDays(-3).Ticks);

        var error = Assert.Throws<DriverException>(() => ServiceHttp.FromEnvironment(Env(), App, Answering(Data)));

        Assert.Contains($"Daoris is not running on {Data}", error.Message);
        Assert.Empty(_asked);
    }

    [Fact]
    public void An_address_that_does_not_answer_as_Daoris_is_not_trusted()
    {
        using var running = Running(Data);

        var error = Assert.Throws<DriverException>(() => ServiceHttp.FromEnvironment(Env(), App, Answering((ServiceStatus?)null)));

        Assert.Contains($"Daoris is running on {Data} (pid {Environment.ProcessId})", error.Message);
        Assert.Contains("nothing answered as its service at http://localhost:5181", error.Message);
    }

    /// <summary>
    /// 🔴 HOMEID1: another install's service at the address this home's file names (the port was this home's once, and another
    /// install's host holds it now) answers as a Daoris host, and is not this home's: it is not taken.
    /// </summary>
    [Fact]
    public void An_address_whose_service_answers_another_homes_id_is_not_taken()
    {
        using var running = Running(Data);
        var another = new ServiceStatus("lexical only", "0123456789abcdef0123456789abcdef");

        var error = Assert.Throws<DriverException>(() => ServiceHttp.FromEnvironment(Env(), App, Answering(another)));

        Assert.Contains($"Daoris is running on {Data} (pid {Environment.ProcessId})", error.Message);
        Assert.Contains("http://localhost:5181 answered as another Daoris home's", error.Message);
        Assert.Equal(["http://localhost:5181"], _asked);
    }

    /// <summary>🔴 HOMEID1: a host that answers no home (one from before HOMEID1, or a shared host) is not taken either.</summary>
    [Fact]
    public void An_address_whose_service_answers_no_home_is_not_taken()
    {
        using var running = Running(Data);

        var error = Assert.Throws<DriverException>(() =>
            ServiceHttp.FromEnvironment(Env(), App, Answering(new ServiceStatus("lexical only", null))));

        Assert.Contains("http://localhost:5181 answered as no Daoris home's", error.Message);
    }

    /// <summary>A home with no id is no home a service can answer for: nothing is taken, whatever answers.</summary>
    [Fact]
    public void A_home_with_no_id_takes_no_address()
    {
        Leave(Data, "http://localhost:5181");
        var some = new ServiceStatus("lexical only", "0123456789abcdef0123456789abcdef");

        var error = Assert.Throws<DriverException>(() => ServiceHttp.FromEnvironment(Env(), App, Answering(some)));

        Assert.Contains("answered as another Daoris home's", error.Message);
        Assert.False(File.Exists(HomeIdentity.PathIn(Data)), "discovery writes nothing");
    }

    /// <summary>Only this machine's own service is taken from a file: a terminal's verbs send machine paths (D46).</summary>
    [Fact]
    public void An_address_on_another_machine_is_not_trusted_and_not_asked()
    {
        using var running = Running(Data, "https://team.example.com");

        var error = Assert.Throws<DriverException>(() => ServiceHttp.FromEnvironment(Env(), App, Answering(Data)));

        Assert.Contains("https://team.example.com", error.Message);
        Assert.Contains("not on this machine", error.Message);
        Assert.Empty(_asked);
    }

    /// <summary>
    /// 🔴 A workspace build reads no address file: the account's <c>DAORIS_HOME</c> names a live install's home on a developer's
    /// machine, and a gate's verb must never reach that install. Its refusal is the one it always was.
    /// </summary>
    [Fact]
    public void A_workspace_build_reads_no_address_file()
    {
        using var running = Running(Data);

        var error = Assert.Throws<DriverException>(() =>
            ServiceHttp.FromEnvironment(Env(("DAORIS_HOME", Data)), AppContext.BaseDirectory, Answering(Data)));

        Assert.Equal(ServiceHttp.NoService, error.Message);
        Assert.Empty(_asked);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, "{\"tier\":\"lexical only\",\"semantic\":false,\"home\":\"0123456789abcdef0123456789abcdef\"}", true, "0123456789abcdef0123456789abcdef")]
    [InlineData(HttpStatusCode.OK, "{\"tier\":\"lexical only\",\"semantic\":false}", true, null)]
    [InlineData(HttpStatusCode.OK, "{\"tier\":\"lexical only\",\"home\":\"not an id\"}", true, null)]
    [InlineData(HttpStatusCode.OK, "{\"semantic\":false}", false, null)]
    [InlineData(HttpStatusCode.OK, "<html></html>", false, null)]
    [InlineData(HttpStatusCode.NotFound, "{\"tier\":\"lexical only\"}", false, null)]
    public void The_probe_reads_a_Daoris_hosts_status_and_its_home(HttpStatusCode code, string body, bool daoris, string? home)
    {
        var asked = new List<string>();
        var status = RunningService.Status("http://localhost:5181/", new Host(request =>
        {
            asked.Add(request.RequestUri!.ToString());
            return new HttpResponseMessage(code) { Content = new StringContent(body) };
        }));

        Assert.Equal(daoris, status is not null);
        Assert.Equal(home, status?.Home);
        Assert.Equal(["http://localhost:5181/api/status"], asked);
    }

    [Fact]
    public void A_probe_nothing_answers_is_no_answer() =>
        Assert.Null(RunningService.Status("http://localhost:5181", new Host(_ => throw new HttpRequestException("refused"))));

    /// <summary>
    /// 🔴 HOMEID1: a redirect is no answer. The address names this machine; where it sends the asker could be another host's, so
    /// the status is asked with redirects off. Two real listeners on the loopback: the first redirects to the second, which would
    /// answer as this home's host, and is never asked.
    /// </summary>
    [Fact]
    public async Task A_redirect_is_no_answer()
    {
        using var target = Listener.Start();
        using var redirecting = Listener.Start();
        var id = HomeIdentity.Ensure(Data);
        var serving = target.AnswerOnceAsync($"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\n", $"{{\"tier\":\"lexical only\",\"home\":\"{id}\"}}");
        var redirect = redirecting.AnswerOnceAsync($"HTTP/1.1 302 Found\r\nLocation: {target.Url}/api/status\r\n", "");

        var status = await Task.Run(() => RunningService.Status(redirecting.Url));

        Assert.Null(status);
        Assert.True(await redirect, "the address was not asked");
        Assert.False(serving.IsCompleted && await serving, "the redirect was followed");
    }

    [Theory]
    [InlineData("http://localhost:5177", true)]
    [InlineData("http://127.0.0.1:5177", true)]
    [InlineData("http://[::1]:5177", true)]
    [InlineData("http://localhost.example.com:5177", false)]
    [InlineData("https://team.example.com", false)]
    [InlineData("not an address", false)]
    public void Only_this_machine_is_this_machine(string url, bool local) => Assert.Equal(local, RunningService.IsLoopback(url));

    private sealed class Host(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken) => answer(request);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(answer(request));
    }

    /// <summary>A loopback listener that answers its first request with what it is given, and then nothing.</summary>
    private sealed class Listener : IDisposable
    {
        private readonly TcpListener _listener;

        private Listener(TcpListener listener) => _listener = listener;

        public string Url => $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}";

        public static Listener Start()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return new Listener(listener);
        }

        /// <summary>True once it answered a request; false where none came within five seconds.</summary>
        public async Task<bool> AnswerOnceAsync(string head, string body)
        {
            using var waiting = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                using var client = await _listener.AcceptTcpClientAsync(waiting.Token);
                var stream = client.GetStream();
                var buffer = new byte[4096];
                var read = new StringBuilder();
                while (!read.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
                {
                    var count = await stream.ReadAsync(buffer, waiting.Token);
                    if (count == 0) break;
                    read.Append(Encoding.ASCII.GetString(buffer, 0, count));
                }

                var bytes = Encoding.UTF8.GetBytes(body);
                await stream.WriteAsync(Encoding.ASCII.GetBytes($"{head}Content-Length: {bytes.Length}\r\nConnection: close\r\n\r\n"), waiting.Token);
                await stream.WriteAsync(bytes, waiting.Token);
                return true;
            }
            catch (Exception error) when (error is OperationCanceledException or SocketException or ObjectDisposedException or IOException)
            {
                return false;
            }
        }

        public void Dispose() => _listener.Stop();
    }
}
