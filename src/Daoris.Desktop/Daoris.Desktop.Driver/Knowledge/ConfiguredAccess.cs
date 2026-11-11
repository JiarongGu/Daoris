using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// Restricted mode's configured access as the person chose it: the home's <c>configured-access.json</c>, the families of acts granted
/// (<see cref="ActFamilies"/>) and the checkouts turned off. The browser's grant is its plugin's approval (<see cref="HelpGrants"/>),
/// never kept here.
/// </summary>
/// <remarks>
/// <para><b>Read at every start and at every press</b> (the trap of a grant read once and reused): each start of Ask Daoris composes
/// its room from it, and each press of Ask Daoris's is judged by the families it grants now (<see cref="ActFamilies.Covers"/>), so a
/// revoke puts the next press on a card.</para>
///
/// <para><b>Absence is none chosen</b>, each grant on its own: no file, one that does not read, a grant that is no list. A code this
/// build does not act on grants nothing, and is never dropped from the file by a reader.</para>
///
/// <para><b>The checkouts are the one grant on by default</b> (on, inherited), the readable
/// set within the conversation's workspace. So the file keeps the person's narrowing, the checkouts turned off by repository
/// (<c>checkouts-off</c>), and absence is every readable checkout on; the set handed is computed at each start from the readable
/// set, never kept here.</para>
/// </remarks>
public sealed record ConfiguredAccess
{
    /// <summary>The checkouts' grant, the page's <c>HelpGrantId</c>: its operations are the repositories, each on or off.</summary>
    public const string CheckoutsGrant = "checkouts";

    /// <summary>The acts' grant, the page's <c>HelpGrantId</c>: its operations are the families of acts (<see cref="ActFamilies"/>).</summary>
    public const string ActsGrant = "acts";

    /// <summary>The families of acts chosen, in <see cref="ActFamilies.Built"/>'s order; a press of one runs with no card, read at the press.</summary>
    public IReadOnlyList<string> Acts { get; init; } = [];

    /// <summary>Where the file keeps the checkouts turned off: a list of repository names.</summary>
    private const string CheckoutsOffKey = "checkouts-off";

    /// <summary>
    /// The checkouts the person turned off, by repository, sorted without case and once each; none by default, when every checkout
    /// that agents outside may read is on.
    /// </summary>
    public IReadOnlyList<string> CheckoutsOff { get; init; } = [];

