using System.IO.Pipes;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace Daoris.Driver;

/// <summary>
/// A terminal's connect to the running Daoris's pipe: <paramref name="Client"/> where it connected; else <paramref name="Refused"/>
/// where the pipe refused this terminal as another account's or elevation's (PIPEOWNER1), and otherwise nothing took it in time.
/// </summary>
public readonly record struct PipeConnect(InstanceClient? Client, bool Refused);

/// <summary>
/// A terminal's connection to the running Daoris's pipe (RPC1b, D171 point 3, the terminal RPC design §6): the wire RPC1a's
/// <c>InstancePipe</c> serves, as one verb speaks it, one request at a time.
/// </summary>
/// <remarks>
/// <para><b>The wire</b>: UTF-8 lines of JSON, LF. The hello first (<c>DAORIS.PIPE</c> <c>HELLO</c>, this home's id and the wire's
/// version); then the kit's request envelope, <c>{ id, module, type, payload }</c>, with <c>key</c> beside it, a secret only this
/// connection's verb holds, which <c>RESULT</c> asks with again; answers are the kit's <c>{ category: "ipc", id, success, data?,
/// error? }</c>, and a route's progress the kit's notification envelope naming the request's id.</para>
///
/// <para><b>This user's pipe only</b> (§4.1): a pipe whose owner is not one of <see cref="ThisUser"/>'s SIDs is refused before a
/// line crosses, as one whose ACL refuses this user is, and said as a refusal (<see cref="PipeConnect.Refused"/>), never as a pipe
/// that did not answer. On Windows the owner is read here rather than through <see cref="PipeOptions.CurrentUserOnly"/>, which takes
/// only the token's default owner: an elevated token's is the Administrators group, so a terminal and a Daoris of one user at two
/// elevations refused each other (PIPEOWNER1). The process serving the pipe is read, for the finder to hold to the address
/// file's.</para>
/// </remarks>
public sealed class InstanceClient : IAsyncDisposable
{
    /// <summary>The pipe's own module, as <c>InstancePipe.Module</c> names it.</summary>
    public const string PipeModule = "DAORIS.PIPE";

