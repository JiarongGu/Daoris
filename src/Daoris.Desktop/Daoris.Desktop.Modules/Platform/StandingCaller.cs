using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Daoris.Driver;
using Microsoft.Win32.SafeHandles;

namespace Daoris.Desktop;

/// <summary>Where a terminal's process stands, as the standing confirmation's judge reads it (the terminal-trust design §3.3 point 3).</summary>
public enum StandingPlace
{
    /// <summary>In none of the jobs any Daoris on this Windows session made for its agents, every one read, and nothing left untold.</summary>
    Outside,

    /// <summary>In one of those jobs: an agent's, or something an agent started.</summary>
    InAgentJob,

    /// <summary>Not known: a job that would not open or read, the namespace not read, an uncontained agent, an older instance, the client unknown.</summary>
    Untold,
}

/// <summary>The look the judge asks (<see cref="StandingCaller"/>); a test hands its own.</summary>
public interface IStandingCaller
{
    /// <summary>Where <paramref name="client"/> stands; <see cref="StandingPlace.Untold"/> wherever it cannot tell.</summary>
    StandingPlace Place(PipeClientProcess? client);
}

/// <summary>
/// A pipe's client, held by a handle from the connection's taking for the connection's life (the terminal-trust design §3.3 point 3,
/// Codex's finding 8): never a process id read again later, which may name another process. A process that would not open, or that was
/// created after the connection was taken, is unknown.
/// </summary>
public sealed class PipeClientProcess : IDisposable
{
    private PipeClientProcess(SafeProcessHandle? handle) => Handle = handle;

    /// <summary>The client's process, opened to query it and to see it end; null where it is unknown.</summary>
    public SafeProcessHandle? Handle { get; }

    /// <summary>No client: not Windows, or the system would not say.</summary>
    public static PipeClientProcess Unknown { get; } = new(null);

    /// <summary>The client of <paramref name="stream"/>, opened as the connection is taken.</summary>
    public static PipeClientProcess Of(NamedPipeServerStream stream)
    {
        if (!OperatingSystem.IsWindows()) return Unknown;
        try
        {
            return GetNamedPipeClientProcessId(stream.SafePipeHandle, out var pid) ? Of((int)pid, DateTime.UtcNow) : Unknown;
        }
        catch (Exception error) when (error is InvalidOperationException or ObjectDisposedException or EntryPointNotFoundException)
        {
            return Unknown;
        }
    }

    /// <summary>The process <paramref name="pid"/>, opened now and taken only where it was created no later than <paramref name="taken"/>.</summary>
    public static PipeClientProcess Of(int pid, DateTime taken)
    {
        if (!OperatingSystem.IsWindows()) return Unknown;
        var handle = OpenProcess(QueryLimited | Synchronize, false, (uint)pid);
        if (handle.IsInvalid)
        {
            handle.Dispose();
            return Unknown;
        }

        if (!GetProcessTimes(handle, out var created, out _, out _, out _) || DateTime.FromFileTimeUtc(created) > taken)
        {
            // A process id reused after the connection was taken names another process: unknown.
            handle.Dispose();
            return Unknown;
        }

        return new PipeClientProcess(handle);
    }

    /// <summary>Whether the client's process has ended (or is unknown).</summary>
    public bool Gone => Handle is not { IsInvalid: false, IsClosed: false } handle || WaitForSingleObject(handle, 0) == 0;

    public void Dispose()
    {
        if (!ReferenceEquals(this, Unknown)) Handle?.Dispose();
    }

    private const uint QueryLimited = 0x1000;
    private const uint Synchronize = 0x00100000;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint pid);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessTimes(SafeProcessHandle process, out long created, out long exited, out long kernel, out long user);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(SafeProcessHandle handle, uint milliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out uint clientProcessId);
}

