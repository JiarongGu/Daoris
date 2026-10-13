using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// `claude-code` over the protocol door (ACP2/D53) — the adapter, its two seams, and what a spawn
/// carries. <b>Everything here is keyless</b>: what is left for a real login is one driven turn.
/// </summary>
/// <remarks>
/// <para>The evaluation's §1a established both seams against the real adapter with no model spent:
/// <c>CLAUDE_CODE_EXECUTABLE</c> points the Agent SDK at a <c>claude</c> of our choosing, and
/// <c>CLAUDE_CONFIG_DIR</c> isolates the account — an empty one received its own `.claude.json` and
/// the machine's real profile was untouched. These tests hold that wiring from this side.</para>
/// </remarks>
public sealed class ClaudeAcpTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-acp2-tests", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private static SessionTarget Target(string root) => new(
        "abc123", "Expose a streaming budget", "World streaming needs a per-frame cap.",
        "game", "engine", root, "http://localhost:5177");

    private static ISessionAdapter Adapter() => AdapterSet.Built().Resolve("claude-code-acp");

    [Fact]
    public void It_is_a_protocol_door_session_and_takes_stdin()
    {
        var adapter = Adapter();
        Assert.Equal(SessionWire.Acp, adapter.Wire);

        var info = adapter.Prepare(Target(_home), command: null);
        // The driver writes JSON-RPC frames into it, unlike a pipe-door session which is given its
        // whole target at once and has nobody to take turns with.
        Assert.True(info.RedirectStandardInput);
    }

    /// <summary>
    /// 🔴 <b>No prompt on the command line.</b> A protocol-door session receives its target as
    /// `session/prompt`, so an adapter that also passed `-p` would send the work twice — and the
    /// permission posture is a MODE on this wire, so `--permission-mode` does not belong here either.
    /// </summary>
    [Fact]
    public void The_target_and_the_posture_are_not_arguments_on_this_door()
    {
        var info = Adapter().Prepare(Target(_home), command: null);
        var arguments = string.Join(' ', info.ArgumentList);

        Assert.DoesNotContain("-p", info.ArgumentList);
        Assert.DoesNotContain("--permission-mode", arguments);
        Assert.DoesNotContain("streaming budget", arguments);
    }

    /// <summary>
    /// Seam one (§1a): the adapter runs <b>the `claude` the toolchain manages</b> rather than
    /// carrying a second copy — which is the whole reason probe 3's failure was the useful part.
    /// </summary>
    [Fact]
    public void The_managed_claude_is_handed_to_the_agent_sdk_through_its_own_seam()
    {
        var info = Adapter().Prepare(Target(_home), command: null);
        ClaudeAcp.PointAtClaude(info, "C:/somewhere/.daoris/toolchain/claude-code/2.1.278/claude.cmd");

        Assert.Equal(
            "C:/somewhere/.daoris/toolchain/claude-code/2.1.278/claude.cmd",
            info.Environment["CLAUDE_CODE_EXECUTABLE"]);
    }

    /// <summary>
    /// 🔴 Nothing pinned means the seam is <b>not set at all</b> — the SDK then finds `claude` the way
    /// it always did. Setting it to an empty string, or to a path that is not there, would break a
    /// machine that works today, which is the additive rule TOOL2 holds everywhere else (D48 §2a).
    /// </summary>
    [Fact]
    public void With_nothing_managed_the_seam_is_left_untouched()
    {
        var info = Adapter().Prepare(Target(_home), command: null);
        ClaudeAcp.PointAtClaude(info, null);

        Assert.False(info.Environment.ContainsKey("CLAUDE_CODE_EXECUTABLE"));
    }

    /// <summary>
    /// Seam two: the account. It is the SAME variable the pipe door uses, applied by the same one
    /// line in the driver — so a session over either door runs as the profile the toolchain chose,
    /// and neither door can forget it.
    /// </summary>
    [Fact]
    public void The_account_seam_is_the_one_the_pipe_door_already_uses()
    {
        var toolchain = Adapter().Toolchain;

        Assert.Equal("CLAUDE_CONFIG_DIR", toolchain?.ProfileVariable);
    }

    /// <summary>
    /// It is a managed toolchain entry of its own (ACP2): the adapter is an npm package, pinned
    /// exact, that Daoris can install into a directory it owns — which is TOOL2's machinery reused
    /// rather than a second mechanism.
    /// </summary>
    [Fact]
    public void The_adapter_is_a_package_the_toolchain_can_pin()
    {
        var toolchain = Adapter().Toolchain;

        Assert.Equal("@agentclientprotocol/claude-agent-acp", toolchain?.Package);
        Assert.NotNull(toolchain?.Install);
        // Its version question is its own, asked of the binary that will actually run.
        Assert.NotEmpty(toolchain!.VersionArguments);
    }

    /// <summary>
    /// The two doors are <b>separate harnesses to the toolchain</b> and must not share a pin: the
    /// ACP adapter and `claude` itself are different packages with different versions, and one pin
    /// for both would install the wrong thing under a name somebody trusted.
    /// </summary>
    [Fact]
    public void The_protocol_door_and_the_pipe_door_are_pinned_separately()
    {
        var acp = Adapter().Toolchain;
        var pipe = AdapterSet.Built().Resolve("claude-code").Toolchain;

        // Since AGT2b they do not even share a SOURCE: `claude` pins from Anthropic's release bucket,
        // verified, and the adapter — which ships only on npm — from its package.
        Assert.Equal(ClaudeReleases.Channel, pipe?.Channel);
        Assert.Null(pipe?.Package);
        Assert.Null(acp?.Channel);
        Assert.NotNull(acp?.Package);
        Assert.NotEqual(acp?.Binary[0], pipe?.Binary[0]);
        // The binary is the PACKAGE's name, not the adapter's — verified against the installed one.
        Assert.Equal("claude-agent-acp", acp?.Binary[0]);
    }

    /// <summary>
    /// 🔴 <b>Both seams reach a real spawn, through the one line that governs both doors.</b> This is
    /// the keyless half of ACP2's proof: everything the real run will depend on, asserted where the
    /// driver actually applies it rather than where an adapter happens to build it.
    /// </summary>
    [Fact]
    public void Both_seams_reach_the_spawn_through_the_driver_s_one_line()
    {
        var adapter = AdapterSet.Built().Resolve("claude-code-acp");
        var info = adapter.Prepare(Target(_home), null);

        // Exactly what the driver does at spawn, with what the roster resolved.
        HarnessProbe.Apply(
            info, adapter.Toolchain!,
            profileHome: Path.Combine(_home, "profile"),
            binary: null,
            claudeExecutable: "C:/somewhere/.daoris/toolchain/claude-code/2.1.278/claude.cmd");

        Assert.Equal(
            "C:/somewhere/.daoris/toolchain/claude-code/2.1.278/claude.cmd",
            info.Environment[ClaudeAcp.ExecutableVariable]);
        Assert.Equal(Path.Combine(_home, "profile"), info.Environment["CLAUDE_CONFIG_DIR"]);
        // The directory is created as part of choosing it — at least one harness refuses to start
        // when its home variable names a path that is not there.
        Assert.True(Directory.Exists(Path.Combine(_home, "profile")));
    }

    /// <summary>
    /// The pipe door's spawn is <b>untouched</b> by the ACP seam — a machine driving the old way must
    /// not acquire an environment variable because a new adapter exists (D48 §2a, again).
    /// </summary>
    [Fact]
    public void The_pipe_door_gains_no_executable_seam()
    {
        var pipe = AdapterSet.Built().Resolve("claude-code");
        var info = pipe.Prepare(Target(_home), null);
        HarnessProbe.Apply(info, pipe.Toolchain!, profileHome: null, binary: null, claudeExecutable: null);

        Assert.False(info.Environment.ContainsKey(ClaudeAcp.ExecutableVariable));
    }

    /// <summary>
    /// The pipe door stays supported until the protocol door passes its real run (ACP2's own
    /// sentence). Asserted so that "we will keep it" cannot quietly stop being true.
    /// </summary>
    [Fact]
    public void The_pipe_door_is_still_there()
    {
        var pipe = AdapterSet.Built().Resolve("claude-code");

        Assert.Equal(SessionWire.Pipe, pipe.Wire);
        Assert.True(pipe.Interactive);
    }
}
