using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;

using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// An agent-performed pull-request step started by the loop's own door: the doors are wired as the watch wires them, so the performer sends
/// the attempt to the step agents and <see cref="StepSessionTurn"/> staffs the session on the stand-in's protocol door, opens its record at the
/// service's own host and hands it the step's <c>daoris-plugins</c> server. The stand-in (its <c>step</c> case) starts that server from
/// <c>session/new</c>, reads the Azure DevOps example's <c>pull-request-status</c> against a stand-in of Azure DevOps on loopback, and
/// answers through <c>step_answer</c>: the step is reached under the plugin with the pull request's address, and the session ends completed.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class StepSessionProcessTests : IAsyncLifetime
{
    private const string Plugin = "daoris-azure-devops";
    private const string Unit = "run:v-0123456789abcdef";
    private const string Tip = "0123456789abcdef0123456789abcdef01234567";
    private const string Web = "https://dev.azure.com/acme/Proj/_git/app";
    private const string Token = "throwaway-token-for-the-stand-in";

    private static readonly NodeDeclaration Step = new("pr", $"{Plugin}/pull-request", 1, NodeReach.Performed)
    {
        Goal = AuthoredText.Of("Open the pull request for the pushed branch."),
        Perform = new NodePerform(Idempotent: true, Reconcile: true, Transient: []),
        Evidence = "azure-devops-pull-request/receipt@1",
        Outputs = new Dictionary<string, string>(StringComparer.Ordinal) { ["pull-request"] = "azure-devops-pull-request/pull-request@1" },
    };

    private readonly string _scratch = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "step-session", Guid.NewGuid().ToString("N")[..8]);
    private string Home => Path.Combine(_scratch, "home");
    private string Root => Path.Combine(_scratch, "engine");

    private RealServiceHost? _host;
    private ServiceClient? _client;
    private AzureStandIn? _azure;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_azure is not null) await _azure.DisposeAsync();
        _client?.Dispose();
        _host?.Dispose();
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_scratch);
    }

    /// <summary>The home as the install keeps it: the example installed, its own token kept in its storage, and the build role on the stand-in.</summary>
    private DriverConfig Prepared(string api)
    {
        var plugin = DaorisHome.Entry(Home, HomeEntry.Plugins, Plugin);
        Directory.CreateDirectory(plugin);
        foreach (var each in Directory.GetFiles(Path.Combine(WorkspaceRoot.Folder, "examples", "plugins", Plugin))) File.Copy(each, Path.Combine(plugin, Path.GetFileName(each)));
        var storage = PluginPrivate.Folder(Home, Plugin);
        Directory.CreateDirectory(storage);
        File.WriteAllText(Path.Combine(storage, "credential.json"), JsonSerializer.Serialize(new { token = Token, kept = "2026-11-12T09:00:00Z" }));
        Directory.CreateDirectory(Root);

        var agent = Path.Combine(AppContext.BaseDirectory, "Agents", "Providers", "Stub", ProviderFiles.ProgramFolder, "agent.mjs");
        var config = ActorChoices.With(
            DriverConfig.Empty with
            {
                Drivable = ["engine"], Actors = StaffedRoles.On("acp-stub"), PollSeconds = 1,
                Commands = new Dictionary<string, IReadOnlyList<string>>
                {
                    ["acp-stub"] = ["node", agent, "--door", "protocol", "--scenario", "step", "--server-env", $"DAORIS_HOST_API={api}"],
                },
            },
            "build", [new ActorChoice("acp-stub")]);
        var file = DaorisHome.Entry(Home, HomeEntry.DriverConfig);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, """{ "adapter": "acp-stub", "actors": { "build": [ { "agent": "acp-stub" } ] } }""");
        Assert.Equal(StaffingImported.Done, new RoleStaffing(Home, () => StoreSettled.Yes).ImportFile().How);
        return config;
    }

    private async Task<ServiceClient> ServiceAsync()
    {
        _host = await RealServiceHost.StartAsync(Path.Combine(_scratch, "service"));
        _client = new ServiceClient(new ServiceHttp(_host.Url, key: null));
        var (ok, message) = await _client.RegisterAsync(new JsonObject
        {
            ["repository"] = "engine",
            ["root"] = Root,
            ["packs"] = new JsonArray(),
            ["domain"] = new JsonObject { ["summary"] = "An engine.", ["owns"] = new JsonArray("its own tree"), ["accepts"] = new JsonArray("a quest") },
        });
        Assert.True(ok, message);
        return _client;
    }

    /// <summary>The program the step's server runs as: the desktop host's own, built beside this suite.</summary>
    private static string Program() => Path.ChangeExtension(DriverHostTests.HostDll(), OperatingSystem.IsWindows() ? ".exe" : null);

    [Fact]
    public async Task The_loop_s_step_session_reads_the_pull_request_with_the_plugin_s_tool_and_answers_the_step_reached()
    {
        _azure = AzureStandIn.Start();
        var config = Prepared(_azure.Url);
        var service = await ServiceAsync();
        var adapters = TestDoors.Registry(TestDoors.Shipped("stub", "acp-stub"));
        var doors = NodeDoors.Wired(new NodeDoorWiring(Home, () => true)
        {
            StepSessions = new StepSessionTurn(() => new Daoris.Driver.Driver(
                service, config, adapters, Home, processes: new SessionProcesses(),
                harnesses: new HarnessRoster(adapters, DaorisHome.Entry(Home, HomeEntry.Harnesses)))),
            StepAsks = (unit, attempt, node, _) => Task.FromResult<StepAgentAsk?>(new StepAgentAsk(
                attempt.Call.Invocation, unit, node.Id, attempt.Attempt, Plugin, "pull-request", node.Goal!.AsWritten, "engine", null, "feature/x",
                Web, "main", attempt.Call.SubjectRevision)
            {
                Evidence = node.Evidence, Root = Root, Outputs = node.Outputs,
            }),
            Program = Program(),
        });
        var agents = Assert.IsType<StepAgents>(doors.Door<IStepAgents>(StepAgents.Door));
        var files = new NodeRecordFiles(Home, () => true);
        var performer = new WorkPerformer(files, new NoPlugins(), () => DateTimeOffset.UtcNow) { Doors = doors };
        var before = new Dictionary<string, NodeStatus>(StringComparer.Ordinal) { [Step.Id] = NodeLifecycle.Start(Unit, Step.Id) };
        var evaluated = WireEvaluation.Evaluate([Step], [], before,
            new WireRun("graph-1", "v-0123456789abcdef") { Revisions = new Dictionary<string, string?> { [Unit] = Tip }, Fired = new HashSet<string>(StringComparer.Ordinal) });
        performer.Keep(before, evaluated);

        Assert.Equal(PerformedWhat.Sent, (await performer.StartAsync(Step, Assert.Single(evaluated.Starts), CancellationToken.None)).What);
        await agents.IdleAsync().WaitAsync(Poll.Patience);
        await performer.ReconcileAsync((unit, node) => unit == Unit && node == Step.Id ? Step : null, CancellationToken.None);

        var heard = string.Join("\n", _azure.Heard);
        Assert.Contains($"GET /acme/Proj/_apis/git/repositories/app/pullrequests Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes($":{Token}"))}", heard);
        var record = files.Read(Unit)!;
        var attempt = record.Attempts.Single();
        var answer = StepAnswer.Read(Home, attempt.Call.Invocation);
        Assert.True(record.Statuses[Step.Id].State == NodeStates.Reached,
            $"{record.Statuses[Step.Id].State}; attempt {attempt.State} {attempt.Code}; answered {answer?.Outcome?.State} {answer?.Outcome?.Code}\nheard: {heard}");
        Assert.Equal(Plugin, attempt.Plugin);
        Assert.Equal($"{Web}/pullrequest/7", answer!.Outcome!.Evidence!.Url);
        Assert.Equal("azure-devops-pull-request/pull-request@1:7", answer.Outcome.Outputs["pull-request"].Ref);

        using var sessions = JsonDocument.Parse(await service.SessionRecordsJsonAsync(default));
        var session = Assert.Single(sessions.RootElement.EnumerateArray());
        Assert.Equal("completed", session.GetProperty("state").GetString());
    }

    private sealed class NoPlugins : IWorkPerformPort
    {
        public Task<int> PerformAsync(string plugin, WorkPerformCall call, CancellationToken ct) => throw new InvalidOperationException($"sent to {plugin}");

        public Task<(WorkReconcileAnswer Answer, int Generation)> ReconcileStepAsync(string plugin, WorkReconcileCall call, CancellationToken ct) =>
            throw new InvalidOperationException($"asked {plugin}");

        public int? GenerationOf(string plugin) => null;

        public void Refused(string plugin, string code)
        {
        }
    }

    /// <summary>Azure DevOps' Git API on loopback as the plugin reads it: one active pull request, 7, from <c>feature/x</c> into <c>main</c>.</summary>
    private sealed class AzureStandIn : IAsyncDisposable
    {
        private readonly HttpListener _listener;
        private readonly Task _serving;
        private readonly List<string> _heard = [];

        private AzureStandIn(HttpListener listener, string url)
        {
            _listener = listener;
            Url = url.TrimEnd('/');
            _serving = ServeAsync();
        }

        public string Url { get; }

        /// <summary>Each request as <c>METHOD path authorization</c>.</summary>
        public IReadOnlyList<string> Heard
        {
            get
            {
                lock (_heard) return [.. _heard];
            }
        }

        public static AzureStandIn Start()
        {
            var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            var url = $"http://127.0.0.1:{port}/";
            var listener = new HttpListener();
            listener.Prefixes.Add(url);
            listener.Start();
            return new AzureStandIn(listener, url);
        }

        private async Task ServeAsync()
        {
            while (_listener.IsListening)
            {
                HttpListenerContext context;
                try { context = await _listener.GetContextAsync(); }
                catch (Exception error) when (error is HttpListenerException or ObjectDisposedException or InvalidOperationException) { return; }

                var request = context.Request;
                lock (_heard) _heard.Add($"{request.HttpMethod} {request.Url!.AbsolutePath} {request.Headers["Authorization"]}");
                var found = request.HttpMethod == "GET" && request.Url.AbsolutePath == "/acme/Proj/_apis/git/repositories/app/pullrequests"
                            && request.QueryString["searchCriteria.sourceRefName"] == "refs/heads/feature/x";
                var body = found
                    ? JsonSerializer.Serialize(new
                    {
                        value = new[]
                        {
                            new
                            {
                                pullRequestId = 7, status = "active", sourceRefName = "refs/heads/feature/x", targetRefName = "refs/heads/main",
                                creationDate = "2026-11-12T09:00:00Z", repository = new { webUrl = Web },
                            },
                        },
                    })
                    : """{ "message": "the stand-in answers one pull request's search" }""";
                var bytes = Encoding.UTF8.GetBytes(body);
                context.Response.StatusCode = found ? 200 : 404;
                context.Response.ContentType = "application/json";
                await context.Response.OutputStream.WriteAsync(bytes);
                context.Response.Close();
            }
        }

        public async ValueTask DisposeAsync()
        {
            _listener.Stop();
            _listener.Close();
            try { await _serving; } catch (ObjectDisposedException) { }
        }
    }
}
