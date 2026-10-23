using System.Globalization;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// What one agent, at one version, last offered on one account (D205 §9.2, MODELROLE1c2): the model and effort values its open listed,
/// and the models it refused there, each with when it was read. An account the tool's own home holds is <see cref="AgentOffers.OwnHome"/>.
/// </summary>
/// <param name="Models">The <c>model</c> category's values its last open listed; null where it listed none (a door with no model option).</param>
/// <param name="Efforts">The effort category's values, as <paramref name="Models"/>.</param>
/// <param name="Refused">The models it refused on that account at an open, by the value asked.</param>
public sealed record OfferReading(
    string Agent,
    string? Version,
    string Account,
    DateTimeOffset At,
    IReadOnlyList<string>? Models,
    IReadOnlyList<string>? Efforts,
    IReadOnlyList<string> Refused)
{
    /// <summary>When it last refused a model there: what a refusal's day is counted from, apart from the opens that list again.</summary>
    public DateTimeOffset? RefusedAt { get; init; }
}

/// <summary>
/// <c>data/agent-offers.json</c> (D205 §9.2, MODELROLE1c2): what each agent, version and account last offered in <c>model</c> and
/// <c>thought_level</c>, and each account's <c>refused</c> readings, written at every protocol <c>session/new</c> and every refusal at an
/// open, read by the walk (<see cref="Judge"/>) and by the editor as hints. A reading, not a setting: never synced, taken by a clear of the
/// home (D195's inventory).
/// </summary>
/// <remarks>
/// <para><b>Keyed per account</b>, since each account is its own home and what an agent offers is its account's (§3.3):
/// <c>{ "schemaVersion": 1, "readings": { "&lt;agent&gt;|&lt;account&gt;": { … } } }</c>, merged per key under the file's
/// <see cref="KeepLock"/> by every process that writes it (the loop, conversations, the host's consoles), each write one hold from its read
/// to its rename. A reading from an older version never replaces a newer one.</para>
/// <para><b>Judged per account</b> (§3.3): a reading passes an account only while its version is the start's and it is under
/// <see cref="Fresh"/> old. An older or other-version reading is a hint, and the account is tried, its open refreshing the reading, so a
/// negative reading never holds back the opening that would correct it for longer than a day.</para>
/// </remarks>
public static class AgentOffers
{
    /// <summary>The shape this build writes.</summary>
    public const int Schema = 1;

    /// <summary>How long a reading passes an account.</summary>
    public static readonly TimeSpan Fresh = TimeSpan.FromDays(1);

    /// <summary>The key of a start on the tool's own sign-in, which names no account of Daoris's.</summary>
    public const string OwnHome = "";

    /// <summary>The account's reading lacks the model asked.</summary>
    public const string NotOffered = "not-offered";

    /// <summary>The account refused the model asked at an open.</summary>
    public const string RefusedCode = "refused";

    /// <summary>Where the record is: the home table's <c>agent-offers</c> entry.</summary>
    public static string PathOf(string home) => DaorisHome.Entry(home, HomeEntry.AgentOffers);

    /// <summary>Every reading kept; none where there is no record, it does not read, or its <c>schemaVersion</c> is newer.</summary>
    public static IReadOnlyList<OfferReading> Read(string home)
    {
        var path = PathOf(home);
        try
        {
            if (!File.Exists(path)) return [];
            return Parse(AtomicFile.ReadText(path)) is { Newer: false } record ? [.. record.Readings.Values] : [];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>
    /// Whether <paramref name="account"/>'s reading passes it for <paramref name="model"/> in a start on <paramref name="agent"/> at
    /// <paramref name="version"/>: where that account refused the model at an open, <see cref="NotOffered"/> if its list lacks it and
    /// <see cref="RefusedCode"/> if the list offered it; null where the account is tried (account default, no reading, a stale or
    /// other-version one, or no refusal of that model).
    /// </summary>
    /// <remarks>
    /// A list that lacks a model is never a pass by itself (MODELROLE1a): Claude's protocol door lists aliases and takes a full id the list
    /// does not name, answering its alias, so only the agent's own refusal at an open passes an account.
    /// </remarks>
    public static string? Judge(
        IReadOnlyList<OfferReading> readings, string agent, string? version, string? account, string? model, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(model)) return null;
        var key = account ?? OwnHome;
        var reading = readings.FirstOrDefault(each =>
            string.Equals(each.Agent, agent, StringComparison.OrdinalIgnoreCase) && string.Equals(each.Account, key, StringComparison.OrdinalIgnoreCase));
        if (reading is null || !string.Equals(reading.Version, version, StringComparison.Ordinal)) return null;
        if (reading.RefusedAt is not { } refusedAt || now - refusedAt >= Fresh) return null;
        if (!reading.Refused.Contains(model, StringComparer.OrdinalIgnoreCase)) return null;
        return reading.Models?.Contains(model, StringComparer.OrdinalIgnoreCase) == true ? RefusedCode : NotOffered;
    }

    /// <summary>
    /// An open's reading (§9.2, at every protocol <c>session/new</c>): what it listed, replacing the account's last list; the models it
    /// refused at this version stay refused until their day is out, since an open that lists them again has not tried them. Null where
    /// written; else why not.
    /// </summary>
    public static string? Offered(
        string home, string agent, string? version, string? account, IReadOnlyList<string>? models, IReadOnlyList<string>? efforts,
        DateTimeOffset now) =>
        Edit(home, agent, account, version, now, held =>
        {
            var same = held is not null && held.Version == version;
            return new OfferReading(agent, version, account ?? OwnHome, now, models, efforts, same ? held!.Refused : [])
            {
                RefusedAt = same ? held!.RefusedAt : null,
            };
        });

    /// <summary>
    /// A refusal at an open (§3.3): <paramref name="model"/> kept as the account's <c>refused</c> reading, before the refused record
    /// ends, so the next look judges it. Null where written; else why not.
    /// </summary>
    public static string? Refused(string home, string agent, string? version, string? account, string model, DateTimeOffset now) =>
        Edit(home, agent, account, version, now, held =>
        {
            var same = held is not null && held.Version == version && held.RefusedAt is { } at && now - at < Fresh;
            List<string> refused = same ? [.. held!.Refused] : [];
            if (!refused.Contains(model, StringComparer.OrdinalIgnoreCase)) refused.Add(model);
            var listed = held is not null && held.Version == version;
            return new OfferReading(agent, version, account ?? OwnHome, now, listed ? held!.Models : null, listed ? held!.Efforts : null, refused)
            {
                RefusedAt = now,
            };
        });

    private static string Key(string agent, string? account) => $"{agent.ToLowerInvariant()}|{(account ?? OwnHome).ToLowerInvariant()}";

    /// <summary>One write: the record read under its lock, the account's reading made, and written over the file as it stands.</summary>
    private static string? Edit(
        string home, string agent, string? account, string? version, DateTimeOffset now, Func<OfferReading?, OfferReading> change)
    {
        var path = PathOf(home);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var held = KeepLock.Take(path);
            Parsed? record = null;
            if (File.Exists(path))
            {
                record = Parse(AtomicFile.ReadText(path));
                if (record is { Newer: true }) return $"agent-offers.json says schemaVersion {record.SchemaVersion}, newer than this build's {Schema}, so it was left as it is";
            }

            var readings = new Dictionary<string, OfferReading>(record?.Readings ?? [], StringComparer.Ordinal);
            var key = Key(agent, account);
            var before = readings.GetValueOrDefault(key);
            // A reading from an older version never replaces a newer one (round 2, Codex 8).
            if (before is not null && Older(version, before.Version)) return null;
            readings[key] = change(before);
            AtomicFile.WriteText(path, Text(readings));
            HomeWrites.Mine(path);
            return null;
        }
        catch (Exception error) when (error is DriverException or IOException or UnauthorizedAccessException)
        {
            return error.Message;
        }
    }

