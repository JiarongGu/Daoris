namespace Daoris.Driver;

/// <summary>
/// The names both doors and <see cref="GitWiring"/> share (GITAUTH1b, the git-auth design §4.1-§4.3): the home variable, the grant
/// variable, the rewrite's prefix and settings, and the command scope's reading and writing. The base library alone: the doors
/// compile it in (<c>Daoris.Desktop.GitDoor</c>).
/// </summary>
public static class GitRouting
{
    /// <summary>The Daoris home both doors act for, written by <c>GitWiring.Hand</c> over any inherited value.</summary>
    public const string HomeVariable = "DAORIS_GIT_HOME";

    /// <summary>The landing's grant, handed to one plugin process alone (§6.7; <see cref="GitGrants"/>).</summary>
    public const string GrantVariable = "DAORIS_GIT_GRANT";

    /// <summary>The prefix the rewrite puts before every HTTP and HTTPS address, which <c>GitRemotes.AddressOf</c> takes off.</summary>
    public const string Prefix = "daoris::";

    /// <summary>
    /// The command scope's settings that send every HTTP and HTTPS act to the remote helper (§4.1): fetch and push addresses alike,
    /// and a submodule's recursive fetch, which git allows an unknown protocol only from the user.
    /// </summary>
    public static readonly IReadOnlyList<(string Key, string Value)> Rewrites =
    [
        ("url." + Prefix + "https://.insteadOf", "https://"),
        ("url." + Prefix + "http://.insteadOf", "http://"),
        ("protocol.daoris.allow", "always"),
    ];

    /// <summary>
    /// git's shell characters (<c>run-command.c</c>'s <c>prepare_shell_cmd</c>, as read): a command holding any of them is run
    /// through <c>sh -c</c>. <c>GitDoorsProcessTests</c> measures a bare path with none started directly.
    /// </summary>
    public const string ShellCharacters = "|&;<>()$`\\\"' \t\n*?[#~=%";

    /// <summary>Append settings to the command scope (<c>GIT_CONFIG_COUNT</c>), after any the environment already holds.</summary>
    public static void Scope(IDictionary<string, string?> environment, IEnumerable<(string Key, string Value)> settings)
    {
        var count = environment.TryGetValue("GIT_CONFIG_COUNT", out var held) && int.TryParse(held, out var number) && number > 0 ? number : 0;
        foreach (var (key, value) in settings)
        {
            environment[$"GIT_CONFIG_KEY_{count}"] = key;
            environment[$"GIT_CONFIG_VALUE_{count}"] = value;
            count++;
        }

        environment["GIT_CONFIG_COUNT"] = count.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Take Daoris's routing out of an environment (§4.3): the remote helper starts git's own transport so, and nothing it starts comes
    /// back through the door. Every other command-scope setting is kept, renumbered.
    /// </summary>
    public static void Unroute(IDictionary<string, string?> environment)
    {
        var count = environment.TryGetValue("GIT_CONFIG_COUNT", out var held) && int.TryParse(held, out var number) && number > 0 ? number : 0;
        var kept = new List<(string Key, string Value)>();
        for (var index = 0; index < count; index++)
        {
            environment.TryGetValue($"GIT_CONFIG_KEY_{index}", out var key);
            environment.TryGetValue($"GIT_CONFIG_VALUE_{index}", out var value);
            environment.Remove($"GIT_CONFIG_KEY_{index}");
            environment.Remove($"GIT_CONFIG_VALUE_{index}");
            if (key is null || Rewrites.Any(rewrite => string.Equals(rewrite.Key, key, StringComparison.OrdinalIgnoreCase))) continue;
            kept.Add((key, value ?? ""));
        }

        environment.Remove("GIT_CONFIG_COUNT");
        if (kept.Count > 0) Scope(environment, kept);
    }
}
