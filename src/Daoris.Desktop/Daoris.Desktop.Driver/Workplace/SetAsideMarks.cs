using System.Globalization;
using static Daoris.Driver.TreeGit;
using static Daoris.Driver.TreeWorktrees;

namespace Daoris.Driver;

/// <summary>What a folder moved aside carries: its moved-at mark and the record of the folders whose delete the purge could not finish.</summary>
internal static class SetAsideMarks
{
    /// <summary>The manifest marked <c>moved</c> once git let go of the tree; only one still at <c>judged</c>, never one made anew.</summary>
    internal static void MarkMoved(string? path)
    {
        if (path is null || SlimManifest.Read(path).Manifest is not { State: SlimStates.Judged } judged) return;
        try
        {
            (judged with { State = SlimStates.Moved, Moved = DateTimeOffset.UtcNow }).Write(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Left at `judged`, the next pass keeps the folder whole and drops it: the move stands, the slim does not happen.
        }
    }

    /// <summary>
    /// A finished tree's manifest marked at a step after its move (KEEPSIZE1b, the keep-size design §3.5): <c>deleted</c> with when, or
    /// <c>refused</c>. Only a schema-2 manifest at <c>moved</c>, <c>judged</c> or <c>refused</c>; one that does not write stays as it was, and
    /// the resume reads git again at the next pass.
    /// </summary>
    internal static void Mark(string path, string state, DateTimeOffset at)
    {
        if (SlimManifest.Read(path).Manifest is not { Finished: not null, State: SlimStates.Moved or SlimStates.Judged or SlimStates.Refused } manifest) return;
        try
        {
            (manifest with { State = state, Deleted = state == SlimStates.Deleted ? at : manifest.Deleted, Why = null }).Write(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Left as it was: the resume finds the branch gone at the next pass and marks it then.
        }
    }

    /// <summary>The manifest of a move refused or a folder put back, gone with the attempt (Codex 2.4).</summary>
    internal static void Forget(string? path)
    {
        // Left at `judged`, the next pass drops it and keeps any folder beside it whole.
        if (path is not null) TryDelete(path);
    }

    /// <summary>A file deleted where it can be; one that cannot is left for a later pass, which judges it again.</summary>
    internal static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Left for the next pass.
        }
    }

    /// <summary>When a folder moved aside was moved, read from its name (<c>&lt;name&gt;-&lt;moment&gt;</c>); null where its name says none.</summary>
    internal static DateTimeOffset? MovedAt(string name)
    {
        var dash = name.LastIndexOf('-');
        return dash >= 0 && DateTimeOffset.TryParseExact(
            name[(dash + 1)..], TreeSetAside.SetAsideStamp, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var moved)
            ? moved
            : null;
    }

    /// <summary>A finished folder's manifest marked <c>unmarked</c>, saying <paramref name="why"/>; one that does not read, or is no finished tree's, is left as it is.</summary>
    internal static void Unmark(string folder, string why)
    {
        var path = SlimManifest.PathFor(folder);
        if (SlimManifest.Read(path).Manifest is not { Finished: not null } manifest) return;
        try
        {
            (manifest with { State = SlimStates.Unmarked, Why = why }).Write(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Read again at the next pass: its late write, or its holding check, keeps it the same way.
        }
    }

    /// <summary><see cref="FailedPath"/> of one place's <c>.tidied</c> (KEEPSIZE1a): each place keeps its own, naming its folders from it.</summary>
    internal static string FailedPathOf(string root) => Path.Combine(root, "purge-failed.json");

    /// <summary>
    /// The folders this pass could not delete, kept where the inventory reads them; written only where they changed, gone where none. A
    /// folder that failed the same way twice carries <c>tried</c>, when (KEEP1a: it waits a day from there), a field added after the rest.
    /// </summary>
    internal static void KeepFailed(string root, IReadOnlyList<(string Folder, string Why, DateTimeOffset? Tried)> failed)
    {
        var path = FailedPathOf(root);
        try
        {
            if (failed.Count == 0)
            {
                if (File.Exists(path)) File.Delete(path);
                return;
            }

            var text = JsonFile.Serialize(writer =>
            {
                writer.WriteStartObject();
                writer.WriteStartArray("folders");
                foreach (var (folder, why, tried) in failed.OrderBy(each => each.Folder, StringComparer.Ordinal))
                {
                    writer.WriteStartObject();
                    writer.WriteString("folder", Path.GetRelativePath(root, folder).Replace('\\', '/'));
                    writer.WriteString("why", why);
                    if (tried is { } at) writer.WriteString("tried", at.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture));
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            });
            if (File.Exists(path) && AtomicFile.ReadText(path) == text) return;
            AtomicFile.WriteText(path, text);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // The purge's own report still says each folder it kept; the inventory then reads it as before.
        }
    }

    /// <summary>
    /// <see cref="Failed"/>'s folders under one place's <c>.tidied</c>, each with when it last failed as before, where it did (KEEP1a's
    /// day's wait).
    /// </summary>
    internal static Dictionary<string, (string? Why, DateTimeOffset? Tried)> FailedEntries(string root)
    {
        var found = new Dictionary<string, (string? Why, DateTimeOffset? Tried)>(StringComparer.OrdinalIgnoreCase);
        var path = FailedPathOf(root);
        try
        {
            if (!File.Exists(path)) return found;
            using var document = System.Text.Json.JsonDocument.Parse(AtomicFile.ReadText(path));
            if (!document.RootElement.TryGetProperty("folders", out var folders) || folders.ValueKind != System.Text.Json.JsonValueKind.Array) return found;
            foreach (var each in folders.EnumerateArray())
            {
                if (each.ValueKind == System.Text.Json.JsonValueKind.Object
                    && each.TryGetProperty("folder", out var folder) && folder.GetString() is { Length: > 0 } named
                    && each.TryGetProperty("why", out var why) && why.GetString() is { Length: > 0 } said)
                {
                    var tried = each.TryGetProperty("tried", out var at) && at.ValueKind == System.Text.Json.JsonValueKind.String
                                && DateTimeOffset.TryParse(at.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var when)
                        ? when
                        : (DateTimeOffset?)null;
                    found[Path.GetFullPath(Path.Combine(root, named))] = (said, tried);
                }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException)
        {
            // A record that does not read claims nothing: each folder reads as its own read says.
        }

        return found;
    }
}
