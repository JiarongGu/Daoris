using System.Diagnostics;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The person's own Codex home (<c>CODEX_HOME</c>, else <c>.codex</c> in the profile) reaches a Codex session as the adapter's session
/// configuration (<c>CODEX_CONFIG</c>), layered over the account's own: its <c>config.toml</c> where the account's sets nothing, its
/// <c>AGENTS.md</c> and the folders of its skills and prompts in the developer instructions. The sign-in, the posture, the trust and the
/// servers are never taken from it; nothing where every family is off or the session runs in the person's own home.
/// </summary>
public sealed class CodexOwnHomeTests : IDisposable
{
    private readonly string _root = Path.Combine(TestHomes.Root, "daoris-own-codex-" + Guid.NewGuid().ToString("N")[..8]);

    private string Home => Path.Combine(_root, "daoris");
    private string Person => Path.Combine(_root, "person");
    private string Account => Path.Combine(_root, "account");
    private string CodexHome => Path.Combine(Person, ".codex");

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static void Put(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private OwnTools Tools(OwnFamilies families, string? sessionHome = null, string? ownVariable = null) => new(
        name => name == "CODEX_HOME" ? ownVariable : null, Person, name => name == "CODEX_HOME" ? sessionHome ?? Account : null, families);

    private JsonObject? Handed(OwnTools tools)
    {
        var info = new ProcessStartInfo();
        SpawnSkills.Hand(
            ProviderRegistry.Built(), ProviderRegistry.Built().Door("codex-acp"), info, Home, "s1", PluginCatalog.Load(Home), PluginServerTo.Sessions, tools);
        return info.Environment.TryGetValue("CODEX_CONFIG", out var config) && config is not null ? (JsonObject)JsonNode.Parse(config)! : null;
    }

    private void PersonsHome()
    {
        Put(Path.Combine(CodexHome, "config.toml"), """
            # the person's own
            model = "person-model"
            model_reasoning_effort = 'high'
            model_provider = "custom"
            approval_policy = "never"
            sandbox_mode = "danger-full-access"
            notify = ["notify-send", "codex"]
            profile = "quick"

            [features]
            web_search_request = true

            [profiles.quick]
            model = "quick-model"
            approval_policy = "never"

            [projects.'C:\work']
            trust_level = "trusted"

            [mcp_servers.docs]
            command = "docs-server"
            args = ["--stdio"]
            env = { TOKEN = "abc" }
            """);
        Put(Path.Combine(CodexHome, "AGENTS.md"), "Prefer small commits.");
        Put(Path.Combine(CodexHome, "skills", "review", "SKILL.md"), "---\nname: review\n---\n");
        Put(Path.Combine(CodexHome, "prompts", "ship.md"), "Ship it.");
    }

    [Fact]
    public void The_persons_configuration_rides_the_adapters_session_config_under_the_accounts_own_and_the_floor()
    {
        PersonsHome();
        Put(Path.Combine(Account, "config.toml"), "model_reasoning_effort = \"low\"\n");

        var config = Handed(Tools(OwnFamilies.Settings))!;

        Assert.Equal("person-model", config["model"]!.GetValue<string>());
        Assert.False(config.ContainsKey("model_reasoning_effort"));
        Assert.Equal(["notify-send", "codex"], config["notify"]!.AsArray().Select(item => item!.GetValue<string>()));
        Assert.True(config["features"]!["web_search_request"]!.GetValue<bool>());
        Assert.Equal("quick", config["profile"]!.GetValue<string>());
        Assert.Equal("quick-model", config["profiles"]!["quick"]!["model"]!.GetValue<string>());
        // The sign-in, the posture, the trust and the servers stay the start's.
        Assert.False(config["profiles"]!["quick"]!.AsObject().ContainsKey("approval_policy"));
        foreach (var key in new[] { "model_provider", "approval_policy", "sandbox_mode", "projects", "mcp_servers" })
        {
            Assert.False(config.ContainsKey(key), key);
        }

        Assert.False(config.ContainsKey("developer_instructions"));
    }

    [Fact]
    public void The_persons_instructions_and_the_folders_of_their_skills_and_prompts_ride_the_developer_instructions()
    {
        PersonsHome();

        var config = Handed(Tools(OwnFamilies.Memory | OwnFamilies.Skills))!;

        var told = config["developer_instructions"]!.GetValue<string>();
        Assert.StartsWith("Prefer small commits.", told, StringComparison.Ordinal);
        Assert.Contains(Path.Combine(CodexHome, "skills"), told, StringComparison.Ordinal);
        Assert.Contains(Path.Combine(CodexHome, "prompts"), told, StringComparison.Ordinal);
        Assert.False(config.ContainsKey("model"));

        Assert.DoesNotContain("Prefer small commits.", Handed(Tools(OwnFamilies.Skills))!["developer_instructions"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public void Nothing_is_handed_with_every_family_off_in_the_persons_own_home_or_from_a_file_that_cannot_be_read()
    {
        PersonsHome();
        Assert.Null(Handed(Tools(OwnFamilies.None)));
        Assert.Null(Handed(Tools(OwnFamilies.Settings | OwnFamilies.Memory, sessionHome: CodexHome)));

        var elsewhere = Path.Combine(_root, "elsewhere");
        Put(Path.Combine(elsewhere, "config.toml"), "model = \"elsewhere-model\"\n");
        Assert.Equal("elsewhere-model", Handed(Tools(OwnFamilies.Settings, ownVariable: elsewhere))!["model"]!.GetValue<string>());

        Put(Path.Combine(CodexHome, "config.toml"), "model = \"unterminated\n");
        Assert.Null(Handed(Tools(OwnFamilies.Settings)));
    }

    [Fact]
    public void The_toml_reader_reads_the_forms_a_codex_configuration_uses_and_refuses_one_it_cannot()
    {
        var read = TomlReader.Read("""
            a = 1
            b.c = "x\ty"
            "quoted key" = true
            list = [
              1, 2, # a comment
              3,
            ]
            text = '''
            two
            lines'''
            inline = { k = "v", n = { m = -2.5 } }

            [[servers]]
            name = "one"
            [[servers]]
            name = "two"
            """);

        Assert.Equal(1, read["a"]!.GetValue<long>());
        Assert.Equal("x\ty", read["b"]!["c"]!.GetValue<string>());
        Assert.True(read["quoted key"]!.GetValue<bool>());
        Assert.Equal([1L, 2L, 3L], read["list"]!.AsArray().Select(item => item!.GetValue<long>()));
        Assert.Equal("two\nlines", read["text"]!.GetValue<string>().ReplaceLineEndings("\n"));
        Assert.Equal(-2.5, read["inline"]!["n"]!["m"]!.GetValue<double>());
        Assert.Equal(["one", "two"], read["servers"]!.AsArray().Select(item => item!["name"]!.GetValue<string>()));

        Assert.Throws<FormatException>(() => TomlReader.Read("a = 1\na = 2\n"));
        Assert.Throws<FormatException>(() => TomlReader.Read("when = 1979-05-27\n"));
    }
}