    /// <summary>The wire's version, as <c>InstancePipe.Protocol</c> names it.</summary>
    public const int Protocol = 1;

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);
    private readonly NamedPipeClientStream _stream;
    private readonly StreamReader _reader;

    private InstanceClient(string pipe, NamedPipeClientStream stream, int? server)
    {
        Pipe = pipe;
        _stream = stream;
        _reader = new StreamReader(stream, Utf8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        Server = server;
    }

    /// <summary>The pipe's name.</summary>
    public string Pipe { get; }

    /// <summary>The process serving the pipe, where Windows says; null where it cannot.</summary>
    public int? Server { get; }

    /// <summary>
    /// The connection to <paramref name="pipe"/>; or none, <see cref="PipeConnect.Refused"/> where the pipe refused this terminal
    /// (its ACL, or an owner not among <paramref name="owners"/>, <see cref="ThisUser"/>'s where null), else where it did not take
    /// one within <paramref name="within"/>.
    /// </summary>
    public static async Task<PipeConnect> ConnectAsync(
        string pipe, TimeSpan within, IReadOnlySet<string>? owners = null, CancellationToken ct = default)
    {
        // Elsewhere the kit's own check stands: a socket's peer is read there, not an owner (§11).
        var options = PipeOptions.Asynchronous | (OperatingSystem.IsWindows() ? PipeOptions.None : PipeOptions.CurrentUserOnly);
        var stream = new NamedPipeClientStream(".", pipe, PipeDirection.InOut, options);
        try
        {
            await stream.ConnectAsync(within, ct).ConfigureAwait(false);
            if (OperatingSystem.IsWindows() && !OwnedBy(stream, owners ?? ThisUser()))
            {
                await stream.DisposeAsync().ConfigureAwait(false);
                return new(null, Refused: true);
            }

            return new(new InstanceClient(pipe, stream, ServerOf(stream)), Refused: false);
        }
        catch (UnauthorizedAccessException)
        {
            await stream.DisposeAsync().ConfigureAwait(false);
            return new(null, Refused: true);
        }
        catch (Exception error) when (error is TimeoutException or IOException or OperationCanceledException)
        {
            await stream.DisposeAsync().ConfigureAwait(false);
            return new(null, Refused: false);
        }
    }

    /// <summary>
    /// The SIDs a pipe of this user's is owned by (PIPEOWNER1): the user's, which a Daoris at either elevation makes its pipe's owner
    /// (<c>InstancePipe.Security</c>), and this token's default owner, the only one <see cref="PipeOptions.CurrentUserOnly"/> took and
    /// the one an older Daoris left its pipe's, so an elevated terminal still reaches an older elevated Daoris, whose pipe the
    /// Administrators group owns. A terminal that is not elevated has one: its default owner is the user.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static IReadOnlySet<string> ThisUser()
    {
        using var self = WindowsIdentity.GetCurrent();
        return new HashSet<string>(StringComparer.OrdinalIgnoreCase) { self.User!.Value, self.Owner!.Value };
    }

    /// <summary>Whether the pipe <paramref name="stream"/> is connected to is owned by one of <paramref name="owners"/>; false where its owner cannot be read.</summary>
    [SupportedOSPlatform("windows")]
    public static bool OwnedBy(PipeStream stream, IReadOnlySet<string> owners)
    {
        try
        {
            return stream.GetAccessControl().GetOwner(typeof(SecurityIdentifier)) is SecurityIdentifier owner && owners.Contains(owner.Value);
        }
        catch (Exception error) when (error is UnauthorizedAccessException or IOException or InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>The hello's answer, the whole envelope, or null where none came within <paramref name="within"/>.</summary>
    public Task<JsonElement?> HelloAsync(string? home, TimeSpan within, CancellationToken ct = default) =>
        RequestAsync(Guid.NewGuid().ToString(), PipeModule, "HELLO",
            new { protocol = Protocol, home, client = "daoris-driver", version = Version }, key: null, told: null, within, ct);

    /// <summary><c>RESULT { id, key }</c>: how the press <paramref name="id"/> ended, the whole envelope, or null where none came.</summary>
    public Task<JsonElement?> ResultAsync(string id, string key, TimeSpan within, CancellationToken ct = default) =>
        RequestAsync(Guid.NewGuid().ToString(), PipeModule, "RESULT", new { id, key }, key: null, told: null, within, ct);

    /// <summary>
    /// One request, and its answer's whole envelope; null where the connection was lost or nothing answered within
    /// <paramref name="within"/>. Each notification naming it on the way is handed to <paramref name="told"/>.
    /// </summary>
    public async Task<JsonElement?> RequestAsync(
        string id, string module, string type, object? payload, string? key, Action<JsonElement>? told, TimeSpan within,
        CancellationToken ct = default)
    {
        using var wait = CancellationTokenSource.CreateLinkedTokenSource(ct);
        wait.CancelAfter(within);
        try
        {
            var line = key is null
                ? JsonSerializer.Serialize(new { id, module, type, payload })
                : JsonSerializer.Serialize(new { id, module, type, payload, key });
            await _stream.WriteAsync(Utf8.GetBytes(line + "\n"), wait.Token).ConfigureAwait(false);
            await _stream.FlushAsync(wait.Token).ConfigureAwait(false);
            while (await _reader.ReadLineAsync(wait.Token).ConfigureAwait(false) is { } read)
            {
                JsonElement envelope;
                try
                {
                    envelope = JsonDocument.Parse(read).RootElement.Clone();
                }
                catch (JsonException)
                {
                    // Not the wire: nothing it says is an answer.
                    continue;
                }

                if (envelope.ValueKind != JsonValueKind.Object) continue;
                if (Text(envelope, "category") == "notification")
                {
                    told?.Invoke(envelope);
                    continue;
                }

                if (Text(envelope, "id") == id) return envelope;
            }

            return null;
        }
        catch (Exception error) when (error is IOException or ObjectDisposedException or OperationCanceledException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>The string <paramref name="element"/> holds in <paramref name="field"/>, or null.</summary>
    internal static string? Text(JsonElement element, string field) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>This build, as the hello names it.</summary>
    private static string Version =>
        typeof(InstanceClient).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0";

    private static int? ServerOf(NamedPipeClientStream stream)
    {
        if (!OperatingSystem.IsWindows()) return null;
        try
        {
            return GetNamedPipeServerProcessId(stream.SafePipeHandle, out var process) ? (int)process : null;
        }
        catch (Exception error) when (error is InvalidOperationException or ObjectDisposedException or EntryPointNotFoundException)
        {
            return null;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint serverProcessId);

    public async ValueTask DisposeAsync()
    {
        _reader.Dispose();
        await _stream.DisposeAsync().ConfigureAwait(false);
    }
}
