namespace Daoris.Driver;

/// <summary>
/// What Daoris writes into a <b>dsh home it created</b> (ACP3/D53, closing HELP2): the two outbound
/// rows off, and Daoris's own skills reachable.
/// </summary>
/// <remarks>
/// <para>🔴 <b>Only where Daoris made the directory.</b> A named credential profile is a directory
/// Daoris created and owns the location of (SES3); with no profile named, <c>DSH_HOME</c> is unset
/// and dsh uses the person's own <c>~/.dsh</c>, which is theirs. Silence means the harness's own
/// configuration home, and that rule is what keeps this feature purely additive — so this class
/// writes into a Daoris profile home and nowhere else, and <see cref="NoticeFor"/> is what happens
/// instead when there is no such home.</para>
///
/// <para><b>Every claim here was read from the harness's own shipped bundle</b> at
/// <c>0.1.6-alpha.2</c>, never guessed — the disable syntax, the two row ids, the patch file's path
/// and role, and the skill provider's option name and resolution order.
/// <c>docs/2026-09-22-acp3-probe-evidence.md</c> §4 and §5 are the record.</para>
/// </remarks>
public static class DshProfile
{
    /// <summary>
    /// The home-level user patch layer. dsh names it itself: "applied over every profile's own
    /// layer", so one write reaches every profile in this home rather than one of them.
    /// </summary>
    public const string PatchFile = "cordis.patch.yml";

    /// <summary>
    /// What says Daoris wrote this — the same provenance idea every materialized canon file carries,
    /// and for the same reason: without it, the only way to decide whether a file may be replaced is
    /// to guess.
    /// </summary>
    public const string Header = "# daoris: generated for a credential profile daoris owns";

    /// <summary>
    /// The rows that send transcript-class material off the machine, both shipped **on**.
    /// </summary>
    /// <remarks>
    /// <c>session-log-deepseek</c> uploads the canonical session log as a field on every
    /// official-route model request; <c>session-telemetry-otel</c> exports to a hosted collector
    /// under <c>FEEDBACK_ONLY</c>. Neither is hidden — each is documented — and D47 §4 says
    /// transcript-class material never leaves the machine, so a deployment either patches them off or
    /// accepts them out loud. Daoris does not accept them on anybody's behalf.
    /// </remarks>
    public static readonly IReadOnlyList<string> OutboundRows =
        ["session-telemetry-otel", "session-log-deepseek"];

    /// <summary>
    /// Where Daoris's skills already are. 🔴 <b>Relative, and that is load-bearing</b>: dsh resolves
    /// the default project roots per session <c>cwd</c>, but resolves <c>customSkillDirs</c> once, at
    /// construction, against the process's own working directory. The driver spawns one process per
    /// tree with its working directory set to that tree (D51), so a relative path lands on the right
    /// repository — where an absolute path written into a shared home would pin every session in it
    /// to whichever tree happened to be first.
    /// </summary>
    public const string SkillRoot = ".claude/skills";

    /// <summary>
    /// Write the patch layer into a dsh home Daoris owns, and answer where it landed.
    /// </summary>
    /// <remarks>
    /// <para><b>Replaces its own file and refuses somebody else's.</b> dsh's own documentation tells
    /// people to put their overlays at exactly this path, so an existing file may well be a person's
    /// work — and the header is what distinguishes the two. This is the same shape as the canon's
    /// region markers: a boundary that cannot be established is refused rather than guessed at,
    /// because what is on the other side of the guess is somebody's own configuration.</para>
    ///
    /// <para>Idempotent by construction: it writes the whole file rather than appending, so the loop
    /// may call it on every profile selection without the file growing or gaining a duplicate id.</para>
    /// </remarks>
    /// <exception cref="DriverException">The file exists and Daoris did not write it.</exception>
    public static string Write(string profileHome)
    {
        Directory.CreateDirectory(profileHome);
        var path = Path.Combine(profileHome, PatchFile);

        if (File.Exists(path) && !File.ReadAllText(path).StartsWith(Header, StringComparison.Ordinal))
        {
            throw new DriverException(
                $"`{path}` already exists and daoris did not write it, so it is not daoris's to "
                + "replace. Point this agent at a profile daoris created, or fold these rows into "
                + $"that file yourself: {string.Join(" and ", OutboundRows)} disabled, and "
                + $"`{SkillRoot}` added to skill-filesystem's `customSkillDirs`.");
        }

        File.WriteAllText(path, Compose());
        return path;
    }

    /// <summary>The file's content — one place, so the tests and the write cannot disagree.</summary>
    private static string Compose()
    {
        var lines = new List<string>
        {
            Header,
            "# Edit the profile, not this: daoris rewrites this file whenever it selects the profile.",
            "",
            "# D47 §4 — transcript-class material never leaves this machine. Both rows ship enabled.",
        };

        foreach (var row in OutboundRows)
        {
            lines.Add($"- id: {row}");
            lines.Add("  disabled: true");
        }

        lines.AddRange([
            "",
            "# HELP2 — daoris's skills are already `<name>/SKILL.md`, which is this provider's own",
            "# bundle format, so this is a root rather than a conversion. Relative on purpose: it",
            "# resolves against the session process's working directory, which is the tree.",
            "- id: skill-filesystem",
            "  config:",
            "    customSkillDirs:",
            $"      - {SkillRoot}",
            "",
        ]);

        return string.Join("\n", lines);
    }

    /// <summary>
    /// What to say when dsh is driven <b>without</b> a profile Daoris owns — and null for every other
    /// case, including every other harness.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>Three answers were possible and only one is honest.</b> Refusing the run would strand a
    /// capability over a default somebody else chose. Writing into <c>~/.dsh</c> would reach into the
    /// person's own configuration home, which SES3 puts out of bounds. So: run, and put the sentence
    /// where the run is read. The person can act on it in one command.
    /// </remarks>
    public static string? NoticeFor(string adapter, string? profileHome)
    {
        if (!string.Equals(adapter, "dsh", StringComparison.OrdinalIgnoreCase)) return null;
        if (profileHome is { Length: > 0 }) return null;

        return "— this dsh session runs in dsh's own configuration home, which daoris does not write "
               + "to. Two rows ship enabled there that send session material off this machine "
               + $"({string.Join(" and ", OutboundRows)}), and daoris has not turned them off. "
               + "`daoris agent profile add dsh <name>` makes a home daoris owns, where it does.";
    }
}
