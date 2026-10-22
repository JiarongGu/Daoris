using Daoris.Driver;
using System.Diagnostics;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// 🔴 LIVELOGIN1: a sign-in under a scratch home (under <c>_fixtures/</c> or the OS's temporary folder) never runs the real agent,
/// which is the agent's own binary found on <c>PATH</c>; a stub at a path, a command of another name and an agent with no binary of
/// its own still sign in. Prepared, never started, and every start of the flow held by a stand-in that records it: the real
/// <c>claude</c> is never run, not even to prove the refusal.
/// </summary>
public sealed class ScratchSignInsTests : IDisposable
{
    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "scratch-sign-ins", Guid.NewGuid().ToString("N")[..8]);

    public ScratchSignInsTests() => Directory.CreateDirectory(Home);

    public void Dispose() => GitSeed.Discard(_root);

    private string Home => Path.Combine(_root, "home");

    private static HarnessToolchain Claude => AdapterSet.Built().Resolve("claude-code").Toolchain!;

    [Fact]
    public void A_scratch_home_is_one_under_fixtures_or_the_temporary_folder()
    {
        Assert.True(ScratchSignIns.IsScratch(Home));
        Assert.True(ScratchSignIns.IsScratch(Path.Combine(Path.GetTempPath(), "daoris-anything", "home")));
        Assert.False(ScratchSignIns.IsScratch(Path.GetTempPath()));
        Assert.False(ScratchSignIns.IsScratch(Path.Combine(WorkspaceRoot.Folder, "local", "home")));
        Assert.False(ScratchSignIns.IsScratch(Path.Combine(WorkspaceRoot.Folder, "_fixtures-not", "home")));
        Assert.False(ScratchSignIns.IsScratch(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)));
        Assert.False(ScratchSignIns.IsScratch(""));
    }

    [Fact]
    public void The_real_binary_is_the_agent_s_own_name_on_PATH_and_nothing_else()
    {
        var stub = Path.Combine(_root, "bin", OperatingSystem.IsWindows() ? "claude.cmd" : "claude");

        Assert.True(ScratchSignIns.IsReal(Claude, ["claude"]));
        Assert.True(ScratchSignIns.IsReal(Claude, ["claude.exe", "--x"]));
        Assert.True(ScratchSignIns.IsReal(CodexAccounts.Toolchain, ["codex"]));
        // A stub named like the real one, by its path, as a pin's managed binary under the scratch home is.
        Assert.False(ScratchSignIns.IsReal(Claude, [stub]));
        Assert.False(ScratchSignIns.IsReal(Claude, ["node", "claude.mjs"]));
        Assert.False(ScratchSignIns.IsReal(Claude, []));
        Assert.False(ScratchSignIns.IsReal(new StubAdapter().Toolchain!, ["claude"]));
    }

    /// <summary>
    /// The window's sign-in, PATH's <c>claude</c> into a new account under the scratch home: refused naming LIVELOGIN1 and the stub
    /// adapters, no folder made and nothing started. A stub laid in the scratch folder by its path, named like the real one, is prepared.
    /// </summary>
    [Fact]
    public async Task A_sign_in_under_a_scratch_home_with_the_real_binary_is_refused_with_nothing_started()
    {
        var fresh = HarnessSettings.ProfileHome(Home, "claude-code", "acct-0000aaaa");
        var started = new List<ProcessStartInfo>();
        using (ProcessStarts.Within(info => { started.Add(info); return null; }))
        {
            var real = new HarnessCommand("claude-code", ["claude"], null, null); // live-logins: refused, never started
            var refused = await Assert.ThrowsAsync<DriverException>(
                () => HarnessActions.LoginAsync(Claude, real, fresh, _ => { }, fresh: true));

            Assert.Equal(
                "a sign-in under a scratch home runs no real agent, and `claude-code`'s would have run the `claude` on PATH, so nothing "
                + "was started — a test, a probe or a transcription signs in with the stub adapters (`stub`, `acp-stub`), or a stub "
                + "binary laid under its scratch home (LIVELOGIN1).",
                refused.Message);
            Assert.False(Directory.Exists(fresh));
        }

        Assert.Empty(started);

        var stub = Path.Combine(_root, "bin", OperatingSystem.IsWindows() ? "claude.cmd" : "claude");
        var info = HarnessActions.PrepareLogin(Claude, new HarnessCommand("claude-code", [stub], null, null), fresh, fresh: true);
        Assert.Equal(stub, info.FileName);
        Assert.Equal(["auth", "login"], info.ArgumentList);
    }

    /// <summary>A home that is no scratch one is not this guard's: PATH's binary is prepared there, as a person's install runs it.</summary>
    [Fact]
    public void A_home_that_is_no_scratch_one_prepares_the_real_binary()
    {
        var home = Path.Combine(WorkspaceRoot.Folder, "local", "scratch-sign-ins-unused");
        var account = HarnessSettings.ProfileHome(home, "claude-code", "acct-0000aaaa");

        var real = new HarnessCommand("claude-code", ["claude"], null, null); // live-logins: refused, never started

        Assert.Null(ScratchSignIns.Refusal(Claude, real, account));
    }
}
