using System.Text;
using System.Text.Json;
using Daoris.Knowledge;
using Daoris.Knowledge.Http;
using Microsoft.AspNetCore.Http;

namespace Daoris.Service.Http.Tests;

/// <summary>
/// The service's confirmation queue: a terminal's keyless call to a
/// person's or the driver's door is refused, and its asker asks the window with the exact request. It waits with no clock until the
/// person confirms or refuses it or its asker withdraws it; the host makes it, as asked, at the Confirm, and a request the door then
/// refuses is <c>stale</c> with that door's own sentence. The asker reads how it ended by its id. Five sessions' asks wait at most, the
/// person's terminal keeps one more place, and an asker at its bound is refused with its waiting named.
/// </summary>
public sealed class ConfirmationTests
{
    private const string Key = KeyedHost.Key;

    private const string Body = """{"workspace":"default","sentence":"Rename the totals column."}""";

    private static (DaorisHost Host, MovableClock Clock) Keyed()
    {
        var clock = new MovableClock();
        return (new DaorisHost(ServiceMode.Local, seed: LocalHost.Seed, input: Key + "\n", clock: clock), clock);
    }

    private static string AskOf(int n) => $$"""{"workspace":"default","sentence":"Ask number {{n}}."}""";

    private static Task<Answer> AskAsync(DaorisHost host, string method, string path, string? body, string? session = null) =>
        host.PostAsync("/api/confirmations", new { method, path, body, session });

    /// <summary>Asks, and answers the confirmation's id and the asker's token; the ask must be taken.</summary>
    private static async Task<(string Id, string Token)> AskedAsync(
        DaorisHost host, string method, string path, string? body, string? session = null)
    {
        var asked = await AskAsync(host, method, path, body, session);
        Assert.True(asked.Status == 200, $"the ask answered {asked.Status}: {asked.Body}");
        return (asked.Json.GetProperty("confirmation").GetProperty("id").GetString()!, asked.Json.GetProperty("token").GetString()!);
    }

    private static Task<Answer> ConfirmAsync(DaorisHost host, string id, string? person = Key) =>
        host.SendAsync("POST", $"/api/confirmations/{id}/confirm", DaorisHost.Loopback, person: person);

    private static Task<Answer> RefuseAsync(DaorisHost host, string id, string? person = Key) =>
        host.SendAsync("POST", $"/api/confirmations/{id}/refuse", DaorisHost.Loopback, person: person);

    private static Task<Answer> WithdrawAsync(DaorisHost host, string id, string? token) =>
        host.PostAsync($"/api/confirmations/{id}/withdraw", new { token });

    /// <summary>The confirmation as its asker polls it.</summary>
    private static async Task<JsonElement> PolledAsync(DaorisHost host, string id)
    {
        var polled = await host.GetAsync($"/api/confirmations/{id}");
        Assert.True(polled.Status == 200, $"the poll answered {polled.Status}: {polled.Body}");
        return polled.Json;
    }

    private static async Task<string> StateAsync(DaorisHost host, string id) =>
        (await PolledAsync(host, id)).GetProperty("state").GetString()!;

    private static async Task<string[]> ListedAsync(DaorisHost host) =>
        [.. (await host.GetAsync("/api/confirmations")).Json.EnumerateArray().Select(row => row.GetProperty("id").GetString()!)];

    private static void AssertRefused(Answer answer, string code, string what)
    {
        Assert.True(answer.Status == 403, $"{what} answered {answer.Status}: {answer.Body}");
        Assert.Equal(code, answer.Json.GetProperty("code").GetString());
    }

    private static List<(string Name, string Value)> Pairs(JsonElement list) =>
        [.. list.EnumerateArray().Select(pair => (pair.GetProperty("name").GetString()!, pair.GetProperty("value").GetString()!))];

    /// <summary>Each value a card shows: its JSON pointer, its JSON type and its value.</summary>
    private static List<(string Name, string Type, string Value)> Typed(JsonElement list) =>
        [.. list.EnumerateArray().Select(field => (
            field.GetProperty("name").GetString()!, field.GetProperty("type").GetString()!, field.GetProperty("value").GetString()!))];

    /// <summary>One request whose body is <paramref name="body"/>, a stream the test controls: how much of it is read.</summary>
    private static async Task<Answer> SendStreamAsync(DaorisHost host, string method, string path, Stream body)
    {
        var context = await host.Server.SendAsync(http =>
        {
            http.Request.Method = method;
            http.Request.Path = path;
            http.Connection.RemoteIpAddress = DaorisHost.Loopback;
            http.Request.Body = body;
            http.Request.ContentType = "application/json";
            http.Features.Set<Microsoft.AspNetCore.Http.Features.IHttpRequestBodyDetectionFeature>(new HasBody());
        });

        using var reader = new StreamReader(context.Response.Body);
        return new Answer(context.Response.StatusCode, await reader.ReadToEndAsync(), context.Response.ContentType);
    }

