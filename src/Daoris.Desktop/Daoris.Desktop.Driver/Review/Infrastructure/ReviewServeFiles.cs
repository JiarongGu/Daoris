namespace Daoris.Driver;

/// <summary>
/// The folders the review's showing looks at (REVIEWENV1d; design §2.3 step 3): a step's build folder found in its tree and never
/// through a link, the head of its <c>index.html</c>, and whether a tree still stands.
/// </summary>
/// <remarks>
/// Review's Infrastructure (STRUCT3c, D166's note). <c>ReviewServed.FolderIn</c> is a static member of a C# 14 extension block on
/// the Domain's <see cref="ReviewServed"/>, whose shape of a folder's name, base and look are pure; <see cref="ReviewDesk"/> asks it.
/// </remarks>
internal static class ReviewServeFiles
{
    extension(ReviewServed)
    {
        /// <summary>
        /// The full path of <paramref name="folder"/> in <paramref name="tree"/>, or null with why not (design §2.3 step 3): a path
        /// from the tree's root with forward slashes, no <c>.</c> or <c>..</c>, and every folder on the way a folder of its own, never
        /// a link, as the service judges <c>review_serve</c>'s folder and a <c>${proof}</c> name is read (D146 §11).
        /// </summary>
        public static string? FolderIn(string tree, string folder, out string? why)
        {
            if (!ReviewServed.IsFolderName(folder))
            {
                why = "is not a folder of its tree named from the tree's root with forward slashes";
                return null;
            }

            var root = Path.GetFullPath(tree);
            var at = root;
            foreach (var segment in folder.Split('/'))
            {
                at = Path.Combine(at, segment);
                var found = new DirectoryInfo(at);
                if (!found.Exists)
                {
                    why = "is not a folder in its tree";
                    return null;
                }

                if (found.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    why = "is reached through a link, and a folder served is the tree's own";
                    return null;
                }
            }

            var full = Path.GetFullPath(at);
            if (!full.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                why = "is not inside its tree";
                return null;
            }

            why = null;
            return full;
        }
    }

    /// <summary>Whether a tree or a build's folder still stands on this machine.</summary>
    public static bool Stands(string folder) => Directory.Exists(folder);

    /// <summary>The first part of a build's <c>index.html</c> in <paramref name="folder"/>, where its <c>&lt;base&gt;</c> is; null where there is none.</summary>
    public static string? IndexHead(string folder)
    {
        var index = Path.Combine(folder, "index.html");
        return File.Exists(index) ? ReadHead(index) : null;
    }

    /// <summary>The first part of a build's <c>index.html</c>, where its <c>&lt;base&gt;</c> is: a head, never a whole bundle.</summary>
    private static string? ReadHead(string path)
    {
        try
        {
            using var reader = new StreamReader(path);
            var buffer = new char[64 * 1024];
            return new string(buffer, 0, reader.ReadBlock(buffer, 0, buffer.Length));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
