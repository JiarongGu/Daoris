using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// Reading <c>$DAORIS_HOME/update.json</c> (D139 §3), as <c>UpdateRequests.Read</c>: here so the launcher, which compiles
/// this file, reads it as the application does: one reader (STRUCT3e). Absent, unreadable or of a mode this build does not
/// know is null, and the default, when idle, then holds.
/// </summary>
public static class UpdateRequestFiles
{
    extension(UpdateRequests)
    {
        /// <summary>The request, or null when there is none or it does not read: the default, when idle, then holds.</summary>
        public static UpdateRequest? Read(string home)
        {
            var path = Path.Combine(home, UpdateRequests.FileName);
            if (!File.Exists(path)) return null;
            try
            {
                if (JsonNode.Parse(File.ReadAllText(path)) is not JsonObject root) return null;
                var mode = root["mode"] is JsonValue value && value.TryGetValue<string>(out var text) ? text.Trim() : null;
                if (mode is null || !UpdateMode.All.Contains(mode, StringComparer.Ordinal)) return null;
                var build = root["build"] is JsonValue named && named.TryGetValue<string>(out var id) && !string.IsNullOrWhiteSpace(id)
                    ? id.Trim()
                    : null;
                var at = root["at"] is JsonValue stamp && stamp.TryGetValue<string>(out var moment)
                         && DateTimeOffset.TryParse(moment, System.Globalization.CultureInfo.InvariantCulture,
                             System.Globalization.DateTimeStyles.RoundtripKind, out var parsed)
                    ? parsed
                    : (DateTimeOffset?)null;
                return new UpdateRequest(mode, build, at);
            }
            catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                return null;
            }
        }
    }
}

/// <summary>
/// The staged build and the swap's journal on the disk (UPDATE1, D139; STRUCT3e): the manifest read and the build checked
/// file by file against it, a file's SHA-256, the journal read and written, the build before cleared, what is staged
/// removed, and what runs from a folder. Declared as <see cref="StagedBuild"/>'s own static members (C# 14 extension
/// blocks), so the application, <c>daoris-driver update</c>, the swap and the launcher keep their calls.
/// </summary>
/// <remarks>
/// <b>Compiled into the launcher too</b> (<c>Daoris.Desktop.Launcher.csproj</c>), beside <see cref="StagedBuild"/> and
/// <see cref="InstallSwap"/>: so it uses nothing but the base library.
/// </remarks>
public static class StagedBuildFiles
{
    private static readonly StringComparison PathCase =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    extension(StagedBuild)
    {
        /// <summary>Whether a build is staged: its manifest is there, readable or not.</summary>
        public static bool IsStaged(string install) => File.Exists(Path.Combine(StagedBuild.StagedOf(install), StagedBuild.Manifest));

        /// <summary>The staged build's manifest, or null when none is staged or it does not read; <paramref name="problem"/> says which.</summary>
        public static StagedManifest? Read(string install, out StagedProblem? problem)
        {
            problem = null;
            var path = Path.Combine(StagedBuild.StagedOf(install), StagedBuild.Manifest);
            if (!File.Exists(path)) return null;

            try
            {
                var root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject;
                if (root is null)
                {
                    problem = new("manifest", $"{StagedBuild.Folder}/{StagedBuild.Staged}/{StagedBuild.Manifest} is not a JSON object.");
                    return null;
                }

                if (Number(root["schema"]) is not { } schema || schema != StagedBuild.Schema)
                {
                    problem = new("schema",
                        $"{StagedBuild.Folder}/{StagedBuild.Staged}/{StagedBuild.Manifest} is schema {root["schema"]?.ToJsonString() ?? "(none)"}, and this build reads schema {StagedBuild.Schema}.");
                    return null;
                }

                var id = Text(root["id"]);
                var version = Text(root["version"]);
                if (id is null || version is null || root["files"] is not JsonArray listed)
                {
                    problem = new("manifest", $"{StagedBuild.Folder}/{StagedBuild.Staged}/{StagedBuild.Manifest} names no id, version or files.");
                    return null;
                }

                var files = new List<StagedFile>();
                foreach (var entry in listed)
                {
                    if (entry is not JsonObject file || Text(file["path"]) is not { } filePath
                        || Number(file["size"]) is not { } size || Text(file["sha256"]) is not { } sha)
                    {
                        problem = new("manifest", $"{StagedBuild.Folder}/{StagedBuild.Staged}/{StagedBuild.Manifest} lists a file without its path, size or sha256.");
                        return null;
                    }

                    files.Add(new StagedFile(filePath, size, sha.ToLowerInvariant()));
                }

                var at = Text(root["at"]) is { } stamp && DateTimeOffset.TryParse(
                    stamp, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out var moment)
                    ? moment
                    : (DateTimeOffset?)null;
                return new StagedManifest(id, version, Text(root["commit"]), at, files);
            }
            catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException or FormatException)
            {
                problem = new("manifest", $"{StagedBuild.Folder}/{StagedBuild.Staged}/{StagedBuild.Manifest} could not be read ({error.Message.TrimEnd('.')}).");
                return null;
            }
        }

