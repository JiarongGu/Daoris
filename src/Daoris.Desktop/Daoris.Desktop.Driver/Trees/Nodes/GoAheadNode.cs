using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The <c>go-ahead</c> node read from D135's answers (FABRIC-NODE1b; the fabric events design §E4: a pressed node on D135's answer): reached
/// by the person's yes to the act it names, rejected by their refusal in their words. Pure: the caller hands in the go-aheads the work's ask
/// holds, and nothing is asked or written here.
/// </summary>
/// <remarks>
/// <para><b>Its act names it.</b> The node's <c>act</c> option (a D157 go-ahead step's, as decoding keeps it) is matched to the act a go-ahead
/// was asked for, word for word, and the newest such answers; a node naming no act is answered by none.</para>
/// <para><b>The person's word alone.</b> A yes reaches it by its reach authority, the person; a refusal rejects it with the words they gave,
/// and one given with none waits, since a rejection is said with its reason. Asked and unanswered, another act's answer, or none read: it
/// waits, never reached or rejected on a guess. Who raises the go-ahead to the person is the session's ask (D135) or HUMAN1's door.</para>
/// </remarks>
public static class GoAheadNode
{
    /// <summary>What D135's answers say of <paramref name="node"/> for <paramref name="unit"/>'s work: the person's yes or refusal, or none.</summary>
    public static NodeObservation Observe(WorkUnit unit, NodeDeclaration node, NodeAcceptance binding, IReadOnlyList<GoAheadView>? asked)
    {
        NodeObservation Seen(string? outcome, string said) => new(unit.Id, node.Id, node.Kind, outcome, said) { Attempt = binding.Attempt };

        if (node.Kind != NodeKinds.GoAhead) return Seen(null, "this kind is not a go-ahead") with { Refused = NodeRefusal.NotObserved };
        if (binding.Unit != unit.Id || binding.Node != node.Id || binding.NodeVersion != node.Version)
        {
            return Seen(null, "its binding names another unit, node or version") with { Refused = NodeRefusal.NotBound };
        }

        if (ActOf(node) is not { } act) return Seen(null, "it names no act, so no go-ahead answers it");
        if (asked is null) return Seen(null, "the work's go-aheads were not read");
        if (asked.Where(each => string.Equals(each.Act, act, StringComparison.Ordinal)).MaxBy(each => each.Number) is not { } goAhead)
        {
            return Seen(null, $"no go-ahead was asked for \"{act}\" yet");
        }

        return goAhead.Answer switch
        {
            null => Seen(null, $"go-ahead #{goAhead.Number} waits for your answer"),
            { Approved: true } => Seen(NodeStates.Reached, $"you gave go-ahead #{goAhead.Number}") with { Source = NodeSource.Person, Acceptance = binding },
            { Words: { } words } when !string.IsNullOrWhiteSpace(words) =>
                Seen(NodeStates.Rejected, $"you refused go-ahead #{goAhead.Number}") with { By = NodeCause.Person, Words = words },
            _ => Seen(null, $"you refused go-ahead #{goAhead.Number} with no words, which a rejection needs"),
        };
    }

    /// <summary>The act a node names, its <c>act</c> option's JSON text read as text; null where it names none.</summary>
    private static string? ActOf(NodeDeclaration node)
    {
        if (!node.Options.TryGetValue("act", out var text)) return null;
        try
        {
            using var read = JsonDocument.Parse(text);
            return read.RootElement.ValueKind == JsonValueKind.String && read.RootElement.GetString() is { Length: > 0 } act ? act : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
