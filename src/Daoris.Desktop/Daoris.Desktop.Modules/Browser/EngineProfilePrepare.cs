using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// What <c>daoris-browser</c> does to its profile before the engine reads it: Daoris's favorites become its folder on the bookmarks bar,
/// and other software's Chrome extensions are refused or offered as the settings say.
/// </summary>
/// <remarks>
/// <para><b>A file another program holds never stops the browser.</b> Each file is read sharing its writing and deletion, and one that still
/// cannot be read is read as absent and said once (<c>unread</c>), as the settings' own readers do for a file they cannot parse.</para>
/// <para><b>Never written over what it could not read.</b> A bookmarks, preferences or record file that exists and could not be read is left
/// as it is: an absent read written back would replace the person's own bookmarks or settings.</para>
/// </remarks>
public static class EngineProfilePrepare
{
    /// <param name="profile">The engine's profile folder.</param>
    /// <param name="registered">The ids of the extensions other software registered for Chrome on this machine, read only where they are refused.</param>
    /// <param name="now">The time the favorites' folder is stamped with where it is made.</param>
    /// <param name="unread">Told the path of each file that exists and could not be read, once.</param>
    public static void Run(string profile, Func<IReadOnlyList<string>> registered, DateTimeOffset now, Action<string> unread)
    {
        var home = EngineBrowser.HomeOf(profile);
        Directory.CreateDirectory(EngineBrowser.ProfileDirectory(profile));

        var held = new HashSet<string>(StringComparer.Ordinal);
        string? Read(string path)
        {
            if (!File.Exists(path)) return null;
            try
            {
                return BesideReads.Text(path);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Held by another program: absent for this start, and not written over.
                held.Add(path);
                unread(path);
                return null;
            }
        }

        var recordPath = EngineProfile.RecordPath(profile);
        var preferencesPath = EngineProfile.PreferencesPath(profile);
        var bookmarksPath = EngineProfile.BookmarksPath(profile);
        var record = EngineProfile.ReadRecord(Read(recordPath));
        var preferences = Read(preferencesPath);
        var preferencesBefore = preferences;

        var favorites = Favorites(home, unread);
        if (favorites.Problem is null
            && EngineProfile.WithFavorites(Read(bookmarksPath), favorites.Favorites, now) is var (bookmarks, created)
            && !held.Contains(bookmarksPath))
        {
            AtomicFile.WriteText(bookmarksPath, bookmarks);

            // Once, when its folder first appears: a bar that is hidden shows nothing Daoris put there.
            // After that the bar is the person's to hide.
            if (created && !record.BarShown && EngineProfile.ShowBar(preferences) is { } shown)
            {
                preferences = shown;
                record = record with { BarShown = true };
            }
        }

        if (Extensions(home, unread) == ExtensionsSetting.Refuse)
        {
            if (EngineProfile.Refuse(preferences, registered(), record.Refused) is var (refusing, refused))
            {
                preferences = refusing;
                record = record with { Refused = refused };
            }
        }
        else if (record.Refused.Count > 0 && EngineProfile.Offer(preferences, record.Refused) is { } offering)
        {
            preferences = offering;
            record = record with { Refused = [] };
        }

        if (preferences is not null && preferences != preferencesBefore && !held.Contains(preferencesPath))
        {
            AtomicFile.WriteText(preferencesPath, preferences);
        }

        if (!held.Contains(recordPath)) AtomicFile.WriteText(recordPath, EngineProfile.WriteRecord(record));
    }

    /// <summary>The favorites file as it reads, or none where another program holds it.</summary>
    private static FavoritesRead Favorites(string home, Action<string> unread)
    {
        try
        {
            return BrowserFavorites.Read(home);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            unread(BrowserFavorites.FilePath(home));
            return new([], null);
        }
    }

    /// <summary>The extensions setting as it reads, or the default where another program holds the file.</summary>
    private static string Extensions(string home, Action<string> unread)
    {
        try
        {
            return BrowserSettings.Read(home).Extensions;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            unread(BrowserSettings.FilePath(home));
            return ExtensionsSetting.Offer;
        }
    }
}
