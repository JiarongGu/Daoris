using System.Net;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// REVIEWENV1j (D154 point 3; the review environment design §1.4–§1.6, §3.6, D50): <c>daoris-driver quest review &lt;id&gt;
/// off|on|&lt;environment&gt;</c>, the terminal's twin of the chain's choice at the gate. <c>off</c> is the skip, through the verdict's
/// door; <c>on</c> or an environment's name is <i>Set it up</i>, through the set-up step's door, in the environment the
/// repository's rule declares. Each answers in the service's sentence, a refusal included. A service stood in: the fast half (MOD8).
/// </summary>
public sealed class QuestReviewChoiceTests
{
    private const string Published =
        "Published set-up step `#q3` to `web-app` on behalf of `ask #a1` — Open: it shows the work of `#q1` in `local` for your "
        + "review, and its done waits for your verdict.";

    public static TheoryData<string[], string> Problems => new()
    {
        { new[] { "review", "q1", "Dev" }, "`Dev`" },
        { new[] { "review", "q1", "prod" }, "`prod`" },
        { new[] { "review", "q1", "none" }, "`none`" },
        { new[] { "review", "q1", "later!" }, "`later!`" },
        { new[] { "review", "q1", "on", "it", "touches", "the", "page" }, "`on`" },
        { new[] { "review", "q1", "dev", "it", "touches", "the", "page" }, "`dev`" },
    };

    /// <summary>A bad value is a problem naming it, which the console prints with the usage; nothing is asked of the service.</summary>
    [Theory]
    [MemberData(nameof(Problems))]
    public void A_bad_value_is_a_problem_naming_it(string[] args, string named)
    {
        Assert.True(QuestReviewCommand.Asks(args));
        Assert.Null(QuestReviewCommand.Read(args, out var problem));
        Assert.Contains(named, problem);
    }

    /// <summary><c>off</c> is the skip (design §3.6), read as one with the person's words; <c>on</c> and a name are a set-up step.</summary>
    [Fact]
    public void It_reads_each_choice()
    {
        Assert.Equal(new QuestReviewAsk("q1", ReviewVerdicts.Skipped, "a readme change"),
            QuestReviewCommand.Read(["review", "#q1", "off", "a", "readme", "change"], out _));
        Assert.Equal(new QuestReviewAsk("q1", ReviewVerdicts.Skipped, null), QuestReviewCommand.Read(["review", "q1", "off"], out _));
        Assert.Equal(new QuestReviewAsk("q1", QuestReviewCommand.SetUpStep, null) { SetUpIn = "on" },
            QuestReviewCommand.Read(["review", "q1", "on"], out _));
        Assert.Equal(new QuestReviewAsk("q1", QuestReviewCommand.SetUpStep, null) { SetUpIn = "dev" },
            QuestReviewCommand.Read(["review", "q1", "dev"], out _));
    }

    /// <summary><c>off</c> goes to the verdict's door as the skip, naming no set-up, with the person's words.</summary>
    [Fact]
    public async Task Off_is_the_skip_through_the_verdicts_door()
    {
        var (service, heard) = Service(Work(), (_, _) => (HttpStatusCode.OK,
            """{"quest":{"id":"q1"},"message":"Skipped the review of quest `#q1`'s work: its landing waits for no review."}"""));
        var output = new StringWriter();

        var ask = QuestReviewCommand.Read(["review", "q1", "off", "a", "readme", "change"], out _)!;
        Assert.Equal(0, await QuestReviewCommand.RunAsync(ask, World(service), output));

        var (_, path, body) = heard.Single(each => each.Method == "POST");
        Assert.Equal("/api/quests/q1/review", path);
        using var sent = JsonDocument.Parse(body);
        Assert.Equal(("skipped", "a readme change"), (sent.RootElement.GetProperty("verdict").GetString(), sent.RootElement.GetProperty("words").GetString()));
        Assert.False(sent.RootElement.TryGetProperty("setUp", out _));
        Assert.Contains("daoris-driver: Skipped the review of quest `#q1`'s work: its landing waits for no review.\n", output.ToString().ReplaceLineEndings("\n"));
    }

    /// <summary>
    /// <c>on</c> is <i>Set it up</i> in the repository's default environment, the first its rule declares: the set-up step's door,
    /// as the gate's press calls it, and the service's sentence verbatim, then what the gate now waits for.
    /// </summary>
    [Fact]
    public async Task On_sets_it_up_in_the_rules_default_environment()
    {
        var (service, heard) = Service(Work(), (_, _) => (HttpStatusCode.OK, $$"""{"quest":{"id":"q3"},"message":"{{Published}}"}"""));
        var output = new StringWriter();

        Assert.Equal(0, await QuestReviewCommand.RunAsync(Choice("on"), World(service), output));

        var (_, path, body) = heard.Single(each => each.Method == "POST");
        Assert.Equal("/api/quests/q1/set-up-step", path);
        using var sent = JsonDocument.Parse(body);
        Assert.Equal("local", sent.RootElement.GetProperty("environment").GetString());
        Assert.Equal(
            $"daoris-driver: {Published}\n"
            + "daoris-driver: the gate: `web-app`'s work waits for set-up step `#q3` to show it in `local`, then for your look: "
            + "`daoris-driver quest review q3 reviewed` once you have looked.\n",
            output.ToString().ReplaceLineEndings("\n"));
    }

