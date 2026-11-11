using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A room's apply is one quest to the repository's own session. It has no chain and no configuration, its receipt is the
/// quest's line alone, its quest is found by its key on a retry, and the rule its session runs the doctrine tool by is the repository's. Fast:
/// the worlds and the ask door are stand-ins, the receipts files in a home of the test's own.
/// </summary>
public sealed class PlaceRoomApplyTests : IDisposable
{
    private static readonly SetupPresetsTests.Catalog Plugins = new(new SetupPresetsTests.Entry("example.github-pull-request", [HookPoints.Perform]));

    private readonly string _home = Path.Combine(AppContext.BaseDirectory, "place-room-apply-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private Stand Make(Action<Stand>? shape = null)
    {
        var stand = new Stand(_home);
        shape?.Invoke(stand);
        return stand;
    }

    private static async Task<string> DigestOf(Stand stand, PlaceAsk ask) =>
        (await PlacePlans.PlanAsync(stand.Place, stand.Config, stand.Home, Plugins, true, ask)).Digest;

    private static Task<PlaceResult> Apply(Stand stand, PlaceAsk ask, string digest) => PlaceApplies.ApplyAsync(stand.Doors(), ask, digest, []);

    [Fact]
    public async Task An_apply_writes_its_receipt_first_then_asks_one_quest_with_no_chain_and_changes_no_configuration()
    {
        var stand = Make();
        var ask = PlaceRoomTests.Room();
        var digest = await DigestOf(stand, ask);
        stand.Setup.OnPublish = () => Assert.Equal(PlaceOutcomes.Uncertain, new PlaceReceipts(_home).Read(digest)!.Of(PlaceOperations.RoomOf("engine"))!.Outcome);

        var result = await Apply(stand, ask, digest);

        Assert.True(result.Done, result.Message);
        Assert.Null(result.Refused);
        Assert.Equal("room asked.", result.Message);
        var published = Assert.Single(stand.Setup.Published);
        Assert.Equal(("aurora", "engine"), (published.Workspace, published.To));
        Assert.StartsWith("Add a room for tools/web\n\n", published.Sentence);
        Assert.EndsWith($"{PlaceApplies.KeyOf(digest)}\n", published.Sentence);
        Assert.Contains("`rooms-and-classes`", published.Sentence);
        Assert.Contains("the web app needs conventions of its own", published.Sentence);
        Assert.Null(Assert.Single(stand.Setup.Thens));
        Assert.Equal(0, stand.Changes);
        var kept = new PlaceReceipts(_home).Read(digest)!;
        var operation = Assert.Single(kept.Operations);
        Assert.Equal((PlaceOperations.RoomOf("engine"), PlaceOperations.Room, "engine", PlaceOutcomes.Done, "q-1"), (operation.Id, operation.Kind, operation.Repository, operation.Outcome, operation.Quest));
        Assert.Equal("tools/web", kept.Ask.Room);
    }

    /// <summary>The receipt of a room is the quest's line alone: the operation names the room, and the message says the quest and nothing of a configuration.</summary>
    [Fact]
    public async Task The_receipt_of_a_room_is_the_quests_line_alone()
    {
        var stand = Make();
        var ask = PlaceRoomTests.Room();

        var result = await Apply(stand, ask, await DigestOf(stand, ask));

        var operation = Assert.Single(result.Operations);
        Assert.Equal(PlaceOperations.Room, operation.Kind);
        Assert.Contains("`tools/web`", operation.Said);
        Assert.DoesNotContain("configuration", result.Message);
    }

    [Fact]
    public async Task The_rule_its_session_runs_the_doctrine_tool_by_is_added_to_the_repository_and_nothing_else()
    {
        var stand = Make();
        var ask = PlaceRoomTests.Room();

        await Apply(stand, ask, await DigestOf(stand, ask));

        Assert.Equal(SetupPressRules.RoomRules, PermissionRules.Load(_home).Repositories["engine"].Allow);
    }

    [Fact]
    public async Task A_repository_not_driven_here_has_the_quest_admitted_alone_and_a_driven_one_has_it_started_by_driving()
    {
        var stand = Make();
        var alone = PlaceRoomTests.Room(repository: "docs");
        await Apply(stand, alone, await DigestOf(stand, alone));
        var driven = PlaceRoomTests.Room(path: "tools/api");
        await Apply(stand, driven, await DigestOf(stand, driven));

        var admitted = DriverConfig.Load(DriverConfig.ResolvePath(_home)).Admitted;
        Assert.Contains(admitted, each => each.Value == "docs");
        Assert.DoesNotContain(admitted, each => each.Value == "engine");
    }

    [Fact]
    public async Task A_press_of_an_applied_plan_again_asks_no_second_quest()
    {
        var stand = Make();
        var ask = PlaceRoomTests.Room();
        var digest = await DigestOf(stand, ask);
        await Apply(stand, ask, digest);

        var again = await Apply(stand, ask, digest);

        Assert.True(again.Done);
        Assert.Single(stand.Setup.Published);
    }

    /// <summary>The ask reaches the service and its answer is lost: the receipt says uncertain, and a retry finds the quest by its key and asks nothing.</summary>
    [Fact]
    public async Task A_retry_after_a_lost_answer_finds_the_quest_by_its_key_and_asks_no_second()
    {
        var stand = Make(each => each.Setup.PublishThenLose = true);
        var ask = PlaceRoomTests.Room();
        var digest = await DigestOf(stand, ask);

        var stopped = await Apply(stand, ask, digest);

        Assert.False(stopped.Done);
        Assert.Equal(PlaceOutcomes.Uncertain, new PlaceReceipts(_home).Read(digest)!.Of(PlaceOperations.RoomOf("engine"))!.Outcome);
        Assert.Single(stand.Setup.Published);

        var settled = Assert.Single(await PlaceApplies.SettleAllAsync(stand.Doors()));
        Assert.Equal((PlaceOutcomes.Done, "q-1"), (settled.Of(PlaceOperations.RoomOf("engine"))!.Outcome, settled.Of(PlaceOperations.RoomOf("engine"))!.Quest));

        var retried = await Apply(stand, ask, digest);
        Assert.True(retried.Done);
        Assert.Single(stand.Setup.Published);
    }

    [Fact]
    public async Task A_retry_that_finds_no_quest_asks_it_now_once()
    {
        var stand = Make(each => each.Setup.LoseBefore = true);
        var ask = PlaceRoomTests.Room();
        var digest = await DigestOf(stand, ask);
        var stopped = await Apply(stand, ask, digest);
        Assert.False(stopped.Done);
        Assert.Empty(stand.Setup.Published);

        stand.Setup.LoseBefore = false;
        var settled = Assert.Single(await PlaceApplies.SettleAllAsync(stand.Doors()));
        Assert.Equal(PlaceOutcomes.Pending, settled.Of(PlaceOperations.RoomOf("engine"))!.Outcome);
        var retried = await Apply(stand, ask, digest);

        Assert.True(retried.Done, retried.Message);
        Assert.Single(stand.Setup.Published);
    }

    /// <summary>The plan the person read no longer holds once the room is there: the digest moved, so nothing is written, and the plan read again refuses it.</summary>
    [Fact]
    public async Task A_room_that_appeared_after_the_plan_moves_it_and_nothing_is_written()
    {
        var stand = Make();
        var ask = PlaceRoomTests.Room();
        var digest = await DigestOf(stand, ask);
        stand.Place.Inner.Rooms["engine"] = new PlaceRoomRead("main", "c0ffee", true, ["tools/web"], [], [], null, false);

        var result = await Apply(stand, ask, digest);

        Assert.False(result.Done);
        Assert.Equal(PlaceApplyRefusals.Moved, result.Refused);
        Assert.Empty(stand.Setup.Published);
        Assert.Null(new PlaceReceipts(_home).Read(digest));
        var again = await PlacePlans.PlanAsync(stand.Place, stand.Config, stand.Home, Plugins, true, ask);
        Assert.Contains(again.Refusals, each => each.Code == PlaceRoomRefusals.Exists);
    }

    /// <summary>
    /// The ask is judged again where it is asked: a stop before the ask leaves the receipt, and a room that appeared before the retry leaves the
    /// operation failed with the plan's own sentence, nothing published.
    /// </summary>
    [Fact]
    public async Task A_room_that_appeared_before_the_retry_is_refused_where_it_is_asked_and_nothing_is_published()
    {
        var stand = Make(each => each.Setup.LoseBefore = true);
        var ask = PlaceRoomTests.Room();
        var digest = await DigestOf(stand, ask);
        await Apply(stand, ask, digest);
        stand.Setup.LoseBefore = false;
        stand.Place.Inner.Rooms["engine"] = new PlaceRoomRead("main", "c0ffee", true, [], [], ["tools/web"], null, false);
        await PlaceApplies.SettleAllAsync(stand.Doors());

        var result = await Apply(stand, ask, digest);

        Assert.False(result.Done);
        Assert.Contains("room refused", result.Message);
        var operation = new PlaceReceipts(_home).Read(digest)!.Of(PlaceOperations.RoomOf("engine"))!;
        Assert.Equal(PlaceOutcomes.Failed, operation.Outcome);
        Assert.Contains("already has a room at `tools/web`", operation.Note);
        Assert.Empty(stand.Setup.Published);
    }

    [Fact]
    public async Task A_plan_that_refuses_a_stale_digest_and_a_word_that_is_no_digest_write_nothing()
    {
        var stand = Make();
        var ask = PlaceRoomTests.Room();
        var refused = PlaceRoomTests.Room(path: "../x");
        var stale = await DigestOf(stand, ask);

        var plan = await Apply(stand, refused, await DigestOf(stand, refused));
        var moved = await Apply(stand, ask with { Purpose = "another reason" }, stale);
        var word = await Apply(stand, ask, "not-a-digest");

        Assert.Equal(PlaceApplyRefusals.Refused, plan.Refused);
        Assert.Equal(PlaceApplyRefusals.Moved, moved.Refused);
        Assert.Equal(PlaceApplyRefusals.Digest, word.Refused);
        Assert.Empty(stand.Setup.Published);
        Assert.Equal(0, stand.Changes);
    }

    /// <summary>The terminal's apply says the plan, then the quest's line: `quest #… for `engine``.</summary>
    [Fact]
    public async Task The_verb_applied_alone_says_the_plan_then_the_quest()
    {
        var stand = Make();
        var output = new StringWriter();

        var code = await PlaceCommand.RunAsync(
            ["engine", "--room", "tools/web", "--purpose", "the web app needs conventions of its own", "--apply"], output, stand.Place, stand.Config, _home, Plugins, true,
            instance: null, apply: stand.Doors());

        var lines = output.ToString().ReplaceLineEndings("\n").TrimEnd('\n').Split('\n');
        Assert.Equal(0, code);
        Assert.Equal("place: room asked.", lines[^2]);
        Assert.Equal("  quest  #q-1 for `engine`", lines[^1]);
        Assert.DoesNotContain(lines, line => line.StartsWith("--plan:", StringComparison.Ordinal));
        Assert.Single(stand.Setup.Published);
    }

    /// <summary>The verb hands its result to the door that asked for it, which is how the window's press says the quest it asked.</summary>
    [Fact]
    public async Task The_verb_applied_alone_hands_its_result_to_the_door_that_asked_for_it()
    {
        var stand = Make();
        PlaceResult? made = null;

        var code = await PlaceCommand.RunAsync(
            ["engine", "--room", "tools/web", "--purpose", "the web app needs conventions of its own", "--apply"], new StringWriter(), stand.Place, stand.Config, _home, Plugins, true,
            instance: null, apply: stand.Doors() with { Made = result => made = result });

        Assert.Equal(0, code);
        var receipt = PressReceipt.OfRoom(made!, "engine", "tools/web");
        Assert.Equal(("room", "engine", "tools/web", true), (receipt.Kind, receipt.Repository, receipt.Room, receipt.Done));
        Assert.Equal(["q-1"], receipt.Quests);
    }

    private sealed class Stand
    {
        public Stand(string home)
        {
            Home = home;
            Place = new Counting(new PlacePlanTests.World());
            Setup = new RoomSetupWorld();
        }

        public string Home { get; }

        public Counting Place { get; }

        public RoomSetupWorld Setup { get; }

        public DriverConfig Config { get; set; } = (DriverConfig.Empty with { Actors = StaffedRoles.On("claude-code-acp") }).WithDrivable("engine", true);

        public int Changes { get; private set; }

        public PlaceApplyDoors Doors() => new(Place, Setup, Home, Plugins, true, () => Config, Change, new DateOnly(2026, 11, 1));

        private void Change(Func<DriverConfig, DriverConfig> change)
        {
            Config = change(Config);
            Changes++;
        }
    }

    private sealed class Counting(PlacePlanTests.World inner) : IPlaceWorld
    {
        public PlacePlanTests.World Inner { get; } = inner;

        public Task<IReadOnlyList<RepoView>> RegistryAsync(CancellationToken ct) => Inner.RegistryAsync(ct);

        public Task<IReadOnlyList<QuestView>> QuestsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<QuestView>>([.. Inner.Quests]);

        public Task<PlaceTip> TipAsync(RepoView repository, DriverConfig config, CancellationToken ct) => Inner.TipAsync(repository, config, ct);

        public Task<PlaceRoomRead> RoomAsync(RepoView repository, DriverConfig config, string path, CancellationToken ct) => Inner.RoomAsync(repository, config, path, ct);
    }

    /// <summary>The set-up press's world as far as a room's quest reaches it: the quests it keeps and an ask door that keeps what it is asked.</summary>
    private sealed class RoomSetupWorld : ISetupWorld
    {
        public List<QuestView> Quests { get; } = [];

        public List<(string Workspace, string Sentence, string To)> Published { get; } = [];

        public List<SetupThen?> Thens { get; } = [];

        public Action? OnPublish { get; set; }

        public bool PublishThenLose { get; set; }

        public bool LoseBefore { get; set; }

        public Task<IReadOnlyList<RepoView>> RegistryAsync(CancellationToken ct) => throw new NotSupportedException("a room's quest reads the registry through the place's world");

        public Task<LineReading> ReadLineAsync(RepoView repository, DriverConfig config, CancellationToken ct) => throw new NotSupportedException("a room reads no line through the set-up's world");

        public Task<SetupTools> ToolsAsync(CancellationToken ct) => throw new NotSupportedException("a room asks no tool");

        public Task<DoctrineReading> StatusAsync(RepoView repository, CancellationToken ct) => throw new NotSupportedException("a room asks no status");

        public Task<IReadOnlyList<QuestView>> QuestsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<QuestView>>([.. Quests]);

        public Task<IReadOnlyList<string>> EntriesAsync(string repository, CancellationToken ct) => throw new NotSupportedException("a room reads no entries");

        public RepositoryDescription? Describe(string root) => null;

        public Task<AskAnswer> PublishAsync(string workspace, string sentence, string to, SetupThen? then, CancellationToken ct)
        {
            OnPublish?.Invoke();
            if (LoseBefore) throw new HttpRequestException("the service did not answer");
            Published.Add((workspace, sentence, to));
            Thens.Add(then);
            var id = $"q-{Published.Count}";
            Quests.Add(new QuestView(id, "work", to, sentence.Split('\n')[0], sentence[(sentence.IndexOf('\n') + 1)..], "Open"));
            if (PublishThenLose) throw new HttpRequestException("the answer was lost");
            return Task.FromResult(new AskAnswer(true, $"Asked as `#a{id}`.", $"a{id}", id));
        }
    }
}
