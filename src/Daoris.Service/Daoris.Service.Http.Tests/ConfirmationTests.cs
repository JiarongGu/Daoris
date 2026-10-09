using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Daoris.Knowledge;
using Daoris.Knowledge.Http;

namespace Daoris.Service.Http.Tests;

/// <summary>A clock a test moves, so two minutes pass without waiting them (PERSONDOOR1b).</summary>
public sealed class MovableClock : TimeProvider
{
    public DateTimeOffset Now { get; private set; } = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);

    public void Move(TimeSpan by) => Now += by;

    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>
/// PERSONDOOR1b (D156 point 4; the person-door design §4.2): a terminal's keyless call to a person's or the driver's door
/// is refused, and the client asks the window: <c>POST /api/confirmations</c> with the exact request and its secret's
/// SHA-256. The window lists what waits by the door's name and the request's own fields, and the person confirms or
/// refuses it with the key. A confirmation is a grant used once, for that request alone, within two minutes of its ask;
/// a changed body, another address, a second use, a refusal and expiry are each refused. At most five wait.
/// </summary>
public sealed class ConfirmationTests
{
    private const string Key = KeyedHost.Key;

    private static (DaorisHost Host, MovableClock Clock) Keyed()
    {
        var clock = new MovableClock();
        return (new DaorisHost(ServiceMode.Local, seed: LocalHost.Seed, input: Key + "\n", clock: clock), clock);
    }

    /// <summary>A secret as a client makes one: 32 random bytes in base64url, a person key's form.</summary>
    private static string NewSecret() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    private static string HashOf(string secret) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    private static Task<Answer> AskAsync(DaorisHost host, string method, string path, string? body, string secret) =>
        host.PostAsync("/api/confirmations", new { method, path, body, secretSha256 = HashOf(secret) });

    /// <summary>Asks, and answers the confirmation's id; the ask must be taken.</summary>
    private static async Task<string> AskedAsync(DaorisHost host, string method, string path, string? body, string secret)
    {
        var asked = await AskAsync(host, method, path, body, secret);
        Assert.True(asked.Status == 200, $"the ask answered {asked.Status}: {asked.Body}");
        return asked.Json.GetProperty("confirmation").GetProperty("id").GetString()!;
    }

    private static Task<Answer> ConfirmAsync(DaorisHost host, string id, string? person = Key) =>
        host.SendAsync("POST", $"/api/confirmations/{id}/confirm", DaorisHost.Loopback, person: person);

    private static Task<Answer> RefuseAsync(DaorisHost host, string id, string? person = Key) =>
        host.SendAsync("POST", $"/api/confirmations/{id}/refuse", DaorisHost.Loopback, person: person);

    private static async Task<string> StateAsync(DaorisHost host, string id)
    {
        var polled = await host.GetAsync($"/api/confirmations/{id}");
        Assert.True(polled.Status == 200, $"the poll answered {polled.Status}: {polled.Body}");
        return polled.Json.GetProperty("state").GetString()!;
    }

    private static void AssertRefused(Answer answer, string code, string what)
    {
        Assert.True(answer.Status == 403, $"{what} answered {answer.Status}: {answer.Body}");
        Assert.Equal(code, answer.Json.GetProperty("code").GetString());
    }

    private static List<(string Name, string Value)> Pairs(JsonElement list) =>
        [.. list.EnumerateArray().Select(pair => (pair.GetProperty("name").GetString()!, pair.GetProperty("value").GetString()!))];

    private static async Task<string> PublishAsync(DaorisHost host, string title)
    {
        var published = await host.PostAsync("/api/quests", new { from = "Asker", to = "Keeper", title, body = "Asked by a session." });
        Assert.Equal(200, published.Status);
        return published.Json.GetProperty("quest").GetProperty("id").GetString()!;
    }

    private static async Task<int> AsksAsync(DaorisHost host) =>
        (await host.GetAsync("/api/asks?workspace=default")).Json.GetArrayLength();

