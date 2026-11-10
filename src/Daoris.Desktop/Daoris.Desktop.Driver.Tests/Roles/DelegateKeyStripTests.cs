using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The delegate key, the holder key's twin for every session Daoris starts: each start of a session that has a record is handed a key
/// of its own beside its id, whose hash Daoris keeps for that session; every Daoris executable's entry and every other start drops it,
/// an inherited one included; and it is forgotten as the session's process ends.
/// </summary>
[Trait("Category", "Census")]
public sealed class DelegateKeyStripTests
{
    private const string Sentinel = "de1e6a7ede1e6a7ede1e6a7ede1e6a7ede1e6a7ede1e6a7ede1e6a7ede1e6a7e";

    private static readonly string Root = TestHomes.Root;

    private static SessionTarget Quest(string session) =>
        new("q1", "Title", "Body", "person", "engine", Root, "http://localhost") { Home = Root, Session = session };

    /// <summary>Each kind of start that names its session's record, and the conversation's.</summary>
    private static IEnumerable<(string Kind, SpawnRequest Request, string Session)> Starts(SessionDelegates delegates) =>
    [
        ("quest", SpawnRequests.Of(Quest("s-quest"), null, delegates), "s-quest"),
        ("intake", SpawnRequests.Of(Quest("s-intake") with { Ask = "a1" }, null, delegates), "s-intake"),
        ("opinion", SpawnRequests.Of(Quest("s-opinion") with { Opinion = "o1" }, null, delegates), "s-opinion"),
        ("helper", SpawnRequests.Of(Quest("s-helper") with { Helper = "h1" }, null, delegates), "s-helper"),
        ("piece", SpawnRequests.Of(Quest("s-piece") with { Piece = "p1" }, null, delegates), "s-piece"),
        ("brainstorm", SpawnRequests.Of(Quest("s-brainstorm") with { Brainstorm = "b1" }, null, delegates), "s-brainstorm"),
        ("chat", SpawnRequests.Chat("engine", Root, "http://localhost", "s-chat", delegates), "s-chat"),
    ];

    [Fact]
    public void Every_kind_s_start_holds_a_key_of_its_own_whose_hash_Daoris_keeps_for_that_session()
    {
        var delegates = new SessionDelegates();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (kind, request, session) in Starts(delegates))
        {
            Assert.Equal(session, request.Identity[ChildMarks.SessionVariable]);
            var key = request.Identity.GetValueOrDefault(DelegateKey.Variable);
            Assert.True(HolderKey.Read(key) is not null, $"{kind}'s start holds no key");
            Assert.True(keys.Add(key!), $"{kind}'s key is another start's");
            Assert.Contains(delegates.Of(session), hash => hash.SequenceEqual(HolderKey.Hash(key)!));
            Assert.Empty(delegates.Of("s-other"));
        }

