using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>What a grant did: the key it wrote under, whether the file moved, and whether a re-read says yes.</summary>
/// <param name="Key">The project key the flag lives under — the harness's own, where it had one.</param>
/// <param name="Changed">False when the folder was already trusted, and nothing was written.</param>
/// <param name="Verified">The hold's own question, asked again of the file after the write.</param>
public sealed record TrustGrant(string Key, bool Changed, bool Verified);

/// <summary>
/// A start held because the harness has not trusted where it would run (DEPLOY1) — as a fact a screen
/// can act on, beside the sentence a person reads (D73).
/// </summary>
/// <param name="Folder">Where the session would have run: the repository's tree, or an intake's room.</param>
/// <param name="TrustFile">The harness's own file the hold read — the account's, which a grant writes.</param>
/// <param name="Quest">The quest it held, when it was a quest's start.</param>
/// <param name="Ask">The ask it held, when it was an intake's.</param>
public sealed record TrustHold(string Folder, string TrustFile, string? Quest = null, string? Ask = null);

/// <summary>
/// The trust holds the loop's last tick reported — what a screen may offer the person to grant (D73).
/// </summary>
/// <remarks>
/// 🔴 <b>Never wider than the hold.</b> A screen's confirmation grants a folder only while the driver
/// is holding it, and only in the file the driver read: the pair has to be one this machine produced.
/// Anything else is the terminal's to name, where the person types the folder themselves.
/// </remarks>
public sealed class TrustHolds
{
    private volatile IReadOnlyList<TrustHold> _latest = [];

    /// <summary>What the last tick held for trust. Replaced whole: a hold the driver no longer meets is gone.</summary>
    public IReadOnlyList<TrustHold> Latest => _latest;

    public void Record(IReadOnlyList<TrustHold> holds) => _latest = [.. holds];

    /// <summary>The hold for exactly this folder in exactly this file, or null.</summary>
    public TrustHold? Holding(string folder, string trustFile) =>
        _latest.FirstOrDefault(hold =>
            string.Equals(hold.Folder, folder, StringComparison.Ordinal)
            && string.Equals(hold.TrustFile, trustFile, StringComparison.Ordinal));
}

