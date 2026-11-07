using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace Daoris.Driver;

/// <summary>A folder of the object manager's namespace, listed (<c>NtOpenDirectoryObject</c>, <c>NtQueryDirectoryObject</c>).</summary>
public static class ObjectNamespace
{
    /// <summary>The folder of this Windows session's named objects.</summary>
    public static string SessionFolder()
    {
        int session;
        using (var self = System.Diagnostics.Process.GetCurrentProcess()) session = self.SessionId;
        return session == 0 ? @"\BaseNamedObjects" : $@"\Sessions\{session.ToString(CultureInfo.InvariantCulture)}\BaseNamedObjects";
    }

    /// <summary>Each object's name and type in <paramref name="folder"/>, or null where it could not be listed whole.</summary>
    [SupportedOSPlatform("windows")]
    public static IReadOnlyList<(string Name, string Type)>? List(string folder)
    {
        var name = new UnicodeString(folder);
        try
        {
            var attributes = new ObjectAttributes
            {
                Length = Marshal.SizeOf<ObjectAttributes>(),
                ObjectName = name.Pointer,
                Attributes = CaseInsensitive,
            };
            if (NtOpenDirectoryObject(out var directory, DirectoryQuery, ref attributes) != 0) return null;
            using (directory)
            {
                var entries = new List<(string, string)>();
                var buffer = Marshal.AllocHGlobal(BufferBytes);
                try
                {
                    uint context = 0;
                    var first = true;
                    while (true)
                    {
                        var status = NtQueryDirectoryObject(directory, buffer, BufferBytes, false, first, ref context, out _);
                        first = false;
                        if (status == NoMoreEntries) return entries;
                        // More entries than the buffer holds is still a success: the rest come on the next call.
                        if (status != 0 && status != MoreEntries) return null;
                        var size = Marshal.SizeOf<DirectoryInformation>();
                        for (var at = buffer; ; at += size)
                        {
                            var entry = Marshal.PtrToStructure<DirectoryInformation>(at);
                            if (entry.Name.Buffer == IntPtr.Zero) break;
                            entries.Add((Text(entry.Name), Text(entry.TypeName)));
                        }
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
        }
        finally
        {
            name.Dispose();
        }
    }

    private static string Text(NativeString text) =>
        text.Buffer == IntPtr.Zero ? "" : Marshal.PtrToStringUni(text.Buffer, text.Length / 2);

    private const int BufferBytes = 64 * 1024;
    private const uint DirectoryQuery = 0x0001;
    private const uint CaseInsensitive = 0x40;
    private const int MoreEntries = 0x00000105;
    private const int NoMoreEntries = unchecked((int)0x8000001A);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeString
    {
        public ushort Length;
        public ushort MaximumLength;
        public IntPtr Buffer;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DirectoryInformation
    {
        public NativeString Name;
        public NativeString TypeName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ObjectAttributes
    {
        public int Length;
        public IntPtr RootDirectory;
        public IntPtr ObjectName;
        public uint Attributes;
        public IntPtr SecurityDescriptor;
        public IntPtr SecurityQualityOfService;
    }

    /// <summary>A native counted string over memory of its own, freed once disposed.</summary>
    private sealed class UnicodeString : IDisposable
    {
        private readonly IntPtr _chars;

        public UnicodeString(string text)
        {
            _chars = Marshal.StringToHGlobalUni(text);
            Pointer = Marshal.AllocHGlobal(Marshal.SizeOf<NativeString>());
            Marshal.StructureToPtr(
                new NativeString { Length = (ushort)(text.Length * 2), MaximumLength = (ushort)(text.Length * 2 + 2), Buffer = _chars },
                Pointer, false);
        }

        public IntPtr Pointer { get; }

        public void Dispose()
        {
            Marshal.FreeHGlobal(Pointer);
            Marshal.FreeHGlobal(_chars);
        }
    }

    [DllImport("ntdll.dll")]
    private static extern int NtOpenDirectoryObject(out SafeFileHandle directory, uint access, ref ObjectAttributes attributes);

    [DllImport("ntdll.dll")]
    private static extern int NtQueryDirectoryObject(
        SafeFileHandle directory, IntPtr buffer, int length, [MarshalAs(UnmanagedType.U1)] bool single,
        [MarshalAs(UnmanagedType.U1)] bool restart, ref uint context, out uint returned);
}

/// <summary>
/// Whether this process is in an agents' job, read as the pipe reads a role act's caller: every job of the agents' prefix in this
/// Windows session's namespace, asked whether it holds this process. A verb that writes the person's roles refuses where one does.
/// </summary>
public static class OwnAgentJob
{
    /// <summary>True where an agents' job holds this process, false where none does, null where that cannot be told (off Windows among it).</summary>
    public static bool? Holds()
    {
        if (!OperatingSystem.IsWindows() || ObjectNamespace.List(ObjectNamespace.SessionFolder()) is not { } entries) return null;
        var prefix = PieceLauncher.AgentsPrefix[@"Local\".Length..];
        using var self = System.Diagnostics.Process.GetCurrentProcess();
        bool? held = false;
        foreach (var job in entries.Where(entry => entry.Type == "Job" && entry.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            switch (ProcessJob.NamedHolds(@"Local\" + job.Name, self.Handle))
            {
                case true: return true;
                case null: held = null; break;
            }
        }

        return held;
    }
}
