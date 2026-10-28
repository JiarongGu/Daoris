using System.Text;

namespace Daoris.Driver;

/// <summary>What a partner is handed (COAGENT1b; the co-agent design §2.3 step 5): the piece as written, its paths, the base, and the lead's uncommitted paths.</summary>
/// <param name="Lead">The session that asked.</param>
/// <param name="Repository">The repository the lead works in.</param>
/// <param name="Base">The exact commit the piece grows from, which the copy stands at.</param>
/// <param name="Piece">The piece, verbatim.</param>
/// <param name="Paths">The repository-relative paths the piece is about; empty where the ask named none.</param>
/// <param name="Uncommitted">The lead's uncommitted paths at the ask, which the partner leaves alone.</param>
/// <param name="Minutes">The bound of its one turn.</param>
public sealed record PartnerPacket(
    string Lead, string Repository, string Base, string Piece, IReadOnlyList<string> Paths, IReadOnlyList<string> Uncommitted, int Minutes);

/// <summary>
/// The partner's instruction (COAGENT1b, D199 points 2 and 10; the co-agent design §2.3 step 5): the piece as its lead's asker wrote it,
/// where it writes, what never, and how it ends. Composed once, from facts, with no model; handed whole in one turn. Project-agnostic,
/// as every instruction is, and one line a paragraph.
/// </summary>
/// <remarks>
/// The words are the second layer, never the only one: the copy, its strict job and the measured mechanism hold the partner, and the
/// server refuses its role's tools. The files it leaves are what Daoris takes, never its words about them.
/// </remarks>
public static class PartnerInstruction
{
    /// <summary>How long one partner's turn may run unless the machine's session timeout comes first (a first value, read again in COAGENT1f).</summary>
    public const int DefaultMinutes = 45;

    public static string Compose(PartnerPacket packet)
    {
        var text = new StringBuilder();
        text.Append($"You are a partner of another session that is working in `{packet.Repository}`. ")
            .Append("It asked you for one piece of its work, below. You write that piece, in one turn, and you ask nobody anything.\n\n")
            .Append($"You are in a copy of the repository made for this piece alone, checked out at `{packet.Base}`, the commit the asking session's work stood at when it asked. ")
            .Append("The copy has no remote. Write only inside it. When your turn ends, Daoris reads the files you left in the copy, ")
            .Append("takes them into one commit of its own on that same commit, and hands it to the session that asked, which decides whether to merge it. ")
            .Append("Your own commits, branches and anything you wrote and then deleted are not taken: only the files as you leave them.\n");

        text.Append("\n## The piece\n\n")
            .Append("The session's asker wrote it as follows, and it is all you were told:\n\n")
            .Append(TextBounds.Quote(packet.Piece, trimEnd: true)).Append("\n\n");
        if (packet.Paths.Count > 0)
        {
            text.Append("It is about these paths. A change outside them is kept and named as beyond the piece:\n\n");
            foreach (var path in packet.Paths) text.Append($"- `{path}`\n");
            text.Append('\n');
        }

        if (packet.Uncommitted.Count > 0)
        {
            text.Append("The asking session had these paths changed and not committed when it asked. Its own work there is not in your copy; leave them alone:\n\n");
            foreach (var path in packet.Uncommitted) text.Append($"- `{path}`\n");
            text.Append('\n');
        }

        text.Append("## What you may do\n\n")
            .Append("You read and write files in your copy, and you may run the repository's own checks there. ")
            .Append("You never write outside your copy; push, publish or deploy; start, stop or touch a process, a port or a server of the person's; ")
            .Append("take, close, decline or publish a quest; or ask the person, a helper, another partner or anyone else. ")
            .Append("Nobody will answer a question from you while you work: decide from what you can read, and say what you could not tell.\n");

        text.Append("\n## How you end\n\n")
            .Append("Say your answer once, before you end, with your connector's `partner_answer`: a summary of what you did, what you did not, ")
            .Append("and what you would need to finish. Your summary is a claim: the session that asked checks it, and Daoris adds what it reads itself, ")
            .Append("the commit and the files it changes. ")
            .Append($"You have one turn, and at most {packet.Minutes} minutes; the files you leave when it ends are your piece.\n");
        return text.ToString();
    }
}

/// <summary>The partner's pass's own words and bounds (COAGENT1b), each said once for every door.</summary>
public static class PartnerPass
{
    /// <summary>
    /// How long one partner's turn may run: its own bound, never capped by a session's time (SESSWATCH1a, D200); only the tests' backstop
    /// (<see cref="DriverConfig.TestBound"/>) comes first.
    /// </summary>
    public static int Bound(DriverConfig config) => Math.Max(1, Math.Min(PartnerInstruction.DefaultMinutes, config.TestBound ?? int.MaxValue));

    /// <summary>What a copy's folder name starts with, beside a session tree's <c>s-</c> and an opinion's <c>o-</c> (the design §2.2).</summary>
    public const string Prefix = "p-";

    /// <summary>The copy's folder for a piece, under the home's trees, named before it is made (the design §7.2's <c>preparing</c>).</summary>
    public static string CopyFor(string home, string? workspace, string repository, string piece) =>
        DaorisHome.Entry(
            home, HomeEntry.Trees, workspace is { } named && !string.IsNullOrWhiteSpace(named) ? named.Trim() : RemoteTarget.DefaultWorkspace,
            repository.Trim(), Prefix + piece);

    /// <summary>
    /// What a partner's transcript says where this machine has no connector: it can write, but cannot say its answer through the tool,
    /// so its last words stand.
    /// </summary>
    public static string NoConnector =>
        $"— no {KnowledgeConnector.ExecutableName} on this machine, so this partner has no connector: its files are still its piece, "
        + "but it cannot say its answer through `partner_answer`, and its last words stand as the answer.";

    /// <summary>The line a partner's transcript opens with: what it writes, where, and what holds it.</summary>
    public static string Opening(string repository, string @base) =>
        $"— a partner writing one piece for a session working in `{repository}`, at `{TextBounds.ShortCommit(@base, 7)}`: "
        + "in a copy of its own with no remote, inside a job of its own; Daoris takes the files it leaves.";

    /// <summary>Why a person's words are refused for a partner while it works, the sentence the bridge carries verbatim.</summary>
    public static string TakesNoWords(string piece) =>
        $"the session writing piece `{piece}` takes no words: a partner writes in one turn and nobody talks to it while it works.";

    /// <summary>The keeper's commit's message: the piece, its partner's agent and account.</summary>
    public static string Message(PartnerAsk ask, string? agent, string? account) =>
        $"Piece {ask.Id} by a partner ({agent ?? "an agent"}{(account is { Length: > 0 } ? $", account {account}" : "")})\n\n"
        + $"Asked by session {ask.Asker}: {TextBounds.Clip(ask.Piece.ReplaceLineEndings(" "), 200)}\n";
}
