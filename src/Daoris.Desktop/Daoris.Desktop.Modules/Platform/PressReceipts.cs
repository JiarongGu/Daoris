using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// What a press's answer says it made: the <c>receipt</c> a department's creation or a room's apply puts in its data (<see cref="PressReceipt"/>),
/// read back from the text the pipe wrote, so the record of Ask Daoris's presses can say it. An answer with none, or one it cannot read, has none.
/// </summary>
public static class PressReceipts
{
    public static PressReceipt? Of(string answer)
    {
        try
        {
            using var document = JsonDocument.Parse(answer);
            if (!document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object
                || !data.TryGetProperty("receipt", out var read) || read.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            string? Field(string name) => read.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text ? text : null;
            var kind = Field("kind");
            if (kind is not (PressReceipt.KindOpened or PressReceipt.KindRoom) || Field("repository") is not { } repository) return null;
            var quests = read.TryGetProperty("quests", out var ids) && ids.ValueKind == JsonValueKind.Array
                ? ids.EnumerateArray().Where(each => each.ValueKind == JsonValueKind.String && each.GetString() is { Length: > 0 }).Select(each => each.GetString()!).ToList()
                : [];
            return new PressReceipt(
                kind, repository, Field("workspace"), Field("folder"), Field("line"), Field("room"), quests,
                read.TryGetProperty("done", out var done) && done.ValueKind == JsonValueKind.True);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
