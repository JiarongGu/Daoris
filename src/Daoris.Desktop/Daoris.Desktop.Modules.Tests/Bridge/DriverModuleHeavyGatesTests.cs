using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// This machine's heavy gates for a repository over the bridge (HEAVYRUN1b2b, D180's HEAVYRUN1b2a note): <c>SET_HEAVY_GATES</c>, the
/// setup page's half of <c>daoris-driver heavy</c>, over the same key in the same file (D50), a repository's whole list at once; the
/// state answering each repository's list as rows, in its order, so no key policy on the bridge respells a repository's name; a list
/// the mapping does not read refused in <see cref="HeavyMapping"/>'s words with nothing written. Edits to <c>driver.json</c> only.
/// </summary>
public sealed class DriverModuleHeavyGatesTests : DriverModuleBridge
{
    private static object Gate(string name, string run, string? cwd = null) => new { name, run, cwd };

    /// <summary>The list is kept whole in the order given, each gate trimmed, and the state answers it as a row.</summary>
    [Fact]
    public async Task A_list_is_kept_whole_in_its_order_and_the_state_answers_it_as_rows()
    {
        var module = Module();
        var empty = await AnswerAsync(module, "STATE");
        Assert.Empty(empty.GetProperty("heavyGates").EnumerateArray());

        var state = await AnswerAsync(module, "SET_HEAVY_GATES", new
        {
            repository = " Work-App ",
            gates = new[] { Gate("web", "npm test -- --maxWorkers=2", "src/web"), Gate(" build ", " dotnet build ", " "), Gate("e2e", "npm run e2e") },
        });

        var row = Assert.Single(state.GetProperty("heavyGates").EnumerateArray());
        Assert.Equal("Work-App", row.GetProperty("repository").GetString());
        var gates = row.GetProperty("gates").EnumerateArray().ToList();
        Assert.Equal(["web", "build", "e2e"], gates.Select(gate => gate.GetProperty("name").GetString()));
        Assert.Equal(["npm test -- --maxWorkers=2", "dotnet build", "npm run e2e"], gates.Select(gate => gate.GetProperty("run").GetString()));
        Assert.Equal("src/web", gates[0].GetProperty("cwd").GetString());
        // A blank folder is none, and none is the checkout itself: left out, or null, never an empty folder.
        Assert.All(gates.Skip(1), gate => Assert.True(!gate.TryGetProperty("cwd", out var cwd) || cwd.ValueKind == JsonValueKind.Null));

        var kept = HeavyMapping.For(DriverConfig.Load(DriverConfigPath), "work-app");
        Assert.Equal(
            [new HeavyGate("web", "npm test -- --maxWorkers=2", "src/web", true), new HeavyGate("build", "dotnet build", null, true), new HeavyGate("e2e", "npm run e2e", null, true)],
            kept);
    }

    /// <summary>A second list replaces the first whole under the spelling first written; none, an empty list or null clears it.</summary>
    [Fact]
    public async Task A_list_replaces_the_one_before_under_its_first_spelling_and_none_clears_it()
    {
        var module = Module();
        await AnswerAsync(module, "SET_HEAVY_GATES", new { repository = "Engine", gates = new[] { Gate("build", "dotnet build"), Gate("unit", "dotnet test") } });
        await AnswerAsync(module, "SET_HEAVY_GATES", new { repository = "shop", gates = new[] { Gate("web", "npm test") } });

        var replaced = await AnswerAsync(module, "SET_HEAVY_GATES", new { repository = "ENGINE", gates = new[] { Gate("unit", "dotnet test"), Gate("build", "dotnet build") } });

        var rows = replaced.GetProperty("heavyGates").EnumerateArray().ToList();
        Assert.Equal(["Engine", "shop"], rows.Select(row => row.GetProperty("repository").GetString()));
        Assert.Equal(["unit", "build"], rows[0].GetProperty("gates").EnumerateArray().Select(gate => gate.GetProperty("name").GetString()));

        var cleared = await AnswerAsync(module, "SET_HEAVY_GATES", new { repository = "engine", gates = (object?)null });
        Assert.Equal(["shop"], cleared.GetProperty("heavyGates").EnumerateArray().Select(row => row.GetProperty("repository").GetString()));
        await AnswerAsync(module, "SET_HEAVY_GATES", new { repository = "shop", gates = Array.Empty<object>() });
        var none = await AnswerAsync(module, "SET_HEAVY_GATES", new { repository = "Engine" });
        Assert.Empty(none.GetProperty("heavyGates").EnumerateArray());
        Assert.Empty(DriverConfig.Load(DriverConfigPath).HeavyGates);
    }

