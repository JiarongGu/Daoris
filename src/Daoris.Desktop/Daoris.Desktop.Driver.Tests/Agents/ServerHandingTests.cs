using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AGENTPROVIDER1 row p (the agent-provider design §4.4): a start's connectors are handed by the door's own carrier, asked once, never by
/// a caller's test of the door. Claude Code's native door writes the plan's file in its own layout and names it, the same file as before;
/// a door whose servers ride its wire, and one that takes none at its spawn, has no carrier there, and nothing is written for it. A
/// plugin's or a test's pipe adapter, which declares none, keeps the file named by its own door.
/// </summary>
public sealed class ServerHandingTests : IDisposable
{
    private readonly string _home = Path.GetFullPath(Path.Combine("_fixtures", "serverhanding-" + Guid.NewGuid().ToString("N")[..8]));

    public ServerHandingTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Discard(_home);

    private static ConnectorPlan Plan(string session) => new(
        session, [new AcpMcpServer("browser", "npx", ["-y", "@playwright/mcp@latest"], new Dictionary<string, string> { ["HEADLESS"] = "1" })]);

    [Fact]
    public void Claude_codes_native_door_hands_the_plan_as_its_own_file_and_every_other_built_door_has_no_carrier()
    {
        var info = new ProcessStartInfo();
        var file = AdapterSet.Built().Resolve("claude-code").ServerHanding()!.Hand(info, _home, Plan("s1"));
        Assert.Equal(DaorisHome.Entry(_home, HomeEntry.Spawn, "s1.mcp.json"), file);
        Assert.Equal(["--mcp-config", file], info.ArgumentList);
        Assert.Contains("\"HEADLESS\"", File.ReadAllText(file!), StringComparison.Ordinal);
        SpawnFiles.Remove(file);

        foreach (var door in new[] { "claude-code-acp", "codex-acp", "dsh", "stub", "acp-stub" })
        {
            Assert.Null(AdapterSet.Built().Resolve(door).ServerHanding());
        }

        Assert.Null(AdapterSet.Built().Resolve("claude-code").ServerHanding()!.Hand(new ProcessStartInfo(), _home, new ConnectorPlan("s2", [])));
        Assert.False(File.Exists(DaorisHome.Entry(_home, HomeEntry.Spawn, "s2.mcp.json")));
    }

    [Fact]
    public void An_adapter_declaring_no_carrier_keeps_the_file_named_by_its_own_door_on_the_pipe_and_none_on_the_wire()
    {
        var info = new ProcessStartInfo();
        var file = new Adapter(SessionWire.Pipe).ServerHanding()!.Hand(info, _home, Plan("s3"));
        Assert.Equal(["servers:" + file], info.ArgumentList);
        Assert.Null(new Adapter(SessionWire.Acp).ServerHanding());
    }

    private sealed class Adapter(SessionWire wire) : ISessionAdapter
    {
        public string Name => "stand-agent";
        public SessionWire Wire => wire;
        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) => new("stand-command");
        public void HandServers(ProcessStartInfo info, string configFile) => info.ArgumentList.Add("servers:" + configFile);
    }
}
