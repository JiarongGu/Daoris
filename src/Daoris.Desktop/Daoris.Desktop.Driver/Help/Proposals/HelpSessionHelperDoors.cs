using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The <c>helper</c> and <c>pass</c> doors' fields of a session proposal's file: the agent, the ask the question asks again
/// after or the answer to pass, and that the answer is the person's.
/// </summary>
public sealed record HelperProposalFields(string? Agent, string? Ask, bool ToMe);

/// <summary>
/// The <c>session</c> kind's helper doors: the person's own ask of a helper, as the page's
/// <i>Ask another agent…</i> and <i>Ask again…</i> make it, and <i>Pass to the lead</i> on an answer kept for the person. A card judges
/// what the machine's records show (the session is one of this machine's that is not Ask Daoris's conversation, the question is within
/// its bound, the agent is one word); the ask itself is judged again as the door takes it, which knows the files.
/// </summary>
internal static class HelpSessionHelperDoors
{
    public static bool Is(string door) => door is "helper" or "pass";

    public static HelpProposal Read(HelpProposal proposal, JsonElement file) =>
        proposal.Kind == "session" && Is(proposal.Door)
            ? proposal with
            {
                Helper = new HelperProposalFields(
                    Json.Text(file, "agent"), Json.Text(file, "ask")?.TrimStart('#'),
                    file.TryGetProperty("toMe", out var flag) && flag.ValueKind == JsonValueKind.True),
            }
            : proposal;

    public static HelpPlan Plan(HelpProposal proposal, HelpMachineFacts facts)
    {
        var (id, fields) = (SessionOf(proposal), proposal.Helper ?? new HelperProposalFields(null, null, false));
        var pass = proposal.Door == "pass";
        var terminal = pass
            ? $"daoris session pass {fields.Ask}"
            : $"daoris session helper {id} \"{proposal.Value?.Trim()}\""
              + (fields.Agent is { Length: > 0 } agent ? $" --agent {agent}" : "")
              + (fields.Ask is { Length: > 0 } after ? $" --after {after}" : "")
              + (fields.ToMe ? " --to-me" : "");
        HelpPlan Refused(string why) => new(why, "", terminal, null);

        if (id is null) return Refused($"the {proposal.Door} door names exactly one session, by its id.");
        var record = facts.Sessions.FirstOrDefault(each => string.Equals(each.Id, id, StringComparison.OrdinalIgnoreCase));
        if (record is null) return Refused($"there is no session `{id}` on this machine");
        if (WordsNever.IsHelp(record)) return Refused($"`{record.Id}` is Ask Daoris's own conversation, which asks no helper and has none to pass");
        if (record.Teammate) return Refused($"`{record.Id}` is a teammate's: its helper is asked on their machine");

        if (pass)
        {
            if (fields.Ask is not { Length: > 0 } ask) return Refused("the pass door names the ask whose kept answer is passed.");
            return new HelpPlan(null,
                $"Pass the answer to ask `#{ask}`, kept for you, to session `{record.Id}`: the driver hands it at its next look as the helper's "
                + "answer, with your word beside it. An answer that is not yet kept for you, or reached that session already, changes nothing.",
                terminal, null) { Sessions = [] };
        }

        var question = proposal.Value?.Trim() ?? "";
        if (question.Length == 0) return Refused("a helper is asked a question, and none was given.");
        if (question.Length > SessionsHelperCommand.MaxQuestion)
        {
            return Refused($"a question is at most {SessionsHelperCommand.MaxQuestion} characters, and this is {question.Length}.");
        }

        if (fields.Agent is { } named && named.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
        {
            return Refused($"an agent is named as one word, as it is listed in Settings, and `{named}` is not.");
        }

        var by = fields.Agent is { Length: > 0 } chosen ? $"agent `{chosen}`" : "another agent Daoris chooses";
        var again = fields.Ask is { Length: > 0 } earlier ? $" It asks again after `#{earlier}`: that ask's question and answer are handed to it quoted." : "";
        var goes = fields.ToMe
            ? "Its answer comes to you, and reaches that session only once you pass it to the lead."
            : "Its answer comes back to that session as its next turn, marked as the helper's.";
        return new HelpPlan(null,
            $"Ask {by}, read-only, a question about session `{record.Id}`'s work at the driver's next look: {question}{again} {goes}",
            terminal, null) { Sessions = [] };
    }

    public static async Task<HelpApplied> ApplyAsync(HelpApplying applying, CancellationToken ct)
    {
        var (proposal, plan) = (applying.Proposal, applying.Plan);
        var (id, fields) = (SessionOf(proposal)!, proposal.Helper ?? new HelperProposalFields(null, null, false));
        var (ok, message) = proposal.Door == "pass"
            ? await applying.Doors.PassHelperAnswerAsync(id, fields.Ask ?? "", ct).ConfigureAwait(false)
            : await applying.Doors.AskHelperAsync(id, proposal.Value ?? "", fields.Agent, fields.Ask, fields.ToMe, ct).ConfigureAwait(false);
        return applying.Settled(ok, $"{(ok ? "Applied" : "Not applied")}: `#{applying.Id}` (`{plan.Terminal}`) — {message}", message);
    }

    /// <summary>The one session the door names, or null where it names none or several.</summary>
    private static string? SessionOf(HelpProposal proposal) =>
        proposal.Target?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) is [var one] ? one.TrimStart('#') : null;
}
