using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The work milestones pushed (PUSH1c2a, D182 points 3-4): one owner in the shell's loop judging, from the machine's facts' work parts
/// (FACTSPLIT1a) and the node records' held attempts, a partition per repository carrying its part of what <c>WORK_STATE</c> answers and
/// one of the attempts held for the person, pushed when one moves and read by a window's first snapshot from memory.
/// </summary>
/// <remarks>
/// Two stands. The live one keeps the facts as the module keeps them, the work read from this test's home by the driver's own reader
/// (<see cref="HelpFacts.ReadWorkAsync"/>), the service's answers from a stand-in, no checkout folder on disk: so no git, and the route
/// answers beside it. The stand-in one keeps the facts on a clock the test moves, each part's work as the test says it.
/// </remarks>
public sealed class WorkStateSliceTests : DriverModuleBridge
{
    private static readonly TimeSpan Within = Poll.Patience;

    private static readonly DateTimeOffset At = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    /// <summary>What the page is handed, as <see cref="Bridge"/>'s answers are read: camelCase, nulls written.</summary>
    private static readonly JsonSerializerOptions Page = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly StandIn _service = new();

    private string Root(string repository) => Path.Combine(Home, "checkouts", repository).Replace('\\', '/');

    /// <summary>The registry as the service answers it: each repository in its workspace, with a checkout here or none.</summary>
    private string Registry(params (string Repository, string Workspace, bool Checkout)[] rows) =>
        JsonSerializer.Serialize(rows.Select(row => new
        {
            repository = row.Repository, workspace = row.Workspace, adopted = true, root = row.Checkout ? Root(row.Repository) : null,
        }));

    /// <summary>A session branch the driver recorded as its tree opened: a unit of <paramref name="repository"/>'s work.</summary>
    private void Grown(string repository, string unit, string branch) =>
        new SessionBranches(Home).Record(new GrownBranch(repository, "w1", branch, "main", new string('a', 40), null, At) { Unit = unit });

    /// <summary>An attempt held for the person on <paramref name="unit"/>'s node record, as the runtime writes it.</summary>
    private void HeldOn(string unit, string node)
    {
        var call = new WorkPerformCall(unit, "acme.deploy/deploy", 1, "i-" + node, "k-" + node, "r1", [], At.AddMinutes(10));
        new NodeRecordFiles(Home, () => true).Write(new NodeRecord(unit)
        {
            Statuses = new Dictionary<string, NodeStatus> { [node] = new(unit, node, NodeStates.Unknown) { Attempt = 1 } },
            Attempts = [new NodeAttempt(node, 1, "acme.deploy", call, AttemptStates.Held) { RecordedAt = At, Code = ReconcileStates.Unknown }],
        });
    }

    /// <summary>
    /// Two writings of the same JSON: what the page parses. A date written through a node escapes its <c>+</c>, as the wire may write
    /// any string, so the text is not compared.
    /// </summary>
    private static void Same(JsonElement expected, JsonElement actual) =>
        Assert.True(JsonElement.DeepEquals(expected, actual), $"expected {expected.GetRawText()}\nactual   {actual.GetRawText()}");

