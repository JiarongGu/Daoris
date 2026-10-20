using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// 🔴 HOMELEASE1 (the terminal RPC design §5): one instance per install was not one writer per home. A verb acting alone, the
/// first-start migration and the launcher's swap took no lock, and two paths to one folder were two homes to every check. The
/// driver's lock grew into the home's lease, which every writer takes, by any path to the folder.
/// </summary>
/// <remarks>
/// A lease naming THIS test process is a live holder as far as any taker can tell — which is what a holder elsewhere on the
/// machine looks like, without starting one. A gone one is a process id no process has. The rows that start a process (an
/// exited one's id, a junction) are <see cref="HomeLeaseProcessTests"/>'; the launcher's swap holding it is read off the
/// launcher's source by the modules' <c>InstallHomeTests</c>, whose gate a change to the launcher reaches.
/// </remarks>
public sealed class HomeLeaseTests : IDisposable
{
    /// <summary>A process id no process has: Windows hands out multiples of four, from the bottom.</summary>
    private const int NoProcess = 0x7FFFFFFC;

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-home-lease-" + Guid.NewGuid().ToString("N")[..8]);

    public HomeLeaseTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string LeaseFile => HomeLease.PathIn(_home);

    private static long OurStart()
    {
        using var self = Process.GetCurrentProcess();
        return self.StartTime.ToUniversalTime().Ticks;
    }

