using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// **What Daoris writes into a dsh home it created** (ACP3, closing HELP2) — the two outbound rows
/// off, and Daoris's own skills reachable.
/// </summary>
/// <remarks>
/// <para>🔴 <b>The boundary this whole file defends: Daoris writes here ONLY where it made the
/// directory.</b> With no named profile, `DSH_HOME` is unset and dsh uses the person's own `~/.dsh`
/// — which is theirs, and which SES3's "silence means the harness's own configuration home" puts
/// firmly out of reach. So the patch is written into a Daoris-created profile home and nowhere else,
/// and a run without one is <b>told what that means</b> rather than quietly accepting it.</para>
///
/// <para>Every fact here was read from `@deepseek-ai/dsh@0.1.6-alpha.2`'s own shipped bundle;
/// `docs/2026-09-22-acp3-probe-evidence.md` §4 and §5 carry the evidence.</para>
/// </remarks>
public sealed class DshProfileTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-dsh-profile", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    /// <summary>
    /// 🔴 <b>Both rows send transcript-class material off the machine, and D47 §4 says it never
    /// leaves.</b> `session-log-deepseek` uploads the canonical session log as a field on every
    /// official-route model request; `session-telemetry-otel` exports to a hosted collector. Neither
    /// is hidden and both ship on — so a deployment either patches them off or accepts them out loud,
    /// and Daoris is not in the business of accepting them on somebody's behalf.
    /// </summary>
    [Fact]
    public void The_two_outbound_rows_are_patched_off()
    {
        var patch = DshProfile.Write(_home);
        var text = File.ReadAllText(patch);

        Assert.Contains("- id: session-telemetry-otel", text);
        Assert.Contains("- id: session-log-deepseek", text);
        // The disable is an id-targeted ROW, not a config key — dsh says so itself beside the
        // telemetry row: "config cannot disable a row".
        Assert.Equal(2, text.Split("disabled: true").Length - 1);
    }

    /// <summary>
    /// HELP2: dsh's skill provider scans `.dsh/skills` and `.agents/skills`, never `.claude/skills`
    /// — but its bundle format is `&lt;name&gt;/SKILL.md`, <b>exactly</b> Daoris's layout. So this is
    /// a root, not a conversion, and nothing on an adopter's disk changes.
    /// </summary>
    [Fact]
    public void Daoris_skills_are_reachable_as_an_additional_root()
    {
        var text = File.ReadAllText(DshProfile.Write(_home));

        Assert.Contains("skill-filesystem", text);
        Assert.Contains(".claude/skills", text);
    }

    /// <summary>
    /// 🔴 <b>The path is RELATIVE on purpose, and the reason is a trap.</b> dsh resolves the default
    /// project roots per session `cwd`, but resolves `customSkillDirs` <b>once, at construction,
    /// against the process's own cwd</b>. The driver spawns one process per tree with its working
    /// directory set to that tree (D51), so a relative path lands on the right repository — and an
    /// absolute one written into a shared home would pin every session to whichever tree happened to
    /// be first.
    /// </summary>
    [Fact]
    public void The_skill_root_is_relative_because_the_process_cwd_is_the_tree()
    {
        var text = File.ReadAllText(DshProfile.Write(_home));

        Assert.DoesNotContain(_home, text);
        Assert.DoesNotContain(":\\", text);
    }

    /// <summary>
    /// dsh names this file itself — "the home-level user patch layer, applied over every profile's
    /// own layer" — so one write reaches every profile in the home Daoris created, rather than one.
    /// </summary>
    [Fact]
    public void It_is_the_home_level_patch_layer_so_it_reaches_every_profile()
    {
        var patch = DshProfile.Write(_home);

        Assert.Equal(Path.Combine(_home, "cordis.patch.yml"), patch);
    }

    /// <summary>
    /// Re-running is not a second copy. The loop writes this per profile selection, so an operation
    /// that appended would grow the file without bound and the second `session-telemetry-otel` row
    /// would be read as a duplicate id.
    /// </summary>
    [Fact]
    public void Writing_twice_leaves_one_file_saying_one_thing()
    {
        var first = File.ReadAllText(DshProfile.Write(_home));
        var second = File.ReadAllText(DshProfile.Write(_home));

        Assert.Equal(first, second);
        Assert.Equal(2, second.Split("disabled: true").Length - 1);
    }

    /// <summary>
    /// 🔴 <b>A person's own patch layer is never overwritten.</b> A home Daoris did not create can
    /// still be pointed at by a profile someone wired by hand, and the file is theirs — dsh's own
    /// docs tell people to put their overlays in exactly this path. An existing file that is not
    /// Daoris's is left alone and reported, which is the same refusal shape the canon's region
    /// markers use: what is on the other side of the guess is somebody's own configuration.
    /// </summary>
    [Fact]
    public void An_existing_hand_written_patch_layer_is_left_exactly_alone()
    {
        Directory.CreateDirectory(_home);
        var patch = Path.Combine(_home, "cordis.patch.yml");
        const string theirs = "# mine\n- id: tool-ralph\n  disabled: false\n";
        File.WriteAllText(patch, theirs);

        var error = Assert.Throws<DriverException>(() => DshProfile.Write(_home));

        Assert.Equal(theirs, File.ReadAllText(patch));
        Assert.Contains("cordis.patch.yml", error.Message);
    }

    /// <summary>
    /// The file Daoris wrote says so, which is what makes the refusal above decidable — and is the
    /// same provenance header every materialized canon file carries, for the same reason.
    /// </summary>
    [Fact]
    public void What_daoris_wrote_says_that_it_did()
    {
        var text = File.ReadAllText(DshProfile.Write(_home));

        Assert.StartsWith(DshProfile.Header, text);
    }

    /// <summary>
    /// 🔴 <b>These exact bytes were composed by the real dsh.</b> Written into a scratch
    /// <c>DSH_HOME</c> at 0.1.6-alpha.2, <c>dsh --profile acp --dump-config</c> answered with all
    /// three rows patched and annotated each one with the file that patched it — so this is not an
    /// assertion that the YAML parses, it is the tree the harness actually builds.
    /// </summary>
    /// <remarks>
    /// Pinned verbatim because every other test here checks a property, and a property-checked file
    /// can drift into something that still satisfies every property and no longer composes: an id
    /// that is not a row, a key one level too deep, a list where a scalar belongs. The composed tree
    /// is the contract and this is the copy of it that was proven.
    /// </remarks>
    [Fact]
    public void The_bytes_are_the_ones_dsh_was_observed_to_compose()
    {
        const string proven = """
            # daoris: generated for a credential profile daoris owns
            # Edit the profile, not this: daoris rewrites this file whenever it selects the profile.

            # D47 §4 — transcript-class material never leaves this machine. Both rows ship enabled.
            - id: session-telemetry-otel
              disabled: true
            - id: session-log-deepseek
              disabled: true

            # HELP2 — daoris's skills are already `<name>/SKILL.md`, which is this provider's own
            # bundle format, so this is a root rather than a conversion. Relative on purpose: it
            # resolves against the session process's working directory, which is the tree.
            - id: skill-filesystem
              config:
                customSkillDirs:
                  - .claude/skills

            """;

        Assert.Equal(proven.ReplaceLineEndings("\n"), File.ReadAllText(DshProfile.Write(_home)));
    }

    /// <summary>
    /// 🔴 <b>Driving dsh with no named profile is allowed and is SAID.</b> Refusing would strand a
    /// capability, and writing into `~/.dsh` would be reaching into the person's own home — so the
    /// third answer is the honest one: run, and put the sentence where the run is read.
    /// </summary>
    [Fact]
    public void Driving_without_a_daoris_profile_is_allowed_and_named()
    {
        var notice = DshProfile.NoticeFor(adapter: "dsh", profileHome: null);

        Assert.NotNull(notice);
        Assert.Contains("telemetry", notice, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("daoris agent profile add dsh", notice);
    }

    /// <summary>With a profile Daoris owns, there is nothing to warn about and nothing is said.</summary>
    [Fact]
    public void A_run_under_a_daoris_profile_has_nothing_to_warn_about()
    {
        Assert.Null(DshProfile.NoticeFor(adapter: "dsh", profileHome: _home));
    }

    /// <summary>
    /// The notice belongs to dsh alone. No other harness ships rows that send a transcript anywhere,
    /// and a warning that fired for all of them would be noise that teaches people to skip warnings.
    /// </summary>
    [Theory]
    [InlineData("claude-code-acp")]
    [InlineData("codex-acp")]
    [InlineData("claude-code")]
    [InlineData("acp-stub")]
    public void No_other_harness_carries_this_notice(string adapter)
    {
        Assert.Null(DshProfile.NoticeFor(adapter, profileHome: null));
    }
}
