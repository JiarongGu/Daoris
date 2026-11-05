using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// ONEWAY1h-iii (AGENTPROVIDER1m-i part 4): a conversation's agent reads its door as the adapter beneath it says it, prepares its process
/// as that door makes it and equipped as every session's is, and makes the one run over its door, which a conversation goes on beside
/// through a start of its own.
/// </summary>
public sealed class ChatAgentTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-chat-agent-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly SessionProcesses _processes;

    public ChatAgentTests()
    {
        Directory.CreateDirectory(_home);
        _processes = new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions));
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Theory]
    [InlineData("claude-code")]
    [InlineData("claude-code-acp")]
    [InlineData("codex-acp")]
    [InlineData("dsh")]
    [InlineData("stub")]
    [InlineData("acp-stub")]
    public void It_reads_its_door_as_the_adapter_beneath_it_says_it(string door)
    {
        var adapter = AdapterSet.Built().Resolve(door);
        var agent = new ChatAgent(adapter);

        Assert.Equal(adapter.Name, agent.Name);
        Assert.Equal(adapter.Wire, agent.Wire);
        Assert.Equal(adapter.TakesSettings, agent.TakesSettings);
        Assert.Equal(adapter.AcpPosture, agent.Posture);
        Assert.Equal(adapter.FullPosture, agent.FullPosture);
        Assert.Equal(adapter.Toolchain?.ErrorWords?.TypedFailures == true, agent.TypedFailures);
        Assert.Equal(adapter.FrameMessage("the person's words"), agent.FrameMessage("the person's words"));
        Assert.Equal(adapter.FrameInterrupt(), agent.FrameInterrupt());
        Assert.Equal(adapter.StructuredOutput() is null, agent.StructuredOutput() is null);
        Assert.Equal(CommitTrailers.For(adapter), agent.Trailers());
        Assert.Equal(TurnBinding.Of(adapter, "account-1"), agent.Binding("account-1"));
    }

    /// <summary>The conversation's process is its door's own, then equipped by the one call every session's is (D230 point 3).</summary>
    [Fact]
    public void Its_process_is_its_doors_own_equipped_as_every_sessions_is()
    {
        var adapter = AdapterSet.Built().Resolve("claude-code");
        var target = new ChatTarget("engine", Path.GetTempPath(), "http://localhost:5177") { Session = "c1" };
        var selection = new HarnessSelection(null);

        var info = new ChatAgent(adapter).Prepare(target, command: null, selection);
        var expected = adapter.PrepareChat(target, command: null);
        SpawnEquipment.Equip(adapter, expected, selection);

        Assert.Equal(expected.FileName, info.FileName);
        Assert.Equal(expected.ArgumentList, info.ArgumentList);
        Assert.NotEmpty(adapter.ToolsUpFront);
        foreach (var (name, value) in adapter.ToolsUpFront) Assert.Equal(value, info.Environment[name]);
    }

    /// <summary>
    /// Its run is the one over its door: handed no selection, it admits no go-on, as a conversation goes on through a start of its own, and a
    /// host whose runs go on so answers no resume's process.
    /// </summary>
    [Theory]
    [InlineData("claude-code")]
    [InlineData("acp-stub")]
    public void Its_run_is_the_one_over_its_door_and_goes_on_through_a_start_of_its_own(string door)
    {
        var adapters = AdapterSet.Built();
        var agent = new ChatAgent(adapters.Resolve(door));
        var start = new TransportStart(new ProcessStartInfo("never-started"), StartClass.Person);

        var run = agent.Run(new RecordingTransportHost(_home, _processes), start);

        Assert.IsType<TransportRun>(run);
        Assert.False(run.GoesOn);
        Assert.False(run.Started);
        using var service = new ServiceClient("http://127.0.0.1:9", null);
        ITransportHost bare = new ChatTransportHost(_home, _processes, service, new HarnessConversations(_home), DriverConfig.Empty, null);
        var target = new SessionTarget("q1", "Title", "Body", "person", "engine", _home, "http://localhost");
        Assert.Throws<NotSupportedException>(() => bare.Prepared(adapters.Resolve(door), target, new HarnessSelection(null), start.Info));
    }
}
