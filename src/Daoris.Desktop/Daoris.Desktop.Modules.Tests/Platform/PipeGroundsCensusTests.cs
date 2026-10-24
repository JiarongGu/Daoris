using System.Text.Json;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The grounds census beside the pipe's (CONFIRMWAIT1a-i, the confirm-wait design §5.1, §10): every route the census makes the person's,
/// in every form its rule gives, names its grounds or is timeless with its reason; a timeless route is none that deletes, closes or moves
/// anything; each field a ground names is one the route's handler reads; and each plan read's fingerprint takes what its row names and
/// none of the words, times and versions beside them, so two reads of an unchanged world across a moved clock and a new day give one
/// fingerprint and one moved item gives another.
/// </summary>
public sealed class PipeGroundsCensusTests
{
    private static readonly string[] Timeless = ["CHANNEL_MARK", "NOTICES_READ", "HELP_RENAME", "HELP_PIN"];

    [Fact]
    public void Every_person_route_names_its_grounds_and_every_row_is_a_person_route()
    {
        var person = PipeCensus.Daoris.Routes
            .Where(each => each.Route is not null && (each.Class == PipeClass.Person || each.When?.Class == PipeClass.Person))
            .Select(each => PipeTargets.Key(each.Module, each.Route!))
            .ToHashSet(StringComparer.Ordinal);

        Assert.Empty(person.Where(each => !PipeTargets.Daoris.Rows.ContainsKey(each)).Order(StringComparer.Ordinal));
        Assert.Empty(PipeTargets.Daoris.Rows.Keys.Where(each => !person.Contains(each)).Order(StringComparer.Ordinal));
        // A route a route-by-route module leaves to the person by default has no row, and is refused `ungrounded` (PipeConfirmTests).
        Assert.Null(PipeTargets.Daoris.Row(DriverModule.Name, "A_ROUTE_NOBODY_CLASSED"));
    }

    [Fact]
    public void Only_what_deletes_moves_and_starts_nothing_is_timeless_and_each_says_why()
    {
        var timeless = PipeTargets.Daoris.Rows.Where(each => each.Value.Grounds.Count == 0).ToList();

        Assert.Equal(Timeless.Order(StringComparer.Ordinal), timeless.Select(each => each.Key).Order(StringComparer.Ordinal));
        Assert.All(timeless, each => Assert.False(string.IsNullOrWhiteSpace(each.Value.Timeless)));
        Assert.All(PipeTargets.Daoris.Rows.Where(each => each.Value.Grounds.Count > 0), each => Assert.Null(each.Value.Timeless));
        foreach (var verb in new[] { "DELETE", "CLEAR", "DISCARD", "DROP", "SWEEP", "SYNC", "LAND", "ABANDON", "FREE", "STOP", "END", "ARCHIVE", "HANDOFF" })
        {
            Assert.DoesNotContain(timeless, each => each.Key.Contains(verb, StringComparison.Ordinal));
        }
    }

    /// <summary>Each field a ground names is one its route's handler reads, as the handler's own source spells it.</summary>
    [Fact]
    public void Each_field_a_ground_names_is_one_its_route_s_handler_reads()
    {
        var routes = Path.Combine(RepositoryRoot(), "src", "Daoris.Desktop", "Daoris.Desktop.Modules");
        var sources = Directory.GetFiles(routes, "*.cs", SearchOption.AllDirectories).ToDictionary(each => each, File.ReadAllText);
        var missing = new List<string>();
        foreach (var (key, row) in PipeTargets.Daoris.Rows)
        {
            var route = key.Contains('.', StringComparison.Ordinal) ? key[(key.LastIndexOf('.') + 1)..] : key;
            var mark = key.Contains('.', StringComparison.Ordinal) ? $"case \"{route}\"" : $"[DriverRoute(\"{route}\")]";
            var handler = sources.Values.Where(each => each.Contains(mark, StringComparison.Ordinal)).ToList();
            Assert.True(handler.Count > 0, $"no handler marks {key}");
            foreach (var field in row.Grounds.SelectMany(ground => ground.Fields).Distinct(StringComparer.Ordinal))
            {
                if (!handler.Any(each => each.Contains($"\"{field}\"", StringComparison.Ordinal))) missing.Add($"{key}.{field}");
            }
        }

        Assert.Empty(missing);
    }

