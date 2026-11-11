using System.Text.Json;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A session's archive, unarchive and delete as their presses' cards show them: judged by where the list places each session, refused
/// before any card where the list would keep it, and shown with its ids and the command it is.
/// </summary>
public sealed class SessionViewsTests
{
    private static StubGrounds List()
    {
        var grounds = new StubGrounds();
        grounds.Reads["SESSION_GROUPS"] = _ => new
        {
            sessions = new object[]
            {
                new { session = "s1", group = "ended", deletable = true },
                new { session = "s2", group = "ended", deletable = false },
                new { session = "s3", group = "working", deletable = false },
                new { session = "s4", group = "you", deletable = false },
                new { session = "s5", group = "review", deletable = false },
                new { session = "t/s6", group = "ended", teammate = true, deletable = false },
            },
        };
        return grounds;
    }

    private static Task<PressViewed> Archive(object payload) =>
        new SessionArchiveView().ViewAsync(List(), JsonSerializer.SerializeToElement(payload), CancellationToken.None);

    private static Task<PressViewed> Delete(object payload) =>
        new SessionDeleteView().ViewAsync(List(), JsonSerializer.SerializeToElement(payload), CancellationToken.None);

    [Fact]
    public async Task An_archive_of_ended_sessions_is_shown_with_their_ids_and_its_command()
    {
        var viewed = await Archive(new { ids = new[] { "s1", "t/s6" }, archived = true, said = true });

        Assert.Null(viewed.Refusal);
        Assert.Equal("session-archive", viewed.View!.Kind);
        Assert.Equal("s1, t/s6", viewed.View.Facts["sessions"]);
        Assert.Equal("2", viewed.View.Facts["count"]);
        Assert.Equal("daoris sessions archive s1 t/s6", viewed.View.Command);
    }

    [Fact]
    public async Task An_id_is_read_exactly_as_the_route_compares_it_so_a_hash_before_it_is_no_session()
    {
        Assert.Equal("there is no session `#s1` on this machine.", (await Archive(new { ids = new[] { "#s1" }, archived = true })).Refusal);
        Assert.Equal("there is no session `#s1` on this machine.", (await Delete(new { id = "#s1" })).Refusal);
    }

    [Fact]
    public async Task An_unarchive_is_shown_even_of_a_session_still_running()
    {
        var viewed = await Archive(new { ids = new[] { "s3" }, archived = false });

        Assert.Equal("session-unarchive", viewed.View!.Kind);
        Assert.Equal("daoris sessions unarchive s3", viewed.View.Command);
    }

    [Theory]
    [InlineData("s3", "`s3` is still running: stop it first.")]
    [InlineData("s4", "`s4` is waiting on you, and archive never hides what needs you.")]
    [InlineData("s5", "`s5` has work to review, and archive never hides what needs you.")]
    [InlineData("nope", "there is no session `nope` on this machine.")]
    public async Task An_archive_the_list_would_keep_is_refused_in_its_words(string id, string words)
    {
        var viewed = await Archive(new { ids = new[] { "s1", id }, archived = true });

        Assert.Null(viewed.View);
        Assert.Equal(words, viewed.Refusal);
    }

    [Fact]
    public async Task An_archive_naming_no_session_is_refused()
    {
        var viewed = await Archive(new { ids = Array.Empty<string>(), archived = true });

        Assert.Equal("a press names the sessions by id, as Sessions lists them.", viewed.Refusal);
    }

    [Fact]
    public async Task A_delete_of_a_session_the_list_offers_it_for_is_shown_with_its_command()
    {
        var viewed = await Delete(new { id = "s1" });

        Assert.Equal("session-delete", viewed.View!.Kind);
        Assert.Equal("s1", viewed.View.Facts["session"]);
        Assert.Equal("daoris sessions delete s1", viewed.View.Command);
    }

    [Theory]
    [InlineData("nope", "there is no session `nope` on this machine.")]
    [InlineData("t/s6", "session `t/s6` is a teammate's, so its record is theirs — archive it here instead.")]
    [InlineData("s3", "session `s3` is still running; stop it first.")]
    [InlineData("s2", "session `s2`'s record stays: it worked on a quest, or a tree or a landing still holds it — archive it instead.")]
    [InlineData("", "a delete names the session by its id, as Sessions lists it.")]
    public async Task A_delete_the_list_would_not_offer_is_refused_in_its_words(string id, string words)
    {
        var viewed = await Delete(new { id });

        Assert.Null(viewed.View);
        Assert.Equal(words, viewed.Refusal);
    }

    [Fact]
    public void The_routes_views_are_found_by_their_module_and_route()
    {
        Assert.IsType<SessionArchiveView>(PressViews.Find("daoris.driver", "SESSION_ARCHIVE"));
        Assert.IsType<SessionDeleteView>(PressViews.Find("daoris.driver", "SESSION_DELETE"));
    }
}
