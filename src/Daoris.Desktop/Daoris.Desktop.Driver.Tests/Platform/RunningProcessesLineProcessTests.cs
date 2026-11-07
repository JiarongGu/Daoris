using System.Diagnostics;
using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>D249: the census is read as a process stops. The line names each purpose that still holds a process, by id, and is not written where none does.</summary>
[Trait(Category.Name, Category.Process)]
public sealed class RunningProcessesLineProcessTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-running-line-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Written(MachineLog log)
    {
        log.Dispose();
        return Directory.Exists(log.Location) ? string.Join('\n', Directory.EnumerateFiles(log.Location, "*.jsonl").Select(File.ReadAllText)) : "";
    }

    [Fact]
    public async Task A_process_still_running_is_named_by_its_purpose_and_id_in_the_line()
    {
        if (!OperatingSystem.IsWindows()) return;
        var info = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"))
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
            StandardInputEncoding = new UTF8Encoding(false),
        };
        foreach (var word in new[] { "/d", "/c", "set /p line=" }) info.ArgumentList.Add(word);
        using var process = (await ProcessStarts.Shared.StartAsync(info, ProcessPurpose.Read, StartClass.Background, CancellationToken.None))!;
        try
        {
            var log = new MachineLog(_home, "driver");
            RunningProcessesLine.Write(log, OwnedProcesses.Shared);
            var text = Written(log);

            Assert.Contains($"\"event\":\"{RunningProcessesLine.Event}\"", text);
            Assert.Contains(process.Id.ToString(), text);
            Assert.Contains("\"read\":", text);
        }
        finally
        {
            process.StandardInput.Close();
            await process.WaitForExitAsync().WaitAsync(Poll.Patience);
        }
    }

    [Fact]
    public void Nothing_is_written_where_no_purpose_holds_a_process()
    {
        var log = new MachineLog(_home, "driver");
        RunningProcessesLine.Write(log, new OwnedProcesses());

        Assert.DoesNotContain(RunningProcessesLine.Event, Written(log));
    }
}
