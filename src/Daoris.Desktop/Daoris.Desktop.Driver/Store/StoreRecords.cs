using System.Security.Cryptography;

namespace Daoris.Driver;

/// <summary>
/// One numbered migration of the desktop store (FOUNDATION1 z-i, the design §13.3): forward only, run in one exclusive transaction, so a
/// crash in it leaves the store as it was, SQLite's DDL being transactional.
/// </summary>
/// <param name="Version">The schema version the store holds once it has run: contiguous from 1.</param>
/// <param name="Writer">
/// The oldest-writer stamp the store carries once it has run, never lower than the one it holds. A step that leaves it where it is adds a
/// table an older store-aware build never opens and runs at the first start; a step that raises it (an older build can no longer write)
/// runs only once the start's confirmation is settled (§13.3, §13.5). The step that makes the store is no raise.
/// </param>
/// <param name="Apply">The step's statements, inside its transaction.</param>
public sealed record StoreMigration(int Version, int Writer, Action<DesktopWrite> Apply);

/// <summary>One process run that has written the store (§13.4): the claim's identity, which a move's executor names from row f-i.</summary>
/// <param name="Run">The run's id, drawn at its start.</param>
/// <param name="Kind">What ran: <c>shell</c>, <c>host</c> or <c>verb</c>.</param>
/// <param name="Pid">Its process id.</param>
/// <param name="Started">
/// When its process started, UTC ticks, the lease's own way of telling a live process (<c>HomeLease</c>); null where it could not be read,
/// which counts as alive.
/// </param>
/// <param name="Began">When the run first wrote, ISO 8601.</param>
public sealed record StoreRun(string Run, string Kind, int Pid, long? Started, string Began);

/// <summary>What an import leaves of its file (§13.5): in place as the backup, or set aside under the import's receipt.</summary>
public static class ImportClass
{
    /// <summary>The file stays where it is, the backup an older build reads as it stood at the import.</summary>
    public const string Left = "left";

    /// <summary>The file is set aside as <c>&lt;file&gt;.imported</c> and never deleted, its readers taking it up only after the import.</summary>
    public const string SetAside = "set-aside";
}

/// <summary>One file imported into a family (§13.3's <c>imports</c>), as the row records it.</summary>
/// <param name="Family">The family the file moved into.</param>
/// <param name="Path">The file's path in the home, home-relative, <c>/</c> between its parts.</param>
/// <param name="Size">The file's size at the import.</param>
/// <param name="Sha256">Its SHA-256, lower-case hex: what the return compares a left file with (§13.5).</param>
/// <param name="Rows">How many rows the import wrote.</param>
/// <param name="Class"><see cref="ImportClass"/>'s words.</param>
public sealed record StoreImport(string Family, string Path, long Size, string Sha256, int Rows, string Class)
{
    /// <summary>An import of <paramref name="file"/>, whose size and SHA-256 are read now.</summary>
    public static StoreImport Of(string family, string homePath, string file, int rows, string importClass)
    {
        using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return new StoreImport(family, homePath, stream.Length, Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant(), rows, importClass);
    }
}

/// <summary>An import the store holds, with the moment and the build that made it.</summary>
public sealed record ImportRecord(long Id, StoreImport Import, string At, string Build);

/// <summary>What an import came to: made, or held with the sentence that says what it waits on, and nothing written.</summary>
public sealed record ImportOutcome(bool Done, string? Said, long? Id)
{
    /// <summary>Held: the start's confirmation is not settled, and nothing was imported.</summary>
    public static ImportOutcome Held(string? said) => new(false, said, null);
}
