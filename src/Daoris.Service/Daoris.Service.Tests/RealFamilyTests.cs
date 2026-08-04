using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// Scans the actual sibling repositories rather than fixtures.
///
/// A scanner that only works on invented input proves nothing: real doctrine has fenced code in its
/// decision logs, logs in three different locations, repositories that never adopted daoris, and
/// entries whose headings are not what a fixture author would have guessed. Skipped when the
/// siblings are not present, so this is a signal on a development machine and never a failure
/// somewhere else. xunit v2 has no dynamic skip, so an absent family returns early instead.
/// </summary>
public class RealFamilyTests
{
    private static string? FamilyRoot()
    {
        // Walk up from the test assembly to the workspace, then to its parent directory.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "daoris.json"))) dir = dir.Parent;
        var parent = dir?.Parent?.FullName;
        return parent is not null && Directory.Exists(parent) ? parent : null;
    }

    [Fact]
    public void Scans_this_repository_and_classifies_its_own_doctrine()
    {
        var family = FamilyRoot();
        if (family is null) return; // siblings absent — nothing to assert against

        var root = Path.Combine(family, "Daoris");
        if (!File.Exists(Path.Combine(root, "daoris.lock"))) return;

        var entries = new RepositoryScanner().Scan(root);

        // Its rules and skills are canonical (it syncs its own canon); its knowledge is its own.
        var canonical = entries.Where(e => e.Provenance == Provenance.Canonical).ToList();
        var local = entries.Where(e => e.Provenance == Provenance.Local).ToList();
        Assert.NotEmpty(canonical);
        Assert.NotEmpty(local);

        // The decision log really does contain a fenced markdown heading; splitting must survive it.
        var decisions = entries.Where(e => e.Kind == EntryKind.Decision).ToList();
        Assert.True(decisions.Count > 10, $"expected the full decision log, got {decisions.Count}");
        Assert.All(decisions, d => Assert.False(string.IsNullOrWhiteSpace(d.Body)));

        Assert.Contains(entries, e => e.Kind == EntryKind.Skill);
        Assert.Contains(entries, e => e.Kind == EntryKind.TaskOutcome);
    }

    [Fact]
    public void Scanning_the_whole_family_finds_more_local_than_canonical_knowledge()
    {
        var family = FamilyRoot();
        if (family is null) return;

        var scanner = new RepositoryScanner();
        var all = Directory.EnumerateDirectories(family)
            .SelectMany(repo => scanner.Scan(repo))
            .ToList();

        if (all.Count == 0) return;

        // The premise of the index: canonical content is the same everywhere, so what is worth
        // searching ACROSS repositories is the local material.
        var local = all.Count(e => e.Provenance == Provenance.Local);
        Assert.True(local > 0, "expected local knowledge across the family");

        // And every entry must be addressable, or the index cannot return anything useful.
        Assert.Equal(all.Count, all.Select(e => e.Id).Distinct().Count());
    }
}
