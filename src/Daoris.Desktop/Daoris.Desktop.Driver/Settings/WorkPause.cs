namespace Daoris.Driver;

/// <summary>
/// One pause of an ask's work or a quest's, as <c>driver.json</c> keeps it (PAUSE1a, design §2.5). Machine-local, like
/// everything in that file: a pause is this machine's (D132 point 10).
/// </summary>
/// <param name="At">When the person paused it, or null where the file does not say in a form this reads.</param>
/// <param name="Stopped">Each quest the pause stopped, against the session it stopped: what <i>Resume</i> writes into <see cref="DriverConfig.Released"/> (design §2.4).</param>
public sealed record WorkPause(DateTimeOffset? At, IReadOnlyDictionary<string, string> Stopped);
