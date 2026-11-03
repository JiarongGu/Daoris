using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// CONTEXTONE1e (D230 point 3; the context-one design §3 E): a session Daoris runs on Claude Code holds the tree's skills and Daoris's
/// per-session plugin copy, and none of the vendor's account-synced skills, plugins or connectors, with its auto-memory off, all through
/// the session's own settings file. Which account runs it changes none of that, and its home is never written.
/// </summary>
public sealed class OwnContextTests : IDisposable
{
    private readonly string _root = Path.GetFullPath(Path.Combine("_fixtures", "owncontext-" + Guid.NewGuid().ToString("N")[..8]));

    private string Home => Path.Combine(_root, "home");

    private string Tree => Path.Combine(_root, "engine");

    public OwnContextTests()
    {
        Directory.CreateDirectory(Home);
        Directory.CreateDirectory(Tree);
    }

    public void Dispose() => GitSeed.Discard(_root);

    /// <summary>🔴 Every session's settings file says the four keys, whatever else it hands or does not.</summary>
    [Fact]
    public void Every_settings_file_hides_the_account_synced_set_and_turns_auto_memory_off()
    {
        var bare = SpawnSettings.Write(Home, "s-bare", RuleLists.Empty, []);
        var ruled = SpawnSettings.Write(Home, "s-ruled", new RuleLists(["Read"], [], ["Bash(git push*)"]), ["Bash(rm -rf*)"]);

        foreach (var file in new[] { bare, ruled })
        {
            AssertOwnContext(JsonNode.Parse(File.ReadAllText(file))!);
        }
    }

    /// <summary>
    /// 🔴 The design's proof: two accounts' spawns, on either Claude door, are handed the same skills, connectors and settings, their
    /// auto-memory off, and differ only in which account home they name; both homes are byte-identical before and after.
    /// </summary>
    [Theory]
    [InlineData("claude-code")]
    [InlineData("claude-code-acp")]
    public void Two_accounts_spawns_hold_the_same_context_and_leave_both_homes_as_they_were(string name)
    {
        var adapters = AdapterSet.Built();
        var adapter = adapters.Resolve(name);
        using var service = new ServiceClient("http://127.0.0.1:9", null);
        var driver = new Daoris.Driver.Driver(service, DriverConfig.Empty, adapters, Home);
        var accounts = new[] { Account("first"), Account("second") };
        var before = accounts.Select(Snapshot).ToList();

        var spawns = new List<(ProcessStartInfo Info, JsonNode Settings, string? Meta)>();
        foreach (var account in accounts)
        {
            var selection = new HarnessSelection(null, Profile: Path.GetFileName(account), ProfileHome: account);
            var info = driver.Prepare(adapter, Target(), selection).Info;
            var (file, meta, _) = driver.HandRules(adapter, info, "s-" + Path.GetFileName(account), null, "engine", Tree, kept: null);
            spawns.Add((info, JsonNode.Parse(File.ReadAllText(file!))!, meta is null ? null : JsonSerializer.Serialize(meta)));
            Assert.Equal(account, info.Environment["CLAUDE_CONFIG_DIR"]);
        }

        foreach (var (info, settings, meta) in spawns)
        {
            AssertOwnContext(settings);
            Assert.False(info.Environment.TryGetValue("CLAUDE_CODE_DISABLE_AUTO_MEMORY", out var forced) && forced is "0" or "false",
                "an account's own variables cannot turn auto-memory back on");
            // The skills a spawn is handed are the tree's and its own plugin copy's: none points into an account's home.
            Assert.All(accounts, account => Assert.DoesNotContain(info.ArgumentList, argument => argument.Contains(account, StringComparison.OrdinalIgnoreCase)));
            if (meta is not null) Assert.All(accounts, account => Assert.DoesNotContain(account.Replace("\\", "\\\\"), meta));
        }

        Assert.Equal(Normal(spawns[0].Settings, "s-first"), Normal(spawns[1].Settings, "s-second"));
        Assert.Equal(Arguments(spawns[0].Info, "s-first"), Arguments(spawns[1].Info, "s-second"));
        Assert.Equal(before, accounts.Select(Snapshot).ToList());
    }

    private static void AssertOwnContext(JsonNode settings)
    {
        Assert.False(settings["autoMemoryEnabled"]!.GetValue<bool>());
        Assert.False(settings["syncClaudeAiSkills"]!.GetValue<bool>());
        Assert.False(settings["syncClaudeAiPlugins"]!.GetValue<bool>());
        Assert.True(settings["disableClaudeAiConnectors"]!.GetValue<bool>());
    }

    /// <summary>An account's home as the vendor leaves one: its sign-in, its own settings, synced skills and plugins, and a memory.</summary>
    private string Account(string name)
    {
        var account = Path.Combine(_root, "accounts", name);
        void Put(string relative, string text)
        {
            var path = Path.Combine(account, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text);
        }

        Put(".credentials.json", "{\"claudeAiOauth\":{\"accessToken\":\"a-" + name + "\",\"refreshToken\":\"r-" + name + "\"}}");
        Put("settings.json", """{"autoMemoryEnabled":true,"syncClaudeAiSkills":true}""");
        Put(Path.Combine("skills", "synced", $"{name}-skill", "SKILL.md"), $"---\nname: {name}-skill\ndescription: synced\n---\n");
        Put(Path.Combine("plugins", "synced", $"{name}-plugin", "plugin.json"), $$"""{"name":"{{name}}-plugin"}""");
        Put(Path.Combine("projects", "engine", "memory", "MEMORY.md"), $"- {name} remembers\n");
        return account;
    }

    /// <summary>Every file under <paramref name="folder"/> by its relative path, and its bytes' digest.</summary>
    private static string Snapshot(string folder) => string.Join('\n', Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
        .Select(path => $"{Path.GetRelativePath(folder, path)} {Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))}")
        .Order(StringComparer.Ordinal));

    /// <summary>A spawn's settings with its own session's name taken out, so two sessions' compare.</summary>
    private static string Normal(JsonNode settings, string session) => settings.ToJsonString().Replace(session, "<session>");

    private static string Arguments(ProcessStartInfo info, string session) =>
        string.Join('\n', info.ArgumentList.Select(argument => argument.Replace(session, "<session>")));

    private SessionTarget Target() => new(
        QuestId: "q1", Title: "Serve the report", Body: "The report needs a port.", Asker: "ask #a1", Repository: "engine", Root: Tree,
        ServiceUrl: "http://127.0.0.1:9");
}
