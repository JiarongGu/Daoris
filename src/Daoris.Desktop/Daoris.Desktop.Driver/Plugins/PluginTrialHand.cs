namespace Daoris.Driver;

/// <summary>
/// The trial's step for the hand the desktop's start asks first: the API token of the identity serving origin, asked through
/// <see cref="PluginHostToken"/>, so <c>plugin try</c> and the start agree on a plugin the start would refuse. Only a plugin that takes
/// the token has the step; with no origin to ask about it says so and passes.
/// </summary>
internal static class PluginTrialHand
{
    public const string Name = "host-token";

    /// <summary>The step for <paramref name="plugin"/> against <paramref name="origin"/>, or null where the plugin takes no token.</summary>
    public static TrialStep? Step(PluginEntry plugin, string home, string? origin)
    {
        if (!PlacedWork.Takes(plugin)) return null;
        if (origin is null) return new(Name, true, "not asked: no origin here to say which identity serves it. Run `try` inside a work checkout to ask it.");

        var hand = PluginHostToken.Hand(home, null, null, origin);
        if (hand.Refusal is null) return new(Name, true, "the identity serving origin holds an API token, so the driver hands it to the plugin's start.");
        return new(Name, false, hand.NeedsToken is { } identity
            ? $"this plugin's start needs an API token on identity {identity}; set it in Settings → Git. The driver would refuse the start."
            : $"{hand.Refusal}. The driver would refuse the start.");
    }
}
