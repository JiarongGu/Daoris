namespace Daoris.Driver;

/// <summary>
/// Ask Daoris's access mode (HELPMODE1a1, D183 point 1), the person's choice, machine-wide for now: <see cref="Restricted"/>, what
/// D89 made it, reading and proposing with the person applying; or <see cref="Full"/>, its harness's own mode that asks nothing.
/// </summary>
/// <remarks>
/// <para><b>Kept in the driver's file</b> as <c>helpMode</c> (<see cref="DriverConfig.HelpMode"/>), written only while full; the
/// CLI's <c>driverconfig.ts</c> reads it by the same table. Set at a terminal with <c>daoris-driver help mode</c>, and never by Ask
/// Daoris itself: no proposal names it (D183 point 2, full access is never chosen for the person).</para>
/// </remarks>
public static class HelpModes
{
    /// <summary>It reads, advises and proposes, and the person applies (D89): the default.</summary>
    public const string Restricted = "restricted";

    /// <summary>It acts on this machine itself, in its harness's mode that asks nothing (D183 point 1).</summary>
    public const string Full = "full";

    /// <summary>The modes, in the order a person is offered them.</summary>
    public static IReadOnlyList<string> All { get; } = [Restricted, Full];

    /// <summary>The mode <paramref name="value"/> names, trimmed and in any case, or null for none.</summary>
    public static string? Named(string? value) =>
        All.FirstOrDefault(mode => string.Equals(mode, value?.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The mark before a protocol door's posture that must be set (<c>AcpSession.SetPostureAsync</c>): an agent that does not offer
    /// it fails the start, so a full start never runs in a narrower mode under the full label (D183 point 3).
    /// </summary>
    public const char Required = '!';
}
