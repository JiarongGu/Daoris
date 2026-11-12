using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Desktop.Driver.Tests;
using Daoris.Driver;
using Microsoft.Extensions.DependencyInjection;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A conversation asked for a new repository creates one in each mode: Ask Daoris's proved press of
/// <c>REGISTRY_CREATE</c> runs at once in full access with no card, and in restricted mode waits on a Confirm card naming Ask Daoris and runs on
/// the person's yes. The route's body is the real one (<see cref="RepositoryCreateCommand.ApplyAsync"/>: the plan, the receipt and a real
/// <c>git init</c> in the repository's gitignored <c>_fixtures</c>); the registry, the identities and the quest door are stand-ins.
/// </summary>
public sealed class PipeDelegateGrowthTests : PipeDelegateHarness
{
    private static readonly CreateAuthor Fixture = new("Fixture", "fixture@example.test");
    private static readonly DateOnly Day = new(2026, 11, 12);

    private static readonly object Creates = new
    {
        name = "ledger", purpose = "keeps the books", workspace = "aurora", work = "build the ledger's first report",
    };

    /// <summary>The route's body as the driver module runs it: the ask the press's words make, applied by the verb's own path, with the receipt it made beside its answer.</summary>
    private static Func<IpcRequest, Task<object?>> CreateRoute(CreationWorld world) => async request =>
    {
        var ask = CreateAskOf(request.Payload!.Value);
        CreateResult? made = null;
        var output = new StringWriter { NewLine = "\n" };
        var code = await RepositoryCreateCommand.ApplyAsync(ask, null, world.Doors(), output, default, result => made = result).ConfigureAwait(false);
        // The answer a press not asked for its words gives: whether the verb exited 0, its lines, and what it opened.
        var receipt = made is { Name: not null } ? PressReceipt.Of(made) : null;
        return new { Done = code == 0, Message = output.ToString().TrimEnd('\n'), Receipt = receipt };
    };

    private static CreateAsk CreateAskOf(JsonElement payload)
    {
        string? Said(string name) => payload.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text ? text : null;
        return new CreateAsk(Said("name")!, Said("purpose")!) { Workspace = Said("workspace"), Work = Said("work"), Kind = Said("kind") ?? PlaceKind.Git };
    }

    /// <summary>The press's own ground, which its card is made from: the route's own planner, said as the plan's wire says it.</summary>
    private static StubGrounds CreateGrounds(CreationWorld world)
    {
        var grounds = new StubGrounds();
        grounds.Reads["REGISTRY_CREATE_PLAN"] = payload =>
            CreatePlanWire.Of(RepositoryCreatePlans.PlanAsync(world.Doors(), CreateAskOf(payload!.Value)).GetAwaiter().GetResult(), Day);
        return grounds;
    }

    [Fact]
    [Trait(Category.Name, Category.Process)]
    public async Task A_conversation_in_full_access_asked_for_a_new_repository_opens_it_at_once_with_no_card_and_keeps_what_it_made()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var scratch = await GrowthScratch.MadeAsync();
        var (pipe, client, nonce, driver, services, log) = await ServeAsync(press: CreateRoute(scratch.World));
        using var _ = services;
        using var __ = log;
        using var ___ = pipe;
        await using var ____ = client;
        var key = _delegates.Draw("h1");
        _modes["h1"] = HelpModes.Full;
        pipe.Grounds = CreateGrounds(scratch.World);

        var answer = await client.AnswerAsync(await SendProvedAsync(client, "REGISTRY_CREATE", Creates, "h1", key, nonce));

