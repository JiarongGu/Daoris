using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// GITLOCK1 over real git and other processes (D51's note of the row): every writer of a repository's checkout holds the checkout alone
/// (<see cref="TreeLock.TryWriting"/>) from its fresh judgement through its write and its read after it. The other process is a PowerShell
/// that opens the lock file as a Daoris process does, the file being the contract as <see cref="RecordLockProcessTests"/>' is, or
/// <c>daoris-driver</c> itself. While another holds the checkout, a line's pull, a promotion and a merge rule's landing each leave it as it
/// was, said; once it lets go, or dies, each moves it, and at the move another process is refused the lock both ways, alone and shared. A
/// writer holding it waits for no lock but a process start's place and a record's. Not in the git half: it starts PowerShell and the host.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class CheckoutLockProcessTests : TreeSyncFixture
{
    public CheckoutLockProcessTests() =>
        DriverConfig.Empty.WithLanding("engine", LandingRule.Merge).Save(DaorisHome.Entry(Home, HomeEntry.DriverConfig));

    private string TreesHome => DaorisHome.Entry(Home, HomeEntry.Trees);

    /// <summary>Another process holding a checkout alone, as a Daoris process does, until it is told to let go or is killed.</summary>
    private sealed class Holder : IAsyncDisposable
    {
        private readonly Process _process;
        private bool _killed;

        private Holder(Process process) => _process = process;

        public static async Task<Holder> TakeAsync(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var script = $$"""
                $lock = [System.IO.File]::Open('{{path}}', 'OpenOrCreate', 'ReadWrite', 'None')
                [Console]::Out.WriteLine('held'); [Console]::Out.Flush()
                [void][Console]::In.ReadLine()
                $lock.Dispose()
                """;
            var process = Process.Start(new ProcessStartInfo("powershell", ["-NoProfile", "-NonInteractive", "-Command", script])
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
            })!;
            Assert.Equal("held", await process.StandardOutput.ReadLineAsync());
            return new Holder(process);
        }

        /// <summary>The holder dies holding it, as a process killed mid-write does.</summary>
        public async Task DieAsync()
        {
            _killed = true;
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync();
        }

        public async ValueTask DisposeAsync()
        {
            if (!_process.HasExited)
            {
                await _process.StandardInput.WriteLineAsync();
                _process.StandardInput.Close();
                await _process.WaitForExitAsync();
            }

            if (!_killed) Assert.True(_process.ExitCode == 0, await _process.StandardError.ReadToEndAsync());
            _process.Dispose();
        }
    }

    /// <summary>
    /// What another process is answered at each <c>merge --ff-only</c> this flow starts, asked at that very start: whether it could take the
    /// checkout alone, then shared, as <c>taken</c> or <c>refused</c> each.
    /// </summary>
    private ConcurrentQueue<string> ProbedAtEachMove(string root)
    {
        var probed = new ConcurrentQueue<string>();
        var path = TreeLock.PathOf(Home, root);
        WorkingTree.Started.Value = arguments =>
        {
            if (arguments is not ["merge", "--ff-only", ..]) return;
            var script = $$"""
                foreach ($ask in @(@('ReadWrite', 'None'), @('Read', 'Read'))) {
                  try { $lock = [System.IO.File]::Open('{{path}}', 'OpenOrCreate', $ask[0], $ask[1]); $lock.Dispose(); [Console]::Out.WriteLine('taken') }
                  catch { [Console]::Out.WriteLine('refused') }
                }
                """;
            using var probe = Process.Start(new ProcessStartInfo("powershell", ["-NoProfile", "-NonInteractive", "-Command", script])
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            })!;
            var errors = probe.StandardError.ReadToEndAsync();
            var said = probe.StandardOutput.ReadToEnd();
            probe.WaitForExit();
            errors.GetAwaiter().GetResult();
            probed.Enqueue(string.Join(" ", said.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)));
        };
        return probed;
    }

    /// <summary>Each wait on a lock this flow makes while it holds a checkout alone (<see cref="TreeLock.Waits"/>).</summary>
    private static ConcurrentQueue<string> WaitsUnderTheHold()
    {
        var waits = new ConcurrentQueue<string>();
        TreeLock.WaitedUnder.Value = waits.Enqueue;
        return waits;
    }

    /// <summary>🔴 Under its hold a writer waits only for locks whose holders wait for nothing, and it was seen waiting for one.</summary>
    private static void OnlyLeavesWaited(ConcurrentQueue<string> waits)
    {
        Assert.Contains(TreeLock.StartsWait, waits);
        Assert.All(waits, what => Assert.Contains(what, new[] { TreeLock.StartsWait, TreeLock.RecordsWait }));
    }

    /// <summary>The checkout as a person sees it: where HEAD and the line stand, the branch it has out, and what git says is changed.</summary>
    private static async Task<(string Head, string Line, string Out, string Status)> CheckoutAsync(string root) =>
        (await RevAsync(root, "HEAD"), await RevAsync(root, "main"), (await GitAsync(root, "rev-parse", "--abbrev-ref", "HEAD")).Trim(),
            (await GitAsync(root, "status", "--porcelain")).Trim());

    /// <summary>A branch from <c>main</c> with one commit, the checkout left on <c>main</c>; the branch's tip.</summary>
    private static async Task<string> BranchAsync(string root, string name, string file, string content)
    {
        await GitAsync(root, "checkout", "--quiet", "-b", name, "main");
        await CommitAsync(root, file, content, $"work on {name}");
        var tip = await RevAsync(root, name);
        await GitAsync(root, "checkout", "--quiet", "main");
        return tip;
    }

    /// <summary>A request for <paramref name="branch"/> queued for <c>main</c> with no gates, and the asks the transport hands its line's run.</summary>
    private (IntegrationStore Store, IntegrationSweepAsks Asks) Queued(string root, string branch, string tip)
    {
        var store = new IntegrationStore(Home, () => StoreSettled.Yes, null, "run-a", StoreProcess.Shell);
        Assert.Null(store.Add(new IntegrationRequest(
            "ra", "engine", "aurora", branch, tip, IntegrationRoutes.Recorded, "main", IntegrationRequester.Card,
            new LandingIntegrate(Gates: LandingIntegrate.NoGates).Resolved(), DateTimeOffset.UtcNow)));
        return (store, new IntegrationSweepAsks(
            new IntegrationReplayAsk("engine", "aurora", root, "main") { Config = DriverConfig.Empty },
            new IntegrationPromoteAsk("engine", "aurora", root, "main", DriverConfig.Empty, Home)));
    }

    /// <summary>What a writer says of a holder that named nothing, as another process holding the file does.</summary>
    private const string Unnamed = "a session was starting in `engine`, or another of Daoris's writes held it";

    /// <summary>🔴 D109's pull: left, said, while another process holds the checkout; fast-forwarded once it lets go, holding it alone.</summary>
    [Fact]
    public async Task A_line_s_pull_leaves_the_checkout_while_another_process_holds_it_and_holds_it_alone_as_it_moves()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        await PlatformCommitAsync(origin, "other.txt", "other\n", "Someone else's (#8)");
        var trees = new SessionTrees(Home);
        var before = await CheckoutAsync(root);

        await using (await Holder.TakeAsync(TreeLock.PathOf(Home, root)))
        {
            var held = (await trees.SyncAsync(Repositories(root), Nobody, fetch: true, scope: SyncScope.Everything)).Lines.Single();

            Assert.False(held.Moved, held.Message);
            Assert.Equal($"{Unnamed}, so `main` was left for another look.", held.Message);
            Assert.Equal(before, await CheckoutAsync(root));
        }

        var probed = ProbedAtEachMove(root);
        var waits = WaitsUnderTheHold();
        var moved = (await trees.SyncAsync(Repositories(root), Nobody, scope: SyncScope.Everything)).Lines.Single();

        Assert.True(moved.Moved, moved.Message);
        Assert.Equal(await RevAsync(root, "origin/main"), await RevAsync(root, "main"));
        var after = await CheckoutAsync(root);
        Assert.Equal((after.Line, "main", ""), (after.Head, after.Out, after.Status));
        Assert.Equal(["refused refused"], probed);
        OnlyLeavesWaited(waits);
    }

    /// <summary>🔴 The integration's promotion, as the transport runs a line: held, said, with nothing moved; promoted once the other lets go.</summary>
    [Fact]
    public async Task A_promotion_leaves_the_checkout_while_another_process_holds_it_and_holds_it_alone_as_it_moves()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await RepositoryAsync("engine");
        var tip = await BranchAsync(root, "feature/a", "a.txt", "a\n");
        var (store, asks) = Queued(root, "feature/a", tip);
        var lines = new IntegrationLines(store, TreesHome);
        var before = await CheckoutAsync(root);

        await using (await Holder.TakeAsync(TreeLock.PathOf(Home, root)))
        {
            var held = await lines.RunAsync(asks);

            Assert.Null(store.Request("ra")!.ClosedAt);
            Assert.Contains(held.Said, said => said.Contains($"{Unnamed}: nothing was moved", StringComparison.Ordinal));
            Assert.Equal(before, await CheckoutAsync(root));
        }

        var probed = ProbedAtEachMove(root);
        var waits = WaitsUnderTheHold();
        await lines.RunAsync(asks);

        Assert.Equal(IntegrationRequestCloses.Settled, store.Request("ra")!.ClosedWhy);
        var after = await CheckoutAsync(root);
        Assert.NotEqual(before.Line, after.Line);
        Assert.Equal((after.Line, "main", ""), (after.Head, after.Out, after.Status));
        Assert.Equal("a", (await File.ReadAllTextAsync(Path.Combine(root, "a.txt"))).Trim());
        Assert.Equal(["refused refused"], probed);
        OnlyLeavesWaited(waits);
    }

    /// <summary>
    /// 🔴 A merge rule's landing, the legacy merge: it writes the checkout through the promotion's effect alone, so it is left, said, while
    /// another process holds the checkout, and its press again goes on with the same request and merges. It takes no hold of its own: one it
    /// took around the promotion would refuse the promotion's, in this very process.
    /// </summary>
    [Fact]
    public async Task A_merge_landing_leaves_the_checkout_while_another_process_holds_it_and_takes_the_hold_once_as_it_merges()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, "budget.ts", "cap the work per frame\n", "cap hydration");
        var before = await CheckoutAsync(root);

        await using (await Holder.TakeAsync(TreeLock.PathOf(Home, root)))
        {
            var held = await trees.MergeAsync(opened.Path);

            Assert.False(held.Merged, held.Message);
            Assert.Contains($"{Unnamed}: nothing was moved", held.Message, StringComparison.Ordinal);
            Assert.Equal(before, await CheckoutAsync(root));
        }

        var probed = ProbedAtEachMove(root);
        var waits = WaitsUnderTheHold();
        var merged = await trees.MergeAsync(opened.Path);

        Assert.True(merged.Merged, merged.Message);
        Assert.Contains("cap hydration", await GitAsync(root, "log", "--oneline", "main"));
        var after = await CheckoutAsync(root);
        Assert.Equal((after.Line, "main", ""), (after.Head, after.Out, after.Status));
        Assert.Equal(["refused refused"], probed);
        OnlyLeavesWaited(waits);
    }

    /// <summary>
    /// 🔴 A holder that dies holding the checkout lets go with its process: the handle is the lock, so nothing stale is left to wait out,
    /// and the next writer takes it at once.
    /// </summary>
    [Fact]
    public async Task A_holder_that_dies_holding_the_checkout_lets_go_of_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await RepositoryAsync("engine");
        await using var other = await Holder.TakeAsync(TreeLock.PathOf(Home, root));
        Assert.Null(TreeLock.TryWriting(Home, root, "engine", TreeLock.Updating));

        await other.DieAsync();

        using var writing = TreeLock.TryWriting(Home, root, "engine", TreeLock.Updating);
        Assert.NotNull(writing);
        Assert.Equal(TreeLock.Updating, TreeLock.HolderOf(Home, root));
    }

    /// <summary>
    /// 🔴 The desktop and a verb: while this process writes the checkout, <c>daoris-driver trees sync --yes</c> in its own process leaves the
    /// line, naming what this process said it holds it for; once this one lets go, the verb pulls it.
    /// </summary>
    [Fact]
    public async Task A_driver_verb_leaves_the_line_while_this_process_writes_the_checkout_and_pulls_it_once_let_go()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        await PlatformCommitAsync(origin, "other.txt", "other\n", "Someone else's (#8)");
        await using var service = Ledger.Start(root);
        var before = await CheckoutAsync(root);

        using (var writing = TreeLock.TryWriting(Home, root, "engine", TreeLock.Promoting))
        {
            Assert.NotNull(writing);
            var (code, said) = await VerbAsync(service, "trees", "sync", "--repository", "engine", "--yes");

            Assert.True(code == 1, said);
            Assert.Contains("`engine`'s line was being moved to what its integration promoted, so `main` was left for another look.", said);
            Assert.Equal(before, await CheckoutAsync(root));
        }

        var (pulled, words) = await VerbAsync(service, "trees", "sync", "--repository", "engine", "--yes");
        Assert.True(pulled == 0, words);
        Assert.Equal(await RevAsync(root, "origin/main"), await RevAsync(root, "main"));
    }

    /// <summary>
    /// 🔴 The order: the hold is tried and never waited for, so a writer that meets it inside another's write answers at once rather than wait
    /// for it. A line's pull pressed while a promotion holds the checkout, at the moment before its move, is left, naming the promotion, within
    /// moments, and the promotion then moves the line; nothing deadlocks.
    /// </summary>
    [Fact]
    public async Task A_writer_meeting_another_s_hold_answers_at_once_and_the_holder_finishes()
    {
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        var tip = await BranchAsync(root, "feature/a", "a.txt", "a\n");
        await PlatformCommitAsync(origin, "other.txt", "other\n", "Someone else's (#8)");
        await GitAsync(root, "fetch", "--quiet", "origin");
        var (store, asks) = Queued(root, "feature/a", tip);
        var formed = await new IntegrationReplays(store, TreesHome).RunAsync(asks.Replay);
        var trees = new SessionTrees(Home);
        PullResult? inside = null;
        var promotes = new IntegrationPromotes(store, TreesHome)
        {
            Seam = async at =>
            {
                if (at != "before-move") return;
                var done = await trees.SyncAsync(Repositories(root), Nobody, scope: SyncScope.Everything).WaitAsync(TimeSpan.FromSeconds(60));
                inside = done.Lines.Single();
            },
        };

        var run = await promotes.RunAsync(formed.Claim!, asks.Promote, formedHere: true).WaitAsync(TimeSpan.FromMinutes(2));

        Assert.NotNull(inside);
        Assert.False(inside!.Moved, inside.Message);
        Assert.Equal("`engine`'s line was being moved to what its integration promoted, so `main` was left for another look.", inside.Message);
        Assert.Equal(IntegrationPromoteRuns.Settled, run.Kind);
        var after = await CheckoutAsync(root);
        Assert.Equal((store.Candidate(formed.Claim!.Candidate)!.Tip, "main", ""), (after.Line, after.Out, after.Status));
    }

    /// <summary>
    /// 🔴 Two writers racing on one checkout, a promotion and a line's pull begun together: both answer, whichever held it first, and the
    /// checkout is left whole, on its line with nothing changed in it.
    /// </summary>
    [Fact]
    public async Task Two_writers_racing_on_one_checkout_both_answer_and_leave_it_whole()
    {
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        var tip = await BranchAsync(root, "feature/a", "a.txt", "a\n");
        await PlatformCommitAsync(origin, "other.txt", "other\n", "Someone else's (#8)");
        await GitAsync(root, "fetch", "--quiet", "origin");
        var (store, asks) = Queued(root, "feature/a", tip);
        var trees = new SessionTrees(Home);
        var before = await RevAsync(root, "main");

        var promoted = new IntegrationLines(store, TreesHome).RunAsync(asks);
        var pulled = trees.SyncAsync(Repositories(root), Nobody, scope: SyncScope.Everything);
        await Task.WhenAll(promoted, pulled).WaitAsync(TimeSpan.FromMinutes(3));

        var after = await CheckoutAsync(root);
        Assert.Equal((after.Line, "main", ""), (after.Head, after.Out, after.Status));
        Assert.NotEqual(before, after.Line);
    }

    /// <summary><c>daoris-driver</c> run against this home and <paramref name="service"/>, hermetic as <see cref="DriverHostTests"/>' runs are.</summary>
    private async Task<(int Code, string Said)> VerbAsync(Ledger service, params string[] args)
    {
        var info = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8, WorkingDirectory = Home,
        };
        info.ArgumentList.Add(DriverHostTests.HostDll());
        foreach (var arg in args) info.ArgumentList.Add(arg);
        foreach (var name in info.Environment.Keys.Where(key => key.StartsWith("DAORIS_", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            info.Environment.Remove(name);
        }

        info.Environment["DAORIS_HOME"] = Home;
        info.Environment["DAORIS_SERVICE_URL"] = service.Url;
        using var process = Process.Start(TestGit.Past(info))!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var bound = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        try
        {
            await process.WaitForExitAsync(bound.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"daoris-driver {string.Join(' ', args)} did not end: {await output}{await error}");
        }

        return (process.ExitCode, await output + await error);
    }

    /// <summary>The service's registry and ledger as the verb reads them, on a loopback port: one repository, no session, no quest.</summary>
    private sealed class Ledger : IAsyncDisposable
    {
        private readonly HttpListener _listener;
        private readonly Task _serving;
        private readonly string _root;

        public string Url { get; }

        private Ledger(HttpListener listener, string url, string root)
        {
            (_listener, Url, _root) = (listener, url.TrimEnd('/'), root);
            _serving = ServeAsync();
        }

        public static Ledger Start(string root)
        {
            var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            var url = $"http://127.0.0.1:{port}/";
            var listener = new HttpListener();
            listener.Prefixes.Add(url);
            listener.Start();
            return new Ledger(listener, url, root);
        }

        private async Task ServeAsync()
        {
            while (_listener.IsListening)
            {
                HttpListenerContext context;
                try { context = await _listener.GetContextAsync(); }
                catch (Exception error) when (error is HttpListenerException or ObjectDisposedException or InvalidOperationException) { return; }

                var (status, body) = (context.Request.HttpMethod, context.Request.Url!.AbsolutePath) switch
                {
                    ("GET", "/api/registry") => (200, new JsonArray(new JsonObject
                    {
                        ["repository"] = "engine", ["adopted"] = true, ["registered"] = true, ["root"] = _root, ["workspace"] = "aurora",
                    }).ToJsonString()),
                    ("GET", _) => (200, "[]"),
                    _ => (404, "{}"),
                };
                var bytes = Encoding.UTF8.GetBytes(body);
                context.Response.StatusCode = status;
                context.Response.ContentType = "application/json";
                await context.Response.OutputStream.WriteAsync(bytes);
                context.Response.Close();
            }
        }

        public async ValueTask DisposeAsync()
        {
            _listener.Stop();
            _listener.Close();
            await _serving;
        }
    }
}
