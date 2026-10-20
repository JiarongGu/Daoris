using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The clean-up's list pushed (PUSH1c4a, D182 points 3-4): one owner in the shell's loop judging, from the machine's facts' parts of the
/// list (each repository's git half by the pull-request answers the landing record keeps) and the sessions' look, a partition per
/// repository with a checkout here carrying what <c>SWEEP_PLAN</c> answers of it, pushed when one moves, so no window's tick reads the
/// list and none asks a plugin.
/// </summary>
/// <remarks>
/// The facts are kept on stand-ins (the list's git half as the test says it), so no git and no plugin; where a route answers beside the
/// slice, its checkouts are folders that do not exist, which the list passes over.
/// </remarks>
public sealed partial class SweepStateSliceTests : DriverModuleBridge
{
    private static readonly TimeSpan Within = Poll.Patience;

    private static readonly DateTimeOffset At = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    /// <summary>What the page is handed, as <see cref="Bridge"/>'s answers are read: camelCase, nulls written.</summary>
    private static readonly JsonSerializerOptions Page = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static string Root(string repository) => $"X:/daoris/checkouts/{repository}";

    private static string Tree(string repository) => $"X:/daoris/trees/w1/{repository}/s-{repository}";

    /// <summary>
    /// A repository's list: a kept session branch whose tree is here, holding a commit no branch of the person's holds, and a landing's
    /// branch whose pull request is open.
    /// </summary>
    private static SweepPlan Plan(string repository, int commits = 1) => new(
        [new SweepItem(repository, "w1", $"daoris/s-{repository}", Tree(repository), SweepKind.Unlanded, commits, null, "a1b2c3d the work")],
        [new LandedItem(repository, "w1", $"feature/q1-{repository}", LandedKind.Differs, null, ["a.txt"], null, "https://example.test/pr/7", 2)]);

    /// <summary>The sessions' look: each repository's session that ended in its tree, failed.</summary>
    private static SessionLook Look(params string[] repositories) => new(
        [.. repositories.Select(repository => new SessionRecord($"s-{repository}", repository, "failed") { Tree = Tree(repository), Created = At })], [], []);

    private static JsonElement Written(object? data) => JsonSerializer.SerializeToElement(data, Page);

    /// <summary>As the desktop's one IPC serializer writes it to the page, the pull's answer and the push alike: a null is left out.</summary>
    private static JsonElement Wire(object? data) => JsonSerializer.SerializeToElement(data, IpcJson.Options);

    private static void Same(JsonElement expected, JsonElement actual) =>
        Assert.True(JsonElement.DeepEquals(expected, actual), $"expected {expected.GetRawText()}\nactual   {actual.GetRawText()}");

    private IReadOnlyList<SlicePush> Pushed(string partition) =>
    [
        .. Raised.Where(each => each.Type == StateSlice.PushType).Select(each => (SlicePush)each.Payload!)
            .Where(push => push.Slice == SweepStateSlice.Name && push.Partition == partition),
    ];

    private IReadOnlyList<SlicePush> Pushes() =>
    [
        .. Raised.Where(each => each.Type == StateSlice.PushType).Select(each => (SlicePush)each.Payload!)
            .Where(push => push.Slice == SweepStateSlice.Name),
    ];

    [Fact]
    public void Each_partition_is_what_SWEEP_PLAN_writes_of_its_repository_through_the_one_builder()
    {
        var (engine, site) = (Plan("engine"), Plan("site", commits: 0));
        var look = Look("engine", "site");

        var judged = SweepStateSlice.Judge([("engine", engine), ("site", site)], look, partly: false)!;

        Assert.Equal(["engine", "site"], judged.Select(each => each.Partition));
        Same(Wire(LinesRoutes.SweepAnswer(engine, look.Records)), Wire(judged[0].Data));
        Same(Wire(LinesRoutes.SweepAnswer(site, look.Records)), Wire(judged[1].Data));
        // The session is read from the facts' look, and its landing offered by the driver's rule, as the door says it.
        var row = Wire(judged[0].Data).GetProperty("branches")[0];
        Assert.Equal(("s-engine", "failed", true), (row.GetProperty("session").GetProperty("id").GetString(),
            row.GetProperty("session").GetProperty("state").GetString(), row.GetProperty("landable").GetBoolean()));
        Assert.False(Wire(judged[1].Data).GetProperty("branches")[0].GetProperty("landable").GetBoolean());
        Assert.Equal("feature/q1-engine", Wire(judged[0].Data).GetProperty("landed")[0].GetProperty("branch").GetString());
    }

