using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>daoris chat</c> as built, holding a conversation end to end (RPC1g2, D238, the terminal-conversation design §5): the host's
/// process started as a person starts it, scripted on its standard input, against a stand-in of the running Daoris's pipe that names
/// the conversation, forwards its lines and ends it. What the process says and how it ends are read out; nothing of it runs the
/// conversation, which is the claim. It replaces the family rehearsal's headless step.
/// </summary>
/// <remarks>The home is a scratch folder in the repository's gitignored <c>_fixtures/</c>; every <c>DAORIS_</c> variable of the machine is dropped from the process.</remarks>
[Trait(Category.Name, Category.Process)]
public sealed class HeldChatProcessTests : IAsyncDisposable
{
    private readonly string _scratch = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "held-chat", Guid.NewGuid().ToString("N")[..8]);

    public async ValueTask DisposeAsync()
    {
        await Task.CompletedTask;
        GitSeed.Discard(_scratch);
    }

    [Fact]
    public async Task A_scripted_chat_is_held_in_the_running_Daoris_and_ends_with_how_the_conversation_ended()
    {
        if (!OperatingSystem.IsWindows()) return;
        var home = Path.Combine(_scratch, "home");
        Directory.CreateDirectory(home);
        var id = HomeIdentity.Ensure(home)!;
        var pipe = PipeName.For(id, Random.Shared.Next(100_000, 999_999));
        await using var daoris = new InstanceStandIn(pipe, id)
        {
            Respond = (type, _) => type switch
            {
                "START_CHAT" => new { sessionId = "c1", message = "conversation c1 started." },
                "TAIL_SESSION" => new { session = "c1", lines = Array.Empty<object>(), sequence = 0, live = true, dropped = 0 },
                "SESSION_INPUT" => new { said = new { @out = Array.Empty<string>(), error = Array.Empty<string>(), code = 0 } },
                _ => new { },
            },
        };

        var info = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = home,
        };
        info.ArgumentList.Add(DriverHostTests.HostDll());
        foreach (var arg in new[] { "chat", "--repository", "newcomer", "--agent", "stub" }) info.ArgumentList.Add(arg);
        foreach (var name in info.Environment.Keys.Where(key => key.StartsWith("DAORIS_", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            info.Environment.Remove(name);
        }

        info.Environment["DAORIS_HOME"] = home;
        info.Environment["DAORIS_INSTANCE"] = pipe;

        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        try
        {
            await UntilAsync(() => Sent(daoris, "TAIL_SESSION") > 0, "the process holds the conversation it started");
            await daoris.PushAsync("DAORIS", "SESSION_OUTPUT", new { session = "c1", lines = new[] { new { sequence = 1L, text = "chat: listening in newcomer" } }, live = true });

            await process.StandardInput.WriteLineAsync("what is this repository for?");
            await UntilAsync(() => Sent(daoris, "SESSION_INPUT") > 0, "the line is said to the conversation");
            await daoris.PushAsync("DAORIS", "SESSION_OUTPUT", new { session = "c1", lines = new[] { new { sequence = 2L, text = "chat heard: what is this repository for?" } }, live = true });

            process.StandardInput.Close();
            await UntilAsync(() => Sent(daoris, "END_CHAT") > 0, "the end of input finishes the conversation");
            await daoris.PushAsync("DAORIS", "SESSION_ENDED", new { session = "c1", state = "completed" });

            using var bound = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            await process.WaitForExitAsync(bound.Token);
        }
        catch
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            throw;
        }

        var said = await output;
        var told = await error;
        Assert.True(process.ExitCode == 0, $"{said}\n{told}");
        Assert.Contains("chat: listening in newcomer", said);
        Assert.Contains("chat heard: what is this repository for?", said);
        Assert.Contains("chat: session c1 is completed.", told);

        // The process ran nothing: the whole of it is requests on the pipe, the start with its hold.
        var start = Assert.Single(daoris.Sent, each => Is(each, "START_CHAT"));
        Assert.True(start.GetProperty("hold").GetBoolean());
        Assert.Equal("newcomer", start.GetProperty("payload").GetProperty("repository").GetString());
        Assert.Equal("stub", start.GetProperty("payload").GetProperty("adapter").GetString());
        Assert.Equal(
            "what is this repository for?",
            Assert.Single(daoris.Sent, each => Is(each, "SESSION_INPUT")).GetProperty("payload").GetProperty("text").GetString());
        var requests = DaorisHome.Entry(home, HomeEntry.Requests);
        Assert.False(Directory.Exists(requests) && Directory.EnumerateFiles(requests).Any());
    }

    [Fact]
    public async Task With_no_Daoris_running_chat_says_so_exits_2_and_runs_nothing()
    {
        var home = Path.Combine(_scratch, "home-none");
        Directory.CreateDirectory(home);
        var info = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = home,
        };
        info.ArgumentList.Add(DriverHostTests.HostDll());
        foreach (var arg in new[] { "chat", "--repository", "newcomer", "--agent", "stub" }) info.ArgumentList.Add(arg);
        foreach (var name in info.Environment.Keys.Where(key => key.StartsWith("DAORIS_", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            info.Environment.Remove(name);
        }

        info.Environment["DAORIS_HOME"] = home;

        using var process = Process.Start(info)!;
        var told = process.StandardError.ReadToEndAsync();
        var said = process.StandardOutput.ReadToEndAsync();
        process.StandardInput.Close();
        using var bound = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        await process.WaitForExitAsync(bound.Token);

        Assert.Equal(2, process.ExitCode);
        Assert.Contains("chat: Daoris is not running on this home; open it, then run this again.", await told);
        Assert.Equal("", await said);
        Assert.DoesNotContain(Directory.EnumerateFileSystemEntries(home, "*", SearchOption.AllDirectories), each => each.EndsWith(".turn", StringComparison.Ordinal));
    }

    private static int Sent(InstanceStandIn daoris, string type) => daoris.Sent.Count(each => Is(each, type));

    private static bool Is(JsonElement request, string type) => request.GetProperty("type").GetString() == type;

    private static async Task UntilAsync(Func<bool> done, string what)
    {
        var until = DateTime.UtcNow.AddSeconds(90);
        while (!done())
        {
            if (DateTime.UtcNow > until) throw new TimeoutException($"never: {what}");
            await Task.Delay(25);
        }
    }
}
