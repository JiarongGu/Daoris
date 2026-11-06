#:project ../../../../Daoris.Desktop/Daoris.Desktop.Modules/Daoris.Desktop.Modules.csproj
// EVENTHOOK1a's M9 and M10 (the event hook design §7): pipes and the agents' job across elevations, in .NET, since Node can call
// neither InstancePipe.Security, OpenJobObject nor IsProcessInJob. The Node half is only the client and server being measured.
// Run by hand from the repository's root, in an elevated shell, never by a gate:
//   dotnet run --file src/Daoris.Cli/test/fixtures/event-hook/PipeAcross.cs --artifacts-path _fixtures/event-probe-build
// The side that is not elevated is started hidden with a restricted token of the probe's own, shaped as UAC's filtered token
// (Unelevated, below). It spends nothing; it starts no window and no console.

using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using Daoris.Desktop;

var role = args.Length > 0 ? args[0] : "matrix";
switch (role)
{
    case "serve": return Serve(args[1], args[2], args.Length > 3 ? args[3] : null);
    case "hold": return Hold(args[1], args[2], args[3], args[4], args[5]);
    case "whoami": File.WriteAllText(args[1], Elevation()); return 0;
}

var repo = Directory.GetCurrentDirectory();
var scratch = Path.Combine(repo, "_fixtures", "event-probe", "pipes-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
Directory.CreateDirectory(scratch);
var self = Environment.ProcessPath!;
var node = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';')
    .Select(folder => Path.Combine(folder.Trim(), "node.exe")).First(File.Exists);
var client = Path.Combine(scratch, "client.mjs");
var server = Path.Combine(scratch, "server.mjs");
File.WriteAllText(client, """
    import net from 'node:net';
    import { writeFileSync } from 'node:fs';
    const [pipe, out] = process.argv.slice(2);
    const done = (word) => { writeFileSync(out, word); process.exit(0); };
    setTimeout(() => {
      const socket = net.connect(pipe);
      socket.on('connect', () => setTimeout(() => { socket.end(); done('opened'); }, 3000));
      socket.on('error', (error) => done(`refused ${error.code}`));
    }, 700);
    setTimeout(() => done('timeout'), 15000);
    """);
File.WriteAllText(server, """
    import net from 'node:net';
    import { writeFileSync } from 'node:fs';
    const [pipe, out] = process.argv.slice(2);
    const listening = net.createServer((socket) => { writeFileSync(out, 'accepted'); socket.end(); process.exit(0); });
    listening.on('error', (error) => { writeFileSync(out, `failed ${error.code}`); writeFileSync(`${out}.ready`, '1'); process.exit(0); });
    listening.listen(pipe, () => writeFileSync(`${out}.ready`, '1'));
    setTimeout(() => { writeFileSync(out, 'no client'); process.exit(0); }, 15000);
    """);

var lines = new List<string>();
void Say(string line) { lines.Add(line); Console.WriteLine(line); }
string Read(string path) => File.Exists(path) ? File.ReadAllText(path).Trim() : "nothing written";
string Name() => "daoris-probe-" + Guid.NewGuid().ToString("n")[..12];
string Pipe(string name) => @"\\.\pipe\" + name;
void Ready(string path) { for (var i = 0; i < 150 && !File.Exists(path + ".ready"); i++) Thread.Sleep(100); }
string Out(string label) => Path.Combine(scratch, label);
string Quote(string value) => "\"" + value + "\"";

Say($"the probe: {Elevation()}");
var whoami = Out("whoami");
Wait(Unelevated($"{Quote(self)} whoami {Quote(whoami)}"));
Say($"the other side: {Read(whoami)}");

// M9a: a pipe made with InstancePipe.Security by a process that is not elevated, opened by Node from an elevated one.
{
    var name = Name();
    var served = Unelevated($"{Quote(self)} serve {name} {Quote(Out("m9a.server"))}");
    Ready(Out("m9a.server"));
    Elevated(node, client, Pipe(name), Out("m9a.client"));
    Wait(served);
    Say($"M9 InstancePipe.Security, server not elevated, Node client elevated: client {Read(Out("m9a.client"))}; server {Read(Out("m9a.server"))}");
}

// M9b: the reverse.
{
    var name = Name();
    var served = Start(self, "serve", name, Out("m9b.server"));
    Ready(Out("m9b.server"));
    Wait(Unelevated($"{Quote(node)} {Quote(client)} {Pipe(name)} {Quote(Out("m9b.client"))}"));
    served.WaitForExit(20000);
    Say($"M9 InstancePipe.Security, server elevated, Node client not elevated: client {Read(Out("m9b.client"))}; server {Read(Out("m9b.server"))}");
}

// M9c and M9d: a Node-made pipe across elevations, to confirm the failure.
{
    var name = Name();
    var served = Start(node, server, Pipe(name), Out("m9c.server"));
    Ready(Out("m9c.server"));
    Wait(Unelevated($"{Quote(node)} {Quote(client)} {Pipe(name)} {Quote(Out("m9c.client"))}"));
    served.WaitForExit(20000);
    Say($"M9 a Node-made pipe, server elevated, Node client not elevated: client {Read(Out("m9c.client"))}; server {Read(Out("m9c.server"))}");
}

