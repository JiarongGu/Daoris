using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What a running install writes of its own accord, the memo folder and a session's start record, is named by the table, so a home
/// these writers made has no stray.
/// </summary>
public sealed class HomeWritersTests : IDisposable
{
    private readonly string _install = Path.Combine(TestHomes.Root, "daoris-writers-" + Guid.NewGuid().ToString("N")[..8]);

    public HomeWritersTests()
    {
        Directory.CreateDirectory(Path.Combine(_install, "data"));
        File.WriteAllText(Path.Combine(_install, DaorisHome.InstallMarker), "# Daoris — installed desktop\n");
    }

    private string Home => Path.Combine(_install, "data");

    public void Dispose()
    {
        ContentProofMemo.Restart(Home);
        if (Directory.Exists(_install)) Directory.Delete(_install, recursive: true);
    }

    [Fact]
    public void The_memo_folder_is_an_entry_of_the_table_under_data()
    {
        var memo = HomeEntry.Named("memo");

        Assert.Equal((HomeCategory.Data, "data/memo"), (memo?.Category, memo?.New));
        Assert.Equal(Path.Combine(_install, "data", "memo", "git-objects.json"), DaorisHome.Entry(Home, HomeEntry.Memo, "git-objects.json"));
    }

    [Fact]
    public void The_content_proof_memo_writes_where_the_table_says_and_leaves_no_stray()
    {
        ContentProofMemo.Keep(Home, "key", new DateTimeOffset(2026, 11, 27, 9, 0, 0, TimeSpan.Zero));

        Assert.True(File.Exists(DaorisHome.Entry(Home, HomeEntry.Memo, "content-proofs.json")));
        Assert.Empty(HomeStrays.Read(Home).Paths);
    }

    [Fact]
    public void A_session_s_start_record_is_an_entry_beside_its_marker_and_no_stray()
    {
        var file = Path.Combine(DaorisHome.Entry(Home, HomeEntry.SessionPids), "s-1.4242" + StartRecords.Extension);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        StartRecords.Mark(file, new StartRecord(4242, 639000000000000000L, "s-1",["C:/agent/run.exe"]), null);
        File.WriteAllText(Path.Combine(DaorisHome.Entry(Home, HomeEntry.SessionPids), "s-1.pid"), "4242");

        Assert.True(File.Exists(file));
        Assert.Equal("run/sessions/*.agent", HomeEntry.SessionStarts.New);
        Assert.Empty(HomeStrays.Read(Home).Paths);
    }

    [Fact]
    public void A_file_at_the_install_s_root_is_a_stray_the_table_says_and_never_refuses()
    {
        File.WriteAllBytes(Path.Combine(_install, "check"), []);

        var stray = Assert.Single(HomeStrays.Read(Home).Paths);

        Assert.Equal(("check", 0L), (stray.Path, stray.Bytes));
    }
}
