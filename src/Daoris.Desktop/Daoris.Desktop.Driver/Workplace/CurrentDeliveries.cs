using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Daoris.Driver;

/// <summary>
/// The delivery a door writes for Current's <c>land</c> (LANDNODE1c3; the land node design §3, §4): every door of a session's tree under
/// Current, the review's Accept, a terminal's <c>trees land</c> and the look's landing at done alike, writes it once the landing is judged and
/// lands through it, so a press and a pass land one delivery once. Its intent is Current's graph read from the live rule (D157 point 6): the
/// method its <c>land</c> names, the tree's line as target and line, the branch the rule names, today's message, the rule's plugin and its
/// line's policy epoch. A unit's earlier delivery is settled by phase first (<see cref="DeliveryReconciler.BeforeStartAsync"/>), so a terminal
/// with no loop runs the same fence.
/// </summary>
internal static class CurrentDeliveries
{
    /// <summary>
    /// The delivery the landing goes on with: started, or this run's own of the same key going on; else the landing's refusal; or neither,
    /// where it lands as before: its key delivered already and the landing waited for nothing (an Accept again, read by observation), or the
    /// start's confirmation not settled, when no delivery starts at all (the store's rollback window).
    /// </summary>
    /// <param name="unit">The unit it lands: the session, as Current's landing record names it.</param>
    /// <param name="tip">The tip judged, which the delivery is bound to.</param>
    /// <param name="tree">The branch the session's tree is on, which a merge's message names.</param>
    /// <param name="branch">The branch a branch method makes or moves, as the landing names it; null for a merge.</param>
    /// <param name="waited">Whether the landing waited for another of its tree, so a delivery its key names delivered meanwhile is said so.</param>
    /// <param name="standing">Whether the standing automatic authority still stands, asked just before the first effect.</param>
    public static async Task<(TreeDelivery? Delivery, TreeLanding? Refusal)> StartAsync(
        string home, IWorkplacePluginWire wire, Landing landing, string repository, string workspace, string root, LandingSubject subject, string tip,
        string line, string tree, string? branch, string acceptedBy, bool waited, Func<string?> standing, CancellationToken ct)
    {
        var rule = landing.Rule;
        var (method, graph) = WorkplaceWorkflows.Current.CurrentLand(rule);
        TreeLanding Refused(string why) => new(false, $"Nothing was landed: {why}.") { Refusal = AutoLandingCode.Refused };
        // A name git would not take is refused before anything is written: a delivery's key holds no name, so one written for it would stand in
        // the way of the name that takes, whose landing would read the key settled and land with no delivery.
        if (method != DeliveryMethods.Merge && !(branch is { } named && BranchName.IsValid(named)))
        {
            return (null, Refused(TreeLands.Untaken(branch ?? "", subject.EnteredName is not null).TrimEnd('.')));
        }

        var squash = rule.Squash && method is DeliveryMethods.Branch or DeliveryMethods.PullRequest;
        var intent = new DeliveryIntent(
            subject.Session, repository, tip, method, line, line, method == DeliveryMethods.Merge ? null : branch, null,
            method == DeliveryMethods.Merge ? $"Merge branch '{tree}'" : TextBounds.OneLine(subject.Title is { Length: > 0 } title ? title : subject.Session),
            method == DeliveryMethods.PullRequest ? rule.Plugin : null, IntegrationEpoch.Of((rule.Integrate ?? new LandingIntegrate()).Resolved(), null));
        var options = JsonSerializer.Serialize(new Dictionary<string, object> { ["method"] = method, ["squash"] = squash });
        var authorization = acceptedBy == AcceptedBy.Auto
            ? DeliveryAuthorization.Automatic($"{landing.Source}:{repository}", tip, graph, options, "[]")
            : DeliveryAuthorization.Person($"{AcceptedBy.Person}:{subject.Session}", tip, graph, options, "[]");

        var store = new DeliveryStore(home);
        var reconciler = DeliveryReconciler.Of(store, home, wire, record =>
            string.Equals(record.Intent.Repository, repository, StringComparison.OrdinalIgnoreCase) ? root : DeliveryReconciler.Checkouts(home)(record));
        TreeDelivery Live(DeliveryClaim claim) => new(store, claim) { Live = true, Standing = _ => standing() };
        // Its key settled already: one delivered while this landing waited is said so; else it lands as before, read by observation.
        (TreeDelivery?, TreeLanding?) Observed(DeliveryRecord? found) =>
            (null, waited && found is { Settled: DeliverySettlements.Delivered } ? TreeDelivering.Meanwhile(found) : null);

        try
        {
            // Twice at most: once, and once more after an earlier delivery of the unit is settled.
            for (var attempt = 0; attempt < 2; attempt++)
            {
                var started = store.Start(intent, authorization);
                if (started.Held is not null) return (null, null);
                if (started.Started) return (Live(started.Claim!), null);
                if (started.Refused is null && started.Delivery is { } same)
                {
                    if (same.Unresolved && started.Claim is { } ours) return (Live(ours), null);
                    if (!same.Unresolved) return Observed(same);
                }

                // Its own key under a run that stopped, or another of its unit not settled: settled by phase, before any new start.
                var settled = await reconciler.BeforeStartAsync(intent.Unit, ct).ConfigureAwait(false);
                if (settled is null) continue;
                var mine = string.Equals(settled.Key, intent.Key, StringComparison.Ordinal);
                switch (settled.Standing)
                {
                    case DeliveryStandings.Delivered or DeliveryStandings.Settled:
                        if (mine) return Observed(store.Delivery(intent.Key));
                        continue;
                    case DeliveryStandings.GoesOn when mine && settled.Claim is { } taken:
                        return (Live(taken), null);
                    case DeliveryStandings.GoesOn or DeliveryStandings.Plugin when !mine && settled.Claim is { } earlier:
                        // What it did is certain and a new tip or method lands now: an earlier one that made nothing is withdrawn, one that made
                        // its branch is stopped there, and what it made stays.
                        var nothing = settled.Next == DeliveryPhases.Prepared;
                        store.Settle(earlier, nothing ? DeliverySettlements.Withdrawn : DeliverySettlements.Stopped,
                            $"a new delivery of `{intent.Unit}` by {intent.Method} at {intent.Tip} began" + (nothing ? ", and nothing of this one stood" : ", and what this one made stays"));
                        continue;
                    default:
                        return (null, Refused(settled.Said.TrimEnd('.')));
                }
            }

            return (null, Refused($"`{intent.Unit}` holds another delivery not settled, which a restart's settlement reads first"));
        }
        catch (DeliveryRefusedException refused)
        {
            return (null, Refused($"its delivery {refused.Key[..12]} was not written: {refused.Why}"));
        }
        catch (Exception error) when (error is SqliteException or DesktopStoreException or IOException)
        {
            return (null, Refused($"its delivery could not be written ({error.Message.Trim()}), and no landing is made with none; accept again"));
        }
    }
}