    /// <summary>
    /// The whole path (design §4.2): refused, asked, listed, confirmed with the key, then sent again with the grant, which
    /// keeps the act as the person's. A grant before the person confirms is nothing, and a second use is refused.
    /// </summary>
    [Fact]
    public async Task A_refused_call_is_asked_confirmed_and_granted_once()
    {
        var (host, _) = Keyed();
        using var _host = host;
        const string Body = """{"workspace":"default","sentence":"Rename the totals column."}""";

        AssertRefused(await host.SendAsync("POST", "/api/asks", DaorisHost.Loopback, json: Body), PersonDoors.PersonOnly, "a keyless ask");

        var secret = NewSecret();
        var asked = await AskAsync(host, "POST", "/api/asks", Body, secret);
        Assert.Equal(200, asked.Status);
        Assert.Equal(PersonConfirmations.WaitingSentence, asked.Json.GetProperty("message").GetString());
        var card = asked.Json.GetProperty("confirmation");
        var id = card.GetProperty("id").GetString()!;
        Assert.Equal("waiting", card.GetProperty("state").GetString());
        Assert.Equal("POST", card.GetProperty("method").GetString());
        Assert.Equal("/api/asks", card.GetProperty("path").GetString());
        Assert.Equal("/api/asks", card.GetProperty("route").GetString());
        Assert.Equal("an ask", card.GetProperty("act").GetString());
        Assert.Empty(Pairs(card.GetProperty("values")));
        Assert.Equal([("workspace", "default"), ("sentence", "Rename the totals column.")], Pairs(card.GetProperty("fields")));
        Assert.Equal(Body, card.GetProperty("body").GetString());
        Assert.Equal(
            card.GetProperty("asked").GetDateTimeOffset() + TimeSpan.FromMinutes(2), card.GetProperty("expires").GetDateTimeOffset());
        Assert.DoesNotContain(HashOf(secret), asked.Body);

        // What waits, as the window lists it, and as the terminal polls it.
        var waiting = await host.GetAsync("/api/confirmations");
        Assert.Equal(200, waiting.Status);
        Assert.Equal([id], waiting.Json.EnumerateArray().Select(row => row.GetProperty("id").GetString()!).ToArray());
        Assert.Equal("waiting", await StateAsync(host, id));

        // Before the person confirms, the secret grants nothing.
        AssertRefused(await host.SendAsync("POST", "/api/asks", DaorisHost.Loopback, json: Body, grant: secret), PersonDoors.Grant, "an unconfirmed grant");
        Assert.Equal(0, await AsksAsync(host));

        var confirmed = await ConfirmAsync(host, id);
        Assert.Equal(200, confirmed.Status);
        Assert.Equal("confirmed", confirmed.Json.GetProperty("confirmation").GetProperty("state").GetString());
        Assert.Equal("confirmed", await StateAsync(host, id));
        Assert.Equal(0, (await host.GetAsync("/api/confirmations")).Json.GetArrayLength());

        var granted = await host.SendAsync("POST", "/api/asks", DaorisHost.Loopback, json: Body, grant: secret);
        Assert.Equal(200, granted.Status);
        Assert.Equal("Rename the totals column.", granted.Json.GetProperty("ask").GetProperty("sentence").GetString());
        Assert.Equal("used", await StateAsync(host, id));

        var again = await host.SendAsync("POST", "/api/asks", DaorisHost.Loopback, json: Body, grant: secret);
        AssertRefused(again, PersonDoors.Grant, "a second use");
        Assert.Equal(PersonDoors.GrantSentence, again.Error);
        Assert.Equal(1, await AsksAsync(host));
    }

