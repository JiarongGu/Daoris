using System.Net;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 mod-x, the Workplace-names-Course cut: the behaviour the cut keeps, written against the surface that exists on both sides of
/// it (the literal historical bytes, the rebase key's text, the press's values and refusals, the land press's told note), so the same
/// cases pass on the base and after it.
/// </summary>
public sealed class WorkplaceCourseBoundaryTests : IDisposable
{
    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "workplace-course", Guid.NewGuid().ToString("N"));
    private string Sessions => DaorisHome.Entry(_home, HomeEntry.Sessions);
    private string Requests => DaorisHome.Entry(_home, HomeEntry.Requests);
    private string ChatTree => DaorisHome.Entry(_home, HomeEntry.Trees, "default", "engine", "s1");

    public WorkplaceCourseBoundaryTests() => Directory.CreateDirectory(_home);
    public void Dispose() => GitSeed.Remove(_home);

    [Fact]
    public void Course_suffix_constants_keep_their_literal_historical_bytes()
    {
        Assert.Equal(".cannot.json", GoOnMarks.Suffix);
        Assert.Equal(".reopened.json", ReopenMarks.Suffix);
        Assert.Equal(".new-session.json", NewSessionChoices.Suffix);
        Assert.Equal(".hand-over.json", HandOverBox.Suffix);
        Assert.Equal(".hand-over-ask.json", HandOverAskFiles.AskSuffix);
        Assert.Equal(".hand-over-state.json", HandOverAskFiles.StateSuffix);
        Assert.Equal(".limit-move.json", LimitBoxes.Suffix);
        foreach (var suffix in new[] { ".cannot.json", ".reopened.json", ".new-session.json", ".hand-over.json", ".hand-over-ask.json", ".hand-over-state.json", ".limit-move.json" })
        {
            Assert.Contains(suffix, SessionHomeFiles.SessionSuffixes);
        }
    }

    [Theory]
    [InlineData("none")]
    [InlineData("ask")]
    [InlineData("reopened")]
    [InlineData("limit")]
    public void Literal_historical_files_keep_inventory_order_mark_names_actual_bytes_and_refusal_continuation(string refusal)
    {
        Directory.CreateDirectory(Sessions);
        var conversation = Path.Combine(Sessions, "s1.events.jsonl");
        var transcript = Path.Combine(Sessions, "s1.log");
        (string Suffix, int Size)[] kept =
        [
            (".cannot.json", 1), (".reopened.json", 2), (".new-session.json", 3), (".hand-over.json", 4),
            (".hand-over-ask.json", 5), (".hand-over-state.json", 6), (".limit-move.json", 7),
        ];
        File.WriteAllText(conversation, "abc");
        File.WriteAllText(transcript, "tx");
        foreach (var (suffix, size) in kept) File.WriteAllText(Path.Combine(Sessions, "s1" + suffix), new string('x', size));
        var refused = refusal switch { "ask" => ".hand-over-ask.json", "reopened" => ".reopened.json", "limit" => ".limit-move.json", _ => null };
        var calls = new List<(string Path, bool Folder)>();
        var files = new SessionHomeFiles(_home, (path, folder) =>
        {
            calls.Add((path, folder));
            if (refused is not null && path.EndsWith(refused, StringComparison.Ordinal)) throw new IOException("held");
            SessionHomeFiles.FromDisk(path, folder);
        });

        Assert.Equal((3L, 2L, 28L), (files.Size("s1").Conversations, files.Size("s1").Transcripts, files.Size("s1").Other));
        var result = files.Remove(["s1"], null);

        Assert.Equal([conversation, transcript, .. kept.Select(each => Path.Combine(Sessions, "s1" + each.Suffix))], calls.Select(call => call.Path));
        Assert.All(calls, call => Assert.False(call.Folder));
        Assert.Equal(["conversation", "transcript", "mark", "choice", "hand-over"], result.Went["s1"]);
        var stays = refusal switch { "ask" or "limit" => new[] { "hand-over" }, "reopened" => ["mark"], _ => [] };
        Assert.Equal(stays, result.Stayed["s1"]);
        Assert.Equal(refused is null ? [] : [Path.Combine(Sessions, "s1" + refused)], result.Failed);
        var refusedSize = refused is null ? 0 : kept.Single(each => each.Suffix == refused).Size;
        Assert.Equal((3L, 2L, 28L - refusedSize), (result.Bytes["s1"].Conversations, result.Bytes["s1"].Transcripts, result.Bytes["s1"].Other));
        Assert.Equal(refused is not null, File.Exists(Path.Combine(Sessions, "s1" + refused)));
    }

    [Fact]
    public void The_rebase_key_is_its_exact_text_and_reads_its_tip_from_the_end_including_a_session_name_with_a_slash()
    {
        Assert.Equal("rebase", DaorisKeys.RebaseKind);
        Assert.Equal("rebase/s1/0123abc", DaorisKeys.Rebase("s1", "0123abc"));
        Assert.Equal("rebase/origin/s1/0123abc", DaorisKeys.Rebase("origin/s1", "0123abc"));
        Assert.Equal("0123abc", DaorisKeys.RebaseTipOf(DaorisKeys.Rebase("s1", "0123abc")));
        Assert.Equal("0123abc", DaorisKeys.RebaseTipOf(DaorisKeys.Rebase("origin/s1", "0123abc")));
        Assert.Equal("0123abc", DaorisKeys.RebaseTipOf(DaorisKeys.Rebase("a/b/c", "0123abc")));
        Assert.Null(DaorisKeys.RebaseTipOf(null));
        Assert.Null(DaorisKeys.RebaseTipOf(""));
        Assert.Null(DaorisKeys.RebaseTipOf("rebase"));
        Assert.Null(DaorisKeys.RebaseTipOf("rebase/"));
        Assert.Null(DaorisKeys.RebaseTipOf("rebase/s1"));
        Assert.Null(DaorisKeys.RebaseTipOf("rebase/s1/"));
        Assert.Null(DaorisKeys.RebaseTipOf("Rebase/s1/0123abc"));
        Assert.Null(DaorisKeys.RebaseTipOf(DaorisKeys.Reopen("q1", "m1", 3)));
        Assert.Null(DaorisKeys.RebaseTipOf(DaorisKeys.TransientRetry("s1", new DateTimeOffset(2026, 10, 27, 9, 0, 0, TimeSpan.Zero))));
    }

    [Fact]
    public async Task The_rebase_telling_says_under_the_exact_key_and_refuses_as_before()
    {
        var conflict = new LandingConflict("it conflicted", "Daoris: rebase your branch.");
        var said = new List<(string Session, string Text, string Key)>();
        var answer = new SayAnswer(true, "kept");

        Assert.Same(answer, await RebaseTelling.TellAsync((s, t, k, _) => { said.Add((s, t, k)); return Task.FromResult<SayAnswer?>(answer); }, "origin/s1", "0123abc", conflict, default));
        Assert.Equal([("origin/s1", "Daoris: rebase your branch.", "rebase/origin/s1/0123abc")], said);

        Assert.Null(await RebaseTelling.TellAsync((_, _, _, _) => throw new InvalidOperationException("never"), "s1", null, conflict, default));
        var refused = await RebaseTelling.TellAsync((_, _, _, _) => throw new DriverException("no door"), "s1", "0123abc", conflict, default);
        Assert.Equal(new SayAnswer(false, "no door"), refused);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            RebaseTelling.TellAsync((_, _, _, _) => throw new InvalidOperationException("kept"), "s1", "0123abc", conflict, default));
    }

    [Fact]
    public async Task The_automatic_landing_says_its_rebase_words_as_Daoris_under_the_keyed_say()
    {
        var bodies = new List<(string Path, string Body)>();
        var handler = new Handler(async request =>
        {
            bodies.Add((request.RequestUri!.AbsolutePath, await request.Content!.ReadAsStringAsync()));
            return Json("{\"message\":\"kept\"}");
        });
        using var client = new ServiceClient("http://ledger.test", null, new HttpClient(handler, disposeHandler: false));

        await new ServiceAutoLandingWorld(client).SayAsync("s1", "Daoris: rebase.", DaorisKeys.Rebase("s1", "0123abc"), default);

        var (path, body) = Assert.Single(bodies);
        Assert.Equal("/api/sessions/s1/say", path);
        using var parsed = JsonDocument.Parse(body);
        Assert.Equal("daoris", parsed.RootElement.GetProperty("from").GetString());
        Assert.Equal("rebase/s1/0123abc", parsed.RootElement.GetProperty("key").GetString());
        Assert.Equal("Daoris: rebase.", parsed.RootElement.GetProperty("text").GetString());
    }

    [Fact]
    public void The_press_hold_keeps_its_four_values_and_order()
    {
        Assert.Equal(["Held", "Busy", "Ending", "NotHere"], Enum.GetNames<PressHold>());
        Assert.Equal([0, 1, 2, 3], Enum.GetValues<PressHold>().Select(each => (int)each));
    }

    [Theory]
    [InlineData(PressHold.Busy, false, "busy")]
    [InlineData(PressHold.Ending, true, "busy")]
    [InlineData(PressHold.NotHere, true, "elsewhere")]
    public async Task The_press_refuses_a_busy_ending_or_elsewhere_conversation_before_judging_and_releases_nothing(PressHold hold, bool live, string code)
    {
        var holder = new Holder(hold);
        var pressed = await ChatPresses.PressAsync(_home, new ChatPressAsk("s1", ChatTree, "chat-s1", "engine", "default", live), holder,
            (_, _) => throw new InvalidOperationException("never judged"), (_, _) => throw new InvalidOperationException("never landed"), () => 0);

        Assert.False(pressed.Landed.Landed);
        Assert.Equal(code, pressed.Code);
        Assert.Equal(["hold:s1"], holder.Calls);
    }

    [Fact]
    public async Task A_held_conversation_whose_commit_cannot_be_read_is_refused_and_released_once()
    {
        var holder = new Holder(PressHold.Held);
        var pressed = await ChatPresses.PressAsync(_home, new ChatPressAsk("s1", ChatTree, "chat-s1", "engine", "default", Live: true), holder,
            (_, _) => throw new InvalidOperationException("never judged"), (_, _) => throw new InvalidOperationException("never landed"), () => 0,
            head: _ => Task.FromResult<string?>(null));

        Assert.False(pressed.Landed.Landed);
        Assert.Null(pressed.Code);
        Assert.Equal(["hold:s1", "release:s1"], holder.Calls);
    }

    [Theory]
    [InlineData(false, "LAND_SESSION_TREE")]
    [InlineData(true, "LAND_WITHOUT_LOOK")]
    public async Task The_land_press_tells_its_note_once_with_the_route_session_and_folder_even_where_it_was_refused(bool withoutLook, string route)
    {
        using var client = Client();
        var before = DateTimeOffset.UtcNow;

        var code = await ChatLandCommand.RunAsync(World(client), "s1", ChatTree, "chat-s1", Ended("s1"), null,
            withoutLook ? new ChatLookPress("why", "terminal") : null);
        var after = DateTimeOffset.UtcNow;

        Assert.Equal(1, code);
        var note = Assert.Single(Directory.EnumerateFiles(Requests, "*" + PressesTold.Suffix));
        Assert.Equal(Requests, Path.GetDirectoryName(note));
        var press = Assert.Single(new PressesTold(_home).Take(after));
        Assert.Equal((route, "s1"), (press.Route, press.Session));
        Assert.InRange(press.At, before.AddSeconds(-1), after);
        Assert.Empty(Directory.EnumerateFiles(Requests, "*" + PressesTold.Suffix));
    }

    [Fact]
    public async Task A_note_that_cannot_be_written_is_ignored_and_the_press_still_answers_its_words()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Requests)!);
        File.WriteAllText(Requests, "a file where the folder would be");
        using var client = Client();
        var world = World(client);

        Assert.Equal(1, await ChatLandCommand.RunAsync(world, "s1", ChatTree, "chat-s1", Ended("s1"), null, null));

        Assert.True(File.Exists(Requests));
        Assert.NotEqual("", ((StringWriter)world.Error).ToString() + ((StringWriter)world.Output).ToString());
    }

    private ServiceClient Client() => new("http://ledger.test", null, new HttpClient(new Handler(_ => Task.FromResult(Json("[]"))), disposeHandler: false));

    private ChatLandWorld World(ServiceClient service) => new(service, _home, new SessionTrees(_home), null, new StringWriter(), new StringWriter());

    private string Ended(string id) => JsonSerializer.Serialize(new[] { new { id, state = "completed", kind = "chat", repository = "engine", tree = ChatTree } });

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => answer(request);
    }

    private sealed class Holder(PressHold hold) : IChatPressing
    {
        public List<string> Calls { get; } = [];
        public Task<PressHold> HoldAsync(string sessionId, CancellationToken ct = default) { Calls.Add("hold:" + sessionId); return Task.FromResult(hold); }
        public void Release(string sessionId) => Calls.Add("release:" + sessionId);
    }
}
