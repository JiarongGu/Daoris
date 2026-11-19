using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Both of Claude Code's doors run a session with its system prompt and tools rendered from what its servers serve now at every request,
/// never the record of its first: the native door's flag ahead of <c>-p</c> on every recipe, so the layer's program
/// carries it too, and the protocol door's preset with <c>snapshot: false</c> on every open and resume. <c>ClaudeResumedToolsProcessTests</c>
/// measures it on the real program.
/// </summary>
public sealed class ClaudeFreshPromptTests
{
    [Fact]
    public void Every_native_recipe_turns_the_record_off_ahead_of_its_print_flag()
    {
        var spawn = new ClaudeCodeSpawn();
        var request = new SpawnRequest("Game", "D:/fam/Game", "http://localhost:5177") { Resume = "conversation-1" };

        foreach (var arguments in new[] { spawn.Start(request).Arguments, spawn.Resume(request)!.Arguments, spawn.Chat(request).Arguments })
        {
            var print = arguments.ToList().IndexOf("-p");
            Assert.Equal(ClaudeCodeSpawn.FreshPrompt, arguments.Take(print));
        }
    }

    [Fact]
    public void The_protocol_door_opens_and_resumes_on_the_preset_with_its_record_off()
    {
        var acp = ProviderRegistry.Built().Door("claude-code-acp");

        foreach (var meta in new[] { acp.AcpSessionMeta("rules.json"), acp.AcpSourcesAloneMeta("rules.json") })
        {
            var prompt = JsonSerializer.SerializeToElement(meta).GetProperty("claudeCode").GetProperty("options").GetProperty("systemPrompt");
            Assert.Equal("preset", prompt.GetProperty("type").GetString());
            Assert.Equal("claude_code", prompt.GetProperty("preset").GetString());
            Assert.False(prompt.GetProperty("snapshot").GetBoolean());
        }
    }
}
