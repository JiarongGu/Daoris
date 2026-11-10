using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// An Apply pressed on a card its first press already settled: a second window's, or the same card pressed again before the page drew it
/// settled. Answered with what the first press came to, as the proposal's file keeps it, and logged with its reason; never refused bare.
/// </summary>
public static class HelpAppliedAgain
{
    /// <summary>The machine log's event for a press answered this way.</summary>
    public const string Event = "help.apply.again";

    /// <summary>Why nothing ran at the press, as the log line names it.</summary>
    public const string Reason = "settled-before";

    /// <summary>Keep what the first press told beside the proposal's settled state, so a later press is answered with it.</summary>
    public static void Keep(string home, string id, string told)
    {
        var path = Path.Combine(HelpProposals.FolderOf(home), $"{Path.GetFileName(id)}.json");
        try
        {
            var node = JsonNode.Parse(AtomicFile.ReadText(path))!.AsObject();
            node["told"] = told;
            JsonFile.Write(path, node);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            // A later press is answered with the settled state alone, as one from before the outcome was kept.
        }
    }

    /// <summary>The answer to a press on <paramref name="proposal"/>, settled already: its state, and what the first press told where it was kept.</summary>
    public static HelpApplied Answer(string home, HelpProposal proposal)
    {
        var told = Told(home, proposal.Id);
        var lead = $"Already {proposal.State}: `#{proposal.Id}` was settled by an earlier press, so nothing ran again.";
        return new HelpApplied(proposal.State == "applied", told is null ? lead : $"{lead} It said: {told}");
    }

    /// <summary>The log line's fields for the press: the proposal, its settled state, and the reason nothing ran.</summary>
    public static IReadOnlyList<(string Key, object? Value)> Line(HelpProposal proposal) =>
        [("proposal", proposal.Id), ("kind", proposal.Kind), ("state", proposal.State), ("reason", Reason)];

    private static string? Told(string home, string id)
    {
        var path = Path.Combine(HelpProposals.FolderOf(home), $"{Path.GetFileName(id)}.json");
        try
        {
            using var document = JsonDocument.Parse(AtomicFile.ReadText(path));
            return document.RootElement.TryGetProperty("told", out var told) && told.ValueKind == JsonValueKind.String ? told.GetString() : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }
}