        /// <summary>
        /// Check the staged build before anything is replaced (D139 §4): its manifest reads at this schema, every file it names
        /// is there with its size and SHA-256, nothing is there it does not name, it carries <see cref="StagedBuild.Required"/>,
        /// and the host when the install carries one. Null when it may be installed, or when nothing is staged.
        /// </summary>
        public static StagedProblem? Check(string install)
        {
            var manifest = StagedBuild.Read(install, out var problem);
            if (problem is not null) return problem;
            if (manifest is null) return null;

            var staged = StagedBuild.StagedOf(install);
            var named = new HashSet<string>(StringComparer.FromComparison(PathCase));
            foreach (var file in manifest.Files)
            {
                if (!StagedBuild.Inside(file.Path))
                {
                    return new("path", $"the staged build's manifest names {file.Path}, which is not a path inside the build.");
                }

                named.Add(file.Path);
                var full = Path.Combine(staged, file.Path.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(full)) return new("missing", $"the staged build has no {file.Path}, which its manifest names.");

                var length = new FileInfo(full).Length;
                if (length != file.Size)
                {
                    return new("size", $"the staged build's {file.Path} is {length} bytes, and its manifest says {file.Size}.");
                }

                if (!string.Equals(StagedBuild.Sha256Of(full), file.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    return new("hash", $"the staged build's {file.Path} is not the file its manifest names: its SHA-256 differs.");
                }
            }

            foreach (var full in Directory.EnumerateFiles(staged, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(staged, full).Replace(Path.DirectorySeparatorChar, '/');
                if (string.Equals(relative, StagedBuild.Manifest, PathCase)) continue;
                if (!named.Contains(relative)) return new("unlisted", $"the staged build holds {relative}, which its manifest does not name.");
            }

            if (StagedBuild.Required.FirstOrDefault(required => !named.Contains(required)) is { } absent)
            {
                return new("required", $"the staged build carries no {absent}, which every build must.");
            }

            if (File.Exists(Path.Combine(install, StagedBuild.Host.Replace('/', Path.DirectorySeparatorChar))) && !named.Contains(StagedBuild.Host))
            {
                return new("host",
                    "this install carries its HTTP host and the staged build does not: stage it with --service, or the update would leave the install without one.");
            }

            return null;
        }

        /// <summary>A file's SHA-256, lower-case hex.</summary>
        public static string Sha256Of(string path)
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }

        /// <summary>The swap's journal, or null when there is none or it does not read.</summary>
        public static SwapRecord? ReadJournal(string install)
        {
            var path = StagedBuild.JournalOf(install);
            if (!File.Exists(path)) return null;
            try
            {
                // Read sharing every access, the delete too: the launcher reads at every poll while the new application writes
                // its confirmation, and a reader that shares less refuses that rename (SWAP2).
                string text;
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var reader = new StreamReader(stream))
                {
                    text = reader.ReadToEnd();
                }

                if (JsonNode.Parse(text) is not JsonObject root || Text(root["phase"]) is not { } phase) return null;
                var moves = new List<SwapMove>();
                if (root["moves"] is JsonArray listed)
                {
                    foreach (var entry in listed)
                    {
                        if (entry is JsonObject move && Text(move["from"]) is { } from && Text(move["to"]) is { } to) moves.Add(new SwapMove(from, to));
                    }
                }

                // Absent on a journal written before SWAP2, which reads as it did.
                List<SwapHold>? holds = null;
                if (root["holds"] is JsonArray held)
                {
                    holds = [];
                    foreach (var entry in held)
                    {
                        if (entry is JsonObject hold && Text(hold["from"]) is { } from && Text(hold["to"]) is { } to
                            && Number(hold["ms"]) is { } ms && Number(hold["tries"]) is { } tries)
                        {
                            holds.Add(new SwapHold(from, to, ms, (int)tries,
                                hold["released"] is JsonValue released && released.TryGetValue<bool>(out var gave) && gave));
                        }
                    }
                }

                var at = Text(root["at"]) is { } stamp && DateTimeOffset.TryParse(
                    stamp, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out var moment)
                    ? moment
                    : (DateTimeOffset?)null;
                return new SwapRecord(
                    phase, Text(root["id"]), Text(root["version"]), Text(root["commit"]), at, moves,
                    Number(root["pid"]) is { } pid ? (int)pid : null, Text(root["reason"]), Text(root["detail"]),
                    root["confirmed"] is JsonValue confirmed && confirmed.TryGetValue<bool>(out var flag) ? flag : null,
                    root["told"] is JsonValue told && told.TryGetValue<bool>(out var said) && said,
                    holds is { Count: > 0 } ? holds : null);
            }
            catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException or FormatException)
            {
                return null;
            }
        }

        /// <summary>Write the journal: beside, then renamed, so a reader never meets half of one.</summary>
        public static void WriteJournal(string install, SwapRecord record)
        {
            var path = StagedBuild.JournalOf(install);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var root = new JsonObject
            {
                ["schema"] = StagedBuild.Schema,
                ["phase"] = record.Phase,
                ["id"] = record.Id,
                ["version"] = record.Version,
                ["commit"] = record.Commit,
                ["at"] = record.At?.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
                ["moves"] = new JsonArray(record.Moves.Select(move => (JsonNode)new JsonObject { ["from"] = move.From, ["to"] = move.To }).ToArray()),
            };
            if (record.Pid is { } pid) root["pid"] = pid;
            if (record.Reason is { } reason) root["reason"] = reason;
            if (record.Detail is { } detail) root["detail"] = detail;
            if (record.Confirmed is { } confirmed) root["confirmed"] = confirmed;
            if (record.Told) root["told"] = true;
            if (record.Holds is { Count: > 0 } holds)
            {
                root["holds"] = new JsonArray(holds.Select(hold => (JsonNode)new JsonObject
                {
                    ["from"] = hold.From,
                    ["to"] = hold.To,
                    ["ms"] = hold.Ms,
                    ["tries"] = hold.Tries,
                    ["released"] = hold.Released,
                }).ToArray());
            }

            var staging = path + ".writing";
            File.WriteAllText(staging, root.ToJsonString(Indented).Replace("\r\n", "\n") + "\n", new System.Text.UTF8Encoding(false));
            Replace(staging, path);
        }

        /// <summary>Whether the journal says a swap is under way: moving, started, or confirmed and not yet finished.</summary>
        public static bool UnderWay(string install) =>
            StagedBuild.ReadJournal(install)?.Phase is SwapPhase.Swapping or SwapPhase.Started or SwapPhase.Confirmed;

        /// <summary>
        /// The new application says it came up (D139 §5): a journal at <see cref="SwapPhase.Started"/> becomes
        /// <see cref="SwapPhase.Confirmed"/>, which the waiting launcher reads. True when this start confirmed a swap, so the
        /// application says once that it was updated; false for every other start, and for a journal a roll-back stopped part
        /// way through, which carries its reason and is finished at the next start, never confirmed (SWAP2).
        /// </summary>
        public static bool Confirm(string install, int pid)
        {
            if (StagedBuild.ReadJournal(install) is not { Phase: SwapPhase.Started, Reason: null } record) return false;
            // Told as it is confirmed: this start is the one that says it, and the launcher keeps the mark as it finishes.
            StagedBuild.WriteJournal(install, record with { Phase = SwapPhase.Confirmed, Pid = pid, Told = true });
            return true;
        }

        /// <summary>Mark the journal's outcome told, so a later start does not say it again.</summary>
        public static void Tell(string install)
        {
            if (StagedBuild.ReadJournal(install) is { Told: false } record) StagedBuild.WriteJournal(install, record with { Told = true });
        }

        /// <summary>
        /// The build before a finished swap, removed. The launcher finishes with its own running launcher inside
        /// <c>update/previous/</c>, which Windows will not delete, so its own clearing leaves a whole build there; the application
        /// tries again at each look once the journal says the swap ended, never while one is under way. True when it is gone.
        /// </summary>
        public static bool ClearPrevious(string install)
        {
            var previous = Path.Combine(install, StagedBuild.Folder, StagedBuild.Previous);
            if (!Directory.Exists(previous)) return true;
            if (StagedBuild.ReadJournal(install) is not { Phase: SwapPhase.Installed or SwapPhase.RolledBack or SwapPhase.Refused }) return false;
            try
            {
                Directory.Delete(previous, recursive: true);
                return true;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Still held, the launcher not yet gone: the next look tries again.
                return false;
            }
        }

        /// <summary>Remove what is staged (a publish in place supersedes it), leaving the journal and the failed build to read.</summary>
        public static void Unstage(string install)
        {
            var staged = StagedBuild.StagedOf(install);
            if (Directory.Exists(staged)) Directory.Delete(staged, recursive: true);
        }

        /// <summary>Every process running from under <paramref name="folder"/>, by id — what the swap waits out (D139 §5).</summary>
        public static IReadOnlyList<int> RunningFrom(string folder)
        {
            var prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)) + Path.DirectorySeparatorChar;
            var found = new List<int>();
            foreach (var process in Process.GetProcesses())
            {
                using (process)
                {
                    try
                    {
                        if (process.MainModule?.FileName is { } file && file.StartsWith(prefix, PathCase)) found.Add(process.Id);
                    }
                    catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException)
                    {
                        // Another account's, or a process that ended as it was asked: not one of the install's.
                    }
                }
            }

            return found;
        }
    }

    /// <summary>
    /// The journal renamed into place, tried again a moment while a reader holds it open without delete sharing, which
    /// Windows refuses as access denied and which clears in milliseconds (AtomicFile.Replace's REV3 measurement, written here
    /// because the launcher compiles this file alone). A refused write would lose a phase the swap reached, or the new
    /// application's confirmation (SWAP2).
    /// </summary>
    private static void Replace(string staging, string path)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                File.Move(staging, path, overwrite: true);
                return;
            }
            catch (Exception busy) when (attempt < 40
                                          && (busy is UnauthorizedAccessException
                                              || busy is IOException and not FileNotFoundException and not DirectoryNotFoundException))
            {
                Thread.Sleep(5 + attempt * 5);
            }
        }
    }

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) ? text.Trim() : null;

    private static long? Number(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<long>(out var number) ? number
        : node is JsonValue other && other.TryGetValue<int>(out var small) ? small
        : null;
}