    /// <summary>
    /// The grant is for that request alone (design §4.2): its words changed by a byte, another quest, another query,
    /// another method or another door are each refused, and none of them uses it up, so the exact request is answered.
    /// </summary>
    [Fact]
    public async Task A_changed_body_or_address_is_refused_and_the_exact_request_is_granted()
    {
        var (host, _) = Keyed();
        using var _host = host;
        var quest = await PublishAsync(host, "A quest the person closes by hand");
        var other = await PublishAsync(host, "Another quest");
        const string Body = """{"note":"Finished by hand."}""";
        var secret = NewSecret();
        await ConfirmAsync(host, await AskedAsync(host, "POST", $"/api/quests/{quest}/done", Body, secret));

        var wrong = new (string Method, string Path, string? Body, string What)[]
        {
            ("POST", $"/api/quests/{quest}/done", """{"note":"Something else."}""", "a changed body"),
            ("POST", $"/api/quests/{quest}/done", """{ "note": "Finished by hand." }""", "the same words spaced otherwise"),
            ("POST", $"/api/quests/{quest}/done", null, "no body"),
            ("POST", $"/api/quests/{other}/done", Body, "another quest"),
            ("POST", $"/api/quests/{quest}/done?also=1", Body, "another query"),
            ("POST", $"/api/quests/{quest}/accept", Body, "another door"),
            ("DELETE", $"/api/quests/{quest}", null, "another method"),
        };
        foreach (var (method, path, body, what) in wrong)
        {
            AssertRefused(await host.SendAsync(method, path, DaorisHost.Loopback, json: body, grant: secret), PersonDoors.Grant, what);
        }

        Assert.Equal("Open", (await host.GetAsync("/api/quests")).Json.EnumerateArray()
            .Single(row => row.GetProperty("id").GetString() == quest).GetProperty("status").GetString());

        var done = await host.SendAsync("POST", $"/api/quests/{quest}/done", DaorisHost.Loopback, json: Body, grant: secret);
        Assert.Equal(200, done.Status);
        Assert.Equal("Done", done.Json.GetProperty("quest").GetProperty("status").GetString());
    }

    /// <summary>
    /// Two minutes from its ask (design §4.2): a grant confirmed within them is answered within them and refused after; a
    /// confirmation still waiting cannot be confirmed after them; each reads <c>expired</c>, and is forgotten two minutes
    /// later.
    /// </summary>
    [Fact]
    public async Task A_confirmation_lasts_two_minutes_from_its_ask()
    {
        var (host, clock) = Keyed();
        using var _host = host;
        const string Body = """{"workspace":"default","sentence":"Rename the totals column."}""";

        var inTime = NewSecret();
        await ConfirmAsync(host, await AskedAsync(host, "POST", "/api/asks", Body, inTime));
        clock.Move(TimeSpan.FromMinutes(2) - TimeSpan.FromSeconds(1));
        Assert.Equal(200, (await host.SendAsync("POST", "/api/asks", DaorisHost.Loopback, json: Body, grant: inTime)).Status);

        var late = NewSecret();
        var confirmed = await AskedAsync(host, "POST", "/api/asks", Body, late);
        Assert.Equal(200, (await ConfirmAsync(host, confirmed)).Status);
        var unanswered = await AskedAsync(host, "POST", "/api/asks", Body, NewSecret());
        clock.Move(TimeSpan.FromMinutes(2));

        AssertRefused(await host.SendAsync("POST", "/api/asks", DaorisHost.Loopback, json: Body, grant: late), PersonDoors.Grant, "an expired grant");
        Assert.Equal("expired", await StateAsync(host, confirmed));
        var tooLate = await ConfirmAsync(host, unanswered);
        Assert.Equal(409, tooLate.Status);
        Assert.Equal(PersonConfirmations.ExpiredSentence, tooLate.Error);
        Assert.Equal("expired", await StateAsync(host, unanswered));
        Assert.Equal(0, (await host.GetAsync("/api/confirmations")).Json.GetArrayLength());
        Assert.Equal(1, await AsksAsync(host));

        clock.Move(TimeSpan.FromMinutes(2));
        Assert.Equal(404, (await host.GetAsync($"/api/confirmations/{confirmed}")).Status);
        Assert.Equal(404, (await ConfirmAsync(host, unanswered)).Status);
    }

    /// <summary>
    /// At most five wait for the person (design §4.2): the sixth is refused <c>confirmations-full</c> with the design's
    /// sentence, and asks nothing. A confirmation the person answered waits no more, so its place is free again.
    /// </summary>
    [Fact]
    public async Task The_sixth_ask_is_refused_and_asks_nothing()
    {
        var (host, _) = Keyed();
        using var _host = host;
        var ids = new List<string>();
        for (var n = 1; n <= PersonConfirmations.Most; n++)
        {
            ids.Add(await AskedAsync(host, "POST", "/api/asks", $$"""{"workspace":"default","sentence":"Ask number {{n}}."}""", NewSecret()));
        }

        var sixth = await AskAsync(host, "POST", "/api/asks", """{"workspace":"default","sentence":"The sixth."}""", NewSecret());
        AssertRefused(sixth, PersonDoors.ConfirmationsFull, "a sixth ask");
        Assert.Equal(PersonDoors.FullSentence, sixth.Error);
        Assert.Equal(5, (await host.GetAsync("/api/confirmations")).Json.GetArrayLength());

        Assert.Equal(200, (await RefuseAsync(host, ids[0])).Status);
        await AskedAsync(host, "POST", "/api/asks", """{"workspace":"default","sentence":"The sixth, again."}""", NewSecret());
        Assert.Equal(200, (await ConfirmAsync(host, ids[1])).Status);
        await AskedAsync(host, "POST", "/api/asks", """{"workspace":"default","sentence":"A seventh."}""", NewSecret());
        Assert.Equal(5, (await host.GetAsync("/api/confirmations")).Json.GetArrayLength());
    }

