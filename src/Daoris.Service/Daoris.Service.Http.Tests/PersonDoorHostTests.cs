using System.Text.Json;
using Daoris.Knowledge;
using Daoris.Knowledge.Http;

namespace Daoris.Service.Http.Tests;

/// <summary>A local host handed its person key on its input (PERSONDOOR1a), over the same two repositories as <see cref="LocalHost"/>.</summary>
public sealed class KeyedHost() : DaorisHost(ServiceMode.Local, seed: LocalHost.Seed, input: Key + "\n")
{
    /// <summary>This start's key.</summary>
    public const string Key = PersonKeyTests.Counting;

    /// <summary>A key from another start: a key, and not this one's.</summary>
    public const string Earlier = PersonKeyTests.Zeros;
}

/// <summary>
/// PERSONDOOR1a (D156 points 3 and 5; the person-door design §3, §5): a local host handed its key gates its writes by the
/// route's class. A read is answered to anyone. An agent's door answers a keyless call and judges it as an agent's. The
/// driver's own and the person's alone want the key, and a call without it is refused 403 with a sentence and a code,
/// never a 500, and nothing is kept. A key from another start is refused at every door but a read. Before this, one
/// <c>curl</c> from any session's shell kept a skip of a review, a yes or a go-ahead's answer as the person's.
/// </summary>
public sealed class PersonDoorHostTests(KeyedHost host) : IClassFixture<KeyedHost>
{
    private const string Key = KeyedHost.Key;
    private const string Commit = "a1b2c3d4e5f60718293a4b5c6d7e8f9012345678";

    private static void AssertRefused(Answer answer, string code, string sentence, string what)
    {
        Assert.True(answer.Status == 403, $"{what} answered {answer.Status}: {answer.Body}");
        Assert.Equal(code, answer.Json.GetProperty("code").GetString());
        Assert.Equal(sentence, answer.Error);
    }

    private static JsonElement QuestOf(Answer answer) => answer.Json.GetProperty("quest");

    private async Task<JsonElement> QuestAsync(string quest) =>
        (await host.GetAsync("/api/quests?includeClosed=true")).Json.EnumerateArray()
            .Single(row => row.GetProperty("id").GetString() == quest);

    /// <summary>An agent's publish, as a session's <c>curl</c> or a stub makes it: no key.</summary>
    private async Task<string> PublishAsync(string title)
    {
        var published = await host.PostAsync("/api/quests", new { from = "Asker", to = "Keeper", title, body = "Asked by a session." });
        Assert.Equal(200, published.Status);
        return QuestOf(published).GetProperty("id").GetString()!;
    }

    /// <summary>
    /// The build quest, published, taken and closed done at the agent's doors, and the set-up step its close published: its
    /// id. Every move here is one a session makes, keyless.
    /// </summary>
    private async Task<string> SetUpStepAsync(string title)
    {
        var published = await host.PostAsync("/api/quests", new
        {
            from = "Asker", to = "Keeper", title, body = "For the weekly report.",
            then = new[] { new { to = "Keeper", title = "Show {parent} in local for review", body = "Set it up.", setUpIn = "local" } },
        });
        Assert.Equal(200, published.Status);
        var build = QuestOf(published).GetProperty("id").GetString()!;
        Assert.Equal(200, (await host.PostAsync($"/api/quests/{build}/respond", new { action = "take" })).Status);
        Assert.Equal(200, (await host.PostAsync($"/api/quests/{build}/respond", new { action = "done", reason = "Built." })).Status);
        return (await host.GetAsync("/api/quests?includeClosed=true")).Json.EnumerateArray()
            .Single(row => row.TryGetProperty("parent", out var parent) && parent.GetString() == build)
            .GetProperty("id").GetString()!;
    }

