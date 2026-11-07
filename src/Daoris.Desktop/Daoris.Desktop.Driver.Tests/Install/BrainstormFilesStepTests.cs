using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AGENTPROVIDER1t-ii (D232): a brainstorm's pass records and a meeting's packet name the door a pass ran on <c>door</c> after the home's
/// migration; an older file's <c>adapter</c> is that door, a meeting's packet is written again under the digest of what it now holds, and a
/// tool's answer is left as it is.
/// </summary>
public sealed class BrainstormFilesStepTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "brainstorm-files-step", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose() => GitSeed.Discard(_home);

    [Fact]
    public void A_pass_record_and_a_meetings_packet_name_their_door_by_today_s_word_and_a_second_run_changes_nothing()
    {
        var files = new BrainstormFiles(_home);
        Directory.CreateDirectory(Path.Combine(files.Folder("b1"), "passes"));
        Directory.CreateDirectory(Path.Combine(files.Folder("b1"), "answers"));
        File.WriteAllText(
            files.PathOf("b1", "passes", "p1.json"),
            """{"pass":"p1","role":"generate","maker":"Anthropic","adapter":"claude-code","account":null,"state":"done","turns":1,"codes":[]}""");
        File.WriteAllText(files.PathOf("b1", "answers", "t1.json"), """{"adapter":"the tool's own field"}""");
        var meetings = new MeetingFiles(_home);
        Directory.CreateDirectory(meetings.Folder("m1"));
        var packet = new MeetingPacket("q", null, null, [], [], [new MeetingParticipant("a1", "Anthropic", "claude-code")], 1, []);
        File.WriteAllText(meetings.PathOf("m1", BrainstormInstruction.PacketFile), System.Text.Encoding.UTF8.GetString(MeetingPackets.Bytes(packet)).Replace("\"door\"", "\"adapter\"", StringComparison.Ordinal));
        var run = new HomeMigrationRun(_home, HomeMigration.Name);

        new BrainstormFilesStep().Run(run);

        Assert.Equal("claude-code", files.ReadPasses("b1").Single().By.Adapter);
        Assert.Equal("claude-code", MeetingPackets.Read(meetings.Turns.PacketBytes("m1")!)!.Participants.Single().Adapter);
        Assert.Contains("\"adapter\"", File.ReadAllText(files.PathOf("b1", "answers", "t1.json")));
        Assert.Equal(2, run.Changed.Count);

        var again = new HomeMigrationRun(_home, HomeMigration.Name);
        new BrainstormFilesStep().Run(again);
        Assert.Empty(again.Changed);
        Assert.Contains(HomeMigration.Steps, step => step is BrainstormFilesStep && step.Name == "brainstorm-files");
    }
}