{
    var name = Name();
    var served = Unelevated($"{Quote(node)} {Quote(server)} {Pipe(name)} {Quote(Out("m9d.server"))}");
    Ready(Out("m9d.server"));
    Elevated(node, client, Pipe(name), Out("m9d.client"));
    Wait(served);
    Say($"M9 a Node-made pipe, server not elevated, Node client elevated: client {Read(Out("m9d.client"))}; server {Read(Out("m9d.server"))}");
}

// M10e: a relay that is not elevated; the holder, its agents' job and the client in it, elevated.
{
    var name = Name();
    var job = @"Local\" + Name();
    var served = Unelevated($"{Quote(self)} serve {name} {Quote(Out("m10e.relay"))} {job}");
    Ready(Out("m10e.relay"));
    Start(self, "hold", job, Quote(node), Quote(client), Pipe(name), Out("m10e.client")).WaitForExit(25000);
    Wait(served);
    Say($"M10 relay not elevated, holder and client elevated: {Read(Out("m10e.relay")).Replace("\n", "; ")}; client {Read(Out("m10e.client"))}");
}

// M10f: the reverse.
{
    var name = Name();
    var job = @"Local\" + Name();
    var served = Start(self, "serve", name, Out("m10f.relay"), job);
    Ready(Out("m10f.relay"));
    Wait(Unelevated($"{Quote(self)} hold {job} {Quote(node)} {Quote(client)} {Pipe(name)} {Quote(Out("m10f.client"))}"));
    served.WaitForExit(25000);
    Say($"M10 relay elevated, holder and client not elevated: {Read(Out("m10f.relay")).Replace("\n", "; ")}; client {Read(Out("m10f.client"))}");
}

File.WriteAllText(Path.Combine(scratch, "..", Path.GetFileName(scratch) + ".txt"), string.Join("\n", lines) + "\n");
Directory.Delete(scratch, recursive: true);
return 0;

Process Start(string program, params string[] arguments)
{
    var start = new ProcessStartInfo(program) { UseShellExecute = false, CreateNoWindow = true };
    foreach (var argument in arguments) start.ArgumentList.Add(argument.Trim('"'));
    return Process.Start(start)!;
}

void Elevated(string program, params string[] arguments) => Start(program, arguments).WaitForExit(25000);

static string Elevation()
{
    using var identity = WindowsIdentity.GetCurrent();
    return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator) ? "elevated" : "not elevated";
}

static int Serve(string name, string output, string? job)
{
    var said = new List<string>();
    using var pipe = NamedPipeServerStreamAcl.Create(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 0, 0,
        InstancePipe.Security());
    File.WriteAllText(output + ".ready", "1");
    if (!pipe.WaitForConnectionAsync().Wait(20000))
    {
        File.WriteAllText(output, "no client");
        return 0;
    }

    said.Add("connected");
    if (job is not null)
    {
        var read = Native.GetNamedPipeClientProcessId(pipe.SafePipeHandle, out var id);
        said.Add(read ? "client pid read" : $"client pid refused ({Marshal.GetLastWin32Error()})");
        var process = read ? Native.OpenProcess(0x1000, false, (int)id) : IntPtr.Zero;
        said.Add(process != IntPtr.Zero ? "client process opened for query" : $"client process refused ({Marshal.GetLastWin32Error()})");
        var held = Native.OpenJobObjectW(0x4, false, job);
        said.Add(held != IntPtr.Zero ? "the holder's job opened for query" : $"the holder's job refused ({Marshal.GetLastWin32Error()})");
        if (process != IntPtr.Zero && held != IntPtr.Zero)
            said.Add(Native.IsProcessInJob(process, held, out var inside) ? $"IsProcessInJob {inside}" : $"IsProcessInJob failed ({Marshal.GetLastWin32Error()})");
        else
            said.Add("NamedHolds would answer null: the client is taken untold");
    }

    File.WriteAllText(output, string.Join("\n", said));
    return 0;
}

static int Hold(string job, string node, string client, string pipe, string output)
{
    var handle = Native.CreateJobObjectW(IntPtr.Zero, job);
    var start = new ProcessStartInfo(node.Trim('"')) { UseShellExecute = false, CreateNoWindow = true };
    start.ArgumentList.Add(client.Trim('"'));
    start.ArgumentList.Add(pipe);
    start.ArgumentList.Add(output);
    using var started = Process.Start(start)!;
    Native.AssignProcessToJobObject(handle, started.Handle);
    started.WaitForExit(20000);
    return 0;
}