    /// <summary>
    /// Every route the host maps, by its own route table and its class: keyless, with this start's key, and with a key from
    /// another start. A door added tomorrow is classed by the table's test, and refused here as the person's until it is.
    /// </summary>
    [Fact]
    public async Task Every_route_answers_by_its_class_keyless_keyed_and_with_an_earlier_key()
    {
        var routes = host.Routes();
        Assert.True(routes.Count >= 50, $"only {routes.Count} routes were enumerated");

        var wrong = new List<string>();
        foreach (var (method, pattern) in routes)
        {
            var door = PersonDoors.Find(method, pattern);
            Assert.True(door is not null, $"{method} {pattern} is in no class");
            var path = PageRequests.Concrete(pattern);
            var body = method == "GET" || method == "DELETE" ? null : "{}";

            // Keyless: a read and an agent's door are answered; the set-up's `{}` names no session, the person's own form.
            var (code, sentence) = door.Class switch
            {
                DoorClass.Open or DoorClass.Agent => ((string?)null, (string?)null),
                DoorClass.Driver when door.Form is not null => (PersonDoors.PersonOnly, PersonDoors.PersonSentence(door.Form.Act)),
                DoorClass.Driver => (PersonDoors.DriverOnly, PersonDoors.DriverSentence(door.Act!)),
                _ => (PersonDoors.PersonOnly, PersonDoors.PersonSentence(door.Act!)),
            };
            var keyless = await host.SendAsync(method, path, DaorisHost.Loopback, json: body);
            if (code is null)
            {
                if (keyless.Status is 403 or >= 500) wrong.Add($"keyless {method} {pattern}: {keyless.Status} {keyless.Body}");
            }
            else if (keyless.Status != 403 || keyless.Json.GetProperty("code").GetString() != code || keyless.Error != sentence)
            {
                wrong.Add($"keyless {method} {pattern}: {keyless.Status} {keyless.Body}");
            }

            var keyed = await host.SendAsync(method, path, DaorisHost.Loopback, json: body, person: Key);
            if (keyed.Status is 403 or >= 500) wrong.Add($"keyed {method} {pattern}: {keyed.Status} {keyed.Body}");

            var earlier = await host.SendAsync(method, path, DaorisHost.Loopback, json: body, person: KeyedHost.Earlier);
            if (door.Class == DoorClass.Open)
            {
                if (earlier.Status is 403 or >= 500) wrong.Add($"earlier key {method} {pattern}: {earlier.Status} {earlier.Body}");
            }
            else if (earlier.Status != 403 || earlier.Json.GetProperty("code").GetString() != PersonDoors.Stale)
            {
                wrong.Add($"earlier key {method} {pattern}: {earlier.Status} {earlier.Body}");
            }
        }

        Assert.True(wrong.Count == 0, string.Join("\n", wrong));
    }

    /// <summary>
    /// 🔴 The case (design §0): a session's shell skips the review of a set-up step. Refused, and the step still waits for
    /// the person; their own skip, with the key, lets it go.
    /// </summary>
    [Fact]
    public async Task A_sessions_skip_of_a_review_is_refused_and_the_persons_is_kept()
    {
        var step = await SetUpStepAsync("Add the export button");
        Assert.Equal(200, (await host.PostAsync(
            $"/api/quests/{step}/set-up", new { commit = Commit, kind = "local", look = "http://localhost:5173/reports" }, person: Key)).Status);
        Assert.Equal(200, (await host.PostAsync($"/api/quests/{step}/done", new { }, person: Key)).Status);
        Assert.True((await QuestAsync(step)).GetProperty("held").GetBoolean());

        AssertRefused(
            await host.PostAsync($"/api/quests/{step}/review", new { verdict = "skipped" }),
            PersonDoors.PersonOnly, PersonDoors.PersonSentence("a review's verdict"), "a keyless skip");
        Assert.True((await QuestAsync(step)).GetProperty("held").GetBoolean());
        Assert.False((await QuestAsync(step)).TryGetProperty("verdicts", out _));

        var skipped = await host.PostAsync($"/api/quests/{step}/review", new { verdict = "skipped", words = "a wording change" }, person: Key);
        Assert.Equal(200, skipped.Status);
        Assert.False(QuestOf(skipped).GetProperty("held").GetBoolean());
    }

