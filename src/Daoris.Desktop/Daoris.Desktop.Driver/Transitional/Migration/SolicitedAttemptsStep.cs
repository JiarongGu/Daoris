using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The helpers' states and the partners' delivery receipts in the shape this build reads (D232; COMMS1b): every hand goes through the keyed
/// door, so <c>keyed</c> is taken out. An attempt made before that door has no key to be asked again under, so its moment is set past the
/// settlement's horizon, which the next look settles as uncertain for the person, as an unkeyed attempt was, never handing it twice.
/// </summary>
public sealed class SolicitedAttemptsStep : IHomeMigrationStep
{
    public string Name => "solicited";

    public void Run(HomeMigrationRun run)
    {
        Each(run, DaorisHome.Entry(run.Home, HomeEntry.Helpers), "*.state.json", "attempted", _ => true);
        Each(run, DaorisHome.Entry(run.Home, HomeEntry.Partners), "*.delivery.json", "at",
            root => HomeMigrationRun.Text(root["outcome"]) == PartnerDelivery.Attempted);
    }

    private static void Each(HomeMigrationRun run, string folder, string pattern, string moment, Func<JsonObject, bool> attempt)
    {
        if (!Directory.Exists(folder)) return;
        foreach (var path in Directory.EnumerateFiles(folder, pattern).Order(StringComparer.Ordinal))
        {
            run.ChangeJson(path, root =>
            {
                var keyed = root["keyed"] is JsonValue flag && flag.TryGetValue<bool>(out var on) && on;
                var changed = root.Remove("keyed");
                if (keyed || !attempt(root) || Moments.Read(HomeMigrationRun.Text(root[moment])) is not { } at) return changed;
                root[moment] = Moments.Write(at - SolicitedHand.Horizon - TimeSpan.FromDays(1));
                return true;
            });
        }
    }
}