    /// <summary>
    /// A lease by hand, as another holder writes it: the file is the contract (twins.md). At the place this build takes it, or,
    /// <paramref name="older"/>, where a build from before HOMELAYOUT3 took it; an older build's <c>driver.lock</c> is at its old place.
    /// </summary>
    private void Plant(string kind, int? pid = null, long? started = null, string? what = null, string file = HomeLease.FileName, bool older = false)
    {
        var lease = new JsonObject
        {
            ["kind"] = kind, ["pid"] = pid ?? Environment.ProcessId, ["started"] = started ?? OurStart(),
            ["since"] = "2026-10-16T08:02:13+00:00",
        };
        if (what is not null) lease["what"] = what;
        var path = file == HomeLease.FileName && !older
            ? HomeLease.PathIn(_home)
            : DaorisHome.Places(_home, file == HomeLease.FileName ? HomeEntry.HomeLease : HomeEntry.DriverLock)[^1];
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, lease.ToJsonString());
    }

    /// <summary>
    /// 🔴 HOMELAYOUT3: the lease is taken at the home's <c>home-lease</c> entry's new place, <c>run/home.lease</c>, its folder made;
    /// a live lease at the old place, a build's from before the switch, still holds the home; and a stale one there is left where it
    /// is. Every spelling of an install (its folder, its <c>data/</c>) meets the one lease.
    /// </summary>
    [Fact]
    public void The_lease_is_taken_at_its_new_place_and_an_older_builds_at_the_old_place_still_holds()
    {
        File.WriteAllText(Path.Combine(_home, DaorisHome.InstallMarker), "# Daoris — installed desktop\n");
        var data = Path.Combine(_home, DaorisHome.InstallData);
        using (var held = HomeLease.TryAcquire(data, LeaseKind.Verb, out _, "trees land"))
        {
            Assert.NotNull(held);
            Assert.True(File.Exists(Path.Combine(_home, "run", HomeLease.FileName)));
            Assert.Null(HomeLease.TryAcquire(_home, LeaseKind.Desktop, out var met));
            Assert.Equal("trees land", met!.What);
        }

        Directory.CreateDirectory(data);
        var older = Path.Combine(data, HomeLease.FileName);
        File.WriteAllText(older, new JsonObject
        {
            ["kind"] = "desktop", ["pid"] = Environment.ProcessId, ["started"] = OurStart(), ["since"] = "2026-10-16T08:02:13+00:00",
        }.ToJsonString());
        Assert.Null(HomeLease.TryAcquire(data, LeaseKind.Verb, out var holder, "trees land"));
        Assert.Equal(LeaseKind.Desktop, holder!.Kind);
        Assert.Equal(LeaseKind.Desktop, HomeLease.HeldBy(_home)!.Kind);

        File.WriteAllText(older, new JsonObject
        {
            ["kind"] = "desktop", ["pid"] = NoProcess, ["started"] = OurStart(), ["since"] = "2026-10-16T08:02:13+00:00",
        }.ToJsonString());
        using var after = HomeLease.TryAcquire(data, LeaseKind.Verb, out _, "trees land");
        Assert.NotNull(after);
        Assert.True(File.Exists(older), "a stale lease at the old place is left where it is");
    }

    [Fact]
    public void A_free_home_is_taken_named_after_its_holder_and_the_homes_id_and_let_go_on_release()
    {
        var held = HomeLease.TryAcquire(_home, LeaseKind.Verb, out var holder, "trees land");

        Assert.NotNull(held);
        Assert.Null(holder);
        Assert.True(held!.Holds);
        var named = HomeLease.HeldBy(_home);
        Assert.Equal(LeaseKind.Verb, named!.Kind);
        Assert.Equal(Environment.ProcessId, named.Pid);
        Assert.Equal("trees land", named.What);
        using (var lease = JsonDocument.Parse(File.ReadAllText(LeaseFile)))
        {
            Assert.Equal("verb", lease.RootElement.GetProperty("kind").GetString());
            Assert.Equal(HomeIdentity.Read(_home), lease.RootElement.GetProperty("home").GetString());
        }

        held.Dispose();
        Assert.False(File.Exists(LeaseFile));
        Assert.Null(HomeLease.HeldBy(_home));
    }

    /// <summary>🔴 Two takers at once: of eight, exactly one holds the home, and every other is told it is held.</summary>
    [Fact]
    public async Task Of_two_takers_at_once_one_holds_it()
    {
        using var start = new ManualResetEventSlim();
        var tries = Enumerable.Range(0, 8).Select(n => Task.Run(() =>
        {
            start.Wait();
            return HomeLease.TryAcquire(_home, n % 2 == 0 ? LeaseKind.Verb : LeaseKind.Desktop, out _);
        })).ToArray();
        start.Set();

        var leases = await Task.WhenAll(tries);

        var holding = Assert.Single(leases, lease => lease is not null);
        holding!.Dispose();
    }

    /// <summary>🔴 A dead holder never blocks: its process gone, its process id another process's since, or a lease that does not read.</summary>
    [Theory]
    [InlineData("gone")]
    [InlineData("reused")]
    [InlineData("torn")]
    public void A_dead_holder_never_blocks(string how)
    {
        switch (how)
        {
            case "gone":
                Plant("verb", pid: NoProcess, what: "trees land");
                break;
            case "reused":
                Plant("desktop", started: OurStart() - TimeSpan.FromMinutes(5).Ticks);
                break;
            default:
                Directory.CreateDirectory(Path.GetDirectoryName(LeaseFile)!);
                File.WriteAllText(LeaseFile, "{ \"kind\": \"verb\", \"pid\": ");
                break;
        }

        Assert.Null(HomeLease.HeldBy(_home));
        using var held = HomeLease.TryAcquire(_home, LeaseKind.Desktop, out var holder);

        Assert.NotNull(held);
        Assert.Null(holder);
        Assert.Equal(LeaseKind.Desktop, HomeLease.HeldBy(_home)!.Kind);
    }

    /// <summary>
    /// 🔴 Another path to the folder is the same home: the lease is a file in it, so a taker through another spelling meets it.
    /// The junction's row, which starts <c>cmd</c> to make one, is <see cref="HomeLeaseProcessTests"/>'.
    /// </summary>
    [Fact]
    public void Another_path_to_the_folder_meets_the_same_lease()
    {
        using var held = HomeLease.TryAcquire(_home, LeaseKind.Verb, out _, "sessions archive");
        Directory.CreateDirectory(Path.Combine(_home, "sub"));

        var aliases = new List<string> { Path.Combine(_home, "sub", "..") };
        if (OperatingSystem.IsWindows()) aliases.Add(_home.ToUpperInvariant());
        foreach (var alias in aliases)
        {
            Assert.Null(HomeLease.TryAcquire(alias, LeaseKind.Desktop, out var holder));
            Assert.Equal("sessions archive", holder!.What);
            Assert.Equal(HomeLease.HeldBy(_home), HomeLease.HeldBy(alias));
        }
    }

    /// <summary>
    /// 🔴 A verb's wait (§5): another verb holds the home, so this one waits up to its thirty seconds, looking again, and then is
    /// refused with the sentence naming which verb holds it. Nothing was done.
    /// </summary>
    [Fact]
    public void A_verb_waits_for_another_verbs_hold_then_says_which_verb_holds_it()
    {
        Plant("verb", what: "trees sync");
        var now = DateTimeOffset.Parse("2026-10-16T08:00:00+00:00");
        var slept = TimeSpan.Zero;

        var lease = HomeLease.ForVerb(_home, "trees land", out var refusal,
            sleep: span => { slept += span; now += span; }, clock: () => now);

        Assert.Null(lease);
        Assert.True(slept >= HomeLease.Wait, $"waited {slept}");
        Assert.StartsWith($"{_home} is held by a terminal's `trees sync` (pid {Environment.ProcessId}, since ", refusal);
        Assert.Contains("for the 30 seconds this waited: nothing was done", refusal);
        Assert.Equal("trees sync", HomeLease.HeldBy(_home)!.What);
    }

    /// <summary>The other verb ends inside the wait: this one takes the home the moment it is free.</summary>
    [Fact]
    public void A_verb_takes_the_home_once_another_verb_lets_it_go()
    {
        Plant("verb", what: "trees sync");
        var looks = 0;

        using var lease = HomeLease.ForVerb(_home, "trees land", out var refusal,
            sleep: _ => { if (++looks == 3) File.Delete(LeaseFile); });

        Assert.Null(refusal);
        Assert.True(lease!.Holds);
        Assert.Equal("trees land", HomeLease.HeldBy(_home)!.What);
    }

    /// <summary>
    /// A verb finding an instance's lease acts beside it, holding nothing, as verbs always have: until RPC1's pipe makes it that
    /// instance's client, a refusal there would take the terminal's door away. Letting go leaves the instance's lease.
    /// </summary>
    [Fact]
    public void A_verb_runs_beside_an_instance_and_leaves_its_lease()
    {
        Plant("desktop");
        var before = File.ReadAllText(LeaseFile);

        var lease = HomeLease.ForVerb(_home, "trees land", out var refusal, sleep: _ => throw new InvalidOperationException("waited"));

        Assert.Null(refusal);
        Assert.False(lease!.Holds);
        Assert.Equal(LeaseKind.Desktop, lease.Beside!.Kind);
        lease.Dispose();
        Assert.Equal(before, File.ReadAllText(LeaseFile));
    }

    /// <summary>A migration's or a swap's wait is for any holder, an instance's too: neither moves files under one.</summary>
    [Fact]
    public void A_swap_waits_for_any_holder_and_is_told_who()
    {
        Plant("desktop");
        var now = DateTimeOffset.UtcNow;

        var lease = HomeLease.Await(_home, LeaseKind.Swap, null, TimeSpan.FromSeconds(1), out var holder,
            sleep: span => now += span, clock: () => now);

        Assert.Null(lease);
        Assert.Equal(LeaseKind.Desktop, holder!.Kind);
        Assert.Contains("the desktop (pid", HomeLease.WaitRefusal(_home, holder));
    }

    /// <summary>
    /// `--share` runs a loop beside an instance, never beside an act: a verb, a migration or a swap holding the home is
    /// waited for as any loop waits.
    /// </summary>
    [Fact]
    public void Sharing_runs_beside_an_instance_but_not_beside_an_act()
    {
        Plant("desktop");
        using (var beside = HomeLease.Share(_home, LeaseKind.Headless, out var none))
        {
            Assert.Null(none);
            Assert.False(beside!.Holds);
            Assert.Equal(LeaseKind.Desktop, beside.Beside!.Kind);
        }

        Plant("migration");
        Assert.Null(HomeLease.Share(_home, LeaseKind.Headless, out var act));
        Assert.Equal(LeaseKind.Migration, act!.Kind);
        Assert.StartsWith("the desktop's first-start move of an older home (pid", act.Named);
    }

    /// <summary>
    /// An older build's <c>driver.lock</c> still holds the home while its holder lives, so a desktop from before HOMELEASE1 is a
    /// holder to this one; a stale one is ignored and left where it is.
    /// </summary>
    [Fact]
    public void An_older_builds_lock_holds_the_home_while_its_holder_lives()
    {
        Plant("headless", file: HomeLease.LegacyFileName);

        Assert.Null(HomeLease.TryAcquire(_home, LeaseKind.Desktop, out var holder));
        Assert.Equal(LeaseKind.Headless, holder!.Kind);
        Assert.Equal(LeaseKind.Headless, HomeLease.HeldBy(_home)!.Kind);

        Plant("headless", pid: NoProcess, file: HomeLease.LegacyFileName);
        using var held = HomeLease.TryAcquire(_home, LeaseKind.Desktop, out _);
        Assert.NotNull(held);
        Assert.True(File.Exists(DaorisHome.Places(_home, HomeEntry.DriverLock)[^1]));
    }

    /// <summary>A release takes away only the lease it wrote — never one another holder has written since.</summary>
    [Fact]
    public void A_release_removes_only_the_lease_it_wrote()
    {
        var held = HomeLease.TryAcquire(_home, LeaseKind.Verb, out _, "plugins install");
        Plant("desktop");

        held!.Dispose();

        Assert.Equal(LeaseKind.Desktop, HomeLease.HeldBy(_home)!.Kind);
    }
}
