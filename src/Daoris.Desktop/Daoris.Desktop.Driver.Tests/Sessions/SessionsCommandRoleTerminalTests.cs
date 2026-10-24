using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// STEWARDROLE1a-i (D209 §3.3, the steward design §10 row 1a-i's <c>SessionsCommandTests</c>): a role's terminal never acts as the owner
/// nor alone. Through the real entry-to-dispatch path, <see cref="HolderKey.Take"/> then the verb, with the variable set to a key and to
/// a text that is no key: a press verb is refused <c>role-terminal</c> at the entry, an allowed read's alone path is refused instead of
/// acting alone, and nothing is written; with the variable absent the same verbs run as today.
/// </summary>
/// <remarks>
/// 🔴 Alone, after the rest: it sets the variable in this process's environment and the entry's marker, which every verb beside it
/// would read. Each case undoes both before it ends.
/// </remarks>
[Collection(Alone.Name)]
public sealed class SessionsCommandRoleTerminalTests : IDisposable
{
    private const string Key = "5e17e1a15e17e1a15e17e1a15e17e1a15e17e1a15e17e1a15e17e1a15e17e1a1";

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-role-terminal-" + Guid.NewGuid().ToString("N")[..8]);

    public SessionsCommandRoleTerminalTests() => Directory.CreateDirectory(DaorisHome.Entry(_home, HomeEntry.Sessions));

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(HolderKey.Variable, null);
        HolderKey.Forget();
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private static SessionsCommandTests.Ledger Parked() => new(
        [
            new JsonObject
            {
                ["id"] = "p4rk3d00", ["repository"] = "engine", ["state"] = "awaiting-person", ["kind"] = "driven", ["quest"] = "q1",
                ["adapter"] = "claude-code", ["created"] = DateTimeOffset.UtcNow.AddHours(-1).ToString("O"),
                ["updated"] = DateTimeOffset.UtcNow.AddMinutes(-50).ToString("O"),
            },
        ],
        [new JsonObject { ["id"] = "q1", ["from"] = "ask #a1", ["to"] = "engine", ["title"] = "A quest", ["body"] = "A body.", ["status"] = "Taken" }]);

    /// <summary>The host's entry as <c>Program.cs</c> runs it: the variable taken, then the guard, then the verb.</summary>
    private async Task<(int Exit, string Said)> EnterAsync(SessionsCommandTests.Ledger ledger, string? variable, string[] args)
    {
        Environment.SetEnvironmentVariable(HolderKey.Variable, variable);
        HolderKey.Take();
        Assert.Null(Environment.GetEnvironmentVariable(HolderKey.Variable));
        var error = new StringWriter();
        if (RoleTerminal.AtEntry(args, error) is { } refused) return (refused, error.ToString());

        using var service = ledger.Client();
        var output = new StringWriter();
        var ask = SessionsCommand.Read(args[1..], out var problem);
        Assert.True(problem is null, problem);
        var world = new SessionsWorld(service, _home, DriverConfig.Empty, SessionWire.Pipe, null)
        {
            Wait = TimeSpan.FromSeconds(5),
            Poll = TimeSpan.FromMilliseconds(20),
            // No Daoris answers this home: a workspace build reads no address file, so every route would act alone.
            Instance = new InstanceWorld { Home = _home, Environment = _ => null, Out = output, Error = error, Verb = "sessions" },
        };
        var exit = await SessionsCommand.RunAsync(ask!, world, output);
        return (exit, output + error.ToString());
    }

    public static TheoryData<string> Variables => [Key, "not a key"];

    [Theory]
    [MemberData(nameof(Variables))]
    public async Task A_press_verb_in_a_roles_terminal_is_refused_at_the_entry_and_writes_nothing(string variable)
    {
        var ledger = Parked();
        var (exit, said) = await EnterAsync(ledger, variable, ["sessions", "stop", "p4rk3d00"]);

        Assert.Equal(2, exit);
        Assert.Contains($"({RoleTerminal.Code})", said, StringComparison.Ordinal);
        Assert.True(HolderKey.RoleTerminal);
        Assert.Equal("awaiting-person", ledger.State("p4rk3d00"));
        Assert.Empty(new SessionRequests(_home).Pending(DateTimeOffset.UtcNow));
    }

    [Theory]
    [MemberData(nameof(Variables))]
    public async Task A_read_the_holder_runs_refuses_its_alone_path_in_a_roles_terminal(string variable)
    {
        var (exit, said) = await EnterAsync(Parked(), variable, ["sessions", "--json"]);

        Assert.Equal(2, exit);
        Assert.Contains($"({RoleTerminal.Code})", said, StringComparison.Ordinal);
        Assert.DoesNotContain("p4rk3d00", said, StringComparison.Ordinal);
    }

    /// <summary>The verbs that act alone with no instance (§3.3's list) are refused at the entry; the roles' verbs and open reads pass it.</summary>
    [Fact]
    public void Every_alone_verb_is_refused_at_the_entry_and_the_roles_verbs_pass_it()
    {
        Environment.SetEnvironmentVariable(HolderKey.Variable, "");
        HolderKey.Take();
        // An empty value is still the variable present: the marker is its presence, whatever it holds.
        Assert.True(HolderKey.RoleTerminal);
        string[][] alone =
        [
            ["trees", "clean"], ["trees", "sync"], ["trees", "land", "s1", "--plan"], ["git", "branches"], ["git", "review", "s1"], ["trace", "s1"],
            ["drive"], ["chat"], ["help", "resume", "h1"], ["driver", "drive", "engine"], ["sessions", "finish", "s1"], ["setup", "engine"],
        ];
        foreach (var args in alone)
        {
            var error = new StringWriter();
            Assert.Equal(2, RoleTerminal.AtEntry(args, error));
            Assert.Contains(RoleTerminal.Code, error.ToString(), StringComparison.Ordinal);
        }

        foreach (var args in new[] { new[] { "roles", "brief" }, ["sessions", "supervise"], ["sessions", "--json"], ["notices"], ["history"] })
        {
            Assert.Null(RoleTerminal.AtEntry(args, TextWriter.Null));
        }
    }

    [Fact]
    public async Task With_the_variable_absent_the_same_press_runs_as_today()
    {
        var ledger = Parked();
        var (exit, said) = await EnterAsync(ledger, null, ["sessions", "stop", "p4rk3d00"]);

        Assert.False(HolderKey.RoleTerminal);
        Assert.Equal(0, exit);
        Assert.DoesNotContain(RoleTerminal.Code, said, StringComparison.Ordinal);
        Assert.Equal("stopped", ledger.State("p4rk3d00"));
    }

    /// <summary>§2.2: the brief names each role this build knows and its revision, and no key nor appointment.</summary>
    [Fact]
    public void The_roles_brief_names_each_role_and_its_revision_and_no_key()
    {
        var brief = RolesBrief.Text;
        Assert.StartsWith($"Daoris roles brief, revision {RolesBrief.Revision}.", brief, StringComparison.Ordinal);
        Assert.Contains("The supervisor (主管)", brief, StringComparison.Ordinal);
        Assert.Contains("The steward (管家)", brief, StringComparison.Ordinal);
        Assert.Contains("daoris-driver roles brief", brief, StringComparison.Ordinal);
        Assert.DoesNotContain(HolderKey.Variable, brief, StringComparison.Ordinal);
        Assert.DoesNotContain("generation", brief, StringComparison.OrdinalIgnoreCase);
        Assert.Equal($"roles held: steward, supervisor · brief {RolesBrief.Revision}", RolesBrief.Said(["steward", "supervisor"]));
    }
}