    /// <summary>
    /// The person's refusal (design §4.2, *its default is no*): the grant is nothing after it, and the confirmation cannot
    /// be confirmed after it.
    /// </summary>
    [Fact]
    public async Task A_refused_confirmation_grants_nothing()
    {
        var (host, _) = Keyed();
        using var _host = host;
        const string Body = """{"workspace":"default","sentence":"Ship it unreviewed."}""";
        var secret = NewSecret();
        var id = await AskedAsync(host, "POST", "/api/asks", Body, secret);

        var refused = await RefuseAsync(host, id);
        Assert.Equal(200, refused.Status);
        Assert.Equal("refused", refused.Json.GetProperty("confirmation").GetProperty("state").GetString());
        Assert.Equal("refused", await StateAsync(host, id));

        AssertRefused(await host.SendAsync("POST", "/api/asks", DaorisHost.Loopback, json: Body, grant: secret), PersonDoors.Grant, "a refused grant");
        var confirmedAfter = await ConfirmAsync(host, id);
        Assert.Equal(409, confirmedAfter.Status);
        Assert.Equal(PersonConfirmations.RefusedSentence, confirmedAfter.Error);
        Assert.Equal(0, await AsksAsync(host));
    }

    /// <summary>
    /// A session can ask, and cannot answer (design §4.2): confirming or refusing without the key is refused as the
    /// person's door, a key from another start as stale, and a grant presented there as a grant for another act. The
    /// confirmation still waits.
    /// </summary>
    [Fact]
    public async Task A_keyless_confirm_or_refuse_is_refused_and_the_confirmation_still_waits()
    {
        var (host, _) = Keyed();
        using var _host = host;
        var id = await AskedAsync(host, "POST", "/api/asks", """{"workspace":"default","sentence":"Rename it."}""", NewSecret());

        var keyless = await ConfirmAsync(host, id, person: null);
        AssertRefused(keyless, PersonDoors.PersonOnly, "a keyless confirm");
        Assert.Equal(PersonDoors.PersonSentence("the confirmation of a terminal's act"), keyless.Error);
        var keylessRefusal = await RefuseAsync(host, id, person: null);
        AssertRefused(keylessRefusal, PersonDoors.PersonOnly, "a keyless refusal");
        Assert.Equal(PersonDoors.PersonSentence("the refusal of a terminal's act"), keylessRefusal.Error);
        AssertRefused(await ConfirmAsync(host, id, person: KeyedHost.Earlier), PersonDoors.Stale, "an earlier key's confirm");

        // A grant the person confirmed for another act is no key here.
        var secret = NewSecret();
        await ConfirmAsync(host, await AskedAsync(host, "POST", "/api/asks", """{"workspace":"default","sentence":"Other."}""", secret));
        AssertRefused(
            await host.SendAsync("POST", $"/api/confirmations/{id}/confirm", DaorisHost.Loopback, grant: secret), PersonDoors.Grant, "a grant at confirm");

        Assert.Equal("waiting", await StateAsync(host, id));
    }