    /// <summary>
    /// The yes to a departure (D133 §4) is the person's: a session that closed its quest with a departure cannot give it to
    /// itself. The ask is the person's words, and is made with the key; the publish, take and done are a session's.
    /// </summary>
    [Fact]
    public async Task A_sessions_yes_to_its_own_departure_is_refused_and_the_persons_is_kept()
    {
        var asked = await host.PostAsync("/api/asks", new { workspace = "default", sentence = "Build the weekly report through the v3 bridge." }, person: Key);
        Assert.Equal(200, asked.Status);
        var ask = asked.Json.GetProperty("ask").GetProperty("id").GetString()!;
        var published = await host.PostAsync($"/api/asks/{ask}/publish", new
        {
            to = "Keeper", title = "Build the weekly report", body = "Reached through the bridge.",
            requirements = new[] { new { quote = "through the v3 bridge", check = "It opens through the bridge's route." } },
        });
        Assert.Equal(200, published.Status);
        var quest = QuestOf(published).GetProperty("id").GetString()!;
        Assert.Equal(200, (await host.PostAsync($"/api/quests/{quest}/respond", new { action = "take" })).Status);
        var closed = await host.PostAsync($"/api/quests/{quest}/respond", new
        {
            action = "done", reason = "Built.",
            answers = new object[] { new { requirement = 1, departed = "The weekly totals needed a route of their own.", quote = "the v3 bridge" } },
        });
        Assert.True(QuestOf(closed).GetProperty("held").GetBoolean());

        AssertRefused(
            await host.SendAsync("POST", $"/api/quests/{quest}/accept", DaorisHost.Loopback),
            PersonDoors.PersonOnly, PersonDoors.PersonSentence("the yes to a departure"), "a keyless yes");
        Assert.True((await QuestAsync(quest)).GetProperty("held").GetBoolean());

        var accepted = await host.SendAsync("POST", $"/api/quests/{quest}/accept", DaorisHost.Loopback, person: Key);
        Assert.Equal(200, accepted.Status);
        Assert.False(QuestOf(accepted).GetProperty("held").GetBoolean());
    }

    /// <summary>
    /// A keyless publish is an agent's (design §3.2, §5.1): it may publish, and it may not turn a chain's review off, as
    /// REVIEWENV1b3 judges a connector's. With the key the same publish is the person's, and their <c>off</c> stands.
    /// </summary>
    [Fact]
    public async Task A_keyless_publish_is_an_agents_and_a_keyed_one_the_persons()
    {
        var agents = await host.PostAsync("/api/quests", new
        {
            from = "Asker", to = "Keeper", title = "Ship it unreviewed", body = "b", review = new { choice = "off" },
        });
        Assert.Equal(400, agents.Status);
        Assert.Contains("only on the person's own words", agents.Error);

        var persons = await host.PostAsync("/api/quests", new
        {
            from = "Asker", to = "Keeper", title = "Ship it unreviewed", body = "b", review = new { choice = "off" },
        }, person: Key);
        Assert.Equal(200, persons.Status);
        Assert.Equal("off", QuestOf(persons).GetProperty("review").GetProperty("choice").GetString());

        // And an agent's publish with no review is answered, as every stub's is.
        Assert.Equal(200, (await host.PostAsync("/api/quests", new { from = "Asker", to = "Keeper", title = "A plain ask", body = "b" })).Status);
    }

