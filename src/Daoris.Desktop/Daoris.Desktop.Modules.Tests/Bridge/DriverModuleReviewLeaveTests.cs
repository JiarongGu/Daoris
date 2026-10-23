using System.Diagnostics;
using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// Leaving Review stops its git (GITAPP1a-iii, D202, the git-app design P1.6 and P1.7's cancellation and window cases), through the
/// real route boundary: <c>SESSION_DIFF</c> with a <c>read</c> id, joined by session, and <c>READ_LEAVE</c>; and through two of the
/// window kit's own <see cref="IpcHostBridge"/>s, one per window, dispatching to the real <see cref="DriverModule"/>.
/// </summary>
/// <remarks>
/// git is stood in for at the gate (<see cref="ProcessStarts.Within"/>) by a node that waits until the test releases it, then runs the
/// real git with git's own arguments in the fixture's repository, so a released read answers a real review. Each stand-in's pid is
/// watched through a handle of the test's own. The fixtures live under the repository's gitignored <c>_fixtures/</c>.
/// 🔴 Nothing here reaches a network.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class DriverModuleReviewLeaveTests() : DriverModuleBridge("review-leave"), IDisposable
{
    private const string Session = "s1a2b3c4";

    private readonly List<Process> _watched = [];
    private string? _released;

    // The stand-ins first, then the home: a stand-in still running holds the tree.
    void IDisposable.Dispose()
    {
        foreach (var watched in _watched)
        {
            try
            {
                if (!watched.HasExited) watched.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException) { /* gone */ }

            watched.Dispose();
        }

        base.Dispose();
    }

    [Fact]
    public async Task The_page_s_leave_kills_the_waiting_git_and_nothing_is_answered()
    {
        var module = await ComeUpAsync();
        using var stand = StandIn();

        var asked = AskAsync(module, "SESSION_DIFF", new { id = Session, read = "r1" });
        var git = await FirstStartAsync();

        var left = await AnswerAsync(module, "READ_LEAVE", new { read = "r1" });
        Assert.True(left.GetProperty("left").GetBoolean());

        // The caller is released at once, as today; the owner's cleanup follows: the git the read started has exited.
        var answer = await asked.WaitAsync(Poll.Patience);
        Assert.False(answer.Success);
        Assert.Equal(IpcErrorCodes.OperationCancelled, answer.Error?.Code);
        await git.WaitForExitAsync().WaitAsync(Poll.Patience);
        Assert.Single(Starts);
        Assert.Equal(0, module.Reads.Waiting);
    }

    [Fact]
    public async Task A_second_window_still_waiting_keeps_the_read_until_it_leaves_too()
    {
        var module = await ComeUpAsync();
        using var stand = StandIn();

        var first = AskAsync(module, "SESSION_DIFF", new { id = Session, read = "r1" });
        var git = await FirstStartAsync();
        var second = AskAsync(module, "SESSION_DIFF", new { id = Session, read = "r2" });
        await WaitingAsync(module, 2);
        // Joined by session, the read ids kept out of the flight's key: one read, one git.
        Assert.Single(Starts);

        Assert.True((await AnswerAsync(module, "READ_LEAVE", new { read = "r1" })).GetProperty("left").GetBoolean());
        Assert.Equal(IpcErrorCodes.OperationCancelled, (await first.WaitAsync(Poll.Patience)).Error?.Code);
        await WaitingAsync(module, 1);
        // The flight decides as the first leaves, and another waits, so it is not stopped: its git still runs.
        Assert.False(git.WaitForExit(TimeSpan.FromSeconds(2)), "the read stopped while another window waited on it");

        Assert.True((await AnswerAsync(module, "READ_LEAVE", new { read = "r2" })).GetProperty("left").GetBoolean());
        Assert.Equal(IpcErrorCodes.OperationCancelled, (await second.WaitAsync(Poll.Patience)).Error?.Code);
        await git.WaitForExitAsync().WaitAsync(Poll.Patience);
    }

    [Fact]
    public async Task A_window_destroyed_mid_read_leaves_it_with_no_script_and_its_git_ends()
    {
        var module = await ComeUpAsync();
        using var stand = StandIn();
        var window = Window(module);

        var asked = window.HandleIncomingAsync(Request("w1", "SESSION_DIFF", new { id = Session, read = "r1" }));
        var git = await FirstStartAsync();
        await WaitingAsync(module, 1);

        // What the window's destruction does to its bridge (the git-app design P1.6's reading of the kit): no READ_LEAVE is sent.
        window.Dispose();

        Assert.Contains(IpcErrorCodes.OperationCancelled, await asked.WaitAsync(Poll.Patience));
        await git.WaitForExitAsync().WaitAsync(Poll.Patience);
        Assert.Equal(0, module.Reads.Waiting);
    }

    [Fact]
    public async Task A_read_both_windows_wait_on_survives_the_first_window_s_destruction_and_answers_the_second()
    {
        var module = await ComeUpAsync();
        using var stand = StandIn();
        var one = Window(module);
        using var two = Window(module);

        var first = one.HandleIncomingAsync(Request("w1", "SESSION_DIFF", new { id = Session, read = "r1" }));
        var git = await FirstStartAsync();
        var second = two.HandleIncomingAsync(Request("w2", "SESSION_DIFF", new { id = Session, read = "r2" }));
        await WaitingAsync(module, 2);

        one.Dispose();
        Assert.Contains(IpcErrorCodes.OperationCancelled, await first.WaitAsync(Poll.Patience));
        await WaitingAsync(module, 1);
        Assert.False(git.HasExited, "the read stopped while another window waited on it");

        // Let git answer: the second window is answered the session's review, read once.
        Release();
        using var answer = JsonDocument.Parse((await second.WaitAsync(Poll.Patience))!);
        var response = answer.RootElement;
        Assert.True(response.GetProperty("success").GetBoolean(), response.GetRawText());
        Assert.Equal(Session, response.GetProperty("data").GetProperty("session").GetString());
        Assert.Contains(response.GetProperty("data").GetProperty("files").EnumerateArray(), file => file.GetProperty("path").GetString() == "work.txt");
    }

    // The fixture: a repository whose one commit after the session's base is the session's work.
    private string _tree = "";
    private string _base = "";

    private async Task<DriverModule> ComeUpAsync()
    {
        _tree = Path.Combine(Home, "tree");
        Directory.CreateDirectory(_tree);
        Git("init", "-q", "-b", "main");
        Git("-c", "user.name=t", "-c", "user.email=t@example.invalid", "commit", "-q", "--allow-empty", "-m", "base");
        _base = Git("rev-parse", "HEAD").Trim();
        File.WriteAllText(Path.Combine(_tree, "work.txt"), "the session's work\n");
        Git("add", "work.txt");
        Git("-c", "user.name=t", "-c", "user.email=t@example.invalid", "commit", "-q", "-m", "work");

        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(new Sessions(_tree, _base))));
        return module;
    }

    private string Git(params string[] arguments)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = _tree, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var git = Process.Start(info)!;
        var output = git.StandardOutput.ReadToEnd();
        var error = git.StandardError.ReadToEnd();
        git.WaitForExit();
        Assert.True(git.ExitCode == 0, $"git {string.Join(' ', arguments)}: {error}");
        return output;
    }

    private readonly TaskCompletionSource<Process> _first = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private IReadOnlyList<Process> Starts
    {
        get
        {
            lock (_watched) return [.. _watched];
        }
    }

    private Task<Process> FirstStartAsync() => _first.Task.WaitAsync(Poll.Patience);

    private void Release() => File.WriteAllText(_released!, "go");

    /// <summary>Every git this flow starts made by the stand-in: a node that waits for the release, then runs git as asked.</summary>
    private IDisposable StandIn()
    {
        _released = Path.Combine(Home, "released");
        return ProcessStarts.Within(info =>
        {
            var stand = new ProcessStartInfo("node")
            {
                RedirectStandardOutput = info.RedirectStandardOutput,
                RedirectStandardError = info.RedirectStandardError,
                RedirectStandardInput = info.RedirectStandardInput,
                StandardOutputEncoding = info.StandardOutputEncoding,
                StandardErrorEncoding = info.StandardErrorEncoding,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = info.WorkingDirectory,
            };
            foreach (var (name, value) in info.Environment) stand.Environment[name] = value;
            stand.ArgumentList.Add("-e");
            stand.ArgumentList.Add(
                "const fs=require('fs'),cp=require('child_process');const [released,git,...args]=process.argv.slice(1);"
                + "const go=()=>{if(!fs.existsSync(released))return setTimeout(go,50);"
                + "const r=cp.spawnSync(git,args,{stdio:'inherit'});process.exit(r.status===null?1:r.status)};go()");
            stand.ArgumentList.Add(_released);
            stand.ArgumentList.Add(info.FileName);
            foreach (var argument in info.ArgumentList) stand.ArgumentList.Add(argument);
            var process = Process.Start(stand)!;
            var watched = Process.GetProcessById(process.Id);
            lock (_watched) _watched.Add(watched);
            _first.TrySetResult(watched);
            return process;
        });
    }

    private static async Task WaitingAsync(DriverModule module, int count)
    {
        var until = DateTime.UtcNow + Poll.Patience;
        while (module.Reads.Waiting != count)
        {
            Assert.True(DateTime.UtcNow < until, $"{module.Reads.Waiting} caller(s) wait on the read, never {count}");
            await Task.Delay(10);
        }
    }

    /// <summary>One window's bridge, the kit's own, dispatching to the module as the application's dispatcher does.</summary>
    private static IpcHostBridge Window(DriverModule module) =>
        new(new IpcHostBridgeOptions { Dispatcher = new MessageDispatcher().MapModule(module) });

    private static string Request(string id, string type, object payload) =>
        JsonSerializer.Serialize(new { id, module = DriverModule.Name, type, payload });

    /// <summary>The service, standing in: one session whose record names the fixture's tree and its base; nothing else.</summary>
    private sealed class Sessions(string tree, string baseCommit) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(
                    request.RequestUri!.AbsolutePath == "/api/sessions"
                        ? JsonSerializer.Serialize(new[] { new { id = Session, tree, baseCommit, repository = "engine", state = "ended" } })
                        : "[]",
                    System.Text.Encoding.UTF8, "application/json"),
            });
    }
}

