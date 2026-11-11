namespace Daoris.Driver;

/// <summary>
/// The trial's step for the hand the desktop's start asks first: whether the place's origin may be written, asked through
/// <see cref="PlaceHands"/>, so <c>plugin try</c> and the start agree on a plugin the start would refuse. Only a plugin with a
/// work process per place has the step; with no origin to ask about it says so and passes.
/// </summary>
internal static class PluginTrialHand
{
    public const string Name = "place-hand";

    /// <summary>The step for <paramref name="plugin"/> against <paramref name="origin"/>, or null where the plugin starts per place at none.</summary>
    public static TrialStep? Step(PluginEntry plugin, string home, string? origin)
    {
        if (!PlacedWork.Takes(plugin)) return null;
        if (origin is null) return new(Name, true, "not asked: no origin here to say whether it may be written. Run `try` inside a work checkout to ask it.");

        var hand = PlaceHands.Judge(home, null, null, origin);
        return hand.Refusal is null
            ? new(Name, true, "origin may be written, so the driver starts the plugin's process for this place.")
            : new(Name, false, $"{hand.Refusal}. The driver would refuse the start.");
    }
}
