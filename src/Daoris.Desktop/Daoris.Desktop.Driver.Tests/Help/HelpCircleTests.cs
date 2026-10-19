using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// ASKSCOPE1c (D158's ASKSCOPE1 note point 5): Ask Daoris's tools are bound by its conversation's workspace, in restricted mode. Its
/// connector is handed its room's workspace as its circle, the checkouts it may read are its workspace's where reading across
/// allows them, and its rules carry the person's denies for that workspace. In full access the shell reaches anything, so the
/// room says the workspace is no fence, and the connector stays scoped. <c>default</c>'s room is the whole machine, as before.
/// </summary>
/// <remarks>No process and no git: the fast half.</remarks>
public sealed class HelpCircleTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-help-circle-" + Guid.NewGuid().ToString("N")[..8]);

    public HelpCircleTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static ISessionAdapter Adapter(string name) => AdapterSet.Built().Resolve(name);

    /// <summary>
    /// The circle is the room's: a named workspace's conversation in its own room is bound to that workspace, under either mode;
    /// <c>default</c>'s, and a conversation going on in <c>help</c> where it started before each workspace had a room, are bound
    /// to none, since their room is the whole machine.
    /// </summary>
    [Fact]
    public void A_conversation_is_bound_to_its_room_s_workspace_and_default_s_to_none()
    {
        var alpha = ChatRunner.HelpPlaceIn(_home, HelpRoom.PathOf(_home, "alpha"), [], "alpha");

        Assert.Equal("alpha", alpha.Circle);
        Assert.Equal("alpha", ChatRunner.HelpUnder(alpha, HelpAccess.For(HelpModes.Full, Adapter("claude-code-acp"))).Circle);
        Assert.Null(ChatRunner.HelpPlaceIn(_home, HelpRoom.PathOf(_home), [], "alpha").Circle);
        Assert.Null(ChatRunner.HelpPlaceIn(_home, HelpRoom.PathOf(_home), []).Circle);
        Assert.Null(ChatRunner.HelpPlaceIn(_home, HelpRoom.PathOf(_home, "default"), [], "Default").Circle);
    }

    /// <summary>
    /// 🔴 The knowledge connector's circle is the record's workspace: it carries the variable the connector reads, beside the
    /// session and the home, and carries none for a conversation bound to none. The name is a twin of the service's
    /// <c>AmbientWorkspace.BoundVariable</c>.
    /// </summary>
    [Fact]
    public void The_connector_carries_its_conversation_s_workspace_and_none_for_the_whole_machine()
    {
        Assert.Equal("DAORIS_SESSION_WORKSPACE", HelpCircle.Variable);

        var bound = ChatRunner.ConnectorScope(_home, "h1a2b3c4", "alpha");
        Assert.Equal("alpha", bound[HelpCircle.Variable]);
        Assert.Equal("h1a2b3c4", bound[IntakeRoom.SessionVariable]);
        Assert.Equal(_home, bound[KnowledgeConnector.RulesHomeVariable]);

        Assert.False(ChatRunner.ConnectorScope(_home, "h1a2b3c4", null).ContainsKey(HelpCircle.Variable));
    }

    /// <summary>
    /// 🔴 The checkout grants intersect: the checkouts a workspace's room may read are its own workspace's, and of those only the
    /// ones reading across already allows (D107: a repository's own setting, then its workspace's). <c>default</c>'s room reads
    /// every readable one, as before.
    /// </summary>
    [Fact]
    public void A_workspace_s_room_reads_its_own_checkouts_where_reading_across_allows()
    {
        var whole = HelpRoomScopeFixture.Machine();
        Assert.Equal(["engine"], whole.Within("alpha").Reads.Select(read => read.Name));
        Assert.Equal(["console"], whole.Within("beta").Reads.Select(read => read.Name));
        Assert.Equal(["console", "engine", "tools"], whole.Reads.Select(read => read.Name));

        var room = HelpRoom.Render(whole.Within("alpha"));
        Assert.Contains("- `engine` (workspace `alpha`) — `/alpha/engine`", room);
        Assert.DoesNotContain("/beta/console", room);
        Assert.DoesNotContain("/default/tools", room);

        var off = DriverConfig.Empty.WithReadAcross("engine", false).WithWorkspaceReadAcross("beta", false);
        var narrowed = HelpRoom.Describe(off, new Snapshot([], HelpRoomScopeFixture.Registry, []), [], [], _ => null, asks: 0);
        Assert.Empty(narrowed.Within("alpha").Reads);
        Assert.Empty(narrowed.Within("beta").Reads);
        Assert.Equal(["tools"], narrowed.Reads.Select(read => read.Name));
    }

    /// <summary>
    /// Its rules are the grants and the person's denies for its workspace (the permissions that already apply), never another
    /// workspace's; under full access they keep those denies. <c>default</c>'s conversation keeps <c>default</c>'s, as before.
    /// </summary>
    [Fact]
    public void Its_rules_carry_the_person_s_denies_for_its_own_workspace()
    {
        var file = PermissionFile.Empty with
        {
            Workspaces = new Dictionary<string, RuleLists>
            {
                ["alpha"] = new([], [], ["Read(//alpha/secret/**)"]),
                ["beta"] = new([], [], ["Read(//beta/secret/**)"]),
                ["default"] = new([], [], ["Read(//default/secret/**)"]),
            },
        };
        var alpha = ChatRunner.HelpPlaceIn(_home, HelpRoom.PathOf(_home, "alpha"), [new("engine", "alpha", "/alpha/engine")], "alpha");

        var rules = alpha.Rules(file, "s1");
        Assert.Contains("Read(//alpha/engine/**)", rules.Allow);
        Assert.Contains("Read(//alpha/secret/**)", rules.Deny);
        Assert.DoesNotContain("Read(//beta/secret/**)", rules.Deny);
        Assert.DoesNotContain("Read(//default/secret/**)", rules.Deny);

        var full = ChatRunner.HelpUnder(alpha, HelpAccess.For(HelpModes.Full, Adapter("claude-code-acp"))).Rules(file, "s1");
        Assert.Contains("Read(//alpha/secret/**)", full.Deny);
        Assert.DoesNotContain("Read(//beta/secret/**)", full.Deny);

        var machine = ChatRunner.HelpPlaceIn(_home, HelpRoom.PathOf(_home), []).Rules(file, "s1");
        Assert.Contains("Read(//default/secret/**)", machine.Deny);
        Assert.DoesNotContain("Read(//alpha/secret/**)", machine.Deny);
    }

    /// <summary>
    /// The room says the binding in its first lines. Restricted: its tools answer for its workspace and refuse another. Full
    /// access (the modes design §7's trap, a workspace's label read as confinement): the shell reaches beyond the workspace, the
    /// workspace is no fence, and the connector is still scoped. <c>default</c>'s room says neither.
    /// </summary>
    [Fact]
    public void The_room_says_its_binding_first_and_under_full_access_that_it_is_no_fence()
    {
        var whole = HelpRoomScopeFixture.Machine();
        var restricted = HelpRoom.Render(whole.Within("alpha"));
        var full = HelpRoom.Render(whole.Within("alpha") with { Mode = HelpModes.Full });

        static int Before(string room, string words)
        {
            var at = room.IndexOf(words, StringComparison.Ordinal);
            Assert.True(at > 0 && at < room.IndexOf("## What you may do", StringComparison.Ordinal), room);
            return at;
        }

        Before(restricted, "`daoris-knowledge` answers for `alpha` alone");
        Before(restricted, "The checkouts you may read are `alpha`'s");
        Assert.DoesNotContain("not confined", restricted);

        Before(full, "Full access is not confined to `alpha`: your shell and file edits reach this whole machine");
        Before(full, "`alpha` is where this conversation belongs, not a fence around it");
        Before(full, "`daoris-knowledge` still answers for `alpha` alone");
        Assert.DoesNotContain("The checkouts you may read are", full);

        foreach (var machine in new[] { whole, whole with { Mode = HelpModes.Full } })
        {
            var said = HelpRoom.Render(machine);
            Assert.DoesNotContain("answers for `", said);
            Assert.DoesNotContain("not confined", said);
        }
    }
}
