using System.Net;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// 🔴 FINDDAORIS1 (D171 point 1): the owner ran <c>daoris-driver setup &lt;repository&gt; --plan</c> in a PowerShell of their
/// own and met <i>no DAORIS_SERVICE_URL</i>, with Daoris running. A program the install carries now finds the service in this
/// order: the variable; else the address the running desktop left in the home, trusted only while the process it names lives
/// and the address answers as a Daoris host on this machine; else the refusal, which says Daoris is not running and how to
/// start it. A workspace build reads no address file, so a gate, a rehearsal or a test never reaches a live install.
/// </summary>
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

    private Func<string, bool> Answering(bool answers) => url =>
    {
        _asked.Add(url);
        return answers;
    };

    private RunningAddress Running(string home, string url = "http://localhost:5181")
    {
        Directory.CreateDirectory(home);
        return RunningAddress.Write(home, url) ?? throw new InvalidOperationException("the address was not written");
    }

    [Fact]
    public void The_variable_still_wins()
    {
        using var running = Running(Data);
        using var service = ServiceHttp.FromEnvironment(
            Env(("DAORIS_SERVICE_URL", "http://localhost:5199/")), App, Answering(true));

        Assert.Equal("http://localhost:5199", service.BaseUrl);
        Assert.Empty(_asked);
    }

    [Fact]
    public void A_program_the_install_carries_finds_the_service_the_running_desktop_holds()
    {
        using var running = Running(Data);
        using var service = ServiceHttp.FromEnvironment(Env(), App, Answering(true));

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
        using var service = ServiceHttp.FromEnvironment(Env(("DAORIS_HOME", other)), App, Answering(true));

        Assert.Equal("http://localhost:5182", service.BaseUrl);
    }

    [Fact]
    public void With_nothing_running_the_refusal_says_so_and_how_to_start_it()
    {
        var error = Assert.Throws<DriverException>(() => ServiceHttp.FromEnvironment(Env(), App, Answering(true)));

        Assert.StartsWith("no DAORIS_SERVICE_URL — the driver watches a service", error.Message);
        Assert.Contains($"Daoris is not running on {Data}", error.Message);
        Assert.Contains(Path.Combine(_install, "Daoris.exe"), error.Message);
        Assert.Empty(_asked);
    }

    /// <summary>A desktop that ended without its clean exit left its address: the process it names is gone, and nothing is asked.</summary>
    [Fact]
    public void An_address_whose_process_is_gone_is_not_trusted()
    {
        Directory.CreateDirectory(Data);
        File.WriteAllText(RunningAddress.PathIn(Data), new JsonObject
        {
            ["url"] = "http://localhost:5181", ["pid"] = Environment.ProcessId, ["started"] = 1L, ["since"] = "2026-10-16T08:00:00+00:00",
        }.ToJsonString());

        var error = Assert.Throws<DriverException>(() => ServiceHttp.FromEnvironment(Env(), App, Answering(true)));

        Assert.Contains($"Daoris is not running on {Data}", error.Message);
        Assert.Empty(_asked);
    }

    [Fact]
    public void An_address_that_does_not_answer_as_Daoris_is_not_trusted()
    {
        using var running = Running(Data);

        var error = Assert.Throws<DriverException>(() => ServiceHttp.FromEnvironment(Env(), App, Answering(false)));

        Assert.Contains($"Daoris is running on {Data} (pid {Environment.ProcessId})", error.Message);
        Assert.Contains("nothing answered as its service at http://localhost:5181", error.Message);
    }

    /// <summary>Only this machine's own service is taken from a file: a terminal's verbs send machine paths (D46).</summary>
    [Fact]
    public void An_address_on_another_machine_is_not_trusted_and_not_asked()
    {
        using var running = Running(Data, "https://team.example.com");

        var error = Assert.Throws<DriverException>(() => ServiceHttp.FromEnvironment(Env(), App, Answering(true)));

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
            ServiceHttp.FromEnvironment(Env(("DAORIS_HOME", Data)), AppContext.BaseDirectory, Answering(true)));

        Assert.Equal(ServiceHttp.NoService, error.Message);
        Assert.Empty(_asked);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, "{\"tier\":\"lexical only\",\"semantic\":false}", true)]
    [InlineData(HttpStatusCode.OK, "{\"semantic\":false}", false)]
    [InlineData(HttpStatusCode.OK, "<html></html>", false)]
    [InlineData(HttpStatusCode.NotFound, "{\"tier\":\"lexical only\"}", false)]
    public void The_probe_takes_a_Daoris_hosts_status_alone(HttpStatusCode status, string body, bool daoris)
    {
        var asked = new List<string>();
        Assert.Equal(daoris, RunningService.AnswersAsDaoris("http://localhost:5181/", new Host(request =>
        {
            asked.Add(request.RequestUri!.ToString());
            return new HttpResponseMessage(status) { Content = new StringContent(body) };
        })));
        Assert.Equal(["http://localhost:5181/api/status"], asked);
    }

    [Fact]
    public void A_probe_nothing_answers_is_no_answer() =>
        Assert.False(RunningService.AnswersAsDaoris("http://localhost:5181", new Host(_ => throw new HttpRequestException("refused"))));

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
}