    /// <summary>Whether the person left <paramref name="repository"/>'s checkout on: not turned off, its name matched without case.</summary>
    public bool Reads(string repository) => !CheckoutsOff.Contains(repository.Trim(), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// What the record keeps of one grant by its name: the families chosen, and for the checkouts, on by default, those turned off
    /// (<see cref="CheckoutsOff"/>); none for a grant not kept here. What a running conversation's start is compared with.
    /// </summary>
    public IReadOnlyList<string> Of(string grant) => grant switch
    {
        CheckoutsGrant => CheckoutsOff,
        ActsGrant => Acts,
        _ => [],
    };

    /// <summary>Whether <paramref name="other"/> turned off the same checkouts; the acts are read at each press, so are left out.</summary>
    public bool Same(ConfiguredAccess other) => CheckoutsOff.SequenceEqual(other.CheckoutsOff, StringComparer.OrdinalIgnoreCase);

    public static readonly ConfiguredAccess None = new();

    /// <summary>Where the record is kept: the home's <see cref="HomeEntry.ConfiguredAccess"/>.</summary>
    public static string PathOf(string home) => DaorisHome.Entry(home, HomeEntry.ConfiguredAccess);

    // One writer at a time in this process: two presses from two windows would otherwise each drop the other's choice.
    private static readonly object Gate = new();

    /// <summary>
    /// The person's choice of one operation (<c>SET_HELP_GRANT</c>): that grant's list edited alone, made onto the file
    /// as it reads now, so another grant, a code this build does not act on and a key it does not know stay as written; the list kept
    /// in the page's order. Answers the record after it.
    /// </summary>
    /// <exception cref="DriverException">
    /// A grant or an operation this build does not act on, or a file that does not read, which is left as it is.
    /// </exception>
    public static ConfiguredAccess Choose(string home, string grant, string operation, bool chosen)
    {
        if (grant is not (CheckoutsGrant or ActsGrant))
        {
            throw new DriverException(grant switch
            {
                "browser" => "The browser's grant is its plugin's approval, made on the plugin's own page; nothing is chosen for it here.",
                "housekeeping" or "settings" => $"Housekeeping and the chosen settings are families of acts: grant `{ActFamilies.CleanUp}`, "
                                                + $"`{ActFamilies.Discard}` or `{ActFamilies.Settings}` under `{ActsGrant}`.",
                _ => $"`{grant}` is not built yet as a grant of Ask Daoris's configured access; only `{CheckoutsGrant}` and `{ActsGrant}` are chosen here.",
            });
        }

        if (grant == CheckoutsGrant) return ChooseCheckout(home, operation, chosen);

        var built = ActFamilies.Built;
        if (!built.Contains(operation))
        {
            throw new DriverException($"`{operation}` is no family of acts Ask Daoris may be granted: they are {string.Join(", ", built.Select(each => $"`{each}`"))}.");
        }

        lock (Gate)
        {
            var path = PathOf(home);
            var file = Load(path);
            var listed = Listed(file, grant);
            listed.Remove(operation);
            if (chosen) listed.Add(operation);
            var ordered = listed.Distinct(StringComparer.Ordinal)
                .OrderBy(each => built.Contains(each) ? built.ToList().IndexOf(each) : built.Count).ToList();
            file[grant] = new JsonArray([.. ordered.Select(each => (JsonNode)JsonValue.Create(each))]);
            JsonFile.Write(path, file);
        }

        return Read(home);
    }

    /// <summary>
    /// A checkout turned on again or off by its repository's name: the list of those turned off edited alone, sorted
    /// without case, the other grants and keys kept as written. Any name of one word is taken: one no checkout bears narrows nothing.
    /// </summary>
    private static ConfiguredAccess ChooseCheckout(string home, string repository, bool chosen)
    {
        var name = repository.Trim();
        if (name.Length == 0 || name.Any(c => char.IsWhiteSpace(c) || char.IsControl(c) || c is '/' or '\\'))
        {
            throw new DriverException($"`{repository}` is no repository's name: a checkout is turned on or off by its repository's registered name.");
        }

        lock (Gate)
        {
            var path = PathOf(home);
            var file = Load(path);
            var off = Listed(file, CheckoutsOffKey);
            off.RemoveAll(each => string.Equals(each, name, StringComparison.OrdinalIgnoreCase));
            if (!chosen) off.Add(name);
            file[CheckoutsOffKey] = new JsonArray([.. off.Order(StringComparer.OrdinalIgnoreCase).Select(each => (JsonNode)JsonValue.Create(each))]);
            JsonFile.Write(path, file);
        }

        return Read(home);
    }

    /// <summary>The file as it reads now, or a new one; one that does not read is left as it is, saying so.</summary>
    private static JsonObject Load(string path)
    {
        try
        {
            return File.Exists(path)
                ? JsonNode.Parse(AtomicFile.ReadText(path)) as JsonObject ?? throw new DriverException($"`{path}` holds no object, so it was left as it is.")
                : [];
        }
        catch (JsonException error)
        {
            throw new DriverException($"`{path}` does not read ({error.Message}), so it was left as it is.");
        }
    }

    /// <summary>The strings one key of the file lists, in its order; none where it is no list.</summary>
    private static List<string> Listed(JsonObject file, string key) =>
        file[key] is JsonArray array
            ? array.Select(each => each is JsonValue value && value.TryGetValue<string>(out var text) ? text : null).OfType<string>().ToList()
            : [];

    /// <summary>
    /// The record as the home holds it now, each grant read on its own (acts that are no list do not hide the checkouts turned off);
    /// <see cref="None"/> where it holds none or it does not read.
    /// </summary>
    public static ConfiguredAccess Read(string home)
    {
        try
        {
            var path = PathOf(home);
            if (!File.Exists(path)) return None;
            using var document = JsonDocument.Parse(AtomicFile.ReadText(path));
            if (document.RootElement.ValueKind != JsonValueKind.Object) return None;
            var root = document.RootElement;
            return new ConfiguredAccess { Acts = Chosen(root, ActsGrant, ActFamilies.Built), CheckoutsOff = Off(root) };
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

    /// <summary>The checkouts turned off, trimmed, once each without case, sorted so; none where the key is no list.</summary>
    private static IReadOnlyList<string> Off(JsonElement root)
    {
        if (!root.TryGetProperty(CheckoutsOffKey, out var off) || off.ValueKind != JsonValueKind.Array) return [];
        return [.. off.EnumerateArray().Where(each => each.ValueKind == JsonValueKind.String).Select(each => each.GetString()!.Trim())
            .Where(each => each.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase)];
    }
}
