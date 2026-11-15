using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>daoris notices --show &lt;subject&gt;</c> of a memory wait: what the window's Memory section shows of it, as lines, with the
/// decision left to the person's press in the window.
/// </summary>
public sealed class MemoryWaitShownTests
{
    private static JsonObject Counts(int ends, int replacements, int staged) =>
        new() { ["ends"] = ends, ["replacements"] = replacements, ["staged"] = staged };

    /// <summary>A session's rows: each said as the section says it, an end with who proposed it, and the press named, never made.</summary>
    [Fact]
    public void A_sessions_rows_are_said_as_the_window_shows_them()
    {
        var ops = JsonNode.Parse("""
            { "ops": [
              { "id": "op1", "repository": "Engine", "state": "awaiting", "at": "2026-11-15T08:00:00Z", "models": "model-a",
                "by": { "session": "s1a2b3c4", "door": "claude-code" }, "change": { "kind": "end", "target": "entry-7", "revision": 3, "text": "It is replaced." } },
              { "id": "op2", "repository": "Engine", "state": "staged", "at": "2026-11-15T08:01:00Z",
                "change": { "kind": "write", "text": "A new fact." } }
            ] }
            """)!;

        var said = MemoryWaitShown.Said("s1a2b3c4", Counts(1, 0, 1), ops, null);

        Assert.Equal(0, said.Code);
        Assert.Equal(
            [
                "memory s1a2b3c4 waits for your word: 1 end, 0 replacements, 1 staged",
                "  op1 · End of Engine:entry-7 at revision 3 · awaits your approval",
                "    It is replaced.",
                "    proposed by s1a2b3c4 · claude-code · model-a",
                "    approve or decline it in the window's What needs you",
                "  op2 · Writes to Engine's memory · kept with this session's work, applied when it is accepted",
                "    A new fact.",
                "  1 staged change waits on your Keep or Drop, in the window",
            ],
            said.Out);
    }

    /// <summary>A sweep's end has no session: its row's own fields are all there is, the press the window's.</summary>
    [Fact]
    public void A_sweeps_end_is_said_from_its_row()
    {
        var end = new JsonObject { ["op"] = "op9", ["repository"] = "Engine", ["target"] = "entry-2", ["revision"] = 4, ["reason"] = "Nothing reads it now." };

        var said = MemoryWaitShown.Said("op9", Counts(1, 0, 0), null, end);

        Assert.Equal(
            [
                "memory op9 waits for your word: 1 end, 0 replacements, 0 staged",
                "  op9 · End of Engine:entry-2 at revision 4 · awaits your approval",
                "    Nothing reads it now.",
                "    proposed by Daoris's recheck · ? · model unknown",
                "    approve or decline it in the window's What needs you",
            ],
            said.Out);
    }

    /// <summary>Said, never judged: a subject that waits on no memory is refused with exit 1.</summary>
    [Fact]
    public void A_subject_that_waits_on_no_memory_is_refused()
    {
        var said = MemoryWaitShown.None("s9");

        Assert.Equal(1, said.Code);
        Assert.Equal(["notices: no memory change waits for your word on `s9`; `daoris notices` lists what does."], said.Error);
    }
}
