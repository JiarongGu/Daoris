using System.Security.Cryptography;

namespace Daoris.Driver;

/// <summary>
/// One file of a return written into the folder so that nothing of anyone else's is lost: the session's bytes staged beside the target,
/// the folder's file renamed aside and hashed under a handle no one else holds, and only then the staged file renamed into the free name,
/// never over a file that is there. A file that moved meanwhile, or that another program holds open, is put back and left for the person.
/// Each file's decision and effect are written to the journal around it, so a return cut off at any boundary is taken up by the next.
/// </summary>
internal sealed class ReturnWrite(FolderCopy copy, ReturnJournal journal, Action<string>? passed)
{
    /// <summary>The end of the name the session's bytes are staged under beside the target.</summary>
    public const string StagedEnd = ".daoris-return";

    /// <summary>The end of the name the folder's file is renamed to while the write holds it.</summary>
    public const string AsideEnd = ".daoris-aside";

    /// <summary>Write <paramref name="entry"/>; whether the folder's file is now the session's (or removed).</summary>
    public bool Write(ReturnEntry entry)
    {
        var (target, staged, aside) = Names(entry.Path);
        journal.Begin(entry);
        Passed(ReturnSteps.Begun);
        try
        {
            if (entry.Session is not null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(Path.Combine(copy.Folder, entry.From ?? entry.Path), staged, overwrite: true);
                Passed(ReturnSteps.Staged);
            }

            if (entry.Expected is not null)
            {
                // A rename is refused while another program holds the file open without sharing its removal, which leaves it to the person.
                File.Move(target, aside, overwrite: false);
                Passed(ReturnSteps.SetAside);
                if (!string.Equals(HashAlone(aside), entry.Expected, StringComparison.Ordinal)) return Leave(entry, target, staged, aside);
            }
            else if (File.Exists(target) || Directory.Exists(target))
            {
                return Leave(entry, target, staged, aside);
            }

            if (entry.Session is not null)
            {
                File.Move(staged, target, overwrite: false);
                Passed(ReturnSteps.Replaced);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return Leave(entry, target, staged, aside);
        }

        if (entry.Expected is not null)
        {
            journal.Keep(entry.Path, aside);
            Passed(ReturnSteps.Kept);
        }

        journal.End(entry, ReturnJournal.Done);
        Passed(ReturnSteps.Done);
        return true;
    }

    /// <summary>
    /// Take up every file a return cut off left begun: finished where the folder's file is already the session's or the bytes set aside
    /// still hold the read's bytes, put back where they do not, and left to a fresh decision where nothing of the folder moved.
    /// </summary>
    /// <returns>The paths it finished.</returns>
    public IReadOnlyList<string> Resume()
    {
        List<string> done = [];
        foreach (var entry in journal.Open())
        {
            var (target, staged, aside) = Names(entry.Path);
            var effect = Settle(entry, target, staged, aside);
            if (File.Exists(staged)) File.Delete(staged);
            journal.End(entry, effect);
            if (effect == ReturnJournal.Done) done.Add(entry.Path);
        }

        return done;
    }

    private string Settle(ReturnEntry entry, string target, string staged, string aside)
    {
        var there = Hash(target);
        if (!File.Exists(aside))
        {
            if (entry.Session is not null && there == entry.Session) return ReturnJournal.Done;
            return entry.Session is null && entry.Expected is not null && there is null && File.Exists(journal.Kept(entry.Path)) ? ReturnJournal.Done : ReturnJournal.Undone;
        }

        if (entry.Session is not null && there == entry.Session)
        {
            journal.Keep(entry.Path, aside);
            return ReturnJournal.Done;
        }

        if (there is not null || Directory.Exists(target))
        {
            journal.Keep(entry.Path, aside);
            return ReturnJournal.Moved;
        }

        var from = Path.Combine(copy.Folder, entry.From ?? entry.Path);
        if (Hash(aside) != entry.Expected || (entry.Session is not null && Hash(from) != entry.Session))
        {
            File.Move(aside, target, overwrite: false);
            return Hash(target) == entry.Expected ? ReturnJournal.Undone : ReturnJournal.Moved;
        }

        if (entry.Session is not null)
        {
            File.Copy(from, staged, overwrite: true);
            File.Move(staged, target, overwrite: false);
        }

        journal.Keep(entry.Path, aside);
        return ReturnJournal.Done;
    }

    /// <summary>Put the folder's file back where its name is still free, else keep its bytes, and say the file moved.</summary>
    private bool Leave(ReturnEntry entry, string target, string staged, string aside)
    {
        if (File.Exists(staged)) File.Delete(staged);
        if (File.Exists(aside))
        {
            if (File.Exists(target) || Directory.Exists(target)) journal.Keep(entry.Path, aside);
            else File.Move(aside, target, overwrite: false);
        }

        journal.End(entry, ReturnJournal.Moved);
        return false;
    }

    private (string Target, string Staged, string Aside) Names(string path)
    {
        var target = Path.Combine(copy.Root, path);
        return (target, target + StagedEnd, target + AsideEnd);
    }

    private void Passed(string step) => passed?.Invoke(step);

    /// <summary>The hash of a file no other program holds open: the handle shares nothing, so a writer still holding it refuses it.</summary>
    private static string HashAlone(string file)
    {
        using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    private static string? Hash(string file)
    {
        if (!File.Exists(file)) return null;
        using var stream = AtomicFile.OpenRead(file);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }
}
