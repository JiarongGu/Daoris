using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// COAGENT1b (D199 points 4 and 7; the co-agent design §3.1, §3.3, §3.5): admission by a measured row. A partner starts only where a row
/// measured its confinement as held in exactly the effective configuration the start would hand; a different version, posture, system or
/// digest reads not measured; a stand-in's row never admits a production start; and every row of the table rests on an evidence
/// document that exists in the repository.
/// </summary>
public sealed class PartnerMeasurementsTests
{
    private static readonly PartnerConfiguration Handed = new(
        "codex-acp", "0.162.0", "agent", PartnerConfigurations.CopyAlone, PartnerConfigurations.HarnessDefault, "windows 10.0.26300",
        "ab12cd34ef56ab12cd34ef56ab12cd34ef56ab12cd34ef56ab12cd34ef56ab12");

    private static PartnerMeasurement Row(PartnerConfiguration at, string outcome = PartnerMeasurements.Held, bool standIn = false) =>
        new(at.Agent, at.HarnessVersion!, "0.9.0", at.Posture!, at.WritableRoots, at.Network, at.System, at.SettingsDigest,
            PartnerMeasurements.Confinement, outcome, "docs/2026-10-22-coagent-design.md")
        {
            StandIn = standIn,
        };

    /// <summary>🔴 Only a row holding the whole configuration admits it; each part moved reads not measured.</summary>
    [Fact]
    public void A_start_is_admitted_only_by_a_row_holding_its_whole_configuration()
    {
        IReadOnlyList<PartnerMeasurement> rows = [Row(Handed)];

        Assert.NotNull(PartnerMeasurements.Admitting(rows, Handed, production: true));
        Assert.Null(PartnerMeasurements.Admitting(rows, Handed with { HarnessVersion = "0.163.0" }, production: true));
        Assert.Null(PartnerMeasurements.Admitting(rows, Handed with { Posture = "agent-full-access" }, production: true));
        Assert.Null(PartnerMeasurements.Admitting(rows, Handed with { System = "windows 10.0.26100" }, production: true));
        Assert.Null(PartnerMeasurements.Admitting(rows, Handed with { SettingsDigest = new string('0', 64) }, production: true));
        Assert.Null(PartnerMeasurements.Admitting(rows, Handed with { Agent = "claude-code-acp" }, production: true));
        Assert.Null(PartnerMeasurements.Admitting(rows, Handed with { HarnessVersion = null }, production: true));
        Assert.Null(PartnerMeasurements.Admitting([Row(Handed, outcome: "escaped")], Handed, production: true));
        Assert.Contains("not admitted as a partner", PartnerMeasurements.Refused(Handed));
    }

    /// <summary>🔴 A stand-in's row admits a test's start and never a production start, at the choice or at the start.</summary>
    [Fact]
    public void A_stand_ins_row_never_admits_a_production_start()
    {
        IReadOnlyList<PartnerMeasurement> rows = [Row(Handed, standIn: true)];

        Assert.NotNull(PartnerMeasurements.Admitting(rows, Handed, production: false));
        Assert.Null(PartnerMeasurements.Admitting(rows, Handed, production: true));
        Assert.True(PartnerMeasurements.MayAdmit(rows, "codex-acp", "0.162.0", production: false));
        Assert.False(PartnerMeasurements.MayAdmit(rows, "codex-acp", "0.162.0", production: true));

        var chosen = new ReviewerChoice("codex-acp", "another-maker", null, "codex-acp writes it.");
        Assert.Equal((false, PartnerWhy.NotMeasured), (PartnerChoice.Admit(chosen, rows, production: true).Chosen, PartnerChoice.Admit(chosen, rows, production: true).Code));
        Assert.True(PartnerChoice.Admit(chosen, rows, production: false).Chosen);
    }

    /// <summary>
    /// The census (§3.1): every row of the code's table names an evidence document that exists under <c>docs/</c>, measures a capability
    /// this build reads, and is no stand-in's. Until M2 runs the table is empty, and no production start is admitted.
    /// </summary>
    [Fact]
    public void Every_row_rests_on_an_evidence_document_in_the_repository()
    {
        var root = GitSeed.RepoRoot();
        foreach (var row in PartnerMeasurements.Rows)
        {
            Assert.StartsWith("docs/", row.Evidence);
            Assert.True(File.Exists(Path.Combine(root, row.Evidence)), $"{row.Agent} {row.HarnessVersion}: no evidence at {row.Evidence}");
            Assert.Equal(PartnerMeasurements.Confinement, row.Capability);
            Assert.False(row.StandIn);
        }

        Assert.False(PartnerMeasurements.Rows.Any(row => row.Agent == "codex-acp")
                     || PartnerMeasurements.MayAdmit(PartnerMeasurements.Rows, "codex-acp", null, production: true));
    }
}
