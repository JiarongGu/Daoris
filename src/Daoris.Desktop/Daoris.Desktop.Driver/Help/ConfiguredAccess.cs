using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// Restricted mode's configured access as the person chose it (HELPMODE1c, D183 point 1; the Ask Daoris modes design §4.1): the
/// home's <c>configured-access.json</c>, the operations chosen per grant by the page's codes. Housekeeping is the grant built
/// first (HELPMODE1c2a), the chosen settings next (HELPMODE1c3); the browser's grant is its plugin's approval
/// (<see cref="HelpGrants"/>), never kept here.
/// </summary>
/// <remarks>
/// <para><b>Read at every start and at every call</b> (the design's trap of a grant read once and reused): each start of Ask Daoris
/// composes its allow-list, its receipt and its room from it, and each housekeeping or settings request is judged by it again
/// (<see cref="HelpHousekeeping"/>, <see cref="HelpSettings"/>), so a revoke refuses the next call.</para>
///
/// <para><b>Absence is none chosen</b>, each grant on its own: no file, one that does not read, a grant that is no list. A code this
/// build does not act on (dropping recovery refs, the mode) grants nothing, and is never dropped from the file by a reader.</para>
/// </remarks>
/// <param name="Housekeeping">The housekeeping operations chosen, in <see cref="HousekeepingBuilt"/>'s order, once each.</param>
public sealed record ConfiguredAccess(IReadOnlyList<string> Housekeeping)
{
    /// <summary>The grant's name in the file, the page's <c>HelpGrantId</c>.</summary>
    public const string HousekeepingGrant = "housekeeping";

    /// <summary>The chosen settings' grant's name in the file, the page's <c>HelpGrantId</c> (HELPMODE1c3).</summary>
    public const string SettingsGrant = "settings";

    /// <summary>Whether a session parking, or ending unasked, is said: <c>daoris driver notify</c>.</summary>
    public const string Notify = "notify";

    /// <summary>A repository's or a workspace's session language: <c>daoris driver language</c>.</summary>
    public const string Language = "language";

    /// <summary>How long one session may run: <c>daoris driver timeout</c>.</summary>
    public const string Timeout = "timeout";

    /// <summary>How many sessions run at once on this machine: <c>daoris driver cap</c>.</summary>
    public const string Cap = "cap";

    /// <summary>How long an account whose limit names no time cools: <c>daoris driver cooloff</c>.</summary>
    public const string CoolOff = "cooloff";

    /// <summary>
    /// The settings this build changes under the grant, in the page's order: the five ordinary ones, and never the mode, the grants,
    /// accounts or a safeguard (the design's §4.1 table).
    /// </summary>
    public static readonly IReadOnlyList<string> SettingsBuilt = [Notify, Language, Timeout, Cap, CoolOff];

    /// <summary>The settings chosen, in <see cref="SettingsBuilt"/>'s order, once each; none by default.</summary>
    public IReadOnlyList<string> Settings { get; init; } = [];

    /// <summary>The connector's tool for a chosen setting, as it is named on the wire: <c>settings_cooloff</c>.</summary>
    public static string SettingToolOf(string setting) => $"settings_{setting}";

    /// <summary>The tools of the settings chosen, in their order.</summary>
    public IReadOnlyList<string> SettingsTools => [.. Settings.Select(SettingToolOf)];

    /// <summary>The operations chosen of one grant by its name; none for a grant not kept here.</summary>
    public IReadOnlyList<string> Of(string grant) => grant switch
    {
        HousekeepingGrant => Housekeeping,
        SettingsGrant => Settings,
        _ => [],
    };

    /// <summary>Whether <paramref name="other"/> chose the same operations of every grant, in the same order (ACCESSSWAP1's comparison).</summary>
    public bool Same(ConfiguredAccess other) =>
        Housekeeping.SequenceEqual(other.Housekeeping, StringComparer.Ordinal) && Settings.SequenceEqual(other.Settings, StringComparer.Ordinal);

    /// <summary>A look at what the cleanup would remove and what it keeps.</summary>
    public const string Inspect = "inspect";

    /// <summary>Removing the session and landed branches named, with their trees, where the cleanup's judges clear them.</summary>
    public const string CleanUp = "clean-up";

    /// <summary>Discarding one session branch by name, unforced.</summary>
    public const string DiscardBranch = "discard-branch";

    /// <summary>The housekeeping operations this build acts on, in the page's order; the page's <c>drop-recovery</c> is not yet one.</summary>
    public static readonly IReadOnlyList<string> HousekeepingBuilt = [Inspect, CleanUp, DiscardBranch];

    public static readonly ConfiguredAccess None = new([]);

    /// <summary>Where the record is kept: the home's <see cref="HomeEntry.ConfiguredAccess"/>.</summary>
    public static string PathOf(string home) => DaorisHome.Entry(home, HomeEntry.ConfiguredAccess);

