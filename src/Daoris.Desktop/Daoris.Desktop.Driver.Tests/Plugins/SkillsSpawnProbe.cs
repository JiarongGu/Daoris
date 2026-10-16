using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What HELPPLUG1c's spawn tests share (D170 point 4): a plugin handing sessions one skill, and a stand-in harness for each door
/// that records, at the moment it is spawned, the plugin folder it was pointed at and the skill it could read there. The pipe
/// door is pointed by <c>--plugin-dir</c>; the protocol door by <c>plugins</c> in <c>session/new</c>'s
/// <c>_meta.claudeCode.options</c>. Both write the probe file their first argument names, and end their turn at once.
/// </summary>
internal static class SkillsSpawnProbe
{
    /// <summary>Both doors, as the adapter that takes the plugin door on each is named.</summary>
    public static IEnumerable<object[]> Doors => [["claude-code"], ["claude-code-acp"]];

    /// <summary>The words the plugin's skill holds, which the harness reads from the composed copy.</summary>
    public const string SkillWords = "Read the board first.";

    /// <summary>Install a plugin that hands sessions one skill, <c>acme.tickets:triage</c>.</summary>
    public static void Install(string home)
    {
        var folder = DaorisHome.Entry(home, HomeEntry.Plugins, "acme.tickets");
        Directory.CreateDirectory(Path.Combine(folder, "skills", "triage"));
        File.WriteAllText(
            Path.Combine(folder, PluginCatalog.ManifestName),
            """{ "id": "acme.tickets", "skills": [ { "name": "triage", "to": ["sessions"] } ] }""");
        File.WriteAllText(
            Path.Combine(folder, "skills", "triage", SpawnSkills.SkillFile),
            $"---\nname: triage\ndescription: Sort the issues by what they block.\n---\n\n{SkillWords}\n");
    }

    /// <summary>
    /// A Daoris account for Claude Code's doors in the home, where a start that walks the roster now runs (OWNSPACE1a, D175
    /// point 2): without one an intake's or a helper's start is held for its sign-in and nothing is spawned. The stand-in
    /// harness answers its sign-in question as signed in. A reviewer's pass is handed its selection, so it needs none.
    /// </summary>
    public static void SignIn(string home) =>
        Directory.CreateDirectory(HarnessSettings.ProfileHome(home, "claude-code", "daoris"));

    /// <summary>The stand-in harness for <paramref name="adapter"/>, written under the home; its command is <c>node script probe</c>.</summary>
    public static IReadOnlyList<string> Command(string home, string adapter)
    {
        var script = Path.Combine(home, adapter + "-skills-probe.mjs");
        File.WriteAllText(script, adapter == "claude-code" ? PipeScript : AcpScript);
        return ["node", script, Probe(home)];
    }

    public static string Probe(string home) => Path.Combine(home, "skills-probe.json");

    /// <summary>
    /// What the spawn handed, as the harness saw it: the plugin folders, and the skill's text read from the first. Fails with the
    /// probe's absence in words, since a spawn that never reached the harness is the likeliest failure.
    /// </summary>
    public static (string[] Plugins, string? Skill) Read(string home)
    {
        Assert.True(File.Exists(Probe(home)), "the harness never recorded its spawn");
        using var probe = JsonDocument.Parse(File.ReadAllText(Probe(home)));
        var plugins = probe.RootElement.GetProperty("plugins").EnumerateArray().Select(path => path.GetString()!).ToArray();
        var skill = probe.RootElement.GetProperty("skill");
        return (plugins, skill.ValueKind == JsonValueKind.String ? skill.GetString() : null);
    }

    /// <summary>The handed skill reached the harness from the home's spawn folder, and no session's plugin folder is left.</summary>
    public static void AssertHandedAndRemoved(string home)
    {
        var (plugins, skill) = Read(home);
        var handed = Assert.Single(plugins);
        Assert.StartsWith(DaorisHome.Entry(home, HomeEntry.Spawn), Path.GetFullPath(handed), StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("acme.tickets", handed.Replace('\\', '/').TrimEnd('/'), StringComparison.Ordinal);
        Assert.Contains(SkillWords, skill);

        var spawn = DaorisHome.Entry(home, HomeEntry.Spawn);
        var left = Directory.Exists(spawn) ? Directory.GetDirectories(spawn, "*.plugins") : [];
        Assert.True(left.Length == 0, "left after the session ended: " + string.Join(", ", left));
    }

    private const string PipeScript = """
        import { writeFileSync, readFileSync, existsSync } from 'node:fs';
        import { join } from 'node:path';
        const argv = process.argv.slice(2);
        if (argv.includes('--version')) { console.log('2.1.299 (Claude Code)'); process.exit(0); }
        if (argv.includes('auth')) { console.log('{"loggedIn": true}'); process.exit(0); }
        if (argv.includes('--login-state')) { console.log('logged-in'); process.exit(0); }
        const plugins = [];
        argv.forEach((word, at) => { if (word === '--plugin-dir') plugins.push(argv[at + 1]); });
        const skill = join(plugins[0] ?? '', 'skills', 'triage', 'SKILL.md');
        writeFileSync(argv[0], JSON.stringify({ plugins, skill: existsSync(skill) ? readFileSync(skill, 'utf8') : null }));
        process.exit(0);
        """;

    private const string AcpScript = """
        import { writeFileSync, readFileSync, existsSync } from 'node:fs';
        import { createInterface } from 'node:readline';
        import { join } from 'node:path';
        const argv = process.argv.slice(2);
        if (argv.includes('--version')) { console.log('0.91.0'); process.exit(0); }
        const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
        createInterface({ input: process.stdin }).on('line', (line) => {
          const frame = JSON.parse(line);
          if (frame.method === 'initialize') {
            send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, agentCapabilities: {} } });
          } else if (frame.method === 'session/new') {
            const plugins = (frame.params?._meta?.claudeCode?.options?.plugins ?? []).map((plugin) => plugin.path);
            const skill = join(plugins[0] ?? '', 'skills', 'triage', 'SKILL.md');
            writeFileSync(argv[0], JSON.stringify({ plugins, skill: existsSync(skill) ? readFileSync(skill, 'utf8') : null }));
            send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: 'probe' } });
          } else if (frame.method === 'session/prompt') {
            send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
          } else if (frame.id !== undefined && frame.method) {
            send({ jsonrpc: '2.0', id: frame.id, result: {} });
          }
        });
        """;
}