/// <summary>
/// <c>READ_LEAVE</c>'s answers that need no process (GITAPP1a-iii): a leave for an id never asked, or already answered, does nothing.
/// </summary>
public sealed class DriverModuleReadLeaveTests : DriverModuleBridge
{
    [Fact]
    public async Task A_leave_for_an_id_never_asked_or_already_answered_does_nothing()
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(new Nobody())));

        Assert.False((await AnswerAsync(module, "READ_LEAVE", new { read = "never" })).GetProperty("left").GetBoolean());

        // A session the service does not know is refused before any git: answered, so its id is dropped.
        var refused = await AskAsync(module, "SESSION_DIFF", new { id = "s9z9z9z9", read = "r1" });
        Assert.False(refused.Success);
        Assert.NotEqual(IpcErrorCodes.OperationCancelled, refused.Error?.Code);
        Assert.False((await AnswerAsync(module, "READ_LEAVE", new { read = "r1" })).GetProperty("left").GetBoolean());
        Assert.Equal(0, module.Reads.Waiting);
    }

    [Fact]
    public async Task A_leave_naming_no_read_is_a_malformed_call()
    {
        await Assert.ThrowsAnyAsync<Exception>(() => AnswerAsync(Module(), "READ_LEAVE", new { }));
    }

    private sealed class Nobody : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json"),
            });
    }
}
