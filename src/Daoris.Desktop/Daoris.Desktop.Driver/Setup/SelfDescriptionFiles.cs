using System.Text;

namespace Daoris.Driver;

/// <summary>
/// The files at a checkout's top that <see cref="SelfDescription"/> reads (D77), read from the disk (STRUCT3e): whether a
/// file of a name, or one matching a pattern, is there, and the top of a file, LF only. Read, never written (D32), and no
/// walk of the tree.
/// </summary>
public static class SelfDescriptionFiles
{
    /// <summary>What <paramref name="root"/> says about itself, or null when it says nothing a reader could use, or is not there.</summary>
    public static RepositoryDescription? Read(string root)
    {
        if (!Directory.Exists(root)) return null;

        return SelfDescription.Of(
            name => File.Exists(Path.Combine(root, name)),
            pattern => Any(root, pattern),
            name => Head(Path.Combine(root, name)));
    }

    private static bool Any(string root, string pattern)
    {
        try
        {
            return Directory.EnumerateFiles(root, pattern, SearchOption.TopDirectoryOnly).Any();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>The top of a file, LF only — a README's point is at its top.</summary>
    private static string Head(string file)
    {
        try
        {
            using var stream = File.OpenRead(file);
            var buffer = new byte[Math.Min(SelfDescription.ReadLimit, (int)Math.Min(stream.Length, SelfDescription.ReadLimit))];
            var read = stream.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false);
            return Encoding.UTF8.GetString(buffer, 0, read).Replace("\r\n", "\n").TrimStart('﻿');
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return "";
        }
    }
}
