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
