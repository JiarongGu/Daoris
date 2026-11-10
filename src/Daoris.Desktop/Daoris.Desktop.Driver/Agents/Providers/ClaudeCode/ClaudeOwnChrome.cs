using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The person's Claude in Chrome switch: <c>/chrome</c>'s enabled by default, which their own run keeps in its global file as
/// <c>claudeInChromeDefaultEnabled</c>. A session's account home holds its own copy of that file, so the person's yes reaches the
/// session as <c>--chrome</c>, the flag their own run reads the same way. The browser and its extension are the person's own.
/// </summary>
/// <remarks>
/// The native door is handed the flag; the protocol door's adapter starts <c>claude</c> itself, so it rides the Agent SDK's
/// <c>extraArgs</c> in the open's options (<see cref="ClaudeProtocolDialect"/>).
/// </remarks>
internal static class ClaudeOwnChrome
{
    /// <summary>The flag that turns Claude in Chrome on for one run.</summary>
    public const string Flag = "--chrome";

    private const string Key = "claudeInChromeDefaultEnabled";

    /// <summary>Whether the person left the family on and their own run turns Claude in Chrome on.</summary>
    public static bool On(OwnTools tools)
    {
        if (!tools.Families.HasFlag(OwnFamilies.Chrome) || ClaudeOwnHome.InOwnHome(tools)) return false;
        try
        {
            var file = ClaudeOwnHome.GlobalFile(tools);
            if (!File.Exists(file)) return false;
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            return document.RootElement.ValueKind == JsonValueKind.Object
                   && document.RootElement.TryGetProperty(Key, out var enabled) && enabled.ValueKind == JsonValueKind.True;
        }
        catch (Exception problem) when (problem is IOException or UnauthorizedAccessException or JsonException)
        {
            // A file their own run cannot read turns nothing on there either.
            return false;
        }
    }
}
