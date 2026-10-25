using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// NEW1a (D162): <c>START_CHAT</c> with opening words where a real process holds the conversation (MOD8). Apart from
/// <see cref="DriverModuleStartFromTests"/>, whose adapter spawns nothing, so that class runs in the fast half.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class DriverModuleStartWithProcessTests : DriverModuleBridge
{
    /// <summary>
    /// The route answers <c>{sessionId, sent: true}</c> and the conversation's first message holds the words: a text-door
    /// agent (node) writes each line it is handed to a file, and that line is the person's words.
    /// </summary>
    [Fact]
    public async Task A_start_on_words_answers_sent_and_the_conversation_is_handed_them()
    {
        var root = Path.Combine(Home, "engine");
        Directory.CreateDirectory(root);
        var heard = Path.Combine(Home, "heard.txt");
        var script = Path.Combine(Home, "text-agent.mjs");
        File.WriteAllText(script, """
            import { appendFileSync } from 'node:fs';
            import { createInterface } from 'node:readline';
            if (process.argv.includes('--version')) { console.log('stub 1.0.0'); process.exit(0); }
            if (process.argv.includes('--login-state')) { console.log('logged-in'); process.exit(0); }
            const heard = process.argv[2];
            const lines = createInterface({ input: process.stdin });
            lines.on('line', (line) => appendFileSync(heard, line + '\n'));
            lines.on('close', () => { process.exitCode = 0; process.stdin.destroy(); });
            """);
        (DriverConfig.Empty with
        {
            Adapter = "stub",
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["stub"] = ["node", script, heard] },
        }).Save(DriverConfigPath);

        var loop = Loop();
        var service = new DriverModuleStartFromTests.StandIn(root);
        var adapters = AdapterSet.Built();
        using var client = new ServiceClient("http://stand-in", null, new HttpClient(service));
        using var chat = new ChatRunner(
            client, adapters, loop.Home, loop.Processes, loop.Output, new HarnessRoster(adapters, HarnessSettingsPath), loop.Events);
        await loop.ComeUpAsync(client, chat);
        var module = new DriverModule(Bus, loop);

        var started = await AnswerAsync(module, "START_CHAT", new { repository = "engine", text = "Fix the build.", requestId = "req-1" });

        try
        {
            Assert.Equal("c1", started.GetProperty("sessionId").GetString());
            Assert.True(started.GetProperty("sent").GetBoolean());
            Assert.Equal(JsonValueKind.Null, started.GetProperty("why").ValueKind);
            await UntilAsync(() => File.Exists(heard) && File.ReadAllText(heard).Contains("Fix the build.", StringComparison.Ordinal));
        }
        finally
        {
            chat.Finish("c1");
        }
    }

    /// <summary>
    /// FOUNDATION1a-ii: the page's account picker is the person's pick. <c>START_CHAT</c> with a <c>profile</c>, with opening words or
    /// without, keeps on the conversation's binding in the desktop store that they picked it; one that names none keeps none, the walk's
    /// account being no pick of theirs.
    /// </summary>
    [Fact]
    public async Task A_start_that_names_a_profile_keeps_the_pick_on_the_binding_with_words_or_without_and_one_that_names_none_keeps_none()
    {
        var root = Path.Combine(Home, "engine");
        Directory.CreateDirectory(root);
        var script = Path.Combine(Home, "text-agent.mjs");
        File.WriteAllText(script, """
            import { createInterface } from 'node:readline';
            if (process.argv.includes('--version')) { console.log('stub 1.0.0'); process.exit(0); }
            if (process.argv.includes('--login-state')) { console.log('logged-in'); process.exit(0); }
            const lines = createInterface({ input: process.stdin });
            lines.on('line', () => { });
            lines.on('close', () => { process.exitCode = 0; process.stdin.destroy(); });
            """);
        (DriverConfig.Empty with
        {
            Adapter = "stub",
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["stub"] = ["node", script] },
        }).Save(DriverConfigPath);
        var loop = Loop();
        foreach (var account in new[] { "account-1", "account-2" })
        {
            Directory.CreateDirectory(HarnessSettings.ProfileHome(loop.Home, "stub", account));
        }

        new HarnessSettings().WithRotation("stub", ["account-1", "account-2"]).Save(HarnessSettingsPath);

        var service = new DriverModuleStartFromTests.StandIn(root);
        var adapters = AdapterSet.Built();
        using var client = new ServiceClient("http://stand-in", null, new HttpClient(service));
        using var chat = new ChatRunner(
            client, adapters, loop.Home, loop.Processes, loop.Output, new HarnessRoster(adapters, HarnessSettingsPath), loop.Events);
        await loop.ComeUpAsync(client, chat);
        var module = new DriverModule(Bus, loop);

        var bare = await AnswerAsync(module, "START_CHAT", new { repository = "engine", profile = "account-1" });
        var words = await AnswerAsync(module, "START_CHAT", new { repository = "engine", text = "Fix the build.", profile = "account-2" });
        var walked = await AnswerAsync(module, "START_CHAT", new { repository = "engine", text = "Fix the build." });

        try
        {
            var conversations = new HarnessConversations(loop.Home);
            Assert.Equal(["c1", "c2", "c3"], new[] { bare, words, walked }.Select(each => each.GetProperty("sessionId").GetString()));
            Assert.True(conversations.PickedOn("c1", "account-1"));
            Assert.True(conversations.PickedOn("c2", "account-2"));
            Assert.False(conversations.PickedOn("c3", "account-1") || conversations.PickedOn("c3", "account-2"));
        }
        finally
        {
            foreach (var id in new[] { "c1", "c2", "c3" }) chat.Finish(id);
        }
    }
}
