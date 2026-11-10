using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// <c>daoris agent account use|order|ready</c> as the card shows them: how an agent's accounts are used, the order they are walked in, and a
/// cool-off ended early. Each is refused before any card where the agent is unknown here, or the change is one the route would refuse; a
/// press of another action (the receipts' <c>said</c>) is shown by its values alone.
/// </summary>
public sealed class AccountUseView : IPressView
{
    public string Module => DriverModule.Name;

    public string Route => "ACCOUNT_USE";

    public async Task<PressViewed> ViewAsync(IPipeGroundWorld world, JsonElement? payload, CancellationToken cancellationToken)
    {
        var action = PressPayload.Text(payload, "action");
        if (action is not ("use" or "order" or "ready")) return new PressViewed(null, null);

        var agent = PressPayload.Text(payload, "door");
        var roster = await world.ReadAsync(DriverModule.Name, "HARNESSES", null, cancellationToken).ConfigureAwait(false);
        var door = PressPayload.Rows(roster, "harnesses").FirstOrDefault(row => PressPayload.Field(row, "door") == agent);
        if (door.ValueKind != JsonValueKind.Object)
        {
            var names = string.Join(", ", PressPayload.Rows(roster, "harnesses").Select(row => PressPayload.Field(row, "door")));
            return PressViewed.Refused($"there is no agent `{agent}` on this machine — one of {names}.");
        }

        var owner = PressPayload.Field(door, "accountOf") is { Length: > 0 } of ? of : agent;
        var accounts = PressPayload.Rows(door, "profiles").Select(row => PressPayload.Field(row, "name")).ToList();
        var pool = await world.ReadAsync(DriverModule.Name, "ACCOUNTS", null, cancellationToken).ConfigureAwait(false);
        var set = PressPayload.Rows(pool, "agents").FirstOrDefault(row => PressPayload.Field(row, "agent") == owner);
        return action switch
        {
            "use" => AccountUseJudge.View(payload, owner, set),
            "order" => AccountOrderJudge.View(payload, owner, accounts, set),
            _ => AccountReadyJudge.View(payload, owner, accounts, set),
        };
    }
}