    /// <summary>
    /// A list the mapping would not read is refused whole, in the words every door says (<see cref="HeavyMapping.Problem"/>), and the
    /// file is as it was: no name, no command, a name twice in any case, a folder not under the repository's root, no repository.
    /// </summary>
    [Fact]
    public async Task A_list_it_would_not_read_is_refused_in_the_mappings_words_and_nothing_is_written()
    {
        var module = Module();
        await AnswerAsync(module, "SET_HEAVY_GATES", new { repository = "engine", gates = new[] { Gate("build", "dotnet build") } });
        var before = File.ReadAllText(DriverConfigPath);

        var unnamed = await RefusalAsync(module, "SET_HEAVY_GATES", new { repository = "engine", gates = new[] { Gate("build", "dotnet build"), Gate(" ", "npm test") } });
        var noCommand = await RefusalAsync(module, "SET_HEAVY_GATES", new { repository = "engine", gates = new[] { Gate("web", " ") } });
        var twice = await RefusalAsync(module, "SET_HEAVY_GATES", new { repository = "engine", gates = new[] { Gate("web", "npm test"), Gate("WEB", "npm run e2e") } });
        var outside = await RefusalAsync(module, "SET_HEAVY_GATES", new { repository = "engine", gates = new[] { Gate("web", "npm test", "src/../..") } });
        var noRepository = await RefusalAsync(module, "SET_HEAVY_GATES", new { repository = " ", gates = new[] { Gate("web", "npm test") } });
        var notAList = await RefusalAsync(module, "SET_HEAVY_GATES", new { repository = "engine", gates = "npm test" });

        Assert.Contains(HeavyMapping.Problem([new HeavyGate("", "npm test", null, true)])!, unnamed);
        Assert.Contains(HeavyMapping.Problem([new HeavyGate("web", "", null, true)])!, noCommand);
        Assert.Contains("`WEB` is named twice", twice);
        Assert.Contains("is not under the repository's root", outside);
        Assert.Contains("name one", noRepository);
        Assert.Contains("a list of gates", notAList);
        Assert.Equal(before, File.ReadAllText(DriverConfigPath));
    }

    /// <summary>🔴 The route and the terminal agree (D50): what one sets the other reads, through the same key.</summary>
    [Fact]
    public async Task The_route_and_the_terminal_set_the_same_key()
    {
        var module = Module();
        string Terminal(params string[] args)
        {
            var output = new StringWriter();
            Assert.Equal(0, HeavyMappingCommand.Run(HeavyMappingCommand.Read(args, out _)!, DriverConfigPath, output));
            return output.ToString();
        }

        Terminal("shop", "web=npm test -- --maxWorkers=2", "--cwd", "web");
        var state = await AnswerAsync(module, "STATE");
        var row = Assert.Single(state.GetProperty("heavyGates").EnumerateArray());
        Assert.Equal("shop", row.GetProperty("repository").GetString());
        Assert.Equal("web", row.GetProperty("gates")[0].GetProperty("cwd").GetString());

        await AnswerAsync(module, "SET_HEAVY_GATES", new { repository = "engine", gates = new[] { Gate("build", "dotnet build -nodeReuse:false") } });
        Assert.Contains("dotnet build -nodeReuse:false", Terminal("engine"));
    }

