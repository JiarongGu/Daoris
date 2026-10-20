using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// PUSH1c3a (D182 points 2 to 4): the session list's groups pushed, the host's half. One owner in the shell's loop takes the look
/// <c>SESSION_GROUPS</c> takes, once per wake for every window, and pushes its rows keyed by session and its entries keyed by line of
/// work, each row with the sort inputs its group is ordered by, so one session's move pushes its row alone and a window composes the
/// order itself.
/// </summary>
/// <remarks>
/// The service stands in for the records and the quests; the loop's last look is recorded empty, so no fresh plan is read. No record
/// names a tree, so no git runs.
/// </remarks>
public sealed partial class GroupsStateSliceTests : DriverModuleBridge
{
    private static readonly DateTimeOffset At = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    private readonly StandIn _service = new();

    /// <summary>A record as the service answers it: a conversation where it serves no quest, its transcript a path the pull keeps.</summary>
    private static JsonObject Record(string id, string state, string? quest, int minute, string repository = "engine") => new()
    {
        ["id"] = id, ["quest"] = quest, ["repository"] = repository, ["adapter"] = "claude-code", ["state"] = state,
        ["kind"] = quest is null ? "chat" : "driven", ["created"] = $"2026-10-20T09:{minute:00}:00Z", ["updated"] = $"2026-10-20T09:{minute + 1:00}:00Z",
        ["transcript"] = $"X:/daoris/transcripts/{id}.jsonl",
    };

    private static string Quest(string id, string status) =>
        $$"""{"id":"{{id}}","from":"game","to":"engine","title":"The work of #{{id}}","body":"A body.","status":"{{status}}"}""";

    /// <summary>
    /// Six sessions over five quests: one waiting on you, two working in two repositories (the later begun named first by its
    /// repository, without case), and three ended.
    /// </summary>
    private static JsonObject[] Six() =>
    [
        Record("wait1", "awaiting-person", "q2", 30), Record("run1", "working", "q3", 40), Record("run2", "working", "q5", 45, "Alpha"),
        Record("done1", "completed", "q4", 50), Record("chat1", "completed", null, 55), Record("fail1", "failed", "q1", 0),
    ];

    private static string Records(params JsonObject[] records) => new JsonArray([.. records.Select(record => record.DeepClone())]).ToJsonString();