    private sealed class HasBody : Microsoft.AspNetCore.Http.Features.IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody => true;
    }

    /// <summary>A body that serves <paramref name="prefix"/>, then <paramref name="more"/> bytes of spaces, counting what it served.</summary>
    private sealed class CountingStream(byte[] prefix, long more) : Stream
    {
        public long Served { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => Served; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count) => Serve(buffer.AsSpan(offset, count));

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Serve(buffer.Span));

        private int Serve(Span<byte> into)
        {
            var written = 0;
            while (written < into.Length && Served < prefix.Length + more)
            {
                into[written++] = Served < prefix.Length ? prefix[Served] : (byte)' ';
                Served++;
            }

            return written;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private static async Task<string> PublishAsync(DaorisHost host, string title)
    {
        var published = await host.PostAsync("/api/quests", new { from = "Asker", to = "Keeper", title, body = "Asked by a session." });
        Assert.Equal(200, published.Status);
        return published.Json.GetProperty("quest").GetProperty("id").GetString()!;
    }

    /// <summary>The note a quest's done door answered it with.</summary>
    private static string? NoteOf(string answer)
    {
        using var quest = JsonDocument.Parse(answer);
        return quest.RootElement.GetProperty("quest").GetProperty("note").GetString();
    }

    private static async Task<int> AsksAsync(DaorisHost host) =>
        (await host.GetAsync("/api/asks?workspace=default")).Json.GetArrayLength();

    /// <summary>
    /// The whole path: refused, asked, listed, left an hour with nothing ending it, then confirmed with the
    /// key, which makes the request as asked, as the person's; the asker reads it made, with the door's own answer. Answered
    /// once.
    /// </summary>
    [Fact]
    public async Task A_request_left_an_hour_then_confirmed_is_made_and_its_asker_reads_it_made()
    {
        var (host, clock) = Keyed();
        using var _host = host;

        AssertRefused(await host.SendAsync("POST", "/api/asks", DaorisHost.Loopback, json: Body), PersonDoors.PersonOnly, "a keyless ask");

        var asked = await AskAsync(host, "POST", "/api/asks", Body);
        Assert.Equal(200, asked.Status);
        Assert.Equal(PersonConfirmations.WaitingSentence, asked.Json.GetProperty("message").GetString());
        var token = asked.Json.GetProperty("token").GetString()!;
        var card = asked.Json.GetProperty("confirmation");
        var id = card.GetProperty("id").GetString()!;
        Assert.Equal("waiting", card.GetProperty("state").GetString());
        Assert.Equal("POST", card.GetProperty("method").GetString());
        Assert.Equal("/api/asks", card.GetProperty("path").GetString());
        Assert.Equal("/api/asks", card.GetProperty("route").GetString());
        Assert.Equal("an ask", card.GetProperty("act").GetString());
        Assert.Empty(Pairs(card.GetProperty("values")));
        Assert.Equal(
            [("/workspace", "string", "default"), ("/sentence", "string", "Rename the totals column.")], Typed(card.GetProperty("fields")));
        Assert.Equal(Body, card.GetProperty("body").GetString());
        Assert.Equal(clock.Now, card.GetProperty("asked").GetDateTimeOffset());
        // No clock ends it, and an ask naming no session is the terminal's.
        Assert.False(card.TryGetProperty("expires", out _));
        Assert.False(card.TryGetProperty("session", out _));

        // An hour on, it still waits: listed for the window, and waiting for the asker that polls it.
        clock.Move(TimeSpan.FromHours(1));
        Assert.Equal([id], await ListedAsync(host));
        Assert.Equal("waiting", await StateAsync(host, id));
        Assert.Equal(0, await AsksAsync(host));
        // The token is the asker's alone: never listed, never polled.
        Assert.DoesNotContain(token, (await host.GetAsync("/api/confirmations")).Body);
        Assert.DoesNotContain(token, (await host.GetAsync($"/api/confirmations/{id}")).Body);

        var confirmed = await ConfirmAsync(host, id);
        Assert.True(confirmed.Status == 200, $"the Confirm answered {confirmed.Status}: {confirmed.Body}");
        Assert.Equal(PersonConfirmations.MadeAnswer, confirmed.Json.GetProperty("message").GetString());
        var made = confirmed.Json.GetProperty("confirmation");
        Assert.Equal("made", made.GetProperty("state").GetString());
        Assert.Equal(200, made.GetProperty("status").GetInt32());
        Assert.Equal(1, await AsksAsync(host));
        Assert.Empty(await ListedAsync(host));

        // The asker reads it made, with the door's own answer.
        var polled = await PolledAsync(host, id);
        Assert.Equal("made", polled.GetProperty("state").GetString());
        Assert.Equal(200, polled.GetProperty("status").GetInt32());
        Assert.Equal(clock.Now, polled.GetProperty("ended").GetDateTimeOffset());
        using (var answer = JsonDocument.Parse(polled.GetProperty("answer").GetString()!))
        {
            Assert.Equal("Rename the totals column.", answer.RootElement.GetProperty("ask").GetProperty("sentence").GetString());
        }

        // Answered once: a second Confirm makes nothing.
        var again = await ConfirmAsync(host, id);
        Assert.Equal(409, again.Status);
        Assert.Equal(PersonConfirmations.ConfirmedAlreadySentence, again.Error);
        Assert.Equal(1, await AsksAsync(host));
    }

    /// <summary>
    /// The request is made exactly as asked (§4 M4: the path, the query and the body to the byte): a body's spacing and its words
    /// beyond ASCII reach the door as asked, a path with a trailing slash reaches its door as sent, and a query is the door's to bind.
    /// </summary>
    [Fact]
    public async Task The_request_is_made_exactly_as_asked_its_path_query_and_body_to_the_byte()
    {
        var (host, _) = Keyed();
        using var _host = host;
        var quest = await PublishAsync(host, "A quest the person closes by hand");
        const string Spaced = """{ "note" : "Finished by hand, café ✓." }""";

        var (done, _) = await AskedAsync(host, "POST", $"/api/quests/{quest}/done", Spaced);
        Assert.Equal(Spaced, (await PolledAsync(host, done)).GetProperty("body").GetString());
        Assert.Equal(200, (await ConfirmAsync(host, done)).Status);
        Assert.Equal("The person marked this done: Finished by hand, café ✓.", NoteOf((await PolledAsync(host, done)).GetProperty("answer").GetString()!));

        // A trailing slash is asked, shown by its door, and made, as sent.
        var slashed = await AskAsync(host, "POST", "/api/asks/", Body);
        Assert.True(slashed.Status == 200, slashed.Body);
        Assert.Equal("/api/asks", slashed.Json.GetProperty("confirmation").GetProperty("route").GetString());
        Assert.Equal("/api/asks/", slashed.Json.GetProperty("confirmation").GetProperty("path").GetString());
        Assert.Equal(200, (await ConfirmAsync(host, slashed.Json.GetProperty("confirmation").GetProperty("id").GetString()!)).Status);
        Assert.Equal(1, await AsksAsync(host));
        Assert.Equal(400, (await AskAsync(host, "POST", "/api/asks//", Body)).Status);
    }

    /// <summary>
    /// The person's refusal (the person-door design §4.2, *its default is no*): nothing is made, its asker reads it refused, and it
    /// can be neither confirmed nor withdrawn after.
    /// </summary>
    [Fact]
    public async Task A_refused_request_does_nothing()
    {
        var (host, _) = Keyed();
        using var _host = host;
        var (id, token) = await AskedAsync(host, "POST", "/api/asks", Body);

        var refused = await RefuseAsync(host, id);
        Assert.Equal(200, refused.Status);
        Assert.Equal(PersonConfirmations.RefusedAnswer, refused.Json.GetProperty("message").GetString());
        Assert.Equal("refused", refused.Json.GetProperty("confirmation").GetProperty("state").GetString());
        Assert.Equal("refused", await StateAsync(host, id));

        var confirmedAfter = await ConfirmAsync(host, id);
        Assert.Equal(409, confirmedAfter.Status);
        Assert.Equal(PersonConfirmations.RefusedSentence, confirmedAfter.Error);
        Assert.Equal(409, (await WithdrawAsync(host, id, token)).Status);
        Assert.Equal(0, await AsksAsync(host));
        Assert.Empty(await ListedAsync(host));
    }

    /// <summary>
    /// A request whose grounds moved while it waited (§4 M4): made at the Confirm, the door refuses it, and that refusal is its reason,
    /// one judge for every door. The window is answered 409 <c>not-made</c> with the door's sentence, the asker reads it <c>stale</c>
    /// with that sentence and the door's status, and nothing else moved.
    /// </summary>
    [Fact]
    public async Task A_stale_request_is_refused_with_its_doors_reason_and_its_asker_reads_it()
    {
        var (host, _) = Keyed();
        using var _host = host;
        var quest = await PublishAsync(host, "A quest the person closes by hand");
        const string Note = """{"note":"Finished by hand."}""";
        var (id, _) = await AskedAsync(host, "POST", $"/api/quests/{quest}/done", Note);

        // Meanwhile the person closes it in the window: the same request now meets the door's refusal.
        var closed = await host.SendAsync("POST", $"/api/quests/{quest}/done", DaorisHost.Loopback, json: """{"note":"Closed in the window."}""", person: Key);
        Assert.Equal(200, closed.Status);
        var direct = await host.SendAsync("POST", $"/api/quests/{quest}/done", DaorisHost.Loopback, json: Note, person: Key);
        Assert.Equal(409, direct.Status);

        var confirmed = await ConfirmAsync(host, id);
        Assert.Equal(409, confirmed.Status);
        Assert.Equal(PersonConfirmations.NotMade, confirmed.Json.GetProperty("code").GetString());
        Assert.Equal(PersonConfirmations.NotDoneSentence(direct.Error), confirmed.Error);

        var polled = await PolledAsync(host, id);
        Assert.Equal("stale", polled.GetProperty("state").GetString());
        Assert.Equal(direct.Error, polled.GetProperty("reason").GetString());
        Assert.Equal(409, polled.GetProperty("status").GetInt32());
        Assert.Equal(direct.Body, polled.GetProperty("answer").GetString());
        Assert.Equal("The person marked this done: Closed in the window.", NoteOf(closed.Body));
        Assert.Equal(409, (await ConfirmAsync(host, id)).Status);
    }

    /// <summary>
    /// Its asker withdraws it, by the token its ask got: nothing is made, it waits
    /// no more, and it can be confirmed no more. Another asker's token withdraws nothing, and one being or already made is no longer
    /// withdrawn.
    /// </summary>
    [Fact]
    public async Task A_withdrawn_request_does_nothing_and_only_its_asker_withdraws_it()
    {
        var (host, _) = Keyed();
        using var _host = host;
        var (id, token) = await AskedAsync(host, "POST", "/api/asks", Body);
        var (other, otherToken) = await AskedAsync(host, "POST", "/api/asks", AskOf(2), session: "s-other");

        foreach (var (presented, what) in new (string?, string)[] { (otherToken, "another asker's token"), (null, "no token"), ("not a token", "no token of this host's") })
        {
            var wrong = await WithdrawAsync(host, id, presented);
            Assert.True(wrong.Status == 403, $"{what} answered {wrong.Status}: {wrong.Body}");
            Assert.Equal(PersonConfirmations.NotAsker, wrong.Json.GetProperty("code").GetString());
        }

        Assert.Equal("waiting", await StateAsync(host, id));

        var withdrawn = await WithdrawAsync(host, id, token);
        Assert.Equal(200, withdrawn.Status);
        Assert.Equal(PersonConfirmations.WithdrawnAnswer, withdrawn.Json.GetProperty("message").GetString());
        Assert.Equal("withdrawn", withdrawn.Json.GetProperty("confirmation").GetProperty("state").GetString());
        Assert.Equal([other], await ListedAsync(host));
        Assert.Equal("withdrawn", await StateAsync(host, id));

        var confirmedAfter = await ConfirmAsync(host, id);
        Assert.Equal(409, confirmedAfter.Status);
        Assert.Equal(PersonConfirmations.WithdrawnSentence, confirmedAfter.Error);
        Assert.Equal(409, (await WithdrawAsync(host, id, token)).Status);
        Assert.Equal(0, await AsksAsync(host));

        // One the person confirmed is made, and no longer withdrawn.
        Assert.Equal(200, (await ConfirmAsync(host, other)).Status);
        var late = await WithdrawAsync(host, other, otherToken);
        Assert.Equal(409, late.Status);
        Assert.Equal(PersonConfirmations.ConfirmedAlreadySentence, late.Error);
        Assert.Equal(1, await AsksAsync(host));
    }

    /// <summary>
    /// With no clock to drain them, sessions' asks could hold every
    /// place. Five may wait for asks that name a session; the person's terminal, which names none, keeps one more, so six sessions' asks
    /// leave it free. A sixth is refused <c>confirmations-full</c> and an asker at its bound <c>asker-full</c>, each naming the asker's
    /// own requests waiting, so it can withdraw one.
    /// </summary>
    [Fact]
    public async Task Six_sessions_asks_leave_the_terminals_place_and_an_asker_past_its_bound_is_refused_with_its_waiting_named()
    {
        var (host, _) = Keyed();
        using var _host = host;
        var sessions = new List<string>();
        for (var n = 1; n <= PersonConfirmations.Most; n++)
        {
            sessions.Add((await AskedAsync(host, "POST", "/api/asks", AskOf(n), session: $"s{n}")).Id);
        }

        var sixth = await AskAsync(host, "POST", "/api/asks", AskOf(6), session: "s6");
        AssertRefused(sixth, PersonDoors.ConfirmationsFull, "a sixth session's ask");
        Assert.StartsWith("5 acts already wait for the person's confirmation", sixth.Error);
        Assert.Equal(PersonConfirmations.Most, (await ListedAsync(host)).Length);

        // The person's terminal names no session, and its place is free.
        var (terminal, _) = await AskedAsync(host, "POST", "/api/asks", AskOf(7));
        Assert.Equal(PersonConfirmations.Most + 1, (await ListedAsync(host)).Length);

        // Its one place taken, the terminal is refused too, its own request named.
        var seventh = await AskAsync(host, "POST", "/api/asks", AskOf(8));
        AssertRefused(seventh, PersonDoors.ConfirmationsFull, "a terminal's ask with every place taken");
        Assert.Contains($"`{terminal}` (an ask)", seventh.Error);
        Assert.DoesNotContain(sessions[0], seventh.Error);

        // Places freed, an asker at its bound is refused with its waiting named, and nothing else.
        foreach (var id in sessions.Skip(1)) Assert.Equal(200, (await RefuseAsync(host, id)).Status);
        var (again, _) = await AskedAsync(host, "POST", "/api/asks", AskOf(9), session: "s1");
        var third = await AskAsync(host, "POST", "/api/asks", AskOf(10), session: "s1");
        AssertRefused(third, PersonDoors.AskerFull, "a session's third");
        Assert.Contains($"`{sessions[0]}` (an ask)", third.Error);
        Assert.Contains($"`{again}` (an ask)", third.Error);
        Assert.DoesNotContain(terminal, third.Error);
        Assert.EndsWith("Nothing was asked.", third.Error);

        // The terminal is bounded alike, while places are free.
        var (terminalAgain, _) = await AskedAsync(host, "POST", "/api/asks", AskOf(11));
        var terminalThird = await AskAsync(host, "POST", "/api/asks", AskOf(12));
        AssertRefused(terminalThird, PersonDoors.AskerFull, "the terminal's third");
        Assert.Contains($"`{terminal}` (an ask)", terminalThird.Error);
        Assert.Contains($"`{terminalAgain}` (an ask)", terminalThird.Error);
        Assert.Equal(4, (await ListedAsync(host)).Length);
    }

    /// <summary>
    /// The host keeps what waits in memory (§4 M4): a restart ends it, and each asker that polls its id reads <i>not confirmed: Daoris
    /// restarted</i>, by the code <c>restarted</c>. An id no start of this host made is not called a restart.
    /// </summary>
    [Fact]
    public async Task A_restart_ends_what_waits_and_its_asker_reads_not_confirmed()
    {
        string id;
        var (before, _) = Keyed();
        using (before)
        {
            id = (await AskedAsync(before, "POST", "/api/asks", Body)).Id;
        }

        var (after, _) = Keyed();
        using var _after = after;
        var polled = await after.GetAsync($"/api/confirmations/{id}");
        Assert.Equal(404, polled.Status);
        Assert.Equal(PersonConfirmations.Restarted, polled.Json.GetProperty("code").GetString());
        Assert.Equal(PersonConfirmations.RestartedSentence, polled.Error);
        Assert.Equal(PersonConfirmations.Restarted, (await ConfirmAsync(after, id)).Json.GetProperty("code").GetString());
        Assert.Empty(await ListedAsync(after));
        Assert.Equal(0, await AsksAsync(after));

        var never = await after.GetAsync("/api/confirmations/abc");
        Assert.Equal(404, never.Status);
        Assert.False(never.Json.TryGetProperty("code", out _));
    }

    /// <summary>What a held wait on what waits answers: its cursor, and the ids waiting, oldest first.</summary>
    private static async Task<(string Since, string[] Waiting)> WaitingAsync(Task<Answer> held)
    {
        var answer = await held.WaitAsync(Poll.Patience);
        Assert.True(answer.Status == 200, $"the wait answered {answer.Status}: {answer.Body}");
        return (answer.Json.GetProperty("since").GetString()!,
            [.. answer.Json.GetProperty("waiting").EnumerateArray().Select(row => row.GetProperty("id").GetString()!)]);
    }

    private static Task<Answer> WaitFromAsync(DaorisHost host, string? since, int wait = 30) =>
        host.GetAsync($"/api/confirmations/waiting?wait={wait}" + (since is null ? "" : $"&since={Uri.EscapeDataString(since)}"));

    /// <summary>
    /// What waits says its change, so the desktop's host holds one wait and no
    /// window polls. No cursor, or another start's, is answered at once; a wait from the current cursor is held and answered by an ask,
    /// a refusal, a withdrawal and a Confirm; with nothing moving, an hour on, at its bound with what waits unchanged.
    /// </summary>
    [Fact]
    public async Task What_waits_is_answered_at_each_change_and_an_hour_moves_nothing()
    {
        var (host, clock) = Keyed();
        using var _host = host;

        var (empty, none) = await WaitingAsync(WaitFromAsync(host, null));
        Assert.Empty(none);
        Assert.Equal(empty, (await WaitingAsync(WaitFromAsync(host, "0123abcd0123.7"))).Since);

        var held = WaitFromAsync(host, empty);
        var (first, firstToken) = await AskedAsync(host, "POST", "/api/asks", Body);
        var (asked, waiting) = await WaitingAsync(held);
        Assert.NotEqual(empty, asked);
        Assert.Equal([first], waiting);

        held = WaitFromAsync(host, asked);
        var (second, _) = await AskedAsync(host, "POST", "/api/asks", AskOf(2), session: "s2");
        var (twice, both) = await WaitingAsync(held);
        Assert.Equal([first, second], both);

        held = WaitFromAsync(host, twice);
        Assert.Equal(200, (await RefuseAsync(host, second)).Status);
        var (refused, left) = await WaitingAsync(held);
        Assert.Equal([first], left);

        // An hour passes with nothing moving: the wait is answered at its bound, what waits unchanged.
        var others = clock.Pending;
        held = WaitFromAsync(host, refused, wait: 10);
        await clock.PendingAsync(others + 1);
        clock.Move(TimeSpan.FromHours(1));
        var (hour, still) = await WaitingAsync(held);
        Assert.Equal(refused, hour);
        Assert.Equal([first], still);

        held = WaitFromAsync(host, refused);
        Assert.Equal(200, (await WithdrawAsync(host, first, firstToken)).Status);
        var (withdrawn, gone) = await WaitingAsync(held);
        Assert.Empty(gone);

        var (third, _) = await AskedAsync(host, "POST", "/api/asks", AskOf(3));
        held = WaitFromAsync(host, (await WaitingAsync(WaitFromAsync(host, withdrawn))).Since);
        Assert.Equal(200, (await ConfirmAsync(host, third)).Status);
        Assert.Empty((await WaitingAsync(held)).Waiting);
    }

    /// <summary>
    /// At most eight waits on what waits are held at once, as the change stream's are; a ninth is answered at once with
    /// what waits and the cursor as it is, and the eight are still answered by the next move.
    /// </summary>
    [Fact]
    public async Task A_ninth_wait_on_what_waits_is_answered_at_once()
    {
        var (host, clock) = Keyed();
        using var _host = host;
        var (since, _) = await WaitingAsync(WaitFromAsync(host, null));
        var others = clock.Pending;
        var held = Enumerable.Range(0, PersonConfirmations.MostWaits).Select(_ => WaitFromAsync(host, since)).ToList();
        // The eight held, each with its timer on the host's clock, which never reaches their bound: only the place answers a ninth.
        await clock.PendingAsync(others + PersonConfirmations.MostWaits);

        Assert.Equal((since, Array.Empty<string>()), await WaitingAsync(WaitFromAsync(host, since)));
        Assert.All(held, each => Assert.False(each.IsCompleted));

        var (asked, _) = await AskedAsync(host, "POST", "/api/asks", """{"workspace":"default","sentence":"A sentence."}""");
        foreach (var each in held) Assert.Equal([asked], (await WaitingAsync(each)).Waiting);
    }

    /// <summary>
    /// 🔴 A wait on what waits held past the request log's two seconds is the wait it asked
    /// for, never a failure. The desktop holds one from its start, and each start's log said <c>request.failed</c> of it at 20 s. The
    /// wait is held on the host's clock, which never reaches its bound, while the machine's passes the log's threshold.
    /// </summary>
    [Fact]
    public async Task A_wait_held_past_the_slow_threshold_is_not_written_as_failed()
    {
        var (host, clock) = Keyed();
        using var _host = host;
        var (since, _) = await WaitingAsync(WaitFromAsync(host, null));
        var others = clock.Pending;
        var held = WaitFromAsync(host, since, wait: 10);
        await clock.PendingAsync(others + 1);

        // The claim is the elapsed time itself: the request's own stopwatch must pass the threshold before it is answered.
        await Task.Delay(TimeSpan.FromMilliseconds(Daoris.Knowledge.Hosting.MachineLogProvider.SlowRequestMs + 500));
        clock.Move(TimeSpan.FromSeconds(10));
        Assert.Equal((since, Array.Empty<string>()), await WaitingAsync(held));

        Assert.DoesNotContain(host.LogLines(), line =>
        {
            using var parsed = JsonDocument.Parse(line);
            return parsed.RootElement.GetProperty("event").GetString() == "request.failed";
        });
    }

    /// <summary>
    /// A session can ask, and cannot answer (the person-door design §4.2): confirming or refusing without the key is refused as the
    /// person's door, and a key from another start as stale. The confirmation still waits.
    /// </summary>
    [Fact]
    public async Task A_keyless_confirm_or_refuse_is_refused_and_the_confirmation_still_waits()
    {
        var (host, _) = Keyed();
        using var _host = host;
        var (id, _) = await AskedAsync(host, "POST", "/api/asks", """{"workspace":"default","sentence":"Rename it."}""");

        var keyless = await ConfirmAsync(host, id, person: null);
        AssertRefused(keyless, PersonDoors.PersonOnly, "a keyless confirm");
        Assert.Equal(PersonDoors.PersonSentence("the confirmation of a terminal's act"), keyless.Error);
        var keylessRefusal = await RefuseAsync(host, id, person: null);
        AssertRefused(keylessRefusal, PersonDoors.PersonOnly, "a keyless refusal");
        Assert.Equal(PersonDoors.PersonSentence("the refusal of a terminal's act"), keylessRefusal.Error);
        AssertRefused(await ConfirmAsync(host, id, person: KeyedHost.Earlier), PersonDoors.Stale, "an earlier key's confirm");

        Assert.Equal("waiting", await StateAsync(host, id));
        Assert.Equal(0, await AsksAsync(host));
    }

    /// <summary>
    /// Only a door that wants the key can be asked for, in the very form that wants it (the person-door design §3.2): a read, an
    /// agent's door, the confirmations' own doors and an address no door answers are refused, and so are a body that is no JSON object
    /// and a session that is no name. Each refusal is a 400 and keeps nothing.
    /// </summary>
    [Fact]
    public async Task Only_a_door_that_wants_the_key_can_be_asked_for()
    {
        var (host, _) = Keyed();
        using var _host = host;
        var refused = new (string Method, string Path, string? Body, string? Session, string What)[]
        {
            ("GET", "/api/quests", null, null, "a read"),
            ("POST", "/api/quests", """{"from":"Asker","to":"Keeper","title":"t","body":"b"}""", null, "an agent's publish"),
            ("POST", "/api/quests/7/respond", """{"action":"take"}""", null, "a take"),
            ("POST", "/api/quests/7/respond", """{"action":"decline","reason":"r","whileOpen":false}""", null, "a decline while open is false"),
            ("POST", "/api/confirmations/abc/confirm", null, null, "a confirmation's confirm"),
            ("POST", "/api/confirmations/abc/refuse", null, null, "a confirmation's refusal"),
            ("POST", "/api/confirmations/abc/withdraw", """{"token":"t"}""", null, "a confirmation's withdrawal"),
            ("POST", "/api/confirmations", "{}", null, "an ask"),
            ("POST", "/api/nothing-here", "{}", null, "an address no door answers"),
            ("POST", "/api/quests/operations", "{}", null, "a shared host's door"),
            ("POST", "/api/quests/7/done/more", "{}", null, "a path longer than any door's"),
            ("POST", "http://localhost:5177/api/asks", "{}", null, "an absolute address"),
            ("POST", "/api/asks", "[1, 2]", null, "a body that is no object"),
            ("POST", "/api/asks", "not json", null, "a body that is no JSON"),
            ("POST", "/api/asks", "{}", new string('s', PersonConfirmations.SessionLimit + 1), "a session too long to be a name"),
            ("POST", "/api/asks", "{}", "s\u00071", "a session holding a control character"),
            ("PATCH", "/api/asks", "{}", null, "a method no door takes"),
            ("", "/api/asks", "{}", null, "no method"),
            ("POST", "", "{}", null, "no path"),
        };
        foreach (var (method, path, body, session, what) in refused)
        {
            var answer = await AskAsync(host, method, path, body, session);
            Assert.True(answer.Status == 400, $"{what} answered {answer.Status}: {answer.Body}");
            Assert.EndsWith("Nothing was asked.", answer.Error);
        }

        Assert.Contains("is no door of this service", (await AskAsync(host, "POST", "/api/quests/operations", "{}")).Error);
        Assert.Contains("A quest's publish is answered without the person's key", (await AskAsync(
            host, "POST", "/api/quests", """{"from":"Asker","to":"Keeper","title":"t","body":"b"}""")).Error);
        Assert.Empty(await ListedAsync(host));

        // The forms are named as the door names them; the driver's doors are asked for too, the query among their values.
        var forms = new (string Method, string Path, string Body, string Act)[]
        {
            ("POST", "/api/quests/7/respond", """{"action":"decline","reason":"Abandoned.","whileOpen":true}""", "an abandon's decline"),
            ("POST", "/api/quests/7/set-up", """{"commit":"abc","kind":"local","session":"s-1"}""", PersonDoors.SetUp.Act!),
            ("POST", "/api/quests/7/set-up", """{"commit":"abc","kind":"local","look":"http://localhost:5173"}""", "a set-up of their own"),
            ("post", "/api/sync?workspace=default", "", "a sync pass"),
        };
        var n = 0;
        foreach (var (method, path, body, act) in forms)
        {
            var asked = await AskAsync(host, method, path, body, session: $"form-{n++}");
            Assert.True(asked.Status == 200, $"{method} {path} answered {asked.Status}: {asked.Body}");
            Assert.Equal(act, asked.Json.GetProperty("confirmation").GetProperty("act").GetString());
        }

        var sync = (await host.GetAsync("/api/confirmations")).Json.EnumerateArray().Last();
        Assert.Equal("POST", sync.GetProperty("method").GetString());
        Assert.Equal("/api/sync", sync.GetProperty("route").GetString());
        Assert.Equal("form-3", sync.GetProperty("session").GetString());
        Assert.Equal([("workspace", "default")], Pairs(sync.GetProperty("values")));
        Assert.Empty(Pairs(sync.GetProperty("fields")));
        Assert.False(sync.TryGetProperty("body", out _));
    }

    /// <summary>
    /// The window shows the request by the door's name and its own fields, never a sentence the asker wrote (design §4.2):
    /// every value the body carries, each by its JSON pointer and its JSON type, so no two bodies show alike and nothing the
    /// request carries is out of the person's sight; the address's values beside them.
    /// </summary>
    [Fact]
    public async Task The_card_names_the_door_and_every_value_the_request_carries_by_its_pointer_and_type()
    {
        var (host, _) = Keyed();
        using var _host = host;

        var deleted = await AskAsync(host, "DELETE", "/api/registry/my%20repo", null, session: "s1");
        var row = deleted.Json.GetProperty("confirmation");
        Assert.Equal("a repository's retire", row.GetProperty("act").GetString());
        Assert.Equal("/api/registry/my%20repo", row.GetProperty("path").GetString());
        Assert.Equal([("repository", "string", "my repo")], Typed(row.GetProperty("values")));
        Assert.Empty(Typed(row.GetProperty("fields")));
        // Answered, it waits no more: this test asks six.
        Assert.Equal(200, (await RefuseAsync(host, row.GetProperty("id").GetString()!)).Status);

        var review = await AskAsync(
            host, "POST", "/api/quests/12/review",
            """{"verdict":"reviewed","words":"Looks right.","setUp":{"machine":"m-1","sequence":3}}""", session: "s2");
        Assert.Equal(200, review.Status);
        var card = review.Json.GetProperty("confirmation");
        Assert.Equal("a review's verdict", card.GetProperty("act").GetString());
        Assert.Equal("/api/quests/{id}/review", card.GetProperty("route").GetString());
        Assert.Equal([("id", "12")], Pairs(card.GetProperty("values")));
        Assert.Equal(
            [
                ("/verdict", "string", "reviewed"), ("/words", "string", "Looks right."),
                ("/setUp/machine", "string", "m-1"), ("/setUp/sequence", "number", "3"),
            ],
            Typed(card.GetProperty("fields")));

        // A string and a number read differently, and a null is a null, never the word.
        var spelled = await AskAsync(
            host, "POST", "/api/quests/12/review", """{"verdict":"null","setUp":{"machine":null,"sequence":"3"}}""", session: "s3");
        Assert.Equal(
            [("/verdict", "string", "null"), ("/setUp/machine", "null", "null"), ("/setUp/sequence", "string", "3")],
            Typed(spelled.Json.GetProperty("confirmation").GetProperty("fields")));

        var cleared = await AskAsync(host, "POST", "/api/history/clear", """{"units":[{"kind":"quest","id":"3"},{}]}""", session: "s4");
        Assert.Equal(
            [("/units/0/kind", "string", "quest"), ("/units/0/id", "string", "3"), ("/units/1", "object", "{}")],
            Typed(cleared.Json.GetProperty("confirmation").GetProperty("fields")));
        var none = await AskAsync(host, "POST", "/api/history/clear", """{"units":[]}""", session: "s5");
        Assert.Equal([("/units", "array", "[]")], Typed(none.Json.GetProperty("confirmation").GetProperty("fields")));

        // A value the door takes as any JSON is shown whole, as it binds.
        var state = await AskAsync(
            host, "POST", "/api/sessions/s-1/state", """{"state":"finished","noteParts":[{"code":"a/b~c","values":[1]}]}""", session: "s6");
        Assert.Equal(
            [("/state", "string", "finished"), ("/noteParts", "array", """[{"code":"a/b~c","values":[1]}]""")],
            Typed(state.Json.GetProperty("confirmation").GetProperty("fields")));
    }

    /// <summary>
    /// 🔴 A card must never show one act while the request made does another. A body is asked for
    /// only with the names its door binds, each once, so the card shows only what binds: a property the binder would ignore (an empty
    /// name above a dismissal's fields, or a near spelling) leaves the door's own fields unbound, and a dismissal that names none
    /// dismisses every conflict. So is a query, and a body on a door that binds none.
    /// </summary>
    [Fact]
    public async Task A_request_is_asked_for_only_with_the_names_its_door_binds()
    {
        var (host, _) = Keyed();
        using var _host = host;
        const string Wire = "/api/registry/engine/workspace";

        var refused = new (string Method, string Path, string? Body, string What)[]
        {
            ("POST", Wire, """{"":{"workspace":"w"}}""", "an empty name above the door's fields"),
            ("POST", Wire, """{"workspac":"w"}""", "near spellings the binder ignores"),
            ("POST", Wire, """{"workspace":"a","Workspace":"b"}""", "a name twice, as the binder reads names"),
            ("POST", "/api/quests/12/review", """{"verdict":"reviewed","setUp":{"machine":"m-1","sequence":3,"extra":1}}""", "a nested name the door does not bind"),
            ("POST", "/api/history/clear", """{"units":[{"kind":"quest","id":"3","all":true}]}""", "a name inside a list"),
            ("POST", "/api/quests/12/accept", "{}", "a body on a door that binds none"),
            ("POST", "/api/sync?workspac=default", null, "a query name the door does not bind"),
            ("POST", "/api/sync?workspace=a&Workspace=b", null, "a query name twice"),
            ("POST", "/api/quests/12/done?note=x", """{"note":"y"}""", "a query on a door that reads none"),
        };
        foreach (var (method, path, body, what) in refused)
        {
            var answer = await AskAsync(host, method, path, body);
            Assert.True(answer.Status == 400, $"{what} answered {answer.Status}: {answer.Body}");
            Assert.EndsWith("Nothing was asked.", answer.Error);
        }

        Assert.Empty(await ListedAsync(host));

        // A re-wiring, as the door binds it; a name in another case binds the same, and is shown as sent.
        var one = await AskAsync(host, "POST", Wire, """{"Workspace":"w"}""");
        Assert.Equal(200, one.Status);
        Assert.Equal([("/Workspace", "string", "w")], Typed(one.Json.GetProperty("confirmation").GetProperty("fields")));
    }

    /// <summary>
    /// An ask is bounded before it is read whole: the ask itself is read no further than
    /// <see cref="PersonConfirmations.AskLimit"/>, a body over <see cref="PersonConfirmations.BodyLimit"/> or carrying over
    /// <see cref="PersonConfirmations.FieldLimit"/> values is refused, and while every place a session's ask may take is taken, a
    /// session's ask is refused before its body is parsed.
    /// </summary>
    [Fact]
    public async Task An_ask_is_bounded_before_it_is_read_whole()
    {
        var (host, _) = Keyed();
        using var _host = host;

        var ask = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { method = "POST", path = "/api/asks", body = "{}" }));
        var padded = new CountingStream(ask, more: 4L * PersonConfirmations.AskLimit);
        var tooLong = await SendStreamAsync(host, "POST", "/api/confirmations", padded);
        Assert.True(tooLong.Status == 413, $"an ask past its limit answered {tooLong.Status}: {tooLong.Body}");
        Assert.True(padded.Served <= PersonConfirmations.AskLimit + 1, $"{padded.Served} bytes of an ask were read");

        var large = JsonSerializer.Serialize(new { workspace = "default", sentence = new string('a', PersonConfirmations.BodyLimit) });
        Assert.Equal(400, (await AskAsync(host, "POST", "/api/asks", large)).Status);

        var units = string.Join(",", Enumerable.Range(0, PersonConfirmations.FieldLimit / 2 + 1).Select(n => $$"""{"kind":"quest","id":"{{n}}"}"""));
        Assert.Equal(400, (await AskAsync(host, "POST", "/api/history/clear", $$"""{"units":[{{units}}]}""")).Status);
        var within = string.Join(",", Enumerable.Range(0, PersonConfirmations.FieldLimit / 2).Select(n => $$"""{"kind":"quest","id":"{{n}}"}"""));
        Assert.Equal(200, (await AskAsync(host, "POST", "/api/history/clear", $$"""{"units":[{{within}}]}""", session: "s0")).Status);

        for (var n = 1; n < PersonConfirmations.Most; n++) await AskedAsync(host, "POST", "/api/asks", "{}", session: $"s{n}");
        AssertRefused(await AskAsync(host, "POST", "/api/asks", "not json at all", session: "s9"), PersonDoors.ConfirmationsFull, "a sixth that is no JSON");
    }

    /// <summary>
    /// The machine log (the person-door design §5.1): <c>person.confirmation</c> once as each is asked, made, found stale, refused or
    /// withdrawn, by the door's method and pattern; an asker at its bound and an ask with every place taken are a <c>person.refused</c>
    /// line each. Never the token, the body's words, the id in the path or the confirmation's id.
    /// </summary>
    [Fact]
    public async Task The_log_says_each_end_by_its_door_and_never_the_token_or_the_words()
    {
        var (host, _) = Keyed();
        using var _host = host;
        const string Words = "words-only-the-body-holds";
        var body = $$"""{"note":"{{Words}}"}""";
        var review = $$"""{"verdict":"not-yet","words":"{{Words}}"}""";
        var ask = $$"""{"workspace":"default","sentence":"{{Words}}"}""";
        var tokens = new List<string>();
        var ids = new List<string>();

        async Task<string> Asked(string method, string path, string? sent, string? session = null)
        {
            var (id, token) = await AskedAsync(host, method, path, sent, session);
            ids.Add(id);
            tokens.Add(token);
            return id;
        }

        // No such quest: made at the Confirm, its door refuses it.
        Assert.Equal(409, (await ConfirmAsync(host, await Asked("POST", "/api/quests/id-in-the-path/done", body))).Status);
        Assert.Equal(200, (await RefuseAsync(host, await Asked("DELETE", "/api/registry/id-in-the-path", null))).Status);
        var withdrawn = await Asked("POST", "/api/quests/id-in-the-path/review", review, "s-review");
        Assert.Equal(200, (await WithdrawAsync(host, withdrawn, tokens[^1])).Status);
        Assert.Equal(200, (await ConfirmAsync(host, await Asked("POST", "/api/asks", ask, "s1"))).Status);

        await Asked("POST", "/api/asks", ask, "s2");
        await Asked("POST", "/api/asks", ask, "s2");
        AssertRefused(await AskAsync(host, "POST", "/api/asks", ask, "s2"), PersonDoors.AskerFull, "a third");
        for (var n = 3; n <= PersonConfirmations.Most; n++) await Asked("POST", "/api/asks", ask, $"s{n}");
        AssertRefused(await AskAsync(host, "POST", "/api/asks", ask, "s9"), PersonDoors.ConfirmationsFull, "a sixth");

        var lines = host.LogLines();
        static JsonElement Parse(string line) => JsonDocument.Parse(line).RootElement;
        var confirmations = lines.Select(Parse).Where(line => line.GetProperty("event").GetString() == PersonConfirmations.Event)
            .Select(line => line.GetProperty("data"))
            .Select(data => (data.GetProperty("state").GetString()!, data.GetProperty("method").GetString()!, data.GetProperty("route").GetString()!))
            .ToList();
        Assert.Equal(
            [
                ("asked", "POST", "/api/quests/{id}/done"), ("stale", "POST", "/api/quests/{id}/done"),
                ("asked", "DELETE", "/api/registry/{repository}"), ("refused", "DELETE", "/api/registry/{repository}"),
                ("asked", "POST", "/api/quests/{id}/review"), ("withdrawn", "POST", "/api/quests/{id}/review"),
                ("asked", "POST", "/api/asks"), ("made", "POST", "/api/asks"),
                ("asked", "POST", "/api/asks"), ("asked", "POST", "/api/asks"),
                ("asked", "POST", "/api/asks"), ("asked", "POST", "/api/asks"), ("asked", "POST", "/api/asks"),
            ],
            confirmations);

        var refused = lines.Select(Parse).Where(line => line.GetProperty("event").GetString() == PersonDoors.Event)
            .Select(line => line.GetProperty("data"))
            .Select(data => (data.GetProperty("route").GetString()!, data.GetProperty("presented").GetString()!, data.GetProperty("code").GetString()!))
            .ToList();
        Assert.Equal(
            [("/api/confirmations", "none", PersonDoors.AskerFull), ("/api/confirmations", "none", PersonDoors.ConfirmationsFull)],
            refused);

        Assert.All(lines, line =>
        {
            foreach (var token in tokens) Assert.DoesNotContain(token, line);
            foreach (var id in ids) Assert.DoesNotContain(id, line);
            Assert.DoesNotContain(Words, line);
            Assert.DoesNotContain("id-in-the-path", line);
        });
    }

    /// <summary>
    /// A host handed no key gates nothing (the person-door design §2.1), so nothing it answers waits for a confirmation: an ask is
    /// refused with a sentence and keeps nothing, and none waits.
    /// </summary>
    [Fact]
    public async Task A_host_without_a_key_asks_nothing_and_answers_as_before()
    {
        using var host = new LocalHost();

        var asked = await AskAsync(host, "POST", "/api/asks", Body);
        Assert.Equal(409, asked.Status);
        Assert.Equal(PersonConfirmations.NoKeySentence, asked.Error);
        Assert.Empty(await ListedAsync(host));
        Assert.Equal(404, (await host.GetAsync("/api/confirmations/abc")).Status);
        Assert.Equal(200, (await host.SendAsync("POST", "/api/asks", DaorisHost.Loopback, json: Body)).Status);
    }

    /// <summary>A shared host has no door that is the person's alone (design §6), and so none for their confirmation.</summary>
    [Fact]
    public void A_shared_host_maps_no_confirmation()
    {
        using var host = new SharedHost();
        Assert.DoesNotContain(host.Routes(), route => route.Pattern.StartsWith("/api/confirmations", StringComparison.Ordinal));
    }
}