    /// <summary>Whether <paramref name="version"/> is older than <paramref name="than"/>, where both read as versions.</summary>
    private static bool Older(string? version, string? than) =>
        Version.TryParse(Numeric(version), out var a) && Version.TryParse(Numeric(than), out var b) && a < b;

    private static string? Numeric(string? version) =>
        version is null ? null : new string([.. version.TakeWhile(c => char.IsDigit(c) || c == '.')]).Trim('.');

    private static string Moment(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    private static string Text(IReadOnlyDictionary<string, OfferReading> readings) => JsonFile.Serialize(writer =>
    {
        writer.WriteStartObject();
        writer.WriteNumber("schemaVersion", Schema);
        writer.WriteStartObject("readings");
        foreach (var (key, reading) in readings.OrderBy(each => each.Key, StringComparer.Ordinal))
        {
            writer.WriteStartObject(key);
            writer.WriteString("agent", reading.Agent);
            writer.WriteString("account", reading.Account);
            if (reading.Version is not null) writer.WriteString("version", reading.Version);
            writer.WriteString("at", Moment(reading.At));
            if (reading.RefusedAt is { } refusedAt) writer.WriteString("refusedAt", Moment(refusedAt));
            List("models", reading.Models);
            List("efforts", reading.Efforts);
            List("refused", reading.Refused);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
        writer.WriteEndObject();

        void List(string name, IReadOnlyList<string>? values)
        {
            if (values is null) return;
            writer.WriteStartArray(name);
            foreach (var value in values) writer.WriteStringValue(value);
            writer.WriteEndArray();
        }
    });

    private static Parsed? Parse(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            string? schemaVersion = null;
            var newer = false;
            if (root.TryGetProperty("schemaVersion", out var version))
            {
                schemaVersion = version.GetRawText();
                newer = version.ValueKind == JsonValueKind.Number && (!version.TryGetInt32(out var n) || n > Schema);
            }

            var readings = new Dictionary<string, OfferReading>(StringComparer.Ordinal);
            if (root.TryGetProperty("readings", out var map) && map.ValueKind == JsonValueKind.Object)
            {
                foreach (var entry in map.EnumerateObject())
                {
                    if (ReadingOf(entry.Value) is { } reading) readings[entry.Name] = reading;
                }
            }

            return new(schemaVersion, newer, readings);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static OfferReading? ReadingOf(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object || Text("agent") is not { } agent
            || Text("at") is not { } at
            || !DateTimeOffset.TryParse(at, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var when))
        {
            return null;
        }

        DateTimeOffset? refusedAt = Text("refusedAt") is { } refused
            && DateTimeOffset.TryParse(refused, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var refusedWhen)
                ? refusedWhen
                : null;
        return new(agent, Text("version"), Text("account") ?? OwnHome, when, Values("models"), Values("efforts"), Values("refused") ?? [])
        {
            RefusedAt = refusedAt,
        };

        string? Text(string name) => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

        IReadOnlyList<string>? Values(string name) =>
            element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
                ? [.. value.EnumerateArray().Where(each => each.ValueKind == JsonValueKind.String).Select(each => each.GetString()!)]
                : null;
    }

    private sealed record Parsed(string? SchemaVersion, bool Newer, Dictionary<string, OfferReading> Readings);
}
