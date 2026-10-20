using System.Net;
using System.Net.Sockets;

namespace Daoris.Driver;

/// <summary>
/// Which of an agent's sign-ins runs (CODEXLOGIN1, D125's note): its own flow, which for Codex opens the system's browser and
/// waits for its answer on a local port, offered first; or its device code, a link and a one-time code that need no port.
/// </summary>
public enum SignInWay
{
    /// <summary>The tool's own sign-in (<see cref="HarnessToolchain.LoginArguments"/>), offered first.</summary>
    Browser,

    /// <summary>The tool's sign-in by a device code (<see cref="HarnessToolchain.DeviceLoginArguments"/>), offered beside it.</summary>
    DeviceCode,
}

/// <summary>
/// The words a sign-in's way travels as (CODEXLOGIN1): <c>HARNESS_ACTION</c>'s <c>way</c>, absent for the first. The CLI's
/// <c>--device-code</c> is the second's twin.
/// </summary>
public static class SignInWays
{
    /// <summary>The tool's own sign-in, the first: <c>codex login</c>.</summary>
    public const string Browser = "browser";

    /// <summary>The sign-in by a device code: <c>codex login --device-auth</c>.</summary>
    public const string DeviceCode = "device-code";

    /// <summary>The way a word names; none is the first. Any other word is refused, naming the two.</summary>
    public static SignInWay Of(string? word) => word?.Trim() switch
    {
        null or "" or Browser => SignInWay.Browser,
        DeviceCode => SignInWay.DeviceCode,
        _ => throw new DriverException($"`{word}` is no way to sign in — `{Browser}` or `{DeviceCode}`."),
    };

    /// <summary>The word a way travels as.</summary>
    public static string Word(SignInWay way) => way == SignInWay.DeviceCode ? DeviceCode : Browser;

    /// <summary>
    /// The arguments a way runs, given to the agent's binary: its own flow, or its device code where it declares one. A device
    /// code asked of an agent that has none is refused, before anything starts.
    /// </summary>
    /// <param name="agent">The agent whose sign-in this is, named in the refusal.</param>
    public static IReadOnlyList<string> Arguments(string agent, HarnessToolchain toolchain, SignInWay way)
    {
        if (way == SignInWay.DeviceCode)
        {
            return toolchain.DeviceLoginArguments is { Count: > 0 } device
                ? device
                : throw new DriverException(
                    $"`{agent}` signs in by no device code, so nothing was signed in — its own sign-in is its one way.");
        }

        return toolchain.LoginArguments is { Count: > 0 } own
            ? own
            : throw new DriverException(
                "that agent declares no sign-in flow — sign in with its own tooling, pointing its "
                + "configuration-home variable at the account's directory.");
    }
}

/// <summary>Whether a local port can be taken now, by trying it (CODEXLOGIN1).</summary>
public enum PortHold
{
    /// <summary>It was taken and let go at once.</summary>
    Free,

    /// <summary>Another program holds it (address in use).</summary>
    InUse,

    /// <summary>Windows refuses it: a range it reserves covers it (os error 10013, access denied); a restart may move the range.</summary>
    Reserved,

    /// <summary>It could not be taken for another reason.</summary>
    Refused,
}

/// <summary>
/// Whether a sign-in that waits for the browser's answer on a local port can have one (CODEXLOGIN1, D125's note): each port the
/// tool tries (<see cref="HarnessToolchain.LoginPorts"/>), taken for a moment on the loopback address and let go, never read
/// from Windows' table of reserved ranges, since a range is not the only holder and the table is not the bind.
/// </summary>
/// <remarks>
/// <para><b>Measured with Codex 0.160.0</b>: <c>codex login</c> listens on 127.0.0.1:1455; where another program holds it, it
/// asks that port's <c>/cancel</c>, tries for about two seconds, then takes 1457, and only where 1457 is held too does it end
/// with <i>Error logging in: Port 127.0.0.1:1457 is already in use</i>, exit 1. Under a reserved range CODEXACCT2 measured
/// <i>os error 10013</i>, exit 1, when that range covered both. So the sign-in is refused here only where no port can be taken,
/// once per press: nothing is tried again in a loop.</para>
/// <para>The CLI's <c>portHolds</c> (<c>service.ts</c>) and <c>portsHeld</c> (<c>toolchain.ts</c>) are the twin, and say the
/// sentence below to the byte.</para>
/// </remarks>
public static class SignInPorts
{
    /// <summary>Take <paramref name="port"/> on the loopback address for a moment, exclusively, and say whether it could be.</summary>
    public static PortHold Hold(int port)
    {
        var listener = new TcpListener(IPAddress.Loopback, port) { ExclusiveAddressUse = true };
        try
        {
            listener.Start();
            return PortHold.Free;
        }
        catch (SocketException error)
        {
            return error.SocketErrorCode switch
            {
                SocketError.AddressAlreadyInUse => PortHold.InUse,
                SocketError.AccessDenied => PortHold.Reserved,
                _ => PortHold.Refused,
            };
        }
        finally
        {
            listener.Stop();
        }
    }

    /// <summary>
    /// The refusal of a browser sign-in where none of its ports can be taken, or null where one can or the tool waits on none.
    /// The first port free is enough: the tool takes the next where the first is held.
    /// </summary>
    /// <param name="agent">The agent whose sign-in this is: <c>codex</c>.</param>
    /// <param name="hold">How a port is tried: <see cref="Hold"/>, unless a test's.</param>
    public static string? Refusal(string agent, HarnessToolchain toolchain, Func<int, PortHold> hold)
    {
        if (toolchain.LoginPorts is not { Count: > 0 } ports) return null;
        var held = new List<(int Port, PortHold Hold)>();
        foreach (var port in ports)
        {
            var now = hold(port);
            if (now == PortHold.Free) return null;
            held.Add((port, now));
        }

        var wayOut = toolchain.DeviceLoginArguments is { Count: > 0 }
            ? "sign in with a device code instead."
            : "close what holds it, or restart, and try again.";
        return $"{Held(agent, toolchain.LoginArguments ?? [], held)} Nothing was signed in — {wayOut}";
    }

    /// <summary>
    /// Which ports are held and by what, in plain words: the CLI's <c>portsHeld</c> says it to the byte. The way out after it is
    /// each door's own.
    /// </summary>
    public static string Held(string agent, IReadOnlyList<string> login, IReadOnlyList<(int Port, PortHold Hold)> held)
    {
        var ports = string.Join(", else ", held.Select(each => each.Port));
        var none = held.Count switch { 1 => "it cannot", 2 => "neither can", _ => "none can" };
        var why = string.Join("; ", held.Select(each => $"{each.Port} is {Words(each.Hold)}"));
        return $"`{string.Join(' ', [agent, .. login])}` waits for your browser on port {ports}, and {none} be taken now: {why}.";
    }

    private static string Words(PortHold hold) => hold switch
    {
        PortHold.InUse => "held by another program",
        PortHold.Reserved => "held by a range Windows reserves, which a restart may move",
        _ => "refused to Daoris",
    };
}
