using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The resolved lines and repository rules, the push path's second slice (PUSH1b1, D182): one owner in the shell's loop judging,
/// from the machine's facts and the driver's file alone, a partition per repository carrying its rows of what <c>LINES</c> answers,
/// pushed when one moves and read by a window's first snapshot from memory.
/// </summary>
/// <remarks>
/// The facts are kept as the module keeps them (<see cref="HelpFacts.Keep"/>), the service's answers read through the driver's own
/// client from a stand-in, and git's guess at each checkout's line stood in by name: no process and no git.
/// </remarks>
public sealed class LinesStateSliceTests : DriverModuleBridge
{
    private static readonly TimeSpan Within = TimeSpan.FromSeconds(20);

    /// <summary>What the page is handed, as <see cref="Bridge"/>'s answers are read: camelCase, nulls written.</summary>
    private static readonly JsonSerializerOptions Page = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly StandIn _service = new();
    private readonly Dictionary<string, Task<string?>> _guesses = new(StringComparer.OrdinalIgnoreCase);

    private string Root(string repository) => Path.Combine(Home, "checkouts", repository).Replace('\\', '/');

    /// <summary>The registry as the service answers it: each repository in its workspace, with a checkout here or none.</summary>
    private string Registry(params (string Repository, string Workspace, bool Checkout)[] rows) =>
        JsonSerializer.Serialize(rows.Select(row => new
        {
            repository = row.Repository, workspace = row.Workspace, adopted = true, root = row.Checkout ? Root(row.Repository) : null,
        }));

    /// <summary>Git's guess at a checkout's line, by the repository its folder is named for: `main` unless a test says otherwise.</summary>
    private Task<string?> Guess(string root, CancellationToken _)
    {
        lock (_guesses) return _guesses.GetValueOrDefault(Path.GetFileName(root)) ?? Task.FromResult<string?>("main");
    }

    private void Guessing(string repository, Task<string?> guess)
    {
        lock (_guesses) _guesses[repository] = guess;
    }