    /// <summary>
    /// The twin (D182 point 3): every field <c>SWEEP_PLAN</c>'s builders write, each shape filled, is pushed or named the pull's alone, and
    /// every field the page's <c>SweepBranch</c> and <c>LandedBranch</c> read (<c>settings/Sweep.tsx</c>) is pushed.
    /// </summary>
    [Fact]
    public void Every_field_the_list_writes_is_classed_and_every_field_the_page_reads_is_pushed()
    {
        var item = new SweepItem("engine", "w1", "daoris/s-1", Tree("engine"), SweepKind.Unlanded, 1, "main", "a1b2c3d the work");
        var landed = new LandedItem("engine", "w1", "feature/x", LandedKind.Differs, "origin/main", ["a.txt"], "git said", "https://example.test/pr/7", 2);
        var session = new SessionRecord("s-1", "engine", "failed") { Tree = Tree("engine") };

        var route = FieldsOf(Written(LinesRoutes.SweepRow(item, session)), "branch")
            .Concat(FieldsOf(Written(LinesRoutes.LandedRow(landed)), "landed"))
            .Concat(FieldsOf(Written(LinesRoutes.SweepAnswer(new SweepPlan([item], [landed]), [session])), "part"))
            .ToList();
        var pushed = SweepStateSlice.Fields.SelectMany(shape => shape.Value.Select(field => $"{shape.Key}.{field}")).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("session.state", route);
        Assert.Contains("landed.pullRequest", route);
        Assert.All(route, field => Assert.True(pushed.Contains(field) || SweepStateSlice.PulledOnly.Contains(field), $"`{field}` is neither pushed nor the pull's"));
        Assert.Equal(pushed.Order(StringComparer.Ordinal), route.Distinct().Where(pushed.Contains).Order(StringComparer.Ordinal));

        var page = PageFields();
        Assert.Contains("session.id", page);
        Assert.All(page, field => Assert.True(pushed.Contains(field), $"the page reads `{field}`, which is not pushed"));
    }

    [Fact]
    public void A_hostile_field_a_builder_comes_to_write_is_never_pushed_until_named()
    {
        var allowed = SweepStateSlice.Allowed(new { Branches = Array.Empty<object>(), Landed = Array.Empty<object>(), Token = "sk-hostile" }, "part");

        Assert.Equal(["branches", "landed"], allowed.Select(each => each.Key));
        var row = SweepStateSlice.Allowed(new { Branches = new[] { new { Branch = "daoris/s-1", Root = "X:/daoris/checkouts/engine" } }, Landed = Array.Empty<object>() }, "part");
        Assert.DoesNotContain("X:/daoris", row.ToJsonString());
    }

