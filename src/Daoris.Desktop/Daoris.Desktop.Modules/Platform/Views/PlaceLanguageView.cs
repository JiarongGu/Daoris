using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// <c>daoris place language</c> as the card shows it (<c>SET_LANGUAGE</c>): the language sessions write to the person in, for a repository
/// or a whole workspace, or cleared. A code the closed table does not hold is refused at the ask in the driver's words.
/// </summary>
public sealed class PlaceLanguageView : IPressView
{
    public string Module => DriverModule.Name;

    public string Route => "SET_LANGUAGE";

    public async Task<PressViewed> ViewAsync(IPipeGroundWorld world, JsonElement? payload, CancellationToken cancellationToken)
    {
        var scoped = await PlaceScope.ReadAsync(world, payload, "a session language", true, cancellationToken).ConfigureAwait(false);
        if (scoped.Refusal is not null) return PlaceScope.Refusal(scoped);

        var spelled = PressPayload.Raw(payload, "language");
        var code = spelled is null ? null : SessionLanguages.Code(spelled);
        if (spelled is not null && code is null) return PressViewed.Refused(SessionLanguages.Refusal(spelled));

        var facts = scoped.Facts();
        if (code is not null) facts["language"] = SessionLanguages.NameOf(code);
        var kind = (scoped.Circle ? "language-workspace-" : "language-") + (code is null ? "clear" : "set");
        return PressViewed.Shown(new PressView(kind, facts, $"daoris place language {scoped.Words} {code ?? "--clear"}"));
    }
}
