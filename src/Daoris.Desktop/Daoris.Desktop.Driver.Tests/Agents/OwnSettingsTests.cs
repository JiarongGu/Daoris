using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The person's own user settings reach a Claude session merged under what outranks them: Daoris's composed rules, the repository's
/// project settings and the account's own. Lists join as the harness joins them across tiers, so the person's denies hold beside
/// Daoris's; a value set above stays above; the sign-in, the posture, the plugins and anything that would switch Daoris's hooks off
/// are never taken from the person's file.
/// </summary>
public sealed class OwnSettingsTests : IDisposable
{
    private readonly string _root = Path.Combine(TestHomes.Root, "daoris-own-settings-" + Guid.NewGuid().ToString("N")[..8]);

    private string Home => Path.Combine(_root, "daoris");
    private string Person => Path.Combine(_root, "person");
    private string Account => Path.Combine(_root, "account");
    private string Tree => Path.Combine(_root, "tree");
    private string ToolHome => Path.Combine(Person, ".claude");

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static void Put(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private OwnTools Tools(OwnFamilies families, string? sessionHome = null, IReadOnlyDictionary<string, string>? session = null) => new(
        _ => null,
        Person,
        name => name == "CLAUDE_CONFIG_DIR" ? sessionHome ?? Account : session?.GetValueOrDefault(name),
        families);

    private void PersonsSettings() => Put(Path.Combine(ToolHome, "settings.json"), """
        {
          "permissions": { "allow": ["Bash(npm test)"], "deny": ["Bash(curl *)"], "ask": ["WebFetch"], "defaultMode": "bypassPermissions",
                           "additionalDirectories": ["../notes"] },
          "env": { "MY_FLAG": "1", "ANTHROPIC_API_KEY": "sk-person", "ANTHROPIC_MODEL": "person-model", "DAORIS_ROLE": "x" },
          "model": "person-model",
          "outputStyle": "Explanatory",
          "spinnerTipsEnabled": false,
          "enabledPlugins": { "tool@market": true },
          "apiKeyHelper": "print-key",
          "disableAllHooks": true,
          "hooks": { "PreToolUse": [ { "matcher": "Bash", "hooks": [ { "type": "command", "command": "persons-hook" } ] } ] }
        }
        """);

    private JsonObject Written(OwnTools tools, bool shaped = false)
    {
        var file = ClaudeRules.Language.Write(Home, "s1", new HandedRules(new RuleLists(["Read"], [], ["Bash(git push *)"]), [], TreeGuard.For(Home, Tree), null)
        {
            Own = tools,
            Folder = Tree,
            Shaped = shaped,
        });
        return (JsonObject)JsonNode.Parse(File.ReadAllText(file))!;
    }

    private static IEnumerable<string> Strings(JsonNode? list) => list is JsonArray array ? array.Select(item => item!.GetValue<string>()) : [];

    [Fact]
    public void The_persons_rules_join_daoris_lists_and_their_preferences_come_in_where_nothing_above_sets_them()
    {
        PersonsSettings();
        Put(Path.Combine(Account, "settings.json"), """{ "spinnerTipsEnabled": true }""");
        Put(Path.Combine(Tree, ".claude", "settings.json"), """{ "outputStyle": "Concise" }""");

        var written = Written(Tools(OwnFamilies.Settings, session: new Dictionary<string, string> { ["ANTHROPIC_MODEL"] = "role-choice" }));

        var permissions = written["permissions"]!.AsObject();
        Assert.Equal(["Read", "Bash(npm test)"], Strings(permissions["allow"]));
        Assert.Equal(["Bash(git push *)", "Bash(curl *)"], Strings(permissions["deny"]));
        Assert.Equal(["WebFetch"], Strings(permissions["ask"]));
        Assert.Equal(["../notes"], Strings(permissions["additionalDirectories"]));
        // The posture is the start's, never the person's file's.
        Assert.False(permissions.ContainsKey("defaultMode"));

        Assert.Equal("person-model", written["model"]!.GetValue<string>());
        // The repository's own value and the account's own stay above the person's.
        Assert.False(written.ContainsKey("outputStyle"));
        Assert.False(written.ContainsKey("spinnerTipsEnabled"));

        // A variable the session already holds, a sign-in and a Daoris one stay the start's.
        var environment = written["env"]!.AsObject();
        Assert.Equal(["MY_FLAG"], environment.Select(pair => pair.Key));

        // The plugins are their own family; the sign-in is the person's; Daoris's hooks are the floor.
        Assert.False(written.ContainsKey("enabledPlugins"));
        Assert.False(written.ContainsKey("apiKeyHelper"));
        Assert.False(written.ContainsKey("disableAllHooks"));

        // Daoris's guard is read first, the person's hook after it.
        var before = written["hooks"]!["PreToolUse"]!.AsArray();
        Assert.Equal(TreeGuardHook.Matcher, before[0]!["matcher"]!.GetValue<string>());
        Assert.Equal("persons-hook", before[^1]!["hooks"]![0]!["command"]!.GetValue<string>());
    }

    [Fact]
    public void A_pass_shaped_for_one_job_takes_the_persons_denies_and_preferences_but_nothing_that_widens_it()
    {
        PersonsSettings();

        var written = Written(Tools(OwnFamilies.Settings), shaped: true);

        var permissions = written["permissions"]!.AsObject();
        Assert.Equal(["Read"], Strings(permissions["allow"]));
        Assert.Contains("Bash(curl *)", Strings(permissions["deny"]));
        Assert.False(permissions.ContainsKey("additionalDirectories"));
        Assert.Equal("person-model", written["model"]!.GetValue<string>());
        Assert.Single(written["hooks"]!["PreToolUse"]!.AsArray());
    }

    [Fact]
    public void Nothing_of_the_persons_file_is_merged_with_the_family_off_in_their_own_home_or_where_it_cannot_be_read()
    {
        PersonsSettings();
        Assert.False(Written(Tools(OwnFamilies.Skills)).ContainsKey("model"));
        Assert.False(Written(Tools(OwnFamilies.Settings, sessionHome: ToolHome)).ContainsKey("model"));

        Put(Path.Combine(ToolHome, "settings.json"), "{ not json");
        var written = Written(Tools(OwnFamilies.Settings));
        Assert.Equal(["Read"], Strings(written["permissions"]!["allow"]));

        var off = PermissionRules.SwitchDefault(PermissionFile.Empty, OwnHomeDefaults.SettingsId, on: false);
        Assert.False(OwnHomeDefaults.Families(off, connectorOnly: false).HasFlag(OwnFamilies.Settings));
        Assert.True(OwnHomeDefaults.Families(PermissionFile.Empty, connectorOnly: true).HasFlag(OwnFamilies.Settings));
    }

    [Fact]
    public void The_persons_file_is_only_read()
    {
        PersonsSettings();
        var before = File.ReadAllText(Path.Combine(ToolHome, "settings.json"));

        Written(Tools(OwnFamilies.Settings));

        Assert.Equal(before, File.ReadAllText(Path.Combine(ToolHome, "settings.json")));
        Assert.Equal(["settings.json"], Directory.GetFileSystemEntries(ToolHome).Select(Path.GetFileName));
        using var document = JsonDocument.Parse(before);
        Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
    }
}
