using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The doors judge a push's force and delete for every agent, whatever its harness's rules. The helper reads git's
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

    [Fact]
    public void A_helpers_push_lines_are_built_into_updates_from_the_remotes_refs_and_the_sources_objects()
    {
        var remote = new Dictionary<string, string> { ["refs/heads/work"] = Old, ["refs/heads/gone"] = Old };
        string? Resolve(string source) => source == "refs/heads/work" ? New : null;

        var batch = GitHelperPush.Build(["push +refs/heads/work:refs/heads/work", "push :refs/heads/gone"], remote, Resolve);

        Assert.NotNull(batch);
        Assert.Equal(new[] { new GitPushUpdate(Old, New, "refs/heads/work"), new GitPushUpdate(Old, Zero, "refs/heads/gone") }, batch.Updates);
        Assert.Equal(new[] { $"push +{New}:refs/heads/work", "push :refs/heads/gone" }, batch.Lines);
        Assert.Equal(new GitPushUpdate(Zero, New, "refs/heads/made"), GitHelperPush.Build(["push refs/heads/work:refs/heads/made"], remote, Resolve)!.Updates[0]);
    }

    [Fact]
    public void A_push_line_whose_source_does_not_resolve_or_that_is_no_push_builds_nothing()
    {
        var remote = new Dictionary<string, string>();

        Assert.Null(GitHelperPush.Build(["push refs/heads/nope:refs/heads/nope"], remote, _ => null));
        Assert.Null(GitHelperPush.Build(["list for-push"], remote, _ => New));
        Assert.Null(GitHelperPush.Build([], remote, _ => New));
    }

    [Fact]
    public void The_remotes_refs_are_those_of_the_answer_to_list_for_push_up_to_its_blank_line()
    {
        var push = new GitHelperPush();
        Assert.Null(push.Refs);
        push.Listing();
        var answer = Encoding.UTF8.GetBytes($"{Old} refs/heads/work\n@refs/heads/work HEAD\n{New} refs/tags/v1\n\n{New} refs/heads/after\n");
        push.Feed(answer[..20], 20);
        Assert.Null(push.Refs);
        push.Feed(answer[20..], answer.Length - 20);

        Assert.Equal(new Dictionary<string, string> { ["refs/heads/work"] = Old, ["refs/tags/v1"] = New }, push.Refs);
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
    public void An_update_is_judged_by_its_kind_a_delete_a_tag_a_mirror_a_force_off_its_history_or_plain()
    {
        static bool Keeps(string old, string now) => old == Old && now == New;

        Assert.Equal(GitPushKind.Delete, GitPushUpdates.KindOf(new(Old, Zero, "refs/heads/work"), Keeps));
        Assert.Equal(GitPushKind.Plain, GitPushUpdates.KindOf(new(Old, New, "refs/heads/work"), Keeps));
        Assert.Equal(GitPushKind.Force, GitPushUpdates.KindOf(new(New, Old, "refs/heads/work"), Keeps));
        Assert.Equal(GitPushKind.Plain, GitPushUpdates.KindOf(new(Zero, New, "refs/heads/made"), (_, _) => false));
        Assert.Equal(GitPushKind.Tag, GitPushUpdates.KindOf(new(Zero, New, "refs/tags/v1"), Keeps));
        Assert.Equal(GitPushKind.Mirror, GitPushUpdates.KindOf(new(Zero, New, "refs/custom/x"), Keeps));
        Assert.Equal(GitPushKind.Mirror, GitPushUpdates.KindOf(new(Zero, New, "refs/remotes/origin/main"), Keeps));
    }

    [Fact]
    public void A_delete_beside_updates_is_a_prune_and_a_delete_alone_is_a_delete()
    {
        var made = new GitPushUpdate(Zero, New, "refs/heads/made");
        var gone = new GitPushUpdate(Old, Zero, "refs/heads/gone");

        Assert.Equal(new[] { GitPushKind.Plain, GitPushKind.Prune }, GitPushUpdates.KindsOf([made, gone], (_, _) => true));
        Assert.Equal(new[] { GitPushKind.Delete }, GitPushUpdates.KindsOf([gone], (_, _) => true));
    }

    private static GitBatchVerdict Judge(
        GitPushUpdate update, GitPushKind kind, string dangerous, IReadOnlyList<GitGrantRef>? grant = null, bool writeByGrant = false) =>
        GitPushJudge.Judge([update], [kind], grant, writeByGrant, dangerous);

    [Theory]
    [InlineData(GitPushKind.Force)]
    [InlineData(GitPushKind.Delete)]
    [InlineData(GitPushKind.Tag)]
    [InlineData(GitPushKind.Mirror)]
    [InlineData(GitPushKind.Prune)]
    public void Each_dangerous_kind_is_refused_by_deny_and_by_the_default_and_passed_by_allow_and_no_person_is_waited_on(GitPushKind kind)
    {
        var update = new GitPushUpdate(Old, New, "refs/heads/work");

        foreach (var value in new[] { GitAccessKeys.Deny, GitAccessKeys.Ask, GitAccessKeys.DefaultOf(GitAccessKeys.Dangerous) })
        {
            var refused = Judge(update, kind, value);
            Assert.Equal((false, "dangerous-denied", GitPushUpdates.DeniedLine(kind)), (refused.Allowed, refused.Code, refused.Line));
        }

        var passed = Judge(update, kind, GitAccessKeys.Allow);
        Assert.Equal((true, "access", false), (passed.Allowed, passed.Code, passed.UsedGrant));
    }

    [Fact]
    public void A_plain_update_is_no_dangerous_act_and_needs_no_value()
    {
        Assert.True(Judge(new(Old, New, "refs/heads/work"), GitPushKind.Plain, GitAccessKeys.Deny).Allowed);
    }

    [Fact]
    public void A_grant_carries_exactly_its_refs_and_a_ref_it_does_not_carry_is_refused()
    {
        var grant = new[] { new GitGrantRef("refs/heads/work", New) };

        var carried = Judge(new(Old, New, "refs/heads/work"), GitPushKind.Plain, GitAccessKeys.Deny, grant, writeByGrant: true);
        var other = Judge(new(Old, New, "refs/heads/other"), GitPushKind.Plain, GitAccessKeys.Allow, grant, writeByGrant: true);
        var moved = Judge(new(Old, Old, "refs/heads/work"), GitPushKind.Plain, GitAccessKeys.Allow, grant, writeByGrant: true);

        Assert.Equal((true, "granted", true), (carried.Allowed, carried.Code, carried.UsedGrant));
        Assert.Equal((false, "grant-bound", GitPushJudge.NotCarriedLine("refs/heads/other")), (other.Allowed, other.Code, other.Line));
        Assert.False(moved.Allowed);
    }

    [Fact]
    public void A_grant_with_no_old_commit_never_carries_a_dangerous_kind_one_with_it_carries_that_exact_update_and_none_carries_a_tag()
    {
        var landing = new[] { new GitGrantRef("refs/heads/work", New) };
        var exact = new[] { new GitGrantRef("refs/heads/work", New, Old) };
        var tagged = new[] { new GitGrantRef("refs/tags/v1", New, Zero) };

        var forced = Judge(new(Old, New, "refs/heads/work"), GitPushKind.Force, GitAccessKeys.Deny, landing, writeByGrant: true);
        var forcedExact = Judge(new(Old, New, "refs/heads/work"), GitPushKind.Force, GitAccessKeys.Deny, exact, writeByGrant: true);
        var tagDenied = Judge(new(Zero, New, "refs/tags/v1"), GitPushKind.Tag, GitAccessKeys.Deny, tagged, writeByGrant: true);
        var tagAllowed = Judge(new(Zero, New, "refs/tags/v1"), GitPushKind.Tag, GitAccessKeys.Allow, tagged, writeByGrant: true);
        var tagElsewhere = Judge(new(Old, New, "refs/tags/v1"), GitPushKind.Tag, GitAccessKeys.Allow, tagged, writeByGrant: true);

        Assert.Equal((false, "dangerous-denied"), (forced.Allowed, forced.Code));
        Assert.Equal((true, true), (forcedExact.Allowed, forcedExact.UsedGrant));
        // A tag is the same act as a branch, its grant naming it for Write, and the place's Dangerous value decides it.
        Assert.Equal((false, "dangerous-denied", GitPushUpdates.DeniedLine(GitPushKind.Tag)), (tagDenied.Allowed, tagDenied.Code, tagDenied.Line));
        Assert.Equal((true, "granted", true), (tagAllowed.Allowed, tagAllowed.Code, tagAllowed.UsedGrant));
        Assert.Equal((false, "grant-bound"), (tagElsewhere.Allowed, tagElsewhere.Code));
    }

    [Fact]
    public void A_batch_with_one_refused_update_is_refused_whole()
    {
        var updates = new GitPushUpdate[] { new(Zero, New, "refs/heads/made"), new(Zero, New, "refs/tags/v1") };

        var denied = GitPushJudge.Judge(updates, [GitPushKind.Plain, GitPushKind.Tag], null, writeByGrant: false, GitAccessKeys.Deny);
        var allowed = GitPushJudge.Judge(updates, [GitPushKind.Plain, GitPushKind.Tag], null, writeByGrant: false, GitAccessKeys.Allow);

        Assert.Equal((false, GitPushUpdates.DeniedLine(GitPushKind.Tag)), (denied.Allowed, denied.Line));
        Assert.True(allowed.Allowed);
    }

    [Fact]
    public void A_grant_is_read_without_being_taken_carries_its_refs_and_is_taken_once()
    {
        var home = Path.Combine(TestHomes.Root, "daoris-grants-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var refs = new[] { new GitGrantRef("refs/heads/work", New), new GitGrantRef("refs/tags/v1", New, Zero) };
            var id = GitGrants.Write(home, "git@host:org/engine.git", refs, now)!;
            var key = GitAddressKeys.Of("git@host:org/engine.git");

            Assert.Equal(refs, GitGrants.Read(home, id, key, now));
            Assert.Equal(refs, GitGrants.Read(home, id, key, now));
            Assert.Null(GitGrants.Read(home, id, "host/org/other", now));
            Assert.Null(GitGrants.Read(home, id, key, now + GitGrants.Bound + TimeSpan.FromSeconds(1)));
            Assert.True(GitGrants.Take(home, id, key, now));
            Assert.False(GitGrants.Take(home, id, key, now));
            Assert.Null(GitGrants.Read(home, id, key, now));
            Assert.Null(GitGrants.Write(home, "git@host:org/engine.git", [], now));
        }
        finally
        {
            try { Directory.Delete(home, recursive: true); } catch (IOException) { }
        }
    }
}