    /// <summary>
    /// The registry: the slice found by its name, judged from the facts, a second window's snapshot read from memory; a hostile field in
    /// the service's answers never reaches a partition, and a judgement starts no process.
    /// </summary>
    [Fact]
    public async Task A_window_s_snapshot_is_read_from_memory_judges_nothing_and_a_judgement_starts_no_process()
    {
        var service = new StandIn
        {
            Registry = $$"""
                [{"repository":"engine","workspace":"w1","adopted":true,"root":"{{Root("engine")}}","token":"sk-hostile-registry"}]
                """,
        };
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        var client = new ServiceClient("http://stand-in", null, new HttpClient(service));
        HelpFacts.Keep(loop.Facts, new HelpFactJudges(
            async ct => new HelpMachineWhole(await client.SnapshotAsync(ct), [], []),
            _ => Task.FromResult<SessionLook?>(Look("engine")),
            _ => Task.FromResult<IReadOnlyList<HistoryReading>?>(null),
            (_, _) => Task.FromResult<HelpWork?>(null))
        {
            Guess = (_, _) => Task.FromResult<string?>("main"),
            Sweep = (part, _) => Task.FromResult<SweepPlan?>(Plan(part.Repository)),
        });
        await loop.ComeUpAsync(client);
        var slice = loop.Slices.Find(SweepStateSlice.Name)!;
        loop.Facts.Start();
        await loop.Facts.FirstAsync(HelpFacts.Sweep, "engine", CancellationToken.None).WaitAsync(Within);
        await loop.Facts.FirstAsync(HelpFacts.Sessions, HelpFacts.Whole, CancellationToken.None).WaitAsync(Within);
        var starts = 0;

        using (ProcessStarts.Within(_ =>
               {
                   Interlocked.Increment(ref starts);
                   return null;
               }))
        {
            await slice.JudgeAsync();
        }

        var first = await AnswerAsync(module, "STATE_SLICE", new { slice = SweepStateSlice.Name });
        var second = await AnswerAsync(module, "STATE_SLICE", new { slice = SweepStateSlice.Name });

        Assert.Equal(0, starts);
        Assert.Equal(1, slice.Judgements);
        Assert.Equal(first.GetRawText(), second.GetRawText());
        Assert.Equal(StateSlice.Ready, first.GetProperty("status").GetString());
        Assert.Equal(["engine"], first.GetProperty("partitions").EnumerateArray().Select(each => each.GetProperty("partition").GetString()));
        Assert.DoesNotContain("sk-hostile", first.GetRawText());
        Assert.DoesNotContain("sk-hostile", JsonSerializer.Serialize(Pushes().Select(push => push.Data), IpcJson.Options));
    }

    /// <summary>
    /// Until every part the service's answers name and the sessions' look were judged once, the slice answers not judged and pushes
    /// nothing: a window keeps the pull, where a list read from the parts judged so far would say there is nothing to clean up.
    /// </summary>
    [Fact]
    public async Task Until_every_part_and_the_look_were_judged_once_the_slice_answers_not_judged()
    {
        using var stand = new Stand(this, "alpha", "beta");
        var alpha = stand.Hold("alpha");
        stand.Facts.Start();
        await stand.Facts.FirstAsync(HelpFacts.Sweep, "beta", CancellationToken.None).WaitAsync(Within);
        await stand.Facts.FirstAsync(HelpFacts.Sessions, HelpFacts.Whole, CancellationToken.None).WaitAsync(Within);

        await stand.Slice.JudgeAsync();
        Assert.Equal(StateSlice.NotJudged, stand.Slice.Snapshot().Status);
        Assert.Empty(Pushes());

        alpha.SetResult(Plan("alpha"));
        await stand.Facts.FirstAsync(HelpFacts.Sweep, "alpha", CancellationToken.None).WaitAsync(Within);
        await stand.Slice.JudgeAsync();
        Assert.Equal(StateSlice.Ready, stand.Slice.Snapshot().Status);
        Assert.Equal(["alpha", "beta"], stand.Slice.Snapshot().Partitions.Select(each => each.Partition));
        // And no judgement before the look: a row's session and its landing are read from it.
        Assert.Null(SweepStateSlice.Judge([("alpha", Plan("alpha"))], look: null, partly: false));
    }

    /// <summary>Once judged whole, a repository registered since is left out until its part is judged, never pushed empty.</summary>
    [Fact]
    public async Task A_repository_registered_after_the_first_judgement_is_left_out_until_judged()
    {
        using var stand = new Stand(this, "alpha");
        stand.Facts.Start();
        await stand.FirstAsync("alpha");
        await stand.Slice.JudgeAsync();

        var gamma = stand.Hold("gamma");
        stand.Register("alpha", "gamma");
        await stand.Facts.JudgedAsync(HelpFacts.Machine, HelpFacts.Whole, CancellationToken.None).WaitAsync(Within);
        await stand.Slice.JudgeAsync();
        Assert.Equal(["alpha"], stand.Slice.Snapshot().Partitions.Select(each => each.Partition));
        Assert.Empty(Pushed("gamma"));

        gamma.SetResult(Plan("gamma"));
        await stand.Facts.FirstAsync(HelpFacts.Sweep, "gamma", CancellationToken.None).WaitAsync(Within);
        await stand.Slice.JudgeAsync();
        Assert.Single(Pushed("gamma"));
    }

