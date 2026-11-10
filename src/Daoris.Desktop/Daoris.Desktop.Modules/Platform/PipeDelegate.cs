using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// The <c>delegate</c> field a press carries beside its envelope (<see cref="DelegateProof.Field"/>): the session it names, its proof,
/// and the payload's text exactly as the line carried it, which the proof is over.
/// </summary>
internal sealed record PipeDelegateClaim(string Session, string Proof, string Payload)
{
    /// <summary>The claim on a line's object; null where it carries none, or none in its form.</summary>
    public static PipeDelegateClaim? Of(JsonElement line)
    {
        if (!line.TryGetProperty(DelegateProof.Field, out var field) || field.ValueKind != JsonValueKind.Object) return null;
        if (Text(field, "session") is not { Length: > 0 and <= 128 } session || Text(field, "proof") is not { Length: > 0 and <= 128 } proof)
        {
            return null;
        }

        return new(session, proof, line.TryGetProperty("payload", out var payload) ? payload.GetRawText() : "");
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}

/// <summary>
/// A session Daoris started, as a proved press reads it at its admission: the hashes of the delegate keys held for it, the mode its
/// Ask Daoris start got (null for every other kind), and where a press it made with no card is said on its conversation's console.
/// Read at each press, never kept with a hash.
/// </summary>
public sealed record PipeDelegateSession(IReadOnlyList<byte[]> Hashes, string? HelpMode, Action<string>? Say = null);

/// <summary>How a proved press is taken: run as the person's own with no card, put to the person on a card, or refused.</summary>
internal enum PipeDelegateTier
{
    Covered,
    Asked,
    Refused,
}

/// <summary>A proved press's tier, who its card names as asking, and, for one run with no card, its session and where it is said.</summary>
internal sealed record PipeDelegateJudgement(PipeDelegateTier Tier, PipeAsker? Asker = null, string? Session = null, Action<string>? Say = null);

/// <summary>
/// A person's press a session's verb sends with the proof of the delegate key handed to its start (<see cref="DelegateKey"/>), judged
/// by the session's tier: an Ask Daoris conversation in full access presses as the person, at once; one in restricted mode, and every
/// other kind, asks the person on the card, named as asking; a proof that does not hold over this connection's nonce, a nonce already
/// used, or a session holding no key is refused.
/// </summary>
public sealed class PipeDelegate(InstancePipe pipe)
{
    /// <summary>The refusal's code: the press named a session whose key it did not prove.</summary>
    public const string Unproved = "delegate-unproved";

    /// <summary>The machine log's event for a press Ask Daoris made in full access, with no card.</summary>
    public const string PressedEvent = "help.pressed";

    /// <summary>The refusal's words, which say nothing of which check failed.</summary>
    public const string UnprovedSentence =
        "This terminal did not prove the key Daoris handed its session, for this connection and this press. Nothing was asked or done.";

    /// <summary>
    /// The press judged: the connection's nonce taken once, the proof verified against what is held for the session it names, then the
    /// session's mode as it stands now.
    /// </summary>
    internal PipeDelegateJudgement Judge(IpcRequest request, string key, PipeDelegateClaim claim, PipeConnection connection)
    {
        var nonce = connection.TakeNonce();
        var session = nonce is null ? null : pipe.Delegates?.Invoke(claim.Session);
        if (session is not { Hashes.Count: > 0 }
            || !DelegateProof.Verify(session.Hashes, nonce!, request.Module, request.Type, claim.Payload, key, claim.Proof))
        {
            return new(PipeDelegateTier.Refused);
        }

        var asker = connection.Asker with { Session = claim.Session };
        return session.HelpMode switch
        {
            HelpModes.Full => new(PipeDelegateTier.Covered, Session: claim.Session, Say: session.Say),
            HelpModes.Restricted => new(PipeDelegateTier.Asked, asker with { Help = true }),
            _ => new(PipeDelegateTier.Asked, asker),
        };
    }

    /// <summary>A press run with no card, said as the person's: in the machine log by its codes, and once on its conversation's console.</summary>
    internal void Pressed(PipeDelegateJudgement judgement, IpcRequest request)
    {
        var route = $"{request.Module}.{request.Type}";
        pipe.Log.Info(PressedEvent, ("session", judgement.Session ?? ""), ("route", route), ("door", PipeDoor.Terminal));
        judgement.Say?.Invoke(route);
    }
}
