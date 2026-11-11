using System.Text;
using LibGit2Sharp;

namespace Daoris.Driver;

/// <summary>
/// A repository's branches and origin's copies of them as the library reads them, printed as git's <c>for-each-ref</c> prints
/// <see cref="RefSnapshot.Format"/> over <c>refs/heads</c> and <c>refs/remotes/origin</c>: each ref's name, the id it stores (a symbolic
/// ref's resolved), that object's type, its upstream in full and short, and a commit's tree. Null where the question is refused or
/// any ref's answer is not one the library gives as git does, and git is asked.
/// </summary>
/// <remarks>
/// <para><b>As git orders and drops them.</b> Sorted by name as bytes, as git sorts them; a symbolic ref whose target is gone is left
/// out, as git leaves a broken ref out. A ref whose object is not here (a partial clone's) refuses the call, since git reads its type.</para>
/// <para><b>The upstream as git's branch configuration gives it</b>: <c>branch.&lt;name&gt;.merge</c>'s first value mapped through its
/// remote's first matching fetch refspec, or the value itself where the remote is <c>.</c>; none where no merge or remote is set or no
/// refspec maps it. A remote with no URL, a negative refspec, a short merge name or a branch or remote key outside the repository's own
/// <c>config</c> refuses the call. Its short form is git's strict shortening: the shortest of git's ref rules whose name no other rule
/// resolves.</para>
/// </remarks>
internal static class GitLibraryRefs
{
    private const string Heads = "refs/heads/";
    private static readonly string[] Under = [Heads, "refs/remotes/origin/"];
    private static readonly (string Prefix, string Suffix)[] Rules =
        [("", ""), ("refs/", ""), ("refs/tags/", ""), ("refs/heads/", ""), ("refs/remotes/", ""), ("refs/remotes/", "/HEAD")];

    /// <summary>The listing in <paramref name="folder"/>; null where git answers it.</summary>
    internal static Task<string?> ListingAsync(string folder, CancellationToken ct) => GitLibrary.AskAsync(folder, GitLibraryQuestion.Refs, Listing, ct);

    /// <summary>The listing over one open call's repository; null where any ref's answer is git's to give.</summary>
    internal static string? Listing(GitLibraryCall call)
    {
        var repository = call.Repository;
        var config = Branching(call);
        if (config is null) return null;
        var rows = new List<(byte[] Key, string Row)>();
        foreach (var reference in repository.Refs)
        {
            var name = reference.CanonicalName;
            if (!Under.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal))) continue;
            if (Resolved(reference) is not { } id) continue;
            if (repository.Lookup(new ObjectId(id)) is not { } target) return null;
            var tree = "";
            if (target is Commit commit)
            {
                if (commit.Tree?.Sha is not { } held) return null;
                tree = held;
            }

            var (upstream, shortName) = ("", "");
            if (name.StartsWith(Heads, StringComparison.Ordinal))
            {
                if (!Upstream(config, name[Heads.Length..], out var full)) return null;
                (upstream, shortName) = full is null ? ("", "") : (full, Shorten(call, full));
            }

