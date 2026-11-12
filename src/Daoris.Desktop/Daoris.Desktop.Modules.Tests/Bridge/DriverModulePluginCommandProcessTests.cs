using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// <c>PLUGIN_COMMAND</c> runs one round of a plugin's declared command from the window, in a real process of the plugin's own:
/// <c>from: window</c>, the arguments by name, the place and the language reach it, the run's id is the same across rounds, a masked
/// ask is drawn by the page and its answer sent back, and the commands switched off at the place, an argument missing or a place that
/// names none are refused before any process starts.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
public sealed class DriverModulePluginCommandProcessTests : DriverModuleBridge
{
    private const string Script = """
        import { createInterface } from 'node:readline';
        const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
        for await (const line of createInterface({ input: process.stdin })) {
          const frame = JSON.parse(line);
          if (frame.method === 'initialize') {
            send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, points: frame.params.points } });
          } else if (frame.method === 'hook/command/run') {
            const p = frame.params;
            const result = p.answers.token
              ? { said: [`${p.from} ${p.run} ${p.args.stage} ${JSON.stringify(p.place)} ${p.language}`], then: 'reload', data: { kept: true }, exit: 0 }
              : { said: ['Paste a token.'], ask: { name: 'token', prompt: 'Token', masked: true } };
            send({ jsonrpc: '2.0', id: frame.id, result });
          } else if (frame.method === 'shutdown') {
            process.exit(0);
          }
        }
        """;

    private void Install()
    {
        var folder = DaorisHome.Entry(Home, HomeEntry.Plugins, "acme-desk");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, PluginCatalog.ManifestName), """
            { "id": "acme-desk", "hooks": { "command": ["node", "${plugin}/desk.mjs"], "points": ["work/watch"] },
              "interfaces": { "commands": [{ "name": "start-stage", "about": "Starts a stage.", "interactive": true,
                "args": [{ "name": "stage", "type": "text", "title": "Stage", "required": true }] }] } }
            """);
        File.WriteAllText(Path.Combine(folder, "desk.mjs"), Script);
    }

    [Fact]
    public async Task A_round_runs_from_the_window_at_its_place_and_the_next_carries_the_answer()
    {
        Install();
        using var loop = Loop();
        var module = new DriverModule(Bus, loop) { Offers = Path.Combine(Home, "no-offers") };
        var asked = new { id = "acme-desk", command = "start-stage", args = new { stage = "qa" }, place = "repository:engine", language = "zh" };

        var first = await AnswerAsync(module, "PLUGIN_COMMAND", asked);
        Assert.True(first.GetProperty("ask").GetProperty("masked").GetBoolean());
        var run = first.GetProperty("run").GetString()!;

        var second = await AnswerAsync(module, "PLUGIN_COMMAND", new
        {
            asked.id, asked.command, run, asked.args, answers = new Dictionary<string, string> { ["token"] = "throwaway" }, asked.place, asked.language,
        });

        Assert.Equal($$"""window {{run}} qa {"kind":"repository","id":"engine"} zh""", second.GetProperty("said")[0].GetString());
        Assert.Equal("reload", second.GetProperty("then").GetString());
        Assert.True(second.GetProperty("data").GetProperty("kept").GetBoolean());
        Assert.Equal(0, second.GetProperty("exit").GetInt32());
    }

    [Fact]
    public async Task Commands_switched_off_at_the_place_a_missing_argument_and_no_place_are_refused()
    {
        Install();
        using var loop = Loop();
        var module = new DriverModule(Bus, loop) { Offers = Path.Combine(Home, "no-offers") };
        PluginSwitches.Set(Home, "acme-desk", PluginSwitches.Commands, "repository:engine", on: false);

        Assert.Contains("switched off at repository:engine",
            await RefusalAsync(module, "PLUGIN_COMMAND", new { id = "acme-desk", command = "start-stage", args = new { stage = "qa" }, place = "repository:engine" }),
            StringComparison.Ordinal);
        Assert.Contains("needs `stage`", await RefusalAsync(module, "PLUGIN_COMMAND", new { id = "acme-desk", command = "start-stage" }), StringComparison.Ordinal);
        Assert.Contains("is no place", await RefusalAsync(module, "PLUGIN_COMMAND", new { id = "acme-desk", command = "start-stage", place = "room:x" }), StringComparison.Ordinal);
    }
}
