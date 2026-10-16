using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// MIXAGENT2a-ii (D163 point 2): the helpers' files are the contract between this box and the driver's reader, which share no
/// code, so each side holds the shape. Here, the driver's state file as it writes it, with the marks only it keeps; the driver's
/// suite (<c>HelperFilesTests</c>) holds the same text from its side.
/// </summary>
public sealed class HelperBoxShapeTests : IDisposable
{
    private readonly string _home = Path.Combine(AppContext.BaseDirectory, "helper-box-" + Guid.NewGuid().ToString("N")[..8]);

    public HelperBoxShapeTests() => Directory.CreateDirectory(DaorisHome.Entry(_home, HomeEntry.Helpers));

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>
    /// The state the driver writes (indented, `code` and `helper` null where unset, then its own `lastWords`, `handed`, `agent` and
    /// `words`) reads here as the five fields the box knows; a state named outside its five is no state.
    /// </summary>
    [Fact]
    public void The_drivers_state_file_reads_with_its_own_marks_ignored_and_an_unknown_state_is_none()
    {
        File.WriteAllText(DaorisHome.Entry(_home, HomeEntry.Helpers,"a1b2c3d4.state.json"),
            "{\n  \"id\": \"a1b2c3d4\",\n  \"state\": \"answered\",\n  \"code\": null,\n  \"helper\": \"h1a2b3c4\",\n"
            + "  \"at\": \"2026-10-12T10:00:00.0000000+00:00\",\n  \"lastWords\": true,\n  \"handed\": \"w1\",\n  \"agent\": \"codex-acp\",\n  \"words\": \"Probably the window.\"\n}\n");
        File.WriteAllText(DaorisHome.Entry(_home, HomeEntry.Helpers,"b1b2c3d4.state.json"),
            "{\n  \"id\": \"b1b2c3d4\",\n  \"state\": \"waiting\",\n  \"code\": \"cap\",\n  \"helper\": null,\n  \"at\": \"2026-10-12T10:00:00.0000000+00:00\"\n}\n");
        File.WriteAllText(DaorisHome.Entry(_home, HomeEntry.Helpers,"c1b2c3d4.state.json"), "{ \"id\": \"c1b2c3d4\", \"state\": \"sleeping\" }");

        var answered = HelperBox.ReadState(_home, "a1b2c3d4")!;
        Assert.Equal(("answered", null, "h1a2b3c4"), (answered.State, answered.Code, answered.Helper));
        var waiting = HelperBox.ReadState(_home, "b1b2c3d4")!;
        Assert.Equal(("waiting", "cap", null), (waiting.State, waiting.Code, waiting.Helper));
        Assert.Null(HelperBox.ReadState(_home, "c1b2c3d4"));
    }
}
