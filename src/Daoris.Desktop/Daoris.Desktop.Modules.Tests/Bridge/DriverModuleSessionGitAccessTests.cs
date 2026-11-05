using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A running session's git access over the bridge (D241 point 7): the driver's state answers each session's record as it holds now,
/// its repository's access narrowed by what the person tightened, and the route that narrows it is a marked person's press on a session.
/// </summary>
public sealed class DriverModuleSessionGitAccessTests : DriverModuleBridge
{
    [Fact]
    public async Task The_state_answers_what_each_session_holds_and_follows_its_repository_where_nothing_was_tightened()
    {
        GitSessionAccess.Begin(Home, "s1a2b3c4", "engine", "shop");
        var module = Module();

        var state = await AnswerAsync(module, "SET_GIT_ACCESS", new { repository = "engine", access = new { write = "ask" } });

        var record = Assert.Single(state.GetProperty("sessionGitAccess").EnumerateArray());
        Assert.Equal("s1a2b3c4", record.GetProperty("session").GetString());
        Assert.Equal("engine", record.GetProperty("repository").GetString());
        Assert.Equal(
            """[{"class":"read","value":"allow","source":"default"},{"class":"write","value":"ask","source":"repository"},{"class":"dangerous","value":"deny","source":"default"}]""",
            record.GetProperty("classes").GetRawText());
    }

    [Fact]
    public async Task A_tightened_class_is_answered_from_the_session_and_a_later_loosening_of_the_repository_does_not_lift_it()
    {
        GitSessionAccess.Begin(Home, "s1a2b3c4", "engine", "shop");
        GitSessionAccess.Tighten(Home, "s1a2b3c4", new Dictionary<string, string> { ["read"] = "ask" });
        var module = Module();

        var state = await AnswerAsync(module, "SET_GIT_ACCESS", new { repository = "engine", access = new { read = "allow" } });

        var read = Assert.Single(state.GetProperty("sessionGitAccess").EnumerateArray()).GetProperty("classes").EnumerateArray().First();
        Assert.Equal("""{"class":"read","value":"ask","source":"session"}""", read.GetRawText());
    }
}
