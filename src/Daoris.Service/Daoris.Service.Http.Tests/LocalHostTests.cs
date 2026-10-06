using System.Text.Json;
using Daoris.Knowledge;

namespace Daoris.Service.Http.Tests;

/// <summary>A local host over two adopted repositories, imported from its folder on first run (D48 §3).</summary>
public sealed class LocalHost() : DaorisHost(ServiceMode.Local, seed: repositories =>
{
    Repository(repositories, "Asker", "Asks for things.");
    Repository(repositories, "Keeper", "Keeps its own area.");
})
{
    public string RootOf(string repository) => Path.Combine(Repositories, repository);

    private static void Repository(string folder, string name, string summary)
    {
        var dir = Path.Combine(folder, name);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "daoris.json"), $$"""
            {
              "source": "s", "packs": [],
              "domain": { "summary": "{{summary}}", "owns": ["its own tree"], "accepts": ["a quest"] }
            }
            """);
    }
}

/// <summary>
/// The local host's doors (HTTP1): the loopback is the trust boundary (D21), so a registration's root
/// answers a caller on this machine and nobody else (D46); the delete doors answer here, with the
/// service's own sentence (D95); and the page is served — the counterpart the shared host's refusal is
/// measured against.
/// </summary>
public sealed class LocalHostTests(LocalHost host) : IClassFixture<LocalHost>
{
    private static JsonElement Row(Answer registry, string repository) =>
        registry.Json.EnumerateArray().Single(row => row.GetProperty("repository").GetString() == repository);

    private async Task<string> PublishAsync(string title)
    {
        var published = await host.PostAsync("/api/quests", new
        {
            from = "Asker", to = "Keeper", title, body = "Published by mistake.",
        });
        Assert.Equal(200, published.Status);
        return published.Json.GetProperty("quest").GetProperty("id").GetString()!;
    }

    private async Task<bool> ListedAsync(string quest) =>
        (await host.GetAsync("/api/quests")).Json.EnumerateArray()
            .Any(row => row.GetProperty("id").GetString() == quest);

    /// <summary>The local driver is on this machine, and it needs the root to spawn in (D46).</summary>
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    public async Task A_registrations_root_is_answered_to_a_caller_on_this_machine(string address)
    {
        var registry = await host.GetAsync("/api/registry", System.Net.IPAddress.Parse(address));

        Assert.Equal(200, registry.Status);
        Assert.Equal(host.RootOf("Keeper"), Row(registry, "Keeper").GetProperty("root").GetString());
    }

