namespace Daoris.Driver;

/// <summary>
/// One of this machine's settings, a value of <c>driver.json</c> the person sets: its key, the one name each door spells, and the
/// field the file keeps it under.
/// </summary>
/// <param name="Key">
/// The setting's one name: the terminal's verb (<c>daoris settings &lt;key&gt;</c>), the window's route (<see cref="Route"/>) and the
/// page's words (<see cref="Words"/>).
/// </param>
/// <param name="File">The field of <c>driver.json</c> that holds it.</param>
/// <param name="Card">The Settings domain whose card draws its row in its own words, or null where another card draws it in its own.</param>
/// <param name="Least">The least whole number it takes, where it is one.</param>
/// <param name="Most">The most it takes, where it has a most.</param>
public sealed record MachineSetting(string Key, string File, string? Card, int? Least = null, int? Most = null)
{
    /// <summary>The window's route that sets it.</summary>
    public string Route => $"SET_{Key.ToUpperInvariant()}";

    /// <summary>The prefix of its words in the page's catalogue: <c>.label</c>, <c>.terminal</c> and <c>.body</c> beneath it.</summary>
    public string Words => $"settings.{Key}";
}

/// <summary>
/// Every machine setting, declared once: the terminal, Ask Daoris, the window's routes and the page are held to this list by
/// the settings census, and the page reads it as <c>SETTINGS</c>, written from it by <c>tools/contracts.mjs</c>. A setting that gains
/// a door is spelled by its key there; one listed twice, or a door under a second name, fails the census.
/// </summary>
public static class MachineSettings
{
    /// <summary>The Settings domain of the driver's dials.</summary>
    public const string Driver = "driver";

    /// <summary>Each setting, in the order the Driver domain draws its rows.</summary>
    public static IReadOnlyList<MachineSetting> All { get; } =
    [
        new("notify", "notify", Card: Driver),
        new("cooloff", "cooloff", Card: Driver, Least: 1),
        // Less would mark a session thinking through a long step; more would leave a stuck one unseen for hours.
        new("quiet", "quietMinutes", Card: Driver, Least: 5, Most: 120),
        new("cap", "cap", Card: Driver, Least: 1),
        // Its kinds are the Disk space card's, worded there.
        new("keep", "keep", Card: null),
    ];

    /// <summary>The setting of a key, or null where none is.</summary>
    public static MachineSetting? Of(string key) => All.FirstOrDefault(setting => string.Equals(setting.Key, key, StringComparison.Ordinal));
}
