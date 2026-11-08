using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The census of the readers the pool moves, held by a scan of the sources by name, as the config seam's is: a record names a line and
/// never a slot, so each reader that opens a record's tree as a folder is listed <i>not yet moved</i> with its row, as is each reader that
/// derives a branch from a folder git lists and each in-use set built from records with every test of a listed folder against one. A new
/// site in any list fails, as does any write of a slot's folder into a record and any path built from a slot's number.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class PoolReadersCensusTests
{
    private static readonly string[] Projects = ["Daoris.Desktop.Driver", "Daoris.Desktop.Modules"];

    private static readonly Lazy<IReadOnlyDictionary<string, string>> Sources = new(() =>
    {
        var desktop = SourceFiles.Project("").TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var read = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var project in Projects)
        {
            foreach (var file in SourceFiles.All(project, "*.cs"))
            {
                read[Path.GetRelativePath(desktop, file).Replace('\\', '/')] = File.ReadAllText(file);
            }
        }

        return read;
    });

    private static IEnumerable<(string File, string Source)> Files => Sources.Value.Select(each => (each.Key, each.Value));

    [Fact]
    public void The_sources_are_scanned_and_each_listed_reader_still_stands_where_it_is_listed()
    {
        Assert.True(Sources.Value.Count >= 1000, $"expected to scan the desktop's sources, scanned {Sources.Value.Count}");
        var fallen = PoolCensusScan.Fallen(Sources.Value, PoolReaders.OpenAsFolder)
            .Concat(PoolCensusScan.Fallen(Sources.Value, PoolReaders.BranchFromFolder))
            .Concat(PoolCensusScan.Fallen(Sources.Value, PoolReaders.InUseSets)).ToList();

        Assert.True(fallen.Count == 0, "a listed reader has moved or changed; list it where it is now, or take it out:\n" + string.Join('\n', fallen));
    }

    [Fact]
    public void Every_reader_that_opens_a_record_s_tree_as_a_folder_is_listed_with_its_row()
    {
        var unlisted = PoolCensusScan.Unlisted(Files, PoolReaders.FolderGone, PoolReaders.OpenAsFolder, PoolReaders.NotSites);

        Assert.True(unlisted.Count == 0, "a reader opens a record's tree as a folder and is in no list; list it with the row that moves it:\n" + string.Join('\n', unlisted));
    }

    [Fact]
    public void Every_reader_that_derives_a_branch_from_a_folder_git_lists_is_listed_with_its_row()
    {
        var unlisted = PoolCensusScan.Unlisted(Files, PoolReaders.BranchOf, PoolReaders.BranchFromFolder, PoolReaders.NotSites);

        Assert.True(unlisted.Count == 0, "a reader derives a branch from a folder and is in no list; a slot's folder would answer a branch nobody made:\n" + string.Join('\n', unlisted));
    }

    [Fact]
    public void Every_in_use_set_built_from_records_and_every_test_of_a_listed_folder_against_one_is_listed_with_its_row()
    {
        var unlisted = PoolCensusScan.Unlisted(Files, PoolReaders.InUseSignal, PoolReaders.InUseSets, PoolReaders.NotSites);

        Assert.True(unlisted.Count == 0, "an in-use set or a test against one is in no list; a running line's slot would read as free there:\n" + string.Join('\n', unlisted));
    }

    [Fact]
    public void The_landing_s_set_and_the_follow_s_test_are_in_the_third_list()
    {
        var files = PoolReaders.InUseSets.Select(each => each.File).ToHashSet();

        Assert.Contains("Daoris.Desktop.Driver/Review/ReviewLandingWorld.cs", files);
        Assert.Contains("Daoris.Desktop.Driver/Workplace/LandPerformer.cs", files);
        Assert.Contains("Daoris.Desktop.Driver/Workplace/LandingPress.cs", files);
        Assert.Contains("Daoris.Desktop.Driver/Workplace/TreeLandSyncs.cs", files);
    }

    [Fact]
    public void Every_entry_names_its_row_and_what_it_reads_and_stands_once_in_its_list()
    {
        foreach (var list in new[] { PoolReaders.OpenAsFolder, PoolReaders.BranchFromFolder, PoolReaders.InUseSets })
        {
            Assert.All(list, each =>
            {
                Assert.Matches("^[b-f]$", each.Row);
                Assert.NotEmpty(each.Reads);
            });
            Assert.Equal(list.Count, list.Select(each => each.File).Distinct().Count());
        }
    }

    [Fact]
    public void No_source_writes_a_slot_s_folder_into_a_record_or_builds_a_path_from_a_slot_s_number()
    {
        var offences = Files.SelectMany(each => PoolCensusScan.Offences(each.File, each.Source)).ToList();

        Assert.True(offences.Count == 0, "a slot's folder is named by the pool alone, and no record names it:\n" + string.Join('\n', offences));
    }

    [Fact]
    public void The_strays_pass_a_kept_slot_s_folder_over_beside_a_replay_s()
    {
        Assert.Contains(KeptTree.Prefix, TreeStrays.OthersOwn);
        Assert.Contains(BranchReplays.Prefix, TreeStrays.OthersOwn);
    }

    // ——— Each kind of planted site fails.

    [Fact]
    public void A_planted_reader_of_each_list_fails_its_scan()
    {
        var planted = new[]
        {
            ("Daoris.Desktop.Driver/Workplace/Planted.cs", "var gone = TreeFolders.TreeGone(path);\nvar b = trees.BranchOfTree(path);\nvar set = new HashSet<string> inUse;"),
        };

        Assert.Equal(["Daoris.Desktop.Driver/Workplace/Planted.cs"], PoolCensusScan.Unlisted(planted, PoolReaders.FolderGone, PoolReaders.OpenAsFolder, PoolReaders.NotSites));
        Assert.Equal(["Daoris.Desktop.Driver/Workplace/Planted.cs"], PoolCensusScan.Unlisted(planted, PoolReaders.BranchOf, PoolReaders.BranchFromFolder, PoolReaders.NotSites));
        Assert.Equal(["Daoris.Desktop.Driver/Workplace/Planted.cs"], PoolCensusScan.Unlisted(planted, PoolReaders.InUseSignal, PoolReaders.InUseSets, PoolReaders.NotSites));
    }

    [Fact]
    public void A_site_in_a_comment_is_no_site_and_a_listed_file_is_no_new_one()
    {
        var comment = new[] { ("Daoris.Desktop.Driver/Workplace/Prose.cs", "/// <see cref=\"TreeFolders.TreeGone\"/>\n// BranchOfTree and InUseAsync") };
        Assert.Empty(PoolCensusScan.Unlisted(comment, PoolReaders.FolderGone, PoolReaders.OpenAsFolder, PoolReaders.NotSites));
        Assert.Empty(PoolCensusScan.Unlisted(comment, PoolReaders.BranchOf, PoolReaders.BranchFromFolder, PoolReaders.NotSites));
        Assert.Empty(PoolCensusScan.Unlisted(comment, PoolReaders.InUseSignal, PoolReaders.InUseSets, PoolReaders.NotSites));

        var listed = new[] { (PoolReaders.BranchFromFolder[0].File, "trees.BranchOfTree(x)") };
        Assert.Empty(PoolCensusScan.Unlisted(listed, PoolReaders.BranchOf, PoolReaders.BranchFromFolder, PoolReaders.NotSites));
    }

    [Fact]
    public void A_listed_reader_that_moved_or_changed_fails_its_scan()
    {
        var list = new[] { new PoolReader("Daoris.Desktop.Driver/Workplace/Reader.cs", "TreeFolders\\.TreeGone", "c", "x") };

        Assert.Empty(PoolCensusScan.Fallen(new Dictionary<string, string> { [list[0].File] = "TreeFolders.TreeGone(a)" }, list));
        Assert.Single(PoolCensusScan.Fallen(new Dictionary<string, string> { [list[0].File] = "var other = 1;" }, list));
        Assert.Single(PoolCensusScan.Fallen(new Dictionary<string, string>(), list));
    }

    [Fact]
    public void A_planted_write_of_a_kept_folder_into_a_record_and_a_path_built_from_a_slot_s_number_fail()
    {
        const string file = "Daoris.Desktop.Driver/Workplace/Planted.cs";

        Assert.Single(PoolCensusScan.Offences(file, "record.Tree = slot.Path;"));
        Assert.Single(PoolCensusScan.Offences(file, "var open = new Start(tree: keptTree.Path, line);"));
        Assert.Single(PoolCensusScan.Offences(file, "var folder = Path.Combine(trees, $\"k-{number}\");"));
        Assert.Single(PoolCensusScan.Offences(file, "var folder = KeptTree.FolderIn(trees, number);"));
        Assert.Single(PoolCensusScan.Offences(file, "var name = KeptTree.Prefix + number;"));
        Assert.Single(PoolCensusScan.Offences(file, "var folder = Path.Combine(trees, \"k-\" + number);"));
    }

    [Fact]
    public void A_slot_s_folder_read_for_a_reader_and_a_key_that_only_starts_with_k_are_no_offence()
    {
        const string file = "Daoris.Desktop.Driver/Workplace/Planted.cs";

        Assert.Empty(PoolCensusScan.Offences(file, "var folder = slot.Path;"));
        Assert.Empty(PoolCensusScan.Offences(file, "var key = $\"k-{n}\";"));
        Assert.Empty(PoolCensusScan.Offences(file, "var key = Random(\"k-\", 16);"));
        Assert.Empty(PoolCensusScan.Offences(file, "/// builds KeptTree.FolderIn(trees, n) from record.Tree = slot.Path"));
        Assert.Empty(PoolCensusScan.Offences("Daoris.Desktop.Driver/Workplace/Pool/KeptTree.cs", "var folder = KeptTree.FolderIn(trees, number);"));
    }
}