    /// <summary>
    /// The ask's publish door the same way: keyless and naming no session, an agent's, which sets no <c>off</c>; with the
    /// key and no session, the person's own choice of receiver, whose <c>off</c> stands.
    /// </summary>
    [Fact]
    public async Task A_keyless_publish_onto_an_ask_is_an_agents_and_a_keyed_one_the_persons()
    {
        var asked = await host.PostAsync("/api/asks", new { workspace = "default", sentence = "Rename the totals column." }, person: Key);
        var ask = asked.Json.GetProperty("ask").GetProperty("id").GetString()!;

        var agents = await host.PostAsync($"/api/asks/{ask}/publish", new { to = "Keeper", title = "Rename it", body = "b", review = new { choice = "off" } });
        Assert.Equal(409, agents.Status);
        Assert.Contains("only on the person's own words", agents.Error);

        var persons = await host.PostAsync(
            $"/api/asks/{ask}/publish", new { to = "Keeper", title = "Rename it", body = "b", review = new { choice = "off" } }, person: Key);
        Assert.Equal(200, persons.Status);
        Assert.Equal("off", QuestOf(persons).GetProperty("review").GetProperty("choice").GetString());
    }

    /// <summary>
    /// WORKFLOW1e (D157 point 10; the workflow design §4.3): an ask's kind and workflow are the person's, as what a review is set to
    /// is. A session's keyless set is refused and nothing is kept; with the key the person's choice stands, and the composer's
    /// kind and workflow ride the person's own ask.
    /// </summary>
    [Fact]
    public async Task A_sessions_set_of_an_asks_kind_and_workflow_is_refused_and_the_persons_is_kept()
    {
        var asked = await host.PostAsync("/api/asks", new
        {
            workspace = "default", sentence = "Write the guide for the export button.", kind = "docs", workflow = "docs-to-pr",
        }, person: Key);
        Assert.Equal(200, asked.Status);
        var ask = asked.Json.GetProperty("ask").GetProperty("id").GetString()!;
        Assert.Equal("docs", asked.Json.GetProperty("ask").GetProperty("workflowChoices")[0].GetProperty("kind").GetString());

        AssertRefused(
            await host.PostAsync($"/api/asks/{ask}/workflow", new { workflow = "current" }),
            PersonDoors.PersonOnly, PersonDoors.PersonSentence("an ask's kind and workflow"), "a keyless set");
        var kept = (await host.GetAsync($"/api/asks/{ask}")).Json.GetProperty("workflowChoices");
        Assert.Equal(1, kept.GetArrayLength());

        var persons = await host.PostAsync($"/api/asks/{ask}/workflow", new { workflow = "current", words = "no pull request for this one" }, person: Key);
        Assert.Equal(200, persons.Status);
        var latest = persons.Json.GetProperty("ask").GetProperty("workflowChoices")[1];
        Assert.Equal(("current", "no pull request for this one"), (latest.GetProperty("workflow").GetString(), latest.GetProperty("words").GetString()));
        Assert.False(latest.TryGetProperty("kind", out var kind) && kind.ValueKind != JsonValueKind.Null);
    }

    /// <summary>
    /// The respond door is an agent's (the stubs take and close there), and its abandon's decline is the person's
    /// (PAUSE1c): <c>whileOpen</c> without the key is refused, and the quest stays open.
    /// </summary>
    [Fact]
    public async Task Respond_is_an_agents_door_and_an_abandons_decline_the_persons()
    {
        var taken = await PublishAsync("A quest a session takes");
        Assert.Equal(200, (await host.PostAsync($"/api/quests/{taken}/respond", new { action = "take" })).Status);

        var open = await PublishAsync("A quest the person abandons");
        AssertRefused(
            await host.PostAsync($"/api/quests/{open}/respond", new { action = "decline", reason = "Abandoned.", whileOpen = true }),
            PersonDoors.PersonOnly, PersonDoors.PersonSentence("an abandon's decline"), "a keyless abandon");
        Assert.Equal("Open", (await QuestAsync(open)).GetProperty("status").GetString());

        var declined = await host.PostAsync(
            $"/api/quests/{open}/respond", new { action = "decline", reason = "Abandoned.", whileOpen = true }, person: Key);
        Assert.Equal(200, declined.Status);
        Assert.Equal("Declined", QuestOf(declined).GetProperty("status").GetString());
    }

