using System.Diagnostics;
using Daoris.Driver;

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
