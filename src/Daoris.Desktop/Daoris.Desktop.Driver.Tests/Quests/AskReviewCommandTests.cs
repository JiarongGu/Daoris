using System.Net;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// REVIEWENV1j (D154 point 3; the review environment design §1.4–§1.5, D50): the ask's review choice at a terminal, the twin of
/// the composer's and the ask page's. <c>daoris-driver ask … --review rule|on|&lt;environment&gt;|off</c> sends it with a new ask,
/// <c>rule</c> sending none, as the composer's default does; <c>daoris-driver ask --set-review &lt;id&gt; on|&lt;environment&gt;|off</c>
/// sets it on an ask, as its page's <i>Set</i> and <i>Apply</i> do. A named environment is one the ask's workspace declares, as the
/// page offers only those; every other answer is the service's sentence. A service stood in: the fast half (MOD8).
/// </summary>
public sealed class AskReviewCommandTests
{
    private const string Chosen = "Ask `#a1b2c3`: no review for the chains it publishes that choose none. Your words are kept with it.";

    public static TheoryData<string?, string?, string?, string?> Composed => new()
    {
        // Nothing said, and `rule`, send nothing: each repository's rule decides, as the composer's default.
        { null, null, null, null },
        { "rule", null, null, null },
        { "on", null, "on", null },
        { "off", "  a readme change ", "off", "a readme change" },
        { "dev", null, "dev", null },
    };

    /// <summary>Each choice the composer offers, read as the composer sends it: <c>rule</c> is none, and words go with a choice.</summary>
    [Theory]
    [MemberData(nameof(Composed))]
    public void The_composers_choice_is_read_as_it_sends_it(string? review, string? words, string? choice, string? kept)
    {
        Assert.Equal(new AskReviewComposed(choice, kept), AskReviewCommand.Compose(review, words, out var problem));
        Assert.Null(problem);
    }

    public static TheoryData<string?, string?, string> ComposeProblems => new()
    {
        { "Dev", null, "`Dev`" },
        { "prod", null, "`prod`" },
        { "none", null, "`none`" },
        { "", null, "`--review`" },
        { null, "a readme change", "--review-words" },
        { "rule", "a readme change", "--review-words" },
    };

    /// <summary>A bad value is a problem naming it, which the console prints with the usage, before the service is asked.</summary>
    [Theory]
    [MemberData(nameof(ComposeProblems))]
    public void A_bad_value_with_a_new_ask_is_a_problem(string? review, string? words, string named)
    {
        Assert.Null(AskReviewCommand.Compose(review, words, out var problem));
        Assert.Contains(named, problem);
    }

    /// <summary>The ask's door hears the choice and the words as the composer sends them, and nothing where none was chosen.</summary>
    [Fact]
    public async Task A_new_ask_carries_the_choice_to_the_asks_door()
    {
        var (service, heard) = ReviewChoiceStandIn.Of((method, path, _) => (method, path) is ("POST", "/api/asks")
            ? (HttpStatusCode.OK, """{"ask":{"id":"a1b2c3"},"message":"Asked as `#a1b2c3` in `default`."}""")
            : null);

        var answer = await service.AskAsync("default", "add the compare setting", [], [], null, review: "dev", reviewWords: "it touches the page");
        Assert.True(answer.Ok);
        await service.AskAsync("default", "add the compare setting", [], [], null);

        using var chosen = JsonDocument.Parse(heard[0].Body);
        Assert.Equal(("dev", "it touches the page"),
            (chosen.RootElement.GetProperty("review").GetString(), chosen.RootElement.GetProperty("reviewWords").GetString()));
        using var none = JsonDocument.Parse(heard[1].Body);
        Assert.False(none.RootElement.TryGetProperty("review", out _));
        Assert.False(none.RootElement.TryGetProperty("reviewWords", out _));
    }