    [Fact]
    public async Task A_ref_moved_in_one_repository_pushes_that_partition_alone()
    {
        using var stand = new Stand(this, "alpha", "beta");
        using var stop = new CancellationTokenSource();
        var running = stand.Slice.RunAsync(stop.Token);
        stand.Facts.Start();
        await stand.FirstAsync("alpha", "beta");
        await UntilAsync(() => Pushed("alpha").Count == 1 && Pushed("beta").Count == 1);

        stand.Moved("alpha", Plan("alpha", commits: 3));
        // Past the rest a burst of one repository's refs is held to, then what the ref watch tells the facts.
        stand.Clock.Pass(HelpFacts.SweepRules.Rest + TimeSpan.FromSeconds(1));
        HelpFacts.Wake(stand.Facts, ChangeTopics.Refs, "alpha");
        await UntilAsync(() => Pushed("alpha").Count == 2);
        await stand.Slice.JudgeAsync();

        Assert.Equal(3, Written(Pushed("alpha")[^1].Data).GetProperty("branches")[0].GetProperty("commits").GetInt32());
        Assert.Single(Pushed("beta"));
        await stop.CancelAsync();
        await running;
    }

    /// <summary>
    /// What each declared scope asks of the parts (as the work's, PUSH1c2a): a landing's or a sweep's judges every part afresh, past their
    /// rest, since a discard, a clean-up and a recovery's drop move refs the watch may not hear as Daoris's own; the sessions', told at
    /// every record's move, wakes them under it, so a burst of moves is one judgement each rest; another scope none.
    /// </summary>
    [Fact]
    public async Task A_sessions_press_wakes_every_part_under_its_rest_and_a_landing_or_a_sweep_judges_them_afresh()
    {
        using var stand = new Stand(this, "alpha", "beta");
        using var hearing = SweepStateSlice.HearPresses(Bus, stand.Facts);
        stand.Facts.Start();
        await stand.FirstAsync("alpha", "beta");
        await UntilAsync(() => stand.Judged() == (1, 1));

        StatePublisher.Publish(Bus, StateScopes.Sessions);
        await Task.Delay(300);
        Assert.Equal((1, 1), stand.Judged());

        stand.Clock.Pass(HelpFacts.SweepRules.Rest + TimeSpan.FromSeconds(1));
        StatePublisher.Publish(Bus, StateScopes.Sessions);
        await UntilAsync(() => stand.Judged() == (2, 2));

        StatePublisher.Publish(Bus, StateScopes.Landing);
        await UntilAsync(() => stand.Judged() == (3, 3));
        StatePublisher.Publish(Bus, StateScopes.Sweep);
        await UntilAsync(() => stand.Judged() == (4, 4));
        StatePublisher.Publish(Bus, StateScopes.Driver);
        await Task.Delay(300);
        Assert.Equal((4, 4), stand.Judged());
    }

