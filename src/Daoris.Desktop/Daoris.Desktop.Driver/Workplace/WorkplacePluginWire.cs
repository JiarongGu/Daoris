namespace Daoris.Driver;

/// <summary>
/// The trees' side of the plugin wire, as they speak it: an occasion's state asks, the moment they reckon by and why a plugin cannot be
/// spoken to. Plugins keeps the wire and its process, so the workplace names only these, which <see cref="LandingPlugins"/> implements directly.
/// </summary>
public interface IWorkplacePluginWire
{
    /// <summary>The moment an occasion reckons by: whether an entry was asked in the last minute, and when an answer was asked.</summary>
    DateTimeOffset Now { get; }

    /// <summary>Why <paramref name="plugin"/> cannot land work here now, or null (<see cref="LandingPlugins.ProblemOf"/>).</summary>
    string? Problem(string plugin);

    /// <summary>An occasion's asks at <see cref="HookPoints.State"/>: one answer per ask, in the order asked, and never a throw for a plugin's sake.</summary>
    Task<IReadOnlyList<StateAnswer>> AskStatesAsync(IReadOnlyList<StateAsk> asks, CancellationToken ct = default);
}
