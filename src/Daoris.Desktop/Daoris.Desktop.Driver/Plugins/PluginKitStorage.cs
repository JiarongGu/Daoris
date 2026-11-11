namespace Daoris.Driver;

/// <summary>
/// The kit's record store, which every plugin it writes keeps its records with: a JSON file written one record at a time under a lock,
/// so two processes of one plugin sharing a folder (the work processes of its places) never write over each other's records. The
/// kit's <c>work.mjs</c> keeps its journal in it; a plugin's commands keep what they sign in with in it, in their private storage.
/// </summary>
public static class PluginKitStorage
{
    public const string Library = "storage.mjs";

    /// <summary>Its test: two processes writing one file at once keep every record either wrote.</summary>
    public const string Test = "storage.test.mjs";

    /// <summary>Both files, the same for every plugin of this build.</summary>
    internal static IEnumerable<ScaffoldFile> Files() =>
        [new(Library, PluginKitFiles.Template(Library)), new(Test, PluginKitFiles.Template(Test))];
}
