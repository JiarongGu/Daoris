namespace Daoris.Driver;

/// <summary>
/// A <c>driver.json</c> that does not read, said as the driver says a refusal (CONFIGREAD1): the file, and where in it when
/// the parser knows, in a sentence every door meets. It is a <see cref="DriverException"/>, so the headless host's one catch
/// says it and exits 2 (REV3), the modules' route answers it as the driver's refusal, and every console's catch already takes
/// it. Thrown by <see cref="DriverConfig.Load"/> alone, the one reader (CONFIGSEAM1).
/// </summary>
/// <param name="path">The file that did not read, as the door resolved it.</param>
/// <param name="message">The sentence.</param>
/// <param name="line">The line the parser stopped on, counted from one; null when the JSON parsed and its shape is wrong.</param>
/// <param name="byte">The byte in that line, counted from one; null with <paramref name="line"/>.</param>
public sealed class DriverConfigUnreadableException(string path, string message, long? line = null, long? @byte = null)
    : DriverException(message)
{
    /// <summary>The words every refusal of a file that did not parse or is not the choices' shape ends with, as the CLI's do.</summary>
    public const string Remedy = "Fix it, or delete it to start from nothing.";

    public string Path { get; } = path;

    public long? Line { get; } = line;

    public long? Byte { get; } = @byte;
}