    /// <summary>
    /// The set-up door has two forms (design §3.2): naming a session, the driver's post of what that session said, with the
    /// commit it read; with where to look, the person's own. Each wants the key, and each refusal names its own form.
    /// </summary>
    [Fact]
    public async Task A_set_up_is_the_drivers_naming_a_session_and_the_persons_with_where_to_look()
    {
        var step = await SetUpStepAsync("Add the import button");

        AssertRefused(
            await host.PostAsync($"/api/quests/{step}/set-up", new { commit = Commit, kind = "local", session = "a-session" }),
            PersonDoors.DriverOnly, PersonDoors.DriverSentence(PersonDoors.SetUp.Act!), "a keyless set-up naming a session");
        AssertRefused(
            await host.PostAsync($"/api/quests/{step}/set-up", new { commit = Commit, kind = "local", look = "http://localhost:5173/import" }),
            PersonDoors.PersonOnly, PersonDoors.PersonSentence("a set-up of their own"), "a keyless set-up of the person's own");
        Assert.False((await QuestAsync(step)).TryGetProperty("setUps", out _));

        var own = await host.PostAsync(
            $"/api/quests/{step}/set-up", new { commit = Commit, kind = "local", look = "http://localhost:5173/import" }, person: Key);
        Assert.Equal(200, own.Status);
        Assert.Equal(1, QuestOf(own).GetProperty("setUps").GetArrayLength());
    }

    /// <summary>
    /// What the driver read for itself is the driver's (D46, D144): a session cannot post its own evidence or its own state,
    /// however it words the body.
    /// </summary>
    [Fact]
    public async Task A_session_cannot_post_its_own_evidence_or_state()
    {
        var quest = await PublishAsync("A quest whose evidence a session would write");

        AssertRefused(
            await host.PostAsync($"/api/quests/{quest}/evidence", new { commit = Commit, how = "read", items = Array.Empty<object>() }),
            PersonDoors.DriverOnly, PersonDoors.DriverSentence("a done's evidence"), "a keyless evidence verdict");
        AssertRefused(
            await host.PostAsync("/api/sessions/a-session/state", new { state = "finished" }),
            PersonDoors.DriverOnly, PersonDoors.DriverSentence("a session's state"), "a keyless state");
    }

    /// <summary>
    /// A key from another start is never this one's (§5.2): refused at the person's, the driver's and an agent's doors
    /// alike, so a caller holding one learns it should ask for the new one rather than being taken for an agent. A read
    /// is answered with it, as with none.
    /// </summary>
    [Fact]
    public async Task A_key_from_an_earlier_start_is_refused_at_every_door_but_a_read()
    {
        var quest = await PublishAsync("A quest an earlier key would accept");

        AssertRefused(
            await host.SendAsync("POST", $"/api/quests/{quest}/accept", DaorisHost.Loopback, person: KeyedHost.Earlier),
            PersonDoors.Stale, PersonDoors.StaleSentence, "an earlier key's yes");
        AssertRefused(
            await host.PostAsync("/api/quests", new { from = "Asker", to = "Keeper", title = "t", body = "b" }, person: KeyedHost.Earlier),
            PersonDoors.Stale, PersonDoors.StaleSentence, "an earlier key's publish");
        AssertRefused(
            await host.PostAsync("/api/quests", new { from = "Asker", to = "Keeper", title = "t", body = "b" }, person: "not a key at all"),
            PersonDoors.Stale, PersonDoors.StaleSentence, "a header that is no key");
        Assert.Equal(200, (await host.GetAsync("/api/quests", person: KeyedHost.Earlier)).Status);
    }

