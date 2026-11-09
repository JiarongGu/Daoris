using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>driver.json</c>'s one writer (the design's §35.5, §49): every write reads the file under its lock, so a change starts from what
/// another writer wrote before it, and what it wrote is what the next read in the same flow answers.
/// </summary>
public sealed class DriverConfigWritesTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "driver-config-writes", Guid.NewGuid().ToString("N")[..8]);

    private string File_ => DaorisHome.Entry(_home, HomeEntry.DriverConfig);

    public DriverConfigWritesTests()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(File_)!);
        File.WriteAllText(File_, """{ "notify": true }""");
    }

    public void Dispose() => GitSeed.Discard(_home);

    [Fact]
    public async Task Two_standing_choices_written_at_once_each_start_from_the_other_and_neither_is_lost()
    {
        using var inside = new ManualResetEventSlim();
        using var second = new ManualResetEventSlim();
        var first = Task.Run(() => StandingChoices.Write(File_, config =>
        {
            inside.Set();
            // Held inside its change: a second write that read the file now would read it before this one is written.
            second.Wait(TimeSpan.FromSeconds(1));
            return config.WithNotify(!config.Notify);
        }));
        inside.Wait(TimeSpan.FromSeconds(10));
        var other = Task.Run(() =>
        {
            second.Set();
            return StandingChoices.Write(File_, config => config.WithNotify(!config.Notify));
        });

        await Task.WhenAll(first, other);

        Assert.True(DriverConfig.Load(File_).Notify);
    }

    [Fact]
    public void An_access_edit_and_an_identity_edit_through_the_writer_are_read_at_once_in_the_same_flow()
    {
        Assert.Empty(GitAccessKeys.Load(_home).Machine);
        DriverConfigWrites.Change(_home, root => root[GitAccessKeys.MachineKey] = new JsonObject { [GitAccessKeys.Write] = GitAccessKeys.Allow });
        Assert.Equal(GitAccessKeys.Allow, GitAccessKeys.Load(_home).Machine[GitAccessKeys.Write]);

        DriverConfigWrites.Change(_home, root => root[GitIdentityUse.MachineKey] = new JsonObject { ["github.com"] = GitIdentityUse.Own });
        Assert.Equal(GitIdentityUse.Own, Assert.Single(GitIdentityUse.Read(_home).Machine).Value);
        Assert.True(DriverConfig.Load(File_).Notify);
    }

    [Fact]
    public void A_file_that_is_no_object_refuses_the_key_edit_and_is_left_as_it_stands()
    {
        File.WriteAllText(File_, "[1]");

        var refused = Assert.Throws<DriverException>(() => DriverConfigWrites.Change(_home, root => root["x"] = 1));

        Assert.Contains("(config-unread)", refused.Message, StringComparison.Ordinal);
        Assert.Equal("[1]", File.ReadAllText(File_));
    }
}
