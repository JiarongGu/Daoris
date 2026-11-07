namespace Daoris.Driver;

/// <summary>
/// What a trace names of the files a spawn is handed: the folder under the home
/// they sit in and the entry a rules file names for the harness's own hard denials. The agents write them (<see cref="SpawnServers"/>,
/// <see cref="SpawnSettings"/>) and spell them by these, which the trace, below them, reads; one spelling for both.
/// </summary>
public static class TraceSpawnFiles
{
    /// <summary>The folder under the home that holds the per-session files.</summary>
    public const string Folder = "spawn";

    /// <summary>The harness's own hard denials, kept by naming them in the list.</summary>
    public const string HarnessDefaults = "$defaults";
}