    /// <summary>
    /// The key is read from a caller on this machine only (design §2.2): a local host binds the loopback alone, so a key
    /// that arrives from anywhere else has left the machine, and is no key here. Its call is judged as a keyless one.
    /// </summary>
    [Fact]
    public async Task A_key_from_a_caller_off_this_machine_is_no_key()
    {
        AssertRefused(
            await host.SendAsync("POST", "/api/quests/probe/accept", DaorisHost.OffMachine, person: Key),
            PersonDoors.PersonOnly, PersonDoors.PersonSentence("the yes to a departure"), "a yes from off the machine");
        var agents = await host.SendAsync(
            "POST", "/api/quests", DaorisHost.OffMachine,
            json: JsonSerializer.Serialize(new { from = "Asker", to = "Keeper", title = "t", body = "b", review = new { choice = "off" } }),
            person: Key);
        Assert.Equal(400, agents.Status);
        Assert.Contains("only on the person's own words", agents.Error);

        Assert.Equal(404, (await host.SendAsync("POST", "/api/quests/probe/accept", DaorisHost.Loopback, person: Key)).Status);
    }

    /// <summary>
    /// The shell's proof of possession (design §2.3): asked with a nonce, a host holding a key answers an HMAC of it under
    /// the key, the twin's vector; asked without, its status is what it always was. The key itself is never answered.
    /// </summary>
    [Fact]
    public async Task Status_proves_the_key_when_asked_and_is_unchanged_when_not()
    {
        var proved = await host.GetAsync("/api/status?prove=" + Uri.EscapeDataString("a nonce, 一个"));
        Assert.Equal(200, proved.Status);
        Assert.Equal("gQMDWolsIQRnw-_d19k3A1c9E6ey2aP9-8RL_n_pJ50", proved.Json.GetProperty("proof").GetString());
        Assert.DoesNotContain(Key, proved.Body);

        var plain = await host.GetAsync("/api/status");
        Assert.Equal(
            new[] { "semantic", "tier", "note" },
            plain.Json.EnumerateObject().Select(field => field.Name).ToArray());
    }

    /// <summary>
    /// The page carries the key on its writes (PERSONDOOR1f), and ORIGIN1's gate still stands in front: the shell's page
    /// with the key is answered, without it refused as an agent, and a page elsewhere is refused by its origin even with it.
    /// </summary>
    [Fact]
    public async Task The_shells_page_with_the_key_is_answered_and_a_page_elsewhere_is_refused_by_its_origin()
    {
        var shell = new Dictionary<string, string> { ["Origin"] = DesktopPage.Origin, ["Sec-Fetch-Site"] = "cross-site" };

        Assert.Equal(404, (await PageRequests.SendAsync(
            host, "POST", "/api/quests/probe/accept", new Dictionary<string, string>(shell) { [PersonKey.Header] = Key })).Status);
        Assert.Equal(PersonDoors.PersonOnly, (await PageRequests.SendAsync(host, "POST", "/api/quests/probe/accept", shell))
            .Json.GetProperty("code").GetString());
        Assert.Equal(BrowserOrigins.Code, (await PageRequests.SendAsync(
            host, "POST", "/api/quests/probe/accept",
            new Dictionary<string, string>(PageRequests.CrossSite) { [PersonKey.Header] = Key })).Json.GetProperty("code").GetString());
    }
}

/// <summary>PERSONDOOR1a's other hosts: the log a refusal writes, a host handed no key, and a start that cannot read one.</summary>
public sealed class PersonDoorHostLogTests
{
    private static JsonElement Parse(string line) => JsonDocument.Parse(line).RootElement;

    /// <summary>Runs <paramref name="start"/> with the process's standard error captured, which is where a refusal is said.</summary>
    private static (Exception? Failed, string Said) Capturing(Action start)
    {
        var said = new StringWriter();
        var before = Console.Error;
        Console.SetError(said);
        try
        {
            return (Record.Exception(start), said.ToString());
        }
        finally
        {
            Console.SetError(before);
        }
    }

