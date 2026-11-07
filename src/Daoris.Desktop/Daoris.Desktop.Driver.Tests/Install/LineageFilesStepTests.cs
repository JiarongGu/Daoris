using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A session's <c>lineage.jsonl</c> names a call's door by <c>door</c> and a child the protocol door announced by
/// <c>"by":"announced"</c> after the home's migration; an older line's <c>harness</c> and <c>"by":"acp"</c> are converted once.
/// </summary>
public sealed class LineageFilesStepTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "lineage-files-step", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose() => GitSeed.Discard(_home);

    private string FileOf(string session) => Path.Combine(DaorisHome.Entry(_home, HomeEntry.Sessions), session, ChildLineage.FileName);

    private void Older(string session, params string[] lines)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FileOf(session))!);
        File.WriteAllText(FileOf(session), string.Concat(lines.Select(line => line + "\n")));
    }

    [Fact]
    public void An_older_line_is_written_in_today_s_words_and_a_second_run_changes_nothing()
    {
        Older("s1",
            """{"at":"2026-10-19T10:00:00.000Z","by":"call","tool":"knowledge_search","harness":"codex","thread":"t2"}""",
            """{"at":"2026-10-19T10:00:01.000Z","by":"acp","event":"started","agent":"a1","depth":1}""",
            """{"at":"2026-10-19T10:00:02.000Z","by":"hook","event":"start","agent":"a3"}""",
            "not json");
        var run = new HomeMigrationRun(_home, HomeMigration.Name);

        new LineageFilesStep().Run(run);

        Assert.Equal(
            [
                """{"at":"2026-10-19T10:00:00.000Z","by":"call","tool":"knowledge_search","thread":"t2","door":"codex"}""",
                """{"at":"2026-10-19T10:00:01.000Z","by":"announced","event":"started","agent":"a1","depth":1}""",
                """{"at":"2026-10-19T10:00:02.000Z","by":"hook","event":"start","agent":"a3"}""",
                "not json",
            ],
            File.ReadAllLines(FileOf("s1")));
        Assert.Equal(["data/sessions/s1/lineage.jsonl"], run.Changed);

        var again = new HomeMigrationRun(_home, HomeMigration.Name);
        new LineageFilesStep().Run(again);
        Assert.Empty(again.Changed);
    }

    [Fact]
    public void A_file_already_in_today_s_words_and_a_home_with_none_are_left_as_they_are()
    {
        var current = """{"at":"2026-10-19T10:00:00.000Z","by":"call","door":"codex","thread":"t2"}""";
        Older("s2", current);
        var run = new HomeMigrationRun(_home, HomeMigration.Name);

        new LineageFilesStep().Run(run);

        Assert.Empty(run.Changed);
        Assert.Equal(current + "\n", File.ReadAllText(FileOf("s2")));
        new LineageFilesStep().Run(new HomeMigrationRun(Path.Combine(_home, "none"), HomeMigration.Name));
    }

    [Fact]
    public void The_migration_carries_the_step()
    {
        Assert.Contains(HomeMigration.Steps, step => step is LineageFilesStep && step.Name == "lineage-files");
    }
}