    /// <summary>A loop come up over the stand-in, its last look recorded empty, and its groups' slice.</summary>
    private async Task<(DriverLoop Loop, StateSlice Slice)> UpAsync(params JsonObject[] records)
    {
        _service.Sessions = Records(records);
        _service.Quests = $"[{Quest("q1", "Open")},{Quest("q2", "Taken")},{Quest("q3", "Taken")},{Quest("q4", "Done")},{Quest("q5", "Taken")}]";
        var loop = Loop();
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(_service)));
        loop.Look.Record([]);
        return (loop, loop.Slices.Find(GroupsStateSlice.Name)!);
    }

    private IReadOnlyList<SlicePush> Pushes(string? partition = null) =>
    [
        .. Raised.Where(each => each.Type == StateSlice.PushType).Select(each => (SlicePush)each.Payload!)
            .Where(push => push.Slice == GroupsStateSlice.Name && (partition is null || push.Partition == partition)),
    ];

    private static JsonObject Written(object? data) => JsonSerializer.SerializeToNode(data, IpcJson.Options)!.AsObject();

    private static JsonObject Partition(SliceSnapshot snapshot, string name) => Written(snapshot.Partitions.Single(each => each.Partition == name).Data);

    /// <summary>A pulled answer as the wire writes it: <c>IpcJson</c> leaves a null out, the pull's included.</summary>
    private static JsonNode? Wire(JsonElement pulled) => WithoutNulls(JsonNode.Parse(pulled.GetRawText()));

    private static JsonNode? WithoutNulls(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject written:
                foreach (var (name, _) in written.Where(each => each.Value is null).ToList()) written.Remove(name);
                foreach (var (_, value) in written) WithoutNulls(value);
                break;
            case JsonArray list:
                foreach (var each in list) WithoutNulls(each);
                break;
        }

        return node;
    }

    /// <summary>
    /// The page's compose, twinned here (D182's PUSH1c3a note): the groups in the reader's order, and within each the rows by the sort
    /// inputs its group pushed, as <c>SessionGroups.Ordered</c> orders them.
    /// </summary>
    private static List<JsonObject> Composed(JsonObject rows)
    {
        var all = rows.Select(each => each.Value!.AsObject()).ToList();
        DateTimeOffset When(JsonObject row, string field) => row[field]!.GetValue<DateTimeOffset>();
        string Id(JsonObject row) => row["session"]!.GetValue<string>();
        return [.. SessionGroup.Order.SelectMany(group =>
        {
            var placed = all.Where(row => row["group"]!.GetValue<string>() == group);
            return group switch
            {
                SessionGroup.You or SessionGroup.Later => placed.OrderBy(row => When(row, "updated")).ThenBy(Id, StringComparer.Ordinal),
                SessionGroup.Review or SessionGroup.Working => placed
                    .OrderBy(row => row["repository"]!.GetValue<string>(), StringComparer.OrdinalIgnoreCase)
                    .ThenBy(row => When(row, "created")).ThenBy(Id, StringComparer.Ordinal),
                _ => placed.OrderByDescending(row => When(row, "updated")).ThenBy(Id, StringComparer.Ordinal),
            };
        })];
    }

    /// <summary>
    /// Each row is <c>SESSION_GROUPS</c>' row as its builder writes it, bar its running call, with the sort inputs its group is ordered
    /// by; each entry is its entry. Composed as the page will, the rows are the pull's in the pull's order, and the entries, by their
    /// leading row's place, the pull's in its order. The look is kept for Ask Daoris's room, as the pull's whole look is.
    /// </summary>
    [Fact]
    public async Task Rows_by_session_and_entries_by_key_compose_into_the_pull_s_answer_in_the_pull_s_order()
    {
        var (loop, slice) = await UpAsync(Six());

        await slice.JudgeAsync();
        var snapshot = slice.Snapshot();
        var pulled = await AnswerAsync(new DriverModule(Bus, loop), "SESSION_GROUPS");

        Assert.Equal((GroupsStateSlice.Name, StateSlice.Ready), (snapshot.Slice, snapshot.Status));
        Assert.Equal([GroupsStateSlice.Entries, GroupsStateSlice.Rows], snapshot.Partitions.Select(each => each.Partition));
        var rows = Partition(snapshot, GroupsStateSlice.Rows);
        var composed = Composed(rows);
        var pulledRows = pulled.GetProperty("sessions").EnumerateArray().ToList();
        Assert.Equal(pulledRows.Select(row => row.GetProperty("session").GetString()), composed.Select(row => row["session"]!.GetValue<string>()));
        // The working rows by repository without case: Alpha's, begun later, before engine's.
        Assert.Equal(["run2", "run1"], composed.Where(row => row["group"]!.GetValue<string>() == SessionGroup.Working).Select(row => row["session"]!.GetValue<string>()));
        foreach (var (row, pushed) in pulledRows.Zip(composed))
        {
            var bare = pushed.DeepClone().AsObject();
            foreach (var field in GroupsStateSlice.OrderedBy(pushed["group"]!.GetValue<string>())) Assert.True(bare.Remove(field), $"no `{field}` on {bare}");
            Assert.True(JsonNode.DeepEquals(Wire(row), bare), $"pulled {row.GetRawText()}\npushed {bare}");
        }

        var entries = Partition(snapshot, GroupsStateSlice.Entries);
        var place = composed.Select((row, at) => (Session: row["session"]!.GetValue<string>(), At: at)).ToDictionary(each => each.Session, each => each.At);
        var ordered = entries.Select(each => each.Value!.AsObject()).OrderBy(entry => place[entry["leading"]!.GetValue<string>()]).ToList();
        var pulledEntries = pulled.GetProperty("entries").EnumerateArray().ToList();
        Assert.Equal(pulledEntries.Count, ordered.Count);
        foreach (var (entry, pushed) in pulledEntries.Zip(ordered))
        {
            Assert.True(JsonNode.DeepEquals(Wire(entry), pushed), $"pulled {entry.GetRawText()}\npushed {pushed}");
            Assert.Same(pushed, entries[entry.GetProperty("entry").GetString()!]);
        }

        Assert.NotNull(loop.SessionsLooked.Within(TimeSpan.FromMinutes(1), DateTimeOffset.UtcNow));
    }

    /// <summary>
    /// The allow-list's twins: every field <c>SESSION_GROUPS</c> answers of a row and an entry is pushed or named the pull's alone, every
    /// field the page's types read of them is pushed or the pull's, and the sort inputs are the push's alone.
    /// </summary>
    [Fact]
    public void Every_field_the_route_answers_is_classed_and_every_field_the_page_reads_is_pushed_or_the_pull_s()
    {
        var row = new SessionGrouping("s1", SessionGroup.Later, "completed")
        {
            Archived = true, Strikes = 2, Awaits = "q9", AwaitsOf = "engine", Work = new TreeWork(3, 1) { Branch = "daoris/s1" }, HoldsQuest = true,
            PausedBy = new PausedBy(WorkScope.Ask, "a1"), Holds = new WordsHold(WordsHold.Cooling, "its account cools") { Repository = "engine", Until = At },
            Deletable = true, Lands = new LandOffer("daoris/s1", "s1", 3, 1),
            Discards = new DiscardOffer("daoris/s1", "s1", new HeldWork(new ContentHold("main", Squash: true), "refs/daoris/kept/s1", null)),
            Calling = new RunningCall("npm test", At),
        };
        var entry = new SessionEntry("quest:q1/engine", "quest:q1", "engine", SessionGroup.Later, "s1",
            [new EntryPart("s1", "q1", "completed", SessionGroup.Later, PartBegan.Asked) { After = "s0", AskedBy = "game" }]);

        var route = FieldsOf(Written(SessionsRoutes.Grouped(row)), "row").Concat(FieldsOf(Written(SessionsRoutes.Entry(entry)), "entry")).ToList();
        var pushed = GroupsStateSlice.Fields.SelectMany(shape => shape.Value.Select(field => $"{shape.Key}.{field}")).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("discards.keptAt", route);
        Assert.Contains("part.askedBy", route);
        Assert.All(route, field => Assert.True(Classed(field), $"`{field}` is neither pushed nor the pull's"));
        Assert.Empty(pushed.Intersect(GroupsStateSlice.PulledOnly));
        // Nothing named that the route no longer writes, bar the sort inputs, which the route answers none of.
        var order = GroupsStateSlice.OrderFields.Select(field => $"row.{field}").ToList();
        Assert.Empty(order.Intersect(route));
        Assert.Equal(pushed.Except(order).Order(StringComparer.Ordinal), route.Where(pushed.Contains).Distinct().Order(StringComparer.Ordinal));
        Assert.Equal(SessionsCommand.JsonEntryFields, GroupsStateSlice.Fields["entry"]);
        Assert.Equal(SessionsCommand.JsonPartFields, GroupsStateSlice.Fields["part"]);

        var page = PageFields();
        Assert.Contains("row.lands", page);
        Assert.Contains("discards.keptAt", page);
        Assert.Contains("part.askedBy", page);
        Assert.All(page, field => Assert.True(Classed(field), $"the page reads `{field}`, which is neither pushed nor the pull's"));
    }

    [Fact]
    public async Task A_hostile_field_a_transcript_and_a_running_call_never_reach_a_partition()
    {
        var hostile = Record("run1", "working", "q3", 40);
        hostile["token"] = "sk-hostile-top";
        var (loop, slice) = await UpAsync(hostile, Record("done1", "completed", "q4", 50));
        loop.Events.Append("run1", new SessionEvent { Kind = SessionEventKind.Tool, Id = "c1", Title = "sk-hostile-call", Status = "in_progress", At = At });

        await slice.JudgeAsync();
        var pulled = await AnswerAsync(new DriverModule(Bus, loop), "SESSION_GROUPS");

        // The pull says the call; the push leaves it to the calls' slice, which the page overlays.
        Assert.Contains("sk-hostile-call", pulled.GetRawText());
        var pushed = JsonSerializer.Serialize(Pushes().Select(push => push.Data), IpcJson.Options);
        Assert.Contains("run1", pushed);
        Assert.DoesNotContain("sk-hostile", pushed);
        Assert.DoesNotContain("calling", pushed);
        Assert.DoesNotContain("transcripts", pushed);
    }

    /// <summary>
    /// A session's move pushes its row alone, a delta on the number held, and its entry alone; a working session whose record moved
    /// only when it was last written, which its group is not ordered by, pushes nothing.
    /// </summary>
    [Fact]
    public async Task One_session_s_move_pushes_its_row_and_its_entry_alone_and_a_working_record_s_write_pushes_nothing()
    {
        var records = Six();
        var (_, slice) = await UpAsync(records);
        await slice.JudgeAsync();
        Assert.Equal(2, Pushes().Count);

        records[2]["updated"] = "2026-10-20T09:58:00Z";
        _service.Sessions = Records(records);
        await slice.JudgeAsync();
        Assert.Equal(2, Pushes().Count);

        records[1]["state"] = "completed";
        records[1]["updated"] = "2026-10-20T09:59:00Z";
        _service.Sessions = Records(records);
        await slice.JudgeAsync();

        var row = Assert.Single(Pushes(GroupsStateSlice.Rows).Skip(1));
        Assert.Equal((2L, (long?)1L), (row.Seq, row.BaseSeq));
        Assert.Equal(["run1"], Written(row.Data).Select(each => each.Key));
        Assert.Equal((SessionGroup.Ended, "2026-10-20T09:59:00+00:00"),
            (Written(row.Data)["run1"]!["group"]!.GetValue<string>(), Written(row.Data)["run1"]!["updated"]!.GetValue<string>()));
        var entry = Assert.Single(Pushes(GroupsStateSlice.Entries).Skip(1));
        Assert.Equal((2L, (long?)1L), (entry.Seq, entry.BaseSeq));
        var moved = Assert.Single(Written(entry.Data));
        Assert.Equal("run1", moved.Value!["leading"]!.GetValue<string>());
    }

    [Fact]
    public async Task Before_the_service_answers_the_slice_is_not_judged_and_pushes_nothing()
    {
        var slice = Loop().Slices.Find(GroupsStateSlice.Name)!;

        await slice.JudgeAsync();

        Assert.Equal(StateSlice.NotJudged, slice.Snapshot().Status);
        Assert.Empty(Pushes());
    }

    [Fact]
    public async Task A_second_window_s_snapshot_is_read_from_memory_and_judges_nothing()
    {
        var (loop, slice) = await UpAsync(Six());
        var module = new DriverModule(Bus, loop);
        await slice.JudgeAsync();

        var first = await AnswerAsync(module, "STATE_SLICE", new { slice = GroupsStateSlice.Name });
        var second = await AnswerAsync(module, "STATE_SLICE", new { slice = GroupsStateSlice.Name });

        Assert.Equal(1, slice.Judgements);
        Assert.Equal(first.GetRawText(), second.GetRawText());
        Assert.Equal(StateSlice.Ready, first.GetProperty("status").GetString());
        Assert.Equal([GroupsStateSlice.Entries, GroupsStateSlice.Rows],
            first.GetProperty("partitions").EnumerateArray().Select(each => each.GetProperty("partition").GetString()));
    }

    /// <summary>
    /// A record or a quest a connector wrote reaches the windows with no desktop dispatch: the change stream's `sessions` and `quests`
    /// families each wake the slice, which takes the look again; another family and a ref's move judge nothing.
    /// </summary>
    [Fact]
    public async Task The_ledger_s_sessions_and_quests_families_wake_it_and_another_family_or_a_ref_does_not()
    {
        var records = Six();
        var (loop, slice) = await UpAsync(records);
        using var stop = new CancellationTokenSource();
        var running = slice.RunAsync(stop.Token);
        await UntilAsync(() => Pushes().Count == 2);

        var judged = slice.Judgements;
        loop.HeardMove(ChangeTopics.Ledger, "asks,channel");
        loop.HeardMove(ChangeTopics.Refs, "engine");
        await Task.Delay(300);
        Assert.Equal(judged, slice.Judgements);

        loop.HeardMove(ChangeTopics.Ledger, "quests");
        await UntilAsync(() => slice.Judgements > judged);

        records[0]["state"] = "failed";
        _service.Sessions = Records(records);
        loop.HeardMove(ChangeTopics.Ledger, "channel,sessions");
        await UntilAsync(() => Pushes(GroupsStateSlice.Rows).Count == 2);
        Assert.Equal(["wait1"], Written(Pushes(GroupsStateSlice.Rows)[^1].Data).Select(each => each.Key));
        await stop.CancelAsync();
        await running;
    }

    /// <summary>A tree's part landing wakes the slice, by its value's reference; the sessions' look and the work land often and wake nothing.</summary>
    [Fact]
    public async Task A_tree_s_part_landing_wakes_it_and_no_other_fact()
    {
        using var facts = new MachineFacts();
        facts.Topic(HelpFacts.Trees, HelpFacts.TreesRules, (_, _) => Task.FromResult<object?>(HelpTrees.None with { }));
        facts.Topic(HelpFacts.Sessions, HelpFacts.WholeRules, (_, _) => Task.FromResult<object?>(new SessionLook([], [], [])));
        var wakes = 0;
        var hearing = GroupsStateSlice.HearTrees(facts, () => Interlocked.Increment(ref wakes));
        facts.Start();

        await facts.JudgedAsync(HelpFacts.Trees, "engine", CancellationToken.None).WaitAsync(Within);
        Assert.Equal(1, Volatile.Read(ref wakes));
        await facts.JudgedAsync(HelpFacts.Sessions, HelpFacts.Whole, CancellationToken.None).WaitAsync(Within);
        Assert.Equal(1, Volatile.Read(ref wakes));
        await facts.JudgedAsync(HelpFacts.Trees, HelpFacts.NoCheckout, CancellationToken.None).WaitAsync(Within);
        Assert.Equal(2, Volatile.Read(ref wakes));

        hearing.Dispose();
        await facts.JudgedAsync(HelpFacts.Trees, "engine", CancellationToken.None).WaitAsync(Within);
        Assert.Equal(2, Volatile.Read(ref wakes));
    }

    [Theory]
    [InlineData("STATE_CHANGED", """{"scopes":["sessions"],"sessions":["s1"]}""", true)]
    [InlineData("STATE_CHANGED", """{"scopes":["driver"]}""", false)]
    [InlineData("STATE_CHANGED", """{"scopes":["quests","asks"]}""", false)]
    [InlineData("STATE_CHANGED", """{"scopes":["landing","sweep"]}""", false)]
    [InlineData("SESSION_ENDED", "{}", true)]
    [InlineData("DRIVER_READY", "{}", true)]
    // The loop's look told to the windows: where every window pulled the groups before.
    [InlineData("DRIVER_TICK", "{}", true)]
    // ACCEPTFOOT1b: a conversation's turns moving, whose end offers its tree's work to land and whose next turn takes the offer back.
    [InlineData("SESSION_QUEUED", "{}", true)]
    [InlineData("SESSION_STREAMS", "{}", false)]
    public void What_wakes_the_groups_slice(string type, string payload, bool wakes)
    {
        var wake = GroupsStateSlice.WakesOn.FirstOrDefault(each => each.Type == type);
        object told = type == StatePublisher.Type ? JsonSerializer.Deserialize<StateChanged>(payload, IpcJson.Options)! : new { };

        Assert.Equal(wakes, wake is not null && (wake.When is null || wake.When(told)));
    }

    private static readonly TimeSpan Within = Poll.Patience;

    /// <summary>Whether a field, as <c>shape.field</c>, is pushed, or the pull's alone, itself or as part of a field the pull keeps.</summary>
    private static bool Classed(string field)
    {
        var (shape, name) = (field[..field.IndexOf('.')], field[(field.IndexOf('.') + 1)..]);
        return GroupsStateSlice.Fields.TryGetValue(shape, out var fields) && fields.Contains(name)
               || GroupsStateSlice.PulledOnly.Contains(field)
               || GroupsStateSlice.PulledOnly.Contains($"row.{shape}");
    }

    /// <summary>Each field of a written object as <c>shape.field</c>, and those of each shape it holds.</summary>
    private static IEnumerable<string> FieldsOf(JsonObject written, string shape)
    {
        foreach (var (name, value) in written)
        {
            yield return $"{shape}.{name}";
            var inner = GroupsStateSlice.Holds.GetValueOrDefault($"{shape}.{name}") ?? name;
            var objects = value switch
            {
                JsonObject one => [one],
                JsonArray many => many.OfType<JsonObject>().ToList(),
                _ => new List<JsonObject>(),
            };
            foreach (var each in objects)
            {
                foreach (var field in FieldsOf(each, inner)) yield return field;
            }
        }
    }

    /// <summary>
    /// Every field the page's types read of a row and an entry, as <c>shape.field</c>: <c>SessionGrouping</c> with its inline shapes
    /// named by their field, its offers by theirs, and an entry's parts (<c>work/groups.ts</c>, <c>work/entries.ts</c>).
    /// </summary>
    private static HashSet<string> PageFields()
    {
        var root = RepositoryRoot();
        var groups = File.ReadAllText(Path.Combine(root, "src", "Daoris.Web", "src", "work", "groups.ts"));
        var entries = File.ReadAllText(Path.Combine(root, "src", "Daoris.Web", "src", "work", "entries.ts"));
        var fields = new HashSet<string>(StringComparer.Ordinal);
        ReadType(TypeBody(groups, "SessionGrouping"), "row", fields);
        ReadType(TypeBody(groups, "LandOffer"), "lands", fields);
        ReadType(TypeBody(groups, "DiscardOffer"), "discards", fields);
        ReadType(TypeBody(entries, "SessionEntry"), "entry", fields);
        ReadType(TypeBody(entries, "EntryPart"), "part", fields);
        return fields;
    }

    private static string TypeBody(string text, string type)
    {
        var block = Regex.Match(text, $@"export type {type} = \{{(?<body>.*?)\}};", RegexOptions.Singleline);
        Assert.True(block.Success, $"no `export type {type} = {{`");
        return Comments().Replace(block.Groups["body"].Value, "");
    }

    /// <summary>The fields a type's body declares, each inline object type read as the shape its field names.</summary>
    private static void ReadType(string body, string shape, HashSet<string> into)
    {
        var at = 0;
        while (at < body.Length && PageField().Match(body, at) is { Success: true } field)
        {
            var name = field.Groups["name"].Value;
            into.Add($"{shape}.{name}");
            at = field.Index + field.Length;
            var (start, depth) = (at, 0);
            while (at < body.Length && !(depth == 0 && body[at] == ';'))
            {
                depth += body[at] switch { '{' => 1, '}' => -1, _ => 0 };
                at++;
            }

            var type = body[start..at];
            if (type.IndexOf('{') is var open and >= 0) ReadType(type[(open + 1)..type.LastIndexOf('}')], name, into);
            at++;
        }
    }

    /// <summary>Walk up to the workspace root, which the tests run under.</summary>
    private static string RepositoryRoot()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        return at?.FullName ?? throw new InvalidOperationException("no workspace root above the test binary");
    }

    [GeneratedRegex(@"/\*.*?\*/|//[^\n]*", RegexOptions.Singleline)]
    private static partial Regex Comments();

    [GeneratedRegex(@"(?<name>\w+)\??:")]
    private static partial Regex PageField();

    /// <summary>The service's doors a look at the sessions reads, standing in: the records and the quests as a test sets them.</summary>
    private sealed class StandIn : HttpMessageHandler
    {
        private string _sessions = "[]";
        private string _quests = "[]";

        public string Sessions
        {
            get => Volatile.Read(ref _sessions);
            set => Volatile.Write(ref _sessions, value);
        }

        public string Quests
        {
            get => Volatile.Read(ref _quests);
            set => Volatile.Write(ref _quests, value);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var answer = request.RequestUri!.PathAndQuery switch
            {
                SessionRecords.Door => Sessions,
                "/api/quests?includeClosed=true" => Quests,
                _ => "[]",
            };
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(answer, Encoding.UTF8, "application/json"),
            });
        }
    }
}
