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
/// carries the driver's whole sentence (REFUSE1); the catalogue code says which refusal it was.</para>
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

        // A request that came over the pipe is said as the terminal's (RPC1a, the terminal RPC design §8); the page's own stay unmarked.
        (string, object?)[] said = PipeDoor.Now is { } door
            ? [("code", code), ("request", $"{request.Module}.{request.Type}"), ("door", door)]
            : [("code", code), ("request", $"{request.Module}.{request.Type}")];
        log.Write(Ours.Contains(code) ? "info" : "warn", "refused", said);
    }
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

    /// <summary>The door the request this call runs for came in by, or null for the page's.</summary>
    public static string? Now => Current.Value;

    /// <summary>What the pipe holds around a dispatch: the request came in by the terminal's door until disposed.</summary>
    internal static IDisposable Enter()
    {
        var before = Current.Value;
        Current.Value = Terminal;
        return new Restore(before);
    }

    private sealed class Restore(string? before) : IDisposable
    {
        public void Dispose() => Current.Value = before;
    }
}
