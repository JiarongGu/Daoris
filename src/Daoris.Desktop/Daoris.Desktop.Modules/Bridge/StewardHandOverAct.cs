using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// A quest's own session handed over to another agent, on an account of it the steward may name: the person's
/// <c>SESSION_HAND_OVER { id, agent[, account] }</c> run inside the role's door, so the session's line says the steward asked. Its
/// route's judge stands: what it refuses is refused with its code and its words, nothing recorded, and outside the acts the person
/// granted that judge refuses the press before any card is raised. A hand-over is itself a release of its quest's hold, so one of a
/// quest held by what only the owner releases is refused <c>owner-held</c>.
/// </summary>
public sealed class StewardHandOverAct : IRoleAct
{
    private const string Route = "SESSION_HAND_OVER";

    public string Name => StewardActNames.HandOver;

    public string? PersonRoute => Route;

    public string TargetKind => OwnerKinds.Session;

    public bool IsForm(JsonElement form) =>
        StewardActRows.Exact(form, Fields(account: false)) || StewardActRows.Exact(form, Fields(account: true));

    private static Dictionary<string, Func<JsonElement, bool>> Fields(bool account)
    {
        var fields = new Dictionary<string, Func<JsonElement, bool>>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = StewardActRows.Named, ["agent"] = StewardActRows.Named,
        };
        if (account) fields["account"] = StewardActRows.Named;
        return fields;
    }

    public string Target(JsonElement form) => StewardActRows.Text(form, "id");

    public async Task<string?> UnderGatesAsync(RoleActRun run, CancellationToken ct)
    {
        if (run.World is not { } world) return null;
        if (await world.SessionAsync(Target(run.Form), ct).ConfigureAwait(false) is not { } record || !StewardActRows.QuestSession(record))
        {
            return StewardWhy.NotQuestSession;
        }

        return await StewardTargets.OwnerHeldAsync(run, record, ct).ConfigureAwait(false) ? StewardWhy.OwnerHeld : null;
    }

    public async Task<object?> RunAsync(RoleActRun run, CancellationToken ct)
    {
        if (run.Route is not { } dispatch) throw new RoleActRefused(StewardWhy.NoRoute);
        var answered = await dispatch(Route, run.Form, ct).ConfigureAwait(false);
        if (!answered.TryGetProperty("success", out var success) || success.ValueKind != JsonValueKind.True)
        {
            var error = answered.TryGetProperty("error", out var held) ? held : default;
            throw new RoleActRefused(Text(error, "code") ?? StewardWhy.Refused, Text(error, "message"));
        }

        var data = answered.TryGetProperty("data", out var said) ? said : default;
        if (data.ValueKind != JsonValueKind.Object || !data.TryGetProperty("kept", out var kept) || kept.ValueKind != JsonValueKind.True)
        {
            throw new RoleActRefused(Text(data, "why") ?? StewardWhy.Refused, Text(data, "message"));
        }

        var id = Target(run.Form);
        var record = run.World is { } world ? await world.SessionAsync(id, ct).ConfigureAwait(false) : null;
        run.Judge.StewardDid(run.Id, Name, record?.Repository, record?.Quest, id);
        return new { Act = Name, Session = id, Agent = StewardActRows.Text(run.Form, "agent"), Message = Text(data, "message") };
    }

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
