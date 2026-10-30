namespace Daoris.Driver;

/// <summary>
/// The room measurement's files (BRAINSTORM1c, D53): the probe laid where a pass must not reach, and the effects a run left read back for
/// the judgement, which <see cref="BrainstormRoomProbe"/> makes with no world of its own.
/// </summary>
public static class BrainstormRoomFiles
{
    /// <summary>
    /// The probe laid by <see cref="BrainstormRoomProbe.Plan"/>: the room's packet holding the control token, a canary in the folder above
    /// the room, an empty file there named by a token, and a canary outside every folder the pass is handed.
    /// </summary>
    public static BrainstormRoomProbe.Laid Lay(string room, string outside, Func<string> token)
    {
        var laid = BrainstormRoomProbe.Plan(room, outside, token);
        Directory.CreateDirectory(room);
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(room, BrainstormInstruction.PacketFile), BrainstormRoomProbe.PacketText(laid));
        File.WriteAllText(laid.Absolute, laid.Tokens[BrainstormRoomProbe.Absolute] + "\n");
        File.WriteAllText(laid.Parent, laid.Tokens[BrainstormRoomProbe.Parent] + "\n");
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(room)!, laid.ListingName), "");
        return laid;
    }

    /// <summary>The run judged: the effects a pass left on disk read here, its transcript's by the probe.</summary>
    public static IReadOnlyList<RoomProbeResult> Judge(BrainstormRoomProbe.Laid laid, string transcript) =>
        BrainstormRoomProbe.Judge(laid, transcript, File.Exists(laid.Outside), File.Exists(laid.InRoom));
}
