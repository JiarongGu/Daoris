using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// The <c>session</c> kind's writer (SESSUX1h2a, D161's SESSUX1h1 note): sessions archived or unarchived by id, or
/// every ended one archived, as the terminal's <c>sessions archive</c> does; and the <c>delete</c> kind's <c>session</c> door.
/// </summary>
public sealed class HelpSessionProposalTests : HelpProposalBoxFixture
{
    [Theory]
    [InlineData("archive")]
    [InlineData("unarchive")]
    public void Sessions_are_written_by_id_comma_joined(string door)
    {
        var (id, message) = Box().ProposeSession(door, " s1, s2 ,s3 ", false, "the person is done with them", "h1", Now);

        var written = Written(id!);
        Assert.Equal(
            ("session", door, "s1,s2,s3"),
            (written.GetProperty("kind").GetString(), written.GetProperty("door").GetString(), written.GetProperty("target").GetString()));
        Assert.Equal(System.Text.Json.JsonValueKind.Null, written.GetProperty("value").ValueKind);
        Assert.Equal("the person is done with them", written.GetProperty("why").GetString());
        Assert.Contains($"Proposed `#{id}`", message);
    }

    [Fact]
    public void The_ended_ones_are_written_with_no_target_and_the_value_ended()
    {
        var (id, _) = Box().ProposeSession("archive", null, true, "tidy up", "h1", Now);

        var written = Written(id!);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, written.GetProperty("target").ValueKind);
        Assert.Equal("ended", written.GetProperty("value").GetString());
        Assert.Equal("archive", written.GetProperty("door").GetString());
    }

    [Theory]
    [InlineData("rename", "s1", false, "a reason", "archive, unarchive, helper or pass")]
    [InlineData("archive", null, false, "a reason", "name exactly one")]
    [InlineData("archive", "s1", true, "a reason", "name exactly one")]
    [InlineData("unarchive", null, true, "a reason", "only with archive")]
    [InlineData("archive", "s1,,s2", false, "a reason", "an empty one")]
    [InlineData("archive", "C:/x/s1", false, "a reason", "never by its path")]
    [InlineData("archive", "s1", false, " ", "needs its reason")]
    public void A_malformed_session_proposal_is_refused_with_nothing_written(string door, string? sessions, bool ended, string why, string says)
    {
        var (id, message) = Box().ProposeSession(door, sessions, ended, why, "h1", Now);

        Assert.Null(id);
        Assert.Contains(says, message);
        Assert.Contains("Nothing was proposed", message);
        Assert.False(Directory.Exists(DaorisHome.Entry(_home, HomeEntry.HelpProposals)));
    }

    [Fact]
    public void A_helper_door_is_written_with_its_question_its_agent_the_ask_it_follows_and_whom_the_answer_is_for()
    {
        var (id, message) = Box().ProposeSession(
            "helper", "s1", false, "the person wants a second reading", "h1", Now, new SessionHelperProposal(" Why? ", "codex-acp", "#a1b2c3d4", true));

        var written = Written(id!);
        Assert.Equal(
            ("session", "helper", "s1", "Why?", "codex-acp", "a1b2c3d4", true),
            (written.GetProperty("kind").GetString(), written.GetProperty("door").GetString(), written.GetProperty("target").GetString(),
                written.GetProperty("value").GetString(), written.GetProperty("agent").GetString(), written.GetProperty("ask").GetString(),
                written.GetProperty("toMe").GetBoolean()));
        Assert.Contains($"Proposed `#{id}`", message);
    }

    [Fact]
    public void A_pass_door_is_written_with_the_ask_to_pass_and_nothing_else()
    {
        var (id, _) = Box().ProposeSession("pass", "s1", false, "the person read it", "h1", Now, new SessionHelperProposal(null, null, "a1b2c3d4", false));

        var written = Written(id!);
        Assert.Equal(("pass", "a1b2c3d4"), (written.GetProperty("door").GetString(), written.GetProperty("ask").GetString()));
        Assert.Equal(System.Text.Json.JsonValueKind.Null, written.GetProperty("value").ValueKind);
    }

    [Theory]
    [InlineData("helper", "s1,s2", "Why?", null, null, false, "exactly one session")]
    [InlineData("helper", "s1", "  ", null, null, false, "none was given")]
    [InlineData("helper", "s1", "Why?", "two words", null, false, "is one word")]
    [InlineData("helper", "s1", "Why?", null, "a 1", false, "is one word")]
    [InlineData("pass", "s1", null, null, null, false, "names the ask to pass")]
    [InlineData("pass", "s1", "Why?", null, "a1", false, "the session and the ask alone")]
    [InlineData("pass", "s1", null, null, "a1", true, "the session and the ask alone")]
    [InlineData("archive", "s1", "Why?", null, null, false, "the helper and pass doors take a question")]
    public void A_malformed_helper_or_pass_proposal_is_refused_with_nothing_written(
        string door, string sessions, string? question, string? agent, string? ask, bool toMe, string says)
    {
        var (id, message) = Box().ProposeSession(door, sessions, false, "a reason", "h1", Now, new SessionHelperProposal(question, agent, ask, toMe));

        Assert.Null(id);
        Assert.Contains(says, message);
        Assert.False(Directory.Exists(DaorisHome.Entry(_home, HomeEntry.HelpProposals)));
    }

    /// <summary>A teammate's session is <c>&lt;origin&gt;/&lt;id&gt;</c> (SYNC4), and §5.2 lets it be archived here.</summary>
    [Fact]
    public void A_teammates_session_id_is_written_as_it_is_listed()
    {
        var (id, message) = Box().ProposeSession("archive", "machine-b/s1,s2", false, "a reason", "h1", Now);

        Assert.Contains("Proposed", message);
        Assert.Equal("machine-b/s1,s2", Written(id!).GetProperty("target").GetString());
    }

    [Theory]
    [InlineData("/x/s1")]
    [InlineData("C:/x/s1")]
    [InlineData("a/b/c")]
    [InlineData("a/")]
    [InlineData("/b")]
    [InlineData("a\\b")]
    [InlineData("a/b\\c")]
    public void A_path_is_still_refused_where_a_teammates_id_is_not(string path)
    {
        var (id, message) = Box().ProposeSession("archive", path, false, "a reason", "h1", Now);

        Assert.Null(id);
        Assert.Contains("never by its path", message);
    }

    [Fact]
    public void Ids_with_a_space_inside_one_are_refused()
    {
        var (id, message) = Box().ProposeSession("archive", "s1,s 2", false, "a reason", "h1", Now);

        Assert.Null(id);
        Assert.Contains("one word", message);
    }

    [Fact]
    public void A_session_delete_is_written_with_its_id()
    {
        var (id, _) = Box().ProposeDelete(null, null, "s9", "a test run", "h1", Now);

        var written = Written(id!);
        Assert.Equal(
            ("delete", "session", "s9"),
            (written.GetProperty("kind").GetString(), written.GetProperty("door").GetString(), written.GetProperty("target").GetString()));
    }

    [Theory]
    [InlineData("q1", null, "s1")]
    [InlineData(null, "a1", "s1")]
    [InlineData("q1", "a1", null)]
    public void A_delete_names_exactly_one_of_quest_ask_or_session(string? quest, string? ask, string? session)
    {
        var (id, message) = Box().ProposeDelete(quest, ask, session, "a reason", "h1", Now);

        Assert.Null(id);
        Assert.Contains("name exactly one", message);
    }
}
