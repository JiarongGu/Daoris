using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AGENTPROVIDER1t (D232): Ask Daoris's wired files (<c>&lt;session&gt;.wired.json</c>) name each receipt's door by <c>door</c> and its
/// transport by <c>transport</c>, <c>native</c> or <c>protocol</c>, after the home's migration: an older receipt's <c>harness</c> is the
/// door and its <c>door</c>, <c>pipe</c> or <c>acp</c>, the transport.
/// </summary>
public sealed class WiredFilesStepTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "wired-files-step", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose() => GitSeed.Discard(_home);

    private string FileOf(string session) => DaorisHome.Entry(_home, HomeEntry.Sessions, session + ".wired.json");

    private void Older(string session, string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FileOf(session))!);
        File.WriteAllText(FileOf(session), json);
    }

    private JsonArray Receipts(string session) => (JsonArray)((JsonObject)JsonNode.Parse(File.ReadAllText(FileOf(session)))!)["receipts"]!;

    [Fact]
    public void A_receipt_names_its_door_and_its_transport_by_today_s_words_and_a_second_run_changes_nothing()
    {
        Older("s1", """{"receipts":[{"at":"2026-10-19T10:00:00+00:00","harness":"claude-code-acp","door":"acp","folder":"room"},{"at":"2026-10-18T10:00:00+00:00","harness":"claude-code","door":"pipe","folder":"room"}]}""");
        var run = new HomeMigrationRun(_home, HomeMigration.Name);

        new WiredFilesStep().Run(run);

        var receipts = Receipts("s1");
        Assert.Equal(("claude-code-acp", "protocol", null), (receipts[0]!["door"]!.GetValue<string>(), receipts[0]!["transport"]!.GetValue<string>(), (string?)receipts[0]!["harness"]));
        Assert.Equal(("claude-code", "native", null), (receipts[1]!["door"]!.GetValue<string>(), receipts[1]!["transport"]!.GetValue<string>(), (string?)receipts[1]!["harness"]));
        Assert.Equal(["data/sessions/s1.wired.json"], run.Changed);
        Assert.True(File.Exists(Path.Combine(run.Copies, "data", "sessions", "s1.wired.json")));

        var again = new HomeMigrationRun(_home, HomeMigration.Name);
        new WiredFilesStep().Run(again);
        Assert.Empty(again.Changed);
    }

    [Fact]
    public void A_file_that_does_not_read_and_a_home_with_none_are_left_as_they_are()
    {
        Older("s2", "{not json");
        var run = new HomeMigrationRun(_home, HomeMigration.Name);

        new WiredFilesStep().Run(run);

        Assert.Empty(run.Changed);
        Assert.Equal("{not json", File.ReadAllText(FileOf("s2")));
    }

    [Fact]
    public void The_migration_carries_the_step()
    {
        Assert.Contains(HomeMigration.Steps, step => step is WiredFilesStep && step.Name == "wired-files");
    }
}