    /// <summary>
    /// A loop come up over the stand-in service, the facts kept as the module keeps them with the work read from this home, the roster
    /// and git's line stood in; the facts not started, as before the service answers.
    /// </summary>
    private async Task<(DriverLoop Loop, DriverModule Module, StateSlice Slice)> UpAsync()
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        var client = new ServiceClient("http://stand-in", null, new HttpClient(_service));
        HelpFacts.Keep(loop.Facts, new HelpFactJudges(
            async ct => new HelpMachineWhole(await client.SnapshotAsync(ct), [], []),
            _ => Task.FromResult<SessionLook?>(null),
            _ => Task.FromResult<IReadOnlyList<HistoryReading>?>(null),
            async (part, ct) => await HelpFacts.ReadWorkAsync(Home, part, ct))
        {
            Guess = (_, _) => Task.FromResult<string?>("main"),
        });
        await loop.ComeUpAsync(client);
        return (loop, module, loop.Slices.Find(WorkStateSlice.Name)!);
    }

    /// <summary>The facts started, as the loop starts them once the service answers, and every part of the work judged once.</summary>
    private static async Task LandedAsync(DriverLoop loop, params string[] parts)
    {
        loop.Facts.Start();
        await loop.Facts.FirstAsync(HelpFacts.Machine, HelpFacts.Whole, CancellationToken.None).WaitAsync(Within);
        foreach (var part in parts) await loop.Facts.FirstAsync(HelpFacts.Work, part, CancellationToken.None).WaitAsync(Within);
    }

    private IReadOnlyList<SlicePush> Pushes() =>
    [
        .. Raised.Where(each => each.Type == StateSlice.PushType).Select(each => (SlicePush)each.Payload!)
            .Where(push => push.Slice == WorkStateSlice.Name),
    ];

    private IReadOnlyList<SlicePush> Pushed(string partition) => [.. Pushes().Where(push => push.Partition == partition)];

    private static JsonElement Written(object? data) => JsonSerializer.SerializeToElement(data, Page);

    /// <summary>As the desktop's one IPC serializer writes it to the page, the pull's answer and the push alike: a null is left out.</summary>
    private static JsonElement Wire(object? data) => JsonSerializer.SerializeToElement(data, IpcJson.Options);

    /// <summary>A route's answer as the wire writes it.</summary>
    private static async Task<JsonElement> WireAsync(IIpcModule module, string type)
    {
        var response = await AskAsync(module, type);
        Assert.True(response.Success, $"{type} was refused: {response.Error?.Message}");
        return Wire(response.Data);
    }

    private static IEnumerable<string> Units(object? data) =>
        Written(data).GetProperty("units").EnumerateArray().Select(unit => unit.GetProperty("unit").GetString()!);

    /// <summary>Every field <paramref name="element"/> carries, as <c>shape.field</c>, and those of the shapes it holds.</summary>
    private static IEnumerable<string> FieldsOf(JsonElement element, string shape) =>
        element.EnumerateObject().SelectMany(property => FieldOf(property, shape));

    /// <summary>
    /// What <c>WORK_STATE</c> answers, field by field: its <c>held</c> the held partition's, the rest a repository's part's.
    /// </summary>
    private static IEnumerable<string> RouteFields(JsonElement answered) => answered.EnumerateObject()
        .SelectMany(property => FieldOf(property, WorkStateSlice.Fields["held"].Contains(property.Name) ? "held" : "part"));

    private static IEnumerable<string> FieldOf(JsonProperty property, string shape)
    {
        yield return $"{shape}.{property.Name}";
        if (!WorkStateSlice.Holds.TryGetValue($"{shape}.{property.Name}", out var inner)) yield break;
        var values = property.Value.ValueKind == JsonValueKind.Array ? [.. property.Value.EnumerateArray()] : new[] { property.Value };
        foreach (var value in values.Where(value => value.ValueKind == JsonValueKind.Object))
        {
            foreach (var field in FieldsOf(value, inner)) yield return field;
        }
    }

    private static HashSet<string> Classed() =>
    [
        .. WorkStateSlice.Fields.SelectMany(shape => shape.Value.Select(field => $"{shape.Key}.{field}")),
        .. WorkStateSlice.PulledOnly,
    ];

    [Fact]
    public async Task Each_repository_is_a_partition_of_its_part_of_what_WORK_STATE_answers_and_the_held_attempts_one_of_their_own()
    {
        Grown("alpha", "unit-a", "daoris/s-a");
        Grown("beta", "unit-b", "daoris/s-b");
        Grown("gamma", "unit-g", "daoris/s-g");
        HeldOn("quest:q7", "deploy");
        _service.Registry = Registry(("alpha", "w1", true), ("beta", "w2", true), ("gamma", "w2", false));
        var (loop, module, slice) = await UpAsync();
        await LandedAsync(loop, "alpha", "beta", HelpFacts.NoCheckout);

        await slice.JudgeAsync();
        var snapshot = slice.Snapshot();
        var answered = await AnswerAsync(module, "WORK_STATE");
        var wire = await WireAsync(module, "WORK_STATE");

        Assert.Equal((WorkStateSlice.Name, StateSlice.Ready), (snapshot.Slice, snapshot.Status));
        Assert.Equal([WorkStateSlice.Held, HelpFacts.NoCheckout, "alpha", "beta"], snapshot.Partitions.Select(each => each.Partition));
        // Every field the route answers, its nulls written, is pushed or named as the pull's alone, never left to chance.
        Assert.All(RouteFields(answered), field => Assert.Contains(field, Classed()));
        Assert.Contains("attempt.words", RouteFields(answered));
        var parts = snapshot.Partitions.Where(each => each.Partition != WorkStateSlice.Held).ToList();
        foreach (var part in parts)
        {
            Assert.Equal(WorkStateSlice.Fields["part"], Wire(part.Data).EnumerateObject().Select(each => each.Name));
        }

        // The pull's units of each repository and the push's, as the wire writes them: one builder, two doors.
        foreach (var (partition, repository) in new (string, string)[] { ("alpha", "alpha"), ("beta", "beta"), (HelpFacts.NoCheckout, "gamma") })
        {
            var pulled = wire.GetProperty("units").EnumerateArray().Where(unit => unit.GetProperty("repository").GetString() == repository).ToList();
            var pushed = Wire(parts.Single(each => each.Partition == partition).Data).GetProperty("units").EnumerateArray().ToList();
            Assert.Equal(pulled.Count, pushed.Count);
            Assert.NotEmpty(pushed);
            foreach (var (one, other) in pulled.Zip(pushed)) Same(one, other);
        }

        Assert.Equal(
            wire.GetProperty("unread").EnumerateArray().Select(said => said.GetString()).Order(),
            parts.SelectMany(part => Wire(part.Data).GetProperty("unread").EnumerateArray().Select(said => said.GetString())).Order());
        var held = Wire(snapshot.Partitions.Single(each => each.Partition == WorkStateSlice.Held).Data);
        Assert.Equal(WorkStateSlice.Fields["held"], held.EnumerateObject().Select(each => each.Name));
        Same(wire.GetProperty("held"), held.GetProperty("held"));
        Assert.Equal("quest:q7", held.GetProperty("held")[0].GetProperty("unit").GetString());
    }

    /// <summary>
    /// The allow-list held against the route's own builders: a unit with every shape filled and a held attempt with every field set,
    /// each field the builders write classed, and the partition carrying each as <c>WORK_STATE</c> writes it.
    /// </summary>
    [Fact]
    public void Every_field_of_a_unit_an_entry_and_a_held_attempt_is_classed_and_pushed_as_the_route_s_builders_write_it()
    {
        static InventoryEntry Entry(string unit, string kind, string role, string disposition) =>
            new(unit, kind, "daoris/s-a", role, disposition, "the driver's clause")
            {
                Until = At.AddDays(7), Repository = "alpha", Commit = "a1b2c3d4", Proof = "every commit of it is on `main`",
                // OVERVIEWNOISE1b: a blocked entry's code and the facts the page words it with.
                Code = InventoryCode.Kept, Facts = new Dictionary<string, string> { ["why"] = "nested", ["at"] = "2026-10-20T09:00:00.0000000+00:00" },
            };

        InventoryEntry[] entries =
        [
            Entry("unit-a", InventoryKind.Line, InventoryRole.Result, InventoryDisposition.Kept),
            Entry("unit-a", InventoryKind.Tree, InventoryRole.Temporary, InventoryDisposition.Pending),
            Entry("unit-a", InventoryKind.SessionBranch, InventoryRole.Temporary, InventoryDisposition.Removed),
            Entry("unit-a", InventoryKind.RecoveryRef, InventoryRole.Recovery, InventoryDisposition.Retained),
        ];
        var unit = new WorkMilestone(
            "unit-a", "alpha", "daoris/s-a", "s-a", new MilestoneEnd("ended", "merged", "main", "a1b2c3d4e5f6", At, "every commit of it is on `main`"),
            [entries[0]], new MilestoneCleanup("pending", [entries[1]], [entries[2]]), [entries[3]], entries);
        var attempt = new HeldAttempt("unit-a", "deploy", 1, "acme.deploy", "acme.deploy/deploy", NodeStates.Unknown, ReconcileStates.Unknown)
        {
            Outcome = "running", Revision = "a1b2c3d4", RecordedAt = At, Resolution = HeldResolutions.DidNot, ResolvedAt = At,
            Words = HeldResolutions.Plugin,
        };

        var judged = WorkStateSlice.Judge([("alpha", new HelpWork([unit], [new InventoryUnread("alpha", "git could not list it")], [entries[3]]))], [attempt], partly: false)!;
        var part = Written(judged.Single(each => each.Partition == "alpha").Data);
        var held = Written(judged.Single(each => each.Partition == WorkStateSlice.Held).Data);

        var built = Written(LinesRoutes.WorkUnit(unit));
        var heldBuilt = Written(LinesRoutes.HeldAnswer([attempt]));
        Assert.All(FieldsOf(built, "unit").Concat(FieldsOf(heldBuilt[0], "attempt")), field => Assert.Contains(field, Classed()));
        Same(built, part.GetProperty("units")[0]);
        Same(Written(LinesRoutes.WorkEntry(entries[3])), part.GetProperty("recovery")[0]);
        Assert.Equal("git could not list it", part.GetProperty("unread")[0].GetString());
        Same(heldBuilt, held.GetProperty("held"));
    }

    [Fact]
    public async Task A_hostile_field_in_the_records_or_the_builders_never_reaches_a_partition()
    {
        Grown("alpha", "unit-a", "daoris/s-a");
        var file = DaorisHome.Entry(Home, HomeEntry.SessionBranches);
        File.WriteAllText(file, File.ReadAllText(file).Replace("\"repository\":", "\"token\":\"sk-hostile-record\",\"repository\":"));
        Assert.Contains("sk-hostile-record", File.ReadAllText(file));
        _service.Registry = $$"""
            [{"repository":"alpha","workspace":"w1","adopted":true,"root":"{{Root("alpha")}}","token":"sk-hostile-registry"}]
            """;
        var (loop, _, slice) = await UpAsync();
        await LandedAsync(loop, "alpha", HelpFacts.NoCheckout);

        await slice.JudgeAsync();

        var pushed = JsonSerializer.Serialize(Pushes().Select(push => push.Data), IpcJson.Options);
        Assert.Contains("unit-a", pushed);
        Assert.DoesNotContain("sk-hostile", pushed);
        // A field a builder comes to write is pushed only once the allow-list names it.
        var allowed = WorkStateSlice.Allowed(new { Units = Array.Empty<object>(), Recovery = Array.Empty<object>(), Unread = Array.Empty<string>(), Token = "sk-hostile" }, "part");
        Assert.Equal(["units", "recovery", "unread"], allowed.Select(each => each.Key));
    }

    [Fact]
    public async Task A_second_window_s_snapshot_is_read_from_memory_and_judges_nothing()
    {
        Grown("alpha", "unit-a", "daoris/s-a");
        _service.Registry = Registry(("alpha", "w1", true));
        var (loop, module, slice) = await UpAsync();
        await LandedAsync(loop, "alpha", HelpFacts.NoCheckout);
        await slice.JudgeAsync();

        var first = await AnswerAsync(module, "STATE_SLICE", new { slice = WorkStateSlice.Name });
        var second = await AnswerAsync(module, "STATE_SLICE", new { slice = WorkStateSlice.Name });

        Assert.Equal(1, slice.Judgements);
        Assert.Equal(first.GetRawText(), second.GetRawText());
        Assert.Equal(StateSlice.Ready, first.GetProperty("status").GetString());
        Assert.Equal([WorkStateSlice.Held, HelpFacts.NoCheckout, "alpha"],
            first.GetProperty("partitions").EnumerateArray().Select(each => each.GetProperty("partition").GetString()));
    }

    /// <summary>
    /// The judgement reads the facts and the node records, never the checkouts: where the pull walks git in every repository, the push
    /// starts nothing, counted at the process seam.
    /// </summary>
    [Fact]
    public async Task A_judgement_starts_no_git_and_no_process()
    {
        Grown("alpha", "unit-a", "daoris/s-a");
        HeldOn("quest:q7", "deploy");
        _service.Registry = Registry(("alpha", "w1", true), ("beta", "w2", true));
        var (loop, _, slice) = await UpAsync();
        await LandedAsync(loop, "alpha", "beta", HelpFacts.NoCheckout);
        var starts = 0;

        using (ProcessStarts.Within(_ =>
               {
                   Interlocked.Increment(ref starts);
                   return null;
               }))
        {
            await slice.JudgeAsync();
        }

        Assert.Equal(0, starts);
        Assert.Equal(["unit-a"], Units(slice.Snapshot().Partitions.Single(each => each.Partition == "alpha").Data));
    }

    /// <summary>
    /// 🔴 The freshness (PUSH1c2a): what a landing writes moves no ref the watch hears here, and a sweep's tree-only removal may move
    /// none, while the parts were judged a moment ago, inside the rest a burst of refs is held to. The press's declared scope judges every
    /// part afresh, so each reaches the slice at once, never at the part's five-minute deadline, and pushes the partition it moved alone.
    /// </summary>
    [Fact]
    public async Task A_landing_and_a_sweep_a_press_declared_reach_the_slice_at_once_each_pushing_the_partition_it_moved()
    {
        Grown("alpha", "unit-a", "daoris/s-a");
        Grown("beta", "unit-b", "daoris/s-b");
        _service.Registry = Registry(("alpha", "w1", true), ("beta", "w1", true));
        var (loop, _, slice) = await UpAsync();
        using var stop = new CancellationTokenSource();
        var running = slice.RunAsync(stop.Token);
        await LandedAsync(loop, "alpha", "beta", HelpFacts.NoCheckout);
        await UntilAsync(() => Pushed("alpha").Count == 1 && Pushed("beta").Count == 1);

        // What a landing writes: the landing branch that carries unit-a's work.
        new LandedBranches(Home).Record(new LandedBranch("alpha", "w1", "daoris/land-a", "main", new string('b', 40), "s-a", null, null, DateTimeOffset.UtcNow)
        {
            Units = ["unit-a"],
        });
        StatePublisher.Publish(Bus, StateScopes.Landing);
        await UntilAsync(() => Pushed("alpha").Count == 2);
        Assert.Equal("handed", Written(Pushed("alpha")[^1].Data).GetProperty("units")[0].GetProperty("end").GetProperty("state").GetString());

        // What a sweep removes: beta's session branch, whose unit then names nothing.
        new SessionBranches(Home).Forget("beta", ["daoris/s-b"]);
        StatePublisher.Publish(Bus, StateScopes.Sweep);
        await UntilAsync(() => Pushed("beta").Count == 2);
        Assert.Empty(Units(Pushed("beta")[^1].Data));

        await slice.JudgeAsync();
        Assert.Equal((2, 2, 1), (Pushed("alpha").Count, Pushed("beta").Count, Pushed(HelpFacts.NoCheckout).Count));
        await stop.CancelAsync();
        await running;
    }

    /// <summary>
    /// A held attempt worded from a window declares the sessions, which wakes the slice; one the runtime holds says nothing, and is
    /// pushed at the slice's next judgement, its period's at the latest: the held partition is read from the node records at each.
    /// </summary>
    [Fact]
    public async Task A_held_attempt_reaches_its_partition_at_a_sessions_press_and_at_the_next_judgement_with_none()
    {
        _service.Registry = Registry(("alpha", "w1", true));
        var (loop, _, slice) = await UpAsync();
        using var stop = new CancellationTokenSource();
        var running = slice.RunAsync(stop.Token);
        await LandedAsync(loop, "alpha", HelpFacts.NoCheckout);
        await UntilAsync(() => Pushed(WorkStateSlice.Held).Count == 1);
        Assert.Equal(0, Written(Pushed(WorkStateSlice.Held)[0].Data).GetProperty("held").GetArrayLength());

        HeldOn("quest:q7", "deploy");
        StatePublisher.Publish(Bus, StateScopes.Sessions);
        await UntilAsync(() => Pushed(WorkStateSlice.Held).Count == 2);
        await stop.CancelAsync();
        await running;

        HeldOn("quest:q8", "deploy");
        await slice.JudgeAsync();
        Assert.Equal(["quest:q7", "quest:q8"], Written(Pushed(WorkStateSlice.Held)[^1].Data).GetProperty("held").EnumerateArray()
            .Select(each => each.GetProperty("unit").GetString()).Order());
    }

    /// <summary>
    /// Until every part the service's answers name was judged once, the slice answers not judged and pushes nothing: a window keeps
    /// the pull, where units read from the parts judged so far would say there is nothing to clean up.
    /// </summary>
    [Fact]
    public async Task Until_every_part_was_judged_once_the_slice_answers_not_judged_and_pushes_nothing()
    {
        using var stand = new Stand(this, "alpha", "beta");
        var alpha = stand.Hold("alpha");
        stand.Facts.Start();
        await stand.Facts.FirstAsync(HelpFacts.Work, "beta", CancellationToken.None).WaitAsync(Within);
        await stand.Facts.FirstAsync(HelpFacts.Work, HelpFacts.NoCheckout, CancellationToken.None).WaitAsync(Within);

        await stand.Slice.JudgeAsync();
        Assert.Equal(StateSlice.NotJudged, stand.Slice.Snapshot().Status);
        Assert.Empty(Pushes());

        alpha.SetResult(stand.Work("alpha"));
        await stand.Facts.FirstAsync(HelpFacts.Work, "alpha", CancellationToken.None).WaitAsync(Within);
        await stand.Slice.JudgeAsync();
        Assert.Equal(StateSlice.Ready, stand.Slice.Snapshot().Status);
        Assert.Equal([WorkStateSlice.Held, HelpFacts.NoCheckout, "alpha", "beta"], stand.Slice.Snapshot().Partitions.Select(each => each.Partition));
    }

    /// <summary>
    /// Once judged whole, a part never judged (a repository registered since) is left out until it is, never pushed empty, which a window
    /// would read as nothing to clean up there; the rest stand and move as before.
    /// </summary>
    [Fact]
    public async Task A_part_registered_after_the_first_judgement_is_left_out_until_judged_never_pushed_empty()
    {
        using var stand = new Stand(this, "alpha");
        stand.Facts.Start();
        await stand.FirstAsync("alpha", HelpFacts.NoCheckout);
        await stand.Slice.JudgeAsync();

        var gamma = stand.Hold("gamma");
        stand.Register("alpha", "gamma");
        await stand.Facts.JudgedAsync(HelpFacts.Machine, HelpFacts.Whole, CancellationToken.None).WaitAsync(Within);
        await stand.Slice.JudgeAsync();
        Assert.Equal([WorkStateSlice.Held, HelpFacts.NoCheckout, "alpha"], stand.Slice.Snapshot().Partitions.Select(each => each.Partition));
        Assert.Empty(Pushed("gamma"));

        gamma.SetResult(stand.Work("gamma"));
        await stand.Facts.FirstAsync(HelpFacts.Work, "gamma", CancellationToken.None).WaitAsync(Within);
        await stand.Slice.JudgeAsync();
        Assert.Equal(["unit-gamma"], Units(Assert.Single(Pushed("gamma")).Data));
    }

    [Fact]
    public async Task A_ref_moved_in_one_repository_pushes_that_partition_alone()
    {
        using var stand = new Stand(this, "alpha", "beta");
        using var stop = new CancellationTokenSource();
        var running = stand.Slice.RunAsync(stop.Token);
        stand.Facts.Start();
        await stand.FirstAsync("alpha", "beta", HelpFacts.NoCheckout);
        await UntilAsync(() => Pushed("alpha").Count == 1 && Pushed("beta").Count == 1);

        stand.Moved("alpha", "unit-alpha-2");
        // Past the rest a burst of one repository's refs is held to, then what the ref watch tells the facts (DriverWatch's Heard).
        stand.Clock.Pass(HelpFacts.WorkRules.Rest + TimeSpan.FromSeconds(1));
        HelpFacts.Wake(stand.Facts, ChangeTopics.Refs, "alpha");
        await UntilAsync(() => Pushed("alpha").Count == 2);
        await stand.Slice.JudgeAsync();

        Assert.Equal(["unit-alpha-2"], Units(Pushed("alpha")[^1].Data));
        Assert.Equal((1, 1, 1), (Pushed("beta").Count, Pushed(HelpFacts.NoCheckout).Count, Pushed(WorkStateSlice.Held).Count));
        await stop.CancelAsync();
        await running;
    }

    /// <summary>
    /// What each declared scope asks of the parts: a landing's or a sweep's judges every part afresh, past their rest; the sessions',
    /// told at every record's move, wakes them under it, so a burst of moves is one judgement each rest.
    /// </summary>
    [Fact]
    public async Task A_sessions_press_wakes_every_part_under_its_rest_and_a_landing_or_a_sweep_judges_them_afresh()
    {
        using var stand = new Stand(this, "alpha", "beta");
        using var hearing = WorkStateSlice.HearPresses(Bus, stand.Facts);
        stand.Facts.Start();
        await stand.FirstAsync("alpha", "beta", HelpFacts.NoCheckout);
        await UntilAsync(() => stand.Judged() == (1, 1, 1));

        StatePublisher.Publish(Bus, StateScopes.Sessions);
        await Task.Delay(300);
        Assert.Equal((1, 1, 1), stand.Judged());

        stand.Clock.Pass(HelpFacts.WorkRules.Rest + TimeSpan.FromSeconds(1));
        StatePublisher.Publish(Bus, StateScopes.Sessions);
        await UntilAsync(() => stand.Judged() == (2, 2, 2));

        StatePublisher.Publish(Bus, StateScopes.Landing);
        await UntilAsync(() => stand.Judged() == (3, 3, 3));
        StatePublisher.Publish(Bus, StateScopes.Sweep);
        await UntilAsync(() => stand.Judged() == (4, 4, 4));
        StatePublisher.Publish(Bus, StateScopes.Driver);
        await Task.Delay(300);
        Assert.Equal((4, 4, 4), stand.Judged());
    }

    /// <summary>The facts' other topics land often (the lines, the sessions, the history) and move nothing here, so they wake nothing.</summary>
    [Fact]
    public async Task The_slice_hears_the_service_s_answers_and_the_work_land_and_no_other_fact()
    {
        using var facts = new MachineFacts();
        var whole = new HelpMachineWhole(new Snapshot([], [new RepoView("alpha", Adopted: true, Root: "X:/daoris/checkouts/alpha")], []), [], []);
        // Each judgement a fresh value, as the service's answers and the parts are read afresh.
        facts.Topic(HelpFacts.Machine, HelpFacts.MachineRules, (_, _) => Task.FromResult<object?>(whole with { }));
        facts.Topic(HelpFacts.Lines, HelpFacts.LinesRules, (_, _) => Task.FromResult<object?>(new HelpLineGuess("main")));
        facts.Topic(HelpFacts.Sessions, HelpFacts.WholeRules, (_, _) => Task.FromResult<object?>(new SessionLook([], [], [])));
        facts.Topic(HelpFacts.Work, HelpFacts.WorkRules, (_, _) => Task.FromResult<object?>(new HelpWork([], [], [])));
        var wakes = 0;
        var hearing = WorkStateSlice.HearFacts(facts, () => Interlocked.Increment(ref wakes));
        facts.Start();

        await facts.JudgedAsync(HelpFacts.Machine, HelpFacts.Whole, CancellationToken.None).WaitAsync(Within);
        Assert.Equal(1, Volatile.Read(ref wakes));
        await facts.JudgedAsync(HelpFacts.Sessions, HelpFacts.Whole, CancellationToken.None).WaitAsync(Within);
        await facts.JudgedAsync(HelpFacts.Lines, "alpha", CancellationToken.None).WaitAsync(Within);
        Assert.Equal(1, Volatile.Read(ref wakes));
        await facts.JudgedAsync(HelpFacts.Work, "alpha", CancellationToken.None).WaitAsync(Within);
        Assert.Equal(2, Volatile.Read(ref wakes));

        hearing.Dispose();
        await facts.JudgedAsync(HelpFacts.Work, "alpha", CancellationToken.None).WaitAsync(Within);
        Assert.Equal(2, Volatile.Read(ref wakes));
    }

    [Theory]
    [InlineData("STATE_CHANGED", """{"scopes":["landing"]}""", true)]
    [InlineData("STATE_CHANGED", """{"scopes":["sweep"]}""", true)]
    [InlineData("STATE_CHANGED", """{"scopes":["sessions"],"sessions":["s1"]}""", true)]
    [InlineData("STATE_CHANGED", """{"scopes":["driver"]}""", false)]
    [InlineData("STATE_CHANGED", """{"scopes":["lines"]}""", false)]
    // The registry's scope judges the service's answers again (the lines slice's owner asks it), whose landing wakes the slice.
    [InlineData("STATE_CHANGED", """{"scopes":["registry"]}""", false)]
    [InlineData("SESSION_ENDED", "{}", false)]
    [InlineData("DRIVER_READY", "{}", false)]
    public void What_wakes_the_work_slice(string type, string payload, bool wakes)
    {
        var wake = WorkStateSlice.WakesOn.FirstOrDefault(each => each.Type == type);
        object told = type == StatePublisher.Type ? JsonSerializer.Deserialize<StateChanged>(payload, IpcJson.Options)! : new { };

        Assert.Equal(wakes, wake is not null && (wake.When is null || wake.When(told)));
    }

    /// <summary>
    /// The facts on a clock the test moves, each part's work as the test says it (one unit named for its repository, unless moved or held),
    /// and the slice over them with no attempt held.
    /// </summary>
    private sealed class Stand : IDisposable
    {
        private readonly Dictionary<string, HelpWork> _work = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, TaskCompletionSource<HelpWork?>> _holding = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _judged = new(StringComparer.OrdinalIgnoreCase);
        private HelpMachineWhole _whole = null!;

        public Stand(WorkStateSliceTests test, params string[] repositories)
        {
            Register(repositories);
            Facts = new MachineFacts(Clock.Now);
            HelpFacts.Keep(Facts, new HelpFactJudges(
                _ => Task.FromResult<HelpMachineWhole?>(Volatile.Read(ref _whole)),
                _ => Task.FromResult<SessionLook?>(null),
                _ => Task.FromResult<IReadOnlyList<HistoryReading>?>(null),
                (part, _) =>
                {
                    var subject = part.Repository ?? HelpFacts.NoCheckout;
                    lock (_work)
                    {
                        _judged[subject] = _judged.GetValueOrDefault(subject) + 1;
                        return _holding.Remove(subject, out var held) ? held.Task : Task.FromResult<HelpWork?>(Work(subject));
                    }
                })
            {
                Guess = (_, _) => Task.FromResult<string?>("main"),
            });
            Slice = WorkStateSlice.Of(Facts, test.Bus, () => []);
        }

        public Clock Clock { get; } = new();

        public MachineFacts Facts { get; }

        public StateSlice Slice { get; }

        /// <summary>The service's answers naming these repositories, each with a checkout here.</summary>
        public void Register(params string[] repositories) => Volatile.Write(ref _whole, new HelpMachineWhole(
            new Snapshot([], [.. repositories.Select(repository => new RepoView(repository, Adopted: true, Root: $"X:/daoris/checkouts/{repository}"))], []), [], []));

        public HelpWork Work(string subject)
        {
            lock (_work)
            {
                return _work.GetValueOrDefault(subject) ?? new HelpWork(subject == HelpFacts.NoCheckout ? [] : [Milestone("unit-" + subject, subject)], [], []);
            }
        }

        /// <summary>The part's next judgement waits for the test's answer.</summary>
        public TaskCompletionSource<HelpWork?> Hold(string subject)
        {
            var held = new TaskCompletionSource<HelpWork?>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_work) _holding[subject] = held;
            return held;
        }

        public void Moved(string subject, string unit)
        {
            lock (_work) _work[subject] = new HelpWork([Milestone(unit, subject)], [], []);
        }

        public (int Alpha, int Beta, int NoCheckout) Judged()
        {
            lock (_work) return (_judged.GetValueOrDefault("alpha"), _judged.GetValueOrDefault("beta"), _judged.GetValueOrDefault(HelpFacts.NoCheckout));
        }

        public async Task FirstAsync(params string[] parts)
        {
            await Facts.FirstAsync(HelpFacts.Machine, HelpFacts.Whole, CancellationToken.None).WaitAsync(Within);
            foreach (var part in parts) await Facts.FirstAsync(HelpFacts.Work, part, CancellationToken.None).WaitAsync(Within);
        }

        public void Dispose() => Facts.Dispose();

        private static WorkMilestone Milestone(string unit, string repository) => new(
            unit, repository, $"daoris/s-{unit}", $"s-{unit}", new MilestoneEnd("not-ended", null, null, null, null, "nothing was handed on"),
            [], new MilestoneCleanup("pending", [], []), [], []);
    }

    /// <summary>The facts' time, moved on by the test alone, so a rest is passed without waiting it out.</summary>
    private sealed class Clock
    {
        private long _ticks = At.UtcTicks;

        public DateTimeOffset Now() => new(Interlocked.Read(ref _ticks), TimeSpan.Zero);

        public void Pass(TimeSpan span) => Interlocked.Add(ref _ticks, span.Ticks);
    }

    /// <summary>The service's doors the facts and the pull read, standing in: the registry as a test sets it, nothing else.</summary>
    private sealed class StandIn : HttpMessageHandler
    {
        private string _registry = "[]";

        public string Registry
        {
            get => Volatile.Read(ref _registry);
            set => Volatile.Write(ref _registry, value);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(request.RequestUri!.AbsolutePath == "/api/registry" ? Registry : "[]", Encoding.UTF8, "application/json"),
            });
    }
}
