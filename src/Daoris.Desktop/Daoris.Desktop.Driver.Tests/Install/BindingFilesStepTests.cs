using Daoris.Driver;
using Microsoft.Data.Sqlite;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AGENTPROVIDER1t (D232): a session's binding file (<c>&lt;session&gt;.harness.json</c>, keyed <c>adapter</c>) is imported into the desktop
/// store's bindings once by the home's migration, a copy of the file kept first, and the file and its set-aside are gone after.
/// </summary>
public sealed class BindingFilesStepTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "binding-files-step", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private string FileOf(string name) => DaorisHome.Entry(_home, HomeEntry.Sessions, name);

    private void Write(string name, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FileOf(name))!);
        File.WriteAllText(FileOf(name), text);
    }

    private BindingStore Bindings => new(_home, () => StoreSettled.Yes);

    [Fact]
    public void A_binding_file_is_imported_once_kept_and_taken_out()
    {
        Write("s1.harness.json", """{"adapter":"codex-acp","conversation":"c-1"}""");
        Write("s2.harness.json.imported", """{"adapter":"codex-acp","conversation":"c-old"}""");
        var run = new HomeMigrationRun(_home, HomeMigration.Name);

        new BindingFilesStep().Run(run);

        Assert.Equal(("codex-acp", "c-1"), Bindings.Read("s1"));
        Assert.Null(Bindings.Read("s2"));
        Assert.False(File.Exists(FileOf("s1.harness.json")));
        Assert.False(File.Exists(FileOf("s2.harness.json.imported")));
        Assert.True(File.Exists(Path.Combine(run.Copies, "data", "sessions", "s1.harness.json")));
        new BindingFilesStep().Run(new HomeMigrationRun(_home, HomeMigration.Name));
        Assert.Equal(("codex-acp", "c-1"), Bindings.Read("s1"));
    }

    [Fact]
    public void A_file_the_store_already_holds_a_later_conversation_for_wins_over_the_row()
    {
        Write("s1.harness.json", """{"adapter":"claude-code","conversation":"c-newer"}""");
        Bindings.Keep("s1", "codex-acp", "c-1");

        new BindingFilesStep().Run(new HomeMigrationRun(_home, HomeMigration.Name));

        Assert.Equal(("claude-code", "c-newer"), Bindings.Read("s1"));
    }

    [Fact]
    public void A_file_that_does_not_read_is_kept_in_the_copies_and_taken_out_without_a_binding()
    {
        Write("s1.harness.json", "{not json");
        var run = new HomeMigrationRun(_home, HomeMigration.Name);

        new BindingFilesStep().Run(run);

        Assert.Null(Bindings.Read("s1"));
        Assert.False(File.Exists(FileOf("s1.harness.json")));
        Assert.True(File.Exists(Path.Combine(run.Copies, "data", "sessions", "s1.harness.json")));
    }

    [Fact]
    public void A_home_with_no_binding_file_makes_no_store()
    {
        Directory.CreateDirectory(_home);

        new BindingFilesStep().Run(new HomeMigrationRun(_home, HomeMigration.Name));

        Assert.False(File.Exists(DesktopStore.LocationIn(_home)));
    }

    [Fact]
    public void The_migration_carries_the_step_after_the_store_s_names()
    {
        var names = HomeMigration.Steps.Select(step => step.Name).ToList();
        Assert.True(names.IndexOf("binding-files") > names.IndexOf("store-names"));
    }
}
