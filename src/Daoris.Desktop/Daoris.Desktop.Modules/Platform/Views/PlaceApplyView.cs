using System.Globalization;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// <c>daoris place &lt;repository&gt;… --preset &lt;id&gt; … --apply [--accept …]</c> as the card shows it (<c>PLACE_APPLY</c>): the repositories and
/// their workspace, each change the plan makes said once with the repositories it is made at (<c>PLACE_PLAN</c>, the press's own ground,
/// which the Confirm reads again), what the ask carries and each acceptance chosen. A plan its own rules refuse is refused at the ask in
/// their words, so the person never sees it.
/// </summary>
public sealed class PlaceApplyView : IPressView
{
    public string Module => DriverModule.Name;

    public string Route => "PLACE_APPLY";

    public async Task<PressViewed> ViewAsync(IPipeGroundWorld world, JsonElement? payload, CancellationToken cancellationToken)
    {
        if (PressPayload.Text(payload, "repository").Length > 0)
        {
            return PressViewed.Refused("a place's apply names its repositories as `repositories`, never a single `repository`.");
        }

        var ask = new PlaceAsk(PressPayload.Words(payload, "repositories"), PressPayload.Text(payload, "preset"))
        {
            Opinion = Flag(payload, "opinion"),
            Look = Flag(payload, "look"),
            Adoption = Flag(payload, "adoption"),
            Builds = PressPayload.Text(payload, "builds") is { Length: > 0 } builds ? builds : null,
            Drive = Flag(payload, "drive"),
            AtOnce = Number(payload, "atOnce"),
            Pilot = Number(payload, "pilot"),
        };
        var plan = await world.ReadAsync(Module, "PLACE_PLAN", new
        {
            repositories = ask.Repositories, preset = ask.Preset, opinion = ask.Opinion, look = ask.Look, adoption = ask.Adoption,
            builds = ask.Builds, drive = ask.Drive, atOnce = ask.AtOnce, pilot = ask.Pilot,
        }, cancellationToken).ConfigureAwait(false);
        var refusals = PressPayload.Rows(plan, "refusals").Select(row => PressPayload.Field(row, "sentence")).Where(sentence => sentence.Length > 0).ToList();
        if (refusals.Count > 0) return PressViewed.Refused(string.Join(' ', refusals));

        IReadOnlyList<PlaceAccept> accepted = [.. PressPayload.Rows(payload ?? default, "accept")
            .Select(row => new PlaceAccept(PressPayload.Field(row, "repository"), PressPayload.Field(row, "purpose")))];
        var workspaces = PressPayload.Rows(plan, "repositories").Select(row => PressPayload.Field(row, "workspace"))
            .Where(name => name.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase);
        var facts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["repositories"] = string.Join(", ", ask.Repositories), ["workspace"] = string.Join(", ", workspaces), ["preset"] = ask.Preset,
        };
        var command = string.Join(' ', ["daoris", "place", .. PlaceCommand.ApplyArgsOf(ask, accepted)
            .Select(word => word.StartsWith("--", StringComparison.Ordinal) ? word : ShellWord.Of(word, ShellWord.Name))]);
        return PressViewed.Shown(new PressView("place-apply", facts, command, [.. Changes(plan), .. Riders(ask), .. accepted.Select(Accepted)]));
    }

    /// <summary>Each change the plan makes, in its words, with the repositories it is made at; or that it changes nothing now.</summary>
    private static List<PressSaid> Changes(JsonElement plan)
    {
        var changes = PressPayload.Rows(plan, "changes").Select(row =>
        {
            var change = row.TryGetProperty("change", out var made) ? made : default;
            return new PressSaid("place-change", ("said", PressPayload.Field(change, "said")), ("before", PressPayload.Field(change, "before")),
                ("after", PressPayload.Field(change, "after")), ("repositories", string.Join(", ", PressPayload.Rows(row, "repositories").Select(name => name.GetString() ?? ""))));
        }).ToList();
        return changes.Count > 0 ? changes : [new PressSaid("place-nothing")];
    }

    private static IEnumerable<PressSaid> Riders(PlaceAsk ask)
    {
        if (ask.Opinion) yield return new PressSaid("place-opinion");
        if (ask.Look) yield return new PressSaid("place-look");
        if (ask.Adoption) yield return ask.Builds is { } builds ? new PressSaid("place-adoption-builds", ("builds", builds)) : new PressSaid("place-adoption");
        if (ask.Drive) yield return new PressSaid("place-apply-drive");
    }

    private static PressSaid Accepted(PlaceAccept accept) => new("place-accept", ("repository", accept.Repository), ("purpose", accept.Purpose));

    private static bool Flag(JsonElement? payload, string name) => payload is { } body && PressPayload.Flag(body, name);

    private static int? Number(JsonElement? payload, string name) =>
        payload is { ValueKind: JsonValueKind.Object } body && body.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out var number) ? number : null;
}
