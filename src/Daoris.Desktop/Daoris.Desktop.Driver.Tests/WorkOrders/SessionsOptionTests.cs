using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>daoris session option &lt;id&gt; &lt;option&gt; &lt;value&gt;</c>, the twin of a conversation page's option picker: its words read
/// into the page's press, sent on <c>SET_SESSION_OPTION</c> so the route judges it, its answer said in one line; with no Daoris
/// running nothing holds the conversation, so nothing is set.
/// </summary>
public sealed class SessionsOptionTests
{
    private static SessionsAsk Read(params string[] words)
    {
        var ask = SessionsCommand.Read(words, out var problem);
        Assert.Null(problem);
        return ask!;
    }

    [Fact]
    public void Its_words_read_into_the_page_s_press_on_the_route_the_census_twins()
    {
        var ask = Read("option", "s1a2b3c4", "model", "opus", "4.7");

        Assert.Equal(("option", "s1a2b3c4", "model", "opus 4.7"), (ask.Verb, ask.Ids[0], ask.Word, ask.Text));
        Assert.Equal(
            new Dictionary<string, object?> { ["id"] = "s1a2b3c4", ["option"] = "model", ["value"] = "opus 4.7", ["said"] = true }, SessionsOption.Body(ask));
        Assert.Equal(("SET_SESSION_OPTION", RouteClass.Person), (PersonPresses.SessionOption.Route, PersonPresses.SessionOption.Class));
        Assert.Contains(PersonPresses.Presses, press => press.Route == PersonPresses.SessionOption && press.Verb == "sessions option <id> <option> <value>");
        Assert.Contains("session option <id> <option> <value>", SessionsCommand.Usage);
    }

    [Theory]
    [InlineData("option")]
    [InlineData("option", "s1")]
    [InlineData("option", "s1", "model")]
    [InlineData("option", "s1", "model", " ")]
    public void A_press_naming_no_value_is_a_usage_problem(params string[] words)
    {
        Assert.Null(SessionsCommand.Read(words, out var problem));
        Assert.Contains("`option` takes one session's id, the option's id, then the value", problem);
    }

    [Fact]
    public void The_route_s_answer_is_said_as_the_option_now_stands()
    {
        var ask = Read("option", "s1", "effort", "high");
        using var answer = JsonDocument.Parse("""{"session":"s1","options":[{"id":"model","current":"opus"},{"id":"effort","current":"high"}]}""");
        var said = new StringWriter();

        Assert.Equal(0, SessionsOption.Said(answer.RootElement, ask, said, TextWriter.Null));
        Assert.Equal("session: `effort` of s1 is now `high`.", said.ToString().Trim());
    }

    [Fact]
    public void An_answer_naming_the_option_nowhere_is_a_form_the_terminal_does_not_read()
    {
        var ask = Read("option", "s1", "effort", "high");
        using var answer = JsonDocument.Parse("""{"session":"s1","options":[]}""");
        var error = new StringWriter();

        Assert.Equal(2, SessionsOption.Said(answer.RootElement, ask, TextWriter.Null, error));
        Assert.Contains("names no option `effort`", error.ToString());
    }

    [Fact]
    public async Task With_no_Daoris_running_nothing_is_set()
    {
        var home = Path.Combine(TestHomes.Root, "daoris-session-option-" + Guid.NewGuid().ToString("N")[..8]);
        var world = new SessionsWorld(new OpinionStandIn().Client(), home, DriverConfig.Empty, false, null);
        var said = new StringWriter();

        Assert.Equal(1, await SessionsCommand.RunAsync(Read("option", "s1", "effort", "high"), world, said));
        Assert.Equal(SessionsOption.Alone, said.ToString().Trim());
        Assert.False(Directory.Exists(home));
    }
}
