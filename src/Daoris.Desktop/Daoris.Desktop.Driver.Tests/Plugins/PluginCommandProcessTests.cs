using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A declared command run against a real plugin process, started as the terminal's command starts it: the real channel passes the
/// round to the plugin and reads its answer. The command tests' stand-in channel could not show that the real one forwards it, and the
/// installed copy answered "this channel runs no commands" until it did.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class PluginCommandProcessTests : IDisposable
{
    private const string Plugin = "acme.tools";

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-cmdproc-" + Guid.NewGuid().ToString("N")[..8]);

    /// <summary>The stand-in: answers the handshake and says the command it is asked, with its arguments.</summary>
    private const string Script = """
        import { createInterface } from 'node:readline';
        const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
        for await (const line of createInterface({ input: process.stdin })) {
          const frame = JSON.parse(line);
          if (frame.method === 'initialize') {
            send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, points: frame.params.points } });
          } else if (frame.method === 'hook/command/run') {
            send({ jsonrpc: '2.0', id: frame.id, result: { said: [`ran ${frame.params.command} ${Object.entries(frame.params.args).map(([name, value]) => `--${name}=${value}`).join(' ')} at ${JSON.stringify(frame.params.place)} in ${frame.params.language}`], exit: 0 } });
          } else if (frame.method === 'shutdown') {
            process.exit(0);
          }
        }
        """;

    public PluginCommandProcessTests()
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.Plugins, Plugin);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "plugin.json"), """
            { "id": "acme.tools",
              "hooks": { "command": ["node", "${plugin}/tools.mjs"], "points": ["work/watch"] },
              "interfaces": { "commands": [{ "name": "status", "about": "Says how it stands." }] } }
            """);
        File.WriteAllText(Path.Combine(folder, "tools.mjs"), Script);
    }

    public void Dispose()
    {
        for (var attempt = 0; Directory.Exists(_home); attempt++)
        {
            try
            {
                Directory.Delete(_home, recursive: true);
            }
            catch (IOException) when (attempt < 20)
            {
                Thread.Sleep(100);
            }
            catch (UnauthorizedAccessException) when (attempt < 20)
            {
                Thread.Sleep(100);
            }
        }
    }

    [Fact]
    public async Task A_declared_command_reaches_the_plugin_s_own_process_and_its_answer_comes_back()
    {
        var (plugin, _) = PluginCommandRun.Find(_home, Plugin, "status");

        var call = CommandCall.Plain("status", new Dictionary<string, string>(), CommandWire.Window) with
        {
            Args = new Dictionary<string, object?> { ["brief"] = true }, Place = new PluginPlace(Repository: "engine"), Language = "zh",
        };
        var round = await PluginCommandRun.RoundAsync(_home, plugin, call, CancellationToken.None);

        Assert.Contains("""ran status --brief=true at {"kind":"repository","id":"engine"} in zh""", string.Join("\n", round.Said));
    }
}
