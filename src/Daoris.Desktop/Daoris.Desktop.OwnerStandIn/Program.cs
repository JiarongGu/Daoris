using System.Diagnostics;
using Daoris.Driver;

// The entry's own strip, by name: this program references no product code.
Environment.SetEnvironmentVariable("DAORIS_ROLE_KEY", null);
Environment.SetEnvironmentVariable("CLAUDE_CODE_MESSAGING_SOCKET", null);
Environment.SetEnvironmentVariable("CLAUDE_CODE_MESSAGING_TOKEN", null);
// A lease left open on purpose, so a test that kills this process finds what a death inside the library leaves.
if (args[0] == "lease")
{
    new GitLibraryLeases(args[1], args[3]).Open(args[2]);
    Console.WriteLine("inside");
    Console.In.ReadToEnd();
    return 0;
}

// argv: the purpose, then the command and its arguments. Says the command's id and whether it joined its purpose's job, then the
// command's first line, and waits on its own input, so it ends when its test lets it go or is gone.
var purpose = Enum.Parse<ProcessPurpose>(args[0]);
var info = new ProcessStartInfo(args[1]) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
foreach (var word in args.Skip(2)) info.ArgumentList.Add(word);

using var child = Process.Start(info)!;
var joined = OwnedProcesses.Shared.Take(child, purpose);
Console.WriteLine($"{child.Id} {joined}");
Console.WriteLine(child.StandardOutput.ReadLine());
Console.In.ReadToEnd();
return 0;
