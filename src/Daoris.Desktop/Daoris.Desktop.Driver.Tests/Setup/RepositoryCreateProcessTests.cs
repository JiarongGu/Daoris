using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>daoris repository create</c> as a person runs it: the host's own process (<c>daoris-driver</c>) over a stand-in service that keeps the
/// registry, a home and two checkouts under the repository's gitignored <c>_fixtures</c>, never OS temp. <c>--plan</c> writes nothing; the
/// press, with no running Daoris to confirm it, is the person's own terminal acting alone: it makes the folder with a real <c>git init</c>
/// and one empty commit, registers it, drives it, keeps its receipt, and says where it stopped when the set-up quest is not asked. Every
/// <c>DAORIS_</c> variable is dropped from the process's environment, so no run reaches this machine's own home or service.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Needs, Needed.Network)]
public sealed class RepositoryCreateProcessTests : IAsyncLifetime
{
    private readonly string _scratch = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "repository-create-process", Guid.NewGuid().ToString("N")[..8]);
    private StubService? _service;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_service is not null) await _service.DisposeAsync();
        GitSeed.Remove(_scratch);
    }

    private string Work(params string[] parts) => Path.Combine([_scratch, "aurora", .. parts]);

    /// <summary>A checkout whose commits carry no author of its own and whose <c>origin</c> is at the host the home's identity serves.</summary>
    private async Task<string> CheckoutAsync(string name)
    {
        var root = Work(name);
        Directory.CreateDirectory(root);
        await GitFixture.RunAsync(root, "init", "-q", "-b", "main");
        await GitFixture.RunAsync(root, "remote", "add", "origin", $"https://example.test/aurora/{name}.git");
        await GitFixture.RunAsync(root, "-c", "user.name=Seed", "-c", "user.email=seed@example.test", "commit", "--allow-empty", "-q", "-m", "seed");
        return root;
    }

    [Fact]
    public async Task The_verb_plans_with_nothing_written_then_makes_a_department_registers_it_and_drives_it()
    {
        var home = Path.Combine(_scratch, "home");
        Directory.CreateDirectory(home);
        GitIdentities.Change(home, _ =>
        [
            new GitIdentity("gi-0123456789ab", "Books", "example.test", GitIdentity.Https, "books", "Books Bot", "books@example.test", null, null, false, null, true),
        ]);
        _service = StubService.Start();
        foreach (var name in new[] { "atlas", "engine" }) _service.Register(name, await CheckoutAsync(name), "aurora");

        var planned = await RunAsync(home, "repository", "create", "ledger", "--workspace", "aurora", "--purpose", "keeps the books", "--plan");

        Assert.True(planned.Code == 0, planned.Said);
        Assert.Contains("repository create: `ledger` in `aurora`", planned.Said);
        Assert.Contains(Work("ledger"), planned.Said);
        Assert.Contains("Books Bot <books@example.test>", planned.Said);
        Assert.Contains("--plan: nothing was written, and nothing was asked.", planned.Said);
        Assert.False(Directory.Exists(Work("ledger")));
        Assert.DoesNotContain("ledger", _service.Names);

        var made = await RunAsync(home, "repository", "create", "ledger", "--workspace", "aurora", "--purpose", "keeps the books");

        // The stand-in takes no ask, so the press stops at its set-up quest, says so and leaves what it made.
        Assert.True(made.Code == 1 && made.Said.Contains("repository create: folder made; history made", StringComparison.Ordinal), made.Said);
        Assert.Contains("registered; driving on; set-up not asked", made.Said);
        Assert.Contains("press the same again to go on from `setup`", made.Said);
        Assert.Equal("main", (await GitFixture.GitAsync(Work("ledger"), "symbolic-ref", "--short", "HEAD")).Trim());
        Assert.Equal("1", (await GitFixture.GitAsync(Work("ledger"), "rev-list", "--count", "HEAD")).Trim());
        Assert.Equal("Books Bot|books@example.test", (await GitFixture.GitAsync(Work("ledger"), "log", "-1", "--format=%an|%ae")).Trim());
        Assert.Equal("", (await GitFixture.GitAsync(Work("ledger"), "remote")).Trim());
        Assert.Equal([".git"], Directory.EnumerateFileSystemEntries(Work("ledger")).Select(Path.GetFileName));
        var row = _service.Row("ledger");
        Assert.Equal((false, "aurora", PlaceKind.Git), ((bool)row["adopted"]!, (string)row["workspace"]!, (string)row["kind"]!));
        Assert.Equal(Path.GetFullPath(Work("ledger")), Path.GetFullPath((string)row["root"]!));
        Assert.Contains("ledger", DriverConfig.Load(DriverConfig.ResolvePath(home)).Drivable);
        Assert.Single(Directory.EnumerateFiles(Path.GetDirectoryName(new CreateReceipts(home).PathOf(new string('0', 64)))!, "*.json"));
        Assert.False(File.Exists(Work("ledger", "daoris.json")));

        // The same words again resume the receipt: its folder holds exactly its empty commit and its row is registered, so nothing is made
        // twice, and the press stops at the set-up again, as the stand-in still takes no ask.
        var commit = (await GitFixture.GitAsync(Work("ledger"), "rev-parse", "HEAD")).Trim();
        var again = await RunAsync(home, "repository", "create", "ledger", "--workspace", "aurora", "--purpose", "keeps the books");
        Assert.True(again.Code == 1 && again.Said.Contains("resumes the receipt", StringComparison.Ordinal), again.Said);
        Assert.Contains("set-up not asked", again.Said);
        Assert.Equal(commit, (await GitFixture.GitAsync(Work("ledger"), "rev-parse", "HEAD")).Trim());
        Assert.Equal(["ledger"], _service.Names.Where(name => name == "ledger"));
    }

    private async Task<(int Code, string Said)> RunAsync(string home, params string[] args)
    {
        var info = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = _scratch,
        };
        info.ArgumentList.Add(DriverHostTests.HostDll());
        foreach (var arg in args) info.ArgumentList.Add(arg);
        foreach (var name in info.Environment.Keys.Where(key => key.StartsWith("DAORIS_", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            info.Environment.Remove(name);
        }

        info.Environment["DAORIS_HOME"] = home;
        info.Environment["DAORIS_SERVICE_URL"] = _service!.Url;

        using var process = Process.Start(info)!;
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

    /// <summary>A service on a loopback port that keeps the registry it is told of and answers every other read empty and every other write refused.</summary>
    private sealed class StubService : IAsyncDisposable
    {
        private readonly HttpListener _listener;
        private readonly Task _serving;
        private readonly Dictionary<string, JsonObject> _rows = new(StringComparer.OrdinalIgnoreCase);

        private StubService(HttpListener listener, string url)
        {
            _listener = listener;
            Url = url.TrimEnd('/');
            _serving = ServeAsync();
        }

        public string Url { get; }

        public IReadOnlyList<string> Names { get { lock (_rows) return [.. _rows.Keys]; } }

        public JsonObject Row(string name)
        {
            lock (_rows) return _rows[name];
        }

        public void Register(string name, string root, string workspace)
        {
            lock (_rows)
            {
                _rows[name] = new JsonObject
                {
                    ["repository"] = name, ["adopted"] = false, ["registered"] = true, ["owns"] = new JsonArray(), ["accepts"] = new JsonArray(),
                    ["packs"] = new JsonArray(), ["entries"] = 0, ["root"] = root, ["joined"] = false, ["sharesKnowledge"] = false, ["workspace"] = workspace,
                    ["addressable"] = true, ["uses"] = new JsonArray(), ["kind"] = PlaceKind.Git,
                };
            }
        }

        public static StubService Start()
        {
            var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            var url = $"http://127.0.0.1:{port}/";
            var listener = new HttpListener();
            listener.Prefixes.Add(url);
            listener.Start();
            return new StubService(listener, url);
        }

        private async Task ServeAsync()
        {
            while (_listener.IsListening)
            {
                HttpListenerContext context;
                try { context = await _listener.GetContextAsync(); }
                catch (Exception error) when (error is HttpListenerException or ObjectDisposedException or InvalidOperationException) { return; }

                var (status, body) = await AnswerAsync(context.Request);
                var bytes = Encoding.UTF8.GetBytes(body);
                context.Response.StatusCode = status;
                context.Response.ContentType = "application/json";
                await context.Response.OutputStream.WriteAsync(bytes);
                context.Response.Close();
            }
        }

        private async Task<(int Status, string Body)> AnswerAsync(HttpListenerRequest request)
        {
            var path = request.Url!.AbsolutePath;
            if (request.HttpMethod == "GET" && path == "/api/registry")
            {
                lock (_rows) return (200, new JsonArray([.. _rows.Values.Select(row => (JsonNode)row.DeepClone())]).ToJsonString());
            }

            if (request.HttpMethod == "POST" && path == "/api/registry")
            {
                using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
                var sent = JsonNode.Parse(await reader.ReadToEndAsync())!.AsObject();
                var workspace = (string?)sent["workspace"] ?? RemoteTarget.DefaultWorkspace;
                Register((string)sent["repository"]!, (string)sent["root"]!, workspace);
                lock (_rows) _rows[(string)sent["repository"]!]["kind"] = (string?)sent["kind"] ?? PlaceKind.Git;
                return (200, new JsonObject { ["repository"] = (string)sent["repository"]!, ["at"] = DateTimeOffset.UtcNow.ToString("O"), ["workspace"] = workspace }.ToJsonString());
            }

            if (request.HttpMethod == "GET" && path == "/api/quests/changes") return (200, """{"mark":"m","order":[],"changed":[],"whole":true}""");
            return request.HttpMethod == "GET" ? (200, "[]") : (404, """{"error":"the stand-in keeps the registry and answers the rest empty"}""");
        }

        public async ValueTask DisposeAsync()
        {
            _listener.Stop();
            _listener.Close();
            try { await _serving; } catch (ObjectDisposedException) { }
        }
    }
}
