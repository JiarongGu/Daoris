namespace Daoris.Driver;

/// <summary>
/// Where the standing choices' command acts: the terminal it says its words on and finds the running Daoris from, the
/// <c>driver.json</c> it reads and writes, and the facts a choice judges by when it acts alone: the registry's checkouts a review's
/// procedure is looked for in. A test hands its own of each.
/// </summary>
public sealed record DriverChoicesWorld
{
    /// <summary>The terminal: its home, its variables (read here, never the process's), its streams, and where it finds the instance.</summary>
    public required IChoiceTerminal Instance { get; init; }

    /// <summary>The <c>driver.json</c> the choices are read from and written to.</summary>
    public required string ConfigPath { get; init; }

    /// <summary>The registry's checkouts, or why they could not be read.</summary>
    public Func<CancellationToken, Task<ChoicesRegistry>> Registry { get; init; } =
        _ => Task.FromResult(new ChoicesRegistry(null, "this command was handed no way to read it"));

    /// <summary>The moment a standing answer is set, and a workflow's version saved.</summary>
    public Func<DateTimeOffset> Now { get; init; } = () => DateTimeOffset.UtcNow;

    /// <summary>Whether an agents' job holds this process (<see cref="OwnAgentJob.Holds"/>), where a write of the person's roles is refused; a test hands its own.</summary>
    public Func<bool?> InAgentJob { get; init; } = OwnAgentJob.Holds;

    /// <summary>
    /// The folder the terminal is in: where a file a verb names by a relative path is read and written, a cycle's, a
    /// workflow's to apply or import, and an export's.
    /// </summary>
    public string Folder { get; init; } = Directory.GetCurrentDirectory();

    /// <summary>
    /// The size of the Daoris folder's volume in bytes, which a floor's size is judged against; the system's read of the home's
    /// folder by default, a test's stand-in otherwise. Null where it does not read.
    /// </summary>
    public Func<long?>? FloorVolume { get; init; }

    /// <summary>
    /// The verbs the subdomains above the settings own (<see cref="ChoiceVerb"/>), wired by Loop (<c>DriverChoiceVerbs</c>); a verb's word
    /// no verb here carries is no verb of the world's.
    /// </summary>
    public IReadOnlyList<ChoiceVerb> Verbs { get; init; } = [];
}
