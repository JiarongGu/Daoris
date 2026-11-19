using System.Text.Json.Nodes;
using static Daoris.Driver.AccountIdJson;

namespace Daoris.Driver;

/// <summary>
/// <c>harnesses.json</c>: the default each agent and workspace names, the orders, the account kept for conversations, and the fold
/// receipts not yet said, in the file's section order (<see cref="HarnessSettingsFile"/>). Every other key stays as written.
/// </summary>
internal sealed class AccountIdWiringStore : IAccountIdStore
{
    public IEnumerable<AccountIdReference> Moved(AccountIdHome at, IReadOnlyList<AccountIdRename> renames, bool write) =>
        File(at, DaorisHome.Entry(at.Home, HomeEntry.Harnesses), write, (root, place) =>
        {
            var found = new List<AccountIdReference>();
            void Said(AccountIdRename rename, string where) => found.Add(new AccountIdReference(rename, place, where));

            foreach (var rename in renames)
            {
                Value(root["defaults"] as JsonObject, rename, "defaults", Said);
                foreach (var (workspace, agents) in Objects(root["workspaces"])) Value(agents, rename, $"workspaces.{workspace}", Said);
                Orders(root["rotation"] as JsonObject, rename, "rotation", Said);
                foreach (var (workspace, agents) in Objects(root["workspaceRotation"])) Orders(agents, rename, $"workspaceRotation.{workspace}", Said);
                Keep(root["rotationUse"] as JsonObject, rename, "rotationUse", Said);
                foreach (var (workspace, agents) in Objects(root["workspaceRotationUse"])) Keep(agents, rename, $"workspaceRotationUse.{workspace}", Said);
                Pooled(root["pooled"] as JsonObject, rename, Said);
            }

            return found;
        });

    private static IEnumerable<(string Key, JsonObject Held)> Objects(JsonNode? node) =>
        node is JsonObject held ? [.. held.Where(pair => pair.Value is JsonObject).Select(pair => (pair.Key, pair.Value!.AsObject()))] : [];

    /// <summary>The agent's value under <paramref name="agents"/> where it names the moving id.</summary>
    private static void Value(JsonObject? agents, AccountIdRename rename, string where, Action<AccountIdRename, string> said)
    {
        if (agents is null) return;
        foreach (var agent in Agents(agents, rename.Agent).Where(agent => Names(agents[agent], rename.From)))
        {
            agents[agent] = rename.To;
            said(rename, $"{where}.{agent}");
        }
    }

    /// <summary>Each place in the agent's order under <paramref name="agents"/> that names the moving id.</summary>
    private static void Orders(JsonObject? agents, AccountIdRename rename, string where, Action<AccountIdRename, string> said)
    {
        if (agents is null) return;
        foreach (var agent in Agents(agents, rename.Agent)) List(agents[agent] as JsonArray, rename, $"{where}.{agent}", said);
    }

    private static void List(JsonArray? order, AccountIdRename rename, string where, Action<AccountIdRename, string> said)
    {
        for (var at = 0; order is not null && at < order.Count; at++)
        {
            if (!Names(order[at], rename.From)) continue;
            order[at] = rename.To;
            said(rename, $"{where}[{at}]");
        }
    }

    /// <summary>The account the agent's use settings keep for conversations, where it is the moving id.</summary>
    private static void Keep(JsonObject? agents, AccountIdRename rename, string where, Action<AccountIdRename, string> said)
    {
        if (agents is null) return;
        foreach (var agent in Agents(agents, rename.Agent))
        {
            if (agents[agent] is JsonObject use && Names(use["keep"], rename.From))
            {
                use["keep"] = rename.To;
                said(rename, $"{where}.{agent}.keep");
            }
        }
    }

    /// <summary>Each fold receipt's order, first moved, joined and order-alone account (<see cref="PoolReceipts"/>).</summary>
    private static void Pooled(JsonObject? agents, AccountIdRename rename, Action<AccountIdRename, string> said)
    {
        if (agents is null) return;
        foreach (var agent in Agents(agents, rename.Agent))
        {
            if (agents[agent] is not JsonArray receipts) continue;
            for (var at = 0; at < receipts.Count; at++)
            {
                if (receipts[at] is not JsonObject receipt) continue;
                var where = $"pooled.{agent}[{at}]";
                for (var each = 0; receipt["scopes"] is JsonArray scopes && each < scopes.Count; each++)
                {
                    if (scopes[each] is not JsonObject scope) continue;
                    foreach (var field in new[] { "default", "begins" })
                    {
                        if (!Names(scope[field], rename.From)) continue;
                        scope[field] = rename.To;
                        said(rename, $"{where}.scopes[{each}].{field}");
                    }

                    List(scope["list"] as JsonArray, rename, $"{where}.scopes[{each}].list", said);
                }

                List(receipt["order"] as JsonArray, rename, $"{where}.order", said);
                foreach (var part in new[] { "moved", "joined" })
                {
                    if (receipt[part] is not JsonArray items) continue;
                    for (var each = 0; each < items.Count; each++)
                    {
                        if (items[each] is not JsonObject item || !Names(item["account"], rename.From)) continue;
                        item["account"] = rename.To;
                        said(rename, $"{where}.{part}[{each}].account");
                    }
                }

                if (Names(receipt["inOrder"], rename.From))
                {
                    receipt["inOrder"] = rename.To;
                    said(rename, $"{where}.inOrder");
                }
            }
        }
    }
}
