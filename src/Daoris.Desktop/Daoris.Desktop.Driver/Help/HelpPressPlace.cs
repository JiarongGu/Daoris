using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The place a press Ask Daoris made named, and whether it lay beyond its conversation's focus at the press
/// (`docs/2026-11-11-ask-scope-ux-design.md` §4): what Notifications' Ask Daoris line and the turn's receipt say of it. The focus
/// fences nothing, so no tier asks because of it.
/// </summary>
public sealed record HelpPressPlace(HelpFocus Place, bool Beyond)
{
    /// <summary>
    /// The place a press's payload named, its <c>workspace</c> and <c>repository</c> as a call's are read (<see cref="HelpBeyond.Place"/>);
    /// null where it named neither, or a repository the registry does not hold.
    /// </summary>
    public static HelpPressPlace? Of(JsonElement? payload, HelpFocus focus, IReadOnlyList<RepoView> registry)
    {
        if (payload is not { ValueKind: JsonValueKind.Object } named) return null;
        var workspace = Json.NonEmpty(named, "workspace")?.Trim();
        var repository = Json.NonEmpty(named, "repository")?.Trim();
        if (workspace is null && repository is null) return null;
        var place = HelpBeyond.Place(new HelpPlaceNote(DateTimeOffset.MinValue, "press", workspace, repository, null), focus, registry);
        return place is null ? null : new(place, HelpBeyond.Outside(focus, place));
    }
}
