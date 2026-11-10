namespace Daoris.Driver;

/// <summary>
/// What a conversation holds of its focus: the place its last turn ran under, the one chosen and not yet applied, and each it
/// held, first to last, none twice.
/// </summary>
public sealed record HelpFoci(HelpFocus Focus, HelpFocus? FocusNext, IReadOnlyList<HelpFocus> Foci)
{
    /// <summary>
    /// A conversation's focus as its file keeps it beside its home workspace. A file from before keeps none, which reads as its
    /// home's (<see cref="HelpFocus.Home"/>), and the foci as that alone.
    /// </summary>
    public static HelpFoci Of(HelpKept kept, string? home)
    {
        var focus = kept.Focus ?? HelpFocus.Home(home);
        return new(focus, kept.FocusNext, kept.Foci is { Count: > 0 } foci ? foci : [focus]);
    }

    /// <summary>
    /// Whether a conversation is about <paramref name="place"/>, the history's <c>about</c>: its home is that place or inside it,
    /// or it held a focus there or inside it at any time.
    /// </summary>
    public static bool About(HelpFocus place, string? home, HelpKept kept) =>
        place.Holds(HelpFocus.OfWorkspace(string.IsNullOrWhiteSpace(home) ? RemoteTarget.DefaultWorkspace : home))
        || Of(kept, home).Foci.Any(place.Holds);
}
