using System.Text.Json;
using System.Text.RegularExpressions;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// Every refusal the bridge answers, into the machine log as the <c>refused</c> event (LOG1b, D94): by its
/// code and the request it answered, never by its sentence.
/// </summary>
/// <remarks>
/// <para><b>The one place every module's answer passes</b> is the dispatcher's pipeline, so this is a
/// middleware in the application's slot of it (the kit's <c>UseMessageDispatcher</c>: its error handler,
/// then this, then the modules). A module needs nothing to be logged, and a new one is logged the day it
/// is registered.</para>
///
/// <para><b>The code and the request, and nothing more.</b> A refusal's parameters and fallback message
/// name what the person asked about (a folder, a quest, a window's name), and <c>DRIVER_REFUSED</c>
/// carries the driver's whole sentence (REFUSE1); the catalogue code says which refusal it was. A harness
/// action's says which action on which agent beside them (SIGNIN1), each a word of a fixed list.</para>
///
/// <para><b>Two levels.</b> A Daoris refusal (<see cref="Refusals"/>) is a decision the person met, and
/// is <c>info</c>. The kit's own codes (an exception a module did not expect, a type or a module this shell
/// does not have) are the page asking for what the host cannot do, a defect, and are <c>warn</c>. A
/// cancelled request is the page going away, and is neither.</para>
/// </remarks>
public static class RefusalLog
{
    private static readonly IReadOnlySet<string> Ours = new HashSet<string>(Refusals.All, StringComparer.Ordinal);

    /// <summary>The middleware, writing to <paramref name="log"/>.</summary>
    public static MessageMiddleware Middleware(MachineLog log) => async (request, next, _) =>
    {
        // No ConfigureAwait: the kit's pipeline keeps the caller's context, and a module reached on the UI
        // thread must be resumed there.
        var response = await next();
        Note(log, request, response);
        return response;
    };

    private static void Note(MachineLog log, IpcRequest request, IpcResponse? response)
    {
        // Nothing handled it: the dispatcher answers NO_HANDLER once the pipeline has run out.
        var code = response is null ? IpcErrorCodes.NoHandler : response.Success ? null : response.Error?.Code;
        if (code is null || code == IpcErrorCodes.OperationCancelled) return;

        List<(string, object?)> said = [("code", code), ("request", $"{request.Module}.{request.Type}")];
        // A harness action's refusal says which action on which agent (SIGNIN1), so a refused sign-in is told from a refused
        // install: each a word of a fixed list read from the request, never its other fields (a name, a key, a pasted code).
        if (request.Module == "DAORIS.DRIVER" && request.Type is "HARNESS_ACTION" or "HARNESS_INPUT" or "HARNESS_CANCEL")
        {
            said.Add(("action", Word(request, "action", word => AgentActions.Words.Contains(word, StringComparer.Ordinal))));
            said.Add(("harness", Word(request, "harness", Agent.IsMatch)));
        }

        // A request that came over the pipe is said as the terminal's (RPC1a, the terminal RPC design §8); the page's own stay unmarked.
        if (PipeDoor.Now is { } door) said.Add(("door", door));
        log.Write(Ours.Contains(code) ? "info" : "warn", "refused", said);
    }

    /// <summary>What an agent's id may be in a line: an identifier, never a phrase or a path.</summary>
    private static readonly Regex Agent = new("^[a-z0-9][a-z0-9.-]{0,63}$", RegexOptions.CultureInvariant);

    /// <summary>A payload's field as a word of its list, <c>other</c> for any other text, and null where it is absent.</summary>
    private static string? Word(IpcRequest request, string field, Func<string, bool> listed) =>
        request.Payload is { ValueKind: JsonValueKind.Object } payload
            && payload.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String
            ? listed(value.GetString()!) ? value.GetString() : "other"
            : null;
}

/// <summary>
/// Which door a request the dispatcher runs came in by (RPC1a, the terminal RPC design §8): <see cref="Terminal"/> for one the
/// pipe hands it, nothing for the page's own. Found by the flow of the call, as <see cref="PressSteps"/> finds its press, so the
/// pipeline's middleware reads it without the request carrying it.
/// </summary>
public static class PipeDoor
{
    /// <summary>The terminal's door: a request that came over the running Daoris's pipe.</summary>
    public const string Terminal = "terminal";

    private static readonly AsyncLocal<string?> Current = new();

    private static readonly AsyncLocal<bool> Granted = new();

    /// <summary>The door the request this call runs for came in by, or null for the page's.</summary>
    public static string? Now => Current.Value;

    /// <summary>
    /// Whether the request this call runs for came in by the terminal's door and was admitted under the person's standing confirmation
    /// (TERMTRUST1a, D201 point 7, the terminal-trust design §5 step 4), with no card: its door is still <see cref="Terminal"/>, so every
    /// handler that refuses the terminal refuses it too, and the press's route writes the record's mark from this.
    /// </summary>
    public static bool Standing => Current.Value == Terminal && Granted.Value;

    private static readonly AsyncLocal<RoleJudgement?> Appointed = new();

    /// <summary>
    /// The role act the request this call runs for was covered as (SUPERVISOR1a-ii, D208; the supervisor design §11 row 1a-ii): the
    /// appointment the judge's role method found it under, read by a role's handler, which refuses every door but a covered terminal's;
    /// null for every other request. Never <see cref="Standing"/>, which is the presses' alone.
    /// </summary>
    public static RoleJudgement? Role => Current.Value == Terminal && Appointed.Value is { Covered: true } covered ? covered : null;

    private static readonly AsyncLocal<PipeClientProcess?> Connected = new();

    /// <summary>
    /// The pipe's client the request this call runs for came from, held by the connection (STEWARDROLE1c: the desk counts a request against
    /// the caller it is keyed by); null for the page's own.
    /// </summary>
    public static PipeClientProcess? Client => Current.Value == Terminal ? Connected.Value : null;

    /// <summary>What the pipe holds around a dispatch: the request came in by the terminal's door until disposed.</summary>
    internal static IDisposable Enter(PipeClientProcess? client = null) => Enter(standing: false, role: null, client);

    /// <summary>The same, for a press the standing confirmation admitted (<see cref="Standing"/>).</summary>
    internal static IDisposable EnterStanding(PipeClientProcess? client = null) => Enter(standing: true, role: null, client);

    /// <summary>The same, for a role's act the judge's role method covered (<see cref="Role"/>).</summary>
    internal static IDisposable EnterRole(RoleJudgement covered, PipeClientProcess? client = null) => Enter(standing: false, role: covered, client);

    private static IDisposable Enter(bool standing, RoleJudgement? role, PipeClientProcess? client)
    {
        var before = (Current.Value, Granted.Value, Appointed.Value, Connected.Value);
        Current.Value = Terminal;
        Granted.Value = standing;
        Appointed.Value = role;
        Connected.Value = client;
        return new Restore(before);
    }

    private sealed class Restore((string? Door, bool Standing, RoleJudgement? Role, PipeClientProcess? Client) before) : IDisposable
    {
        public void Dispose() => (Current.Value, Granted.Value, Appointed.Value, Connected.Value) = before;
    }
}
