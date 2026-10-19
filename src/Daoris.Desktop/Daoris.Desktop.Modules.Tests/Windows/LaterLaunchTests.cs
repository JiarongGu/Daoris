using Daoris.Driver;
using Microsoft.Extensions.DependencyInjection;
using Shenora;
using Shenora.Core.Shell;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// ONEINSTANCE2 (D171 point 2): a later launch of the installed Daoris opened no window and wrote nothing, and ONEINSTANCE1's
/// only proof had been that its wiring's words were in the application's source. Here the options the application hands the
/// kit (<see cref="LaterLaunch.Options"/>) are run as the kit's WinForms shell runs them: the kit's own single-instance guard
/// holds the scope on a thread of its own, a later launch hands itself over the guard's channel, and the running side's
/// callback reaches <see cref="SecondLaunch"/> in a composed container, with only the window stubbed. What a test cannot run is
/// the shell's half on the window's thread (bringing the first forward, then the callback), which the installed window proves.
/// </summary>
public sealed class LaterLaunchTests : IDisposable
{
    private const string Service = "http://localhost:5177";

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-laterlaunch-" + Guid.NewGuid().ToString("N")[..8]);

    // Each test's own instance name, so no two tests share a scope or a channel.
    private readonly string _name = "DaorisLaterLaunch" + Guid.NewGuid().ToString("N")[..8];

    public LaterLaunchTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>The shell's half, stubbed: what was asked for, or a window that could not be made.</summary>
    private sealed class Windows(Exception? failing = null) : ISecondaryWindows
    {
        public readonly List<(string Name, string Address)> Asked = [];

        public bool Open(string name, string address)
        {
            if (failing is not null) throw failing;
            lock (Asked) Asked.Add((name, address));
            return true;
        }

        public IReadOnlyList<string> Opened
        {
            get
            {
                lock (Asked) return [.. Asked.Select(asked => asked.Name)];
            }
        }

        public bool SetTheme(string name, bool dark) => true;
    }

    /// <summary>
    /// The running instance's side of the kit's guard: it holds the scope from a thread of its own, as a running process holds
    /// it from its main thread (the scope's mutex is the thread's), and hands each later launch to <paramref name="activated"/>
    /// on the channel's thread, where the WinForms shell would post it to the window's.
    /// </summary>
    private sealed class Running : IDisposable
    {
        private readonly ManualResetEventSlim _done = new();
        private readonly Thread _thread;

        public Running(string name, string scope, Action<SingleInstanceLaunch> activated)
        {
            var listening = new ManualResetEventSlim();
            Exception? failure = null;
            _thread = new Thread(() =>
            {
                try
                {
                    using var guard = new SingleInstanceGuard(name, scope);
                    if (guard.TryAcquire() is not SingleInstanceResult.Acquired) throw new InvalidOperationException("the scope was taken");
                    guard.Listen(activated);
                    listening.Set();
                    _done.Wait();
                }
                catch (Exception error)
                {
                    failure = error;
                    listening.Set();
                }
            })
            { IsBackground = true, Name = "LaterLaunchTests.Running" };
            _thread.Start();
            listening.Wait();
            if (failure is not null) throw failure;
        }

        public void Dispose()
        {
            _done.Set();
            _thread.Join();
            _done.Dispose();
        }
    }

    /// <summary>A container as the application composes one, with this test's hand-over in it.</summary>
    private ShenoraApplication App(Action<IServiceCollection> compose)
    {
        var builder = ShenoraApplication.CreateBuilder(new ShenoraApplicationOptions
        {
            ApplicationName = _name,
            Paths = new ShenoraPathsOptions { ExplicitRoot = Path.Combine(_home, "install") },
        });
        compose(builder.Services);
        return builder.Build();
    }

    private string[] Lines()
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.Logs);
        if (!Directory.Exists(folder)) return [];
        return [.. Directory.GetFiles(folder).SelectMany(path =>
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        })];
    }

    private int Index(string @event) => Array.FindIndex(Lines(), line => line.Contains($"\"event\":\"{@event}\""));

    private static async Task UntilAsync(Func<bool> condition)
    {
        var patience = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < patience, "the condition never held");
            await Task.Delay(25);
        }
    }

    [Fact]
    public async Task A_launch_handed_over_the_kits_channel_opens_a_main_window_and_is_written_as_it_arrives()
    {
        using var log = new MachineLog(_home, "desktop");
        var windows = new Windows();
        using var app = App(services => services.AddSingleton(
            new SecondLaunch(windows, new PlatformAddress(Service), () => Task.FromResult(true), log)));
        var options = LaterLaunch.Options(log);
        var scope = Path.Combine(_home, "install");

        using (new Running(_name, scope, launch => options.OnActivated!(app, launch)))
        {
            using var later = new SingleInstanceGuard(_name, scope);
            Assert.Equal(SingleInstanceResult.AlreadyRunning, later.TryAcquire());
            Assert.Equal(SingleInstanceResult.AlreadyRunning,
                later.ActivateOrTakeOver(["--app-root", "D:/install"], TimeSpan.FromSeconds(5)));

            await UntilAsync(() => Index("window.launched") >= 0);
        }

        Assert.Equal(("main:2", "/?window=main%3A2&host=http%3A%2F%2Flocalhost%3A5177"), windows.Asked.Single());
        // Its arrival first, before anything was resolved or awaited, with how many arguments it carried and never them.
        var arrived = Index("window.launch");
        Assert.True(arrived >= 0, "the launch's arrival was not written");
        Assert.True(arrived < Index("window.launched"));
        Assert.Contains("\"arguments\":2", Lines()[arrived]);
        Assert.DoesNotContain("D:/install", string.Concat(Lines()));
    }

    /// <summary>
    /// A hand-over that cannot be made is a line, never an exception into the kit's callback, which the kit swallows: the
    /// container that holds no <see cref="SecondLaunch"/> stands for any resolution that throws.
    /// </summary>
    [Fact]
    public void A_hand_over_that_cannot_be_resolved_is_written_and_not_thrown()
    {
        using var log = new MachineLog(_home, "desktop");
        using var app = App(_ => { });

        LaterLaunch.Options(log).OnActivated!(app, new SingleInstanceLaunch([], _home));

        var lines = Lines();
        Assert.Contains(lines, line => line.Contains("\"event\":\"window.launch\"") && line.Contains("\"arguments\":0"));
        Assert.Contains(lines, line => line.Contains("\"level\":\"error\",\"event\":\"error\"")
            && line.Contains($"\"where\":\"{LaterLaunch.Where}\"") && line.Contains("System.InvalidOperationException"));
    }

    /// <summary>A window that could not be opened is written where it happened, not left to an unobserved task.</summary>
    [Fact]
    public async Task A_hand_over_whose_window_fails_is_written_and_not_left_unobserved()
    {
        using var log = new MachineLog(_home, "desktop");
        var windows = new Windows(new IOException("the window's thread would not start"));
        using var app = App(services => services.AddSingleton(
            new SecondLaunch(windows, new PlatformAddress(Service), () => Task.FromResult(true), log)));

        LaterLaunch.Options(log).OnActivated!(app, new SingleInstanceLaunch(["--startup-screen", "1"], _home));

        await UntilAsync(() => Index("error") >= 0);
        var failed = Lines()[Index("error")];
        Assert.Contains($"\"where\":\"{LaterLaunch.Where}\"", failed);
        Assert.Contains("the window's thread would not start", failed);
        Assert.True(Index("window.launch") < Index("error"));
        Assert.Equal(-1, Index("window.launched"));
    }
}