/// <summary>
/// The judge's look at where a terminal's process stands (TERMTRUST1a part (ii), D201 point 4, the terminal-trust design §3.3 point 3):
/// every job of the agents' prefix, <c>Local\daoris-agents-&lt;process id&gt;</c>, in this Windows session's namespace, found by listing it
/// and asked by handle whether the client is in it; any <c>-open</c> event of the prefix, which a Daoris holds while one of its agents
/// runs uncontained; this process's own job; and every instance pipe of this session, whose server must hold a job of the prefix.
/// </summary>
/// <remarks>
/// <para><b>This only ever adds a card.</b> Being in none of those jobs never makes a caller the person (D156 §2.4): the person's grant
/// covers the press, and the jobs only take the cover from an agent. Anything it cannot tell is <see cref="StandingPlace.Untold"/>.</para>
///
/// <para><b>The prefix is part (i)'s</b> (<c>PieceLauncher.AgentsPrefix</c> and <c>AgentJobs.NameOf</c> on its branch): a constant both
/// sides need, kept here as <see cref="Prefix"/> with this comment naming the other copy. Until part (i) lands, this process holds no job
/// of the prefix, so <see cref="OwnContained"/> answers false and every press asks (D201's note on order).</para>
///
/// <para><b>Elevation</b> (§3.4): a judge that is not elevated cannot open an elevated Daoris's job, which answers untold; safe, and said.</para>
/// </remarks>
public sealed class StandingCaller : IStandingCaller
{
    /// <summary>What every agents' job, and its <c>-open</c> event, is named by (part (i)'s twin: <c>PieceLauncher.AgentsPrefix</c>).</summary>
    public const string Prefix = "daoris-agents-";

    /// <summary>What an uncontained agent's event adds to its process's job name.</summary>
    public const string OpenSuffix = "-open";

    /// <summary>
    /// Whether every agent this process runs is contained (§3.3 point 3): its own agents' job is in the listing, and its own <c>-open</c>
    /// event is not, which the listing already reads. Part (i) may hand a sharper one (its <c>AgentJobs.Shared.Uncontained</c>).
    /// </summary>
    public Func<StandingListing, bool> OwnContained { get; set; } =
        listing => listing.Jobs.Contains(Prefix + Environment.ProcessId.ToString(CultureInfo.InvariantCulture), StringComparer.OrdinalIgnoreCase);

    /// <summary>The pipe this instance serves, which the look never opens; null before it serves.</summary>
    public Func<string?> OwnPipe { get; set; } = () => null;

    /// <summary>How the namespace is listed: the system's, or a test's.</summary>
    public Func<StandingListing?> List { get; set; } = ListSession;

    /// <summary>How the instance pipes' servers are read: the system's, or a test's; null where one could not be read.</summary>
    public Func<string?, IReadOnlyList<int>?> Servers { get; set; } = InstanceServers;

    /// <summary>Whether a process is in a named job: true, false, or null where the job would not open or answer.</summary>
    public Func<SafeProcessHandle, string, bool?> InJob { get; set; } = IsInJob;

    public StandingPlace Place(PipeClientProcess? client)
    {
        if (!OperatingSystem.IsWindows()) return StandingPlace.Untold;
        if (client?.Handle is not { } handle || client.Gone) return StandingPlace.Untold;
        if (List() is not { } listing) return StandingPlace.Untold;
        if (listing.Open.Count > 0) return StandingPlace.Untold;
        if (!OwnContained(listing)) return StandingPlace.Untold;

        // An older Daoris beside this one (Fable's F9): a server with no job of the prefix runs its agents in none.
        if (Servers(OwnPipe()) is not { } servers) return StandingPlace.Untold;
        if (servers.Any(pid => !listing.Jobs.Contains(Prefix + pid.ToString(CultureInfo.InvariantCulture), StringComparer.OrdinalIgnoreCase)))
        {
            return StandingPlace.Untold;
        }

        foreach (var job in listing.Jobs)
        {
            switch (InJob(handle, job))
            {
                case null:
                    return StandingPlace.Untold;
                case true:
                    return StandingPlace.InAgentJob;
            }
        }

        return StandingPlace.Outside;
    }

