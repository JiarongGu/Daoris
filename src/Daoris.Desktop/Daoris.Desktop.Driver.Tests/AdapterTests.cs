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
}
