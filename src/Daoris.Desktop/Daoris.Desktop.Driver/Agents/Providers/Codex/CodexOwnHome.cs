using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// Codex's own home as the person's own run reads it (<c>CODEX_HOME</c>, else <c>.codex</c> in the profile), handed to a session that
/// runs in its account's home through the one session-only door its adapter keeps: <c>CODEX_CONFIG</c>, a JSON configuration the
/// adapter passes as each thread's configuration, layered over the account's <c>config.toml</c> (codex-acp 2.1.1, <c>dist/index.js</c>,
/// <c>startAcpServer</c> and <c>createSessionConfig</c>). The person's <c>config.toml</c> comes in where the account's sets nothing
/// (<c>own-settings</c>); their <c>AGENTS.md</c> (<c>own-memory</c>) and the folders of their skills and prompts (<c>own-skills</c>) ride
/// its <c>developer_instructions</c>, after the account's own. The person's folder is read, never written.
/// </summary>
/// <remarks>
/// <para><b>The floor</b>: nothing that signs in or picks the provider (the person's sign-in stays theirs, agents rule 28), sets the
/// posture or the sandbox (rules 21-22) or trusts a folder (rule 24) is taken, in a profile either.</para>
/// <para><b>Servers are not handed here</b>: the adapter replaces the configuration's <c>mcp_servers</c> with the open's whenever the
/// open names any, and Daoris's connector always rides it; the person's go in the open (<see cref="CodexOwnServers"/>).</para>
/// </remarks>
internal static class CodexOwnHome
{
    /// <summary>The variable the adapter reads its session configuration from.</summary>
    public const string ConfigVariable = "CODEX_CONFIG";

    private const string HomeVariable = "CODEX_HOME";

    private const string Told = "developer_instructions";

    private static readonly string[] Never =
    [
        "model_provider", "model_providers", "preferred_auth_method", "forced_login_method", "forced_chatgpt_workspace_id", "chatgpt_base_url",
        "cli_auth_credentials_store", "mcp_oauth_credentials_store", "approval_policy", "sandbox_mode", "sandbox_workspace_write", "projects",
        "mcp_servers",
    ];

    /// <summary>The home the person's own run reads.</summary>
    public static string Place(OwnTools tools) =>
        tools.Environment(HomeVariable) is { Length: > 0 } named ? named : Path.Combine(tools.UserHome, ".codex");

    /// <summary>The session configuration the person's home hands, or null where it hands nothing.</summary>
    public static JsonObject? Config(OwnTools tools)
    {
        var own = Place(tools);
        if (tools.Families == OwnFamilies.None || !Directory.Exists(own) || InOwnHome(tools)) return null;

        var account = tools.Session(HomeVariable) is { Length: > 0 } home ? Read(Path.Combine(home, "config.toml")) : null;
        var config = new JsonObject();
        if (tools.Families.HasFlag(OwnFamilies.Settings) && Read(Path.Combine(own, "config.toml")) is { } person)
        {
            Floor(person);
            foreach (var profile in (person["profiles"] as JsonObject)?.Select(entry => entry.Value).OfType<JsonObject>() ?? []) Floor(profile);
            Beneath(config, person, account);
        }

        // The account's own instructions first, which this value replaces; then the person's, which come in only where it has none.
        var accountTold = account?[Told] is JsonValue accounts && accounts.TryGetValue<string>(out var said) ? said : null;
        List<string> told = accountTold is null ? [] : [accountTold];
        if (config[Told] is JsonValue persons && persons.TryGetValue<string>(out var written)) told.Add(written);
        if (tools.Families.HasFlag(OwnFamilies.Memory) && Text(Path.Combine(own, "AGENTS.md")) is { } memory) told.Add(memory);
        if (tools.Families.HasFlag(OwnFamilies.Skills)) told.AddRange(Folders(own));
        config.Remove(Told);
        if (told.Count > (accountTold is null ? 0 : 1)) config[Told] = string.Join("\n\n", told);
        return config.Count > 0 ? config : null;
    }

    /// <summary>Whether the session runs in the person's own home, whose program reads all of it by itself.</summary>
    public static bool InOwnHome(OwnTools tools) => Same(tools.Session(HomeVariable) ?? Path.Combine(tools.UserHome, ".codex"), Place(tools));

    private static IEnumerable<string> Folders(string own)
    {
        var skills = Path.Combine(own, "skills");
        if (Directory.Exists(skills))
        {
            yield return $"Your own skills are in `{skills}`, a folder each: its SKILL.md says what it does and when it applies; read it before that work.";
        }

        var prompts = Path.Combine(own, "prompts");
        if (Directory.Exists(prompts)) yield return $"Your own prompts are in `{prompts}`, a file each, for when the person names one.";
    }

    private static void Floor(JsonObject table)
    {
        foreach (var key in Never) table.Remove(key);
    }

    /// <summary>The person's entries into <paramref name="target"/> where the account's own (<paramref name="above"/>) sets none.</summary>
    private static void Beneath(JsonObject target, JsonObject person, JsonObject? above)
    {
        foreach (var (key, value) in person.ToList())
        {
            if (value is null) continue;
            var set = above?[key];
            if (value is JsonObject table && set is null or JsonObject)
            {
                var into = new JsonObject();
                Beneath(into, table, set as JsonObject);
                if (into.Count > 0) target[key] = into;
            }
            else if (set is null && !(above?.ContainsKey(key) ?? false))
            {
                target[key] = value.DeepClone();
            }
        }
    }

    private static JsonObject? Read(string path)
    {
        try
        {
            return File.Exists(path) ? TomlReader.Read(File.ReadAllText(path)) : null;
        }
        catch (Exception problem) when (problem is IOException or UnauthorizedAccessException or FormatException)
        {
            // A file their own run cannot read configures nothing there either.
            return null;
        }
    }

    private static string? Text(string path)
    {
        try
        {
            return File.Exists(path) && File.ReadAllText(path).Trim() is { Length: > 0 } text ? text : null;
        }
        catch (Exception problem) when (problem is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool Same(string one, string other) => string.Equals(
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(one)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(other)),
        StringComparison.OrdinalIgnoreCase);
}
