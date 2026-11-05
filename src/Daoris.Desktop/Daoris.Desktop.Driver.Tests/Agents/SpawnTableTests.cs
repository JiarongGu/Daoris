using System.Diagnostics;
using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AGENTPROVIDER1 row h (the agent-provider design §4.6, §11): each built-in door's spawn, a driven start's, a conversation's and a native
/// resume's, as the transports' one shell completes the door's recipe: the program, the argument vector, the input and Daoris's variables
/// equal to the table, which is what each door's adapter class spawned before its recipe moved into its provider's folder. And s-v: which
/// doors take Claude Code's seam.
/// </summary>
public sealed class SpawnTableTests
{
    private static SessionTarget Target() => new(
        QuestId: "abc123", Title: "Fix the flaky gate", Body: "It fails one run in five.", Asker: "Platform",
        Repository: "Game", Root: "D:/fam/Game", ServiceUrl: "http://localhost:5177");

    private static ChatTarget Chat() => new("Game", "D:/fam/Game", "http://localhost:5177");

    private static readonly string[] Stream = ["--output-format", "stream-json", "--verbose", "--include-partial-messages"];

    /// <summary>Door, configured command, program, arguments, stdin open, and the recipe's own variable (name=value).</summary>
    public static TheoryData<string, string[]?, string, string[], bool, string?> Starts => new()
    {
        { "claude-code", null, "claude", ["-p", "--permission-mode", "acceptEdits", .. Stream], true, null },
        { "claude-code-acp", null, "claude-agent-acp", [], true, null },
        { "codex-acp", null, "codex-acp", [], true, null },
        { "dsh", null, "dsh", ["--profile", "acp"], true, "DSH_PERMISSION_MODE=workspace-write" },
        { "stub", ["node", "agent.mjs"], "node", ["agent.mjs"], false, null },
        { "acp-stub", ["node", "acp-agent.mjs"], "node", ["acp-agent.mjs"], true, null },
    };

    /// <summary>Door, configured command, program and arguments of a conversation.</summary>
    public static TheoryData<string, string[]?, string, string[]> Chats => new()
    {
        { "claude-code", null, "claude", ["-p", "--input-format", "stream-json", "--permission-mode", "acceptEdits", .. Stream] },
        { "claude-code-acp", null, "claude-agent-acp", [] },
        { "codex-acp", null, "codex-acp", [] },
        { "stub", ["node", "agent.mjs"], "node", ["agent.mjs"] },
        { "acp-stub", ["node", "acp-agent.mjs"], "node", ["acp-agent.mjs"] },
    };

    [Theory]
    [MemberData(nameof(Starts))]
    public void A_driven_start_is_its_door_s_recipe_completed_by_the_shell(
        string door, string[]? command, string program, string[] arguments, bool input, string? own)
    {
        var info = AdapterSet.Built().Resolve(door).Prepare(Target(), command);

        Assert.Equal(program, info.FileName);
        Assert.Equal(arguments, info.ArgumentList);
        Assert.Equal(input, info.RedirectStandardInput);
        Shelled(info);
        Assert.Equal("abc123", info.Environment["DAORIS_QUEST_ID"]);
        Assert.Equal("Platform", info.Environment["DAORIS_QUEST_ASKER"]);
        Assert.False(string.IsNullOrEmpty(info.Environment["DAORIS_TARGET"]));
        if (own?.Split('=') is [var name, var value]) Assert.Equal(value, info.Environment[name]);
        // The native door's prompt goes on stdin as BOM-less UTF-8; no other door writes words there.
        if (door == "claude-code") Assert.Empty(info.StandardInputEncoding!.GetPreamble());
    }

    [Theory]
    [MemberData(nameof(Chats))]
    public void A_conversation_is_its_door_s_recipe_with_stdin_open_and_no_target(string door, string[]? command, string program, string[] arguments)
    {
        var info = AdapterSet.Built().Resolve(door).PrepareChat(Chat(), command);

        Assert.Equal(program, info.FileName);
        Assert.Equal(arguments, info.ArgumentList);
        Assert.True(info.RedirectStandardInput);
        Shelled(info);
        Assert.False(info.Environment.ContainsKey("DAORIS_TARGET"));
        Assert.False(info.Environment.ContainsKey("DAORIS_QUEST_ID"));
    }

    [Fact]
    public void The_native_door_resumes_its_kept_conversation_and_the_protocol_doors_know_none()
    {
        var info = AdapterSet.Built().Resolve("claude-code").PrepareResume(Target(), null, "conversation-1")!;

        Assert.Equal("claude", info.FileName);
        Assert.Equal(["-p", "--resume", "conversation-1", "--permission-mode", "acceptEdits", .. Stream], info.ArgumentList);
        Assert.True(info.RedirectStandardInput);
        Shelled(info);
        Assert.Null(AdapterSet.Built().Resolve("codex-acp").PrepareResume(Target(), null, "conversation-1"));
    }

    [Fact]
    public void Dsh_holds_no_conversation_as_before()
    {
        var refused = Assert.Throws<DriverException>(() => AdapterSet.Built().Resolve("dsh").PrepareChat(Chat(), null));
        Assert.Contains("cannot hold a conversation", refused.Message);
    }

