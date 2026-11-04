namespace Daoris.Desktop.Driver.Tests;

/// <summary>The driver host's program as its source says it: the entry that dispatches the verbs, then the file the headless loop is in.</summary>
internal static class HostProgramSource
{
    /// <summary>The program's source and the loop's, in one text; <paramref name="hostFolder"/> is the driver host's folder.</summary>
    public static string Read(string hostFolder) =>
        File.ReadAllText(Path.Combine(hostFolder, "Program.cs")) + "\n" + File.ReadAllText(Path.Combine(hostFolder, "DriveConsole.cs")) + "\n";
}
