using System.Text.Json;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The grounds bind what a press acts on, not a count or a name that stays the same while it changes (CONFIRMWAIT1, the confirm-wait
/// design §15.1, Codex's findings 1 to 5, 7 and 12): a branch rewritten to as many commits reads moved; a discard binds its tree by
/// <c>id</c> or <c>path</c>; a Resume binds the pause it was asked on; a repository binds what its name resolves to; a terminal's
/// attachments bind their bytes; a baseline that lacks a ground holds nothing; and a setting assigned whole is answered <c>already</c>
/// unless it is a named exception. Read through the stand-in for the loop's readers (<see cref="StubGrounds"/>).
/// </summary>
public sealed class PipeGroundsBindingTests
{
    private static JsonElement Payload(object payload) => JsonSerializer.SerializeToElement(payload);

    private static PipeGroundRow Row(string route, string module = DriverModule.Name) =>
        PipeTargets.Daoris.Row(module, route) ?? throw new InvalidOperationException($"no row for {route}");

    private static Task<IReadOnlyList<PipeReading>> ReadAsync(PipeGroundRow row, StubGrounds world, JsonElement payload) =>
        row.ReadAsync(world, payload, TimeProvider.System, CancellationToken.None);

    /// <summary>
    /// Finding 1: each plan read whose rows name branches binds each branch's full tip, so a branch rewritten to as many commits (its count,
    /// kind and short id kept) reads moved, and the same tips read again hold.
    /// </summary>
    [Theory]
    [MemberData(nameof(Rewrites))]
    public async Task A_branch_rewritten_to_as_many_commits_moves_its_plan(string route, string read, string answer, string payload, string moved)
    {
        var world = new StubGrounds();
        world.Reads[read] = _ => JsonDocument.Parse(answer).RootElement.Clone();
        world.Tips["Lyntai:quest/x"] = new string('a', 40);
        world.Tips["Lyntai:main"] = new string('b', 40);
        world.Tips["Lyntai:origin/main"] = new string('c', 40);
        world.TreeTips["s1:HEAD"] = new string('d', 40);
        world.TreeTips["s1:main"] = new string('e', 40);
        var press = JsonDocument.Parse(payload).RootElement;
        var row = Row(route);

        var then = await ReadAsync(row, world, press);
        var again = await ReadAsync(row, world, press);
        if (moved.StartsWith("s1:", StringComparison.Ordinal)) world.TreeTips[moved] = new string('f', 40);
        else world.Tips[moved] = new string('f', 40);
        var now = await ReadAsync(row, world, press);

        Assert.Null(PipeGroundRow.Judge(then, again));
        Assert.Equal("plan", PipeGroundRow.Judge(then, now)?.Reason);
    }

    public static TheoryData<string, string, string, string, string> Rewrites() => new()
    {
        {
            "SWEEP", "SWEEP_PLAN",
            """{"branches":[{"repository":"Lyntai","branch":"quest/x","kind":"unlanded","commits":2,"removable":false,"discardable":false}],"landed":[]}""",
            """{"workspace":"games"}""", "Lyntai:quest/x"
        },
        {
            "TREES_SYNC", "TREES_SYNC_PLAN",
            """{"lines":[{"repository":"Lyntai","line":"main","kind":"behind","from":"bbbbbbbb","to":"cccccccc","commits":1,"moves":true}],"rebases":[],"deletes":[]}""",
            """{"all":true}""", "Lyntai:origin/main"
        },
        {
            "TREES_SYNC", "TREES_SYNC_PLAN",
            """{"lines":[],"rebases":[{"repository":"Lyntai","branch":"quest/x","kind":"replay","commits":2,"replays":true}],"deletes":[]}""",
            """{"all":true}""", "Lyntai:quest/x"
        },
        {
            // An id-only hand-off names no branch in its payload: the branch its plan resolved is the one bound.
            "HANDOFF", "HANDOFF_PLAN",
            """{"session":"s1","branch":"quest/x","repository":"Lyntai","plugin":"github","commits":2}""",
            """{"id":"s1"}""", "Lyntai:quest/x"
        },
        {
            "LAND_SESSION_TREE", "LANDING",
            """{"session":"s1","form":"merge","target":"main","source":"quest/x","own":true,"stands":true,"uncommitted":0}""",
            """{"id":"s1"}""", "s1:main"
        },
        {
            "LAND_ENTRY", "ENTRY_LANDING",
            """{"entry":"ask:a1/Lyntai","repository":"Lyntai","form":"merge","target":"main","parts":[{"session":"s1","lands":[{"commit":"0a1b2c3d"}]}]}""",
            """{"entry":"ask:a1/Lyntai"}""", "s1:HEAD"
        },
        {
            "LAND_ASK", "ASK_LANDING",
            """{"entries":[{"entry":"ask:a1/Lyntai","repository":"Lyntai","form":"merge","target":"main","parts":[{"session":"s1"}]}]}""",
            """{"ask":"a1"}""", "Lyntai:main"
        },
    };

    /// <summary>Finding 2: a discard binds its tree whether the press names it by <c>id</c> or by <c>path</c>, forced or not.</summary>
    [Theory]
    [InlineData("""{"id":"s1"}""")]
    [InlineData("""{"id":"s1","force":true}""")]
    [InlineData("""{"path":"C:\\trees\\s1"}""")]
    [InlineData("""{"path":"C:\\trees\\s1","force":true}""")]
    public async Task A_discard_binds_the_tree_it_removes_by_id_or_path(string payload)
    {
        var world = new StubGrounds().Session("s1", "completed");
        world.Prints["tree"] = "one";
        var press = JsonDocument.Parse(payload).RootElement;
        var row = Row("DISCARD_SESSION_TREE");

        var then = await ReadAsync(row, world, press);
        world.Prints["tree"] = "two";
        var now = await ReadAsync(row, world, press);

        Assert.Equal("plan", PipeGroundRow.Judge(then, now)?.Reason);
        Assert.DoesNotContain(row.Grounds.OfType<LoopGround>(), each => each.What == "tree-uncommitted");
    }

    /// <summary>Finding 3: a Resume binds the pause it was asked on, so a pause removed and made again while the ask stays open moves it.</summary>
    [Fact]
    public async Task An_old_Resume_does_not_release_a_newer_pause()
    {
        var world = new StubGrounds();
        world.Asks["a1"] = "open";
        var at = "2026-10-24T08:00:00Z";
        world.Reads["WORK_PLAN"] = _ => new
        {
            scope = "ask", id = "a1", quests = new[] { new { quest = "q1", status = "Taken", pause = "paused" } },
            sessions = new[] { new { session = "s1", state = "stopped" } },
            paused = new { at, stopped = new[] { new { quest = "q1", session = "s1" } } },
        };
        var row = Row("WORK_RESUME");
        var press = Payload(new { ask = "a1" });

        var then = await ReadAsync(row, world, press);
        at = "2026-10-24T11:00:00Z";
        var now = await ReadAsync(row, world, press);

        Assert.Equal("plan", PipeGroundRow.Judge(then, now)?.Reason);
    }

    /// <summary>Finding 3: a Pause binds the work it would stop: a session started under the ask meanwhile moves it.</summary>
    [Fact]
    public async Task A_Pause_binds_the_work_it_stops()
    {
        var world = new StubGrounds();
        world.Asks["a1"] = "open";
        var sessions = new List<object> { new { session = "s1", state = "working" } };
        world.Reads["WORK_PLAN"] = _ => new { scope = "ask", id = "a1", quests = new[] { new { quest = "q1", status = "Taken", pause = "stop" } }, sessions = sessions.ToArray() };
        var row = Row("WORK_PAUSE");
        var press = Payload(new { ask = "a1" });

        var then = await ReadAsync(row, world, press);
        sessions.Add(new { session = "s2", state = "working" });
        var now = await ReadAsync(row, world, press);

        Assert.Equal("plan", PipeGroundRow.Judge(then, now)?.Reason);
    }

    /// <summary>Finding 4: a repository's name registered again at another checkout reads moved, though it is still registered.</summary>
    [Fact]
    public async Task A_name_registered_again_elsewhere_moves_its_repository_ground()
    {
        var world = new StubGrounds();
        world.Registrations["Lyntai"] = @"Lyntai|games|True|C:\checkouts\Lyntai";
        var row = Row("SET_TREES");
        var press = Payload(new { repository = "Lyntai", ownTree = true });
        world.Reads["STATE"] = _ => new { trees = Array.Empty<string>() };

        var then = await ReadAsync(row, world, press);
        world.Registrations["Lyntai"] = @"Lyntai|games|True|D:\elsewhere\Lyntai";
        var now = await ReadAsync(row, world, press);

        var change = PipeGroundRow.Judge(then, now);
        Assert.Equal("repository", change?.Reason);
        Assert.Equal("Lyntai", change!.Values["repository"]);
        Assert.Contains("as it was", change.Sentence, StringComparison.Ordinal);
    }

    /// <summary>Finding 5: words sent with files bind the files' bytes, so a file replaced at the same path moves the press.</summary>
    [Fact]
    public async Task A_terminal_s_attachment_replaced_at_its_path_moves_its_press()
    {
        var world = new StubGrounds().Session("s1", "awaiting-person");
        world.Prints["attachments"] = "one";
        var row = Row("SESSION_INPUT");
        var press = Payload(new { id = "s1", text = "see this", said = true, paths = new[] { @"C:\work\notes.md" } });

        var then = await ReadAsync(row, world, press);
        world.Prints["attachments"] = "two";
        var now = await ReadAsync(row, world, press);

        Assert.Equal("plan", PipeGroundRow.Judge(then, now)?.Reason);
        // Words sent without files bind none.
        Assert.DoesNotContain(await ReadAsync(row, world, Payload(new { id = "s1", text = "go on" })), each => each.Subject == "attachments");
    }

    /// <summary>Finding 7: a ground read now that the ask's baseline lacks is moved, so a baseline missing a ground protects nothing it omitted.</summary>
    [Fact]
    public void A_ground_the_baseline_lacks_is_moved()
    {
        var then = new[] { new PipeReading(PipeGroundKind.Session, "s1", "completed|1", "completed") };
        var now = new[]
        {
            new PipeReading(PipeGroundKind.Session, "s1", "completed|1", "completed"),
            new PipeReading(PipeGroundKind.Branch, "s1", "abcd"),
        };

        Assert.Equal("branch", PipeGroundRow.Judge(then, now)?.Reason);
        Assert.Null(PipeGroundRow.Judge(now, now));
    }

    /// <summary>Finding 7: each ground says what readings a payload yields, which the restore holds a record's grounds to.</summary>
    [Fact]
    public async Task Each_row_says_the_readings_its_payload_yields_as_it_reads_them()
    {
        var world = new StubGrounds().Session("s1", "completed");
        world.Registrations["Lyntai"] = "Lyntai|games|True|C:\\c";
        world.Quests["q1"] = "Open";
        world.Asks["a1"] = "open";
        foreach (var (route, payload) in new (string, object)[]
                 {
                     ("SESSION_ARCHIVE", new { ids = new[] { "s1", "s2" }, archived = true }),
                     ("LAND_SESSION_TREE", new { id = "s1" }),
                     ("HANDOFF", new { repository = "Lyntai", branch = "quest/x" }),
                     ("WORK_ABANDON", new { ask = "a1", pieces = new[] { "quest:q1" } }),
                     ("DISCARD_SESSION_TREE", new { path = "C:\\t", force = true }),
                     ("SESSION_INPUT", new { id = "s1", text = "x", question = "q" }),
                     ("SESSION_INPUT", new { id = "s1", text = "x", said = true, paths = new[] { "C:\\a.md" } }),
                 })
        {
            var press = Payload(payload);
            var row = Row(route);
            var read = await ReadAsync(row, world, press);

            Assert.Equal(
                read.Select(each => (each.Kind, each.Subject)).Order(),
                row.Expected(press).Order());
        }
    }

    /// <summary>Finding 12: heavy gates set whole are <c>already</c> where the list is the same, in order, and not where it differs.</summary>
    [Fact]
    public void Heavy_gates_already_as_asked_are_already()
    {
        var row = Row("SET_HEAVY_GATES");
        var ground = Assert.Single(row.Grounds.OfType<ReadGround>());
        var gates = new[] { new { name = "e2e", run = "npm run e2e", cwd = (string?)null }, new { name = "rehearse", run = "npm run rehearse", cwd = (string?)"tools" } };
        var world = JsonSerializer.SerializeToElement(new { heavyGates = new[] { new { repository = "Lyntai", gates } } });
        var state = new PipeReading(PipeGroundKind.Setting, "STATE", ground.Take(world, Payload(new { repository = "Lyntai" })));

        Assert.True(row.Already([state], Payload(new { repository = "Lyntai", gates })));
        Assert.False(row.Already([state], Payload(new { repository = "Lyntai", gates = gates.Reverse() })));
        Assert.False(row.Already([state], Payload(new { repository = "Lyntai" })));
    }

    /// <summary>Finding 12: every setting ground says the value it sets, or is a named exception with its reason.</summary>
    [Fact]
    public void Every_setting_ground_says_what_it_sets_or_why_it_cannot()
    {
        var unsaid = PipeTargets.Daoris.Rows
            .Where(each => each.Value.Grounds.OfType<ReadGround>().Any(ground => ground.Kind == PipeGroundKind.Setting && ground.Sets is null))
            .Select(each => each.Key)
            .Where(each => !PipeTargets.NotAlready.ContainsKey(each))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Empty(unsaid);
        Assert.All(PipeTargets.NotAlready, each => Assert.False(string.IsNullOrWhiteSpace(each.Value)));
        Assert.All(PipeTargets.NotAlready.Keys, each => Assert.True(PipeTargets.Daoris.Rows.ContainsKey(each), each));
    }
}
