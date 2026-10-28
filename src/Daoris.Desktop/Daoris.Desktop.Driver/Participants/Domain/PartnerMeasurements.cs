namespace Daoris.Driver;

/// <summary>
/// One row of the measurements table (COAGENT1b, D199 points 4 and 7; the co-agent design §3.1, §3.5): a capability measured on the
/// machine named, in the effective configuration named, with its outcome and the evidence document it rests on.
/// </summary>
/// <param name="Agent">The adapter's name.</param>
/// <param name="HarnessVersion">The harness's version measured; a start on another version reads not measured.</param>
/// <param name="AdapterVersion">The adapter's version measured, where it has one of its own (codex-acp's), kept for the record.</param>
/// <param name="Posture">The posture measured, in the harness's own words.</param>
/// <param name="WritableRoots">What it was handed to write (<see cref="PartnerConfigurations.CopyAlone"/>).</param>
/// <param name="Network">What it was handed of network reach (<see cref="PartnerConfigurations.HarnessDefault"/>).</param>
/// <param name="System">The operating system and its version, as <see cref="PartnerConfiguration.System"/> says it.</param>
/// <param name="SettingsDigest">The digest of what Daoris passed (<see cref="PartnerConfiguration.SettingsDigest"/>).</param>
/// <param name="Capability">What was measured: <see cref="PartnerMeasurements.Confinement"/>.</param>
/// <param name="Outcome"><see cref="PartnerMeasurements.Held"/> where every probe was refused; anything else admits nothing.</param>
/// <param name="Evidence">The evidence document's repository-relative path, which a census holds to a file that exists.</param>
public sealed record PartnerMeasurement(
    string Agent, string HarnessVersion, string? AdapterVersion, string Posture, string WritableRoots, string Network, string System,
    string SettingsDigest, string Capability, string Outcome, string Evidence)
{
    /// <summary>A stand-in's row, for the tests' stand-in agents alone: it never admits a production start.</summary>
    public bool StandIn { get; init; }
}

/// <summary>
/// The driver's measurements table and admission by it (COAGENT1b, D199 point 4; the co-agent design §3.3): a partner starts only where a
/// row measured its confinement as held in exactly the effective configuration the start would hand it. A row is added in the same change
/// as its evidence document (§3.5); none is assumed. Pure: the start hands in the configuration it made and whether it is a production
/// start.
/// </summary>
public static class PartnerMeasurements
{
    /// <summary>The capability a partner's admission reads: confinement, measured through an identified mechanism (M2).</summary>
    public const string Confinement = "partner-confinement";

    /// <summary>Every probe through the mechanism was refused or had no effect, and the hashes matched.</summary>
    public const string Held = "held";

    /// <summary>
    /// The rows measured on a machine, each with its evidence. Empty until M2 runs (D199's part-five note says what it runs): until then
    /// every production start is refused <see cref="PartnerWhy.NotMeasured"/>, the person's own included.
    /// </summary>
    public static readonly IReadOnlyList<PartnerMeasurement> Rows = [];

    /// <summary>
    /// Whether any row could admit <paramref name="agent"/> at <paramref name="harnessVersion"/>: the choice's first look, before the
    /// start makes its whole configuration. A production start reads no stand-in's row.
    /// </summary>
    public static bool MayAdmit(IReadOnlyList<PartnerMeasurement> rows, string agent, string? harnessVersion, bool production) =>
        Usable(rows, production).Any(row =>
            string.Equals(row.Agent, agent, StringComparison.OrdinalIgnoreCase)
            && (harnessVersion is null || string.Equals(row.HarnessVersion, harnessVersion, StringComparison.Ordinal)));

    /// <summary>
    /// The row that admits a start handing <paramref name="configuration"/>, or null: the same agent, harness version, posture, writable
    /// roots, network, system and settings digest, its confinement measured as held. A configuration with no harness version read is
    /// admitted by none, and a production start by no stand-in's row.
    /// </summary>
    public static PartnerMeasurement? Admitting(IReadOnlyList<PartnerMeasurement> rows, PartnerConfiguration configuration, bool production) =>
        configuration.HarnessVersion is not { Length: > 0 } version
            ? null
            : Usable(rows, production).FirstOrDefault(row =>
                string.Equals(row.Agent, configuration.Agent, StringComparison.OrdinalIgnoreCase)
                && row.HarnessVersion == version
                && row.Posture == configuration.Posture
                && row.WritableRoots == configuration.WritableRoots
                && row.Network == configuration.Network
                && row.System == configuration.System
                && string.Equals(row.SettingsDigest, configuration.SettingsDigest, StringComparison.OrdinalIgnoreCase));

    /// <summary>The sentence a start refused for want of a row says, naming what it would have handed.</summary>
    public static string Refused(PartnerConfiguration configuration) =>
        $"{configuration.Agent} is not admitted as a partner: no measurement of its confinement holds the configuration this start would hand it "
        + $"(version {configuration.HarnessVersion ?? "unread"}, posture {configuration.Posture ?? "none"}, {configuration.WritableRoots}, "
        + $"network {configuration.Network}, {configuration.System}, settings {configuration.SettingsDigest[..Math.Min(12, configuration.SettingsDigest.Length)]}).";

    private static IEnumerable<PartnerMeasurement> Usable(IReadOnlyList<PartnerMeasurement> rows, bool production) =>
        rows.Where(row => row.Capability == Confinement && row.Outcome == Held && !(production && row.StandIn));
}