// The side that is not elevated: UAC's filtered token's shape made from the probe's own (a machine with UAC off has no other): the
// Administrators group deny-only, no privilege but change-notify, medium integrity, the user its default owner and its default DACL
// the user's and SYSTEM's. A restricted version of the caller's own token needs no privilege to start a process with.
static IntPtr Unelevated(string commandLine)
{
    if (!Native.OpenProcessToken(Process.GetCurrentProcess().Handle, 0x1 | 0x2 | 0x8 | 0x80, out var token)) throw new InvalidOperationException("the token");
    var admins = Sid(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null));
    var disabled = new Native.SidAndAttributes { Sid = admins, Attributes = 0 };
    if (!Native.CreateRestrictedToken(token, 0x1, 1, [disabled], 0, IntPtr.Zero, 0, IntPtr.Zero, out var restricted))
        throw new InvalidOperationException($"the restricted token ({Marshal.GetLastWin32Error()})");
    using var identity = WindowsIdentity.GetCurrent();
    var user = Sid(identity.User!);
    var medium = Sid(new SecurityIdentifier("S-1-16-8192"));
    Set(restricted, 25, [medium, 0x20], 16);
    Set(restricted, 4, [user], 8);
    var acl = new System.Security.AccessControl.RawAcl(2, 2);
    acl.InsertAce(0, new System.Security.AccessControl.CommonAce(0, System.Security.AccessControl.AceQualifier.AccessAllowed,
        unchecked((int)0x10000000), identity.User!, false, null));
    acl.InsertAce(1, new System.Security.AccessControl.CommonAce(0, System.Security.AccessControl.AceQualifier.AccessAllowed,
        unchecked((int)0x10000000), new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), false, null));
    var bytes = new byte[acl.BinaryLength];
    acl.GetBinaryForm(bytes, 0);
    var aclMemory = Marshal.AllocHGlobal(bytes.Length);
    Marshal.Copy(bytes, 0, aclMemory, bytes.Length);
    Set(restricted, 6, [aclMemory], 8);
    var startup = new Native.StartupInfo { Size = Marshal.SizeOf<Native.StartupInfo>(), Flags = 0x1, ShowWindow = 0 };
    if (!Native.CreateProcessAsUserW(restricted, null, new StringBuilder(commandLine), IntPtr.Zero, IntPtr.Zero, false, 0x08000000,
            IntPtr.Zero, null, ref startup, out var started))
        throw new InvalidOperationException($"the start ({Marshal.GetLastWin32Error()})");
    Native.CloseHandle(started.Thread);
    return started.Process;
}

static IntPtr Sid(SecurityIdentifier sid)
{
    var bytes = new byte[sid.BinaryLength];
    sid.GetBinaryForm(bytes, 0);
    var memory = Marshal.AllocHGlobal(bytes.Length);
    Marshal.Copy(bytes, 0, memory, bytes.Length);
    return memory;
}

// One token class set from a struct of pointer-sized words: TokenIntegrityLevel (25), TokenOwner (4), TokenDefaultDacl (6).
static void Set(IntPtr token, int kind, IntPtr[] words, int size)
{
    var memory = Marshal.AllocHGlobal(16);
    for (var i = 0; i < words.Length; i++) Marshal.WriteIntPtr(memory, i * IntPtr.Size, words[i]);
    if (!Native.SetTokenInformation(token, kind, memory, size)) throw new InvalidOperationException($"token class {kind} ({Marshal.GetLastWin32Error()})");
}

static void Wait(IntPtr process) => Native.WaitForSingleObject(process, 30000);

static class Native
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct StartupInfo
    {
        public int Size;
        public string? Reserved, Desktop, Title;
        public int X, Y, XSize, YSize, XCountChars, YCountChars, FillAttribute, Flags;
        public short ShowWindow, Reserved2Size;
        public IntPtr Reserved2, StdInput, StdOutput, StdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ProcessInformation
    {
        public IntPtr Process, Thread;
        public int ProcessId, ThreadId;
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);

    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool DuplicateTokenEx(IntPtr token, uint access, IntPtr attributes, int level, int type, out IntPtr duplicate);

    [StructLayout(LayoutKind.Sequential)]
    public struct SidAndAttributes
    {
        public IntPtr Sid;
        public uint Attributes;
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool CreateRestrictedToken(IntPtr token, uint flags, uint disableCount, SidAndAttributes[] disable, uint deleteCount,
        IntPtr delete, uint restrictCount, IntPtr restrict, out IntPtr restricted);

    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool SetTokenInformation(IntPtr token, int kind, IntPtr information, int length);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool CreateProcessAsUserW(IntPtr token, string? application, StringBuilder commandLine, IntPtr processAttributes,
        IntPtr threadAttributes, bool inherit, uint flags, IntPtr environment, string? directory, ref StartupInfo startup,
        out ProcessInformation started);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr OpenProcess(uint access, bool inherit, int id);

    [DllImport("kernel32.dll")]
    public static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

    [DllImport("kernel32.dll")]
    public static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr OpenJobObjectW(uint access, bool inherit, string name);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr CreateJobObjectW(IntPtr attributes, string name);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool IsProcessInJob(IntPtr process, IntPtr job, out bool result);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetNamedPipeClientProcessId(Microsoft.Win32.SafeHandles.SafePipeHandle pipe, out uint id);
}
