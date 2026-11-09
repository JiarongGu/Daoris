using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The window's model change and the terminal's effort change land on one account's settings file together: whichever order the lock gives,
/// both keys stand. The second writer is run from the first's own read seam, held off by the lock until the first has written.
/// </summary>
public sealed class AgentSettingsInterleaveTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-settings-interleave-" + Guid.NewGuid().ToString("N")[..8]);

    public AgentSettingsInterleaveTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public async Task A_model_change_and_an_effort_change_that_read_the_same_file_both_stand()
    {
        var path = Path.Combine(_home, AgentSettings.FileName);
        File.WriteAllText(path, "{\"theme\":\"dark\"}\n");
        Task? effort = null;
        AgentSettings.AfterRead.Value = () =>
        {
            effort = Task.Run(() => AgentSettings.Write(path, effort: new("high")));
            Thread.Sleep(300);
        };

        AgentSettings.Write(path, model: new("opus"));
        await effort!;

        var read = AgentSettings.Read(path);
        Assert.Equal(("opus", "high"), (read.Model, read.Effort));
        Assert.Contains("\"theme\": \"dark\"", File.ReadAllText(path));
    }
}
