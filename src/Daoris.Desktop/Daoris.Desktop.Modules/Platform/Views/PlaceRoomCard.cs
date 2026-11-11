using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// A room asked of a repository as <see cref="PlaceApplyView"/> shows it: its own sentence, naming the room and the reason as the plan keeps them,
/// whether it is asked alone because the repository is not driven, and the first lines of the one quest it asks, which are the quest's own
/// (<see cref="RoomBrief"/>). The room's one change is that quest, so the card words it rather than the plan's.
/// </summary>
internal static class PlaceRoomCard
{
    public static PressView Of(PlaceAsk ask, Dictionary<string, string> facts, string command, JsonElement plan)
    {
        var kept = PlaceRooms.Normalized(ask);
        var (room, purpose) = (kept.Room!, kept.Purpose ?? "");
        facts["room"] = room;
        facts["purpose"] = purpose;
        List<PressSaid> also = [];
        if (PressPayload.Rows(plan, "changes").Any(row => PressPayload.Field(row.TryGetProperty("change", out var made) ? made : default, "said") == PlacePlans.AloneSaid))
        {
            also.Add(new PressSaid("place-room-alone", ("repository", facts["repositories"])));
        }

        also.Add(new PressSaid("place-room-quest", ("title", RoomBrief.TitleOf(room)), ("head", QuestHeads.Plain(RoomBrief.Opening(room, purpose)))));
        return new PressView("place-room", facts, command, also);
    }
}
