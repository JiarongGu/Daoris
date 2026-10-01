using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A plugin's health (PLUGUI1d, D119 §2): five states, decided by the loop's own record in the process that
/// runs the loop, and read by a terminal from the machine log's last word. One table of cases holds both
/// readings: every row is played once into the record and the log together, and the two must agree.
/// </summary>
public sealed class PluginHealthTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-health-" + Guid.NewGuid().ToString("N")[..8]);

    private static readonly DateTimeOffset Zero = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    private DateTimeOffset _now = Zero;

    public PluginHealthTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>The kinds of plugin a row is about: what its manifest declares.</summary>
    public enum Kind
    {
        /// <summary>Speaks at <c>quest/consider</c>: the loop keeps its process.</summary>
        Loop,

        /// <summary>Speaks only at <c>work/land</c>: a landing starts it for one frame.</summary>
        Lands,

        /// <summary>Speaks at both.</summary>
        Both,

        /// <summary>Declares an agent and runs nothing itself.</summary>
        Declares,
    }

    /// <summary>
    /// The table. Each step is one second after the last, numbered from 1. A step is
    /// <c>start[:landing]</c>, <c>call:&lt;point&gt;</c>, <c>fail:&lt;where&gt;:&lt;kind&gt;[:&lt;by&gt;]</c>,
    /// <c>stop:&lt;why&gt;[:&lt;by&gt;]</c>, or <c>restart</c> (Daoris itself: a new record, and the log's
    /// <c>app.stopped</c> and <c>app.started</c>). <paramref name="since"/> is the step a state dates from.
    /// </summary>
    public static TheoryData<string, Kind, bool, bool, string[], string, int?, string?, string[]> Cases => new()
    {
        { "no word yet: on and sound, nothing up", Kind.Loop, true, false, [], "ready", null, null, [] },
        { "started by the loop", Kind.Loop, true, false, ["start"], "running", 1, null, ["quest/consider"] },
        { "a good answer keeps it running", Kind.Loop, true, false, ["start", "call:quest/consider"], "running", 1, null, ["quest/consider"] },
        { "a late answer", Kind.Loop, true, false, ["start", "fail:quest/consider:late"], "failing", 2, "quest/consider:late", ["quest/consider"] },
        { "its next good answer clears it", Kind.Loop, true, false, ["start", "fail:quest/consider:late", "call:quest/consider"], "running", 1, null, ["quest/consider"] },
        { "a start that failed", Kind.Loop, true, false, ["fail:start:unstartable"], "failing", 1, "start:unstartable", [] },
        { "a restart of its process alone does not clear it", Kind.Loop, true, false, ["fail:start:unstartable", "start"], "failing", 1, "start:unstartable", ["quest/consider"] },
        { "an exit, then started again", Kind.Loop, true, false, ["start", "fail:process:exited", "start"], "failing", 2, "process:exited", ["quest/consider"] },
        { "switched off and on again starts its record afresh", Kind.Loop, true, false, ["start", "fail:quest/consider:late", "stop:off"], "ready", null, null, [] },
        { "updated starts its record afresh", Kind.Loop, true, false, ["start", "fail:quest/consider:late", "stop:updated", "start"], "running", 4, null, ["quest/consider"] },
        { "a manifest that changed is not the person's act", Kind.Loop, true, false, ["start", "fail:quest/consider:late", "stop:changed", "start"], "failing", 2, "quest/consider:late", ["quest/consider"] },
        { "the loop ended", Kind.Loop, true, false, ["start", "stop:ended"], "ready", null, null, [] },
        { "Daoris restarted: the process went with it, and a new record starts", Kind.Loop, true, false, ["start", "restart"], "ready", null, null, [] },
        { "Daoris restarted: a failure went with the old record", Kind.Loop, true, false, ["start", "fail:quest/consider:late", "restart", "start"], "running", 4, null, ["quest/consider"] },
        { "off is off, whatever its record", Kind.Loop, false, false, ["start"], "off", null, null, [] },
        { "refused is refused, whatever its record", Kind.Loop, true, true, ["start"], "refused", null, null, [] },
        { "a landing plugin, never spoken to", Kind.Lands, true, false, [], "ready", null, null, [] },
        { "a landing plugin after a landing that pushed", Kind.Lands, true, false, ["start:landing", "call:work/land", "stop:ended:landing"], "ready", null, null, [] },
        { "a landing plugin whose landing failed", Kind.Lands, true, false, ["start:landing", "fail:work/land:late:landing", "stop:ended:landing"], "failing", 2, "work/land:late", [] },
        { "a landing plugin that failed to start for a hand-off", Kind.Lands, true, false, ["fail:start:unstartable:hand"], "failing", 1, "start:unstartable", [] },
        { "its next landing that answers clears it", Kind.Lands, true, false, ["start:landing", "fail:work/land:late:landing", "stop:ended:landing", "start:landing", "call:work/land", "stop:ended:landing"], "ready", null, null, [] },
        { "a landing's one frame leaves the loop's process up", Kind.Both, true, false, ["start", "start:landing", "call:work/land", "stop:ended:landing"], "running", 1, null, ["quest/consider", "work/land"] },
        { "a landing's failure is the plugin's, the loop's process still up", Kind.Both, true, false, ["start", "start:landing", "fail:work/land:errored:landing", "stop:ended:landing"], "failing", 3, "work/land:errored", ["quest/consider", "work/land"] },
        { "a plugin that only declares", Kind.Declares, true, false, [], "ready", null, null, [] },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void One_table_read_from_the_record_and_from_the_log(
        string row, Kind kind, bool enabled, bool refused, string[] steps, string state, int? since, string? failure, string[] listening)
    {
        var entry = Entry(kind, enabled, refused);
        using var machine = new MachineLog(_home, "desktop", () => _now);
        var health = new PluginHealth(() => _now);
        var log = new PluginLog(machine, health);

        foreach (var step in steps)
        {
            _now = _now.AddSeconds(1);
            if (step == "restart")
            {
                machine.Info("app.stopped");
                machine.Info("app.started");
                health = new PluginHealth(() => _now);
                log = new PluginLog(machine, health);
                continue;
            }

            Play(log, entry, step);
        }

        var lines = MachineLogReader.Read(Path.Combine(_home, MachineLog.Folder), new LogFilter()).Lines;
        foreach (var (reading, said) in new[] { ("the record", health.Of(entry)), ("the log", PluginHealth.FromLog(entry, lines)) })
        {
            var where = $"{row}, from {reading}";
            Assert.True(state == said.State, $"{where}: {said.State}, not {state}");
            Assert.True(Equals(since is { } at ? Zero.AddSeconds(at) : (DateTimeOffset?)null, said.Since), $"{where}: since {said.Since}");
            Assert.True(failure == (said.Failure is { } f ? $"{f.Where}:{f.Kind}" : null), $"{where}: failure {said.Failure}");
            Assert.True(listening.SequenceEqual(said.Listening), $"{where}: listening {string.Join(",", said.Listening)}");
        }
    }

    /// <summary>A terminal says the state is the log's, and when and from which process its last word came (D119 §2).</summary>
    [Fact]
    public void The_logs_reading_says_when_its_last_word_was_and_which_process_said_it()
    {
        using (var desktop = new MachineLog(_home, "desktop", () => _now))
        {
            _now = _now.AddSeconds(5);
            new PluginLog(desktop).Started("acme.gate", ["quest/consider"], 40, PluginEvents.ByLoop);
        }

        using (var driver = new MachineLog(_home, "driver", () => _now))
        {
            _now = _now.AddSeconds(5);
            new PluginLog(driver).Called("acme.other", "quest/consider", "allow", 3);
        }

        var said = PluginHealth.FromLog(Entry(Kind.Loop, true, false), _home);

        Assert.Equal("running", said.State);
        Assert.Equal(Zero.AddSeconds(5), said.Word);
        Assert.Equal("desktop", said.Source);
    }

    [Fact]
    public void A_log_with_no_word_of_it_is_ready_with_no_word_and_a_torn_line_is_passed_over()
    {
        var folder = Path.Combine(_home, MachineLog.Folder);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "2026-10-01.desktop.jsonl"),
            """{"time":"2026-10-01T09:00:01.000Z","source":"desktop","level":"info","event":"plugin.started","data":{"plugin":"acme.gate","po""" + "\n");

        var said = PluginHealth.FromLog(Entry(Kind.Loop, true, false), _home);

        Assert.Equal("ready", said.State);
        Assert.Null(said.Word);
        Assert.Null(said.Source);
    }

    [Fact]
    public void The_record_of_one_plugin_is_not_anothers_and_an_id_is_read_without_case()
    {
        var health = new PluginHealth(() => _now);
        var log = new PluginLog(null, health);

        log.Failed("acme.other", "quest/consider", PluginEvents.Late, null, 10_000, PluginEvents.ByLoop);
        log.Started("ACME.GATE", ["quest/consider"], 40, PluginEvents.ByLoop);

        Assert.Equal("running", health.Of(Entry(Kind.Loop, true, false)).State);
    }

    // ——— helpers

    private static void Play(PluginLog log, PluginEntry entry, string step)
    {
        var parts = step.Split(':');
        var points = entry.Manifest.Hooks?.Points ?? [];
        switch (parts[0])
        {
            case "start":
                var by = parts.Length > 1 ? parts[1] : PluginEvents.ByLoop;
                // The loop's process listens on the loop's points; a landing's, on what the landing asks.
                log.Started(entry.Manifest.Id, by == PluginEvents.ByLoop ? points : ["work/land"], 40, by);
                break;
            case "call":
                // `quest/consider` and `work/land` are one segment each once rejoined.
                var point = string.Join(':', parts[1..]);
                log.Called(entry.Manifest.Id, point, point == "work/land" ? PluginEvents.Pushed : PluginEvents.Allow, 12);
                break;
            case "fail":
                log.Failed(entry.Manifest.Id, parts[1], parts[2], null, 10_000, parts.Length > 3 ? parts[3] : PluginEvents.ByLoop);
                break;
            case "stop":
                log.Stopped(entry.Manifest.Id, parts[1], parts.Length > 2 ? parts[2] : PluginEvents.ByLoop);
                break;
            default:
                throw new ArgumentException($"no step `{step}`");
        }
    }

    private static PluginEntry Entry(Kind kind, bool enabled, bool refused)
    {
        IReadOnlyList<string>? points = kind switch
        {
            Kind.Loop => ["quest/consider"],
            Kind.Lands => ["work/land"],
            Kind.Both => ["quest/consider", "work/land"],
            _ => null,
        };
        var harnesses = kind == Kind.Declares ? new[] { new PluginHarness("acme-agent", ["acme"]) } : [];
        var manifest = new PluginManifest(
            "acme.gate", 1, "Gate", "1.0.0", "", harnesses, points is null ? null : new PluginHooks(["node", "hooks.mjs"], points), []);
        return new PluginEntry(manifest, "C:/home/plugins/acme.gate", "C:/home/plugins/.data/acme.gate", enabled,
            refused ? "declares agent `claude-code`, which this build already carries" : null);
    }
}
