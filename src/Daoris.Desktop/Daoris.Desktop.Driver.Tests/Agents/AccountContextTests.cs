using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A session Daoris runs on Claude Code keeps what the account it runs as gives the same harness run by hand: its
/// auto-memory, the skills and plugins the person synced to the account and the account's connectors. The session's settings file
/// says none of them, so the account's own settings decide, and Daoris writes nothing in the account's home.
/// </summary>
public sealed class AccountContextTests : IDisposable
{
    private static readonly string[] AccountKeys = ["autoMemoryEnabled", "syncClaudeAiSkills", "syncClaudeAiPlugins", "disableClaudeAiConnectors"];

    private readonly string _root = Path.GetFullPath(Path.Combine("_fixtures", "accountcontext-" + Guid.NewGuid().ToString("N")[..8]));

    private string Home => Path.Combine(_root, "home");

    private string Tree => Path.Combine(_root, "engine");

    public AccountContextTests()
    {
        Directory.CreateDirectory(Home);
        Directory.CreateDirectory(Tree);
    }

    public void Dispose() => GitSeed.Discard(_root);

    /// <summary>No settings file a session is handed turns the account's memory, synced set or connectors off.</summary>
    [Fact]
    public void No_settings_file_turns_the_accounts_memory_synced_set_or_connectors_off()
    {
        var bare = ClaudeRules.Render(Home, "s-bare", RuleLists.Empty, []);
        var ruled = ClaudeRules.Render(Home, "s-ruled", new RuleLists(["Read"], [], ["Bash(git push*)"]), ["Bash(rm -rf*)"]);

        foreach (var file in new[] { bare, ruled })
        {
            AssertLeavesTheAccount(JsonNode.Parse(File.ReadAllText(file))!);
        }
    }

    /// <summary>
    /// Two accounts' spawns, on either Claude door, are handed the same settings and arguments, none of which names an account's home
    /// or says the account's keys; both homes are byte-identical before and after.
    /// </summary>
    [Theory]
    [InlineData("claude-code")]
    [InlineData("claude-code-acp")]
    public void Two_accounts_spawns_leave_each_accounts_context_to_it_and_both_homes_as_they_were(string name)
    {
        var adapters = ProviderRegistry.Built();
        var adapter = adapters.Door(name);
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
            AssertLeavesTheAccount(settings);
            Assert.False(info.Environment.ContainsKey("CLAUDE_CODE_DISABLE_AUTO_MEMORY"), "no variable turns the account's auto-memory off");
            Assert.All(accounts, account => Assert.DoesNotContain(info.ArgumentList, argument => argument.Contains(account, StringComparison.OrdinalIgnoreCase)));
            if (meta is not null) Assert.All(accounts, account => Assert.DoesNotContain(account.Replace("\\", "\\\\"), meta));
        }

        Assert.Equal(Normal(spawns[0].Settings, "s-first"), Normal(spawns[1].Settings, "s-second"));
        Assert.Equal(Arguments(spawns[0].Info, "s-first"), Arguments(spawns[1].Info, "s-second"));
        Assert.Equal(before, accounts.Select(Snapshot).ToList());
    }

    private static void AssertLeavesTheAccount(JsonNode settings) =>
        Assert.All(AccountKeys, key => Assert.False(settings.AsObject().ContainsKey(key), $"the session's settings say `{key}`"));

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
