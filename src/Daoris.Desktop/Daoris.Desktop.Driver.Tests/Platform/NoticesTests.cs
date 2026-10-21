using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// NOTIFY2f's record (D196 point 6, the notification design §2.5): <c>data/notices.json</c>, a map by id written under the keep lock from
/// its read to its rename by any writer, each notice keeping its workspace and what a later build wrote inside it, a week and at most
/// 200 kept, a record it cannot take set aside by the writer alone, a newer one left as it is, a refused write said and dropped. Files
/// only, in a home under <c>_fixtures/</c>; another process is its lock file, the contract between processes.
/// </summary>
public sealed class NoticesTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "notices", Guid.NewGuid().ToString("N")[..8]);

    public NoticesTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Discard(_home);

    private string PathOf => Notices.PathOf(_home);

    private static Notice Ended(string session, DateTimeOffset at, string? workspace = "games") =>
        new($"ended-{session}", Notices.Ended, at, Workspace: workspace, Session: session, Repository: "Engine", State: "failed");

    private static Notice Expired(string press, DateTimeOffset at) =>
        new($"expired-{press}", Notices.Expired, at, Route: "SESSION_INPUT");

    private JsonObject Written() => JsonNode.Parse(File.ReadAllText(PathOf))!.AsObject();

    /// <summary>§2.5: a notice of each kind kept as the record says, newest first, its workspace with it; one of the same id once.</summary>
    [Fact]
    public void A_notice_of_each_kind_is_kept_as_the_record_says()
    {
        Assert.Null(Notices.Add(_home, Ended("s1", Now.AddMinutes(-5)), Now));
        Assert.Null(Notices.Add(_home, Expired("p1", Now.AddMinutes(-1)), Now));
        Assert.Null(Notices.Add(_home, Ended("s1", Now) with { State = "done" }, Now));

        var record = Written();
        Assert.Equal(1, record["schemaVersion"]!.GetValue<int>());
        var ended = record["notices"]!["ended-s1"]!;
        Assert.Equal(("ended", "2026-10-22T08:55:00.000Z", false), (ended["kind"]!.GetValue<string>(), ended["at"]!.GetValue<string>(), ended["read"]!.GetValue<bool>()));
        Assert.Equal(("games", "s1", "Engine", "failed"), (ended["workspace"]!.GetValue<string>(), ended["session"]!.GetValue<string>(), ended["repository"]!.GetValue<string>(), ended["state"]!.GetValue<string>()));
        Assert.Equal("SESSION_INPUT", record["notices"]!["expired-p1"]!["route"]!.GetValue<string>());

        var read = Notices.Read(_home, Now)!;
        Assert.Equal(["expired-p1", "ended-s1"], read.Select(notice => notice.Id));
        Assert.Equal(Ended("s1", Now.AddMinutes(-5)), read[1]);
        Assert.False(File.Exists(KeepLock.PathOf(PathOf)));
    }

    /// <summary>
    /// NOTIFY2g (§2.4): a conversation's answer is one notice per conversation, its workspace with it; a later answer renews it unread at
    /// its moment, and an older or the same one changes nothing.
    /// </summary>
    [Fact]
    public void A_conversation_s_answer_is_one_notice_renewed_unread_by_each_later_answer()
    {
        var answered = Notices.Of(new AttentionEvent(AttentionKind.Answered, "c1", "Engine") { Workspace = "games" }, Now.AddMinutes(-5))!;
        Assert.Equal(new Notice("answered-c1", Notices.Answered, Now.AddMinutes(-5), Workspace: "games", Session: "c1", Repository: "Engine"), answered);

        Assert.Null(Notices.Renew(_home, answered, Now));
        Assert.Null(Notices.MarkRead(_home, ["answered-c1"], Now));
        Assert.True(Assert.Single(Notices.Read(_home, Now)!).Read);

        var stamp = File.GetLastWriteTimeUtc(PathOf);
        Assert.Null(Notices.Renew(_home, answered, Now));
        Assert.True(Assert.Single(Notices.Read(_home, Now)!).Read);
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(PathOf));

        Assert.Null(Notices.Renew(_home, answered with { At = Now.AddMinutes(-1) }, Now));
        var renewed = Assert.Single(Notices.Read(_home, Now)!);
        Assert.Equal((Now.AddMinutes(-1), false, "games"), (renewed.At, renewed.Read, renewed.Workspace));
        Assert.Equal("answered", Written()["notices"]!["answered-c1"]!["kind"]!.GetValue<string>());
    }

    /// <summary>§2.4: read moves only the notices named; dismiss removes them; an id not held is nothing.</summary>
    [Fact]
    public void A_read_moves_the_notices_named_and_a_dismiss_removes_them()
    {
        Notices.Add(_home, Ended("s1", Now.AddMinutes(-3)), Now);
        Notices.Add(_home, Ended("s2", Now.AddMinutes(-2)), Now);
        Notices.Add(_home, Ended("s3", Now.AddMinutes(-1)), Now);

        Assert.Null(Notices.MarkRead(_home, ["ended-s1", "ended-gone"], Now));
        Assert.Equal([false, false, true], Notices.Read(_home, Now)!.Select(notice => notice.Read));

        Assert.Null(Notices.Dismiss(_home, ["ended-s2", "ended-gone"], Now));
        Assert.Equal(["ended-s3", "ended-s1"], Notices.Read(_home, Now)!.Select(notice => notice.Id));
    }

    /// <summary>
    /// §2.5's age: a notice older than seven days is never read, even after a restart that wrote nothing, and goes at the next write.
    /// </summary>
    [Fact]
    public void A_notice_older_than_a_week_is_not_read_and_goes_at_the_next_write()
    {
        Notices.Add(_home, Ended("old", Now.AddDays(-6)), Now.AddDays(-6));
        Notices.Add(_home, Ended("new", Now.AddDays(-1)), Now.AddDays(-1));

        // Nothing written since: the record still holds it, and a reader a day later reads it as gone.
        Assert.NotNull(Written()["notices"]!["ended-old"]);
        Assert.Equal(["ended-new"], Notices.Read(_home, Now.AddDays(1).AddMinutes(1))!.Select(notice => notice.Id));

        Notices.MarkRead(_home, ["ended-new"], Now.AddDays(1).AddMinutes(1));
        Assert.Null(Written()["notices"]!["ended-old"]);
    }

    /// <summary>§2.5's count: at most 200 kept, the oldest going first whether read or not.</summary>
    [Fact]
    public void At_most_two_hundred_are_kept_the_oldest_first_read_or_not()
    {
        for (var n = 0; n < Notices.Most; n++) Notices.Add(_home, Ended($"s{n:000}", Now.AddMinutes(n - 300)), Now);
        Notices.MarkRead(_home, ["ended-s001"], Now);

        Assert.Null(Notices.Add(_home, Ended("last", Now), Now));
        Assert.Null(Notices.Add(_home, Ended("later", Now.AddSeconds(1)), Now.AddSeconds(1)));

        var read = Notices.Read(_home, Now.AddSeconds(1))!;
        Assert.Equal(Notices.Most, read.Count);
        Assert.DoesNotContain(read, notice => notice.Id is "ended-s000" or "ended-s001");
        Assert.Equal(["ended-later", "ended-last"], read.Take(2).Select(notice => notice.Id));
        Assert.Equal(Notices.Most, Written()["notices"]!.AsObject().Count);
    }

    /// <summary>§2.5: a field a later build wrote inside a notice, and a notice this build cannot read, kept across an add, a read, a dismiss and a prune.</summary>
    [Fact]
    public void What_a_later_build_wrote_is_kept_across_every_write()
    {
        Notices.Add(_home, Ended("s1", Now.AddDays(-6)), Now.AddDays(-6));
        Notices.Add(_home, Ended("s2", Now.AddMinutes(-2)), Now);
        var record = Written();
        record["notices"]!["ended-s2"]!["later"] = "kept";
        record["notices"]!["odd"] = 5;
        record["elsewhere"] = new JsonObject { ["a"] = 1 };
        File.WriteAllText(PathOf, record.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        Assert.Null(Notices.Add(_home, Ended("s3", Now), Now.AddDays(1)));
        Assert.Null(Notices.MarkRead(_home, ["ended-s2"], Now.AddDays(1)));
        Assert.Null(Notices.Dismiss(_home, ["ended-s3"], Now.AddDays(1)));

        var after = Written();
        Assert.Null(after["notices"]!["ended-s1"]);
        Assert.Equal("kept", after["notices"]!["ended-s2"]!["later"]!.GetValue<string>());
        Assert.True(after["notices"]!["ended-s2"]!["read"]!.GetValue<bool>());
        Assert.Equal(5, after["notices"]!["odd"]!.GetValue<int>());
        Assert.Equal(1, after["elsewhere"]!["a"]!.GetValue<int>());
    }

    /// <summary>§2.5's workspace: stored when the notice is made, and kept with it, never read again from a session since gone.</summary>
    [Fact]
    public void A_notice_keeps_its_workspace_after_its_session_is_gone()
    {
        Notices.Add(_home, Ended("s1", Now, workspace: "games"), Now);
        Notices.Add(_home, Expired("p1", Now), Now);

        var read = Notices.Read(_home, Now)!;
        Assert.Equal("games", read.Single(notice => notice.Id == "ended-s1").Workspace);
        Assert.Null(read.Single(notice => notice.Id == "expired-p1").Workspace);
    }

    /// <summary>
    /// §2.5: a record that does not parse is left as it is by a reader, which keeps its last snapshot; the writer, holding the lock, sets
    /// it aside under a dated name and begins a new one.
    /// </summary>
    [Fact]
    public void A_record_that_does_not_parse_is_set_aside_by_the_writer_and_left_by_a_reader()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathOf)!);
        File.WriteAllText(PathOf, "{ \"notices\": ");

        Assert.Null(Notices.Read(_home, Now));
        Assert.Equal("{ \"notices\": ", File.ReadAllText(PathOf));

        Assert.Null(Notices.Add(_home, Ended("s1", Now), Now));
        var aside = Path.Combine(Path.GetDirectoryName(PathOf)!, "notices.unreadable-20261022T090000Z.json");
        Assert.Equal("{ \"notices\": ", File.ReadAllText(aside));
        Assert.Equal(["ended-s1"], Notices.Read(_home, Now)!.Select(notice => notice.Id));
    }

    /// <summary>§2.5: a record whose <c>schemaVersion</c> is newer is read as none and never written over.</summary>
    [Fact]
    public void A_newer_record_is_read_as_none_and_left_as_it_is()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathOf)!);
        const string newer = "{ \"schemaVersion\": 2, \"notices\": { \"x\": { \"kind\": \"ended\", \"at\": \"2026-10-22T08:00:00.000Z\" } } }";
        File.WriteAllText(PathOf, newer);

        Assert.Empty(Notices.Read(_home, Now)!);
        Assert.Contains("newer", Notices.Add(_home, Ended("s1", Now), Now));
        Assert.Equal(newer, File.ReadAllText(PathOf));
    }

    /// <summary>§2.5: a write refused, the keep lock held past its wait, is said and dropped, the record unchanged.</summary>
    [Fact]
    public async Task A_write_refused_by_a_held_lock_is_said_and_the_record_left()
    {
        Notices.Add(_home, Ended("s1", Now), Now);
        var before = File.ReadAllText(PathOf);
        string? refused;
        using (KeepLock.Take(PathOf))
        {
            refused = await Task.Run(() => Notices.Add(_home, Ended("s2", Now), Now, within: TimeSpan.FromMilliseconds(100)));
        }

        Assert.NotNull(refused);
        Assert.Equal(before, File.ReadAllText(PathOf));
    }

    /// <summary>§2.5: a write refused, a key written twice in one object, is said and dropped, the record unchanged.</summary>
    [Fact]
    public void A_write_refused_by_a_key_written_twice_is_said_and_the_record_left()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathOf)!);
        const string twice = "{ \"schemaVersion\": 1, \"notices\": {}, \"notices\": {} }";
        File.WriteAllText(PathOf, twice);

        Assert.Contains("twice", Notices.Add(_home, Ended("s1", Now), Now));
        Assert.Equal(twice, File.ReadAllText(PathOf));
    }

    /// <summary>§2.5: a write refused, a shape changed under the edit (its notices no longer a map), is said and dropped, the record unchanged.</summary>
    [Fact]
    public void A_write_refused_by_a_shape_changed_is_said_and_the_record_left()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathOf)!);
        const string changed = "{ \"schemaVersion\": 1, \"notices\": \"none\" }";
        File.WriteAllText(PathOf, changed);

        Assert.NotNull(Notices.Add(_home, Ended("s1", Now), Now));
        Assert.Equal(changed, File.ReadAllText(PathOf));
    }

    /// <summary>
    /// 🔴 §2.5: two writers on one home as two processes. Another process holds the lock as the file says it (<c>notices.json.lock</c>,
    /// its live pid and its moment, which is all a process sees of another's hold) across its read and its write, adding its notice to
    /// what it read; this process's add, made meanwhile, waits for it, then reads the record again and keeps the other's notice.
    /// </summary>
    [Fact]
    public async Task Two_writers_as_two_processes_lose_none()
    {
        Assert.Null(Notices.Add(_home, Ended("s1", Now.AddMinutes(-2)), Now));
        var lockPath = KeepLock.PathOf(PathOf);
        using var other = System.Diagnostics.Process.GetCurrentProcess();
        File.WriteAllText(lockPath, $"{{\"pid\":{other.Id},\"since\":\"{DateTime.UtcNow:yyyy-MM-dd'T'HH:mm:ss.fff'Z'}\"}}\n");
        var theirs = File.ReadAllText(PathOf);

        var mine = Task.Run(() => Notices.Add(_home, Ended("mine", Now), Now));
        await Task.Delay(400);
        Assert.False(mine.IsCompleted, "this process's write waits for the other's hold");
        var record = JsonNode.Parse(theirs)!.AsObject();
        record["notices"]!["ended-other"] = new JsonObject { ["kind"] = "ended", ["at"] = "2026-10-22T08:59:00.000Z", ["read"] = false };
        File.WriteAllText(PathOf, record.ToJsonString());
        File.Delete(lockPath);

        Assert.Null(await mine.WaitAsync(TimeSpan.FromMinutes(1)));
        Assert.Equal(["ended-mine", "ended-other", "ended-s1"], Notices.Read(_home, Now)!.Select(notice => notice.Id));
    }

    /// <summary>§2.5: two writers on one home as two threads, adding, reading and dismissing at once, lose none.</summary>
    [Fact]
    public async Task Two_writers_as_two_threads_lose_none()
    {
        for (var n = 0; n < 20; n++) Notices.Add(_home, Ended($"d{n}", Now.AddMinutes(-30)), Now);

        var adding = Task.Run(() =>
        {
            for (var n = 0; n < 20; n++) Assert.Null(Notices.Add(_home, Ended($"a{n}", Now.AddMinutes(-n)), Now));
        });
        var reading = Task.Run(() =>
        {
            for (var n = 0; n < 20; n++) Assert.Null(Notices.MarkRead(_home, [$"ended-d{n}"], Now));
        });
        var dismissing = Task.Run(() =>
        {
            for (var n = 0; n < 10; n++) Assert.Null(Notices.Dismiss(_home, [$"ended-d{n}"], Now));
        });
        await Task.WhenAll(adding, reading, dismissing);

        var read = Notices.Read(_home, Now)!;
        Assert.Equal(20, read.Count(notice => notice.Id.StartsWith("ended-a", StringComparison.Ordinal)));
        var left = read.Where(notice => notice.Id.StartsWith("ended-d", StringComparison.Ordinal)).ToList();
        Assert.Equal(Enumerable.Range(10, 10).Select(n => $"ended-d{n}").Order(StringComparer.Ordinal), left.Select(notice => notice.Id).Order(StringComparer.Ordinal));
        Assert.All(left, notice => Assert.True(notice.Read));
    }
}