    /// <summary>
    /// The slice hears the facts it is judged from landing (the service's answers, a part of the list, the sessions' look) by their values'
    /// references, and no other: the lines and the work land often and move nothing here.
    /// </summary>
    [Fact]
    public async Task The_slice_hears_the_answers_a_part_and_the_look_land_and_no_other_fact()
    {
        using var facts = new MachineFacts();
        var whole = new HelpMachineWhole(new Snapshot([], [new RepoView("alpha", Adopted: true, Root: Root("alpha"))], []), [], []);
        facts.Topic(HelpFacts.Machine, HelpFacts.MachineRules, (_, _) => Task.FromResult<object?>(whole with { }));
        facts.Topic(HelpFacts.Lines, HelpFacts.LinesRules, (_, _) => Task.FromResult<object?>(new HelpLineGuess("main")));
        facts.Topic(HelpFacts.Work, HelpFacts.WorkRules, (_, _) => Task.FromResult<object?>(new HelpWork([], [], [])));
        facts.Topic(HelpFacts.Sessions, HelpFacts.WholeRules, (_, _) => Task.FromResult<object?>(new SessionLook([], [], [])));
        facts.Topic(HelpFacts.Sweep, HelpFacts.SweepRules, (_, _) => Task.FromResult<object?>(new SweepPlan([], [])));
        var wakes = 0;
        var hearing = SweepStateSlice.HearFacts(facts, () => Interlocked.Increment(ref wakes));
        facts.Start();

        await facts.JudgedAsync(HelpFacts.Machine, HelpFacts.Whole, CancellationToken.None).WaitAsync(Within);
        Assert.Equal(1, Volatile.Read(ref wakes));
        await facts.JudgedAsync(HelpFacts.Lines, "alpha", CancellationToken.None).WaitAsync(Within);
        await facts.JudgedAsync(HelpFacts.Work, "alpha", CancellationToken.None).WaitAsync(Within);
        Assert.Equal(1, Volatile.Read(ref wakes));
        await facts.JudgedAsync(HelpFacts.Sweep, "alpha", CancellationToken.None).WaitAsync(Within);
        Assert.Equal(2, Volatile.Read(ref wakes));
        await facts.JudgedAsync(HelpFacts.Sessions, HelpFacts.Whole, CancellationToken.None).WaitAsync(Within);
        Assert.Equal(3, Volatile.Read(ref wakes));

        hearing.Dispose();
        await facts.JudgedAsync(HelpFacts.Sweep, "alpha", CancellationToken.None).WaitAsync(Within);
        Assert.Equal(3, Volatile.Read(ref wakes));
    }

    /// <summary>
    /// 🔴 The person's look asks (D148 point 2) and keeps the answer in the landing record, a file no watch hears, moving no ref: the
    /// repositories whose kept answers that look wrote are judged again at once, past the rest, and no other.
    /// </summary>
    [Fact]
    public async Task The_look_s_kept_answers_judge_their_repository_s_part_again_at_once_and_no_other()
    {
        using var stand = new Stand(this, "alpha", "beta");
        stand.Facts.Start();
        await stand.FirstAsync("alpha", "beta");
        await UntilAsync(() => stand.Judged() == (1, 1));
        var entry = new LandedBranch("alpha", "w1", "feature/q1-alpha", "main", new string('a', 40), "s-alpha", "q1", "Work", At);
        var other = new LandedBranch("beta", "w1", "feature/q2-beta", "main", new string('b', 40), "s-beta", "q2", "Work", At);
        var open = new PullRequestState(PullRequestStates.Open) { AskedAt = At };

        Assert.Empty(SweepStateSlice.Asked(stand.Facts, [entry, other], [entry, other]));
        var woken = SweepStateSlice.Asked(stand.Facts, [entry, other], [entry with { PullRequestState = open }, other]);

        Assert.Equal(["alpha"], woken);
        await UntilAsync(() => stand.Judged().Alpha == 2);
        await Task.Delay(200);
        Assert.Equal((2, 1), stand.Judged());
        // A repository the answers name no checkout of is no part, and is woken as none.
        Assert.Empty(SweepStateSlice.Asked(stand.Facts, [], [entry with { Repository = "gone" }]));
    }