    /// <summary>An environment's name is <i>Set it up</i> in that environment, where the repository's rule declares it.</summary>
    [Fact]
    public async Task A_name_sets_it_up_in_that_environment()
    {
        var (service, heard) = Service(Work(), (_, _) => (HttpStatusCode.OK, $$"""{"quest":{"id":"q3"},"message":"{{Published}}"}"""));

        Assert.Equal(0, await QuestReviewCommand.RunAsync(Choice("dev"), World(service), new StringWriter()));

        using var sent = JsonDocument.Parse(heard.Single(each => each.Method == "POST").Body);
        Assert.Equal("dev", sent.RootElement.GetProperty("environment").GetString());
    }

    /// <summary>
    /// The door reads the rule (design §1.4): a name the repository does not declare is refused naming the repository and what it
    /// declares, and a repository that declares none is refused naming it, for <c>on</c> too. Nothing is sent.
    /// </summary>
    [Fact]
    public async Task An_environment_the_repository_does_not_declare_is_refused_naming_it()
    {
        var (service, heard) = Service(Work(), (_, _) => null);
        var output = new StringWriter();

        Assert.Equal(1, await QuestReviewCommand.RunAsync(Choice("staging"), World(service), output));
        Assert.Contains("`web-app` declares no review environment `staging`: it declares `local` or `dev`.", output.ToString());

        var (none, _) = Service(Work() with { To = "notes" }, (_, _) => null);
        Assert.Equal(1, await QuestReviewCommand.RunAsync(Choice("on"), World(none), output));
        Assert.Contains("`notes` declares no review environment", output.ToString());
        Assert.Contains("`daoris driver review notes <environment> --kind local|deployed --procedure <path>`", output.ToString());

        Assert.DoesNotContain(heard, each => each.Method == "POST");
    }

    /// <summary>A workspace's rule stands for a repository that sets none of its own (design §1.4, rows 5 and 6).</summary>
    [Fact]
    public async Task A_workspaces_rule_stands_for_a_repository_with_none_of_its_own()
    {
        var (service, heard) = Service(Work() with { To = "cart", Workspace = "shop" },
            (_, _) => (HttpStatusCode.OK, """{"quest":{"id":"q3"},"message":"Published set-up step `#q3`."}"""));

        Assert.Equal(0, await QuestReviewCommand.RunAsync(Choice("on"), World(service), new StringWriter()));

        using var sent = JsonDocument.Parse(heard.Single(each => each.Method == "POST").Body);
        Assert.Equal("preview", sent.RootElement.GetProperty("environment").GetString());
    }

    public static TheoryData<HttpStatusCode, string> Refusals => new()
    {
        // The exchange's own refusal: a set-up step follows a done quest.
        { HttpStatusCode.Conflict, "Quest `#q1` is Open: a set-up step shows work that is done, so it follows a done quest. Nothing was published." },
        // A host handed the person's key refuses a keyless terminal at the person's door (PERSONDOOR1a).
        {
            HttpStatusCode.Forbidden,
            "Only the person can give a set-up step, and this call carried no key of theirs. Nothing was kept. In Daoris's window, "
            + "press it there; from a terminal, run it again and confirm it in the window. An agent asks the person instead: "
            + "a go-ahead, or its closing note."
        },
    };

    /// <summary>A refusal is the service's sentence, passed through whole, and exit 1: never re-worded, and no gate line follows.</summary>
    [Theory]
    [MemberData(nameof(Refusals))]
    public async Task A_refusal_is_the_services_sentence_passed_through(HttpStatusCode status, string refusal)
    {
        var (service, _) = Service(Work(), (_, _) => (status, JsonSerializer.Serialize(new { error = refusal, code = "person-only" })));
        var output = new StringWriter();

        Assert.Equal(1, await QuestReviewCommand.RunAsync(Choice("on"), World(service), output));

        Assert.Equal($"daoris-driver: {refusal}\n", output.ToString().ReplaceLineEndings("\n"));
    }

    [Fact]
    public void The_usage_names_the_choice()
    {
        var usage = DriverCommand.Usage.ReplaceLineEndings("\n");
        Assert.Contains("\n  quest review <id> off [\"…\"]  ·  quest review <id> on|<environment>\n", usage);
        Assert.Contains("daoris-driver quest review <id> off [\"…\"]", QuestReviewCommand.Usage);
        Assert.Contains("daoris-driver quest review <id> on|<environment>", QuestReviewCommand.Usage);
    }

    /// <summary>The console hands the world this machine's review rule, which the set-up's door reads.</summary>
    [Fact]
    public void The_console_hands_the_rule_to_the_door()
    {
        var console = File.ReadAllText(Path.Combine(
            Daoris.Driver.Tests.HelpProposalKindsTests.RepositoryRoot(), "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Host", "QuestReviewConsole.cs"));
        Assert.Contains("Config = config,", console);
    }

    private static QuestView Work() => new("q1", "ask #a1", "web-app", "Add the compare setting", "", "Done");

    private static QuestReviewAsk Choice(string choice) => QuestReviewCommand.Read(["review", "q1", choice], out _)!;

    private static QuestReviewWorld World(ServiceClient service) => new(service) { Config = ReviewChoiceStandIn.Config() };

    /// <summary>A service holding <paramref name="quest"/>, answering each post by <paramref name="post"/>.</summary>
    private static (ServiceClient Service, List<(string Method, string Path, string Body)> Heard) Service(
        QuestView quest, Func<string, string, (HttpStatusCode Status, string Body)?> post)
    {
        var json = JsonSerializer.Serialize(new { id = quest.Id, from = quest.From, to = quest.To, title = quest.Title, body = "", status = quest.Status, workspace = quest.Workspace });
        return ReviewChoiceStandIn.Of((method, path, body) => (method, path) switch
        {
            ("GET", "/api/quests") => (HttpStatusCode.OK, $"[{json}]"),
            ("POST", _) => post(path, body),
            _ => null,
        });
    }
}
