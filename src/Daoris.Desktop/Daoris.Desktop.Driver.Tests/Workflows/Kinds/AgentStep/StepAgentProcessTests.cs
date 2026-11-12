using System.Diagnostics;
using System.Text;
using System.Text.Json;

using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// An agent-performed pull-request step on real processes: the performer sends the attempt to the step agents, whose session is a stand-in
/// agent (node) that starts the <c>daoris-plugins</c> server it is handed through the built program, sees the Azure DevOps example's tools
/// and <c>step_answer</c> alone, calls a tool, and answers; the answer reaches the step under the plugin, its address the pull request's.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
public sealed class StepAgentProcessTests : IDisposable
{
    private const string Plugin = "daoris-azure-devops";
    private const string Unit = "run:v-0123456789abcdef";
    private const string Tip = "0123456789abcdef0123456789abcdef01234567";

    private static readonly NodeDeclaration Step = new("pr", $"{Plugin}/pull-request", 1, NodeReach.Performed)
    {
        Goal = AuthoredText.Of("Open the pull request for the pushed branch."),
        Perform = new NodePerform(Idempotent: true, Reconcile: true, Transient: []),
        Evidence = "azure-devops-pull-request/receipt@1",
    };

    /// <summary>The stand-in agent: starts the server it is handed, lists its tools, calls one of the plugin's, answers the step, and says what it saw.</summary>
    private const string Agent = """
        import { spawn } from 'node:child_process';
        import { createInterface } from 'node:readline';
        const server = JSON.parse(process.env.STEP_SERVER);
        const child = spawn(server.command, server.args, { env: { ...process.env, ...server.env }, stdio: ['pipe', 'pipe', 'inherit'] });
        const lines = createInterface({ input: child.stdout })[Symbol.asyncIterator]();
        let id = 0;
        const ask = async (method, params) => {
          child.stdin.write(JSON.stringify({ jsonrpc: '2.0', id: ++id, method, params }) + '\n');
          return JSON.parse((await lines.next()).value);
        };
        await ask('initialize', { protocolVersion: '2025-06-18', capabilities: {}, clientInfo: { name: 'stand-in-agent', version: '0' } });
        child.stdin.write(JSON.stringify({ jsonrpc: '2.0', method: 'notifications/initialized' }) + '\n');
        const tools = (await ask('tools/list', {})).result.tools.map((tool) => tool.name);
        const status = (await ask('tools/call', { name: 'daoris-azure-devops_status', arguments: {} })).result;
        const answered = (await ask('tools/call', { name: 'step_answer', arguments: {
          state: 'reached', url: 'https://dev.azure.com/acme/Proj/_git/app/pullrequest/7', message: 'opened pull request 7' } })).result;
        console.log(JSON.stringify({ tools, status: status.content[0].text, answered: answered.content[0].text, error: answered.isError }));
        child.stdin.end();
        """;

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-stepagent-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly string _agent;
    private readonly List<string> _said = [];

    public StepAgentProcessTests()
    {
        foreach (var plugin in new[] { Plugin, "daoris-github" })
        {
            var to = DaorisHome.Entry(_home, HomeEntry.Plugins, plugin);
            Directory.CreateDirectory(to);
            foreach (var file in Directory.GetFiles(Path.Combine(WorkspaceRoot.Folder, "examples", "plugins", plugin))) File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
        }

        _agent = Path.Combine(_home, "stand-in-agent.mjs");
        File.WriteAllText(_agent, Agent);
    }

    public void Dispose()
    {
        for (var attempt = 0; Directory.Exists(_home); attempt++)
        {
            try
            {
                Directory.Delete(_home, recursive: true);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException && attempt < 20)
            {
                Thread.Sleep(100);
            }
        }
    }