    /// <summary>
    /// 🔴 The door: <c>SWEEP_PLAN</c>, the person's look, wakes the part of each repository whose kept answer it wrote while it ran (the
    /// answer written here while the look is held at its first read, as a plugin's would be), at once, within the rest.
    /// </summary>
    [Fact]
    public async Task SWEEP_PLAN_that_kept_an_answer_judges_that_repository_s_part_again_at_once()
    {
        var service = new StandIn
        {
            Registry = $$"""
                [{"repository":"alpha","workspace":"w1","adopted":true,"root":"{{Root("alpha")}}"},
                 {"repository":"beta","workspace":"w1","adopted":true,"root":"{{Root("beta")}}"}]
                """,
        };
        var judged = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        var client = new ServiceClient("http://stand-in", null, new HttpClient(service));
        var whole = new HelpMachineWhole(new Snapshot([], [new RepoView("alpha", true, Root("alpha"), "w1"), new RepoView("beta", true, Root("beta"), "w1")], []), [], []);
        HelpFacts.Keep(loop.Facts, new HelpFactJudges(
            _ => Task.FromResult<HelpMachineWhole?>(whole),
            _ => Task.FromResult<SessionLook?>(Look()),
            _ => Task.FromResult<IReadOnlyList<HistoryReading>?>(null),
            (_, _) => Task.FromResult<HelpWork?>(null))
        {
            Guess = (_, _) => Task.FromResult<string?>("main"),
            Sweep = (part, _) =>
            {
                lock (judged) judged[part.Repository] = judged.GetValueOrDefault(part.Repository) + 1;
                return Task.FromResult<SweepPlan?>(Plan(part.Repository));
            },
        });
        await loop.ComeUpAsync(client);
        loop.Facts.Start();
        await loop.Facts.FirstAsync(HelpFacts.Sweep, "alpha", CancellationToken.None).WaitAsync(Within);
        await loop.Facts.FirstAsync(HelpFacts.Sweep, "beta", CancellationToken.None).WaitAsync(Within);
        int Judged(string repository)
        {
            lock (judged) return judged.GetValueOrDefault(repository);
        }

        var landings = new LandedBranches(Home);
        landings.Record(new LandedBranch("alpha", "w1", "feature/q1-alpha", "main", new string('a', 40), "s-alpha", "q1", "Work", At));
        service.Hold();

        var asking = AskAsync(module, "SWEEP_PLAN");
        await service.HeldAsync().WaitAsync(Within);
        landings.Answered(landings.All()[0], new PullRequestState(PullRequestStates.Open) { AskedAt = DateTimeOffset.UtcNow });
        service.Answer();
        var answer = await asking.WaitAsync(Within);

        Assert.True(answer.Success, answer.Error?.Message);
        await UntilAsync(() => Judged("alpha") == 2);
        await Task.Delay(200);
        Assert.Equal((2, 1), (Judged("alpha"), Judged("beta")));
    }

    /// <summary>Each field of a written object as <c>shape.field</c>, and those of each shape it holds.</summary>
    private static IEnumerable<string> FieldsOf(JsonElement element, string shape)
    {
        foreach (var property in element.EnumerateObject())
        {
            yield return $"{shape}.{property.Name}";
            if (!SweepStateSlice.Holds.TryGetValue($"{shape}.{property.Name}", out var inner)) continue;
            var values = property.Value.ValueKind == JsonValueKind.Array ? [.. property.Value.EnumerateArray()] : new[] { property.Value };
            foreach (var value in values.Where(value => value.ValueKind == JsonValueKind.Object))
            {
                foreach (var field in FieldsOf(value, inner)) yield return field;
            }
        }
    }