        Assert.True(answer.GetProperty("success").GetBoolean(), answer.GetRawText());
        Assert.True(answer.GetProperty("data").GetProperty("done").GetBoolean(), answer.GetRawText());
        Assert.Empty(pipe.Confirmations.Now().Waiting);
        Assert.Equal(1, driver.Ran("REGISTRY_CREATE"));
        Assert.Equal(("h1", $"{DriverModule.Name}.REGISTRY_CREATE"), Assert.Single(_said));
        await scratch.AssertMadeAsync();
        await UntilAsync(() => pipe.StandingConfirmation.Now().HelpPresses is [{ Outcome: HelpPressRecord.Done, Receipt: not null }]);
        var kept = pipe.StandingConfirmation.Now().HelpPresses.Single();
        Assert.Equal(("REGISTRY_CREATE", "h1", false), (kept.Route, kept.Session, kept.Asked));
        Assert.Equal(
            ("opened", "ledger", "aurora", scratch.Work("ledger"), "main", true),
            (kept.Receipt!.Kind, kept.Receipt.Repository, kept.Receipt.Workspace, kept.Receipt.Folder, kept.Receipt.Line, kept.Receipt.Done));
        Assert.Single(kept.Receipt.Quests);
        Assert.Contains(
            $$"""{"session":"h1","route":"{{DriverModule.Name}}.REGISTRY_CREATE","door":"terminal"}""", Logged(PipeDelegate.PressedEvent));
    }

    [Fact]
    [Trait(Category.Name, Category.Process)]
    public async Task A_conversation_in_restricted_mode_asked_for_a_new_repository_waits_on_a_card_naming_Ask_Daoris_and_opens_it_on_the_persons_yes()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var scratch = await GrowthScratch.MadeAsync();
        var (pipe, client, nonce, driver, services, log) = await ServeAsync(press: CreateRoute(scratch.World));
        using var _ = services;
        using var __ = log;
        using var ___ = pipe;
        await using var ____ = client;
        pipe.Grounds = CreateGrounds(scratch.World);
        pipe.Confirmations.Answered = (card, answer) => HelpAskedPress.Answered(pipe, card, answer);
        var key = _delegates.Draw("h1");
        _modes["h1"] = HelpModes.Restricted;

        var id = await SendProvedAsync(client, "REGISTRY_CREATE", Creates, "h1", key, nonce);
        await UntilAsync(() => pipe.Confirmations.Now().Waiting.Count == 1);
        var card = Assert.Single(pipe.Confirmations.Now().Waiting);

        // The card names Ask Daoris and the department it would open, with nothing made, said or run yet.
        Assert.Equal(("h1", true, "repository-create"), (card.Asker.Session, card.Asker.Help, card.View?.Kind));
        Assert.Equal(("ledger", "aurora", scratch.Work("ledger")), (card.View!.Facts["name"], card.View.Facts["workspace"], card.View.Facts["folder"]));
        Assert.Equal(0, driver.Ran("REGISTRY_CREATE"));
        Assert.False(Directory.Exists(scratch.Work("ledger")));
        Assert.Empty(scratch.World.Bodies);
        Assert.Empty(_said);
        Assert.Null(Assert.Single(pipe.StandingConfirmation.Now().HelpPresses).Receipt);

        // The person's yes runs it, and what it made is kept with the press.
        Assert.Equal(PipeAnswered.Answered, (await pipe.Confirmations.AnswerAsync(card.Id, confirm: true)).Outcome);
        var answer = await client.AnswerAsync(id);
        Assert.True(answer.GetProperty("success").GetBoolean(), answer.GetRawText());
        Assert.True(answer.GetProperty("data").GetProperty("done").GetBoolean(), answer.GetRawText());
        Assert.Equal(1, driver.Ran("REGISTRY_CREATE"));
        await scratch.AssertMadeAsync();
        await UntilAsync(() => pipe.StandingConfirmation.Now().HelpPresses is [{ Asked: true, Outcome: HelpPressRecord.Allowed, Receipt.Repository: "ledger" }]);
    }

    [Fact]
    [Trait(Category.Name, Category.Process)]
    public async Task A_creation_the_plan_refuses_never_reaches_a_card_and_makes_nothing()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var scratch = await GrowthScratch.MadeAsync();
        scratch.World.Registry.Add(new RepoView("Ledger", true, scratch.Work("Ledger"), "billing"));
        var (pipe, client, nonce, driver, services, log) = await ServeAsync(press: CreateRoute(scratch.World));
        using var _ = services;
        using var __ = log;
        using var ___ = pipe;
        await using var ____ = client;
        pipe.Grounds = CreateGrounds(scratch.World);
        var key = _delegates.Draw("h1");
        _modes["h1"] = HelpModes.Restricted;

        var refused = await client.AnswerAsync(await SendProvedAsync(client, "REGISTRY_CREATE", Creates, "h1", key, nonce));

        Assert.Equal(InstancePipe.NotAllowed, refused.GetProperty("error").GetProperty("code").GetString());
        Assert.Contains("`Ledger`", refused.GetProperty("error").GetProperty("message").GetString());
        Assert.Empty(pipe.Confirmations.Now().Waiting);
        Assert.Equal(0, driver.Ran("REGISTRY_CREATE"));
        Assert.False(Directory.Exists(scratch.Work("ledger")));
    }

    /// <summary>A scratch folder of the repository's own with two real checkouts of workspace <c>aurora</c> side by side, and the world that creates beside them.</summary>
    private sealed class GrowthScratch : IDisposable
    {
        private readonly string _root = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "pipe-delegate-growth", Guid.NewGuid().ToString("N")[..8]);

        private GrowthScratch()
        {
            World = new CreationWorld(Path.Combine(_root, "home"));
        }

        public CreationWorld World { get; }

        public string Work(params string[] parts) => Path.Combine([_root, "aurora", .. parts]);

        public static async Task<GrowthScratch> MadeAsync()
        {
            var scratch = new GrowthScratch();
            await GitSeed.PlainAsync(scratch.Work("atlas"), "atlas");
            await GitSeed.PlainAsync(scratch.Work("engine"), "engine");
            scratch.World.Registry.Add(new RepoView("atlas", false, scratch.Work("atlas"), "aurora"));
            scratch.World.Registry.Add(new RepoView("engine", true, scratch.Work("engine"), "aurora"));
            return scratch;
        }

        public void Dispose() => GitSeed.Remove(_root);

        /// <summary>The department is a real folder with one empty commit and no remote, registered unadopted, its set-up quest asked with the work chained behind it.</summary>
        public async Task AssertMadeAsync()
        {
            var folder = Work("ledger");
            Assert.Equal("1", (await GitFixture.GitAsync(folder, "rev-list", "--count", "HEAD")).Trim());
            Assert.Equal("Fixture|fixture@example.test", (await GitFixture.GitAsync(folder, "log", "-1", "--format=%an|%ae")).Trim());
            Assert.Contains("keeps the books", await GitFixture.GitAsync(folder, "log", "-1", "--format=%s"));
            Assert.Equal("", (await GitFixture.GitAsync(folder, "remote")).Trim());
            var body = Assert.Single(World.Bodies);
            Assert.Equal(("ledger", "aurora", false), ((string?)body["repository"], (string?)body["workspace"], (bool?)body["adopted"]));
            var (to, then) = Assert.Single(World.Published);
            Assert.Equal("ledger", to);
            Assert.NotNull(then);
            Assert.Contains("build the ledger's first report", then.Body);
        }
    }

    /// <summary>The conversation's stand-ins for a created place's world: the registry the registration door keeps, the identities, and the ask door.</summary>
    private sealed class CreationWorld(string home) : ICreateWorld, ISetupWorld
    {
        private readonly LayoutFacts _facts = new() { Line = "main", Commit = new string('a', 40) };

        public List<RepoView> Registry { get; } = [];

        public List<JsonObject> Bodies { get; } = [];

        public List<(string To, SetupThen? Then)> Published { get; } = [];

        public CreateDoors Doors() => new(
            this, this, home, true, () => DriverConfig.Empty with { Actors = StaffedRoles.On("claude-code-acp") }, _ => { }, Day);

        public Task<IReadOnlyList<RepoView>> RegistryAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<RepoView>>([.. Registry]);

        public bool LocalService => true;

        public DateTime CreatedAt(string root) => DateTime.UnixEpoch;

        public Task<string?> LineOfAsync(RepoView row, DriverConfig config, CancellationToken ct) => Task.FromResult<string?>(null);

        public Task<CreateAuthor?> AuthorOfAsync(RepoView row, CancellationToken ct) => Task.FromResult<CreateAuthor?>(Fixture);

        public string? RemoteStepOf(DriverConfig config, string repository, string workspace) => null;

        public Task<(bool Ok, string Message)> RegisterAsync(JsonObject body, CancellationToken ct)
        {
            Bodies.Add((JsonObject)body.DeepClone());
            Registry.Add(new RepoView((string)body["repository"]!, (bool)body["adopted"]!, (string?)body["root"], (string?)body["workspace"] ?? RemoteTarget.DefaultWorkspace)
            {
                Kind = (string)body["kind"]!,
            });
            return Task.FromResult((true, ""));
        }

        public IsolateStep? IsolationOf(DriverConfig config, RepoView repository) => IsolateStep.Trees;

        public Task<LineReading> ReadLineAsync(RepoView repository, DriverConfig config, CancellationToken ct) => Task.FromResult(new LineReading(_facts, null));

        public Task<SetupTools> ToolsAsync(CancellationToken ct) => Task.FromResult(new SetupTools("node", "v22.11.0", null, "daoris", "0.0.1", null));

        public Task<DoctrineReading> StatusAsync(RepoView repository, CancellationToken ct) => Task.FromResult(new DoctrineReading(new DoctrineStatus { Synced = true }, null));

        public Task<IReadOnlyList<QuestView>> QuestsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<QuestView>>([]);

        public Task<IReadOnlyList<string>> EntriesAsync(string repository, CancellationToken ct) => Task.FromResult<IReadOnlyList<string>>([]);

        public RepositoryDescription? Describe(string root) => null;

        public Task<AskAnswer> PublishAsync(string workspace, string sentence, string to, SetupThen? then, CancellationToken ct)
        {
            Published.Add((to, then));
            var id = $"q-{Published.Count}";
            return Task.FromResult(new AskAnswer(true, $"Asked as `#a{id}`.", $"a{id}", id));
        }
    }
}
