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
public sealed record PipeDelegateSession(IReadOnlyList<byte[]> Hashes, string? HelpMode, Action<string>? Say = null)
{
    /// <summary>A working session's tier: its repository's permission prompts now (<see cref="AccessRules.Permissions"/>); null where it names none.</summary>
    public string? Permissions { get; init; }

    /// <summary>The families of acts the person granted Ask Daoris in restricted mode, read now (<see cref="ConfiguredAccess.Acts"/>).</summary>
    public IReadOnlyList<string> Families { get; init; } = [];

    /// <summary>The repository a working session's start serves, whose allow supervisor may answer its asked press.</summary>
    public string? Repository { get; init; }

    /// <summary>The place an Ask Daoris press named, against its conversation's focus now; null for every other kind.</summary>
    public Func<IpcRequest, CancellationToken, Task<HelpPressPlace?>>? PlaceOf { get; init; }
}

/// <summary>How a proved press is taken: run as the person's own with no card, put to the person on a card, or refused.</summary>
internal enum PipeDelegateTier
{
    Covered,
    Asked,
    Refused,
}

/// <summary>A press's tier, who its card names as asking, and, run with no card, its session and where it is said; <paramref name="Own"/> is a working session's, <paramref name="Terminal"/> a terminal's (<see cref="PipeTerminalTier"/>).</summary>
internal sealed record PipeDelegateJudgement(
    PipeDelegateTier Tier, PipeAsker? Asker = null, string? Session = null, Action<string>? Say = null, bool Own = false,
    string? Repository = null, Func<IpcRequest, CancellationToken, Task<HelpPressPlace?>>? PlaceOf = null, bool Terminal = false);

/// <summary>
/// A person's press a session's verb sends with the proof of the delegate key handed to its start (<see cref="DelegateKey"/>), judged
/// by the session's tier. Ask Daoris acts on the person's words: in full access, and in restricted mode for a family of acts the person
/// granted, it presses as the person, at once; otherwise it asks the person on the card, named as asking. A working session's tier is
/// its repository's access setup: unrestricted presses at once as the session's own, attributed to it and making nothing the owner's;
/// asking and the judged default ask the person, naming it. A proof that does not hold over this connection's nonce, a nonce already
/// used, or a session holding no key is refused.
/// </summary>
public sealed class PipeDelegate(InstancePipe pipe)
{
    /// <summary>The refusal's code: the press named a session whose key it did not prove.</summary>
    public const string Unproved = "delegate-unproved";

    /// <summary>The machine log's event for a press Ask Daoris made as the person, with no card.</summary>
    public const string PressedEvent = "help.pressed";

    /// <summary>The machine log's event for a press a working session made as its own, with no card.</summary>
    public const string SessionPressedEvent = "session.pressed";

    public const string TerminalPressedEvent = "terminal.pressed";

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
        var granted = string.Equals(request.Module, DriverModule.Name, StringComparison.OrdinalIgnoreCase)
            && ActFamilies.Covers(session.Families, request.Type);
        return session.HelpMode switch
        {
            HelpModes.Full => new(PipeDelegateTier.Covered, Session: claim.Session, Say: session.Say, PlaceOf: session.PlaceOf),
            HelpModes.Restricted when granted => new(PipeDelegateTier.Covered, Session: claim.Session, Say: session.Say, PlaceOf: session.PlaceOf),
            HelpModes.Restricted => new(PipeDelegateTier.Asked, asker with { Help = true }, PlaceOf: session.PlaceOf),
            null when session.Permissions == AccessRules.Unrestricted => new(PipeDelegateTier.Covered, Session: claim.Session, Own: true),
            null => new(PipeDelegateTier.Asked, asker, claim.Session, Repository: session.Repository),
            _ => new(PipeDelegateTier.Asked, asker),
        };
    }

    /// <summary>A press run with no card, in the machine log: Ask Daoris's as the person's, kept for the window's list; a terminal's as the person's, kept in the terminal tier's record (<see cref="PipeTerminalTier.Kept"/>); a working session's as its own.</summary>
    internal void Pressed(PipeDelegateJudgement judgement, IpcRequest request, Task<string> answer)
    {
        var route = $"{request.Module}.{request.Type}";
        if (judgement.Own)
        {
            pipe.Log.Info(SessionPressedEvent, ("session", judgement.Session ?? ""), ("route", route), ("by", By(judgement.Session)), ("door", PipeDoor.Terminal));
            return;
        }

        if (judgement.Terminal)
        {
            pipe.Log.Info(TerminalPressedEvent, ("route", route), ("by", "terminal"), ("door", PipeDoor.Terminal));
            PipeTerminalTier.Kept(pipe, request, answer);
            return;
        }

        pipe.Log.Info(PressedEvent, ("session", judgement.Session ?? ""), ("route", route), ("door", PipeDoor.Terminal));
        judgement.Say?.Invoke(route);
        // Kept for the window's list, which is told at the press and again at its answer.
        var presses = pipe.StandingConfirmation.HelpPresses;
        var kept = presses.Began(request, judgement.Session ?? "");
        pipe.TellDriverState();
        if (judgement.PlaceOf is { } placeOf) _ = PlaceAsync(placeOf, request, kept);
        _ = answer.ContinueWith(done =>
        {
            presses.Ended(kept, done.IsCompletedSuccessfully && HelpPressRecord.Succeeded(done.Result));
            pipe.TellDriverState();
        }, TaskScheduler.Default);
    }

    /// <summary>The press's place, read beside its run.</summary>
    private async Task PlaceAsync(Func<IpcRequest, CancellationToken, Task<HelpPressPlace?>> placeOf, IpcRequest request, string kept)
    {
        if (await ReadPlaceAsync(placeOf, request, CancellationToken.None).ConfigureAwait(false) is not { } place) return;
        pipe.StandingConfirmation.HelpPresses.Placed(kept, place);
        pipe.TellDriverState();
    }

    /// <summary>The place an Ask Daoris press named, read beside its run or before its card; one that cannot be read names none.</summary>
    internal static async Task<HelpPressPlace?> ReadPlaceAsync(
        Func<IpcRequest, CancellationToken, Task<HelpPressPlace?>>? placeOf, IpcRequest request, CancellationToken ct)
    {
        if (placeOf is null) return null;
        try
        {
            return await placeOf(request, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or IOException or InvalidOperationException or HttpRequestException)
        {
            return null;
        }
    }

    /// <summary>Whose a working session's own press is, as its log and its record say it: <c>session &lt;id&gt;</c>.</summary>
    internal static string By(string? session) => $"session {session}";
}
