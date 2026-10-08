using System.Text.Json;

namespace Daoris.Service.Http.Tests;

/// <summary>
/// REVIEWENV1b3 (D154's REVIEWENV1b3 note; the review environment design §3.3, §3.5): what the local host's doors refuse of
/// a review on the record. Each was seen failing before its fix: a verdict whose set-up reference was absent or half given
/// approved the newest set-up, which the person may never have looked at, and a chain sent with a null step answered 500.
/// </summary>
public sealed class ReviewGuardHostTests(LocalHost host) : IClassFixture<LocalHost>
{
    private const string Commit = "a1b2c3d4e5f60718293a4b5c6d7e8f9012345678";
    private const string Later = "0123456789abcdef0123456789abcdef01234567";

    /// <summary>The build quest, taken and closed done, and the set-up step its close published: its id.</summary>
    private async Task<string> SetUpStepAsync(string title)
    {
        var published = await host.PostAsync("/api/quests", new
        {
            from = "Asker", to = "Keeper", title, body = "For the weekly report.",
            then = new[] { new { to = "Keeper", title = "Show {parent} in local for review", body = "Set it up.", setUpIn = "local" } },
        });
        Assert.Equal(200, published.Status);
        var build = published.Json.GetProperty("quest").GetProperty("id").GetString()!;
        Assert.Equal(200, (await host.PostAsync($"/api/quests/{build}/respond", new { action = "take" })).Status);
        Assert.Equal(200, (await host.PostAsync($"/api/quests/{build}/respond", new { action = "done", reason = "Built." })).Status);
        return (await host.GetAsync("/api/quests?includeClosed=true")).Json.EnumerateArray()
            .Single(row => row.TryGetProperty("parent", out var parent) && parent.GetString() == build)
            .GetProperty("id").GetString()!;
    }

    private static (string Machine, long Sequence) Named(JsonElement setUp) =>
        (setUp.GetProperty("machine").GetString()!, setUp.GetProperty("sequence").GetInt64());

    /// <summary>
    /// 🔴 The person looked at the first set-up; a second was posted after. A `reviewed` or a `not-yet` that names no set-up,
    /// or names one by half its reference, is refused 400 with a sentence, and one naming the first is refused 409 as stale:
    /// none approves the second unseen. Named in full, the second is reviewed.
    /// </summary>
    [Fact]
    public async Task A_verdict_names_the_set_up_the_persons_view_showed_or_is_refused()
    {
        var step = await SetUpStepAsync("Add the export button");
        var first = await host.PostAsync($"/api/quests/{step}/set-up", new { commit = Commit, kind = "deployed", look = "https://dev.example.test/r/1" });
        Assert.Equal(200, first.Status);
        Assert.Equal(200, (await host.PostAsync($"/api/quests/{step}/done", new { })).Status);
        var second = await host.PostAsync($"/api/quests/{step}/set-up", new { commit = Later, kind = "deployed", look = "https://dev.example.test/r/2" });
        Assert.Equal(200, second.Status);
        var setUps = second.Json.GetProperty("quest").GetProperty("setUps");
        var (machine, looked) = Named(setUps[0]);
        var (_, newest) = Named(setUps[1]);

        var unnamed = await host.PostAsync($"/api/quests/{step}/review", new { verdict = "reviewed" });
        Assert.Equal(400, unnamed.Status);
        Assert.Contains("names the set-up you looked at", unnamed.Error);
        Assert.Equal(400, (await host.PostAsync($"/api/quests/{step}/review", new { verdict = "not-yet", words = "the label is wrong" })).Status);

        var half = await host.PostAsync($"/api/quests/{step}/review", new { verdict = "reviewed", setUp = new { machine } });
        Assert.Equal(400, half.Status);
        Assert.Contains("both its `machine` and its `sequence`", half.Error);
        Assert.Equal(400, (await host.PostAsync($"/api/quests/{step}/review", new { verdict = "reviewed", setUp = new { sequence = newest } })).Status);

        var stale = await host.PostAsync($"/api/quests/{step}/review", new { verdict = "reviewed", setUp = new { machine, sequence = looked } });
        Assert.Equal(409, stale.Status);
        Assert.Contains("shown again since", stale.Error);

        var reviewed = await host.PostAsync($"/api/quests/{step}/review", new { verdict = "reviewed", setUp = new { machine, sequence = newest } });
        Assert.Equal(200, reviewed.Status);
        Assert.Equal(Later, reviewed.Json.GetProperty("quest").GetProperty("verdicts")[0].GetProperty("commit").GetString());
    }

    /// <summary>A skip answers no set-up, so it needs no reference.</summary>
    [Fact]
    public async Task A_skip_needs_no_set_up_reference()
    {
        var step = await SetUpStepAsync("Add the import button");
        Assert.Equal(200, (await host.PostAsync($"/api/quests/{step}/set-up", new { commit = Commit, kind = "deployed", look = "https://dev.example.test/r/3" })).Status);
        Assert.Equal(200, (await host.PostAsync($"/api/quests/{step}/done", new { })).Status);

        var skipped = await host.PostAsync($"/api/quests/{step}/review", new { verdict = "skipped", words = "a wording change" });

        Assert.Equal(200, skipped.Status);
        Assert.False(skipped.Json.GetProperty("quest").GetProperty("held").GetBoolean());
    }

    /// <summary>
    /// 🔴 A chain sent with a null step is refused naming the step, as a step missing its words is, at both publish doors:
    /// 400 at the quest door, and the ask door's answer for a quest it could not publish. Never a 500.
    /// </summary>
    [Fact]
    public async Task A_null_chain_step_is_refused_naming_it()
    {
        var quest = await host.PostAsync("/api/quests", new
        {
            from = "Asker", to = "Keeper", title = "A chain with a hole", body = "b", then = new object?[] { null },
        });
        Assert.Equal(400, quest.Status);
        Assert.Contains("Step 1 of the chain needs whom to ask", quest.Error);

        var asked = await host.PostAsync("/api/asks", new { workspace = "default", sentence = "Add a hole to the chain, please." });
        Assert.Equal(200, asked.Status);
        var ask = asked.Json.GetProperty("ask").GetProperty("id").GetString()!;
        var published = await host.PostAsync($"/api/asks/{ask}/publish", new
        {
            to = "Keeper", title = "A chain with a hole", body = "b", then = new object?[] { null },
        });
        Assert.NotEqual(500, published.Status);
        Assert.Contains("Step 1 of the chain needs whom to ask", published.Error);
    }
}
