using System.IO.Pipes;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;

namespace Daoris.Driver;

/// <summary>One connection the relay's pipe took: its stream, and its client's process where the system said it.</summary>
public sealed class RelayClient(Stream stream, int? pid, Action ended) : IAsyncDisposable
{
    private int _ended;

    public Stream Stream { get; } = stream;

    /// <summary>The client's process, asked of the system; null off Windows, where nothing here asks.</summary>
    public int? Pid { get; } = pid;

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _ended, 1) == 1) return;
        await Stream.DisposeAsync().ConfigureAwait(false);
        ended();
    }
}

/// <summary>
/// The relay's one pipe: on Windows <c>daoris-event-</c> and 32 random hex characters,
/// made by this user's rule (<see cref="UserPipe.Security"/>) as its first instance, for two instances, so as one is taken by a client
/// the other listens and a second client reaches the relay instead of a busy pipe; elsewhere a Unix socket of that name in the
/// subscribers' folder, which only the user reads.
/// </summary>
public sealed class RelayPipe : IAsyncDisposable
{
    /// <summary>The instances the pipe is made for.</summary>
    public const int Instances = 2;

    private readonly SemaphoreSlim _places = new(Instances, Instances);
    private readonly Socket? _socket;
    private bool _first = true;

    private RelayPipe(string name, string address, Socket? socket) => (Name, Address, _socket) = (name, address, socket);

    /// <summary>The pipe's name: what the subscriber's file says, a bare name on Windows and the socket's path elsewhere.</summary>
    public string Name { get; }

    /// <summary>Where a client connects: the pipe's name on Windows, the socket's path elsewhere.</summary>
    public string Address { get; }

    /// <summary>A new pipe, served for this relay alone; elsewhere its socket inside <paramref name="subscribers"/>.</summary>
    public static RelayPipe Serve(string subscribers)
    {
        var name = "daoris-event-" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        if (OperatingSystem.IsWindows()) return new RelayPipe(name, name, null);
        var path = Path.Combine(subscribers, name);
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        socket.Bind(new UnixDomainSocketEndPoint(path));
        socket.Listen(Instances);
        return new RelayPipe(path, path, socket);
    }

    /// <summary>The next client, once a place is free: at most <see cref="Instances"/> are held at once, the one listening among them.</summary>
    public async Task<RelayClient> AcceptAsync(CancellationToken ct)
    {
        await _places.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (OperatingSystem.IsWindows()) return await ListenAsync(ct).ConfigureAwait(false);
            var accepted = await _socket!.AcceptAsync(ct).ConfigureAwait(false);
            return new RelayClient(new NetworkStream(accepted, ownsSocket: true), null, Free);
        }
        catch
        {
            _places.Release();
            throw;
        }
    }

    [SupportedOSPlatform("windows")]
    private async Task<RelayClient> ListenAsync(CancellationToken ct)
    {
        var server = NamedPipeServerStreamAcl.Create(
            Name, PipeDirection.InOut, Instances, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | (_first ? PipeOptions.FirstPipeInstance : PipeOptions.None), 0, 0, UserPipe.Security());
        _first = false;
        try
        {
            await server.WaitForConnectionAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            await server.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        int? pid = GetNamedPipeClientProcessId(server.SafePipeHandle, out var client) ? (int)client : null;
        return new RelayClient(server, pid, Free);
    }

    private void Free() => _places.Release();

    public ValueTask DisposeAsync()
    {
        if (_socket is not null)
        {
            _socket.Dispose();
            try
            {
                File.Delete(Address);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // The folder's own; the next relay's start removes what a dead relay left.
            }
        }

        return ValueTask.CompletedTask;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeClientProcessId(Microsoft.Win32.SafeHandles.SafePipeHandle pipe, out uint processId);
}