    /// <summary>
    /// Only a door that wants the key can be asked for, in the very form that wants it (design §3.2): a read, an agent's
    /// door, the confirmations' own doors and an address no door answers are refused, and so are a body that is no JSON
    /// object and a secret's hash that is not one. Each refusal is a 400 and keeps nothing.
    /// </summary>
    [Fact]
    public async Task Only_a_door_that_wants_the_key_can_be_asked_for()
    {
        var (host, _) = Keyed();
        using var _host = host;
        var refused = new (string Method, string Path, string? Body, string? Hash, string What)[]
        {
            ("GET", "/api/quests", null, null, "a read"),
            ("POST", "/api/quests", """{"from":"Asker","to":"Keeper","title":"t","body":"b"}""", null, "an agent's publish"),
            ("POST", "/api/quests/7/respond", """{"action":"take"}""", null, "a take"),
            ("POST", "/api/quests/7/respond", """{"action":"decline","reason":"r","whileOpen":false}""", null, "a decline while open is false"),
            ("POST", "/api/confirmations/abc/confirm", null, null, "a confirmation's confirm"),
            ("POST", "/api/confirmations/abc/refuse", null, null, "a confirmation's refusal"),
            ("POST", "/api/confirmations", "{}", null, "an ask"),
            ("POST", "/api/nothing-here", "{}", null, "an address no door answers"),
            ("POST", "/api/quests/operations", "{}", null, "a shared host's door"),
            ("POST", "/api/quests/7/done/more", "{}", null, "a path longer than any door's"),
            ("POST", "http://localhost:5177/api/asks", "{}", null, "an absolute address"),
            ("POST", "/api/asks", "[1, 2]", null, "a body that is no object"),
            ("POST", "/api/asks", "not json", null, "a body that is no JSON"),
            ("POST", "/api/asks", "{}", "abc", "a hash that is not one"),
            ("POST", "/api/asks", "{}", new string('G', 64), "a hash that is not hex"),
            ("PATCH", "/api/asks", "{}", null, "a method no door takes"),
            ("", "/api/asks", "{}", null, "no method"),
            ("POST", "", "{}", null, "no path"),
        };
        foreach (var (method, path, body, hash, what) in refused)
        {
            var answer = await host.PostAsync(
                "/api/confirmations", new { method, path, body, secretSha256 = hash ?? HashOf(NewSecret()) });
            Assert.True(answer.Status == 400, $"{what} answered {answer.Status}: {answer.Body}");
            Assert.EndsWith("Nothing was asked.", answer.Error);
        }

        Assert.Contains("is no door of this service", (await AskAsync(host, "POST", "/api/quests/operations", "{}", NewSecret())).Error);
        Assert.Contains("A quest's publish is answered without the person's key", (await AskAsync(
            host, "POST", "/api/quests", """{"from":"Asker","to":"Keeper","title":"t","body":"b"}""", NewSecret())).Error);

        var noHash = await host.PostAsync("/api/confirmations", new { method = "POST", path = "/api/asks", body = "{}" });
        Assert.Equal(400, noHash.Status);
        Assert.Equal(0, (await host.GetAsync("/api/confirmations")).Json.GetArrayLength());

        // A hash already asked is asked once: the grant is found by it.
        var secret = NewSecret();
        await AskedAsync(host, "POST", "/api/asks", "{}", secret);
        Assert.Equal(400, (await AskAsync(host, "POST", "/api/asks", """{"sentence":"x"}""", secret)).Status);

        // The forms are named as the door names them; the driver's doors are asked for too, the query among their values.
        var forms = new (string Method, string Path, string Body, string Act)[]
        {
            ("POST", "/api/quests/7/respond", """{"action":"decline","reason":"Abandoned.","whileOpen":true}""", "an abandon's decline"),
            ("POST", "/api/quests/7/set-up", """{"commit":"abc","kind":"local","session":"s-1"}""", PersonDoors.SetUp.Act!),
            ("POST", "/api/quests/7/set-up", """{"commit":"abc","kind":"local","look":"http://localhost:5173"}""", "a set-up of their own"),
            ("post", "/api/sync?workspace=default", "", "a sync pass"),
        };
        foreach (var (method, path, body, act) in forms)
        {
            var asked = await AskAsync(host, method, path, body, NewSecret());
            Assert.True(asked.Status == 200, $"{method} {path} answered {asked.Status}: {asked.Body}");
            Assert.Equal(act, asked.Json.GetProperty("confirmation").GetProperty("act").GetString());
        }

        var sync = (await host.GetAsync("/api/confirmations")).Json.EnumerateArray().Last();
        Assert.Equal("POST", sync.GetProperty("method").GetString());
        Assert.Equal("/api/sync", sync.GetProperty("route").GetString());
        Assert.Equal([("workspace", "default")], Pairs(sync.GetProperty("values")));
        Assert.Empty(Pairs(sync.GetProperty("fields")));
        Assert.False(sync.TryGetProperty("body", out _));
    }