    /// <summary>The step's session as a harness runs one: the stand-in agent, handed the server entry with the built program in its place.</summary>
    private sealed class StandInSessions(string agent, List<string> said) : IStepSessions
    {
        public async Task RunAsync(StepAgentAsk ask, Func<string, ServerEntry> toolsOf, CancellationToken ct)
        {
            var tools = toolsOf("s-stand-in");
            var info = new ProcessStartInfo("node")
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, StandardOutputEncoding = Encoding.UTF8,
            };
            info.ArgumentList.Add(agent);
            foreach (var name in info.Environment.Keys.Where(key => key.StartsWith("DAORIS_", StringComparison.OrdinalIgnoreCase)).ToList()) info.Environment.Remove(name);
            info.Environment["STEP_SERVER"] = JsonSerializer.Serialize(new { command = tools.Command, args = new[] { DriverHostTests.HostDll() }.Concat(tools.Arguments), env = tools.Environment });
            using var process = Process.Start(info)!;
            var output = await process.StandardOutput.ReadToEndAsync(ct);
            using var bound = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            await process.WaitForExitAsync(bound.Token);
            lock (said) said.Add(output.Trim());
        }

        public void Stop(string invocation)
        {
        }
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

    [Fact]
    public async Task A_stand_in_agent_holding_the_plugin_s_tools_answers_the_step_through_step_answer_and_the_step_is_reached()
    {
        var files = new NodeRecordFiles(_home, () => true);
        var agents = new StepAgents(_home, new StandInSessions(_agent, _said), (unit, attempt, node, _) => Task.FromResult<StepAgentAsk?>(new StepAgentAsk(
            attempt.Call.Invocation, unit, node.Id, attempt.Attempt, Plugin, "pull-request", node.Goal!.AsWritten, "engine", "games", "feature/x",
            "https://dev.azure.com/acme/Proj/_git/app", "main", attempt.Call.SubjectRevision) { Evidence = node.Evidence }), program: "dotnet");
        var performer = new WorkPerformer(files, new NoPlugins(), () => DateTimeOffset.UtcNow) { Doors = NodeDoors.None.With(StepAgents.Door, agents) };
        var before = new Dictionary<string, NodeStatus>(StringComparer.Ordinal) { [Step.Id] = NodeLifecycle.Start(Unit, Step.Id) };
        var evaluated = WireEvaluation.Evaluate([Step], [], before,
            new WireRun("graph-1", "v-0123456789abcdef") { Revisions = new Dictionary<string, string?> { [Unit] = Tip }, Fired = new HashSet<string>(StringComparer.Ordinal) });
        performer.Keep(before, evaluated);

        Assert.Equal(PerformedWhat.Sent, (await performer.StartAsync(Step, Assert.Single(evaluated.Starts), CancellationToken.None)).What);
        await agents.IdleAsync().WaitAsync(TimeSpan.FromSeconds(120));
        await performer.ReconcileAsync((unit, node) => unit == Unit && node == Step.Id ? Step : null, CancellationToken.None);

        using var saw = JsonDocument.Parse(Assert.Single(_said));
        var tools = saw.RootElement.GetProperty("tools").EnumerateArray().Select(tool => tool.GetString()!).ToList();
        Assert.Contains(StepAnswer.Tool, tools);
        Assert.Contains("daoris-azure-devops_create-pull-request", tools);
        Assert.DoesNotContain(tools, tool => tool.StartsWith("daoris-github_", StringComparison.Ordinal));
        Assert.Contains("No token is kept", saw.RootElement.GetProperty("status").GetString());
        Assert.False(saw.RootElement.GetProperty("error").GetBoolean());
        var record = files.Read(Unit)!;
        Assert.Equal(NodeStates.Reached, record.Statuses[Step.Id].State);
        Assert.Equal(Plugin, record.Attempts.Single().Plugin);
        Assert.Equal("https://dev.azure.com/acme/Proj/_git/app/pullrequest/7", StepAnswer.Read(_home, record.Attempts.Single().Call.Invocation)!.Outcome!.Evidence!.Url);
    }
}
