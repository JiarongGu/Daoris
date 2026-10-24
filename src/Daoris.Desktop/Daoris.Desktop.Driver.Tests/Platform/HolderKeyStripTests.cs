using System.Diagnostics;
using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// SUPERVISOR1a-ii (D208 §2.2; the supervisor design §11 row 1a-ii): the holder key leaves every Daoris process at its entry and every
/// start Daoris builds, so an agent of a Daoris started with the variable set holds no key and presents no proof. Each entry is found by
/// searching for the hosts' entries, never listed by hand, and must remove the variable as its first statement.
/// </summary>
public sealed partial class HolderKeyStripTests
{
    private const string Sentinel = "5e17e1a15e17e1a15e17e1a15e17e1a15e17e1a15e17e1a15e17e1a15e17e1a1";

    /// <summary>The executables' entries: each <c>Program.cs</c> of the desktop's and the service's projects, tests aside.</summary>
    private static IReadOnlyList<string> Entries()
    {
        var src = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        return
        [
            .. new[] { "Daoris.Desktop", "Daoris.Service" }
                .SelectMany(product => Directory.EnumerateDirectories(Path.Combine(src, product)))
                .Where(project => !Path.GetFileName(project).EndsWith(".Tests", StringComparison.Ordinal))
                .Select(project => Path.Combine(project, "Program.cs"))
                .Where(File.Exists),
        ];
    }

    [Fact]
    public void Each_entry_removes_the_key_as_its_first_statement()
    {
        var entries = Entries();
        Assert.True(entries.Count >= 7, $"the search found {entries.Count} entries: {string.Join(", ", entries)}");
        foreach (var entry in entries)
        {
            var code = Code(File.ReadAllText(entry));
            // A class's Main: its body. Top-level statements: the file.
            var body = MainBody().Match(code) is { Success: true } main ? code[(main.Index + main.Length)..] : code;
            var first = body[..(body.IndexOf(';') + 1)].Trim();
            Assert.True(
                first is "HolderKey.Strip();" or "HolderKey.Take();" or "Daoris.Driver.HolderKey.Strip();"
                    or "Environment.SetEnvironmentVariable(\"DAORIS_ROLE_KEY\", null);",
                $"{entry}'s first statement is `{first}`, not the key's removal");
        }

        // The verb is the one reader: the driver's host takes the key, every other entry only strips it.
        Assert.Single(entries, entry => Code(File.ReadAllText(entry)).Contains("HolderKey.Take();", StringComparison.Ordinal));
    }

    /// <summary>Every start's seams drop a key a start inherited or was handed, and the holder's terminal alone adds it back.</summary>
    [Fact]
    public void Every_start_s_hand_drops_the_key()
    {
        Assert.Contains(HolderKey.Variable, GitWiring.Removed);
        var started = new ProcessStartInfo("git") { UseShellExecute = false };
        started.Environment[HolderKey.Variable] = Sentinel;
        GitWiring.Hand(started);
        Assert.False(started.Environment.ContainsKey(HolderKey.Variable));

        var tool = new ProcessStartInfo("node") { UseShellExecute = false };
        tool.Environment[HolderKey.Variable] = Sentinel;
        Tools.Hand(tool, Path.Combine(Path.GetTempPath(), "daoris-holder-strip-" + Guid.NewGuid().ToString("N")[..8]));
        Assert.False(tool.Environment.ContainsKey(HolderKey.Variable));
    }

    /// <summary>
    /// An agent of a Daoris started with the variable set: Daoris took it out at its entry, its start dropped it, so the agent's own verb
    /// finds no key, presents no proof and sends nothing; a proof it could make without the key verifies against no appointment's hash.
    /// </summary>
    [Fact]
    public async Task An_agent_of_a_Daoris_started_with_the_key_set_holds_no_key_and_presents_no_proof()
    {
        // What the agent's verb reads: Daoris's environment stripped, its start's dropped.
        var agent = new ProcessStartInfo("daoris-driver") { UseShellExecute = false };
        agent.Environment[HolderKey.Variable] = Sentinel;
        GitWiring.Hand(agent);
        var inherited = agent.Environment.TryGetValue(HolderKey.Variable, out var value) ? value : null;
        Assert.Null(inherited);
        Assert.Null(HolderKey.Read(inherited));

        var home = Path.Combine(Path.GetTempPath(), "daoris-holder-strip-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            using var service = new ServiceClient("http://127.0.0.1:9", null);
            var world = new SessionsWorld(service, home, DriverConfig.Empty, SessionWire.Pipe, null)
            {
                Instance = new InstanceWorld { Home = home, Environment = _ => null, Out = TextWriter.Null, Error = TextWriter.Null },
                HolderKey = () => inherited,
            };
            var ask = SessionsCommand.Read(
                ["supervise", "s1", "--question", "q", "--generation", "g", "--choice", "A", "--reason", "r", "--source", "s"], out _)!;
            var output = new StringWriter();
            Assert.Equal(2, await SessionsCommand.RunAsync(ask, world, output));
            Assert.Contains(HolderKey.Variable, output.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(home)) Directory.Delete(home, recursive: true);
        }

        // A key of its own proves nothing against the appointment's hash.
        var nonce = SupervisorProof.Nonce();
        var form = SupervisorProof.Canonical("s1", "q", "g", "A", [new("r", "s")]);
        var forged = SupervisorProof.Prove(HolderKey.Draw(), nonce, form);
        Assert.False(SupervisorProof.Verify(HolderKey.Hash(Sentinel)!, nonce, form, forged));
    }

    /// <summary>A file's code with its comments out, so a commented line is never read as its first statement.</summary>
    private static string Code(string source) => string.Join('\n', source.Split('\n')
        .Select(line => line.TrimStart())
        .Where(line => !line.StartsWith("//", StringComparison.Ordinal) && !line.StartsWith("using ", StringComparison.Ordinal)
                       && !line.StartsWith("namespace ", StringComparison.Ordinal) && !line.StartsWith('[')));

    [GeneratedRegex(@"\bMain\([^)]*\)\s*\{")]
    private static partial Regex MainBody();
}