    /// <summary>
    /// 🔴 A root never leaves the machine (D46): a caller off it is answered the row, and no path — not
    /// in the field, and not anywhere else in the body.
    /// </summary>
    [Fact]
    public async Task A_registrations_root_is_never_answered_to_a_caller_off_this_machine()
    {
        var registry = await host.GetAsync("/api/registry", DaorisHost.OffMachine);

        Assert.Equal(200, registry.Status);
        Assert.False(Row(registry, "Keeper").TryGetProperty("root", out _));
        Assert.DoesNotContain(host.Repositories, registry.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(JsonEncodedText.Encode(host.Repositories).ToString(), registry.Body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// D115 §2.2 (DEV4), through the HTTP doors: a registration's lanes arrive as words and are listed
    /// by the registry's answer; a registration silent about them keeps them; a quest to one of them is
    /// published to the repository with its lanes, and one to a lane nobody declared is refused, naming
    /// the lanes there are.
    /// </summary>
    [Fact]
    public async Task Lanes_are_registered_listed_kept_and_addressed_through_the_doors()
    {
        var lanes = new[]
        {
            new { id = "core", title = "Core", summary = "The runtime.", steward = false },
            new { id = "assets", title = "Assets", summary = "The pipeline.", steward = false },
        };
        Assert.Equal(200, (await host.PostAsync("/api/registry", new
        {
            repository = "Laned", root = host.RootOf("Laned"), packs = Array.Empty<string>(),
            domain = new { summary = "Has lanes.", owns = new[] { "its runtime" }, accepts = new[] { "a quest" } },
            lanes,
        })).Status);

        var listed = Row(await host.GetAsync("/api/registry"), "Laned").GetProperty("lanes");
        Assert.Equal(["core", "assets"], listed.EnumerateArray().Select(lane => lane.GetProperty("id").GetString()));
        Assert.Equal("The runtime.", listed[0].GetProperty("summary").GetString());

        // The page's own add says nothing of lanes, and a row keeps what it had.
        Assert.Equal(200, (await host.PostAsync("/api/registry", new
        {
            repository = "Laned", root = host.RootOf("Laned"),
            domain = new { summary = "Has lanes, said again.", owns = new[] { "its runtime" }, accepts = new[] { "a quest" } },
        })).Status);
        Assert.Equal(2, Row(await host.GetAsync("/api/registry"), "Laned").GetProperty("lanes").GetArrayLength());

        var published = await host.PostAsync("/api/quests", new
        {
            from = "Asker", to = "Laned:core", title = "Cap the frame's work", body = "It runs unbounded.",
        });
        Assert.Equal(200, published.Status);
        var quest = published.Json.GetProperty("quest");
        Assert.Equal("Laned", quest.GetProperty("to").GetString());
        Assert.Equal(["core"], quest.GetProperty("lanes").EnumerateArray().Select(lane => lane.GetString()));

        var refused = await host.PostAsync("/api/quests", new
        {
            from = "Asker", to = "Laned:nope", title = "Cap the frame's work", body = "It runs unbounded.",
        });
        Assert.Equal(400, refused.Status);
        var said = refused.Json.GetProperty("error").GetString()!;
        Assert.Contains("`core`", said);
        Assert.Contains("`assets`", said);
    }

    /// <summary>A quest nobody took goes, and the answer is the exchange's sentence (D95).</summary>
    [Fact]
    public async Task A_quest_nobody_took_is_deleted()
    {
        var quest = await PublishAsync("A quest nobody took");
        Assert.True(await ListedAsync(quest));

        var deleted = await host.DeleteAsync($"/api/quests/{quest}");

        Assert.Equal(200, deleted.Status);
        Assert.Equal(quest, deleted.Json.GetProperty("id").GetString());
        Assert.Contains($"Deleted quest `#{quest}`", deleted.Json.GetProperty("message").GetString());
        Assert.False(await ListedAsync(quest));
    }

    /// <summary>
    /// 🔴 A taken quest is somebody's work in a tree: 409, the lock's own shape, and the refusal is the
    /// exchange's sentence verbatim — compared with what the exchange itself says, not re-typed here.
    /// </summary>
    [Fact]
    public async Task A_taken_quest_is_refused_with_409_and_the_services_sentence()
    {
        var quest = await PublishAsync("A quest somebody took");
        Assert.Equal(200, (await host.PostAsync($"/api/quests/{quest}/respond", new { action = "take" })).Status);

        var refused = await host.DeleteAsync($"/api/quests/{quest}");

        Assert.Equal(409, refused.Status);
        Assert.Contains("is Taken", refused.Error);
        Assert.Contains("Decline it", refused.Error);
        var said = await host.Composed.Exchange.DeleteAsync(quest, DateTimeOffset.UtcNow);
        Assert.Equal(said.Message, refused.Error);
        Assert.True(await ListedAsync(quest));
    }

    /// <summary>
    /// A decline made while open (PAUSE1c) is the respond door's too, so an abandon reaches it from the driver: an
    /// open quest is declined with the flag kept, and a taken one answers 409, the lock's shape, with the exchange's
    /// sentence, and stays taken.
    /// </summary>
    [Fact]
    public async Task A_decline_made_while_open_declines_an_open_quest_and_is_refused_with_409_on_a_taken_one()
    {
        var open = await PublishAsync("A quest an abandon declines");
        var taken = await PublishAsync("A quest somebody took before the abandon");
        Assert.Equal(200, (await host.PostAsync($"/api/quests/{taken}/respond", new { action = "take" })).Status);

        var declined = await host.PostAsync(
            $"/api/quests/{open}/respond", new { action = "decline", reason = "Abandoned.", whileOpen = true });
        var refused = await host.PostAsync(
            $"/api/quests/{taken}/respond", new { action = "decline", reason = "Abandoned.", whileOpen = true });

        Assert.Equal(200, declined.Status);
        Assert.Equal("Declined", declined.Json.GetProperty("quest").GetProperty("status").GetString());
        Assert.True((await host.Composed.Quests.HistoryAsync(open))[^1].WhileOpen);
        Assert.Equal(409, refused.Status);
        Assert.Contains("only while", refused.Error);
        Assert.Equal(
            (await host.Composed.Exchange.RespondAsync(taken, "decline", "Abandoned.", DateTimeOffset.UtcNow, whileOpen: true)).Message,
            refused.Error);
        Assert.Equal(QuestStatus.Taken, (await host.Composed.Quests.FindAsync(taken))!.Status);
    }

    [Fact]
    public async Task A_quest_that_is_not_held_answers_404()
    {
        var missing = await host.DeleteAsync("/api/quests/0000000000");

        Assert.Equal(404, missing.Status);
        Assert.Contains("0000000000", missing.Error);
    }

    /// <summary>An ask made by mistake goes, and so does the one quest it became (D95).</summary>
    [Fact]
    public async Task An_ask_made_by_mistake_is_deleted_with_its_quest()
    {
        var asked = await host.PostAsync("/api/asks", new
        {
            workspace = "default", sentence = "Please look at the thing I meant to ask someone else.", to = "Keeper",
        });
        Assert.Equal(200, asked.Status);
        var ask = asked.Json.GetProperty("ask").GetProperty("id").GetString()!;
        var quest = asked.Json.GetProperty("quest").GetProperty("id").GetString()!;

        var deleted = await host.DeleteAsync($"/api/asks/{ask}");

        Assert.Equal(200, deleted.Status);
        Assert.Contains($"Deleted ask `#{ask}`", deleted.Json.GetProperty("message").GetString());
        Assert.Equal(404, (await host.GetAsync($"/api/asks/{ask}")).Status);
        Assert.False(await ListedAsync(quest));
    }

    /// <summary>An ask one of whose quests was taken stays whole: 409, and the desk's sentence.</summary>
    [Fact]
    public async Task An_ask_whose_quest_was_taken_is_refused_with_409_and_the_services_sentence()
    {
        var asked = await host.PostAsync("/api/asks", new
        {
            workspace = "default", sentence = "Please do the thing somebody has already started on.", to = "Keeper",
        });
        var ask = asked.Json.GetProperty("ask").GetProperty("id").GetString()!;
        var quest = asked.Json.GetProperty("quest").GetProperty("id").GetString()!;
        Assert.Equal(200, (await host.PostAsync($"/api/quests/{quest}/respond", new { action = "take" })).Status);

        var refused = await host.DeleteAsync($"/api/asks/{ask}");

        Assert.Equal(409, refused.Status);
        Assert.Contains($"Ask `#{ask}` stays", refused.Error);
        Assert.Contains("close the ask instead", refused.Error);
        Assert.Equal((await host.Composed.Asks.DeleteAsync(ask, DateTimeOffset.UtcNow)).Message, refused.Error);
        Assert.Equal(200, (await host.GetAsync($"/api/asks/{ask}")).Status);
    }

    /// <summary>A session on <paramref name="quest"/> in a tree of its own, so no other test's session holds it.</summary>
    private async Task<string> RunningAsync(string quest, string tree)
    {
        var opened = await host.PostAsync("/api/sessions", new
        {
            quest, adapter = "stub", tree = Path.Combine(host.Repositories, "trees", tree),
        });
        Assert.Equal(200, opened.Status);
        var id = opened.Json.GetProperty("session").GetProperty("id").GetString()!;
        foreach (var state in new[] { "starting", "working" })
        {
            Assert.Equal(200, (await host.PostAsync($"/api/sessions/{id}/state", new { state })).Status);
        }

        return id;
    }

    /// <summary>
    /// DRIFT1a (D133 §1): what the person adds to a running session and what they answer it are kept on the
    /// ask its quest was asked by, and both ask routes answer them as `words`, after the ask's own sentence:
    /// each verbatim, with when, the session and the quest. An ask made now keeps its words whole, so it
    /// says nothing of when keeping began.
    /// </summary>
    [Fact]
    public async Task An_added_message_and_an_answer_are_read_back_from_the_asks_routes()
    {
        var asked = await host.PostAsync("/api/asks", new
        {
            workspace = "default", sentence = "Build the report through the v3 bridge.", to = "Keeper",
        });
        var ask = asked.Json.GetProperty("ask").GetProperty("id").GetString()!;
        var quest = asked.Json.GetProperty("quest").GetProperty("id").GetString()!;
        Assert.Equal(["asked"], asked.Json.GetProperty("ask").GetProperty("words").EnumerateArray().Select(w => w.GetProperty("kind").GetString()));
        var id = await RunningAsync(quest, "drift1a-words");

        var added = await host.PostAsync($"/api/sessions/{id}/added", new { text = "no need for a new backend api" });

        Assert.Equal(200, added.Status);
        Assert.True(added.Json.GetProperty("kept").GetBoolean());
        Assert.Contains($"ask `#{ask}`", added.Json.GetProperty("message").GetString());
        Assert.Equal(2, added.Json.GetProperty("ask").GetProperty("words").GetArrayLength());

        Assert.Equal(200, (await host.PostAsync($"/api/sessions/{id}/state", new { state = "awaiting-person", note = "Which report?" })).Status);
        Assert.Equal(200, (await host.PostAsync($"/api/sessions/{id}/answer", new { answer = "use the v3 common-report" })).Status);

        var read = await host.GetAsync($"/api/asks/{ask}");
        Assert.Equal(200, read.Status);
        var words = read.Json.GetProperty("words").EnumerateArray().ToList();
        Assert.Equal(["asked", "added", "answered"], words.Select(w => w.GetProperty("kind").GetString()));
        Assert.Equal(
            ["Build the report through the v3 bridge.", "no need for a new backend api", "use the v3 common-report"],
            words.Select(w => w.GetProperty("text").GetString()));
        Assert.False(words[0].TryGetProperty("session", out _));
        foreach (var word in words[1..])
        {
            Assert.Equal(id, word.GetProperty("session").GetString());
            Assert.Equal(quest, word.GetProperty("quest").GetString());
            Assert.True(word.GetProperty("at").GetDateTimeOffset() >= words[0].GetProperty("at").GetDateTimeOffset());
        }

        Assert.False(read.Json.TryGetProperty("wordsKeptFrom", out _));
        var listed = (await host.GetAsync("/api/asks?includeClosed=true")).Json.EnumerateArray()
            .Single(row => row.GetProperty("id").GetString() == ask);
        Assert.Equal(3, listed.GetProperty("words").GetArrayLength());
    }

    /// <summary>
    /// KNOWUSE1a (D135 §2): a go-ahead a session asked is answered on both ask routes as `goAheads`, the act by its kind,
    /// place and words, with each session's request; the person's answer door keeps their yes or no with their words,
    /// 200 with the ask as it now stands. An answer that is neither, a go-ahead the ask does not hold and an ask nobody
    /// holds are refused, and nothing is kept.
    /// </summary>
    [Fact]
    public async Task A_go_ahead_is_read_back_from_the_asks_routes_and_answered_at_its_door()
    {
        var asked = await host.PostAsync("/api/asks", new
        {
            workspace = "default", sentence = "The dashboard figure reads zero; fix it.", to = "Keeper",
        });
        var ask = asked.Json.GetProperty("ask").GetProperty("id").GetString()!;
        var quest = asked.Json.GetProperty("quest").GetProperty("id").GetString()!;
        Assert.Equal(0, asked.Json.GetProperty("ask").GetProperty("goAheads").GetArrayLength());
        var id = await RunningAsync(quest, "knowuse1a-go-ahead");
        await host.Composed.Ledger.AskGoAheadAsync(id, "write", "production", "dashboard configuration", "The tile's target.", DateTimeOffset.UtcNow);

        var read = await host.GetAsync($"/api/asks/{ask}");

        var goAhead = Assert.Single(read.Json.GetProperty("goAheads").EnumerateArray());
        Assert.Equal(1, goAhead.GetProperty("number").GetInt32());
        Assert.Equal(["write", "production", "dashboard configuration", "asked"],
            new[] { "kind", "on", "act", "state" }.Select(field => goAhead.GetProperty(field).GetString()));
        var request = Assert.Single(goAhead.GetProperty("asked").EnumerateArray());
        Assert.Equal([id, quest, "The tile's target."], new[] { "session", "quest", "why" }.Select(field => request.GetProperty(field).GetString()));
        // Absent while it waits, as every null this host answers is.
        Assert.False(goAhead.TryGetProperty("answer", out _));

        Assert.Contains(("POST", "/api/asks/{id}/go-aheads/{number}"), host.Routes());
        var answered = await host.PostAsync($"/api/asks/{ask}/go-aheads/1", new { answer = "approved", words = "run the put" });

        Assert.Equal(200, answered.Status);
        Assert.Contains("approved", answered.Json.GetProperty("message").GetString());
        var now = Assert.Single(answered.Json.GetProperty("ask").GetProperty("goAheads").EnumerateArray());
        Assert.Equal("approved", now.GetProperty("state").GetString());
        Assert.True(now.GetProperty("answer").GetProperty("approved").GetBoolean());
        Assert.Equal("run the put", now.GetProperty("answer").GetProperty("words").GetString());
        var listed = (await host.GetAsync("/api/asks?includeClosed=true")).Json.EnumerateArray()
            .Single(row => row.GetProperty("id").GetString() == ask);
        Assert.Equal("approved", listed.GetProperty("goAheads")[0].GetProperty("state").GetString());

        Assert.Equal(400, (await host.PostAsync($"/api/asks/{ask}/go-aheads/1", new { answer = "maybe" })).Status);
        Assert.Equal(400, (await host.PostAsync($"/api/asks/{ask}/go-aheads/1", new { answer = "refused", words = new string('x', 2_001) })).Status);
        var noGoAhead = await host.PostAsync($"/api/asks/{ask}/go-aheads/7", new { answer = "refused" });
        Assert.Equal(404, noGoAhead.Status);
        Assert.Contains("go-ahead 7", noGoAhead.Error);
        Assert.Equal(404, (await host.PostAsync("/api/asks/ffffff/go-aheads/1", new { answer = "refused" })).Status);
        Assert.Equal("approved", (await host.GetAsync($"/api/asks/{ask}")).Json.GetProperty("goAheads")[0].GetProperty("state").GetString());
    }

    /// <summary>
    /// DRIFT1c (D133 §3): the ask's publish door takes requirements, each the person's words with its check.
    /// A quote they never said is refused, 409 with the exchange's sentence naming the words, and nothing is
    /// published; one they said is published, and the quest answers it on the publish and on the list.
    /// </summary>
    [Fact]
    public async Task An_asks_publish_takes_requirements_and_refuses_a_quote_the_person_never_said()
    {
        var asked = await host.PostAsync("/api/asks", new
        {
            workspace = "default", sentence = "Build the daily report, and it will need the v3 bridge.",
        });
        var ask = asked.Json.GetProperty("ask").GetProperty("id").GetString()!;

        var refused = await host.PostAsync($"/api/asks/{ask}/publish", new
        {
            to = "Keeper", title = "Build the daily report", body = "Reached through the bridge.",
            requirements = new[] { new { quote = "make it reachable through the v3 bridge", check = "It opens in the older shell." } },
        });

        Assert.Equal(409, refused.Status);
        Assert.Contains("\"make it reachable through the v3 bridge\"", refused.Error);
        Assert.Empty((await host.GetAsync($"/api/asks/{ask}")).Json.GetProperty("quests").EnumerateArray());

        var published = await host.PostAsync($"/api/asks/{ask}/publish", new
        {
            to = "Keeper", title = "Build the daily report", body = "Reached through the bridge.",
            requirements = new[] { new { quote = "it will need the v3 bridge", check = "The report opens through the bridge's route." } },
        });

        Assert.Equal(200, published.Status);
        var quest = published.Json.GetProperty("quest");
        var requirement = Assert.Single(quest.GetProperty("requirements").EnumerateArray().ToList());
        Assert.Equal("it will need the v3 bridge", requirement.GetProperty("quote").GetString());
        Assert.Equal("The report opens through the bridge's route.", requirement.GetProperty("check").GetString());
        var listed = (await host.GetAsync("/api/quests")).Json.EnumerateArray()
            .Single(row => row.GetProperty("id").GetString() == quest.GetProperty("id").GetString());
        Assert.Equal(1, listed.GetProperty("requirements").GetArrayLength());
    }

    /// <summary>
    /// SESSUX1j: the quest door takes the composer's short title and answers every quest with what a list calls it, the
    /// publisher's short title or a name read from its words; one past 40 characters is refused, 400, in the service's words.
    /// </summary>
    [Fact]
    public async Task The_quest_door_keeps_a_short_title_and_answers_every_quest_with_its_name()
    {
        var given = await host.PostAsync("/api/quests", new
        {
            from = "Asker", to = "Keeper", title = "Cap the frame's work so the editor stays responsive", body = "It stalls.",
            @short = "Frame cap",
        });
        Assert.Equal(200, given.Status);
        Assert.Equal("Frame cap", given.Json.GetProperty("quest").GetProperty("short").GetString());

        var derived = await host.PostAsync("/api/quests", new
        {
            from = "Asker", to = "Keeper", title = "Read the field names from the configuration file instead", body = "Hard-coded.",
        });
        Assert.Equal(200, derived.Status);
        Assert.Equal("Read the field names from the…", derived.Json.GetProperty("quest").GetProperty("short").GetString());

        var refused = await host.PostAsync("/api/quests", new
        {
            from = "Asker", to = "Keeper", title = "A quest with too long a short title", body = "b",
            @short = "A short title that runs well past the forty characters a list gives it",
        });
        Assert.Equal(400, refused.Status);
        Assert.Contains("40 characters", refused.Error);
    }

    /// <summary>
    /// DRIFT1c: the quest door keeps working for a client that names no requirements, answering the quest
    /// with none; a repository naming some is asking on no ask, so there is nothing to quote, 400.
    /// </summary>
    [Fact]
    public async Task The_quest_door_publishes_without_requirements_and_refuses_them_on_no_ask()
    {
        var plain = await host.PostAsync("/api/quests", new
        {
            from = "Asker", to = "Keeper", title = "A quest from a client before requirements", body = "It names none.",
        });

        Assert.Equal(200, plain.Status);
        Assert.Equal(0, plain.Json.GetProperty("quest").GetProperty("requirements").GetArrayLength());

        var refused = await host.PostAsync("/api/quests", new
        {
            from = "Asker", to = "Keeper", title = "A quest that quotes nobody", body = "It names some.",
            requirements = new[] { new { quote = "its own tree", check = "Kept." } },
        });

        Assert.Equal(400, refused.Status);
        Assert.Contains("`Asker`", refused.Error);
        Assert.Contains("on no ask", refused.Error);
    }

    /// <summary>
    /// DRIFT1d (D133 §4): the respond door's done answers each requirement. One left unanswered is refused, 400 naming
    /// it; a departure closes the quest done, answered on the quest with <c>held</c>, and it stays on the outstanding
    /// list. The accept door, the person's yes, releases it: 200 once, 409 after, 404 for a quest nobody holds.
    /// </summary>
    [Fact]
    public async Task A_done_answers_each_requirement_and_a_departure_waits_for_the_accept_door()
    {
        var asked = await host.PostAsync("/api/asks", new
        {
            workspace = "default", sentence = "Build the weekly report through the v3 bridge, using the common-report.",
        });
        var ask = asked.Json.GetProperty("ask").GetProperty("id").GetString()!;
        var published = await host.PostAsync($"/api/asks/{ask}/publish", new
        {
            to = "Keeper", title = "Build the weekly report", body = "Reached through the bridge.",
            requirements = new[]
            {
                new { quote = "through the v3 bridge", check = "It opens through the bridge's route." },
                new { quote = "using the common-report", check = "It is a common-report configuration." },
            },
        });
        var quest = published.Json.GetProperty("quest").GetProperty("id").GetString()!;
        Assert.Equal(200, (await host.PostAsync($"/api/quests/{quest}/respond", new { action = "take" })).Status);

        var unanswered = await host.PostAsync($"/api/quests/{quest}/respond", new
        {
            action = "done", reason = "Built.", answers = new[] { new { requirement = 1, met = "It opens through the route." } },
        });
        Assert.Equal(400, unanswered.Status);
        Assert.Contains("requirement 2: \"using the common-report\"", unanswered.Error);

        var closed = await host.PostAsync($"/api/quests/{quest}/respond", new
        {
            action = "done", reason = "Built.",
            answers = new object[]
            {
                new { requirement = 1, met = "It opens through the route." },
                new { requirement = 2, departed = "The weekly totals needed a type of its own.", quote = "the common-report" },
            },
        });
        Assert.Equal(200, closed.Status);
        var answered = closed.Json.GetProperty("quest");
        Assert.True(answered.GetProperty("held").GetBoolean());
        Assert.Equal("the common-report", answered.GetProperty("answers")[1].GetProperty("quote").GetString());
        Assert.Contains((await host.GetAsync("/api/quests")).Json.EnumerateArray(), row => row.GetProperty("id").GetString() == quest);

        Assert.Contains(("POST", "/api/quests/{id}/accept"), host.Routes());
        var accepted = await host.PostAsync($"/api/quests/{quest}/accept", new { });
        Assert.Equal(200, accepted.Status);
        Assert.False(accepted.Json.GetProperty("quest").GetProperty("held").GetBoolean());
        Assert.Equal(JsonValueKind.String, accepted.Json.GetProperty("quest").GetProperty("accepted").ValueKind);
        Assert.Equal(409, (await host.PostAsync($"/api/quests/{quest}/accept", new { })).Status);
        Assert.Equal(404, (await host.PostAsync("/api/quests/feedfacecafe/accept", new { })).Status);
    }

    /// <summary>
    /// DRIFT1a: the added door keeps nothing for a session on no ask, and says so with a 200 — its own record
    /// holds what was said, which is no error; it refuses a session it does not hold, 404, and no words, 400.
    /// </summary>
    [Fact]
    public async Task The_added_door_keeps_nothing_off_an_ask_and_refuses_an_unknown_session_or_no_words()
    {
        var quest = await PublishAsync("A quest one repository asked of another");
        var id = await RunningAsync(quest, "drift1a-none");

        var none = await host.PostAsync($"/api/sessions/{id}/added", new { text = "use the hook" });
        var unknown = await host.PostAsync("/api/sessions/nothing1/added", new { text = "use the hook" });
        var blank = await host.PostAsync($"/api/sessions/{id}/added", new { text = "  " });

        Assert.Equal(200, none.Status);
        Assert.False(none.Json.GetProperty("kept").GetBoolean());
        Assert.Contains("`Asker`", none.Json.GetProperty("message").GetString());
        Assert.False(none.Json.TryGetProperty("ask", out _));
        Assert.Equal(404, unknown.Status);
        Assert.Contains("nothing1", unknown.Error);
        Assert.Equal(400, blank.Status);
        Assert.Equal(200, (await host.PostAsync($"/api/sessions/{id}/state", new { state = "stopped" })).Status);
    }

    /// <summary>
    /// D104: the driver says a stop was not the person's — the sweep's, or a shutdown's — through the
    /// state door, and the record answers it back. Asked of any other move, it is refused, 409.
    /// </summary>
    [Fact]
    public async Task A_stop_the_driver_says_was_interrupted_is_kept_and_answered_back()
    {
        var quest = await PublishAsync("A quest whose session the driver's shutdown ended");
        var opened = await host.PostAsync("/api/sessions", new { quest, adapter = "stub" });
        Assert.Equal(200, opened.Status);
        var id = opened.Json.GetProperty("session").GetProperty("id").GetString()!;
        foreach (var state in new[] { "starting", "working" })
        {
            Assert.Equal(200, (await host.PostAsync($"/api/sessions/{id}/state", new { state })).Status);
        }

        var refused = await host.PostAsync($"/api/sessions/{id}/state", new { state = "failed", interrupted = true });
        Assert.Equal(409, refused.Status);
        Assert.Contains("stopped", refused.Error);

        var stopped = await host.PostAsync(
            $"/api/sessions/{id}/state", new { state = "stopped", note = "the driver was stopped while this ran.", interrupted = true });

        Assert.Equal(200, stopped.Status);
        Assert.True(stopped.Json.GetProperty("session").GetProperty("interrupted").GetBoolean());
        var listed = (await host.GetAsync("/api/sessions?includeClosed=true")).Json.EnumerateArray()
            .Single(row => row.GetProperty("id").GetString() == id);
        Assert.True(listed.GetProperty("interrupted").GetBoolean());
    }

    /// <summary>
    /// CARRY2: a carry-on over a take that is not this machine's is a state conflict, 409, as a taken quest is, and its
    /// sentence names the session it will not carry on. Here that session failed before its take, and the quest was taken
    /// through the door after it ended.
    /// </summary>
    [Fact]
    public async Task A_carry_on_over_a_take_made_after_its_session_ended_is_refused_409()
    {
        var quest = await PublishAsync("A quest whose session failed before it took it");
        var opened = await host.PostAsync("/api/sessions", new { quest, adapter = "stub" });
        Assert.Equal(200, opened.Status);
        var id = opened.Json.GetProperty("session").GetProperty("id").GetString()!;
        foreach (var state in new[] { "starting", "working" })
        {
            Assert.Equal(200, (await host.PostAsync($"/api/sessions/{id}/state", new { state })).Status);
        }

        Assert.Equal(200, (await host.PostAsync(
            $"/api/sessions/{id}/state", new { state = "failed", note = "the harness exited before its first turn." })).Status);
        Assert.Equal(200, (await host.PostAsync($"/api/quests/{quest}/respond", new { action = "take" })).Status);

        var refused = await host.PostAsync("/api/sessions", new { quest, adapter = "stub" });

        Assert.Equal(409, refused.Status);
        Assert.Contains($"taken here after session `{id}` ended", refused.Error);
    }

    /// <summary>
    /// LANG1a (D142 point 2): the state door takes a note's parts beside it, and the record's route and the list answer
    /// them back, the note unchanged beside them. A note sent without parts clears them, so an older driver's note never
    /// sits beside stale ones; a move that sends no note keeps both; an answer adds its own lines after them.
    /// </summary>
    [Fact]
    public async Task A_note_s_parts_go_in_through_the_state_door_and_come_back_beside_it()
    {
        var quest = await PublishAsync("A quest whose session parked to ask, by code");
        var opened = await host.PostAsync("/api/sessions", new { quest, adapter = "stub" });
        var id = opened.Json.GetProperty("session").GetProperty("id").GetString()!;
        foreach (var state in new[] { "starting", "working" })
        {
            Assert.Equal(200, (await host.PostAsync($"/api/sessions/{id}/state", new { state })).Status);
        }

        var parked = await host.PostAsync($"/api/sessions/{id}/state", new
        {
            state = "awaiting-person",
            note = "It stopped with its quest still taken, to ask you:\n\nWhich port?",
            noteParts = new object[]
            {
                new { code = "ended.parked-asked", values = new { }, text = "It stopped with its quest still taken, to ask you:" },
                new { words = "Which port?", by = "agent" },
            },
        });
        Assert.Equal(200, parked.Status);
        Assert.Equal(["ended.parked-asked", null], Codes(parked.Json.GetProperty("session")));

        Assert.Equal(200, (await host.PostAsync($"/api/sessions/{id}/answer", new { answer = "8080" })).Status);
        var listed = (await host.GetAsync("/api/sessions?includeClosed=true")).Json.EnumerateArray()
            .Single(row => row.GetProperty("id").GetString() == id);
        Assert.Equal("It stopped with its quest still taken, to ask you:\n\nWhich port?\n\nAnswered: 8080", listed.GetProperty("note").GetString());
        Assert.Equal(["ended.parked-asked", null, "ledger.answered", null], Codes(listed));

        var older = await host.PostAsync($"/api/sessions/{id}/state", new { state = "completed", note = "an older driver's line." });
        Assert.Equal(200, older.Status);
        var cleared = older.Json.GetProperty("session");
        Assert.True(!cleared.TryGetProperty("noteParts", out var none) || none.ValueKind == JsonValueKind.Null);
    }

    private static IReadOnlyList<string?> Codes(JsonElement session) =>
    [
        .. session.GetProperty("noteParts").EnumerateArray()
            .Select(part => part.TryGetProperty("code", out var code) ? code.GetString() : null),
    ];

    /// <summary>
    /// ANSWER1b (D131 §5): the answer door replies with the session as it now stands, still `awaiting-person`, its words
    /// kept and said on its note, which is how the driver tells a watcher nothing moved; a second answer joins the first
    /// (MSG1a, D137 §6), `answer` the words joined for a client from before. The words are answered to this machine only,
    /// like a transcript, and a record that is not parked is a 409.
    /// </summary>
    [Fact]
    public async Task An_answer_replies_with_the_session_still_parked_and_its_words_kept()
    {
        var quest = await PublishAsync("A quest whose session parked to ask which port");
        var opened = await host.PostAsync("/api/sessions", new { quest, adapter = "stub" });
        var id = opened.Json.GetProperty("session").GetProperty("id").GetString()!;
        foreach (var state in new[] { "starting", "working" })
        {
            Assert.Equal(200, (await host.PostAsync($"/api/sessions/{id}/state", new { state })).Status);
        }

        Assert.Equal(200, (await host.PostAsync($"/api/quests/{quest}/respond", new { action = "take" })).Status);
        var early = await host.PostAsync($"/api/sessions/{id}/answer", new { answer = "8080." });
        Assert.Equal(409, early.Status);
        Assert.Contains("not waiting on you", early.Error);
        Assert.Equal(200, (await host.PostAsync($"/api/sessions/{id}/state", new { state = "awaiting-person", note = "which port?" })).Status);

        await host.PostAsync($"/api/sessions/{id}/answer", new { answer = "8080." });
        var answered = await host.PostAsync($"/api/sessions/{id}/answer", new { answer = "9090." });

        Assert.Equal(200, answered.Status);
        var session = answered.Json.GetProperty("session");
        Assert.Equal(("awaiting-person", "8080.\n\n9090.", "which port?\n\nAnswered: 8080.\n\nAnswered: 9090."),
            (session.GetProperty("state").GetString(), session.GetProperty("answer").GetString(), session.GetProperty("note").GetString()));
        Assert.Equal(["8080.", "9090."], session.GetProperty("said").EnumerateArray().Select(word => word.GetProperty("text").GetString()));
        Assert.Equal($"Answered session `{id}`: it carries on with `#{quest}` at the driver's next look.",
            answered.Json.GetProperty("message").GetString());
        var offMachine = (await host.GetAsync("/api/sessions?includeClosed=true", DaorisHost.OffMachine)).Json.EnumerateArray()
            .Single(row => row.GetProperty("id").GetString() == id);
        Assert.Equal("awaiting-person", offMachine.GetProperty("state").GetString());
        Assert.False(offMachine.TryGetProperty("answer", out _));
        Assert.False(offMachine.TryGetProperty("said", out _));

        // The record holds its tree until the driver goes on with it; stopped, nothing of it holds a later test.
        Assert.Equal(200, (await host.PostAsync($"/api/sessions/{id}/state", new { state = "stopped" })).Status);
    }

    /// <summary>
    /// MSG1a (D137 §2.3, §5.3): the say door keeps the person's words on an ended session of this machine's, the record
    /// staying as it ended. It answers the word with its id, when and its files' names (never a path), and the session as
    /// it stands. The words are answered to this machine only, `answer` the words joined for a client from before. The
    /// state door then takes the record out of its ended state to working, and the taken door takes the words off it.
    /// </summary>
    [Fact]
    public async Task Words_said_to_an_ended_session_wait_on_it_and_it_goes_on_through_the_state_door()
    {
        var quest = await PublishAsync("A quest whose session finished and is written to");
        var id = await RunningAsync(quest, "msg1a-goes-on");
        Assert.Equal(200, (await host.PostAsync($"/api/sessions/{id}/state", new { state = "completed", note = "landed." })).Status);
        var file = Path.Combine(host.Repositories, "msg1a", "plan.md");

        var said = await host.PostAsync($"/api/sessions/{id}/say", new { text = "Also add the changelog line.", files = new[] { file } });

        Assert.Equal(200, said.Status);
        Assert.Equal("completed", said.Json.GetProperty("session").GetProperty("state").GetString());
        Assert.Equal(
            $"Kept for session `{id}`: the same session goes on with your words at the driver's next look.",
            said.Json.GetProperty("message").GetString());
        var word = said.Json.GetProperty("said");
        var wordId = word.GetProperty("id").GetString()!;
        Assert.Equal("Also add the changelog line.", word.GetProperty("text").GetString());
        Assert.Equal(["plan.md"], word.GetProperty("files").EnumerateArray().Select(name => name.GetString()));
        Assert.True(word.GetProperty("reopens").GetBoolean());
        Assert.Equal(JsonValueKind.String, word.GetProperty("at").ValueKind);
        Assert.DoesNotContain("msg1a", word.GetRawText(), StringComparison.OrdinalIgnoreCase);

        var listed = (await ListedSessionAsync(id))!.Value;
        Assert.Equal([wordId], listed.GetProperty("said").EnumerateArray().Select(w => w.GetProperty("id").GetString()));
        Assert.Equal("Also add the changelog line.", listed.GetProperty("answer").GetString());
        var offMachine = (await host.GetAsync("/api/sessions?includeClosed=true", DaorisHost.OffMachine)).Json.EnumerateArray()
            .Single(row => row.GetProperty("id").GetString() == id);
        Assert.False(offMachine.TryGetProperty("said", out _));
        Assert.False(offMachine.TryGetProperty("answer", out _));

        var reopened = await host.PostAsync($"/api/sessions/{id}/state", new { state = "working" });

        Assert.Equal(200, reopened.Status);
        var going = reopened.Json.GetProperty("session");
        Assert.Equal("working", going.GetProperty("state").GetString());
        Assert.StartsWith("landed.\n\nWent on with your words at ", going.GetProperty("note").GetString());
        Assert.Equal(1, going.GetProperty("said").GetArrayLength());

        var taken = await host.PostAsync($"/api/sessions/{id}/taken", new { said = new[] { wordId } });

        Assert.Equal(200, taken.Status);
        Assert.Equal(0, taken.Json.GetProperty("session").GetProperty("said").GetArrayLength());
        Assert.False(taken.Json.GetProperty("session").TryGetProperty("answer", out _));
        Assert.Equal(400, (await host.PostAsync($"/api/sessions/{id}/taken", new { said = Array.Empty<string>() })).Status);
        Assert.Equal(404, (await host.PostAsync("/api/sessions/nothing2/taken", new { said = new[] { wordId } })).Status);
        Assert.Equal(200, (await host.PostAsync($"/api/sessions/{id}/state", new { state = "stopped" })).Status);
    }

    /// <summary>
    /// MSG1a (D137 §2.2, §5.3): the say door refuses what never goes on, each by its word beside the ledger's sentence and
    /// the fact the word names: a stood-down session (its quest), a teammate's record (its machine), an intake (its ask)
    /// and a running session, each 409; no words, 400; and a session this host does not hold, 404.
    /// </summary>
    [Fact]
    public async Task The_say_door_refuses_what_never_goes_on_by_its_word()
    {
        var quest = await PublishAsync("A quest whose session stood down");
        var stood = await RunningAsync(quest, "msg1a-stood");
        Assert.Equal(200, (await host.PostAsync($"/api/sessions/{stood}/state", new { state = "stood-down" })).Status);
        var running = await RunningAsync(await PublishAsync("A quest whose session runs"), "msg1a-running");
        await host.Composed.Sessions.MirrorAsync(new Session(
            "cd34ef56", quest, "Keeper", "claude-code", SessionState.Completed, "landed.", null, null,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow) { Origin = "alice-laptop" });
        var asked = await host.PostAsync("/api/asks", new { workspace = "default", sentence = "Find who should build the weekly page." });
        var ask = asked.Json.GetProperty("ask").GetProperty("id").GetString()!;
        var intake = await host.PostAsync("/api/sessions/intake", new { ask, room = Path.Combine(host.Repositories, "rooms", "msg1a") });
        Assert.Equal(200, intake.Status);
        var intakeId = intake.Json.GetProperty("session").GetProperty("id").GetString()!;

        var stoodDown = await host.PostAsync($"/api/sessions/{stood}/say", new { text = "Do it anyway." });
        var theirs = await host.PostAsync("/api/sessions/cd34ef56/say", new { text = "One more thing." });
        var anIntake = await host.PostAsync($"/api/sessions/{intakeId}/say", new { text = "Ask Keeper." });
        var working = await host.PostAsync($"/api/sessions/{running}/say", new { text = "Hello." });
        var blank = await host.PostAsync($"/api/sessions/{running}/say", new { text = "  " });
        var unknown = await host.PostAsync("/api/sessions/nothing3/say", new { text = "Hello." });

        Assert.Equal((409, "stood-down", quest), (stoodDown.Status, Refusal(stoodDown), stoodDown.Json.GetProperty("quest").GetString()));
        Assert.Equal((await host.Composed.Ledger.SayAsync(stood, "Do it anyway.", null, DateTimeOffset.UtcNow)).Message, stoodDown.Error);
        Assert.Equal((409, "not-ours", "alice-laptop"), (theirs.Status, Refusal(theirs), theirs.Json.GetProperty("origin").GetString()));
        Assert.Equal((409, "intake", ask), (anIntake.Status, Refusal(anIntake), anIntake.Json.GetProperty("ask").GetString()));
        Assert.Equal((409, "running"), (working.Status, Refusal(working)));
        Assert.Equal((400, "no-words"), (blank.Status, Refusal(blank)));
        Assert.Equal((404, "not-found"), (unknown.Status, Refusal(unknown)));
        Assert.Equal(200, (await host.PostAsync($"/api/sessions/{running}/state", new { state = "stopped" })).Status);
        Assert.Equal(200, (await host.PostAsync($"/api/sessions/{intakeId}/state", new { state = "stopped" })).Status);

        static string? Refusal(Answer answer) => answer.Json.GetProperty("refusal").GetString();
    }

    /// <summary>
    /// MSG1a (D137 §2.4, D133 §1): the person's words reach the ask the session's work is for. Said to a parked session,
    /// through the say door as through the answer door, they are its answer and kept at once. Said to an ended one, they
    /// are kept once taken, as `reopened`, said to the session that took them; an answer taken off the record is not kept
    /// twice.
    /// </summary>
    [Fact]
    public async Task Words_reach_the_ask_as_an_answer_at_once_and_as_reopened_once_taken()
    {
        var asked = await host.PostAsync("/api/asks", new
        {
            workspace = "default", sentence = "Build the monthly report through the v3 bridge.", to = "Keeper",
        });
        var ask = asked.Json.GetProperty("ask").GetProperty("id").GetString()!;
        var quest = asked.Json.GetProperty("quest").GetProperty("id").GetString()!;
        var id = await RunningAsync(quest, "msg1a-ask");
        Assert.Equal(200, (await host.PostAsync($"/api/sessions/{id}/state", new { state = "awaiting-person", note = "Which report?" })).Status);

        var answer = await host.PostAsync($"/api/sessions/{id}/say", new { text = "use the common-report" });
        Assert.Equal(200, answer.Status);
        Assert.Equal(["asked", "answered"], await KindsAsync());

        foreach (var state in new[] { "working", "completed" })
        {
            Assert.Equal(200, (await host.PostAsync($"/api/sessions/{id}/state", new { state })).Status);
        }

        var later = await host.PostAsync($"/api/sessions/{id}/say", new { text = "and add the changelog line" });
        Assert.Equal(["asked", "answered"], await KindsAsync());

        var taken = await host.PostAsync($"/api/sessions/{id}/taken", new
        {
            said = new[] { answer.Json.GetProperty("said").GetProperty("id").GetString(), later.Json.GetProperty("said").GetProperty("id").GetString() },
        });

        Assert.Equal(200, taken.Status);
        var words = (await host.GetAsync($"/api/asks/{ask}")).Json.GetProperty("words").EnumerateArray().ToList();
        Assert.Equal(["asked", "answered", "reopened"], words.Select(w => w.GetProperty("kind").GetString()));
        Assert.Equal(("and add the changelog line", id, quest),
            (words[2].GetProperty("text").GetString(), words[2].GetProperty("session").GetString(), words[2].GetProperty("quest").GetString()));

        async Task<IEnumerable<string?>> KindsAsync() =>
            (await host.GetAsync($"/api/asks/{ask}")).Json.GetProperty("words").EnumerateArray().Select(w => w.GetProperty("kind").GetString()).ToList();
    }

    /// <summary>
    /// TOOL4c (D125 §5.2): the driver says a failure was an account's limit through the state door, and the
    /// record answers it back to every caller, off this machine too: the flag names no account, so it
    /// travels where the account's name does not. Asked of any other move, it is refused, 409.
    /// </summary>
    [Fact]
    public async Task A_failure_the_driver_says_was_a_limit_is_kept_and_answered_to_any_caller()
    {
        var quest = await PublishAsync("A quest whose session an account's limit cut off");
        var opened = await host.PostAsync("/api/sessions", new { quest, adapter = "stub", profile = "account-limited-here" });
        Assert.Equal(200, opened.Status);
        var id = opened.Json.GetProperty("session").GetProperty("id").GetString()!;
        Assert.False(opened.Json.GetProperty("session").GetProperty("limit").GetBoolean());
        foreach (var state in new[] { "starting", "working" })
        {
            Assert.Equal(200, (await host.PostAsync($"/api/sessions/{id}/state", new { state })).Status);
        }

        var refused = await host.PostAsync($"/api/sessions/{id}/state", new { state = "stopped", limit = true });
        Assert.Equal(409, refused.Status);
        Assert.Contains("failed", refused.Error);

        var failed = await host.PostAsync(
            $"/api/sessions/{id}/state", new { state = "failed", note = "the ACP agent refused the call.", limit = true });

        Assert.Equal(200, failed.Status);
        Assert.True(failed.Json.GetProperty("session").GetProperty("limit").GetBoolean());
        foreach (var from in new[] { DaorisHost.Loopback, DaorisHost.OffMachine })
        {
            var answer = await host.GetAsync("/api/sessions?includeClosed=true", from);
            var listed = answer.Json.EnumerateArray().Single(row => row.GetProperty("id").GetString() == id);
            Assert.True(listed.GetProperty("limit").GetBoolean());
        }

        Assert.DoesNotContain("account-limited-here", (await host.GetAsync("/api/sessions?includeClosed=true", DaorisHost.OffMachine)).Body);
    }

    /// <summary>A chat in <c>Keeper</c>, in a tree of its own so it holds nothing another test opens, ended <c>completed</c>.</summary>
    private async Task<string> EndedChatAsync(string tree)
    {
        var opened = await host.PostAsync("/api/sessions/chat", new { repository = "Keeper", adapter = "stub", tree = host.RootOf(tree) });
        Assert.Equal(200, opened.Status);
        var id = opened.Json.GetProperty("session").GetProperty("id").GetString()!;
        foreach (var state in new[] { "starting", "working", "completed" })
        {
            Assert.Equal(200, (await host.PostAsync($"/api/sessions/{id}/state", new { state })).Status);
        }

        return id;
    }

    private async Task<JsonElement?> ListedSessionAsync(string id) =>
        (await host.GetAsync("/api/sessions?includeClosed=true")).Json.EnumerateArray()
            .Cast<JsonElement?>().SingleOrDefault(row => row!.Value.GetProperty("id").GetString() == id);

    /// <summary>
    /// SESSUX1f (D126 §5.4): a conversation that served no quest is listed as deletable and goes, with the ledger's
    /// sentence; the judgement alone, which the driver asks before its own half, answers the same and deletes nothing.
    /// </summary>
    [Fact]
    public async Task A_conversation_that_served_no_quest_is_deletable_and_deleted()
    {
        var id = await EndedChatAsync("keeper-chat-deleted");
        Assert.True((await ListedSessionAsync(id))!.Value.GetProperty("deletable").GetBoolean());

        var judged = await host.GetAsync($"/api/sessions/{id}/deletable");
        Assert.Equal(200, judged.Status);
        Assert.True(judged.Json.GetProperty("deletable").GetBoolean());
        Assert.NotNull(await ListedSessionAsync(id));

        var deleted = await host.DeleteAsync($"/api/sessions/{id}");

        Assert.Equal(200, deleted.Status);
        Assert.Equal(id, deleted.Json.GetProperty("id").GetString());
        Assert.Equal($"Deleted session `{id}`: its record is gone from this machine.", deleted.Json.GetProperty("message").GetString());
        Assert.Null(await ListedSessionAsync(id));
    }

    /// <summary>
    /// SESSUX1f: a session that served a quest is listed as not deletable, and its delete is a 409 carrying the ledger's
    /// sentence verbatim and the refusal's word and facts, which the driver reads instead of the sentence. The judgement
    /// answers the same refusal.
    /// </summary>
    [Fact]
    public async Task A_session_that_served_a_quest_is_refused_with_409_its_word_and_the_ledgers_sentence()
    {
        var quest = await PublishAsync("A quest whose session is that work's record");
        var opened = await host.PostAsync("/api/sessions", new { quest, adapter = "stub", tree = host.RootOf("keeper-served") });
        var id = opened.Json.GetProperty("session").GetProperty("id").GetString()!;
        Assert.Equal(200, (await host.PostAsync($"/api/sessions/{id}/state", new { state = "failed" })).Status);
        Assert.False((await ListedSessionAsync(id))!.Value.GetProperty("deletable").GetBoolean());

        var refused = await host.DeleteAsync($"/api/sessions/{id}");

        Assert.Equal(409, refused.Status);
        Assert.Equal((await host.Composed.Ledger.JudgeDeleteAsync(id)).Message, refused.Error);
        Assert.Equal(("served-quest", quest), (refused.Json.GetProperty("refusal").GetString(), refused.Json.GetProperty("quest").GetString()));
        Assert.NotNull(await ListedSessionAsync(id));

        var judged = await host.GetAsync($"/api/sessions/{id}/deletable");
        Assert.Equal(200, judged.Status);
        Assert.False(judged.Json.GetProperty("deletable").GetBoolean());
        Assert.Equal(("served-quest", refused.Error), (judged.Json.GetProperty("refusal").GetString(), judged.Json.GetProperty("error").GetString()));
    }

    [Fact]
    public async Task A_session_that_is_not_held_answers_404_to_a_delete_and_its_judgement()
    {
        var missing = await host.DeleteAsync("/api/sessions/nope1234");
        var judged = await host.GetAsync("/api/sessions/nope1234/deletable");

        Assert.Equal((404, 404), (missing.Status, judged.Status));
        Assert.Contains("`nope1234`", missing.Error);
        Assert.Equal("not-found", missing.Json.GetProperty("refusal").GetString());
    }

    // ——— Clearing finished history (HIST1b, D153; the history-clearing design §6.3): listed first, then pressed (D88).

    /// <summary>A quest of <c>Asker</c>'s to <c>Keeper</c>, taken and closed done through the respond door.</summary>
    private async Task<string> ClosedAsync(string title)
    {
        var quest = await PublishAsync(title);
        Assert.Equal(200, (await host.PostAsync($"/api/quests/{quest}/respond", new { action = "take" })).Status);
        Assert.Equal(200, (await host.PostAsync($"/api/quests/{quest}/respond", new { action = "done", reason = "Landed." })).Status);
        return quest;
    }

    private async Task<bool> HeldAsync(string quest) =>
        (await host.GetAsync("/api/quests?includeClosed=true")).Json.EnumerateArray()
            .Any(row => row.GetProperty("id").GetString() == quest);

    /// <summary>
    /// A closed quest is listed with what a clear takes, by its quest and with its workspace, deleting nothing; the press
    /// names it and clears exactly it, and the answer says what went. Here nothing was ever numbered by a remote, so it
    /// simply goes, row and log together.
    /// </summary>
    [Fact]
    public async Task A_closed_quest_is_listed_then_cleared_through_the_history_doors()
    {
        var quest = await ClosedAsync("A quest whose history is cleared");

        var listed = await host.GetAsync($"/api/history?quest={quest}");
        var workspace = await host.GetAsync("/api/history?workspace=default");

        Assert.Equal((200, 200), (listed.Status, workspace.Status));
        var unit = Assert.Single(listed.Json.GetProperty("units").EnumerateArray());
        Assert.Equal(("quest", quest, true), (unit.GetProperty("kind").GetString(), unit.GetProperty("id").GetString(), unit.GetProperty("clearable").GetBoolean()));
        Assert.Equal([quest], unit.GetProperty("quests").EnumerateArray().Select(id => id.GetString()));
        Assert.Empty(unit.GetProperty("forgotten").EnumerateArray());
        Assert.False(unit.TryGetProperty("refusal", out _));
        Assert.Contains(workspace.Json.GetProperty("units").EnumerateArray(), row => row.GetProperty("id").GetString() == quest);
        Assert.True(await HeldAsync(quest));

        var cleared = await host.PostAsync("/api/history/clear", new { units = new[] { new { kind = "quest", id = quest } } });

        Assert.Equal(200, cleared.Status);
        var outcome = Assert.Single(cleared.Json.GetProperty("units").EnumerateArray());
        Assert.True(outcome.GetProperty("cleared").GetBoolean());
        Assert.StartsWith($"Cleared `#{quest}` from this machine", outcome.GetProperty("message").GetString());
        Assert.Equal(quest, outcome.GetProperty("unit").GetProperty("id").GetString());
        Assert.False(await HeldAsync(quest));
        Assert.Empty(await host.Composed.Quests.HistoryAsync(quest));
    }

    /// <summary>
    /// A unit that may not go is listed with its refusal's word, the desk's sentence as <c>error</c> and what it names,
    /// as SESSUX1f's refusals are, so the driver reads the word and never the sentence; pressed, it stays.
    /// </summary>
    [Fact]
    public async Task A_unit_that_stays_answers_its_word_the_desks_sentence_and_what_it_names()
    {
        var quest = await PublishAsync("A quest still open, so its history stays");

        var listed = await host.GetAsync($"/api/history?quest={quest}");
        var pressed = await host.PostAsync("/api/history/clear", new { units = new[] { new { kind = "quest", id = quest } } });

        Assert.Equal(200, listed.Status);
        var refusal = listed.Json.GetProperty("units")[0].GetProperty("refusal");
        Assert.Equal(("open", quest), (refusal.GetProperty("refusal").GetString(), refusal.GetProperty("quest").GetString()));
        Assert.Equal(
            (await host.Composed.History.PlanAsync(new HistoryUnitRef(HistoryUnitKind.Quest, quest))).Refusal!.Message,
            refusal.GetProperty("error").GetString());
        var outcome = pressed.Json.GetProperty("units")[0];
        Assert.Equal((200, false), (pressed.Status, outcome.GetProperty("cleared").GetBoolean()));
        Assert.Equal("open", outcome.GetProperty("unit").GetProperty("refusal").GetProperty("refusal").GetString());
        Assert.True(await ListedAsync(quest));
    }

    /// <summary>A listing names exactly one scope, and a press names each unit by a kind it knows and an id: anything else is 400.</summary>
    [Fact]
    public async Task The_history_doors_refuse_a_request_that_names_no_unit()
    {
        foreach (var query in new[] { "", "?workspace=default&quest=abc", "?ask=abcdef&failed=true", "?workspace=default&failed=true" })
        {
            var refused = await host.GetAsync($"/api/history{query}");
            Assert.True(refused.Status == 400, $"GET /api/history{query} answered {refused.Status}");
        }

        foreach (var body in new object[]
                 {
                     new { },
                     new { units = Array.Empty<object>() },
                     new { units = new[] { new { kind = "workspace", id = "default" } } },
                     new { units = new[] { new { kind = "quest", id = " " } } },
                 })
        {
            Assert.Equal(400, (await host.PostAsync("/api/history/clear", body)).Status);
        }
    }

    /// <summary>
    /// The same words as a quest this machine forgot are refused at the publish door with 409, the exchange's sentence,
    /// as a closed quest's move is: the remote still holds it closed (HIST1b, H5). Forgotten here by the store's own rows,
    /// as a clear of a quest a remote numbered leaves them, since this host has no remote.
    /// </summary>
    [Fact]
    public async Task The_same_words_as_a_forgotten_quest_are_refused_at_the_publish_door_with_409()
    {
        var quest = await ClosedAsync("A quest a remote held, forgotten here");
        await using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={host.Database}"))
        {
            await connection.OpenAsync();
            await using var forget = connection.CreateCommand();
            forget.CommandText = """
                INSERT INTO quest_forgotten (id, at) VALUES ($id, '2026-10-07T10:00:00Z');
                DELETE FROM quest_log WHERE quest = $id; DELETE FROM quests WHERE id = $id;
                """;
            forget.Parameters.AddWithValue("$id", quest);
            await forget.ExecuteNonQueryAsync();
        }

        var refused = await host.PostAsync("/api/quests", new
        {
            from = "Asker", to = "Keeper", title = "A quest a remote held, forgotten here", body = "Asked again.",
        });

        Assert.Equal(409, refused.Status);
        Assert.StartsWith($"Quest `#{quest}` was cleared from this machine;", refused.Error);
        Assert.False(await HeldAsync(quest));
    }

    /// <summary>
    /// The page is served here — which is what makes the shared host's 404 for the same file a refusal
    /// rather than a missing file.
    /// </summary>
    [Fact]
    public async Task The_page_is_served_on_a_local_host()
    {
        var page = await host.GetAsync("/");

        Assert.Equal(200, page.Status);
        Assert.Contains(DaorisHost.PageMarker, page.Body);
    }
}