    /// <summary>Whether <paramref name="name"/> is an agents' job's name: the prefix and a process id.</summary>
    public static bool IsJobName(string name) => name.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)
        && name.Length > Prefix.Length && name.AsSpan(Prefix.Length).ToString().All(char.IsAsciiDigit);

    /// <summary>Whether <paramref name="name"/> is an uncontained agent's event: a job's name and <see cref="OpenSuffix"/>.</summary>
    public static bool IsOpenName(string name) => name.EndsWith(OpenSuffix, StringComparison.OrdinalIgnoreCase)
        && IsJobName(name[..^OpenSuffix.Length]);

    /// <summary>This Windows session's named objects of the prefix, or null where they could not be listed.</summary>
    public static StandingListing? ListSession()
    {
        if (!OperatingSystem.IsWindows()) return null;
        int session;
        using (var self = Process.GetCurrentProcess()) session = self.SessionId;
        var folder = session == 0 ? @"\BaseNamedObjects" : $@"\Sessions\{session.ToString(CultureInfo.InvariantCulture)}\BaseNamedObjects";
        return ObjectNamespace.List(folder) is { } entries
            ? new StandingListing(
                [.. entries.Where(entry => entry.Type == "Job" && IsJobName(entry.Name)).Select(entry => entry.Name)],
                // An `-open` object of any type takes the cover: a job's name is no event's, and another type is still a Daoris saying so.
                [.. entries.Where(entry => IsOpenName(entry.Name)).Select(entry => entry.Name)])
            : null;
    }

    /// <summary>
    /// The server process of every instance pipe of this Windows session but <paramref name="own"/>, or null where one could not be read.
    /// A pipe's name is <c>daoris-&lt;home id&gt;-&lt;session&gt;</c> (<see cref="PipeName"/>); its server is read by a client's end, opened
    /// and closed at once, which that instance reads as a connection that said no hello.
    /// </summary>
    public static IReadOnlyList<int>? InstanceServers(string? own)
    {
        if (!OperatingSystem.IsWindows()) return null;
        int session;
        using (var self = Process.GetCurrentProcess()) session = self.SessionId;
        var suffix = "-" + session.ToString(CultureInfo.InvariantCulture);
        string[] names;
        try
        {
            names = [.. Directory.GetFiles(@"\\.\pipe\").Select(Path.GetFileName).OfType<string>()];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }

        var servers = new List<int>();
        foreach (var name in names)
        {
            if (!name.StartsWith(PipeName.Prefix, StringComparison.Ordinal) || !name.EndsWith(suffix, StringComparison.Ordinal)) continue;
            if (!HomeIdentity.IsId(name[PipeName.Prefix.Length..^suffix.Length]) || name == own) continue;
            using var pipe = CreateFileW(@"\\.\pipe\" + name, ReadAttributes, 0, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
            if (pipe.IsInvalid || !GetNamedPipeServerProcessId(pipe, out var pid)) return null;
            if (pid != (uint)Environment.ProcessId) servers.Add((int)pid);
        }

        return servers;
    }

    /// <summary>Whether <paramref name="process"/> is in the job named <paramref name="name"/>, asked by the job's handle.</summary>
    public static bool? IsInJob(SafeProcessHandle process, string name)
    {
        if (!OperatingSystem.IsWindows()) return null;
        using var job = OpenJobObjectW(JobQuery, false, @"Local\" + name);
        if (job.IsInvalid) return null;
        return IsProcessInJob(process, job, out var inside) ? inside : null;
    }

    private const uint JobQuery = 0x0004;
    private const uint ReadAttributes = 0x0080;
    private const uint OpenExisting = 3;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle OpenJobObjectW(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, string name);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsProcessInJob(SafeProcessHandle process, SafeFileHandle job, [MarshalAs(UnmanagedType.Bool)] out bool result);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFileW(
        string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeServerProcessId(SafeFileHandle pipe, out uint serverProcessId);
}

/// <summary>The named objects of the agents' prefix in a session's namespace: the jobs, and the <c>-open</c> events.</summary>
public sealed record StandingListing(IReadOnlyList<string> Jobs, IReadOnlyList<string> Open);

/// <summary>A folder of the object manager's namespace, listed (<c>NtOpenDirectoryObject</c>, <c>NtQueryDirectoryObject</c>).</summary>
internal static class ObjectNamespace
{
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