        // A start that names no record holds no key, and none is kept.
        var bare = SpawnRequests.Chat("engine", Root, "http://localhost", null, delegates);
        Assert.False(bare.Identity.ContainsKey(DelegateKey.Variable));
    }

    /// <summary>Every start the driver composes names the registry its key is drawn in: a start handed none would hold no key.</summary>
    [Fact]
    public void Every_start_the_driver_composes_names_where_its_key_is_drawn()
    {
        var calls = 0;
        foreach (var file in SourceFiles.All(SourceFiles.DriverProject, "*.cs"))
        {
            var code = File.ReadAllText(file);
            foreach (var call in new[] { "SpawnRequests.Of(", "SpawnRequests.Chat(" })
            {
                for (var at = code.IndexOf(call, StringComparison.Ordinal); at >= 0; at = code.IndexOf(call, at + 1, StringComparison.Ordinal))
                {
                    var statement = code[at..code.IndexOf(';', at)];
                    Assert.True(statement.Contains("elegates", StringComparison.Ordinal), $"{Path.GetFileName(file)}: `{statement}` names no registry");
                    calls++;
                }
            }
        }

        Assert.True(calls >= 5, $"the search found {calls} starts");
    }

    /// <summary>The shell sets the start's own key over one the driver inherited, and a start with none drops the inherited one.</summary>
    [Fact]
    public void A_start_drops_an_inherited_key_and_carries_only_its_own()
    {
        Assert.Contains(DelegateKey.Variable, Spawning.Roles);
        var delegates = new SessionDelegates();
        var own = SpawnRequests.Of(Quest("s1"), null, delegates);
        var held = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase) { [DelegateKey.Variable] = Sentinel };
        Spawning.Unrole(held);
        Assert.False(held.ContainsKey(DelegateKey.Variable));

        var started = Shell.Start(new ProcessRecipe("stand-command", []), own);
        Assert.Equal(own.Identity[DelegateKey.Variable], started.Environment[DelegateKey.Variable]);
        Assert.NotEqual(Sentinel, started.Environment[DelegateKey.Variable]);
    }

    /// <summary>Every other start's seams drop the key: git's and the tools' hand, and the window's own terminal.</summary>
    [Fact]
    public void Every_other_start_s_hand_drops_the_key()
    {
        Assert.Contains(DelegateKey.Variable, GitWiring.Removed);
        var started = new ProcessStartInfo("stand-command") { UseShellExecute = false, CreateNoWindow = true };
        started.Environment[DelegateKey.Variable] = Sentinel;
        GitWiring.Hand(started);
        Assert.False(started.Environment.ContainsKey(DelegateKey.Variable));

        var tool = new ProcessStartInfo("node") { UseShellExecute = false, CreateNoWindow = true };
        tool.Environment[DelegateKey.Variable] = Sentinel;
        Tools.Hand(tool, Path.Combine(TestHomes.Root, "daoris-delegate-strip-" + Guid.NewGuid().ToString("N")[..8]));
        Assert.False(tool.Environment.ContainsKey(DelegateKey.Variable));

        var inherited = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [DelegateKey.Variable] = Sentinel, ["DAORIS_KEPT"] = "kept" };
        var block = PseudoConsole.EnvironmentBlock(new Dictionary<string, string>(), inherited).Split('\0');
        Assert.DoesNotContain(block, line => line.StartsWith(DelegateKey.Variable + "=", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("DAORIS_KEPT=kept", block);
    }

    /// <summary>
    /// The entry's strip takes the key out of this process, and the verb's take reads it with the session's id first; a key with no session
    /// beside it is no delegate's.
    /// </summary>
    [Fact]
    public void The_entry_takes_the_key_with_its_session_and_strips_it()
    {
        // Only the delegate's own variable is set on this process, which no other test reads.
        var before = Environment.GetEnvironmentVariable(DelegateKey.Variable);
        try
        {
            Environment.SetEnvironmentVariable(DelegateKey.Variable, Sentinel);
            HolderKey.Strip();
            Assert.Null(Environment.GetEnvironmentVariable(DelegateKey.Variable));
        }
        finally
        {
            Environment.SetEnvironmentVariable(DelegateKey.Variable, before);
        }

        string? Of(string? key, string? session, string name) =>
            name == DelegateKey.Variable ? key : name == ChildMarks.SessionVariable ? session : null;
        Assert.Equal(new DelegateHeld("s1", Sentinel), DelegateKey.Read(name => Of(Sentinel, "s1", name)));
        Assert.Null(DelegateKey.Read(name => Of(Sentinel, null, name)));
        Assert.Null(DelegateKey.Read(name => Of("not-a-key", "s1", name)));
        Assert.Null(DelegateKey.Read(name => Of(null, "s1", name)));

        // The verb's take is inside the holder key's, so the entry's one first statement takes both.
        var host = File.ReadAllText(Path.Combine(SourceFiles.Project(SourceFiles.DriverProject), "Roles", "HolderKey.cs"));
        Assert.Contains("DelegateKey.Take();", host, StringComparison.Ordinal);
    }

    /// <summary>The session's process ending forgets its keys; one drawn after that process is tracked, a start still coming, is kept.</summary>
    [Fact]
    public void The_session_s_process_end_forgets_its_keys()
    {
        var processes = new SessionProcesses();
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "node",
            ArgumentList = { "-e", "setTimeout(() => {}, 60000)" },
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;
        try
        {
            SpawnRequests.Of(Quest("s1"), null, processes.Delegates);
            var tracked = processes.Track("s1", process);
            var coming = SpawnRequests.Of(Quest("s1"), null, processes.Delegates).Identity[DelegateKey.Variable];
            Assert.Equal(2, processes.Delegates.Of("s1").Count);

            tracked.Dispose();
            var kept = Assert.Single(processes.Delegates.Of("s1"));
            Assert.Equal(HolderKey.Hash(coming), kept);

            processes.Delegates.Forget("s1");
            Assert.Empty(processes.Delegates.Of("s1"));
        }
        finally
        {
            SessionProcesses.EndIfRunning(process);
        }
    }

    /// <summary>The verb's line carries the payload as the very text its proof is over, so the pipe reads the same bytes back.</summary>
    [Fact]
    public void The_verb_s_line_carries_the_payload_its_proof_is_over()
    {
        var delegates = new SessionDelegates();
        var key = delegates.Draw("s1");
        var nonce = SupervisorProof.Nonce();
        var payload = System.Text.Json.JsonSerializer.Serialize(new { quest = "q1", note = "naïve ✓ \"quoted\"" });
        var line = InstanceClient.DelegatedLine("id1", "DAORIS.DRIVER", "WORK_PAUSE", payload, "k1",
            text => new { session = "s1", proof = DelegateProof.Prove(key, nonce, "DAORIS.DRIVER", "WORK_PAUSE", text, "k1") });

        using var sent = System.Text.Json.JsonDocument.Parse(line);
        var said = sent.RootElement.GetProperty(DelegateProof.Field);
        Assert.Equal("s1", said.GetProperty("session").GetString());
        Assert.True(DelegateProof.Verify(
            delegates.Of("s1"), nonce, "DAORIS.DRIVER", "WORK_PAUSE", sent.RootElement.GetProperty("payload").GetRawText(),
            sent.RootElement.GetProperty("key").GetString()!, said.GetProperty("proof").GetString()));
    }

    /// <summary>A proof is over the press's own bytes: its route, its payload and its key, by the session's key alone.</summary>
    [Fact]
    public void A_proof_holds_for_its_own_press_alone()
    {
        var delegates = new SessionDelegates();
        var key = delegates.Draw("s1");
        var nonce = SupervisorProof.Nonce();
        var proof = DelegateProof.Prove(key, nonce, "DRIVER", "QUEST_PAUSE", """{"id":"q1"}""", "k1");

        Assert.True(DelegateProof.Verify(delegates.Of("s1"), nonce, "DRIVER", "QUEST_PAUSE", """{"id":"q1"}""", "k1", proof));
        Assert.False(DelegateProof.Verify(delegates.Of("s1"), nonce, "DRIVER", "QUEST_PAUSE", """{"id":"q2"}""", "k1", proof));
        Assert.False(DelegateProof.Verify(delegates.Of("s1"), nonce, "DRIVER", "QUEST_RESUME", """{"id":"q1"}""", "k1", proof));
        Assert.False(DelegateProof.Verify(delegates.Of("s1"), nonce, "DRIVER", "QUEST_PAUSE", """{"id":"q1"}""", "k2", proof));
        Assert.False(DelegateProof.Verify(delegates.Of("s1"), SupervisorProof.Nonce(), "DRIVER", "QUEST_PAUSE", """{"id":"q1"}""", "k1", proof));
        Assert.False(DelegateProof.Verify([HolderKey.Hash(Sentinel)!], nonce, "DRIVER", "QUEST_PAUSE", """{"id":"q1"}""", "k1", proof));
    }
}