/// <summary>
/// The real host, as a terminal reaches it: the path a client sends, escapes and all, and a body, each made at the
/// Confirm through the host's own pipeline and reaching its door as asked. What the in-process host cannot show: Kestrel's own
/// reading of the address.
/// </summary>
public sealed class ConfirmationProcessTests : IDisposable
{
    private readonly string _scratch = Path.Combine(Path.GetTempPath(), "daoris-confirm-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        for (var attempt = 0; attempt < 20 && Directory.Exists(_scratch); attempt++)
        {
            try
            {
                Directory.Delete(_scratch, recursive: true);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(100);
            }
        }
    }

    private static async Task<(int Status, string Body)> SendAsync(
        HttpClient client, string url, string method, string path, string? body, string? person = null)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), url + path);
        if (body is not null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        if (person is not null) request.Headers.Add(PersonKey.Header, person);
        using var response = await client.SendAsync(request);
        return ((int)response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private static async Task<(string Id, string Token)> AskedAsync(HttpClient client, string url, string method, string path, string? body)
    {
        var asked = await SendAsync(client, url, "POST", "/api/confirmations", JsonSerializer.Serialize(new { method, path, body }));
        Assert.True(asked.Status == 200, asked.Body);
        using var answer = JsonDocument.Parse(asked.Body);
        return (answer.RootElement.GetProperty("confirmation").GetProperty("id").GetString()!, answer.RootElement.GetProperty("token").GetString()!);
    }

    [Fact]
    public async Task The_real_host_makes_the_exact_request_its_client_asked_escapes_and_body_alike()
    {
        using var host = await RealHost.StartAsync(
            _scratch,
            new Dictionary<string, string?> { [PersonKey.InputVariable] = "1", [InputEndStop.Variable] = "1" },
            input => input.WriteLine(KeyedHost.Key));
        using var client = new HttpClient { Timeout = Poll.Patience };

        // A retire whose name holds a space, sent escaped: the address the client asked for is the one made, and its door reads the
        // name unescaped. Nothing by that name is registered here, so the door answers that it retired nothing.
        const string Retire = "/api/registry/my%20repo";
        Assert.Equal(403, (await SendAsync(client, host.Url, "DELETE", Retire, null)).Status);
        var (retire, retireToken) = await AskedAsync(client, host.Url, "DELETE", Retire, null);
        var retired = await SendAsync(client, host.Url, "POST", $"/api/confirmations/{retire}/confirm", null, person: KeyedHost.Key);
        Assert.True(retired.Status == 200, $"{retired.Status}: {retired.Body}");
        using (var made = JsonDocument.Parse(retired.Body))
        {
            var confirmation = made.RootElement.GetProperty("confirmation");
            Assert.Equal("made", confirmation.GetProperty("state").GetString());
            Assert.Contains("\"repository\":\"my repo\"", confirmation.GetProperty("answer").GetString());
        }

        // An ask with a body: made at the Confirm, the door binds it whole. This machine holds no workspace, so the door's own
        // refusal names the one the body named, and that is the reason its asker reads.
        const string Body = """{"workspace":"default","sentence":"Rename the totals column."}""";
        var (second, secondToken) = await AskedAsync(client, host.Url, "POST", "/api/asks", Body);
        var answered = await SendAsync(client, host.Url, "POST", $"/api/confirmations/{second}/confirm", null, person: KeyedHost.Key);
        Assert.True(answered.Status == 409, $"{answered.Status}: {answered.Body}");
        Assert.Contains("`default` is not a workspace this machine holds", answered.Body);
        var polled = await SendAsync(client, host.Url, "GET", $"/api/confirmations/{second}", null);
        using (var stale = JsonDocument.Parse(polled.Body))
        {
            Assert.Equal("stale", stale.RootElement.GetProperty("state").GetString());
            Assert.Contains("`default` is not a workspace this machine holds", stale.RootElement.GetProperty("reason").GetString());
        }

        host.Process.StandardInput.Close();
        Assert.True(host.Process.WaitForExit(Poll.Patience), "the host did not stop when its input closed");
        Assert.All(host.LogLines(), line =>
        {
            Assert.DoesNotContain(retireToken, line);
            Assert.DoesNotContain(secondToken, line);
            Assert.DoesNotContain(KeyedHost.Key, line);
        });
    }
}