    /// <summary>Every field the page's types read of the list's rows, as <c>shape.field</c> (<c>settings/Sweep.tsx</c>).</summary>
    private static HashSet<string> PageFields()
    {
        var sweep = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "Daoris.Web", "src", "settings", "Sweep.tsx"));
        var fields = new HashSet<string>(StringComparer.Ordinal);
        ReadType(TypeBody(sweep, "SweepBranch"), "branch", fields);
        ReadType(TypeBody(sweep, "LandedBranch"), "landed", fields);
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

    /// <summary>
    /// The facts on a clock the test moves, each repository's list as the test says it (one kept branch, unless moved or held), the
    /// sessions' look naming each repository's session, and the slice over them.
    /// </summary>
    private sealed class Stand : IDisposable
    {
        private readonly Dictionary<string, SweepPlan> _plans = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, TaskCompletionSource<SweepPlan?>> _holding = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _judged = new(StringComparer.OrdinalIgnoreCase);
        private HelpMachineWhole _whole = null!;

        public Stand(SweepStateSliceTests test, params string[] repositories)
        {
            Register(repositories);
            Facts = new MachineFacts(Clock.Now);
            HelpFacts.Keep(Facts, new HelpFactJudges(
                _ => Task.FromResult<HelpMachineWhole?>(Volatile.Read(ref _whole)),
                _ => Task.FromResult<SessionLook?>(Look(repositories)),
                _ => Task.FromResult<IReadOnlyList<HistoryReading>?>(null),
                (_, _) => Task.FromResult<HelpWork?>(null))
            {
                Guess = (_, _) => Task.FromResult<string?>("main"),
                Sweep = (part, _) =>
                {
                    lock (_plans)
                    {
                        _judged[part.Repository] = _judged.GetValueOrDefault(part.Repository) + 1;
                        return _holding.Remove(part.Repository, out var held)
                            ? held.Task
                            : Task.FromResult<SweepPlan?>(_plans.GetValueOrDefault(part.Repository) ?? Plan(part.Repository));
                    }
                },
            });
            Slice = SweepStateSlice.Of(Facts, test.Bus);
        }

        public Clock Clock { get; } = new();

        public MachineFacts Facts { get; }

        public StateSlice Slice { get; }

        /// <summary>The service's answers naming these repositories, each with a checkout here.</summary>
        public void Register(params string[] repositories) => Volatile.Write(ref _whole, new HelpMachineWhole(
            new Snapshot([], [.. repositories.Select(repository => new RepoView(repository, Adopted: true, Root: Root(repository), "w1"))], []), [], []));

        /// <summary>The part's next judgement waits for the test's answer.</summary>
        public TaskCompletionSource<SweepPlan?> Hold(string repository)
        {
            var held = new TaskCompletionSource<SweepPlan?>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_plans) _holding[repository] = held;
            return held;
        }

        public void Moved(string repository, SweepPlan plan)
        {
            lock (_plans) _plans[repository] = plan;
        }

        public (int Alpha, int Beta) Judged()
        {
            lock (_plans) return (_judged.GetValueOrDefault("alpha"), _judged.GetValueOrDefault("beta"));
        }

        public async Task FirstAsync(params string[] parts)
        {
            await Facts.FirstAsync(HelpFacts.Machine, HelpFacts.Whole, CancellationToken.None).WaitAsync(Within);
            await Facts.FirstAsync(HelpFacts.Sessions, HelpFacts.Whole, CancellationToken.None).WaitAsync(Within);
            foreach (var part in parts) await Facts.FirstAsync(HelpFacts.Sweep, part, CancellationToken.None).WaitAsync(Within);
        }

        public void Dispose() => Facts.Dispose();
    }

    /// <summary>The facts' time, moved on by the test alone, so a rest is passed without waiting it out.</summary>
    private sealed class Clock
    {
        private long _ticks = At.UtcTicks;

        public DateTimeOffset Now() => new(Interlocked.Read(ref _ticks), TimeSpan.Zero);

        public void Pass(TimeSpan span) => Interlocked.Add(ref _ticks, span.Ticks);
    }

    /// <summary>
    /// The service's doors the facts and the pull read, standing in: the registry as a test sets it, nothing else; once held, the next
    /// registry read waits for the test, as a slow machine holds a look's first read.
    /// </summary>
    private sealed class StandIn : HttpMessageHandler
    {
        private readonly TaskCompletionSource _go = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _held = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private string _registry = "[]";
        private int _holding;

        public string Registry
        {
            get => Volatile.Read(ref _registry);
            set => Volatile.Write(ref _registry, value);
        }

        public void Hold() => Volatile.Write(ref _holding, 1);

        public Task HeldAsync() => _held.Task;

        public void Answer() => _go.TrySetResult();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var registry = request.RequestUri!.AbsolutePath == "/api/registry";
            if (registry && Interlocked.Exchange(ref _holding, 0) == 1)
            {
                _held.TrySetResult();
                await _go.Task.WaitAsync(ct);
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(registry ? Registry : "[]", Encoding.UTF8, "application/json"),
            };
        }
    }
}
