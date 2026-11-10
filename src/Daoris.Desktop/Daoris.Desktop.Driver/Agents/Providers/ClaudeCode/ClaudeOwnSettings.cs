using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The person's user settings, their tool home's <c>settings.json</c>, merged into a session's rules file beneath everything that
/// outranks them in a run of their own or holds the session's start: Daoris's composed rules, the repository's project settings (shared
/// and local, in the folder the session works in) and the account's own user settings. A list joins the one above it, as the harness
/// joins lists across its tiers, so the person's denies hold beside Daoris's and the harness's deny still wins; a value comes in only
/// where nothing above sets it, so the one command-line tier never raises the person's file over the repository's.
/// </summary>
/// <remarks>
/// <para><b>The floor</b>: nothing that signs in (the person's sign-in stays theirs, agents rule 28), sets the mode (the start's
/// posture, rules 21-22), names plugins (their own family, <see cref="ClaudeOwnPlugins"/>) or switches the hooks off (Daoris's guard is
/// one) is taken, nor a variable the session's process already holds, a Daoris one or any provider's home, key or pin.</para>
/// <para><b>A session shaped for one job</b> takes the person's denies, asks and preferences but none of their allows, extra
/// folders, hooks or project-server approvals, which would widen the shape its job gives it.</para>
/// </remarks>
internal static class ClaudeOwnSettings
{
    private const string FileName = "settings.json";

    private static readonly string[] Never =
    [
        "$schema", "enabledPlugins", "extraKnownMarketplaces", "strictKnownMarketplaces", "apiKeyHelper", "awsAuthRefresh", "awsCredentialExport",
        "gcpAuthRefresh", "forceLoginMethod", "forceLoginOrgUUID", "disableAllHooks", "allowManagedHooksOnly",
    ];

    private static readonly string[] Widening = ["hooks", "enableAllProjectMcpServers", "enabledMcpjsonServers"];

    private static readonly string[] SignInEndings = ["_API_KEY", "_AUTH_TOKEN", "_OAUTH_TOKEN", "_OAUTH_REFRESH_TOKEN"];

    /// <summary>Merge the person's settings into the session's rules <paramref name="file"/> where <paramref name="rules"/> leave the family on.</summary>
    public static void Merge(string file, HandedRules rules)
    {
        if (rules.Own is not { } tools || !tools.Families.HasFlag(OwnFamilies.Settings) || ClaudeOwnHome.InOwnHome(tools)) return;
        if (Read(Path.Combine(ClaudeOwnHome.Place(tools), FileName)) is not { } person) return;
        if (Read(file) is not { } composed) return;

        Floor(person, tools, rules.Shaped);
        JsonObject?[] above =
        [
            rules.Folder is { Length: > 0 } folder ? Read(Path.Combine(folder, ".claude", "settings.local.json")) : null,
            rules.Folder is { Length: > 0 } shared ? Read(Path.Combine(shared, ".claude", FileName)) : null,
            tools.Session("CLAUDE_CONFIG_DIR") is { Length: > 0 } account ? Read(Path.Combine(account, FileName)) : null,
        ];
        Beneath(composed, person, [.. above.OfType<JsonObject>()]);
        JsonFile.Write(file, composed);
    }

    private static void Floor(JsonObject person, OwnTools tools, bool shaped)
    {
        foreach (var key in shaped ? [.. Never, .. Widening] : Never) person.Remove(key);
        if (person["permissions"] is JsonObject permissions)
        {
            permissions.Remove("defaultMode");
            if (shaped)
            {
                permissions.Remove("allow");
                permissions.Remove("additionalDirectories");
            }
        }

        if (person["env"] is JsonObject environment)
        {
            foreach (var name in environment.Select(pair => pair.Key).Where(name => !Kept(name, tools)).ToList()) environment.Remove(name);
        }
    }

    private static bool Kept(string name, OwnTools tools) =>
        tools.Session(name) is null
        && !name.StartsWith("DAORIS_", StringComparison.OrdinalIgnoreCase) && !Spawning.Daoris(name) && !RegisteredVariables.Registered(name)
        && !SignInEndings.Any(ending => name.EndsWith(ending, StringComparison.OrdinalIgnoreCase))
        && !name.StartsWith("CLAUDE_CODE_USE_", StringComparison.OrdinalIgnoreCase)
        && !string.Equals(name, "ANTHROPIC_BASE_URL", StringComparison.OrdinalIgnoreCase);

    /// <summary>The person's entries into <paramref name="target"/>: a list joined, a value only where neither it nor a tier above holds one.</summary>
    private static void Beneath(JsonObject target, JsonObject person, IReadOnlyList<JsonObject> above)
    {
        foreach (var (key, value) in person.ToList())
        {
            if (value is null) continue;
            var held = target[key];
            if (value is JsonArray list)
            {
                if (held is null && list.Count > 0) target[key] = held = new JsonArray();
                if (held is not JsonArray joined) continue;
                foreach (var item in list.Where(item => !joined.Any(kept => JsonNode.DeepEquals(kept, item)))) joined.Add(item?.DeepClone());
            }
            else if (value is JsonObject table)
            {
                if (held is not null and not JsonObject || above.Any(tier => tier[key] is { } set && set is not JsonObject)) continue;
                var into = held as JsonObject ?? new JsonObject();
                Beneath(into, table, [.. above.Select(tier => tier[key]).OfType<JsonObject>()]);
                if (held is null && into.Count > 0) target[key] = into;
            }
            else if (held is null && above.All(tier => !tier.ContainsKey(key)))
            {
                target[key] = value.DeepClone();
            }
        }
    }

    private static JsonObject? Read(string path)
    {
        try
        {
            return File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path)) as JsonObject : null;
        }
        catch (Exception problem) when (problem is IOException or UnauthorizedAccessException or JsonException)
        {
            // A file a run cannot read sets nothing there either: the person's is not merged, a tier above outranks nothing.
            return null;
        }
    }
}
