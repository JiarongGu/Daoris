using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// A build the swap refused <c>busy</c>, set aside in <c>update/failed/</c> because something still ran from <c>app/</c> or the
/// home was held, staged again once it is not: the build is sound, only the moment was wrong, so nothing is left for a person to
/// run. Compiled into the launcher beside <see cref="StagedBuild"/>, so it uses the base library and that alone.
/// </summary>
public static class StagedRestage
{
    /// <summary>
    /// Whether the build the journal says was refused <c>busy</c> is staged again: only where nothing is staged and no swap is under way,
    /// and the build aside is that same one (its manifest's id is the journal's). A move that fails leaves what is left aside
    /// for the next start, which moves the rest, the manifest last so a half-staged build never reads as staged.
    /// </summary>
    public static bool Again(string install)
    {
        if (StagedBuild.IsStaged(install) || StagedBuild.UnderWay(install)) return false;
        if (StagedBuild.ReadJournal(install) is not { Phase: SwapPhase.Refused, Reason: "busy", Id: { } id }) return false;

        var aside = Path.Combine(install, StagedBuild.Folder, StagedBuild.Failed);
        var manifest = Path.Combine(aside, StagedBuild.Manifest);
        if (!File.Exists(manifest) || IdOf(manifest) != id) return false;

        var staged = StagedBuild.StagedOf(install);
        try
        {
            Directory.CreateDirectory(staged);
            foreach (var entry in Directory.EnumerateFileSystemEntries(aside).Where(entry => Path.GetFileName(entry) != StagedBuild.Manifest).ToList())
            {
                InstallSwap.MoveOnDisk(entry, Path.Combine(staged, Path.GetFileName(entry)), overwrite: true);
            }

            File.Move(manifest, Path.Combine(staged, StagedBuild.Manifest), overwrite: true);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string? IdOf(string manifest)
    {
        try
        {
            return JsonNode.Parse(File.ReadAllText(manifest)) is JsonObject root && root["id"] is JsonValue value && value.TryGetValue<string>(out var id) ? id : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            return null;
        }
    }
}