    /// <summary>The connector's tool for a housekeeping operation, as it is named on the wire: <c>housekeeping_clean_up</c>.</summary>
    public static string ToolOf(string operation) => $"housekeeping_{operation.Replace('-', '_')}";

    /// <summary>The tools of the operations chosen, in their order.</summary>
    public IReadOnlyList<string> HousekeepingTools => [.. Housekeeping.Select(ToolOf)];

    /// <summary>Every housekeeping code the page draws, in its order: the built ones, then dropping recovery refs, not built.</summary>
    private static readonly string[] HousekeepingDrawn = [Inspect, CleanUp, DiscardBranch, "drop-recovery"];

    // One writer at a time in this process: two presses from two windows would otherwise each drop the other's choice.
    private static readonly object Gate = new();

    /// <summary>
    /// The person's choice of one operation (HELPMODE1c2b, <c>SET_HELP_GRANT</c>): that grant's list edited alone, made onto the file
    /// as it reads now, so another grant, a code this build does not act on and a key it does not know stay as written; the list kept
    /// in the page's order. Answers the record after it.
    /// </summary>
    /// <exception cref="DriverException">
    /// A grant or an operation this build does not act on, or a file that does not read, which is left as it is.
    /// </exception>
    public static ConfiguredAccess Choose(string home, string grant, string operation, bool chosen)
    {
        if (grant is not (HousekeepingGrant or SettingsGrant))
        {
            throw new DriverException(grant == "browser"
                ? "The browser's grant is its plugin's approval, made on the plugin's own page; nothing is chosen for it here."
                : $"`{grant}` is not built yet as a grant of Ask Daoris's configured access; only `{HousekeepingGrant}` and `{SettingsGrant}` "
                  + "are chosen here.");
        }

        var (built, drawn) = grant == SettingsGrant ? (SettingsBuilt, SettingsBuilt.ToArray()) : (HousekeepingBuilt, HousekeepingDrawn);
        if (!built.Contains(operation))
        {
            var named = string.Join(", ", built.Select(each => $"`{each}`"));
            throw new DriverException(drawn.Contains(operation)
                ? $"`{operation}` is not built yet, so it cannot be granted: {grant} grants {named}."
                : grant == SettingsGrant
                    ? $"`{operation}` is no setting Ask Daoris may be granted: they are {named}."
                    : $"`{operation}` is no housekeeping operation: they are {named}.");
        }

        lock (Gate)
        {
            var path = PathOf(home);
            JsonObject file;
            try
            {
                file = File.Exists(path)
                    ? JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? throw new DriverException($"`{path}` holds no object, so it was left as it is.")
                    : [];
            }
            catch (JsonException error)
            {
                throw new DriverException($"`{path}` does not read ({error.Message}), so it was left as it is.");
            }

            var listed = file[grant] is JsonArray array
                ? array.Select(each => each is JsonValue value && value.TryGetValue<string>(out var text) ? text : null).OfType<string>().ToList()
                : [];
            listed.Remove(operation);
            if (chosen) listed.Add(operation);
            var ordered = listed.Distinct(StringComparer.Ordinal)
                .OrderBy(each => Array.IndexOf(drawn, each) < 0 ? drawn.Length : Array.IndexOf(drawn, each)).ToList();
            file[grant] = new JsonArray([.. ordered.Select(each => (JsonNode)JsonValue.Create(each))]);
            JsonFile.Write(path, file);
        }

        return Read(home);
    }

    /// <summary>
    /// The record as the home holds it now, each grant read on its own (HELPMODE1c3: a housekeeping that is no list no longer hides
    /// the settings chosen); <see cref="None"/> where it holds none or it does not read.
    /// </summary>
    public static ConfiguredAccess Read(string home)
    {
        try
        {
            var path = PathOf(home);
            if (!File.Exists(path)) return None;
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.ValueKind != JsonValueKind.Object) return None;
            var root = document.RootElement;
            return new ConfiguredAccess(Chosen(root, HousekeepingGrant, HousekeepingBuilt)) { Settings = Chosen(root, SettingsGrant, SettingsBuilt) };
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return None;
        }
    }

    /// <summary>One grant's operations chosen, those of <paramref name="built"/> alone, in its order; none where the grant is no list.</summary>
    private static IReadOnlyList<string> Chosen(JsonElement root, string grant, IReadOnlyList<string> built)
    {
        if (!root.TryGetProperty(grant, out var chosen) || chosen.ValueKind != JsonValueKind.Array) return [];
        var named = chosen.EnumerateArray().Where(each => each.ValueKind == JsonValueKind.String).Select(each => each.GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        return [.. built.Where(named.Contains)];
    }
}
