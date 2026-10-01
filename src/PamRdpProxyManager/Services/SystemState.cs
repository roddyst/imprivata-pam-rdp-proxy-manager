using System.Net.Sockets;
using System.Runtime.InteropServices;
using static PamRdpProxyManager.Services.NativeMethods;

namespace PamRdpProxyManager.Services;

/// <summary>Queries of the Windows state used for security decisions (no admin rights required).</summary>
public static class SystemState
{
    /// <summary>Time since the last keyboard or mouse input in this Windows session (also inside mstsc).</summary>
    public static TimeSpan IdleTime
    {
        get
        {
            var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
            if (!GetLastInputInfo(ref info))
            {
                return TimeSpan.Zero;
            }

            // Both are 32-bit tick counts; unchecked subtraction handles the wrap-around after 49.7 days.
            return TimeSpan.FromMilliseconds(unchecked((uint)Environment.TickCount - info.dwTime));
        }
    }

    /// <summary>
    /// <c>true</c> if process <paramref name="processId"/> has an established TCP connection to
    /// <paramref name="remotePort"/>, <c>false</c> if not, <c>null</c> if the connection table cannot be read.
    /// </summary>
    public static bool? HasEstablishedTcpConnection(int processId, int remotePort)
    {
        var v4 = HasConnection(AddressFamily.InterNetwork, processId, remotePort);
        var v6 = HasConnection(AddressFamily.InterNetworkV6, processId, remotePort);
        return v4 == true || v6 == true ? true : v4 is null && v6 is null ? null : false;
    }

    private static bool? HasConnection(AddressFamily family, int processId, int remotePort)
    {
        // Row layouts of MIB_TCPROW_OWNER_PID / MIB_TCP6ROW_OWNER_PID (all fields are 4-byte aligned).
        var (rowSize, stateOffset, remotePortOffset, pidOffset) = family == AddressFamily.InterNetwork
            ? (24, 0, 16, 20)
            : (56, 48, 44, 52);

        var size = 0;
        var buffer = IntPtr.Zero;
        var read = false;
        try
        {
            for (var attempt = 0; attempt < 5; attempt++)
            {
                var result = GetExtendedTcpTable(buffer, ref size, false, (int)family, TCP_TABLE_OWNER_PID_ALL, 0);
                if (result == 0 && buffer != IntPtr.Zero)
                {
                    read = true;
                    break;
                }

                if (result is not (0 or ERROR_INSUFFICIENT_BUFFER))
                {
                    return null;
                }

                Marshal.FreeHGlobal(buffer);
                buffer = Marshal.AllocHGlobal(Math.Max(size, 4));
            }

            if (!read)
            {
                return null;
            }

            var count = Marshal.ReadInt32(buffer);
            for (var i = 0; i < count; i++)
            {
                var row = 4 + (i * rowSize);
                if (Marshal.ReadInt32(buffer, row + pidOffset) != processId || Marshal.ReadInt32(buffer, row + stateOffset) != MIB_TCP_STATE_ESTAB)
                {
                    continue;
                }

                // The port is stored in network byte order in the low 16 bits.
                var raw = Marshal.ReadInt32(buffer, row + remotePortOffset);
                if (((raw & 0xFF) << 8 | ((raw >> 8) & 0xFF)) == remotePort)
                {
                    return true;
                }
            }

            return false;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}
