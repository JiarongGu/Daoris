namespace Daoris.Driver;

/// <summary>
/// A pass's fixed instruction (BRAINSTORM1a, D176 points 2-4): the packet named first and unchanged by its digest, so every pass reads
/// what the person saw; then the pass's one job and its one result tool. Never the caller's conversation, another pass's answer, or
/// which other makers run.
/// </summary>
public static class BrainstormInstruction
{
    /// <summary>The packet's file in every pass's room.</summary>
    public const string PacketFile = "packet.json";

    /// <summary>The anonymous proposals' file in the synthesis's room.</summary>
    public const string ProposalsFile = "proposals.json";

    /// <summary>The instruction's file in every pass's room.</summary>
    public const string InstructionFile = "instruction.md";

    /// <summary>A generator's instruction: read the packet, answer the question once through <c>brainstorm_give</c>.</summary>
    public static string Generator(string digest) =>
        Lead(digest)
        + "Your job: propose answers to the packet's question. Read the packet's sources; they are everything you are given, and "
        + "nothing outside this folder is yours to read.\n\n"
        + "Answer once, through your tool `brainstorm_give`, with `{ brainstorm: 1, proposals: [...], needs: [...], none }`. Each "
        + "proposal: `idea` (at most 200 characters), `rationale` (at most 1,200), `effort` (small, medium, large or unknown), `risk` "
        + "(low, medium, high or unknown), `reversible` (yes, partly, no or unknown), `undo` (how it would be undone or why it cannot, "
        + "at most 400, required unless reversible is yes), `confidence` (low, medium or high), `assumptions` (at most five of 200), "
        + "and `sources` (the packet's source ids, S1 and on, that it rests on). At most eight proposals, no idea twice. Put what you "
        + "would need and the packet lacks in `needs` (at most five of 200). With no proposal, say why in `none`, and give no "
        + "proposal beside it. Unknown is an answer: say it rather than guess the best value.\n\n"
        + "You change nothing: your proposals are claims a person reads, and nobody can ask you anything after.\n";

    /// <summary>
    /// The synthesizer's instruction: read the packet and the anonymous proposals, group the same ideas, rank the groups, and answer
    /// through <c>brainstorm_synthesize</c>. Who gave which proposal is not said, and is not its to guess.
    /// </summary>
    public static string Synthesizer(string digest) =>
        Lead(digest)
        + $"Your job: synthesize the proposals in `{ProposalsFile}`, each under an anonymous id (P1 and on). Group the proposals "
        + "that are the same idea, and rank the groups by how well each answers the question as asked; then reversible before not; "
        + "then lower risk; then lower effort. How many proposals a group holds is no reason to rank it higher.\n\n"
        + "Answer once, through your tool `brainstorm_synthesize`, with `{ brainstorm: 1, groups: [...], leftOut: [...] }`. Each "
        + "group: `title` (at most 200 characters), `idea` (the merged idea, at most 400), `members` (the proposal ids), `rank` (1 "
        + "and on, no gap and no tie), `why` (why this rank, at most 600), and `flags` you add, each `{ flag, why }` with flag "
        + "hard-to-reverse or risky. Every proposal id appears exactly once: in one group, or in `leftOut` as `{ id, why }`.\n\n"
        + "You change nothing: your ranking is a claim a person reads, and nobody can ask you anything after.\n";

    /// <summary>The one repair (D176 point 3): the validator's codes in fixed words, and the same job again.</summary>
    public static string Repair(string instruction, IReadOnlyList<string> codes) =>
        instruction + "\nYour last answer was refused by Daoris's validator, with these codes: " + string.Join(", ", codes)
        + ". Answer again, whole, through the same tool. This is your last turn.\n";

    private static string Lead(string digest) =>
        $"Daoris asks you one question, in the context packet `{PacketFile}` in this folder, packet `{digest}`. Read the packet "
        + "first: its question, then its sources, each with where it came from and how much of it was kept.\n\n";
}