    /// <summary>
    /// One warning per refusal (design §5.1), by the method, the route's pattern, what was presented and the code: never
    /// the key, the id in the path or the body's words, and no failed request beside it. The forms a route judges write
    /// the same line.
    /// </summary>
    [Fact]
    public async Task A_refusal_is_one_warning_of_codes_and_never_the_key_the_id_or_the_body()
    {
        using var host = new KeyedHost();
        const string Words = "words-only-the-body-holds";

        Assert.Equal(403, (await host.PostAsync("/api/quests/a-quest-id-in-the-path/review", new { verdict = "skipped", words = Words })).Status);
        Assert.Equal(403, (await host.PostAsync(
            "/api/quests/another-id-in-the-path/accept", new { }, person: KeyedHost.Earlier)).Status);
        Assert.Equal(403, (await host.PostAsync(
            "/api/quests/a-third-id-in-the-path/respond", new { action = "decline", reason = Words, whileOpen = true })).Status);

        var lines = host.LogLines();
        var refused = lines.Where(line => Parse(line).GetProperty("event").GetString() == PersonDoors.Event)
            .Select(line => Parse(line)).ToList();
        Assert.Equal(3, refused.Count);
        Assert.All(refused, line => Assert.Equal("warn", line.GetProperty("level").GetString()));
        Assert.Equal(
            new[]
            {
                ("POST", "/api/quests/{id}/review", "none", PersonDoors.PersonOnly),
                ("POST", "/api/quests/{id}/accept", "stale", PersonDoors.Stale),
                ("POST", "/api/quests/{id}/respond", "none", PersonDoors.PersonOnly),
            },
            refused.Select(line => line.GetProperty("data")).Select(data => (
                data.GetProperty("method").GetString()!, data.GetProperty("route").GetString()!,
                data.GetProperty("presented").GetString()!, data.GetProperty("code").GetString()!)).ToArray());
        Assert.DoesNotContain(lines, line => Parse(line).GetProperty("event").GetString() == "request.failed");
        Assert.All(lines, line =>
        {
            Assert.DoesNotContain(KeyedHost.Key, line);
            Assert.DoesNotContain(KeyedHost.Earlier, line);
            Assert.DoesNotContain(Words, line);
            Assert.DoesNotContain("id-in-the-path", line);
        });
    }

    /// <summary>
    /// A host handed no key keeps today's trust (design §2.1) until the shell and the gates hand one (PERSONDOOR1d–h): every
    /// route answers a keyless call as before, a key presented to it means nothing, a keyless publish is the person's,
    /// and it proves nothing it does not hold.
    /// </summary>
    [Fact]
    public async Task A_host_handed_no_key_answers_as_today()
    {
        using var host = new LocalHost();

        var refused = new List<string>();
        foreach (var (method, pattern) in host.Routes())
        {
            var body = method == "GET" || method == "DELETE" ? null : "{}";
            foreach (var person in new[] { null, KeyedHost.Key })
            {
                var answer = await host.SendAsync(method, PageRequests.Concrete(pattern), DaorisHost.Loopback, json: body, person: person);
                if (answer.Status is 403 or >= 500) refused.Add($"{method} {pattern} ({(person is null ? "keyless" : "a key")}): {answer.Status} {answer.Body}");
            }
        }

        Assert.True(refused.Count == 0, string.Join("\n", refused));

        var published = await host.PostAsync("/api/quests", new
        {
            from = "Asker", to = "Keeper", title = "Ship it unreviewed", body = "b", review = new { choice = "off" },
        });
        Assert.Equal(200, published.Status);
        Assert.Equal("off", published.Json.GetProperty("quest").GetProperty("review").GetProperty("choice").GetString());

        var status = await host.GetAsync("/api/status?prove=a-nonce");
        Assert.Equal(200, status.Status);
        Assert.False(status.Json.TryGetProperty("proof", out _));
        Assert.DoesNotContain(host.LogLines(), line => Parse(line).GetProperty("event").GetString() == PersonDoors.Event);
    }

