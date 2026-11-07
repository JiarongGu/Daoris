using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Http;

namespace Daoris.Knowledge.Http;

/// <summary>
/// The process on the other end of a loopback connection: the owner of the TCP row whose local port is the
/// connection's remote port. A session can leave its name off an ask, and the terminal names none, so the process is the only thing
/// the host can tell askers apart by (the person-door design §4.1). Read on Windows, the desktop's own platform; elsewhere, and for any
/// connection not loopback or not found, null: the asker is then bound as the terminal's one asker is.
/// </summary>
public static class AskerProcess
{
    private const int TcpTableOwnerPidAll = 5;

    /// <summary>The id of the process holding the connection's far end, or null when it cannot be read.</summary>
    public static int? Of(ConnectionInfo connection)
    {
        if (!OperatingSystem.IsWindows()) return null;
        if (connection.RemoteIpAddress is not { } address || !IPAddress.IsLoopback(address) || connection.RemotePort <= 0) return null;
        try
        {
            var v6 = address.AddressFamily == AddressFamily.InterNetworkV6 && !address.IsIPv4MappedToIPv6;
            return Owner(v6, connection.RemotePort);
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }

    private static int? Owner(bool v6, int port)
    {
        var family = v6 ? 23 : 2;
        var size = 0;
        _ = GetExtendedTcpTable(IntPtr.Zero, ref size, false, family, TcpTableOwnerPidAll, 0);
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var buffer = Marshal.AllocHGlobal(size);
            try
            {
                var result = GetExtendedTcpTable(buffer, ref size, false, family, TcpTableOwnerPidAll, 0);
                if (result == 122) { size += 4096; continue; }
                if (result != 0) return null;

                // Rows follow a count: v4 rows are six words (state, local address, local port, remote address, remote port, pid),
                // v6 rows carry a 16-byte address and a scope id on each side. The port is network order in the low word.
                var count = Marshal.ReadInt32(buffer);
                var rowSize = v6 ? 56 : 24;
                var localPort = v6 ? 20 : 8;
                var pid = v6 ? 52 : 20;
                for (var row = 0; row < count; row++)
                {
                    var at = buffer + 4 + (row * rowSize);
                    var raw = Marshal.ReadInt32(at, localPort);
                    var found = ((raw & 0xFF) << 8) | ((raw >> 8) & 0xFF);
                    if (found == port) return Marshal.ReadInt32(at, pid);
                }

                return null;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        return null;
    }

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(IntPtr table, ref int size, bool sort, int family, int tableClass, uint reserved);
}
