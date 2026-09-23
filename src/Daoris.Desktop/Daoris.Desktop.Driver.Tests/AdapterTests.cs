using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// D23, one layer up: one adapter is supported, others are explicit, and an unknown one is an error
/// NAMING WHAT EXISTS — never a silent fallback. An adapter names a harness, never a model (D24).
/// </summary>
public sealed class AdapterTests
{
    private static SessionTarget Target() => new(
        QuestId: "abc123",
        Title: "Fix the flaky gate",
        Body: "It fails one run in five; the log is attached.",
        Asker: "Platform",
        Repository: "Game",
        Root: "D:/fam/Game",
        ServiceUrl: "http://localhost:5177");

    [Fact]
    public void An_unknown_adapter_errors_naming_what_exists()
    {
        var error = Assert.Throws<DriverException>(() => AdapterSet.Built().Resolve("codex"));

        Assert.Contains("codex", error.Message);
        Assert.Contains("stub", error.Message);
    }

    [Fact]
    public void The_stub_adapter_is_built_in()
    {
        Assert.Equal("stub", AdapterSet.Built().Resolve("stub").Name);
    }

    /// <summary>
    /// The protocol door is opt-in per adapter (D53). Silence means the pipe, so every adapter that
    /// existed before the door behaves exactly as it did — the same silence-preserves rule the
    /// toolchain and the session trees follow.
    /// </summary>
    [Fact]
    public void An_adapter_that_says_nothing_is_held_over_the_pipe()
    {
        Assert.Equal(SessionWire.Pipe, AdapterSet.Built().Resolve("stub").Wire);
        Assert.Equal(SessionWire.Pipe, AdapterSet.Built().Resolve("claude-code").Wire);
    }

    /// <summary>
    /// The protocol stub declares the ACP door AND opens stdin — the difference that matters, since
    /// the driver writes frames into it for as long as the session lives. A pipe-door session is
    /// handed its target once and has nobody to take turns with.
    /// </summary>
    [Fact]
    public void The_acp_stub_declares_the_protocol_door_and_opens_stdin()
    {
        var adapter = AdapterSet.Built().Resolve("acp-stub");
        var info = adapter.Prepare(Target(), ["node", "acp-agent.mjs"]);

        Assert.Equal(SessionWire.Acp, adapter.Wire);
        Assert.True(info.RedirectStandardInput);
        Assert.True(info.RedirectStandardOutput);
        Assert.Equal("D:/fam/Game", info.WorkingDirectory);
        // The target still rides the environment, so the same composition serves both doors.
        Assert.Equal("abc123", info.Environment["DAORIS_QUEST_ID"]);
    }

    /// <summary>The protocol stub needs its command named, for the same reason the pipe stub does.</summary>
    [Fact]
    public void The_acp_stub_needs_a_command_and_says_how_to_give_one()
    {
        var error = Assert.Throws<DriverException>(
            () => AdapterSet.Built().Resolve("acp-stub").Prepare(Target(), command: null));

        Assert.Contains("acp-stub", error.Message);
        Assert.Contains("driver.json", error.Message);
    }

    /// <summary>The stub runs whatever command the config names — a test double with real mechanics.</summary>
    [Fact]
    public void The_stub_needs_a_command_and_says_how_to_give_one()
    {
        var error = Assert.Throws<DriverException>(
            () => AdapterSet.Built().Resolve("stub").Prepare(Target(), command: null));

        Assert.Contains("command", error.Message);
    }

    /// <summary>
    /// The session runs IN the repository — cwd is the root — and the target arrives in the
    /// environment, so any script shape can read it without argument parsing.
    /// </summary>
    [Fact]
    public void The_stub_spawns_in_the_root_with_the_target_in_the_environment()
    {
        var info = AdapterSet.Built().Resolve("stub").Prepare(Target(), ["node", "agent.mjs"]);

        Assert.Equal("node", info.FileName);
        Assert.Equal("agent.mjs", Assert.Single(info.ArgumentList));
        Assert.Equal("D:/fam/Game", info.WorkingDirectory);
        Assert.Equal("abc123", info.Environment["DAORIS_QUEST_ID"]);
        Assert.Equal("Game", info.Environment["DAORIS_REPOSITORY"]);
        Assert.Equal("http://localhost:5177", info.Environment["DAORIS_SERVICE_URL"]);
        Assert.True(info.RedirectStandardOutput);
        Assert.True(info.RedirectStandardError);
    }

    /// <summary>`claude-code` is the supported harness (D23, one layer up) and ships in the set.</summary>
    [Fact]
    public void The_claude_code_adapter_is_built_in()
    {
        Assert.Equal("claude-code", AdapterSet.Built().Resolve("claude-code").Name);
    }

