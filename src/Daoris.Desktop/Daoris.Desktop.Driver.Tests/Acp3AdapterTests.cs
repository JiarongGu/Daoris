using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// **dsh and codex as configurations of the protocol door** (ACP3/D53). Everything here is keyless,
/// and every claim about somebody else's program was established against an installed artefact at an
/// exact version — `docs/2026-09-22-acp3-probe-evidence.md` is the record, and it says which facts
/// came from a live wire and which from a shipped bundle.
/// </summary>
/// <remarks>
/// <para>🔴 <b>The single finding these tests exist to hold: the D37 posture lives in three
/// different places.</b> Claude Code and Codex express it as an ACP <b>mode</b> on the wire — with
/// <em>different mode ids</em> — and dsh does not express it on the wire at all, because its
/// `session/new` carries no `modes` key. Its posture is an environment variable read at boot. An
/// adapter set that assumed one mechanism would have silently set nothing for two of the three.</para>
/// </remarks>
public sealed class Acp3AdapterTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-acp3-tests", Guid.NewGuid().ToString("N")[..8]);

    public Acp3AdapterTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private static SessionTarget Target(string root) => new(
        "abc123", "Expose a streaming budget", "World streaming needs a per-frame cap.",
        "game", "engine", root, "http://localhost:5177");

    private static ISessionAdapter Adapter(string name) => AdapterSet.Built().Resolve(name);

    // ---- the seam: a posture per adapter, in that adapter's own vocabulary --------------------

    /// <summary>
    /// 🔴 <b>Each ACP harness names its own posture, and nothing has a default.</b> The mode id was a
    /// constant inside the session while one harness rode the door; a second harness with a different
    /// id would have been silently driven at whatever mode it happened to start in.
    /// </summary>
    [Theory]
    [InlineData("claude-code-acp", "auto|acceptEdits")]  // D81: auto where offered, else as observed live (evaluation §1a)
    [InlineData("codex-acp", "agent")]              // read from codex-acp@1.12.0's own bundle
    public void Each_protocol_harness_states_the_posture_in_its_own_vocabulary(string name, string posture)
    {
        Assert.Equal(posture, Adapter(name).AcpPosture);
    }

    /// <summary>
    /// dsh carries no posture on the wire because its `session/new` offers no modes — so the adapter
    /// says so rather than naming a mode that would never be found. It is set in the environment
    /// instead, and the next test holds that.
    /// </summary>
    [Theory]
    [InlineData("dsh")]
    [InlineData("acp-stub")]
    public void An_adapter_whose_wire_carries_no_posture_names_none(string name)
    {
        Assert.Null(Adapter(name).AcpPosture);
    }

    // ---- dsh --------------------------------------------------------------------------------

    /// <summary>
    /// dsh's posture is `DSH_PERMISSION_MODE`, and `workspace-write` is D37 in its vocabulary: writes
    /// inside the workspace are sandbox-legal and proceed, and anything escalating past the sandbox
    /// asks — which over ACP arrives as `session/request_permission` and is refused by construction.
    /// </summary>
    /// <remarks>
    /// 🔴 It is <b>stated even though it is also dsh's default</b>. A posture that happens to match
    /// somebody else's default is not a posture Daoris has set, and the default is theirs to change.
    /// `danger-full-access` is the value that would turn approvals off entirely, which is what D37
    /// forbids — so the test names it as the thing that must never appear.
    /// </remarks>
    [Fact]
    public void Dsh_carries_the_posture_in_the_environment_because_its_wire_will_not()
    {
        var info = Adapter("dsh").Prepare(Target(_home), command: null);

        Assert.Equal("workspace-write", info.Environment["DSH_PERMISSION_MODE"]);
        Assert.DoesNotContain("danger-full-access", info.Environment.Values);
    }

    /// <summary>
    /// The profile is a whole dsh HOME, not a flag: `--profile <name>` names a directory under
    /// `$DSH_HOME/profiles`, which is why `DSH_HOME` is the account seam and one variable isolates
    /// credentials, settings and sessions together.
    /// </summary>
    [Fact]
    public void Dsh_boots_the_acp_profile_and_seams_on_its_home()
    {
        var adapter = Adapter("dsh");
        var info = adapter.Prepare(Target(_home), command: null);

        Assert.Equal(SessionWire.Acp, adapter.Wire);
        Assert.True(info.RedirectStandardInput);
        Assert.Contains("--profile", info.ArgumentList);
        Assert.Contains("acp", info.ArgumentList);
        Assert.Equal("DSH_HOME", adapter.Toolchain!.ProfileVariable);
    }

    /// <summary>
    /// No target on the command line, on either protocol adapter: the work arrives as
    /// `session/prompt`. An adapter that also passed it as an argument would send it twice.
    /// </summary>
    [Theory]
    [InlineData("dsh")]
    [InlineData("codex-acp")]
    public void The_target_is_never_an_argument_on_the_protocol_door(string name)
    {
        var arguments = string.Join(' ', Adapter(name).Prepare(Target(_home), command: null).ArgumentList);

        Assert.DoesNotContain("streaming budget", arguments);
        Assert.DoesNotContain("-p", arguments.Split(' '));
    }

    /// <summary>
    /// dsh asks no login question (SES3: a cached refusal is re-asked, and only a definite *out*
    /// refuses — dsh has no notion of an account to be out of, so `unknown` is permissive and a
    /// session starts).
    /// </summary>
    [Fact]
    public void Dsh_has_no_login_question_to_ask()
    {
        Assert.Null(Adapter("dsh").Toolchain!.LoginCheck);
    }

    // ---- codex ------------------------------------------------------------------------------

    [Fact]
    public void Codex_acp_is_a_protocol_door_session_seamed_on_codex_home()
    {
        var adapter = Adapter("codex-acp");
        var info = adapter.Prepare(Target(_home), command: null);

        Assert.Equal(SessionWire.Acp, adapter.Wire);
        Assert.True(info.RedirectStandardInput);
        Assert.Equal("CODEX_HOME", adapter.Toolchain!.ProfileVariable);
        Assert.Equal("codex-acp", adapter.Toolchain!.Binary[0]);
    }

    /// <summary>
    /// 🔴 <b>`CODEX_HOME` must ALREADY EXIST</b>, where the Claude adapter creates
    /// `CLAUDE_CONFIG_DIR` for itself. Pointed at a path that is not there, codex-acp exits 1 before
    /// `initialize` completes, naming the directory. Observed at 1.12.0; the probe evidence note has
    /// the verbatim error.
    /// </summary>
    [Fact]
    public void Codex_declares_that_its_profile_directory_must_exist()
    {
        Assert.True(Adapter("codex-acp").Toolchain!.ProfileMustExist);
        // The other door's seam is created by the harness, so it makes no such demand.
        Assert.False(Adapter("claude-code-acp").Toolchain!.ProfileMustExist);
    }

    /// <summary>
    /// The account belongs to `codex`, exactly as the Claude ACP adapter's belongs to `claude-code`:
    /// the adapter runs the harness and reads the home the harness logged into, so it has no login
    /// flow of its own to offer.
    /// </summary>
    [Fact]
    public void Codex_acp_borrows_the_account_of_codex_rather_than_owning_one()
    {
        Assert.Equal("codex", Adapter("codex-acp").Toolchain!.AccountOf);
        Assert.Null(Adapter("codex-acp").Toolchain!.LoginArguments);
    }

    /// <summary>
    /// 🔴 <b>The declaration is bound to the behaviour, or it is decoration.</b> The driver creates
    /// every profile home before spawning, which is what makes `ProfileMustExist` true today — but
    /// nothing said WHY that line must stay, so removing it would have broken exactly one harness in
    /// a way that reads as an unrelated crash. This asserts the contract for every harness that
    /// declares the requirement, rather than for the one that happens to have it.
    /// </summary>
    [Fact]
    public void A_harness_that_demands_its_profile_exist_is_given_one_before_it_spawns()
    {
        var demanding = AdapterSet.Built().Names
            .Select(AdapterSet.Built().Resolve)
            .Where(a => a.Toolchain is { ProfileMustExist: true })
            .ToList();

        Assert.NotEmpty(demanding);

        foreach (var adapter in demanding)
        {
            var home = Path.Combine(_home, "profiles", adapter.Name);
            Assert.False(Directory.Exists(home), "the profile must not exist before the spawn is prepared");

            var info = adapter.Prepare(Target(_home), command: null);
            HarnessProbe.Apply(info, adapter.Toolchain!, home);

            Assert.True(
                Directory.Exists(home),
                $"`{adapter.Name}` exits before `initialize` when its home is missing, so preparing a "
                + "spawn under a named profile must create the directory");
            Assert.Equal(home, info.Environment[adapter.Toolchain!.ProfileVariable!]);
        }
    }

    /// <summary>
    /// 🔴 <b>A transcript is read as UTF-8 or it is not the transcript.</b> Every adapter, both doors.
    /// </summary>
    /// <remarks>
    /// <para>Found on the first real deployment. A driven <c>claude-code</c> session wrote an em-dash
    /// (<c>e2 80 94</c>) and the transcript held <c>e9 88 a5 3f</c> — that sequence decoded as the
    /// machine's ANSI codepage (CP936 here) and re-encoded. .NET defaults a redirected stream to the
    /// console's codepage, which on an English machine is close enough to ASCII to look fine and on
    /// this one is not.</para>
    ///
    /// <para>It matters more than a mangled dash: this platform speaks <b>简体中文</b>, and a
    /// transcript that cannot carry a dash carries no Chinese at all. The failure is also silent —
    /// the file is still valid UTF-8 afterwards, so nothing downstream can tell it was ever wrong.</para>
    /// </remarks>
    [Theory]
    [InlineData("claude-code")]
    [InlineData("claude-code-acp")]
    [InlineData("codex-acp")]
    [InlineData("dsh")]
    [InlineData("stub")]
    [InlineData("acp-stub")]
    public void Every_adapter_reads_its_session_as_utf8(string name)
    {
        var adapter = Adapter(name);
        var info = adapter.Prepare(Target(_home), command: ["node", "agent.mjs"]);

        Assert.Equal(System.Text.Encoding.UTF8, info.StandardOutputEncoding);
        Assert.Equal(System.Text.Encoding.UTF8, info.StandardErrorEncoding);
    }

    // ---- the roster -------------------------------------------------------------------------

    /// <summary>
    /// Both arrive BESIDE what was there, never replacing it — the additive rule every part of this
    /// toolchain holds. An unknown name still errors naming what exists (D23).
    /// </summary>
    [Fact]
    public void The_roster_gained_two_configurations_of_the_door_and_lost_nothing()
    {
        var names = AdapterSet.Built().Names;

        Assert.Contains("dsh", names);
        Assert.Contains("codex-acp", names);
        Assert.Contains("claude-code", names);
        Assert.Contains("claude-code-acp", names);
        Assert.Contains("acp-stub", names);
        Assert.Contains("stub", names);
    }

    /// <summary>
    /// Every ACP adapter that names a posture must be pinnable, because a protocol adapter that
    /// moves under a running loop is the moving target D49 §4 refuses for harnesses — and D53 wrote
    /// that rule from an adapter that reached 0.79 in the week it was evaluated.
    /// </summary>
    [Fact]
    public void Every_protocol_adapter_declares_a_package_so_it_can_be_pinned()
    {
        var acp = AdapterSet.Built().Names
            .Select(AdapterSet.Built().Resolve)
            .Where(a => a.Wire == SessionWire.Acp && a.Toolchain is { Install: not null });

        Assert.All(acp, adapter => Assert.False(string.IsNullOrWhiteSpace(adapter.Toolchain!.Package)));
    }

    /// <summary>
    /// 🔴 <b>This set and the CLI's `TOOLCHAINS` are twins, and the risk is not membership.</b> The
    /// two differ on purpose — managing a tool and spawning sessions on it are different questions
    /// (D23), which is why the CLI has `codex` with no adapter. What must never differ is a shared
    /// NAME's descriptor: the CLI probing one binary while the driver spawns another is a
    /// `harness list` reporting on a program nothing runs, and it reads as correct from both sides.
    /// </summary>
    /// <remarks>
    /// The binaries are pinned on both sides rather than compared across a language boundary — the
    /// same shape the remotes map's three twins use, where the FILE is the contract and the test
    /// tables move together. The 🔴 here is that the binary is the field that has already been wrong
    /// once: `claude-agent-acp` was guessed as the adapter's Daoris name and caught by `harness list`
    /// reporting a pin that was installed as absent.
    /// </remarks>
    public static TheoryData<string, string, string, string, string> Twins => new()
    {
        { "claude-code", "claude", "CLAUDE_CONFIG_DIR", "Claude Code", "Anthropic" },
        { "claude-code-acp", "claude-agent-acp", "CLAUDE_CONFIG_DIR", "Claude Code", "Anthropic" },
        { "codex-acp", "codex-acp", "CODEX_HOME", "Codex", "OpenAI" },
        { "dsh", "dsh", "DSH_HOME", "dsh", "DeepSeek" },
    };

    [Theory]
    [MemberData(nameof(Twins))]
    public void A_shared_name_means_the_same_binary_seam_and_tool_on_both_sides(
        string name, string binary, string seam, string product, string maker)
    {
        var toolchain = Adapter(name).Toolchain!;

        Assert.Equal(binary, Assert.Single(toolchain.Binary));
        Assert.Equal(seam, toolchain.ProfileVariable);
        // AGT1: what a person calls it, and whose it is — `dsh` meant nothing until it said.
        Assert.Equal(product, toolchain.Product);
        Assert.Equal(maker, toolchain.Maker);
    }

    /// <summary>
    /// 🔴 <b>And the table covers every adapter that names a real binary</b> — because proving one
    /// row's reach proves nothing about the next one.
    /// </summary>
    /// <remarks>
    /// The table above was written during ACP3 and pinned the three arrivals, which left
    /// <c>claude-code</c> — the oldest entry, and the one a machine actually drives with — asserted
    /// on neither side. Nothing was wrong with it; nothing would have said so either. A count that
    /// never <i>rose</i> is the half of this failure that has no earlier number to fall from, so the
    /// membership is derived from the adapter set rather than remembered.
    ///
    /// The stubs are excluded by the only honest test there is: they name no binary, because the
    /// "binary" is whatever a configuration says (D46 §8).
    /// </remarks>
    [Fact]
    public void Every_adapter_with_a_real_binary_is_in_the_twin_table()
    {
        var set = AdapterSet.Built();
        var real = set.Names
            .Where(name => set.Resolve(name).Toolchain is { Binary.Count: > 0 })
            .ToList();

        var pinned = Twins.Select(row => (string)row[0]!).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.All(real, name => Assert.True(
            pinned.Contains(name),
            $"`{name}` names a binary the driver spawns and is pinned by no twin row — so the CLI's "
            + "TOOLCHAINS could describe a different program under the same name and both sides would "
            + "read as correct."));
    }
}