    /// <summary>
    /// A named environment is one the ask's workspace declares: its own rule's, or a rule of a repository in it, as the page
    /// offers. <c>on</c> and <c>off</c> need no rule read, so nothing is read for them.
    /// </summary>
    [Fact]
    public async Task A_named_environment_is_one_the_workspace_declares()
    {
        var (service, heard) = ReviewChoiceStandIn.Of((method, path, _) => (method, path) is ("GET", "/api/registry")
            ? (HttpStatusCode.OK, """[{"repository":"web-app","workspace":"default"},{"repository":"cart","workspace":"shop"}]""")
            : null);
        var world = World(service);

        Assert.Null(await AskReviewCommand.UndeclaredAsync("on", "default", world));
        Assert.Null(await AskReviewCommand.UndeclaredAsync("off", "default", world));
        Assert.Empty(heard);

        Assert.Null(await AskReviewCommand.UndeclaredAsync("dev", "default", world));
        Assert.Null(await AskReviewCommand.UndeclaredAsync("preview", "shop", world));
        Assert.Equal("No review environment `preview` is declared in workspace `default`: its rules declare `local` or `dev`. Nothing was kept.",
            await AskReviewCommand.UndeclaredAsync("preview", "default", world));
        Assert.Contains("No review environment is declared in workspace `home`", await AskReviewCommand.UndeclaredAsync("dev", "home", world));
    }

    /// <summary>The ask page's choice: the ask's id, then the choice, then the person's words, joined as said.</summary>
    [Fact]
    public void It_reads_the_choice_on_an_ask()
    {
        Assert.Equal(new AskReviewAsk("a1b2c3", "off", "a readme change"), AskReviewCommand.Read("#a1b2c3", ["off", "a", "readme", "change"], out _));
        Assert.Equal(new AskReviewAsk("a1b2c3", "on", null), AskReviewCommand.Read("a1b2c3", ["on"], out _));
        Assert.Equal(new AskReviewAsk("a1b2c3", "dev", null), AskReviewCommand.Read("a1b2c3", ["dev"], out _));
    }

    public static TheoryData<string, string[], string> SetProblems => new()
    {
        { "a1b2c3", [], "on" },
        { "a1b2c3", ["rule"], "`rule`" },
        { "a1b2c3", ["Dev"], "`Dev`" },
        { "a1b2c3", ["production"], "`production`" },
        { "--yes", ["on"], "id" },
    };

    /// <summary>A bad value on an ask is a problem naming it, which the console prints with the usage.</summary>
    [Theory]
    [MemberData(nameof(SetProblems))]
    public void A_bad_value_on_an_ask_is_a_problem(string id, string[] words, string named)
    {
        Assert.Null(AskReviewCommand.Read(id, words, out var problem));
        Assert.Contains(named, problem);
    }

    /// <summary>The ask page's press: its review door, with the choice and the words, and the service's sentence verbatim.</summary>
    [Fact]
    public async Task The_choice_on_an_ask_goes_to_its_review_door()
    {
        var (service, heard) = Asks((_, _) => (HttpStatusCode.OK, $$"""{"ask":{"id":"a1b2c3"},"message":"{{Chosen}}"}"""));
        var output = new StringWriter();

        Assert.Equal(0, await AskReviewCommand.RunAsync(new AskReviewAsk("a1b2c3", "off", "a readme change"), World(service), output));

        var (_, path, body) = heard.Single(each => each.Method == "POST");
        Assert.Equal("/api/asks/a1b2c3/review", path);
        using var sent = JsonDocument.Parse(body);
        Assert.Equal(("off", "a readme change"), (sent.RootElement.GetProperty("choice").GetString(), sent.RootElement.GetProperty("words").GetString()));
        Assert.Equal($"{Chosen}\n", output.ToString().ReplaceLineEndings("\n"));
    }

