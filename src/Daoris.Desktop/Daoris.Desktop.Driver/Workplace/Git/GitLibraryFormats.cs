using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>A repository's object format (<c>sha1</c>, <c>sha256</c>) and ref format (<c>files</c>, <c>reftable</c>), as git names them.</summary>
internal sealed record GitLibraryFormat(string Objects, string Refs)
{
    /// <summary>Whether the library reads this repository: SHA-1 objects in files refs, the formats every repository made before git 3.0 has.</summary>
    internal bool Readable => Objects == "sha1" && Refs == "files";
}

/// <summary>
/// The probe behind <c>git.read.formats</c>: a repository's formats, read from its own configuration and refs folder with no git
/// started, once for each stamp of them. The admission asks it first (<see cref="GitLibraryAdmission"/>), so a format the library
/// cannot read is git's binary's alone and the library is never opened on it.
/// </summary>
/// <remarks>
/// <para><b>Stamped.</b> The stamp is the configuration file's length and write time and whether a <c>reftable</c> folder is there: a
/// repository converted with <c>git refs migrate</c> or a changed <c>extensions</c> key is read again at the next question, an
/// unchanged one never.</para>
/// <para><b>Only the repository's own file.</b> git reads <c>extensions.*</c> from the repository's configuration alone, never from a
/// global file or an include, so the probe reads that one file's <c>[extensions]</c> section.</para>
/// </remarks>
internal static class GitLibraryFormats
{
    private static readonly ConcurrentDictionary<string, (string Stamp, GitLibraryFormat Format)> Known = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, int> Counts = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>How many times <paramref name="commonDir"/>'s formats have been read from its files in this process; counted for the one repository, so another's reads never move it.</summary>
    internal static int ReadsOf(string commonDir) => Counts.GetValueOrDefault(commonDir);

    /// <summary><paramref name="commonDir"/>'s formats, from the answer kept for its stamp where there is one.</summary>
    internal static GitLibraryFormat Of(string commonDir)
    {
        var config = Path.Combine(commonDir, "config");
        var reftable = Directory.Exists(Path.Combine(commonDir, "reftable"));
        var info = new FileInfo(config);
        var stamp = info.Exists ? $"{info.Length}:{info.LastWriteTimeUtc.Ticks}:{reftable}" : $"none:{reftable}";
        if (Known.TryGetValue(commonDir, out var kept) && kept.Stamp == stamp) return kept.Format;
        Counts.AddOrUpdate(commonDir, 1, (_, count) => count + 1);
        var format = Read(info.Exists ? File.ReadAllLines(config) : [], reftable);
        Known[commonDir] = (stamp, format);
        return format;
    }

    private static GitLibraryFormat Read(IEnumerable<string> lines, bool reftable)
    {
        string objects = "sha1", refs = reftable ? "reftable" : "files";
        var inExtensions = false;
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.StartsWith('['))
            {
                inExtensions = line.Equals("[extensions]", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (!inExtensions || line.Split('=', 2) is not [var key, var value]) continue;
            value = value.Trim().Trim('"').ToLowerInvariant();
            if (key.Trim().Equals("objectformat", StringComparison.OrdinalIgnoreCase)) objects = value;
            else if (key.Trim().Equals("refstorage", StringComparison.OrdinalIgnoreCase)) refs = value;
        }

        return new GitLibraryFormat(objects, refs);
    }
}
