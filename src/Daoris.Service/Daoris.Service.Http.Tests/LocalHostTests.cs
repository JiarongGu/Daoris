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
    /// ANSWER1b (D131 §5): the answer door replies with the session as it now stands, still `awaiting-person`, its words
    /// kept and said on its note, which is how the driver tells a watcher nothing moved; a second answer replaces the
    /// first. The words are answered to this machine only, like a transcript, and a record that is not parked is a 409.
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
        Assert.Equal(("awaiting-person", "9090.", "which port?\n\nAnswered: 9090."),
            (session.GetProperty("state").GetString(), session.GetProperty("answer").GetString(), session.GetProperty("note").GetString()));
        Assert.Equal($"Answered session `{id}`: it carries on with `#{quest}` at the driver's next look.",
            answered.Json.GetProperty("message").GetString());
        var offMachine = (await host.GetAsync("/api/sessions?includeClosed=true", DaorisHost.OffMachine)).Json.EnumerateArray()
            .Single(row => row.GetProperty("id").GetString() == id);
        Assert.Equal("awaiting-person", offMachine.GetProperty("state").GetString());
        Assert.False(offMachine.TryGetProperty("answer", out _));

        // The record holds its tree until the driver goes on with it; stopped, nothing of it holds a later test.
        Assert.Equal(200, (await host.PostAsync($"/api/sessions/{id}/state", new { state = "stopped" })).Status);
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