    /// <summary>
    /// HEAVYRUN1b2c: <c>HEAVY_MARKS</c> reads the repository's checkout file and says whether its own gates win (marks some, with their
    /// names), marks none or has no file (this machine's list is used), or does not read (null, with the driver's words: never "marks
    /// none"). A read of one file, never <c>Resolve</c>, which throws with no file and no mapping.
    /// </summary>
    [Fact]
    public async Task Heavy_marks_says_whether_the_repositorys_own_file_wins()
    {
        string Checkout(string name, string? gates)
        {
            var folder = Path.Combine(Home, name);
            Directory.CreateDirectory(folder);
            if (gates is not null) File.WriteAllText(Path.Combine(folder, "daoris.gates.json"), gates);
            return folder;
        }

        var rows = new[]
        {
            ("marks", Checkout("marks", """{"gates":[{"name":"build","run":"x"},{"name":"web","run":"y","heavy":true}]}""")),
            ("none", Checkout("none", """{"gates":[{"name":"build","run":"x"}]}""")),
            ("nofile", Checkout("nofile", null)),
            ("broken", Checkout("broken", "{ \"gates\": [ ")),
        };
        var body = "[" + string.Join(",", rows.Select(row => $$"""{"repository":"{{row.Item1}}","workspace":"w","adopted":true,"root":{{JsonSerializer.Serialize(row.Item2)}}}""")) + "]";
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(new RegistryStandIn(body))));

        var marks = await AnswerAsync(module, "HEAVY_MARKS", new { repository = "marks" });
        Assert.True(marks.GetProperty("marks").GetBoolean());
        Assert.Equal(["web"], marks.GetProperty("names").EnumerateArray().Select(name => name.GetString()));
        // FABRIC-PIPELINE1b: and the gates it declares and does not mark heavy, its light gates, which Setup's sessions' checks name.
        Assert.Equal(["build"], marks.GetProperty("light").EnumerateArray().Select(name => name.GetString()));

        var none = await AnswerAsync(module, "HEAVY_MARKS", new { repository = "NONE" });
        Assert.False(none.GetProperty("marks").GetBoolean());
        Assert.Equal(["build"], none.GetProperty("light").EnumerateArray().Select(name => name.GetString()));

        var noFile = await AnswerAsync(module, "HEAVY_MARKS", new { repository = "nofile" });
        Assert.False(noFile.GetProperty("marks").GetBoolean());

        var broken = await AnswerAsync(module, "HEAVY_MARKS", new { repository = "broken" });
        Assert.Equal(JsonValueKind.Null, broken.GetProperty("marks").ValueKind);
        Assert.Contains("is not valid JSON", broken.GetProperty("refusal").GetString());

        // A repository with no checkout here cannot be told either, and the page is not made to guess.
        var absent = await AnswerAsync(module, "HEAVY_MARKS", new { repository = "ghost" });
        Assert.Equal(JsonValueKind.Null, absent.GetProperty("marks").ValueKind);
        Assert.NotNull(absent.GetProperty("refusal").GetString());
    }

    /// <summary>
    /// HEAVYSLOT1 (the UI/UX design §3.5): <c>HEAVY_SLOT</c> answers who holds the repository's slot, at which gate, and who waits, from
    /// the records beside it, a run's process read live: a holder whose process runs, with its gate; a waiter with no session (a run from
    /// a terminal) answered with none; a waiter whose process is gone left out. A repository with no checkout here is refused in words.
    /// </summary>
    [Fact]
    public async Task Heavy_slot_answers_the_holder_at_its_gate_and_the_live_waiters()
    {
        var checkout = Path.Combine(Home, "engine");
        Directory.CreateDirectory(Path.Combine(checkout, ".git"));
        var key = HeavySlot.KeyOf(checkout)!;
        var self = System.Diagnostics.Process.GetCurrentProcess();
        var started = self.StartTime.ToUniversalTime().Ticks;
        var since = DateTimeOffset.Parse("2026-10-20T09:00:00Z");
        using var slot = HeavySlot.TryTake(Home, key)!;
        slot.Keep(new HeavyHolder(self.Id, started, checkout, ["build", "web"], since, "s-12", null)
        {
            Now = new HeavyGateNow("web", 1, since + TimeSpan.FromMinutes(4)),
        });
        var waits = HeavySlot.WaitersOf(Home, key);
        Directory.CreateDirectory(waits);
        // As a waiting run writes them: one from a terminal whose process runs (another process here, since this one is the holder,
        // and a holder's own process is never its waiter), and one whose process is gone (a start that is not its own).
        var (other, otherStarted) = AnotherProcess(self.Id);
        string Waiter(long start, string at, string? session) => JsonSerializer.Serialize(new { pid = other, started = start, checkout, since = at, session });
        File.WriteAllText(Path.Combine(waits, "a.json"), Waiter(otherStarted, "2026-10-20T09:05:00Z", null));
        File.WriteAllText(Path.Combine(waits, "b.json"), Waiter(otherStarted + 1, "2026-10-20T09:00:00Z", "s-gone"));

        var body = $$"""[{"repository":"engine","workspace":"w","adopted":true,"root":{{JsonSerializer.Serialize(checkout)}}}]""";
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(new RegistryStandIn(body))));

        var answer = await AnswerAsync(module, "HEAVY_SLOT", new { repository = "engine" });
        var holder = answer.GetProperty("holder");
        Assert.Equal("s-12", holder.GetProperty("session").GetString());
        Assert.Equal(["build", "web"], holder.GetProperty("gates").EnumerateArray().Select(gate => gate.GetString()));
        Assert.Equal("web", holder.GetProperty("now").GetProperty("gate").GetString());
        Assert.Equal(1, holder.GetProperty("now").GetProperty("at").GetInt32());
        Assert.Equal(since + TimeSpan.FromMinutes(4), holder.GetProperty("now").GetProperty("since").GetDateTimeOffset());
        var waiter = Assert.Single(answer.GetProperty("waiters").EnumerateArray());
        Assert.True(!waiter.TryGetProperty("session", out var session) || session.ValueKind == JsonValueKind.Null);
        Assert.Equal(since + TimeSpan.FromMinutes(5), waiter.GetProperty("since").GetDateTimeOffset());

        var absent = await AnswerAsync(module, "HEAVY_SLOT", new { repository = "ghost" });
        Assert.True(!absent.TryGetProperty("holder", out var none) || none.ValueKind == JsonValueKind.Null);
        Assert.NotNull(absent.GetProperty("refusal").GetString());
    }

    /// <summary>A running process other than <paramref name="self"/> whose start this user can read: its id and its start in UTC ticks.</summary>
    private static (int Pid, long Started) AnotherProcess(int self)
    {
        foreach (var process in System.Diagnostics.Process.GetProcesses())
        {
            using (process)
            {
                if (process.Id == self) continue;
                try
                {
                    return (process.Id, process.StartTime.ToUniversalTime().Ticks);
                }
                catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException)
                {
                    // Another user's or one gone already: the next.
                }
            }
        }

        throw new InvalidOperationException("no other process here has a start this user can read");
    }

    private sealed class RegistryStandIn(string registry) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(request.RequestUri!.AbsolutePath == "/api/registry" ? registry : "[]", System.Text.Encoding.UTF8, "application/json"),
            });
    }

    [Fact]
    public async Task A_machine_that_kept_none_answers_none()
    {
        var state = await AnswerAsync(Module(), "STATE");

        Assert.Equal(0, state.GetProperty("heavyGates").GetArrayLength());
    }
}
