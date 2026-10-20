using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// MIXAGENT2a-ii (D163 point 2): the helpers' files are the contract between the service's <c>HelperBox</c> and the driver's
/// reader, which share no code, so each side holds the shape (the service's suite holds the same text from its side). The ask is
/// read as <c>helper_ask</c> writes it, the state is written as the box reads it, and the answer is read as <c>helper_answer</c>
/// writes it.
/// </summary>
public sealed class HelperFilesTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-helper-files-" + Guid.NewGuid().ToString("N")[..8]);

    public HelperFilesTests() => Directory.CreateDirectory(DaorisHome.Entry(_home, HomeEntry.Helpers));

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private void Put(string name, string text) => File.WriteAllText(DaorisHome.Entry(_home, HomeEntry.Helpers,name), text);

    /// <summary>An ask as the connector writes it, indented, `agent` null or a word, and the person's door as `person`; oldest first.</summary>
    [Fact]
    public void An_ask_is_read_as_helper_ask_writes_it_oldest_first_and_an_unreadable_one_is_none()
    {
        Put("b2.ask.json", "{\n  \"id\": \"b2\",\n  \"asker\": \"s1\",\n  \"question\": \"Second?\",\n  \"agent\": \"codex-acp\",\n  \"askedBy\": \"person\",\n  \"at\": \"2026-10-12T10:05:00.0000000+00:00\"\n}\n");
        Put("a1.ask.json", "{\n  \"id\": \"a1\",\n  \"asker\": \"s1\",\n  \"question\": \"First?\",\n  \"agent\": null,\n  \"askedBy\": \"agent\",\n  \"at\": \"2026-10-12T10:00:00.0000000+00:00\"\n}\n");
        Put("c3.ask.json", "not json");
        Put("d4.ask.json", "{ \"id\": \"d4\", \"question\": \"no asker\" }");

        var asks = new HelperFiles(_home).Asks();

        Assert.Equal(["a1", "b2"], asks.Select(ask => ask.Id));
        Assert.Equal(("s1", "First?", null, HelperAsk.ByAgent), (asks[0].Asker, asks[0].Question, asks[0].Agent, asks[0].AskedBy));
        Assert.Equal(("Second?", "codex-acp", HelperAsk.ByPerson), (asks[1].Question, asks[1].Agent, asks[1].AskedBy));
    }

    /// <summary>The state is written whole and atomically as the box reads it, with the driver's own marks beside, and read back.</summary>
    [Fact]
    public void A_state_is_written_in_the_shape_the_service_reads_and_reads_back_with_its_marks()
    {
        var files = new HelperFiles(_home);
        var at = DateTimeOffset.Parse("2026-10-12T10:00:00Z");

        files.WriteState(new HelperState("a1", HelperStates.Waiting, "cap", null, at));
        files.WriteState(new HelperState("a2", HelperStates.Answered, null, "h1", at) { LastWords = true, Handed = "w1", Agent = "codex-acp", Words = "Probably." });

        var text = File.ReadAllText(DaorisHome.Entry(_home, HomeEntry.Helpers,"a1.state.json"));
        Assert.Equal("{\n  \"id\": \"a1\",\n  \"state\": \"waiting\",\n  \"code\": \"cap\",\n  \"helper\": null,\n  \"at\": \"2026-10-12T10:00:00.0000000+00:00\"\n}\n", text);
        Assert.DoesNotContain("\r", text);
        var kept = files.ReadState("a2")!;
        Assert.Equal((HelperStates.Answered, "h1", true, "w1", "codex-acp", "Probably."), (kept.State, kept.Helper, kept.LastWords, kept.Handed, kept.Agent, kept.Words));
        Assert.Null(files.ReadState("a3"));

        Put("a4.state.json", "{ \"id\": \"a4\", \"state\": \"sleeping\" }");
        Assert.Null(files.ReadState("a4"));
        Assert.Empty(Directory.GetFiles(DaorisHome.Entry(_home, HomeEntry.Helpers), "*.writing"));
    }

    /// <summary>
    /// 🔴 COWORK1d (the message-urgency design §9): an ask `helper_ask` wrote urgent (the service's suite, <c>HelperBoxShapeTests</c>,
    /// holds the same text) reads urgent; an ask with no level, every ask from before among them, reads ordinary, today's delivery.
    /// </summary>
    [Fact]
    public void An_urgent_ask_reads_urgent_and_one_with_no_level_reads_ordinary()
    {
        Put("u1.ask.json", "{\n  \"id\": \"u1\",\n  \"asker\": \"s1\",\n  \"question\": \"Why?\",\n  \"agent\": null,\n  \"askedBy\": \"agent\",\n  \"urgent\": true,\n  \"at\": \"2026-10-12T10:00:00.0000000\\u002B00:00\"\n}\n");
        Put("o1.ask.json", "{\n  \"id\": \"o1\",\n  \"asker\": \"s1\",\n  \"question\": \"Where?\",\n  \"agent\": null,\n  \"askedBy\": \"agent\",\n  \"at\": \"2026-10-12T10:01:00.0000000+00:00\"\n}\n");

        var asks = new HelperFiles(_home).Asks();

        Assert.Equal([("u1", true), ("o1", false)], asks.Select(ask => (ask.Id, ask.Urgent)));
    }

    [Fact]
    public void An_answer_is_read_as_helper_answer_writes_it_and_the_persons_ask_is_written_as_helper_ask_does()
    {
        Put("a1.answer.json", "{\n  \"id\": \"a1\",\n  \"helper\": \"h1\",\n  \"answer\": \"Line 41, once.\",\n  \"at\": \"2026-10-12T10:09:00.0000000+00:00\"\n}\n");
        var files = new HelperFiles(_home);

        var answer = files.ReadAnswer("a1")!;
        Assert.Equal(("h1", "Line 41, once."), (answer.Helper, answer.Answer));
        Assert.Null(files.ReadAnswer("a9"));

        var id = files.WriteAsk("s1", "Why?", "codex-acp", DateTimeOffset.Parse("2026-10-12T10:10:00Z"));
        var ask = Assert.Single(files.Asks(), each => each.Id == id || each.Question == "Why?");
        Assert.Equal(("s1", "Why?", "codex-acp", HelperAsk.ByPerson), (ask.Asker, ask.Question, ask.Agent, ask.AskedBy));
    }
}
