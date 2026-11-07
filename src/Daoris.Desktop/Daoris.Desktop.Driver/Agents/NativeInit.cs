using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The native door's init frame read once: <c>system</c> of subtype <c>init</c>, whose <c>tools</c>, <c>mcp_servers</c>,
/// <c>skills</c> and <c>plugins</c> are what the harness says it loaded. Read from the frame the capture already parsed
/// (<see cref="NativeFrame"/>), tolerant of a field a harness version leaves out, and never from the settings Daoris handed.
/// </summary>
public static class NativeInit
{
    /// <summary>What the frame says it loaded; null where the frame is no init frame.</summary>
    public static WireLoaded? Read(NativeFrame frame)
    {
        if (!frame.IsObject || frame.Type != "system" || frame.Subtype != "init") return null;
        var root = frame.Root;
        return new WireLoaded(
            Names(root, "tools"),
            [.. Json.Items(root, "mcp_servers")
                .Where(server => Json.NonEmpty(server, "name") is not null)
                .Select(server => new WireServer(Json.NonEmpty(server, "name")!, Json.NonEmpty(server, "status") ?? ""))],
            Names(root, "skills"),
            Names(root, "plugins"));
    }

    // A name is a bare string or an object's `name`, by the harness version.
    private static IReadOnlyList<string> Names(JsonElement root, string field) =>
        !root.TryGetProperty(field, out var list) || list.ValueKind != JsonValueKind.Array
            ? []
            : [.. list.EnumerateArray()
                .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : Json.NonEmpty(item, "name"))
                .OfType<string>().Where(name => name.Length > 0)];
}
