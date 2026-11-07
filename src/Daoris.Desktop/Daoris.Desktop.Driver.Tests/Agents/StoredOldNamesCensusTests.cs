using System.Text.RegularExpressions;
using Daoris.Driver;
using Microsoft.Data.Sqlite;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The census of stored names: no column of a fresh desktop store is spelled with an old seam's word, and every source that spells one as
/// a stored key, column or value is a record the agents-and-harnesses area names with its end; a source outside the list fails here.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class StoredOldNamesCensusTests : IDisposable
{
    private static readonly Regex OldWord = new(
        "\"(?:adapter|harness|intakeAdapter|helperAdapter|harnessVersion|harness_version|pipe|acp)\""
        + @"|\b(?:adapter|harness|harness_version|intake_adapter|helper_adapter)\s+TEXT\b",
        RegexOptions.Compiled);

    private static readonly Regex OldColumn = new(@"adapter|harness", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Each kept record, by the name the area's census gives it, and the sources that spell its old word.</summary>
    private static readonly IReadOnlyDictionary<string, string[]> Kept = new Dictionary<string, string[]>
    {
        ["the service stores' renamed columns and notes"] =
        [
            "Daoris.Service/Daoris.Service.Core/Sessions/SessionColumnNames.cs",
            "Daoris.Service/Daoris.Service.Core/Knowledge/Memory/MemoryAuthorColumnNames.cs",
            "Daoris.Service/Daoris.Service.Core/Sessions/NotePartNames.cs",
        ],
        ["a plugin's work event"] =
        [
            "Daoris.Desktop/Daoris.Desktop.Driver/Plugins/Domain/HookPoints.cs",
            "Daoris.Desktop/Daoris.Desktop.Driver/Plugins/Domain/PluginActivityRead.cs",
            "Daoris.Desktop/Daoris.Desktop.Driver/Plugins/Domain/WorkEventGrammar.cs",
            "Daoris.Desktop/Daoris.Desktop.Driver/Plugins/Relay/RelaySaid.cs",
        ],
        ["a brainstorm's and a meeting's files"] = ["Daoris.Desktop/Daoris.Desktop.Driver/Transitional/Migration/BrainstormFilesStep.cs"],
        ["a binding file"] = ["Daoris.Desktop/Daoris.Desktop.Driver/Transitional/Migration/BindingFilesStep.cs"],
        ["driver.json's staffing keys"] =
        [
            "Daoris.Desktop/Daoris.Desktop.Driver/Roles/RoleStaffing.cs",
            "Daoris.Desktop/Daoris.Desktop.Driver/Transitional/Migration/StaffingStep.cs",
            "Daoris.Desktop/Daoris.Desktop.Driver/Agents/Registry/RecordIds.cs",
            "Daoris.Desktop/Daoris.Desktop.Driver/Store/DesktopStoreSteps.cs",
        ],
        ["Ask Daoris's wired file"] = ["Daoris.Desktop/Daoris.Desktop.Driver/Transitional/Migration/WiredFilesStep.cs"],
        ["the usage file"] = ["Daoris.Desktop/Daoris.Desktop.Driver/Transitional/Migration/UsageFileStep.cs"],
        ["the lineage files"] = ["Daoris.Desktop/Daoris.Desktop.Driver/Transitional/Migration/LineageFilesStep.cs"],
        ["not a seam's word"] =
        [
            "Daoris.Desktop/Daoris.Desktop.Driver/Help/HelpWired.cs",
            "Daoris.Desktop/Daoris.Desktop.Driver/Platform/DaorisHome.cs",
            "Daoris.Service/Daoris.Service.Core/Platform/DaorisHome.cs",
            "Daoris.Desktop/Daoris.Desktop.Driver/Service/RunningAddress.cs",
            "Daoris.Desktop/Daoris.Desktop.Modules/Platform/StartStages.cs",
            "Daoris.Desktop/Daoris.Desktop.Modules/Update/HomePlacing.cs",
            "Daoris.Desktop/Daoris.Desktop.Driver/Plugins/Relay/RelayRun.cs",
            "Daoris.Desktop/Daoris.Desktop.Driver/Agents/Providers/Dsh/DshSpawn.cs",
            "Daoris.Service/Daoris.Service.Core/Registry/DaorisLock.cs",
            "Daoris.Service/Daoris.Service.Core/Registry/RepositoryLayout.cs",
            "Daoris.Desktop/Daoris.Desktop.Driver/Service/Domain/LayoutFacts.cs",
        ],
    };

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "stored-old-names", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    [Fact]
    public void No_column_of_a_fresh_desktop_store_is_spelled_with_an_old_seam_word()
    {
        var db = DesktopStore.LocationIn(_home);
        Directory.CreateDirectory(Path.GetDirectoryName(db)!);
        using (DesktopStore.Open(db, new DesktopStoreOptions { Settled = () => StoreSettled.Yes }))
        {
        }

        using var store = DesktopStoreReader.Open(db)!;
        var found = store.Query(
                "SELECT m.name, c.name FROM sqlite_master m JOIN pragma_table_info(m.name) c WHERE m.type = 'table' ORDER BY m.name, c.cid",
                row => (Table: row.GetString(0), Column: row.GetString(1)))
            .Where(each => OldColumn.IsMatch(each.Table) || OldColumn.IsMatch(each.Column))
            .Select(each => $"{each.Table}.{each.Column}")
            .ToList();

        Assert.Empty(found);
    }

    [Fact]
    public void Every_source_spelling_an_old_seam_word_as_a_stored_name_is_a_kept_record()
    {
        var root = Path.Combine(WorkspaceRoot.Folder, "src");
        var sep = Path.DirectorySeparatorChar;
        var spelling = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{sep}obj{sep}") && !path.Contains($"{sep}bin{sep}") && !path.Contains($".Tests{sep}"))
            .Where(path => File.ReadLines(path).Any(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal) && OldWord.IsMatch(line)))
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToList();
        var listed = Kept.Values.SelectMany(files => files).Order(StringComparer.Ordinal).ToList();

        Assert.Equal(listed.Count, listed.Distinct(StringComparer.Ordinal).Count());
        Assert.Empty(spelling.Except(listed, StringComparer.Ordinal));
        Assert.Empty(listed.Except(spelling, StringComparer.Ordinal));
    }
}
