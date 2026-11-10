using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// Following a row registered as a folder: its kind is read from its root at each follow. A folder has no line to register from, so its
/// row keeps what its add said; a folder that has become a git checkout is registered as <c>git</c>, with the declaration its row holds,
/// and is read from its line from the next follow. A row registered as a repository, or that never said, is never taken here: a root
/// that stops being a checkout is held on the line's path.
/// </summary>
public static class FolderFollow
{
    /// <summary>The outcome of a folder's row; null where the row is a repository's and follows its line.</summary>
    public static async Task<RegistrationFollowed?> OneAsync(IRegistrationWorld world, RegistrationRow row, CancellationToken ct)
    {
        if (PlaceKind.Of(row.Kind) != PlaceKind.Folder || row.Root is not { } root) return null;
        var name = row.Repository;
        if (PlaceKind.At(root) == PlaceKind.Folder)
        {
            return new(name, RegistryOutcome.Folder,
                $"`{name}` is a folder, worked in place: it has no line to register from, and its row keeps what its add said.");
        }

        var (ok, message) = await world.RegisterAsync(Body(row, world.LocalService), ct).ConfigureAwait(false);
        return ok
            ? new(name, RegistryOutcome.Registered,
                $"`{name}` is a git checkout now: it is registered as a repository, and read from its line from the next follow.", Sent: true)
            : new(name, RegistryOutcome.Refused, $"the service refused `{name}` as a repository: {message} Its row keeps what it held.");
    }

    /// <summary>The row's own declaration with the kind <c>git</c>: a registration replaces the declaration, so it carries the row's.</summary>
    private static JsonObject Body(RegistrationRow row, bool local)
    {
        var body = new JsonObject
        {
            ["repository"] = row.Repository,
            ["adopted"] = row.Adopted,
            ["packs"] = Strings(row.Packs),
            ["domain"] = new JsonObject
            {
                ["summary"] = row.Summary ?? "",
                ["owns"] = Strings(row.Owns),
                ["accepts"] = Strings(row.Accepts),
                ["uses"] = Strings(row.Uses),
            },
            ["join"] = row.Joined,
            ["shareKnowledge"] = row.SharesKnowledge,
            ["kind"] = PlaceKind.Git,
        };
        if (local) body["root"] = row.Root;
        return body;
    }

    private static JsonArray Strings(IEnumerable<string> values) => [.. values.Select(value => (JsonNode)JsonValue.Create(value))];
}
