using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// What the door said it loaded, kept on the receipt of the start it said it for: the init
/// frame's tools, servers with their status, skills and plugins, shown beside what the start handed so the person sees where the two
/// differ. Never what the start handed, which <see cref="HelpWired.Compose"/> records from the spawn's inputs: the two are kept apart so a
/// disagreement stays visible.
/// </summary>
public static class HelpLoaded
{
    /// <summary>
    /// The sink part for an Ask Daoris run's start: the first init frame of the run is kept on the newest receipt of <paramref name="session"/>
    /// that has none. A conversation with no receipt (a repository's chat) keeps nothing.
    /// </summary>
    public static IReadingSink Part(string home, string session) =>
        ReadingSinks.On<WireReading.Loaded>(said =>
        {
            if (said.Replayed) return;
            new HelpWired(home).Amend(session, receipt => receipt.Loaded is null ? receipt with { Loaded = said.Init } : receipt);
        });

    /// <summary>The <c>loaded</c> field as the file and the bridge's answer keep it; a null where the door said none.</summary>
    internal static void Write(Utf8JsonWriter writer, WireLoaded? loaded)
    {
        if (loaded is null)
        {
            writer.WriteNull("loaded");
            return;
        }

        writer.WriteStartObject("loaded");
        Strings(writer, "tools", loaded.Tools);
        writer.WriteStartArray("servers");
        foreach (var server in loaded.Servers)
        {
            writer.WriteStartObject();
            writer.WriteString("name", server.Name);
            writer.WriteString("status", server.Status);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        Strings(writer, "skills", loaded.Skills);
        Strings(writer, "plugins", loaded.Plugins);
        writer.WriteEndObject();
    }

    /// <summary>The <c>loaded</c> field of a receipt as kept; null for a start recorded before it, or one whose door said none.</summary>
    internal static WireLoaded? Read(JsonElement receipt) =>
        receipt.TryGetProperty("loaded", out var loaded) && loaded.ValueKind == JsonValueKind.Object
            ? new WireLoaded(
                Json.Strings(loaded, "tools"),
                [.. Json.Items(loaded, "servers")
                    .Where(server => Json.NonEmpty(server, "name") is not null)
                    .Select(server => new WireServer(Json.NonEmpty(server, "name")!, Json.Text(server, "status") ?? ""))],
                Json.Strings(loaded, "skills"),
                Json.Strings(loaded, "plugins"))
            : null;

    private static void Strings(Utf8JsonWriter writer, string name, IEnumerable<string> values)
    {
        writer.WriteStartArray(name);
        foreach (var value in values) writer.WriteStringValue(value);
        writer.WriteEndArray();
    }
}