    /// <summary>
    /// The window shows the request by the door's name and its own fields, never a sentence the asker wrote (design §4.2):
    /// every value the body carries, nested ones named by their path, so nothing a grant would carry is out of the
    /// person's sight; the address's values beside them.
    /// </summary>
    [Fact]
    public async Task The_card_names_the_door_and_every_field_the_request_carries()
    {
        var (host, _) = Keyed();
        using var _host = host;

        var review = await AskAsync(
            host, "POST", "/api/quests/12/review",
            """{"verdict":"reviewed","words":"Looks right.","setUp":{"machine":"m-1","sequence":3},"extra":[true,null,{"deep":"x"}]}""",
            NewSecret());
        Assert.Equal(200, review.Status);
        var card = review.Json.GetProperty("confirmation");
        Assert.Equal("a review's verdict", card.GetProperty("act").GetString());
        Assert.Equal("/api/quests/{id}/review", card.GetProperty("route").GetString());
        Assert.Equal([("id", "12")], Pairs(card.GetProperty("values")));
        Assert.Equal(
            [
                ("verdict", "reviewed"), ("words", "Looks right."), ("setUp.machine", "m-1"), ("setUp.sequence", "3"),
                ("extra[0]", "true"), ("extra[1]", "null"), ("extra[2].deep", "x"),
            ],
            Pairs(card.GetProperty("fields")));

        var cleared = await AskAsync(host, "POST", "/api/history/clear", """{"units":[{"kind":"quest","id":"3"}],"empty":{},"none":[]}""", NewSecret());
        Assert.Equal(
            [("units[0].kind", "quest"), ("units[0].id", "3"), ("empty", "{}"), ("none", "[]")],
            Pairs(cleared.Json.GetProperty("confirmation").GetProperty("fields")));

        var deleted = await AskAsync(host, "DELETE", "/api/registry/my%20repo", null, NewSecret());
        var row = deleted.Json.GetProperty("confirmation");
        Assert.Equal("a repository's retire", row.GetProperty("act").GetString());
        Assert.Equal("/api/registry/my%20repo", row.GetProperty("path").GetString());
        Assert.Equal([("repository", "my repo")], Pairs(row.GetProperty("values")));
    }

    /// <summary>
    /// A grant is read from a caller on this machine only, as the key is (design §2.2): one that arrives from anywhere else
    /// is no grant, and its call is judged keyless; it does not use the grant up. A grant at an agent's door, where no
    /// confirmation can be asked, is refused rather than taken as an agent's call.
    /// </summary>
    [Fact]
    public async Task A_grant_from_off_this_machine_is_none_and_one_at_an_agents_door_is_refused()
    {
        var (host, _) = Keyed();
        using var _host = host;
        const string Body = """{"workspace":"default","sentence":"Rename the totals column."}""";
        var secret = NewSecret();
        await ConfirmAsync(host, await AskedAsync(host, "POST", "/api/asks", Body, secret));

        AssertRefused(await host.SendAsync("POST", "/api/asks", DaorisHost.OffMachine, json: Body, grant: secret), PersonDoors.PersonOnly, "a grant from off the machine");

        var publish = """{"from":"Asker","to":"Keeper","title":"t","body":"b"}""";
        AssertRefused(await host.SendAsync("POST", "/api/quests", DaorisHost.Loopback, json: publish, grant: secret), PersonDoors.Grant, "a grant at a publish");
        Assert.Equal(0, (await host.GetAsync("/api/quests")).Json.GetArrayLength());

        // A read is answered whatever was presented, and leaves the grant as it was.
        Assert.Equal(200, (await host.SendAsync("GET", "/api/quests", DaorisHost.Loopback, grant: secret)).Status);
        Assert.Equal(200, (await host.SendAsync("POST", "/api/asks", DaorisHost.Loopback, json: Body, grant: secret)).Status);
    }