    [Fact]
    public void A_door_s_recipe_names_none_of_Daoris_s_variables_and_the_carriers_are_its_own()
    {
        foreach (var name in AdapterSet.Built().Names)
        {
            var door = AdapterSet.Built().Registry.Door(name);
            var command = name.EndsWith("stub", StringComparison.Ordinal) ? new[] { "node", "agent.mjs" } : null;
            var recipe = door.Spawn.Required.Start(Target(), command);
            Assert.DoesNotContain(recipe.Variables.Keys, Spawning.Daoris);
        }

        var pipe = AdapterSet.Built().Resolve("claude-code");
        var info = pipe.Prepare(Target(), null);
        pipe.HandServers(info, "servers.json");
        pipe.HandSettings(info, "settings.json");
        pipe.HandPlugins(info, ["one", "two"]);
        Assert.Equal(
            ["--mcp-config", "servers.json", "--settings", "settings.json", "--plugin-dir", "one", "--plugin-dir", "two"],
            info.ArgumentList.Skip(3 + Stream.Length));
        Assert.Equal("false", pipe.ToolsUpFront["ENABLE_TOOL_SEARCH"]);
        Assert.Equal("false", AdapterSet.Built().Resolve("claude-code-acp").ToolsUpFront["ENABLE_TOOL_SEARCH"]);
        Assert.Empty(AdapterSet.Built().Resolve("codex-acp").ToolsUpFront);
    }

    /// <summary>
    /// s-v (the design §7 row 7): Claude Code's seam goes on a door whose program runs Claude Code's, and on a plugin's protocol door, whose
    /// program is unmeasured; Codex's and dsh's doors take neither variable.
    /// </summary>
    [Theory]
    [InlineData("claude-code", false)]
    [InlineData("claude-code-acp", true)]
    [InlineData("codex-acp", false)]
    [InlineData("dsh", false)]
    [InlineData("stub", false)]
    [InlineData("acp-stub", true)]
    public void Claude_Code_s_seam_is_taken_where_the_door_runs_claude(string door, bool seam)
    {
        Assert.Equal(seam, AdapterSet.Built().Resolve(door).TakesClaudeSeam());
        Assert.True(new UnmeasuredProtocolDoor().TakesClaudeSeam());
    }

    /// <summary>The seam on the process: both variables where a managed <c>claude</c> is handed, neither where none is.</summary>
    [Fact]
    public void The_seam_sets_both_variables_or_neither()
    {
        var acp = AdapterSet.Built().Resolve("claude-code-acp");
        var pointed = acp.Prepare(Target(), null);
        Shell.Equip(pointed, acp.Toolchain!, profileHome: null, claudeExecutable: "C:/toolchain/claude-code/2.1.300/claude.exe");
        Assert.Equal("C:/toolchain/claude-code/2.1.300/claude.exe", pointed.Environment[ClaudeAcp.ExecutableVariable]);
        Assert.Equal("1", pointed.Environment["DISABLE_UPDATES"]);

        var codex = AdapterSet.Built().Resolve("codex-acp");
        var plain = codex.Prepare(Target(), null);
        plain.Environment.Remove(ClaudeAcp.ExecutableVariable);
        Shell.Equip(plain, codex.Toolchain!, profileHome: null, claudeExecutable: null);
        Assert.False(plain.Environment.ContainsKey(ClaudeAcp.ExecutableVariable));
    }

    /// <summary>
    /// A carrier path holding <c>&amp;</c> on a <c>.cmd</c> shim starts as before (O3-F4): the carriers are appended after the program is
    /// resolved, so the shim's refusal reads only the arguments the recipe built.
    /// </summary>
    [Fact]
    public void A_carrier_holding_an_ampersand_on_a_cmd_shim_is_appended_after_the_program_is_resolved()
    {
        if (!OperatingSystem.IsWindows()) return;
        var folder = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "spawn-table", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(folder);
        try
        {
            var shim = Path.Combine(folder, "claude.cmd");
            File.WriteAllText(shim, "@echo off\r\n");
            var pipe = AdapterSet.Built().Resolve("claude-code");
            var info = pipe.Prepare(Target(), [shim]);
            Shell.Equip(info, pipe.Toolchain!, profileHome: null);
            pipe.HandServers(info, @"C:\one&two\servers.json");

            Assert.Equal(shim, info.FileName);
            Assert.Equal(["--mcp-config", @"C:\one&two\servers.json"], info.ArgumentList.TakeLast(2));
        }
        finally { GitSeed.Discard(folder); }
    }

    /// <summary>What every completed process carries, whatever its door: a window-less, UTF-8 child in the root, with Daoris's git.</summary>
    private static void Shelled(ProcessStartInfo info)
    {
        Assert.Equal("D:/fam/Game", info.WorkingDirectory);
        Assert.True(info.CreateNoWindow);
        Assert.False(info.UseShellExecute);
        Assert.Equal(Encoding.UTF8, info.StandardOutputEncoding);
        Assert.Equal(Encoding.UTF8, info.StandardErrorEncoding);
        Assert.Equal("Game", info.Environment["DAORIS_REPOSITORY"]);
        Assert.Equal("http://localhost:5177", info.Environment["DAORIS_SERVICE_URL"]);
        // The session's git last of Daoris's: its long paths the last entry of the command scope, after any it inherited.
        var count = int.Parse(info.Environment["GIT_CONFIG_COUNT"]!, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal("core.longpaths", info.Environment[$"GIT_CONFIG_KEY_{count - 1}"]);
        Assert.True(info.Environment.ContainsKey("GH_CONFIG_DIR"));
    }

    private sealed class UnmeasuredProtocolDoor : ISessionAdapter
    {
        public string Name => "plugin-door";

        public SessionWire Wire => SessionWire.Acp;

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) => throw new NotSupportedException();
    }
}
