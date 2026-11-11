namespace Daoris.Desktop.Driver.Tests;

/// <summary>`daoris tree` as the host source says it: the console that dispatches the verbs, then the files its verbs and its usage are in.</summary>
internal static class TreesConsoleSource
{
    /// <summary>
    /// The console's source and its verbs', in one text; <paramref name="hostFolder"/> is the driver host's folder. Each verb is a
    /// <c>Trees*Verb.cs</c> of its own, found by that shape so a verb split out of the console is read with it.
    /// </summary>
    public static string Read(string hostFolder)
    {
        var verbs = Directory.GetFiles(hostFolder, "Trees*Verb.cs").Select(Path.GetFileName).OfType<string>().Order(StringComparer.Ordinal);
        string[] parts = ["TreesConsole.cs", .. verbs, "TreesUsage.cs"];
        return string.Concat(parts.Select(part => File.ReadAllText(Path.Combine(hostFolder, part)) + "\n"));
    }
}