    /// <summary>
    /// The machine log (design §5.1): <c>person.confirmation</c> once as each is asked, confirmed, refused or expires, by
    /// the door's method and pattern; a refused grant and a sixth ask are a <c>person.refused</c> line each. Never the
    /// secret, its hash, the body's words or the id in the path; and nothing at all when a grant is used.
    /// </summary>
    [Fact]
    public async Task The_log_says_each_answer_by_its_door_and_never_the_secret_or_the_words()
    {
        var (host, clock) = Keyed();
        using var _host = host;
        const string Words = "words-only-the-body-holds";
        var body = $$"""{"note":"{{Words}}"}""";
        var secrets = Enumerable.Range(0, 3).Select(_ => NewSecret()).ToArray();

        var used = await AskedAsync(host, "POST", "/api/quests/id-in-the-path/done", body, secrets[0]);
        await ConfirmAsync(host, used);
        Assert.NotEqual(403, (await host.SendAsync("POST", "/api/quests/id-in-the-path/done", DaorisHost.Loopback, json: body, grant: secrets[0])).Status);
        await RefuseAsync(host, await AskedAsync(host, "DELETE", "/api/quests/id-in-the-path", null, secrets[1]));
        await AskedAsync(host, "POST", "/api/quests/id-in-the-path/review", body, secrets[2]);
        for (var n = 0; n < PersonConfirmations.Most - 1; n++) await AskedAsync(host, "POST", "/api/asks", body, NewSecret());
        AssertRefused(await AskAsync(host, "POST", "/api/asks", body, NewSecret()), PersonDoors.ConfirmationsFull, "a sixth");
        AssertRefused(await host.SendAsync("POST", "/api/quests/id-in-the-path/done", DaorisHost.Loopback, json: body, grant: secrets[1]), PersonDoors.Grant, "a refused grant");
        clock.Move(TimeSpan.FromMinutes(2));
        Assert.Equal(0, (await host.GetAsync("/api/confirmations")).Json.GetArrayLength());

        var lines = host.LogLines();
        static JsonElement Parse(string line) => JsonDocument.Parse(line).RootElement;
        var confirmations = lines.Select(Parse).Where(line => line.GetProperty("event").GetString() == PersonConfirmations.Event)
            .Select(line => line.GetProperty("data"))
            .Select(data => (data.GetProperty("state").GetString()!, data.GetProperty("method").GetString()!, data.GetProperty("route").GetString()!))
            .ToList();
        Assert.Equal(
            [
                ("asked", "POST", "/api/quests/{id}/done"), ("confirmed", "POST", "/api/quests/{id}/done"),
                ("asked", "DELETE", "/api/quests/{id}"), ("refused", "DELETE", "/api/quests/{id}"),
                ("asked", "POST", "/api/quests/{id}/review"),
                ("asked", "POST", "/api/asks"), ("asked", "POST", "/api/asks"), ("asked", "POST", "/api/asks"), ("asked", "POST", "/api/asks"),
                ("expired", "POST", "/api/quests/{id}/review"),
                ("expired", "POST", "/api/asks"), ("expired", "POST", "/api/asks"), ("expired", "POST", "/api/asks"), ("expired", "POST", "/api/asks"),
            ],
            confirmations);

        var refused = lines.Select(Parse).Where(line => line.GetProperty("event").GetString() == PersonDoors.Event)
            .Select(line => line.GetProperty("data"))
            .Select(data => (data.GetProperty("route").GetString()!, data.GetProperty("presented").GetString()!, data.GetProperty("code").GetString()!))
            .ToList();
        Assert.Equal(
            [("/api/confirmations", "none", PersonDoors.ConfirmationsFull), ("/api/quests/{id}/done", "grant", PersonDoors.Grant)],
            refused);

        Assert.All(lines, line =>
        {
            foreach (var secret in secrets)
            {
                Assert.DoesNotContain(secret, line);
                Assert.DoesNotContain(HashOf(secret), line);
            }

            Assert.DoesNotContain(Words, line);
            Assert.DoesNotContain("id-in-the-path", line);
            Assert.DoesNotContain(used, line);
        });
    }

