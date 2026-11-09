using System.Reflection;
using LibGit2Sharp;

namespace Daoris.Driver;

/// <summary>
/// The native-fault guard (design §5.6): every library call is a lease in the home's run folder (<see cref="GitLibraryLeases"/>), and the
/// first question of a start in a home reads what dead processes left there. A repository a dead process ended inside is journalled as
/// faulted under the package that faulted it and refused from the library while the package is that one, so the repeat of a fault that
/// ended the window cannot end it at each open; a new package lifts the refusal.
/// </summary>
internal static class GitLibraryFaults
{
    private static readonly Lock Gate = new();
    private static readonly HashSet<string> Settled = new(StringComparer.OrdinalIgnoreCase);
    private static (string Home, GitLibraryLeases Leases)? s_writer;

    /// <summary>The library package the running driver carries; a test sets it to stand for an upgrade.</summary>
    internal static string Package { get; set; } = PackageOf();

    /// <summary>The journal's word where <paramref name="folder"/>'s repository is faulted under this package; null where it is not.</summary>
    internal static string? RefusalOf(string folder)
    {
        if (ProgramHome.Named is not { } home) return null;
        Settle(home);
        return GitLibraryJournal.FaultOf(folder) == Package ? GitLibraryJournal.NativeFault : null;
    }

    /// <summary>The lease a call over <paramref name="folder"/> holds, appended before the library is entered; close it with <see cref="Leave"/>.</summary>
    internal static (GitLibraryLeases Leases, long Id)? Enter(string folder)
    {
        if (ProgramHome.Named is not { } home) return null;
        GitLibraryLeases leases;
        lock (Gate)
        {
            if (s_writer is not { } held || !string.Equals(held.Home, home, StringComparison.OrdinalIgnoreCase))
            {
                s_writer = (home, new GitLibraryLeases(DaorisHome.Entry(home, HomeEntry.GitLibraryLeases)));
            }

            leases = s_writer.Value.Leases;
        }

        return (leases, leases.Open(folder));
    }

    /// <summary>Appends the close of a lease <see cref="Enter"/> gave.</summary>
    internal static void Leave((GitLibraryLeases Leases, long Id)? lease) => lease?.Leases.Close(lease.Value.Id);

    private static void Settle(string home)
    {
        lock (Gate)
        {
            if (!Settled.Add(home)) return;
            foreach (var repository in GitLibraryLeases.Left(DaorisHome.Entry(home, HomeEntry.GitLibraryLeases)))
            {
                GitLibraryJournal.Faulted(repository, Package);
            }
        }
    }

    private static string PackageOf()
    {
        var assembly = typeof(Repository).Assembly;
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? assembly.GetName().Version?.ToString() ?? "unknown";
    }
}
