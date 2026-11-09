using System.Diagnostics;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A running process holding one of a slot's files as a test host holds its assemblies: an assembly loaded by its path, any other file
/// opened for reading, then held until the bench stops it. The holder is a console program built once in the bench's scratch, outside
/// every tree, shielded from the checkout's build files.
/// </summary>
internal sealed class BenchHolder : IDisposable
{
    private readonly Process _process;

    private BenchHolder(Process process) => _process = process;

    /// <summary>The holder's program built in <paramref name="folder"/>; its entry assembly.</summary>
    internal static async Task<string> BuildAsync(string folder, BenchRuns run)
    {
        Directory.CreateDirectory(folder);
        foreach (var shield in new[] { "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props" })
        {
            await File.WriteAllTextAsync(Path.Combine(folder, shield), "<Project />\n");
        }

        await File.WriteAllTextAsync(Path.Combine(folder, "holder.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings></PropertyGroup>
            </Project>
            """);
        await File.WriteAllTextAsync(Path.Combine(folder, "Program.cs"),
            """
            object held = args[0].EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                ? System.Reflection.Assembly.LoadFrom(args[0])
                : File.Open(args[0], FileMode.Open, FileAccess.Read, FileShare.Read);
            Console.WriteLine("held " + held);
            Console.ReadLine();
            GC.KeepAlive(held);
            """);
        var (_, code) = await run.TimedAsync(folder, "dotnet", "build", "-nodeReuse:false", "--nologo", "-v", "q", "-o", Path.Combine(folder, "out"));
        if (code != 0) throw new InvalidOperationException("the holder did not build");
        return Path.Combine(folder, "out", "holder.dll");
    }

    /// <summary>The holder started on <paramref name="file"/>, returned once it says it holds it.</summary>
    internal static async Task<BenchHolder> HoldAsync(string holder, string file)
    {
        var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardInput = true, UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add(holder);
        start.ArgumentList.Add(file);
        var process = Process.Start(start)!;
        var said = await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromMinutes(1));
        if (said?.StartsWith("held", StringComparison.Ordinal) != true) throw new InvalidOperationException($"the holder did not hold {file}");
        return new BenchHolder(process);
    }

    internal int Id => _process.Id;

    public void Dispose()
    {
        if (!_process.HasExited) _process.Kill(entireProcessTree: true);
        _process.WaitForExit();
        _process.Dispose();
    }
}