    /// <summary>A named environment its workspace declares is sent; one it does not is refused here, and nothing is sent.</summary>
    [Fact]
    public async Task A_name_on_an_ask_is_judged_against_its_workspace()
    {
        var (service, heard) = Asks((_, _) => (HttpStatusCode.OK, """{"ask":{"id":"a1b2c3"},"message":"Ask `#a1b2c3`: reviewed in `dev`."}"""));

        Assert.Equal(0, await AskReviewCommand.RunAsync(new AskReviewAsk("a1b2c3", "dev", null), World(service), new StringWriter()));
        var output = new StringWriter();
        Assert.Equal(1, await AskReviewCommand.RunAsync(new AskReviewAsk("a1b2c3", "preview", null), World(service), output));

        Assert.Single(heard, each => each.Method == "POST");
        Assert.Contains("No review environment `preview` is declared in workspace `default`", output.ToString());
    }

    public static TheoryData<HttpStatusCode, string> Refusals => new()
    {
        { HttpStatusCode.Conflict, "Ask `#a1b2c3` is closed (answered elsewhere): nothing it publishes waits for a review." },
        {
            HttpStatusCode.Forbidden,
            "Only the person can give an ask's review choice, and this call carried no key of theirs. Nothing was kept. In Daoris's "
            + "window, press it there; from a terminal, run it again and confirm it in the window. An agent asks the person instead: "
            + "a go-ahead, or its closing note."
        },
    };

    /// <summary>A refusal is the service's sentence, passed through whole, and exit 1.</summary>
    [Theory]
    [MemberData(nameof(Refusals))]
    public async Task A_refusal_is_the_services_sentence_passed_through(HttpStatusCode status, string refusal)
    {
        var (service, _) = Asks((_, _) => (status, JsonSerializer.Serialize(new { error = refusal })));
        var output = new StringWriter();

        Assert.Equal(1, await AskReviewCommand.RunAsync(new AskReviewAsk("a1b2c3", "on", null), World(service), output));

        Assert.Equal($"{refusal}\n", output.ToString().ReplaceLineEndings("\n"));
    }

    [Fact]
    public void The_usage_names_both_forms()
    {
        var usage = DriverCommand.Usage.ReplaceLineEndings("\n");
        Assert.Contains("\n  ask … --review rule|on|<environment>|off [--review-words \"…\"]\n", usage);
        Assert.Contains("\n  ask --set-review <id> on|<environment>|off [\"…\"]\n", usage);
    }

    /// <summary>
    /// The console reads both flags into the library's judgement, prints a problem with the usage, and names both forms in its own
    /// usage: the console is the host's, which no test runs, so it is read as text.
    /// </summary>
    [Fact]
    public void The_console_reads_both_forms()
    {
        var console = File.ReadAllText(Path.Combine(
            Daoris.Driver.Tests.HelpProposalKindsTests.RepositoryRoot(), "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Host", "AskConsole.cs"));
        Assert.Contains("case \"--review\": review = Value(); break;", console);
        Assert.Contains("case \"--review-words\": reviewWords = Value(); break;", console);
        Assert.Contains("case \"--set-review\": setReview = Value(); break;", console);
        Assert.Contains("AskReviewCommand.Compose(review, reviewWords, out var reviewProblem)", console);
        Assert.Contains("AskReviewCommand.Read(setReview, words, out var setProblem)", console);
        Assert.Contains("daoris-driver ask --set-review <id> on|<environment>|off [\\\"…\\\"]", console);
    }

    private static AskReviewWorld World(ServiceClient service) => new(service, () => ReviewChoiceStandIn.Config());

    /// <summary>A service holding ask <c>a1b2c3</c> in <c>default</c> and the registry, answering each post by <paramref name="post"/>.</summary>
    private static (ServiceClient Service, List<(string Method, string Path, string Body)> Heard) Asks(
        Func<string, string, (HttpStatusCode Status, string Body)?> post) =>
        ReviewChoiceStandIn.Of((method, path, body) => (method, path) switch
        {
            ("GET", "/api/asks/a1b2c3") => (HttpStatusCode.OK,
                """{"id":"a1b2c3","workspace":"default","sentence":"add the compare setting","state":"Open","tier":"declarations"}"""),
            ("GET", "/api/registry") => (HttpStatusCode.OK, """[{"repository":"web-app","workspace":"default"}]"""),
            ("POST", _) => post(path, body),
            _ => null,
        });
}