    /// <summary>
    /// One pair per plan read (§10's next-morning row): the same world read again after a night, an update and a commit on the line, its
    /// words and times all moved, gives one fingerprint; one moved item gives another.
    /// </summary>
    [Theory]
    [MemberData(nameof(Plans))]
    public async Task A_plan_s_fingerprint_takes_what_it_acts_on_and_leaves_out_its_words_and_times(
        string route, string read, string asked, string moved, string payload)
    {
        var ground = Assert.Single(PipeTargets.Daoris.Row(DriverModule.Name, route)!.Grounds.OfType<ReadGround>(), each => each.Kind == PipeGroundKind.Plan);
        Assert.Equal(read, ground.Route);
        var press = JsonDocument.Parse(payload).RootElement;

        var morning = await Fingerprint(ground, asked, press, later: false);
        var nextDay = await Fingerprint(ground, asked, press, later: true);
        var other = await Fingerprint(ground, moved, press, later: false);

        Assert.Equal(morning, nextDay);
        Assert.NotEqual(morning, other);
    }

    public static TheoryData<string, string, string, string, string> Plans() => new()
    {
        {
            "SETUP_PRESS", "SETUP_PLAN",
            """{"repository":"Lyntai","workspace":"games","line":"main","commit":"0a1b2c3d4e5f","rules":["one","two"],"title":"Set up Lyntai @@DAY@@","body":"Daoris @@VERSION@@ on @@DAY@@","daoris":"@@VERSION@@","node":"24","refusals":[{"code":"no-checkout","sentence":"@@WORDS@@"}],"pressable":true}""",
            """{"repository":"Lyntai","workspace":"games","line":"main","commit":"0a1b2c3d4e5f","rules":["one","three"],"title":"t","body":"b","refusals":[{"code":"no-checkout","sentence":"s"}],"pressable":true}""",
            """{"repository":"Lyntai"}"""
        },
        {
            "LAND_SESSION_TREE", "LANDING",
            """{"session":"s1","form":"merge","target":"main","source":"quest/x","own":true,"stands":true,"uncommitted":0,"lands":[{"commit":"0a1b2c3d","subject":"@@WORDS@@"}],"opinion":{"state":"settled","until":"@@DAY@@","says":"@@WORDS@@"}}""",
            """{"session":"s1","form":"merge","target":"main","source":"quest/x","own":true,"stands":true,"uncommitted":0,"lands":[{"commit":"9f8e7d6c","subject":"x"}],"opinion":{"state":"settled"}}""",
            """{"id":"s1"}"""
        },
        {
            "SWEEP", "SWEEP_PLAN",
            """{"branches":[{"repository":"Lyntai","branch":"quest/x","kind":"landed","commits":2,"detail":"@@WORDS@@","removable":true,"discardable":false}],"landed":[]}""",
            """{"branches":[{"repository":"Lyntai","branch":"quest/x","kind":"unlanded","commits":3,"detail":"x","removable":false,"discardable":false}],"landed":[]}""",
            """{"workspace":"games"}"""
        },
        {
            "TREES_SYNC", "TREES_SYNC_PLAN",
            """{"lines":[{"repository":"Lyntai","line":"main","kind":"behind","from":"0a1b2c3d","to":"1b2c3d4e","commits":1,"moves":true,"lastFetch":"@@DAY@@","detail":"@@WORDS@@"}],"rebases":[],"deletes":[]}""",
            """{"lines":[{"repository":"Lyntai","line":"main","kind":"behind","from":"0a1b2c3d","to":"2c3d4e5f","commits":2,"moves":true}],"rebases":[],"deletes":[]}""",
            """{"all":true}"""
        },
        {
            "HISTORY_CLEAR", "HISTORY_PLAN",
            """{"scope":"quest","id":"q1","units":[{"kind":"quest","id":"q1","clearable":true,"bytes":{"total":"@@SIZE@@"}}]}""",
            """{"scope":"quest","id":"q1","units":[{"kind":"quest","id":"q1","clearable":false,"keep":{"code":"HISTORY_RUNNING"}}]}""",
            """{"quest":"q1","units":[{"kind":"quest","id":"q1"}]}"""
        },
        {
            "WORK_ABANDON", "WORK_PLAN",
            """{"quests":[{"quest":"q1","title":"@@WORDS@@","status":"Taken","joined":"asked","key":"quest:q1","abandon":"decline"}],"sessions":[{"session":"s1","state":"working"}],"abandon":{"pieces":["quest:q1"]},"paused":{"at":"@@DAY@@"}}""",
            """{"quests":[{"quest":"q1","title":"t","status":"Done","joined":"asked","key":"quest:q1","abandon":"decline"}],"sessions":[{"session":"s1","state":"working"}],"abandon":{"pieces":["quest:q1"]}}""",
            """{"quest":"q1","pieces":["quest:q1"],"reason":"r"}"""
        },
        {
            "SYSTEM_DOWNLOADS_CLEAR", "SYSTEM_DOWNLOADS",
            """{"versions":[{"kind":"tool","name":"git","version":"2.47","bytes":"@@SIZE@@","path":"@@WORDS@@","unfinished":false}]}""",
            """{"versions":[{"kind":"tool","name":"git","version":"2.48","unfinished":false}]}""",
            """{"versions":[{"kind":"tool","name":"git","version":"2.47"}]}"""
        },
        {
            "SYSTEM_SPACE_FREE", "SYSTEM_SPACE_FREE",
            """{"at":"@@DAY@@","now":[{"kind":"tree","id":"t1","bytes":"@@SIZE@@","why":"@@WORDS@@"}],"early":[]}""",
            """{"now":[{"kind":"tree","id":"t2"}],"early":[]}""",
            """{"units":[{"kind":"tree","id":"t1"}]}"""
        },
        {
            "HARNESS_ACTION", "HARNESSES",
            """{"harnesses":[{"harness":"claude-code","present":true,"version":"2.0","profiles":[{"name":"work","login":"signed-in","read":"@@DAY@@","account":"@@WORDS@@"}]}]}""",
            """{"harnesses":[{"harness":"claude-code","present":true,"version":"2.1","profiles":[{"name":"work","login":"signed-in"}]}]}""",
            """{"action":"update","harness":"claude-code"}"""
        },
        {
            "PLUGIN_ACTION", "PLUGINS",
            """{"plugins":[{"id":"p1","version":"1.0","enabled":true,"listening":true,"health":{"state":"ok","since":"@@DAY@@"},"description":"@@WORDS@@"}]}""",
            """{"plugins":[{"id":"p1","version":"1.1","enabled":true,"listening":true}]}""",
            """{"action":"disable","id":"p1"}"""
        },
        {
            "TOOLS_DELETE", "TOOLS_LIST",
            """{"tools":[{"tool":"git","way":"managed","version":"2.47","downloaded":[{"version":"2.47","size":"@@SIZE@@"}],"name":"@@WORDS@@"}]}""",
            """{"tools":[{"tool":"git","way":"managed","version":"2.47","downloaded":[{"version":"2.48"}]}]}""",
            """{"tool":"git","version":"2.47"}"""
        },
        {
            "HANDOFF", "HANDOFF_PLAN",
            """{"session":"s1","branch":"quest/x","repository":"Lyntai","plugin":"github","commits":2,"pullRequest":"@@WORDS@@"}""",
            """{"session":"s1","branch":"quest/x","repository":"Lyntai","plugin":"github","commits":3}""",
            """{"id":"s1"}"""
        },
        {
            "LAND_ENTRY", "ENTRY_LANDING",
            """{"entry":"ask:a1/Lyntai","repository":"Lyntai","form":"merge","target":"main","parts":[{"session":"s1","lands":[{"commit":"0a1b2c3d","subject":"@@WORDS@@"}],"held":"held"}]}""",
            """{"entry":"ask:a1/Lyntai","repository":"Lyntai","form":"merge","target":"main","parts":[{"session":"s1","lands":[{"commit":"0a1b2c3d"},{"commit":"1b2c3d4e"}],"held":"held"}]}""",
            """{"entry":"ask:a1/Lyntai"}"""
        },
    };

    /// <summary>
    /// The read's answer made as it was asked (<paramref name="later"/> false: the evening before) or the next morning, a newer Daoris
    /// after an update, sizes and words all moved and nothing it acts on, and its fingerprint.
    /// </summary>
    private static async Task<string> Fingerprint(ReadGround ground, string answer, JsonElement press, bool later)
    {
        var world = new StubGrounds();
        var made = answer
            .Replace("@@DAY@@", later ? "2026-10-25T09:00:00Z" : "2026-10-24T15:00:00Z", StringComparison.Ordinal)
            .Replace("@@VERSION@@", later ? "0.0.2" : "0.0.1", StringComparison.Ordinal)
            .Replace("\"@@SIZE@@\"", later ? "2048" : "1024", StringComparison.Ordinal)
            .Replace("@@WORDS@@", later ? "said the next morning" : "said the evening before", StringComparison.Ordinal);
        world.Reads[ground.Route] = _ => JsonDocument.Parse(made).RootElement.Clone();
        return Assert.Single(await ground.ReadAsync(world, press, CancellationToken.None)).Value;
    }

    private static string RepositoryRoot()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        return at?.FullName ?? throw new InvalidOperationException("the census reads the checkout's sources.");
    }
}
