using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The place a press Ask Daoris made named, whether it lay beyond its conversation's focus at the press, and that focus
/// (`docs/2026-11-11-ask-scope-ux-design.md` §4): what Notifications' Ask Daoris line, the turn's receipt and a restricted press's card
/// say of it. The focus fences nothing, so no tier asks because of it.
/// </summary>
public sealed record HelpPressPlace(HelpFocus Place, bool Beyond, HelpFocus? Focus = null)
{
    /// <summary>
    /// The place a press's payload named, its <c>workspace</c> and <c>repository</c> as a call's are read (<see cref="HelpBeyond.Place"/>),
    /// or, for a press naming only a quest, the repository that quest asks (<paramref name="questTo"/>); null where it named neither, or
    /// a repository the registry does not hold.
    /// </summary>
    public static HelpPressPlace? Of(JsonElement? payload, HelpFocus focus, IReadOnlyList<RepoView> registry, string? questTo = null)
    {
        if (payload is not { ValueKind: JsonValueKind.Object } named) return null;
        var workspace = Json.NonEmpty(named, "workspace")?.Trim();
        var repository = Json.NonEmpty(named, "repository")?.Trim();
        if (workspace is null && repository is null) repository = QuestOnly(payload) is null ? null : questTo?.Trim();
        if (workspace is null && repository is null) return null;
        var place = HelpBeyond.Place(new HelpPlaceNote(DateTimeOffset.MinValue, "press", workspace, repository, null), focus, registry);
        return place is null ? null : new(place, HelpBeyond.Outside(focus, place), focus);
    }

    /// <summary>The quest a press names where it names no workspace or repository, whose repository is then its place; else null.</summary>
    public static string? QuestOnly(JsonElement? payload) =>
        payload is { ValueKind: JsonValueKind.Object } named
        && Json.NonEmpty(named, "workspace") is null && Json.NonEmpty(named, "repository") is null
            ? Json.NonEmpty(named, "quest")?.Trim()
            : null;
}
