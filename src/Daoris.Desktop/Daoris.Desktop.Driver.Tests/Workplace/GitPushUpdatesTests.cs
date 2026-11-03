using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// D233 choice 3: the doors judge a push's force and delete for every agent, whatever its harness's rules. The helper reads git's
/// <c>push</c> lines; the ssh door reads receive-pack's commands, forwarded byte for byte once judged; a force or delete is refused
/// unless Dangerous allows it, and the landing's grant passes it as it passes the landing's push.
/// </summary>
public sealed class GitPushUpdatesTests
{
    private const string Old = "1111111111111111111111111111111111111111";

    private const string New = "2222222222222222222222222222222222222222";

    private const string Zero = "0000000000000000000000000000000000000000";

    private static byte[] Packet(string text)
    {
        var payload = Encoding.UTF8.GetBytes(text);
        return [.. Encoding.ASCII.GetBytes((payload.Length + 4).ToString("x4")), .. payload];
    }

    [Theory]
    [InlineData("push refs/heads/work:refs/heads/work", GitPushKind.Plain)]
    [InlineData("push +refs/heads/work:refs/heads/work", GitPushKind.Force)]
    [InlineData("push :refs/heads/work", GitPushKind.Delete)]
    public void A_helpers_push_line_says_what_it_asks(string line, GitPushKind kind)
    {
        Assert.Equal(kind, GitPushUpdates.OfHelperPush(line));
    }

    [Fact]
    public void A_line_that_is_no_push_asks_nothing()
    {
        Assert.Null(GitPushUpdates.OfHelperPush("list for-push"));
        Assert.Null(GitPushUpdates.OfHelperPush("fetch 1111 refs/heads/work"));
    }

    [Fact]
    public void Receive_packs_commands_are_read_to_their_flush_and_every_byte_read_is_kept_to_forward()
    {
        var sent = new MemoryStream();
        foreach (var part in new[]
        {
            Packet($"shallow {Old}\n"),
            Packet($"{Old} {New} refs/heads/work\0report-status side-band-64k\n"),
            Packet($"{Old} {Zero} refs/heads/gone\n"),
            Encoding.ASCII.GetBytes("0000"),
        })
        {
            sent.Write(part);
        }

        var whole = sent.ToArray();
        sent.Write("PACK"u8);
        sent.Position = 0;

        var commands = GitPushUpdates.ReadCommands(sent);

        Assert.NotNull(commands);
        Assert.Equal(whole, commands.Value.Read);
        Assert.Equal(
            new[] { new GitPushUpdate(Old, New, "refs/heads/work"), new GitPushUpdate(Old, Zero, "refs/heads/gone") },
            commands.Value.Updates);
        Assert.Equal('P', (char)sent.ReadByte());
    }

    [Fact]
    public void A_stream_that_ends_before_its_flush_reads_as_none()
    {
        Assert.Null(GitPushUpdates.ReadCommands(new MemoryStream(Packet($"{Old} {New} refs/heads/work\n"))));
    }

    [Fact]
    public void An_update_is_a_delete_a_force_off_its_history_or_plain()
    {
        static bool Keeps(string old, string now) => old == Old && now == New;

        Assert.Equal(GitPushKind.Delete, GitPushUpdates.KindOf(new(Old, Zero, "refs/heads/work"), Keeps));
        Assert.Equal(GitPushKind.Plain, GitPushUpdates.KindOf(new(Old, New, "refs/heads/work"), Keeps));
        Assert.Equal(GitPushKind.Force, GitPushUpdates.KindOf(new(New, Old, "refs/heads/work"), Keeps));
        Assert.Equal(GitPushKind.Plain, GitPushUpdates.KindOf(new(Zero, New, "refs/heads/made"), (_, _) => false));
        Assert.Equal(GitPushKind.Delete, GitPushUpdates.Most([GitPushKind.Plain, GitPushKind.Delete, GitPushKind.Force]));
        Assert.Equal(GitPushKind.Plain, GitPushUpdates.Most([]));
    }

    [Theory]
    [InlineData(GitAccessKeys.Deny, false, false, "dangerous-denied")]
    [InlineData(GitAccessKeys.Allow, false, true, "access")]
    [InlineData(GitAccessKeys.Deny, true, true, "granted")]
    public void A_force_or_delete_is_refused_unless_git_access_allows_it_or_the_landings_grant_carries_it(
        string value, bool granted, bool allowed, string code)
    {
        var verdict = GitAccessRules.JudgeDangerous(value, granted);

        Assert.Equal((allowed, code), (verdict.Allowed, verdict.Code));
        if (!allowed) Assert.Equal(GitPushUpdates.DeniedLine, verdict.Line);
    }

    [Fact]
    public void Dangerous_asked_is_held_at_the_door_and_its_default_refuses()
    {
        Assert.True(GitAccessRules.JudgeDangerous(GitAccessKeys.Ask, granted: false).Ask);
        Assert.False(GitAccessRules.JudgeDangerous(GitAccessKeys.DefaultOf(GitAccessKeys.Dangerous), granted: false).Allowed);
    }
}