    /// <summary>
    /// The session is the harness's non-interactive mode, in the repository root, with the composed
    /// target as its prompt. Edits auto-accept — reversible, in-repository, D37's middle — and
    /// everything else stays under the repository's own checked-in permission configuration: the
    /// adapter grants nothing an interactive session there would not have.
    /// </summary>
    [Fact]
    public void Claude_code_spawns_headless_with_the_target_as_the_prompt()
    {
        var info = AdapterSet.Built().Resolve("claude-code").Prepare(Target(), command: null);

        Assert.Equal("claude", info.FileName);
        Assert.Equal("D:/fam/Game", info.WorkingDirectory);
        Assert.Contains("-p", info.ArgumentList);
        Assert.Contains(info.ArgumentList, a => a.Contains("#abc123"));
        var mode = info.ArgumentList.IndexOf("--permission-mode");
        Assert.True(mode >= 0 && info.ArgumentList[mode + 1] == "acceptEdits");
        Assert.Equal("abc123", info.Environment["DAORIS_QUEST_ID"]);
    }

    /// <summary>A machine whose shim needs a path names it in the config, like any other command.</summary>
    [Fact]
    public void Claude_code_takes_its_command_from_the_config_when_one_is_named()
    {
        var info = AdapterSet.Built().Resolve("claude-code").Prepare(Target(), ["C:/tools/claude.exe"]);

        Assert.Equal("C:/tools/claude.exe", info.FileName);
        Assert.Contains("-p", info.ArgumentList);
    }

    /// <summary>An adapter names a harness, never a model (D24) — which model answers is the
    /// repository's own harness configuration, and the driver must not reach into it.</summary>
    [Fact]
    public void Claude_code_never_names_a_model()
    {
        var info = AdapterSet.Built().Resolve("claude-code").Prepare(Target(), command: null);

        Assert.DoesNotContain("--model", info.ArgumentList);
    }

    /// <summary>
    /// The composed target is the claiming instruction of D46 §3: take, work under the repository's
    /// own doctrine, close with done or a reasoned decline, stand down if someone already has it —
    /// and never write outside the repository. The wording is project-agnostic: it travels to
    /// repositories that know nothing of this one's decision numbering.
    /// </summary>
    [Fact]
    public void The_target_prompt_carries_the_claim_the_close_and_the_boundary()
    {
        var prompt = TargetPrompt.Compose(Target());

        Assert.Contains("#abc123", prompt);
        Assert.Contains("Fix the flaky gate", prompt);
        Assert.Contains("one run in five", prompt);
        Assert.Contains("take", prompt);
        Assert.Contains("decline", prompt);
        Assert.Contains("stand down", prompt);
        Assert.Contains("Never write outside", prompt);
        Assert.DoesNotContain("D32", prompt);
        Assert.DoesNotContain("D46", prompt);
    }

    // ——— What a quest carries (D65 §2): its links, and its files — handed by name and by directory.

    private const string Kept = "D:/home/quests/abc123/attachments";

    private static SessionTarget Carrying() => Target() with
    {
        Links = ["https://tickets.example/T-1"],
        Attachments =
        [
            new QuestFileView("before.png", "ab12", 2048, $"{Kept}/ab12-before.png"),
            new QuestFileView("trace.log", "cd34", 300, Path: null),
        ],
    };

    /// <summary>
    /// The session is TOLD what the asker gave — each link, each file with where it lies — and a file
    /// named on the record but not on this machine is said to be elsewhere rather than listed as if a
    /// path would open it. Nothing is claimed that the session would find untrue.
    /// </summary>
    [Fact]
    public void The_target_prompt_names_the_links_and_the_files_and_says_which_are_not_here()
    {
        var prompt = TargetPrompt.Compose(Carrying());

        Assert.Contains("https://tickets.example/T-1", prompt);
        Assert.Contains($"{Kept}/ab12-before.png", prompt);
        Assert.Contains("DAORIS_QUEST_ATTACHMENTS", prompt);
        Assert.Contains("trace.log", prompt);
        Assert.Contains("not on this machine", prompt);
        Assert.DoesNotContain("D65", prompt);
    }

    /// <summary>
    /// A chain's step is told what it follows, and a quest with steps after it is told what its close
    /// will publish (D65 §4) — so a developing session knows a verifier comes next, and a verifying one
    /// knows which quest's work it is checking.
    /// </summary>
    [Fact]
    public void The_target_prompt_says_what_a_step_follows_and_what_closing_it_publishes()
    {
        var prompt = TargetPrompt.Compose(Target() with
        {
            Parent = "a1b2c3",
            Then = [new QuestStepView("Checker", "Report on #abc123", "Say what was done.")],
        });

        Assert.Contains("#a1b2c3", prompt);
        Assert.Contains("`Checker`", prompt);
        Assert.Contains("Report on #abc123", prompt);
    }

