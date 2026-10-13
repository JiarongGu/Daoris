using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// An install's update on the disk (UPDATE1, D139 §3; STRUCT3e): <c>$DAORIS_HOME/update.json</c> read, written and cleared,
/// and the install a home belongs to. Declared as <see cref="InstallUpdate"/>'s own static members (C# 14 extension blocks),
/// so the application, its update module and <c>daoris-driver update</c> keep their calls.
/// </summary>
public static class InstallUpdateFile
{
    extension(InstallUpdate)
    {
        /// <summary>The request, or null when there is none or it does not read: the default then holds. The launcher's reader.</summary>
        public static UpdateRequest? Read(string home) => UpdateRequests.Read(home);

        /// <summary>Write the request, atomically, LF; refused for a mode this build does not know.</summary>
        /// <exception cref="ArgumentException">A mode that is not one of <see cref="UpdateMode.All"/>.</exception>
        public static void Write(string home, UpdateRequest request)
        {
            if (!UpdateMode.All.Contains(request.Mode, StringComparer.Ordinal))
            {
                throw new ArgumentException($"`{request.Mode}` is not an update mode: one of {string.Join(", ", UpdateMode.All)}.");
            }

            var root = new JsonObject
            {
                ["mode"] = request.Mode,
                ["build"] = request.Build,
                ["at"] = (request.At ?? DateTimeOffset.UtcNow).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            };
            Directory.CreateDirectory(home);
            AtomicFile.WriteText(InstallUpdate.PathOf(home), root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }).Replace("\r\n", "\n") + "\n");
        }

        /// <summary>The request said and done with: removed as the application closes for the update, so it never outlives its build.</summary>
        public static void Clear(string home)
        {
            try
            {
                File.Delete(InstallUpdate.PathOf(home));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // A request naming an installed build holds for no later one (ModeFor), so a leftover changes nothing.
            }
        }

        /// <summary>
        /// The install a home belongs to (D63): the folder above it when that is a marked install and the home is its
        /// <c>data</c> — or null, for a home named for itself (a gate's, a terminal's), which a terminal points at with <c>--install</c>.
        /// </summary>
        public static string? InstallOf(string home)
        {
            var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(home));
            return string.Equals(Path.GetFileName(full), "data", StringComparison.OrdinalIgnoreCase)
                   && Path.GetDirectoryName(full) is { } parent
                   && File.Exists(Path.Combine(parent, StagedBuild.Marker))
                ? parent
                : null;
        }

        /// <summary>The install a terminal names (<c>--install &lt;folder&gt;</c>), as a full path: read against the working folder.</summary>
        public static string InstallAt(string folder) => Path.GetFullPath(folder);
    }
}
