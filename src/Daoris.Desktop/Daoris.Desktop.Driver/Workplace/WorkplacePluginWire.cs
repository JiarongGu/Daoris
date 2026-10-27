namespace Daoris.Driver;

/// <summary>
/// The landing's side of the plugin wire (WSR4, D100), as the trees speak it: a land, a hand-on and an occasion's state asks, the moment
/// they reckon by and why a plugin cannot be spoken to. Plugins keeps the wire and its process, so the workplace names only these five,
/// which <see cref="LandingPlugins"/> implements directly.
/// </summary>
public interface IWorkplacePluginWire
{
    /// <summary>The moment an occasion reckons by: whether an entry was asked in the last minute, and when an answer was asked.</summary>
    DateTimeOffset Now { get; }

    /// <summary>Why <paramref name="plugin"/> cannot land work here now, or null (<see cref="LandingRules.PluginProblem"/>).</summary>
    string? Problem(string plugin);

    /// <summary>Speak the one frame and hear the answer — or the sentence saying why there is none.</summary>
    Task<PluginLanding> LandAsync(string plugin, LandingFrame frame, CancellationToken ct = default);

    /// <summary>The same frame for a branch a landing made earlier, handed on after it (WSR5b): the machine log says it was a hand-off.</summary>
    Task<PluginLanding> HandAsync(string plugin, LandingFrame frame, CancellationToken ct = default);

    /// <summary>An occasion's asks at <see cref="HookPoints.State"/> (PLUGHOOK1a): one answer per ask, in the order asked, and never a throw for a plugin's sake.</summary>
    Task<IReadOnlyList<StateAnswer>> AskStatesAsync(IReadOnlyList<StateAsk> asks, CancellationToken ct = default);
}
