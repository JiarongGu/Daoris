namespace Daoris.Desktop.Driver.Tests;

/// <summary>`daoris tree` as the host source says it: the console that dispatches the verbs, then the files its verbs and its usage are in.</summary>
internal static class TreesConsoleSource
{
    private static readonly string[] Parts =
    [
        "TreesConsole.cs", "TreesCleanVerb.cs", "TreesEntryLandVerb.cs", "TreesHandVerb.cs", "TreesRemoveVerb.cs", "TreesStateVerb.cs", "TreesUsage.cs",
    ];

    /// <summary>The console's source and its verbs', in one text; <paramref name="hostFolder"/> is the driver host's folder.</summary>
    public static string Read(string hostFolder) => string.Concat(Parts.Select(part => File.ReadAllText(Path.Combine(hostFolder, part)) + "\n"));
}
