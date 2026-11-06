namespace Daoris.Driver;

/// <summary>
/// The person's own act on a piece (COAGENT1b2, D199; the co-agent design §6.1, §6.4): the one judge and writer the page's <i>Discard…</i>
/// and <i>Hand over what it left</i> share. It judges what a reader of the file cannot know, the piece and the state that takes the act, and
/// writes the act's file; the driver's next look does the act, since it alone writes a piece's state and runs git for it.
/// </summary>
public static class PartnerPersonAct
{
    public static PartnerActed Ask(string home, string piece, string act, DateTimeOffset at)
    {
        static PartnerActed No(string why, string sentence) => new(false, why, $"{sentence} Nothing was done.");
        if (!PartnerPersonActs.All.Contains(act)) return No(PartnerPersonActs.Why.Unknown, $"`{act}` is not an act of a piece's.");
        var files = new PartnerFiles(home);
        if (files.ReadAsk(piece) is null) return No(PartnerPersonActs.Why.NoPiece, $"No piece `{piece}` is on file here.");
        var state = files.ReadState(piece);
        var standing = state?.State ?? (files.Unreadable(piece, "state") ? PartnerStates.Uncertain : PartnerStates.Asked);
        if (!PartnerPersonActs.Allows(act, standing))
        {
            return No(PartnerPersonActs.Why.State, $"Piece `{piece}` stands at {standing}, which does not take {act}.");
        }

        if (act == PartnerPersonActs.HandOver && (state?.Copy is not { Length: > 0 } copy || !Directory.Exists(copy)))
        {
            return No(PartnerPersonActs.Why.NoCopy, $"Piece `{piece}` has no copy left to hand over.");
        }

        new PartnerActFiles(home).Write(new PartnerActAsk(piece, act, at));
        return new PartnerActed(true, null, act == PartnerPersonActs.Discard
            ? $"Discarding piece `{piece}`: the driver removes its ref and its copy at its next look."
            : $"Handing over what piece `{piece}` left: the driver keeps its work and hands it at its next look.");
    }
}