    /// <summary>
    /// A host handed no key gates nothing (design §2.1), so nothing it answers waits for a confirmation: an ask is refused
    /// with a sentence and keeps nothing, none waits, and a grant presented to it changes nothing about its answer.
    /// </summary>
    [Fact]
    public async Task A_host_without_a_key_asks_nothing_and_answers_as_before()
    {
        using var host = new LocalHost();
        const string Body = """{"workspace":"default","sentence":"Rename the totals column."}""";

        var asked = await AskAsync(host, "POST", "/api/asks", Body, NewSecret());
        Assert.Equal(409, asked.Status);
        Assert.Equal(PersonConfirmations.NoKeySentence, asked.Error);
        Assert.Equal(0, (await host.GetAsync("/api/confirmations")).Json.GetArrayLength());
        Assert.Equal(404, (await host.GetAsync("/api/confirmations/abc")).Status);

        Assert.Equal(200, (await host.SendAsync("POST", "/api/asks", DaorisHost.Loopback, json: Body, grant: NewSecret())).Status);
        Assert.Equal(200, (await host.SendAsync("POST", "/api/asks", DaorisHost.Loopback, json: Body, grant: "not a secret")).Status);
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
/// The real host, as a terminal reaches it (PERSONDOOR1b): the path a client sends, escapes and all, and a body read twice,
/// once by the grant and once by the door. What the in-process host cannot show: Kestrel's own reading of the address.
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
        HttpClient client, string url, string method, string path, string? body, string? person = null, string? grant = null)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), url + path);
        if (body is not null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        if (person is not null) request.Headers.Add(PersonKey.Header, person);
        if (grant is not null) request.Headers.Add(PersonConfirmations.GrantHeader, grant);
        using var response = await client.SendAsync(request);
        return ((int)response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task The_real_host_grants_the_exact_request_its_client_sent_escapes_and_body_alike()
    {
        using var host = await RealHost.StartAsync(
            _scratch,
            new Dictionary<string, string?> { [PersonKey.InputVariable] = "1", [InputEndStop.Variable] = "1" },
            input => input.WriteLine(KeyedHost.Key));
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        // A retire whose name holds a space, sent escaped: the address the client asked for is the one it sends.
        const string Retire = "/api/registry/my%20repo";
        Assert.Equal(403, (await SendAsync(client, host.Url, "DELETE", Retire, null)).Status);
        var secret = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
        var asked = await SendAsync(
            client, host.Url, "POST", "/api/confirmations", JsonSerializer.Serialize(new { method = "DELETE", path = Retire, secretSha256 = hash }));
        Assert.True(asked.Status == 200, asked.Body);
        var id = JsonDocument.Parse(asked.Body).RootElement.GetProperty("confirmation").GetProperty("id").GetString()!;
        Assert.Equal(200, (await SendAsync(client, host.Url, "POST", $"/api/confirmations/{id}/confirm", null, person: KeyedHost.Key)).Status);

        // Past the gate, the door answers for itself: nothing by that name is registered here.
        var retired = await SendAsync(client, host.Url, "DELETE", Retire, null, grant: secret);
        Assert.True(retired.Status is not 403 and < 500, $"{retired.Status}: {retired.Body}");
        Assert.Equal(403, (await SendAsync(client, host.Url, "DELETE", Retire, null, grant: secret)).Status);

        // An ask with a body: the grant reads it, and the door binds it after. This machine holds no workspace, so the door's
        // own refusal names the one the body named: the body reached it whole.
        const string Body = """{"workspace":"default","sentence":"Rename the totals column."}""";
        var second = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        var askedAgain = await SendAsync(
            client, host.Url, "POST", "/api/confirmations",
            JsonSerializer.Serialize(new { method = "POST", path = "/api/asks", body = Body, secretSha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(second))) }));
        var secondId = JsonDocument.Parse(askedAgain.Body).RootElement.GetProperty("confirmation").GetProperty("id").GetString()!;
        await SendAsync(client, host.Url, "POST", $"/api/confirmations/{secondId}/confirm", null, person: KeyedHost.Key);
        var answered = await SendAsync(client, host.Url, "POST", "/api/asks", Body, grant: second);
        Assert.True(answered.Status == 404, $"{answered.Status}: {answered.Body}");
        Assert.Contains("`default` is not a workspace this machine holds", answered.Body);

        host.Process.StandardInput.Close();
        Assert.True(host.Process.WaitForExit(15_000), "the host did not stop when its input closed");
        Assert.All(host.LogLines(), line =>
        {
            Assert.DoesNotContain(secret, line);
            Assert.DoesNotContain(hash, line);
        });
    }
}
