using System.Diagnostics;
using System.Text;
using Daoris.Knowledge;
using Xunit.Abstractions;

namespace Daoris.Service.Http.Tests;

/// <summary>
/// A local host over two repositories, each with enough knowledge that a refresh's transaction stays open while other
/// requests arrive: the shape of the install's index beside its driver loop.
/// </summary>
public sealed class BusyHost() : DaorisHost(ServiceMode.Local, seed: repositories =>
{
    Repository(repositories, "Asker", "Asks for things.");
    Repository(repositories, "Keeper", "Keeps its own area.");
})
{
    private static void Repository(string folder, string name, string summary)
    {
        var dir = Path.Combine(folder, name);
        Directory.CreateDirectory(Path.Combine(dir, ".claude", "knowledge"));
        File.WriteAllText(Path.Combine(dir, "daoris.json"), $$"""
            {
              "source": "s", "packs": [],
              "domain": { "summary": "{{summary}}", "owns": ["its own tree"], "accepts": ["a quest"] }
            }
            """);
        for (var note = 0; note < 60; note++)
        {
            var text = new StringBuilder($"# {name} note {note}\n\n");
            for (var section = 0; section < 6; section++)
            {
                text.Append($"## Section {section}\n\nWhat {name} learned about the bridge, the report and step {note}.{section}.\n\n");
            }

            File.WriteAllText(Path.Combine(dir, ".claude", "knowledge", $"note-{note:D2}.md"), text.ToString());
        }
    }
}

/// <summary>
/// SQLITETX1: the host answers requests at once over the one connection its stores share, and no request may meet
/// another's open transaction. On the install a say answered 500, <i>Execute requires the command to have a transaction
/// object when the connection assigned to the command is in a pending local transaction</i>, while its words were kept:
/// one of its commands was made before another request began a transaction and ran after.
/// </summary>
public sealed class ConcurrentRequestsTests(BusyHost host, ITestOutputHelper output) : IClassFixture<BusyHost>
{
    private const int Says = 50;
    private const int Looks = 50;

    /// <summary>
    /// Fifty says to a parked session beside fifty of the driver's looks (its reads, then a refresh of the index or a quest
    /// published, each a transaction): every request answers, every word is kept on the session and on its
    /// ask exactly once, and the store answers the next request.
    /// </summary>
    [Fact]
    public async Task Says_beside_the_driver_s_looks_all_answer_and_every_word_is_kept_once()
    {
        var asked = await host.PostAsync("/api/asks", new
        {
            workspace = "default", sentence = "Build the monthly report through the v3 bridge.", to = "Keeper",
        });
        Assert.Equal(200, asked.Status);
        var ask = asked.Json.GetProperty("ask").GetProperty("id").GetString()!;
        var quest = asked.Json.GetProperty("quest").GetProperty("id").GetString()!;
        var parked = await RunningAsync(quest, "sqlitetx1-parked");
        Assert.Equal(200, (await host.PostAsync($"/api/sessions/{parked}/state", new { state = "awaiting-person", note = "Which report?" })).Status);

        var failures = new List<string>();
        var slowest = new Dictionary<string, TimeSpan>();
        var requests = Enumerable.Range(0, Says).Select(i => Answered($"say {i}", () =>
                host.PostAsync($"/api/sessions/{parked}/say", new { text = $"word {i:D2}" })))
            .Concat(Enumerable.Range(0, Looks).Select(i => Answered($"look {i}", () => LookAsync(i))));
        var clock = Stopwatch.StartNew();
        await Task.WhenAll(requests.Select(request => Task.Run(request)));
        output.WriteLine(
            $"{Says + Looks} requests in {clock.ElapsedMilliseconds} ms; slowest say {slowest.GetValueOrDefault("say").TotalMilliseconds:F0} ms, "
            + $"slowest look {slowest.GetValueOrDefault("look").TotalMilliseconds:F0} ms");
        var burst = failures.Count;

        // KNOW500's shape: whatever the burst met, the next request on the same store answers.
        await Answered("after: repositories", () => host.GetAsync("/api/repositories"))();
        await Answered("after: search", () => host.GetAsync("/api/search?q=bridge"))();
        await Answered("after: a quest", () => host.PostAsync("/api/quests", new { from = "Asker", to = "Keeper", title = "After the burst", body = "why" }))();

        Assert.True(
            failures.Count == 0,
            $"{burst} of {Says + Looks} requests failed, and {failures.Count - burst} of 3 after:\n" + string.Join('\n', failures));

        var session = (await host.GetAsync("/api/sessions?includeClosed=true")).Json.EnumerateArray()
            .Single(row => row.GetProperty("id").GetString() == parked);
        Assert.Equal(
            Enumerable.Range(0, Says).Select(i => $"word {i:D2}"),
            session.GetProperty("said").EnumerateArray().Select(word => word.GetProperty("text").GetString()).Order());
        var answers = (await host.GetAsync($"/api/asks/{ask}")).Json.GetProperty("words").EnumerateArray()
            .Where(word => word.GetProperty("kind").GetString() == "answered")
            .Select(word => word.GetProperty("text").GetString())
            .Order();
        Assert.Equal(Enumerable.Range(0, Says).Select(i => $"word {i:D2}"), answers);
        Assert.Equal(200, (await host.PostAsync($"/api/sessions/{parked}/state", new { state = "stopped" })).Status);

        Func<Task> Answered(string name, Func<Task<Answer>> send) => async () =>
        {
            var took = Stopwatch.StartNew();
            try
            {
                var answer = await send();
                if (answer.Status != 200)
                {
                    lock (failures) failures.Add($"{name}: {answer.Status} {answer.Body}");
                }
            }
            catch (Exception error)
            {
                lock (failures) failures.Add($"{name}: {error.GetType().FullName}: {error.Message}");
            }

            var kind = name.Split(' ')[0];
            lock (slowest)
            {
                if (took.Elapsed > slowest.GetValueOrDefault(kind)) slowest[kind] = took.Elapsed;
            }
        };
    }

    /// <summary>
    /// The driver's look as the host sees it (`ServiceClient`): the quests, the registry, the sessions; then a refresh of
    /// the index on some, which rewrites each repository's entries in a transaction, and a quest published on the rest,
    /// which is one too.
    /// </summary>
    private async Task<Answer> LookAsync(int i)
    {
        foreach (var path in new[] { "/api/quests", "/api/registry", "/api/sessions", "/api/sessions?includeClosed=true" })
        {
            var read = await host.GetAsync(path);
            if (read.Status != 200) return read;
        }

        return i % 5 == 0
            ? await host.PostAsync("/api/refresh", new { })
            : await host.PostAsync("/api/quests", new { from = "Asker", to = "Keeper", title = $"Look {i}", body = "Asked mid-burst." });
    }

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
}