    /// <summary>
    /// A loop come up over the stand-in service, its module keeping Ask Daoris's facts, then the facts' judges standing in for the
    /// roster and git; the facts not started, as before the service answers.
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
            (_, _) => Task.FromResult<HelpWork?>(null))
        {
            Guess = Guess,
        });
        await loop.ComeUpAsync(client);
        return (loop, module, loop.Slices.Find(LinesStateSlice.Name)!);
    }

    /// <summary>The facts started, as the loop starts them once the service answers, and the service's answers judged.</summary>
    private static async Task LandedAsync(DriverLoop loop)
    {
        loop.Facts.Start();
        await loop.Facts.FirstAsync(HelpFacts.Machine, HelpFacts.Whole, CancellationToken.None).WaitAsync(Within);
    }

    private IReadOnlyList<SlicePush> Pushes() =>
    [
        .. Raised.Where(each => each.Type == StateSlice.PushType).Select(each => (SlicePush)each.Payload!)
            .Where(push => push.Slice == LinesStateSlice.Name),
    ];

    private IReadOnlyList<SlicePush> Pushed(string repository) => [.. Pushes().Where(push => push.Partition == repository)];

    private static JsonObject Line(SlicePush push) =>
        JsonSerializer.SerializeToNode(push.Data, IpcJson.Options)!["lines"]!.AsArray().Single()!.AsObject();

    /// <summary>The branch the newest push of a repository says, or null where none was pushed or it says none.</summary>
    private string? Branch(string repository) =>
        Pushed(repository) is { Count: > 0 } pushed && pushed[^1].Status == StateSlice.Ready ? Line(pushed[^1])["branch"]?.GetValue<string>() : null;

    [Fact]
    public async Task Each_repository_is_a_partition_of_its_rows_of_what_LINES_answers_and_every_field_LINES_answers_is_classed()
    {
        DriverConfig.Empty.WithLine("alpha", "trunk").WithWorkspaceLanding("w2", LandingRule.Merge).Save(DriverConfigPath);
        _service.Registry = Registry(("alpha", "w1", true), ("beta", "w2", false));
        var (loop, module, slice) = await UpAsync();
        await LandedAsync(loop);

        await slice.JudgeAsync();
        var snapshot = slice.Snapshot();
        var lines = await AnswerAsync(module, "LINES");

        Assert.Equal(LinesStateSlice.Name, snapshot.Slice);
        Assert.Equal(["alpha", "beta"], snapshot.Partitions.Select(each => each.Partition));
        // A field LINES comes to answer is pushed or named as the pull's alone, never left to chance.
        var classed = LinesStateSlice.Fields.Concat(LinesStateSlice.PulledOnly).ToHashSet();
        Assert.All(lines.EnumerateObject().Select(each => each.Name), field => Assert.Contains(field, classed));
        foreach (var partition in snapshot.Partitions)
        {
            var data = JsonSerializer.SerializeToElement(partition.Data, Page);
            Assert.Equal(LinesStateSlice.Fields.Order(), data.EnumerateObject().Select(each => each.Name).Order());
            foreach (var field in LinesStateSlice.Fields)
            {
                // The pull's rows of this repository and the push's, written alike: one builder, two doors.
                var pulled = lines.GetProperty(field).EnumerateArray()
                    .Where(row => row.GetProperty("repository").GetString() == partition.Partition).Select(row => row.GetRawText());
                Assert.Equal(pulled, data.GetProperty(field).EnumerateArray().Select(row => row.GetRawText()));
            }
        }

        Assert.Equal(("trunk", "repository"), (Line(Pushed("alpha")[0])["branch"]!.GetValue<string>(), Line(Pushed("alpha")[0])["source"]!.GetValue<string>()));
    }

    /// <summary>
    /// AUTONOMY1a (D184 point 1): each repository's session access setup as it resolves, every pause with its value and where it came
    /// from, is answered by <c>LINES</c> and pushed in the repository's partition beside its other rules, which the page's setup reads.
    /// </summary>
    [Fact]
    public async Task Each_repository_s_access_setup_is_pushed_as_it_resolves_with_where_each_pause_came_from()
    {
        DriverConfig.Empty
            .WithAccess(new AccessEdit { Workspace = "w1", Full = true })
            .WithAccess(new AccessEdit { Repository = "alpha", Set = [new("permissions", "asking")] })
            .Save(DriverConfigPath);
        _service.Registry = Registry(("alpha", "w1", true), ("beta", "w2", false));
        var (loop, module, slice) = await UpAsync();
        await LandedAsync(loop);

        await slice.JudgeAsync();
        var pushed = slice.Snapshot().Partitions.ToDictionary(
            each => each.Partition, each => JsonSerializer.SerializeToElement(each.Data, Page).GetProperty("access").EnumerateArray().Single().GetRawText());
        var pulled = (await AnswerAsync(module, "LINES")).GetProperty("access");

        Assert.Equal(
            """{"repository":"alpha","workspace":"w1","full":false,"pauses":[{"pause":"permissions","value":"asking","source":"repository"},"""
            + """{"pause":"routing","value":"intake","source":"workspace"},{"pause":"landing","value":"rule","source":"workspace"},"""
            + """{"pause":"goAheads","value":"never","source":"workspace"},{"pause":"questions","value":"claude-code","source":"workspace"}]}""",
            pushed["alpha"]);
        Assert.Equal(
            """{"repository":"beta","workspace":"w2","full":false,"pauses":[{"pause":"permissions","value":"auto","source":"default"},"""
            + """{"pause":"routing","value":"person","source":"default"},{"pause":"landing","value":"person","source":"default"},"""
            + """{"pause":"goAheads","value":"named","source":"default"},{"pause":"questions","value":"claude-code","source":"default"}]}""",
            pushed["beta"]);
        Assert.Equal([pushed["alpha"], pushed["beta"]], pulled.EnumerateArray().Select(row => row.GetRawText()));
    }

    [Fact]
    public async Task A_hostile_field_in_the_driver_s_file_or_the_registry_never_reaches_a_partition()
    {
        File.WriteAllText(DriverConfigPath, """
            {
              "token": "sk-hostile-top",
              "commands": { "alpha": ["deploy --key sk-hostile-command"] },
              "landings": { "alpha": { "form": "merge", "secret": "sk-hostile-nested" } },
              "lines": { "alpha": "trunk" }
            }
            """);
        _service.Registry = $$"""
            [{"repository":"alpha","workspace":"w1","adopted":true,"root":"{{Root("alpha")}}","token":"sk-hostile-registry"}]
            """;
        var (loop, _, slice) = await UpAsync();
        await LandedAsync(loop);

        await slice.JudgeAsync();

        var pushed = JsonSerializer.Serialize(Pushes().Select(push => push.Data), IpcJson.Options);
        Assert.Contains("trunk", pushed);
        Assert.DoesNotContain("sk-hostile", pushed);
        // The checkout's folder is this machine's: a row names its repository and its workspace, never where it is.
        Assert.DoesNotContain(Path.GetFileName(Home), pushed);
        Assert.DoesNotContain("root", pushed);
    }

    [Fact]
    public async Task A_second_window_s_snapshot_is_read_from_memory_and_judges_nothing()
    {
        _service.Registry = Registry(("alpha", "w1", true));
        var (loop, module, slice) = await UpAsync();
        await LandedAsync(loop);
        await loop.Facts.FirstAsync(HelpFacts.Lines, "alpha", CancellationToken.None).WaitAsync(Within);
        await slice.JudgeAsync();

        var first = await AnswerAsync(module, "STATE_SLICE", new { slice = "lines" });
        var second = await AnswerAsync(module, "STATE_SLICE", new { slice = "lines" });

        Assert.Equal(1, slice.Judgements);
        Assert.Equal(first.GetRawText(), second.GetRawText());
        Assert.Equal(StateSlice.Ready, first.GetProperty("status").GetString());
        Assert.Equal(["alpha"], first.GetProperty("partitions").EnumerateArray().Select(each => each.GetProperty("partition").GetString()));
    }

    /// <summary>The slices start before the machine's facts, which wait for the service: until they land, nothing is the truth.</summary>
    [Fact]
    public async Task Until_the_machine_s_facts_land_the_slice_answers_not_judged_and_pushes_nothing()
    {
        _service.Registry = Registry(("alpha", "w1", true));
        var (loop, module, slice) = await UpAsync();

        await slice.JudgeAsync();
        var before = await AnswerAsync(module, "STATE_SLICE", new { slice = "lines" });

        Assert.Equal(StateSlice.NotJudged, before.GetProperty("status").GetString());
        Assert.Empty(before.GetProperty("partitions").EnumerateArray());
        Assert.Empty(Pushes());

        await LandedAsync(loop);
        await slice.JudgeAsync();
        Assert.Equal(StateSlice.Ready, slice.Snapshot().Status);
        Assert.Equal(["alpha"], slice.Snapshot().Partitions.Select(each => each.Partition));
    }

    [Fact]
    public async Task A_repository_whose_line_was_not_judged_yet_is_pushed_said_not_read_yet_then_its_line()
    {
        _service.Registry = Registry(("alpha", "w1", true), ("beta", "w1", false));
        var held = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        Guessing("alpha", held.Task);
        var (loop, _, slice) = await UpAsync();
        using var stop = new CancellationTokenSource();
        var running = slice.RunAsync(stop.Token);
        await LandedAsync(loop);

        await UntilAsync(() => Pushed("alpha").Count == 1 && Pushed("beta").Count == 1);
        var unread = Line(Pushed("alpha")[0]);
        Assert.Null(unread["branch"]);
        Assert.True(unread["notReadYet"]!.GetValue<bool>());
        // Its rules are pushed beside it all the same.
        Assert.Single(JsonSerializer.SerializeToNode(Pushed("alpha")[0].Data, IpcJson.Options)!["landings"]!.AsArray());
        // No checkout here leaves nothing to read: what is set, or none, is the line.
        Assert.False(Line(Pushed("beta")[0]).ContainsKey("notReadYet"));

        held.SetResult("main");
        await UntilAsync(() => Pushed("alpha").Count == 2);
        var read = Line(Pushed("alpha")[1]);
        Assert.Equal(("main", "checkout"), (read["branch"]!.GetValue<string>(), read["source"]!.GetValue<string>()));
        Assert.False(read.ContainsKey("notReadYet"));
        Assert.Single(Pushed("beta"));

        await stop.CancelAsync();
        await running;
    }

    [Fact]
    public async Task A_ref_moved_in_one_repository_pushes_that_partition_alone()
    {
        _service.Registry = Registry(("alpha", "w1", true), ("beta", "w1", true));
        var (loop, _, slice) = await UpAsync();
        using var stop = new CancellationTokenSource();
        var running = slice.RunAsync(stop.Token);
        await LandedAsync(loop);
        await UntilAsync(() => Branch("alpha") == "main" && Branch("beta") == "main");
        var beta = Pushed("beta").Count;

        Guessing("alpha", Task.FromResult<string?>("trunk"));
        // What the ref watch tells the facts of a ref moved (DriverWatch's Heard, PARALLEL1a).
        HelpFacts.Wake(loop.Facts, ChangeTopics.Refs, "alpha");
        await UntilAsync(() => Branch("alpha") == "trunk");
        await slice.JudgeAsync();

        Assert.Equal(beta, Pushed("beta").Count);
        await stop.CancelAsync();
        await running;
    }

    [Fact]
    public async Task A_line_set_by_a_press_pushes_only_the_partitions_it_changes()
    {
        _service.Registry = Registry(("alpha", "w1", true), ("beta", "w2", true), ("gamma", "w2", true));
        var (loop, module, slice) = await UpAsync();
        using var stop = new CancellationTokenSource();
        var running = slice.RunAsync(stop.Token);
        await LandedAsync(loop);
        await UntilAsync(() => Branch("alpha") == "main" && Branch("beta") == "main" && Branch("gamma") == "main");
        var (beta, gamma) = (Pushed("beta").Count, Pushed("gamma").Count);

        await AnswerAsync(module, "SET_LINE", new { repository = "alpha", branch = "release" });
        // What the dispatcher's publisher says once the press has answered.
        StatePublisher.Publish(Bus, StateScopes.Driver);
        await UntilAsync(() => Branch("alpha") == "release");
        await slice.JudgeAsync();
        Assert.Equal((beta, gamma), (Pushed("beta").Count, Pushed("gamma").Count));

        var alpha = Pushed("alpha").Count;
        await AnswerAsync(module, "SET_LINE", new { workspace = "w2", branch = "develop" });
        StatePublisher.Publish(Bus, StateScopes.Driver);
        await UntilAsync(() => Branch("beta") == "develop" && Branch("gamma") == "develop");
        await slice.JudgeAsync();
        Assert.Equal(alpha, Pushed("alpha").Count);

        await stop.CancelAsync();
        await running;
    }

    /// <summary>
    /// A terminal's `daoris driver line` writes the file no press declares: the home's watch tells the facts (HomeWatch's config topic,
    /// through DriverWatch's Heard), which judge the service's answers again, and their landing wakes the slice.
    /// </summary>
    [Fact]
    public async Task A_terminal_s_edit_of_the_driver_s_file_reaches_the_slice_through_the_machine_s_answers_landing()
    {
        _service.Registry = Registry(("alpha", "w1", true), ("beta", "w1", true));
        var (loop, _, slice) = await UpAsync();
        using var stop = new CancellationTokenSource();
        var running = slice.RunAsync(stop.Token);
        await LandedAsync(loop);
        await UntilAsync(() => Branch("alpha") == "main" && Branch("beta") == "main");
        var (beta, asked) = (Pushed("beta").Count, _service.RegistryReads);

        DriverConfig.Empty.WithLine("alpha", "release").Save(DriverConfigPath);
        HelpFacts.Wake(loop.Facts, ChangeTopics.Config, "driver.json");
        await UntilAsync(() => Branch("alpha") == "release");
        await slice.JudgeAsync();

        Assert.True(_service.RegistryReads > asked);
        Assert.Equal(beta, Pushed("beta").Count);
        await stop.CancelAsync();
        await running;
    }

    /// <summary>
    /// The service's answers are judged again at their deadline, a ledger move or the driver's file moving: a repository a terminal
    /// imported, which no event tells, is a partition within that minute; one the registry no longer names is pushed gone.
    /// </summary>
    [Fact]
    public async Task A_repository_registered_appears_once_the_machine_s_answers_land_and_one_retired_is_pushed_gone()
    {
        _service.Registry = Registry(("alpha", "w1", true), ("beta", "w1", true));
        var (loop, _, slice) = await UpAsync();
        using var stop = new CancellationTokenSource();
        var running = slice.RunAsync(stop.Token);
        await LandedAsync(loop);
        await UntilAsync(() => Branch("alpha") == "main" && Branch("beta") == "main");

        _service.Registry = Registry(("alpha", "w1", true), ("gamma", "w1", true));
        await loop.Facts.JudgedAsync(HelpFacts.Machine, HelpFacts.Whole, CancellationToken.None).WaitAsync(Within);

        await UntilAsync(() => Branch("gamma") == "main");
        await UntilAsync(() => Pushed("beta")[^1].Status == StateSlice.Gone);
        Assert.Equal(["alpha", "gamma"], slice.Snapshot().Partitions.Select(each => each.Partition));
        Assert.True(HelpFacts.MachineRules.Deadline <= TimeSpan.FromMinutes(1));

        await stop.CancelAsync();
        await running;
    }

    /// <summary>
    /// A repository imported from a window is declared (the registry's scope), and no ledger family moves: the slice's owner has the
    /// service's answers judged again at once, so it is a partition without waiting for their deadline.
    /// </summary>
    [Fact]
    public async Task A_registry_change_a_press_declared_judges_the_service_s_answers_again_and_the_repository_appears_at_once()
    {
        _service.Registry = Registry(("alpha", "w1", true));
        var (loop, _, slice) = await UpAsync();
        using var stop = new CancellationTokenSource();
        var running = slice.RunAsync(stop.Token);
        await LandedAsync(loop);
        await UntilAsync(() => Branch("alpha") == "main");
        var asked = _service.RegistryReads;

        _service.Registry = Registry(("alpha", "w1", true), ("gamma", "w1", true));
        StatePublisher.Publish(Bus, StateScopes.Registry);

        await UntilAsync(() => Branch("gamma") == "main");
        Assert.True(_service.RegistryReads > asked);
        await stop.CancelAsync();
        await running;
    }

    /// <summary>
    /// A repository imported from a terminal moves no ledger family, and the tick hears it in the registry's signature (REGWAKE1): a
    /// changed signature has the service's answers judged again within the tick; the first one heard, and an unchanged one, wake nothing.
    /// </summary>
    [Fact]
    public async Task A_registry_change_the_tick_hears_judges_the_service_s_answers_again_and_the_first_signature_wakes_nothing()
    {
        _service.Registry = Registry(("alpha", "w1", true));
        var (loop, _, slice) = await UpAsync();
        using var stop = new CancellationTokenSource();
        var running = slice.RunAsync(stop.Token);
        await LandedAsync(loop);
        await UntilAsync(() => Branch("alpha") == "main");

        var asked = _service.RegistryReads;
        Assert.True(DriverLoop.RegistryHeard(loop.Facts, "a", null));
        Assert.True(DriverLoop.RegistryHeard(loop.Facts, "a", "a") is false);
        await Task.Delay(300);
        Assert.Equal(asked, _service.RegistryReads);

        _service.Registry = Registry(("alpha", "w1", true), ("gamma", "w1", true));
        Assert.True(DriverLoop.RegistryHeard(loop.Facts, "b", "a"));

        await UntilAsync(() => Branch("gamma") == "main");
        Assert.True(_service.RegistryReads > asked);
        Assert.Contains("gamma", slice.Snapshot().Partitions.Select(each => each.Partition));
        await stop.CancelAsync();
        await running;
    }

    /// <summary>The facts' other topics land often (the sessions, the history, the work) and move nothing here, so they wake nothing.</summary>
    [Fact]
    public async Task The_slice_hears_the_service_s_answers_and_the_lines_land_and_no_other_fact()
    {
        using var facts = new MachineFacts();
        var whole = new HelpMachineWhole(new Snapshot([], [new RepoView("alpha", Adopted: true, Root: "checkouts/alpha")], []), [], []);
        // Each judgement a fresh value, as the service's answers are read afresh.
        facts.Topic(HelpFacts.Machine, HelpFacts.MachineRules, (_, _) => Task.FromResult<object?>(whole with { }));
        facts.Topic(HelpFacts.Lines, HelpFacts.LinesRules, (_, _) => Task.FromResult<object?>(new HelpLineGuess("main")));
        facts.Topic(HelpFacts.Sessions, HelpFacts.WholeRules, (_, _) => Task.FromResult<object?>(new SessionLook([], [], [])));
        var wakes = 0;
        var hearing = LinesStateSlice.HearFacts(facts, () => Interlocked.Increment(ref wakes));
        facts.Start();

        await facts.JudgedAsync(HelpFacts.Machine, HelpFacts.Whole, CancellationToken.None).WaitAsync(Within);
        Assert.Equal(1, Volatile.Read(ref wakes));
        await facts.JudgedAsync(HelpFacts.Sessions, HelpFacts.Whole, CancellationToken.None).WaitAsync(Within);
        Assert.Equal(1, Volatile.Read(ref wakes));
        await facts.JudgedAsync(HelpFacts.Lines, "alpha", CancellationToken.None).WaitAsync(Within);
        Assert.Equal(2, Volatile.Read(ref wakes));

        hearing.Dispose();
        await facts.JudgedAsync(HelpFacts.Machine, HelpFacts.Whole, CancellationToken.None).WaitAsync(Within);
        Assert.Equal(2, Volatile.Read(ref wakes));
    }

    /// <summary>
    /// The judgement reads the facts and the driver's file, never the checkouts: where the pull asks git of each checkout with nothing
    /// set, the push starts nothing, counted at the process seam.
    /// </summary>
    [Fact]
    public async Task A_judgement_starts_no_git_and_no_process()
    {
        _service.Registry = Registry(("alpha", "w1", true), ("beta", "w2", true));
        var (loop, _, slice) = await UpAsync();
        await LandedAsync(loop);
        await loop.Facts.FirstAsync(HelpFacts.Lines, "alpha", CancellationToken.None).WaitAsync(Within);
        await loop.Facts.FirstAsync(HelpFacts.Lines, "beta", CancellationToken.None).WaitAsync(Within);
        var (gits, starts) = (0, 0);

        using (ProcessStarts.Within(info =>
               {
                   Interlocked.Increment(ref starts);
                   if (Path.GetFileNameWithoutExtension(info.FileName).Equals("git", StringComparison.OrdinalIgnoreCase)) Interlocked.Increment(ref gits);
                   return null;
               }))
        {
            await slice.JudgeAsync();
        }

        Assert.Equal((0, 0), (gits, starts));
        Assert.Equal(["main", "main"], slice.Snapshot().Partitions.Select(each =>
            JsonSerializer.SerializeToNode(each.Data, IpcJson.Options)!["lines"]![0]!["branch"]!.GetValue<string>()));
    }

    [Theory]
    [InlineData("STATE_CHANGED", """{"scopes":["driver"]}""", true)]
    [InlineData("STATE_CHANGED", """{"scopes":["lines"]}""", true)]
    [InlineData("STATE_CHANGED", """{"scopes":["harnesses"]}""", true)]
    [InlineData("STATE_CHANGED", """{"scopes":["sessions"],"sessions":["s1"]}""", false)]
    // The registry's scope judges the service's answers again, whose landing wakes the slice: never the slice before they land.
    [InlineData("STATE_CHANGED", """{"scopes":["registry"]}""", false)]
    [InlineData("SESSION_ENDED", "{}", false)]
    [InlineData("DRIVER_READY", "{}", true)]
    public void What_wakes_the_lines_slice(string type, string payload, bool wakes)
    {
        var wake = LinesStateSlice.WakesOn.FirstOrDefault(each => each.Type == type);
        object told = type == StatePublisher.Type ? JsonSerializer.Deserialize<StateChanged>(payload, IpcJson.Options)! : new { };

        Assert.Equal(wakes, wake is not null && (wake.When is null || wake.When(told)));
    }

    /// <summary>The service's doors the facts and the pull read, standing in: the registry as a test sets it, nothing else.</summary>
    private sealed class StandIn : HttpMessageHandler
    {
        private string _registry = "[]";
        private int _registryReads;

        public string Registry
        {
            get => Volatile.Read(ref _registry);
            set => Volatile.Write(ref _registry, value);
        }

        /// <summary>How often the registry was read: once each judgement of the service's answers, and each pull.</summary>
        public int RegistryReads => Volatile.Read(ref _registryReads);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var registry = request.RequestUri!.AbsolutePath == "/api/registry";
            if (registry) Interlocked.Increment(ref _registryReads);
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(registry ? Registry : "[]", Encoding.UTF8, "application/json"),
            });
        }
    }
}
