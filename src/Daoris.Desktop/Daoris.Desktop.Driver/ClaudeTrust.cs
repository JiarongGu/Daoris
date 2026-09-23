using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// Whether the harness has been told it may work in a tree (DEPLOY1).
/// </summary>
/// <remarks>
/// <para>🔴 <b>The step Daoris cannot take for you.</b> Claude Code ignores a repository's
/// <c>permissions.allow</c> until a person has accepted that path — *"this workspace has not been
/// trusted"* — so a driven session there does the work, cannot reach its connector, and cannot take
/// or close the quest it exists to serve. Measured: three real runs, nine minutes and a real login
/// each, all recorded `failed` for exactly this.</para>
///
/// <para><b>Read, never written.</b> That flag <em>is</em> the person's grant. A tool that set it on
/// their behalf would have removed the only step in the chain that was theirs, so this class answers
/// a question and the driver refuses on the answer — which turns a silent nine-minute failure into
/// an immediate sentence naming the one command that fixes it.</para>
///
/// <para><b>Only a definite NO refuses</b>, the same rule SES3 gives the login question. No file, an
/// unreadable one, or a shape this build does not recognise are all <c>null</c> — unknown, and
/// permissive. The cost of a wrong refusal is higher than the cost of a run.</para>
/// </remarks>
public static class ClaudeTrust
{
    /// <summary>The harness's own config, beside its configuration home.</summary>
    public const string FileName = ".claude.json";

    /// <summary>
    /// <c>true</c> accepted, <c>false</c> definitely not, <c>null</c> unknown.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>A path the harness has never recorded is `false`, not `null`.</b> It prompts on first
    /// visit, so never-recorded means never-accepted and the allow-list is ignored exactly as if the
    /// flag were off. That is the case a real deployment hit; treating it as unknown would have let
    /// the run proceed and fail the way it already did.
    /// </remarks>
    public static bool? Accepted(string configPath, string tree)
    {
        if (!File.Exists(configPath)) return null;

        JsonElement root;
        try
        {
            root = JsonDocument.Parse(File.ReadAllText(configPath)).RootElement;
        }
        catch (JsonException)
        {
            // Somebody else's file, in a shape we do not know. Unknown, not untrusted.
            return null;
        }

        if (!root.TryGetProperty("projects", out var projects)
            || projects.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var wanted = Normalise(tree);
        foreach (var project in projects.EnumerateObject())
        {
            if (!Normalise(project.Name).Equals(wanted, StringComparison.OrdinalIgnoreCase)) continue;

            return project.Value.ValueKind == JsonValueKind.Object
                && project.Value.TryGetProperty("hasTrustDialogAccepted", out var flag)
                && flag.ValueKind == JsonValueKind.True;
        }

        // Recorded nowhere: never visited, therefore never accepted.
        return false;
    }

    /// <summary>
    /// 🔴 Windows spells the same directory several ways — case, separator, a trailing slash — and
    /// all of them name one place. A comparison that missed any would refuse a run the person had
    /// already allowed, which is worse than not checking.
    /// </summary>
    private static string Normalise(string path) =>
        path.Replace('/', '\\').TrimEnd('\\');

    /// <summary>The sentence a person can act on, naming the path, the fix, and what it costs.</summary>
    public static string Refusal(string tree) =>
        $"`{tree}` has never been trusted by this agent on this machine, so it ignores the "
        + "repository's own `permissions.allow` — a session here can do the work but cannot take or "
        + "close its quest. Run `claude` in that directory once and accept the trust prompt. Daoris "
        + "does not set that flag for you: it is your grant to give.";
}