    [Fact]
    public void A_quest_that_carries_nothing_says_nothing_about_carrying()
    {
        var prompt = TargetPrompt.Compose(Target());

        Assert.DoesNotContain("DAORIS_QUEST_ATTACHMENTS", prompt);
        Assert.DoesNotContain("Links", prompt);
        Assert.DoesNotContain("follows", prompt);
        Assert.DoesNotContain("next step", prompt);
    }

    /// <summary>
    /// The directory rides the environment when a file is here — and is ABSENT, not empty, when none
    /// is: a blank directory would read to a session as one that was emptied.
    /// </summary>
    [Fact]
    public void The_attachments_directory_is_in_the_environment_only_when_a_file_is_here()
    {
        var stub = AdapterSet.Built().Resolve("stub");

        var carrying = stub.Prepare(Carrying(), ["node", "agent.mjs"]);
        var elsewhere = stub.Prepare(
            Target() with { Attachments = [new QuestFileView("trace.log", "cd34", 300, Path: null)] },
            ["node", "agent.mjs"]);

        Assert.Equal(Path.GetDirectoryName($"{Kept}/ab12-before.png"), carrying.Environment["DAORIS_QUEST_ATTACHMENTS"]);
        Assert.False(elsewhere.Environment.ContainsKey("DAORIS_QUEST_ATTACHMENTS"));
        Assert.False(stub.Prepare(Target(), ["node", "agent.mjs"]).Environment.ContainsKey("DAORIS_QUEST_ATTACHMENTS"));
    }

    // ——— The interactive capability (D49 §3). Declared honestly and opted into: an adapter that has
    // not been wired for a conversation says so, and asking errors naming what the harness is.

    private static ChatTarget Chat() => new("Game", "D:/fam/Game", "http://localhost:5177");

    [Fact]
    public void The_built_adapters_declare_whether_they_can_hold_a_conversation()
    {
        Assert.True(AdapterSet.Built().Resolve("stub").Interactive);
        Assert.True(AdapterSet.Built().Resolve("claude-code").Interactive);
    }

    /// <summary>
    /// A chat's process has stdin open — that IS the seam: the person's messages go in, the harness's
    /// answers come out, and Daoris pipes text without ever being the conversation.
    /// </summary>
    [Fact]
    public void A_chat_is_spawned_with_the_person_s_channel_open()
    {
        var info = AdapterSet.Built().Resolve("claude-code").PrepareChat(Chat(), null);

        Assert.True(info.RedirectStandardInput);
        Assert.True(info.RedirectStandardOutput);
        Assert.Equal("D:/fam/Game", info.WorkingDirectory);
        Assert.Equal("Game", info.Environment["DAORIS_REPOSITORY"]);
        Assert.Equal("http://localhost:5177", info.Environment["DAORIS_SERVICE_URL"]);
    }

    /// <summary>
    /// No quest variables at all, rather than empty ones: a conversation serves no quest, and a blank
    /// id would read to a session as one it failed to parse.
    /// </summary>
    [Fact]
    public void A_chat_carries_no_quest_and_no_target_prompt()
    {
        var info = AdapterSet.Built().Resolve("claude-code").PrepareChat(Chat(), null);

        Assert.False(info.Environment.ContainsKey("DAORIS_QUEST_ID"));
        Assert.False(info.Environment.ContainsKey("DAORIS_TARGET"));
        // And no one-shot prompt: the person supplies the first message.
        Assert.DoesNotContain("-p", info.ArgumentList);
    }

    /// <summary>The permission posture does not soften because a person is watching (D46 §5).</summary>
    [Fact]
    public void A_chat_runs_under_the_same_permission_posture_as_driven_work()
    {
        var info = AdapterSet.Built().Resolve("claude-code").PrepareChat(Chat(), null);

        Assert.Contains("--permission-mode", info.ArgumentList);
        Assert.Contains("acceptEdits", info.ArgumentList);
        Assert.DoesNotContain("--dangerously-skip-permissions", info.ArgumentList);
    }

    /// <summary>
    /// An adapter that has not been wired for turn-taking refuses in the harness's own terms, rather
    /// than spawning something that will never answer — the same shape as an unknown adapter name.
    /// </summary>
    [Fact]
    public void A_non_interactive_adapter_says_so_rather_than_spawning_a_silent_process()
    {
        // Through the interface, which is where the default lives — and where every caller reaches it.
        ISessionAdapter adapter = new OneShotAdapter();

        Assert.False(adapter.Interactive);
        var error = Assert.Throws<DriverException>(() => adapter.PrepareChat(Chat(), null));
        Assert.Contains("one-shot", error.Message);
        Assert.Contains("conversation", error.Message);
    }

    /// <summary>A harness that takes its target once and runs to completion — the default shape.</summary>
    private sealed class OneShotAdapter : ISessionAdapter
    {
        public string Name => "one-shot";

        public System.Diagnostics.ProcessStartInfo Prepare(
            SessionTarget target, IReadOnlyList<string>? command) => new();
    }
}
