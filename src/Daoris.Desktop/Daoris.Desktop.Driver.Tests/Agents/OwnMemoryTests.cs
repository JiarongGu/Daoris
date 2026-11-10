using System.Diagnostics;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The person's own memory file (<c>CLAUDE.md</c> in their tool home) reaches a Claude session appended to its system prompt, on the
/// native door as the file named on its command line and on the protocol door as the SDK's preset with the text appended; the
/// protocol door takes the Chrome switch as the SDK's extra argument. Nothing where the family is off or the session runs in the
/// person's own home, which reads it by itself.
/// </summary>
public sealed class OwnMemoryTests : IDisposable
{
    private readonly string _root = Path.Combine(TestHomes.Root, "daoris-own-memory-" + Guid.NewGuid().ToString("N")[..8]);

    private string Home => Path.Combine(_root, "daoris");
    private string Person => Path.Combine(_root, "person");
    private string ToolHome => Path.Combine(Person, ".claude");
    private string Memory => Path.Combine(ToolHome, "CLAUDE.md");

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static void Put(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private OwnTools Tools(OwnFamilies families, string? sessionHome = null) => new(
        _ => null, Person, name => name == "CLAUDE_CONFIG_DIR" ? sessionHome ?? Path.Combine(_root, "account") : null, families);

    private ProcessStartInfo Native(OwnTools tools, string door = "claude-code")
    {
        var info = new ProcessStartInfo();
        SpawnSkills.Hand(
            ProviderRegistry.Built(), ProviderRegistry.Built().Door(door), info, Home, "s1", PluginCatalog.Load(Home), PluginServerTo.Sessions, tools);
        return info;
    }

    [Fact]
    public void The_native_door_names_the_persons_memory_file_to_append_to_its_system_prompt()
    {
        Put(Memory, "Answer in British English.");

        Assert.Equal(["--append-system-prompt-file", Memory], Native(Tools(OwnFamilies.Memory)).ArgumentList);
        Assert.Empty(Native(Tools(OwnFamilies.Skills)).ArgumentList);
        Assert.Empty(Native(Tools(OwnFamilies.Memory, sessionHome: ToolHome)).ArgumentList);
        Assert.Empty(Native(Tools(OwnFamilies.Memory), "claude-code-acp").ArgumentList);

        var off = PermissionRules.SwitchDefault(PermissionFile.Empty, OwnHomeDefaults.MemoryId, on: false);
        Assert.False(OwnHomeDefaults.Families(off, connectorOnly: false).HasFlag(OwnFamilies.Memory));
    }

    [Fact]
    public void The_protocol_door_carries_the_memory_text_on_the_preset_and_the_chrome_switch_as_an_extra_argument()
    {
        Put(Memory, "Answer in British English.");
        Put(Path.Combine(Person, ".claude.json"), """{ "claudeInChromeDefaultEnabled": true }""");
        var acp = ProviderRegistry.Built().Door("claude-code-acp");

        using var meta = JsonDocument.Parse(JsonSerializer.Serialize(acp.AcpSessionMeta("rules.json", [], Tools(OwnFamilies.Memory | OwnFamilies.Chrome))));
        var options = meta.RootElement.GetProperty("claudeCode").GetProperty("options");
        Assert.Equal("rules.json", options.GetProperty("settings").GetString());
        var prompt = options.GetProperty("systemPrompt");
        Assert.Equal("preset", prompt.GetProperty("type").GetString());
        Assert.Equal("claude_code", prompt.GetProperty("preset").GetString());
        Assert.Equal("Answer in British English.", prompt.GetProperty("append").GetString());
        Assert.Equal(JsonValueKind.Null, options.GetProperty("extraArgs").GetProperty("chrome").ValueKind);

        using var alone = JsonDocument.Parse(JsonSerializer.Serialize(acp.AcpSourcesAloneMeta("rules.json", [], Tools(OwnFamilies.Memory))));
        Assert.Equal("claude_code", alone.RootElement.GetProperty("claudeCode").GetProperty("options").GetProperty("systemPrompt").GetProperty("preset").GetString());

        using var none = JsonDocument.Parse(JsonSerializer.Serialize(acp.AcpSessionMeta("rules.json", [], Tools(OwnFamilies.Skills))));
        var plain = none.RootElement.GetProperty("claudeCode").GetProperty("options");
        Assert.False(plain.TryGetProperty("systemPrompt", out _));
        Assert.False(plain.TryGetProperty("extraArgs", out _));
        Assert.Equal(
            JsonSerializer.Serialize(acp.AcpSessionMeta("rules.json", [])),
            JsonSerializer.Serialize(acp.AcpSessionMeta("rules.json", [], Tools(OwnFamilies.Memory | OwnFamilies.Chrome, sessionHome: ToolHome))));
    }
}
