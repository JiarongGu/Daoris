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
}
