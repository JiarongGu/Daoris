using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// An account the change itself adds or removes (<see cref="HarnessSettings.Change"/>'s <c>account</c>): a sign-in's or a key's new
/// folder (<paramref name="Added"/>), or a removal's, so the fold before the edit does not say it.
/// </summary>
public sealed record AccountChange(string Agent, string Account, bool Added);

/// <summary>
/// <c>harnesses.json</c>'s <c>pooled</c> section (POOL1a; the account-pool design §6.3): agent → receipts, oldest first. A receipt or
/// a field that does not read is passed over at the read; a save is measured from the section as read, so it is kept on the file.
/// </summary>
internal static class PoolReceipts
{
    /// <summary>A fresh receipt id: random, as an account's is.</summary>
    public static string NewId() => RandomNumberGenerator.GetHexString(12, lowercase: true);

    public static IReadOnlyDictionary<string, IReadOnlyList<PoolReceipt>> Read(JsonElement root)
    {
        var pooled = new Dictionary<string, IReadOnlyList<PoolReceipt>>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty("pooled", out var section) || section.ValueKind != JsonValueKind.Object) return pooled;
        foreach (var agent in section.EnumerateObject())
        {
            if (agent.Value.ValueKind != JsonValueKind.Array) continue;
            List<PoolReceipt> receipts = [.. agent.Value.EnumerateArray().Select(ReceiptOf).OfType<PoolReceipt>()];
            if (receipts.Count > 0) pooled[agent.Name] = receipts;
        }

        return pooled;
    }

    public static void Write(Utf8JsonWriter writer, IReadOnlyDictionary<string, IReadOnlyList<PoolReceipt>> pooled)
    {
        if (!pooled.Any(each => each.Value.Count > 0)) return;
        writer.WriteStartObject("pooled");
        foreach (var (agent, receipts) in pooled.OrderBy(each => each.Key, StringComparer.Ordinal))
        {
            if (receipts.Count == 0) continue;
            writer.WriteStartArray(agent);
            foreach (var receipt in receipts) WriteReceipt(writer, receipt);
            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }

    private static PoolReceipt? ReceiptOf(JsonElement raw)
    {
        if (raw.ValueKind != JsonValueKind.Object || Text(raw, "id") is not { Length: > 0 } id) return null;
        var at = Text(raw, "at") is { } stamp && DateTimeOffset.TryParse(stamp, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var when)
            ? when : DateTimeOffset.UnixEpoch;
        return new PoolReceipt(
            id,
            at,
            [.. Items(raw, "scopes").Select(scope => new FoldedScope(Text(scope, "workspace"), Text(scope, "default"), Names(scope, "list"), Text(scope, "begins")))],
            Names(raw, "order"),
            [.. Items(raw, "moved").Where(each => Text(each, "account") is not null)
                .Select(each => new MovedFirst(Text(each, "workspace"), Text(each, "account")!, Number(each, "place")))],
            [.. Items(raw, "joined").Where(each => Text(each, "account") is not null)
                .Select(each => new PlacedAccount(Text(each, "account")!, Number(each, "place")))],
            Text(raw, "inOrder"),
            [.. Items(raw, "notKept").Where(each => Text(each, "setting") is not null && each.TryGetProperty("value", out _))
                .Select(each => new NotKeptSetting(Text(each, "workspace"), Text(each, "setting")!, each.GetProperty("value").Clone()))],
            Names(raw, "gone"),
            raw.TryGetProperty("said", out var said) && said.ValueKind == JsonValueKind.True);
    }

    private static void WriteReceipt(Utf8JsonWriter writer, PoolReceipt receipt)
    {
        writer.WriteStartObject();
        writer.WriteString("id", receipt.Id);
        writer.WriteString("at", receipt.At.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
        writer.WriteStartArray("scopes");
        foreach (var scope in receipt.Scopes)
        {
            writer.WriteStartObject();
            WriteText(writer, "workspace", scope.Workspace);
            WriteText(writer, "default", scope.Default);
            WriteNames(writer, "list", scope.List);
            WriteText(writer, "begins", scope.Begins);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        WriteNames(writer, "order", receipt.Order);
        writer.WriteStartArray("moved");
        foreach (var moved in receipt.Moved)
        {
            writer.WriteStartObject();
            WriteText(writer, "workspace", moved.Workspace);
            writer.WriteString("account", moved.Account);
            writer.WriteNumber("place", moved.Place);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteStartArray("joined");
        foreach (var joined in receipt.Joined)
        {
            writer.WriteStartObject();
            writer.WriteString("account", joined.Account);
            writer.WriteNumber("place", joined.Place);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        WriteText(writer, "inOrder", receipt.InOrder);
        writer.WriteStartArray("notKept");
        foreach (var setting in receipt.NotKept)
        {
            writer.WriteStartObject();
            WriteText(writer, "workspace", setting.Workspace);
            writer.WriteString("setting", setting.Setting);
            writer.WritePropertyName("value");
            setting.Value.WriteTo(writer);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        WriteNames(writer, "gone", receipt.Gone);
        writer.WriteBoolean("said", receipt.Said);
        writer.WriteEndObject();
    }

    private static string? Text(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static int Number(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : 0;

    private static IEnumerable<JsonElement> Items(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.Object)
            : [];

    private static IReadOnlyList<string> Names(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? [.. value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString()!)]
            : [];

    private static void WriteText(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is null) writer.WriteNull(name);
        else writer.WriteString(name, value);
    }

    private static void WriteNames(Utf8JsonWriter writer, string name, IReadOnlyList<string> names)
    {
        writer.WriteStartArray(name);
        foreach (var each in names) writer.WriteStringValue(each);
        writer.WriteEndArray();
    }
}