/// <summary>
/// Whether the harness has been told it may work in a tree (DEPLOY1) — and, on the person's word, the
/// telling (D73).
/// </summary>
/// <remarks>
/// <para>🔴 <b>The step that is the person's.</b> Claude Code ignores a repository's
/// <c>permissions.allow</c> until a person has accepted that path — *"this workspace has not been
/// trusted"* — so a driven session there does the work, cannot reach its connector, and cannot take
/// or close the quest it exists to serve. Measured: three real runs, nine minutes and a real login
/// each, all recorded `failed` for exactly this. Both doors: the protocol door ignores an untrusted
/// room's allow-list too (`docs/2026-09-24-deploy1-acp-trust-evidence.md`).</para>
///
/// <para><b>It gates driving only where Daoris's own rules do not reach the session</b> (D73). The rules
/// handed over at spawn (PERM1) are honoured untrusted on both doors, measured, so a session carried the
/// `connector` default is never held here; trust then decides only whether a repository's OWN
/// allow-list counts.</para>
///
/// <para><b>Asked, then written — never silently</b> (D73). That flag <em>is</em> the person's grant.
/// The driver only ever READS it and refuses on the answer, which turns a silent nine-minute failure
/// into an immediate sentence. <see cref="Grant"/> writes it, and is reached only by a person's
/// explicit act naming the folder: the terminal's <c>daoris agent trust … --yes</c>, or the screen's
/// confirmation of a hold the driver is showing. Nothing writes it at adoption, sync or spawn.</para>
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

    /// <summary>
    /// The key a folder the harness never recorded is written under: forward slashes, as Claude Code
    /// writes its own entries today (the older backslash form is still read, and kept where it exists).
    /// </summary>
    public static string Key(string folder) =>
        folder.Replace('\\', '/').TrimEnd('/');

    /// <summary>
    /// Set <c>hasTrustDialogAccepted</c> for <paramref name="folder"/> in the harness's own
    /// <paramref name="configPath"/> — and nothing else in that file (D73).
    /// </summary>
    /// <remarks>
    /// <para><b>One flag moves.</b> The file is the harness's: its counters, its other projects and the
    /// entry's own history are read and written back meaning exactly what they meant. An existing
    /// entry for the folder, in any spelling <see cref="Accepted"/> matches, is updated IN PLACE under
    /// the key the harness wrote; a new one takes <see cref="Key"/>.</para>
    ///
    /// <para><b>A file this build cannot read is refused, never replaced.</b> For the question, unknown
    /// is permissive; for a write it cannot be, because our guess at somebody else's file would
    /// destroy whatever in it we did not understand.</para>
    ///
    /// <para>🔴 <b>Written beside, then renamed, then read back.</b> The harness rewrites this file
    /// itself, whole, whenever it saves its state. A Claude Code already running under this
    /// configuration home may save a copy it read before this grant and so undo it. That is not
    /// silent: the driver re-reads the file before every start, so a lost grant shows as the same
    /// hold again, naming the same folder. <see cref="TrustGrant.Verified"/> is the re-read at the
    /// moment of writing, not a promise about the future.</para>
    /// </remarks>
    public static TrustGrant Grant(string configPath, string folder)
    {
        string? original = File.Exists(configPath) ? File.ReadAllText(configPath) : null;

        JsonObject root;
        if (original is null)
        {
            root = new JsonObject();
        }
        else
        {
            try
            {
                root = JsonNode.Parse(original) as JsonObject ?? throw Unreadable(configPath);
            }
            catch (JsonException)
            {
                throw Unreadable(configPath);
            }
        }

        JsonObject projects;
        switch (root["projects"])
        {
            case null:
                projects = new JsonObject();
                root["projects"] = projects;
                break;
            case JsonObject existing:
                projects = existing;
                break;
            default:
                throw Unreadable(configPath);
        }

        var wanted = Normalise(folder);
        var key = projects.Select(project => project.Key)
            .FirstOrDefault(name => Normalise(name).Equals(wanted, StringComparison.OrdinalIgnoreCase));

        if (key is not null)
        {
            if (projects[key] is not JsonObject known) throw Unreadable(configPath);
            if (known["hasTrustDialogAccepted"] is JsonValue flag && flag.GetValueKind() == JsonValueKind.True)
            {
                return new TrustGrant(key, Changed: false, Verified: Accepted(configPath, folder) == true);
            }

            known["hasTrustDialogAccepted"] = true;
        }
        else
        {
            key = Key(folder);
            projects[key] = new JsonObject { ["hasTrustDialogAccepted"] = true };
        }

        var text = root.ToJsonString(Written);
        if (original is null || original.EndsWith('\n')) text += "\n";
        WriteBeside(configPath, text);

        return new TrustGrant(key, Changed: true, Verified: Accepted(configPath, folder) == true);
    }

    /// <summary>
    /// The harness's own formatting, as near as a re-serialisation comes: two-space indent, LF, and
    /// text left as text rather than escaped — a folder named in any script stays readable.
    /// </summary>
    private static readonly JsonSerializerOptions Written = new()
    {
        WriteIndented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static void WriteBeside(string path, string text)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        var beside = Path.Combine(directory, $"{Path.GetFileName(path)}.daoris-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(beside, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(beside, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(beside)) File.Delete(beside);
        }
    }

    private static DriverException Unreadable(string configPath) =>
        new($"`{configPath}` is not a file this build can read, so nothing was written to it — accept "
            + "the trust prompt by running `claude` in that folder instead.");

    /// <summary>The sentence a person can act on, naming the path, the fix, and what it costs.</summary>
    /// <param name="tree">The folder the session would have run in.</param>
    /// <param name="profile">
    /// The named account the hold read, if any — so the command it names grants THAT account, not the
    /// machine's default, and the same hold does not come straight back.
    /// </param>
    public static string Refusal(string tree, string? profile = null) =>
        $"`{tree}` has never been trusted by this agent on this machine, so it ignores the "
        + "repository's own `permissions.allow` — a session here can do the work but cannot take or "
        + "close its quest. Run `claude` in that directory once and accept the trust prompt, or "
        + $"`daoris agent trust claude-code \"{tree}\"{(profile is null ? "" : $" --profile {ShellWord.Of(profile, ShellWord.Account)}")} --yes`. "
        + "Daoris sets that flag only when you tell it to: it is your grant to give.";
}