            rows.Add((Encoding.UTF8.GetBytes(name), $"{name} {id} {TypeOf(target)} {upstream} {shortName} {tree}\n"));
        }

        rows.Sort((one, other) => one.Key.AsSpan().SequenceCompareTo(other.Key));
        return string.Concat(rows.Select(row => row.Row));
    }

    private static string? Resolved(Reference reference) => reference switch
    {
        DirectReference direct => direct.TargetIdentifier,
        SymbolicReference { Target: { } target } => Resolved(target),
        _ => null,
    };

    private static string TypeOf(GitObject target) => target switch
    {
        Commit => "commit",
        Tree => "tree",
        Blob => "blob",
        _ => "tag",
    };

    /// <summary>
    /// Every <c>branch.</c> and <c>remote.</c> value by its key, in the file's order, read from the common directory's <c>config</c>:
    /// LibGit2Sharp's own configuration of a linked tree reads that tree's directory alone. Null where the tree's
    /// <c>config.worktree</c> names a branch or a remote, or a value is not the repository's own.
    /// </summary>
    private static Dictionary<string, List<string>>? Branching(GitLibraryCall call)
    {
        var own = Path.Combine(call.GitDir, "config.worktree");
        if (File.Exists(own) && File.ReadAllText(own) is var text
            && (text.Contains("[branch", StringComparison.OrdinalIgnoreCase) || text.Contains("[remote", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        using var configuration = Configuration.BuildFrom(Path.Combine(call.CommonDir, "config"));
        var keys = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var entry in configuration)
        {
            if (!entry.Key.StartsWith("branch.", StringComparison.OrdinalIgnoreCase) && !entry.Key.StartsWith("remote.", StringComparison.OrdinalIgnoreCase)) continue;
            if (entry.Level != ConfigurationLevel.Local || entry.Value is null) return null;
            if (!keys.TryGetValue(entry.Key, out var values)) keys[entry.Key] = values = [];
            values.Add(entry.Value);
        }

        return keys;
    }

    /// <summary>The branch's upstream in full, null where it has none; false where git must say it.</summary>
    private static bool Upstream(Dictionary<string, List<string>> config, string branch, out string? upstream)
    {
        upstream = null;
        if (!config.TryGetValue($"branch.{branch}.merge", out var merges) || !config.TryGetValue($"branch.{branch}.remote", out var remotes)) return true;
        var (merge, remote) = (merges[0], remotes[^1]);
        if (!merge.StartsWith("refs/", StringComparison.Ordinal)) return false;
        if (remote == ".")
        {
            upstream = merge;
            return true;
        }

        if (!config.ContainsKey($"remote.{remote}.url")) return false;
        foreach (var spec in config.GetValueOrDefault($"remote.{remote}.fetch") ?? [])
        {
            if (spec.StartsWith('^')) return false;
            var parts = spec.TrimStart('+').Split(':');
            if (parts.Length != 2 || parts[1].Length == 0) continue;
            if (Mapped(parts[0], parts[1], merge) is { } mapped)
            {
                upstream = mapped;
                return true;
            }
        }

        return true;
    }

    private static string? Mapped(string source, string destination, string name)
    {
        var star = source.IndexOf('*');
        if (star < 0) return source == name ? destination : null;
        var (prefix, suffix) = (source[..star], source[(star + 1)..]);
        if (name.Length <= prefix.Length + suffix.Length || !name.StartsWith(prefix, StringComparison.Ordinal) || !name.EndsWith(suffix, StringComparison.Ordinal)) return null;
        return destination.Replace("*", name[prefix.Length..^suffix.Length], StringComparison.Ordinal);
    }

    /// <summary>git's strict shortening of a full name: the shortest rule's name that no other rule resolves, else the full name.</summary>
    private static string Shorten(GitLibraryCall call, string full)
    {
        for (var rule = Rules.Length - 1; rule > 0; rule--)
        {
            var (prefix, suffix) = Rules[rule];
            if (full.Length <= prefix.Length + suffix.Length || !full.StartsWith(prefix, StringComparison.Ordinal) || !full.EndsWith(suffix, StringComparison.Ordinal)) continue;
            var shortName = full[prefix.Length..^suffix.Length];
            var other = 0;
            while (other < Rules.Length && (other == rule || !Exists(call, Rules[other].Prefix + shortName + Rules[other].Suffix))) other++;
            if (other == Rules.Length) return shortName;
        }

        return full;
    }

    private static bool Exists(GitLibraryCall call, string name)
    {
        if (!name.StartsWith("refs/", StringComparison.Ordinal)) return File.Exists(Path.Combine(call.GitDir, name));
        try
        {
            return call.Repository.Refs[name] is { } reference && Resolved(reference) is not null;
        }
        catch (InvalidSpecificationException)
        {
            return false;
        }
    }
}