    /// <summary>
    /// A host asked for its key and handed no key, or a line that is not one, does not start (design §2.1): a starter that
    /// meant to gate the doors and failed must not leave them open by its mistake.
    /// </summary>
    [Theory]
    [InlineData("not a key\n")]
    [InlineData("")]
    public void A_host_asked_for_its_key_and_handed_none_refuses_to_start(string input)
    {
        var (failed, said) = Capturing(() => new DaorisHost(ServiceMode.Local, input: input).Dispose());

        Assert.NotNull(failed);
        Assert.Contains("DAORIS_PERSON_KEY_ON_INPUT is 1, and the first line of standard input is not a person key", said);
    }

    /// <summary>A shared host reads no person key (design §6): asked for one, it does not start.</summary>
    [Fact]
    public void A_shared_host_asked_for_a_key_refuses_to_start()
    {
        var (failed, said) = Capturing(() => new DaorisHost(ServiceMode.Shared, input: KeyedHost.Key + "\n").Dispose());

        Assert.NotNull(failed);
        Assert.Contains("a shared host reads none (D156 point 6)", said);
    }
}

/// <summary>
/// The real host, as the shell will start it (PERSONDOOR1g): its key on the first line of a redirected input, the stop on
/// that input's end after it (LOG2a). What the in-process host cannot show: the real standard input, read before the host
/// answers anyone.
/// </summary>
public sealed class PersonDoorProcessTests : IDisposable
{
    private readonly string _scratch = Path.Combine(Path.GetTempPath(), "daoris-persondoor-" + Guid.NewGuid().ToString("N")[..8]);

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

    private static async Task<(int Status, string Body)> SendAsync(string url, string path, string? person)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        using var request = new HttpRequestMessage(HttpMethod.Post, url + path) { Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json") };
        if (person is not null) request.Headers.Add(PersonKey.Header, person);
        using var response = await client.SendAsync(request);
        return ((int)response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task The_real_host_reads_its_key_from_its_input_gates_by_it_and_still_stops_when_the_input_ends()
    {
        using var host = await RealHost.StartAsync(
            _scratch,
            new Dictionary<string, string?> { [PersonKey.InputVariable] = "1", [InputEndStop.Variable] = "1" },
            input => input.WriteLine(KeyedHost.Key));

        var keyless = await SendAsync(host.Url, "/api/quests/probe/accept", person: null);
        Assert.Equal(403, keyless.Status);
        Assert.Contains($"\"code\":\"{PersonDoors.PersonOnly}\"", keyless.Body);
        Assert.Equal(404, (await SendAsync(host.Url, "/api/quests/probe/accept", KeyedHost.Key)).Status);

        using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) })
        {
            var proved = await client.GetStringAsync(host.Url + "/api/status?prove=" + Uri.EscapeDataString("a nonce, 一个"));
            Assert.Contains("\"proof\":\"gQMDWolsIQRnw-_d19k3A1c9E6ey2aP9-8RL_n_pJ50\"", proved);
        }

        host.Process.StandardInput.Close();
        Assert.True(host.Process.WaitForExit(15_000), "the host did not stop when its input closed");
        Assert.Equal(0, host.Process.ExitCode);
        var lines = host.LogLines();
        Assert.Contains(lines, line => line.Contains($"\"event\":\"{PersonDoors.Event}\"", StringComparison.Ordinal));
        Assert.All(lines, line => Assert.DoesNotContain(KeyedHost.Key, line));
    }

    [Fact]
    public async Task The_real_host_handed_a_line_that_is_no_key_never_answers()
    {
        Exception? failed = null;
        try
        {
            // A host that answered anyway is stopped here: one left running would hold the test run's own output open.
            using var answered = await RealHost.StartAsync(
                _scratch,
                new Dictionary<string, string?> { [PersonKey.InputVariable] = "1" },
                input => input.WriteLine("the person's password, which is no key"));
        }
        catch (InvalidOperationException error)
        {
            failed = error;
        }

        Assert.NotNull(failed);
        Assert.Contains("exited (2)", failed.Message);
    }
}
