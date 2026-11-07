#:project ../../../../Daoris.Desktop/Daoris.Desktop.Driver/Daoris.Desktop.Driver.csproj
// EVENTHOOK1a's probe session (the event channel design §4.4, D244 point 8; the event hook design §7: M1, M5, M8).
// It starts a throwaway interactive Claude Code session in a pseudo-console, in a scratch git room, types its input, reads what the
// room's hooks log, and ends it. Never the owner's session or the steward's; it signs nothing in or out and reads no credential.
// Run by hand from the repository's root, never by a gate:
//   dotnet run --file src/Daoris.Cli/test/fixtures/event-hook/ProbeSession.cs --artifacts-path _fixtures/event-probe-build -- [--hour <s>]
// It spends a few short turns of the cheapest model on the machine's signed-in account (the owner allowed it once).

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Daoris.Driver;

var hour = args.Length >= 2 && args[0] == "--hour" ? int.Parse(args[1]) : 3720;
var repo = Directory.GetCurrentDirectory();
if (!File.Exists(Path.Combine(repo, "daoris.json"))) { Console.Error.WriteLine("run from the repository's root"); return 2; }

var scratch = Path.Combine(repo, "_fixtures", "event-probe", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
var room = Path.Combine(scratch, "room");
var roomM = room.Replace('\\', '/');
var outDir = Path.Combine(room, "probe", "out");
var hooksPath = Path.Combine(room, "probe", "hooks.log");
var logPath = Path.Combine(scratch, "probe.log");
const string BuildA = "a1a1a1a1a1a1a1a1";
const string BuildB = "b2b2b2b2b2b2b2b2";
Directory.CreateDirectory(outDir);

void Log(string line)
{
    var text = $"{DateTime.UtcNow:HH:mm:ss} {line}";
    Console.WriteLine(text);
    File.AppendAllText(logPath, text + "\n");
}

void Put(string relative, string content)
{
    var path = Path.Combine(room, relative.Replace('/', Path.DirectorySeparatorChar));
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, content.Replace("\r\n", "\n"));
}

// The room: a git repository holding a stand-in install (`app/daoris-driver/daoris-driver`, `--where`'s stand-in) and a stand-in home
// whose `bin/events-relay/<build>/` holds the relay's stand-in copy, each a bash script under the session's Bash.
Put("README.md", "# event probe room\n");
Put("probe/hour.txt", hour.ToString());
Put("probe/build.txt", BuildA);
Put("probe/hook.sh", """
    #!/usr/bin/env bash
    room="$(cd "$(dirname "$0")/.." && pwd)"
    json="$(cat | tr -d '\r\n')"
    printf '%s\t%s\t%s\n' "$(date +%s%3N)" "$1" "$json" >> "$room/probe/hooks.log"
    if [ "$1" = "SessionStart" ] && printf '%s' "$json" | grep -q '"source": *"startup"'; then
      date +%s > "$room/probe/out/hookwait.start"
      ( sleep "$(cat "$room/probe/hour.txt")"; date +%s > "$room/probe/out/hookwait.end" ) > /dev/null 2>&1 < /dev/null &
    fi
    exit 0
    """);
Put("probe/ancestors.ps1", """
    param([int]$Id)
    $chain = @(); $p = $Id
    for ($i = 0; $i -lt 25 -and $p; $i++) { $chain += $p; $p = (Get-CimInstance Win32_Process -Filter "ProcessId=$p").ParentProcessId }
    $chain -join ' '
    """);
Put("probe/env.sh", """
    #!/usr/bin/env bash
    room="$(cd "$(dirname "$0")/.." && pwd)"
    say() { for v in CLAUDE_CODE_MESSAGING_SOCKET CLAUDE_CODE_MESSAGING_TOKEN; do if [ -n "${!v}" ]; then echo "$1 $v present"; else echo "$1 $v absent"; fi; done; }
    {
      say shell
      bash -c "$(declare -f say); say child"
      env -u CLAUDE_CODE_MESSAGING_SOCKET -u CLAUDE_CODE_MESSAGING_TOKEN bash -c "$(declare -f say); say stand-in"
      echo "names $(env | cut -d= -f1 | grep -E '^(CLAUDE|AI_AGENT)' | sort | tr '\n' ' ')"
    } > "$room/probe/out/m8.txt" 2>&1
    echo done
    """);
Put("app/daoris-driver/daoris-driver", """
    #!/usr/bin/env bash
    room="$(cd "$(dirname "$0")/../.." && pwd -W)"
    echo "$room/daoris-home/bin/events-relay/$(cat "$room/probe/build.txt")/daoris-driver events relay --once --home $room/daoris-home --probe ${@: -1}"
    """);
var relay = """
    #!/usr/bin/env bash
    here="$(cd "$(dirname "$0")" && pwd)"
    room="$(cd "$here/../../../.." && pwd)"
    probe="${@: -1}"
    out="$room/probe/out"
    winpid="$(cat /proc/$$/winpid)"
    {
      echo "winpid $winpid"
      echo "build $(basename "$here")"
      echo "ancestors $(powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$(cygpath -w "$room/probe/ancestors.ps1")" "$winpid")"
      echo "names $(env | cut -d= -f1 | grep -E '^(CLAUDE|AI_AGENT)' | sort | tr '\n' ' ')"
    } > "$out/$probe.part" && mv "$out/$probe.part" "$out/$probe.start"
    case "$probe" in
      hour) sleep "$(cat "$room/probe/hour.txt")"; echo "relay out $((40+2)) after the hour" ;;
      wake) sleep 40; echo "relay out $((20+3))"; echo "relay err $((30+4))" >&2; echo "relay out last $((20+4))" ;;
      silent) sleep 30 ;;
      kill) sleep 900 ;;
      compact) sleep 75; echo "relay out $((50+6))" ;;
      clear) sleep 45; echo "relay out $((60+7))" ;;
      rebuilt) sleep 25; echo "relay out $((70+8))" ;;
      outlive) sleep 40 ;;
    esac
    date +%s > "$out/$probe.end"
    """;
Put($"daoris-home/bin/events-relay/{BuildA}/daoris-driver", relay);

string Hook(string name) => $$"""{"hooks":[{"type":"command","command":"bash probe/hook.sh {{name}}"}]}""";
var where = $"{roomM}/app/daoris-driver/daoris-driver events relay --where";
Put(".claude/settings.json", $$"""
    {
      "permissions": {
        "allow": [
          "Bash(bash probe/env.sh)",
          "Bash({{where}}:*)",
          "Bash({{roomM}}/daoris-home/bin/events-relay/:*)",
          "Bash({{roomM}}/daoris-home/bin/events-relay/*)"
        ]
      },
      "hooks": {
        "SessionStart": [{{Hook("SessionStart")}}],
        "SessionEnd": [{{Hook("SessionEnd")}}],
        "Stop": [{{Hook("Stop")}}],
        "Notification": [{{Hook("Notification")}}],
        "PreCompact": [{{Hook("PreCompact")}}],
        "PreToolUse": [{{Hook("PreToolUse")}}]
      }
    }
    """);
File.WriteAllText(Path.Combine(scratch, "mcp.json"), """{"mcpServers":{}}""");

void Git(params string[] arguments)
{
    var start = new ProcessStartInfo("git") { WorkingDirectory = room, UseShellExecute = false, CreateNoWindow = true };
    foreach (var argument in arguments) start.ArgumentList.Add(argument);
    using var git = Process.Start(start)!;
    git.WaitForExit();
}

Git("init", "-q", "-b", "main");
Git("config", "user.email", "probe@example.com");
Git("config", "user.name", "event probe");
Git("add", "-A");
Git("commit", "-q", "-m", "room");

var claudePath = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';')
    .Select(folder => Path.Combine(folder.Trim(), "claude.exe")).FirstOrDefault(File.Exists);
if (claudePath is null) { Console.Error.WriteLine("claude.exe is not on PATH"); return 2; }
var version = Run(claudePath, "--version");
Log($"M1 version: {version}");

// The enclosing session's markers and messaging variables, and any Daoris variable, are not handed on (DRV4).
var environment = Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>()
    .Select(entry => (Key: (string)entry.Key, Value: (string?)entry.Value ?? ""))
    .Where(entry => !entry.Key.StartsWith("CLAUDE", StringComparison.OrdinalIgnoreCase)
        && !entry.Key.StartsWith("DAORIS", StringComparison.OrdinalIgnoreCase)
        && !entry.Key.Equals("AI_AGENT", StringComparison.OrdinalIgnoreCase))
    .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase);

const string Instructions =
    "You are a measurement probe. Do exactly what each message says and nothing more: never explore, never read a file you were " +
    "not told to, never poll a background command. When a background command you started ends, reply with exactly one line, " +
    "ENDED followed by the last line it printed, or ENDED NONE if it printed nothing, and run no tool.";

var screen = new StringBuilder();
var exited = new ManualResetEventSlim();
var ansi = new Regex(@"\x1b\[[0-?]*[ -/]*[@-~]|\x1b\][^\x07\x1b]*(\x07|\x1b\\)|\x1b[@-Z\\-_]|[\x00-\x08\x0b-\x1f]");
var launch = new TerminalLaunch(
    new TerminalShell("claude", claudePath,
    [
        "--model", "haiku", "--permission-mode", "default", "--setting-sources", "project,local",
        "--strict-mcp-config", "--mcp-config", Path.Combine(scratch, "mcp.json"), "--append-system-prompt", Instructions,
    ]),
    room, environment, 140, 45, Whole: true);

var found = new List<string>();
void Found(string line) { found.Add(line); Log(line); }

using var console = PseudoConsole.Start(
    launch,
    text =>
    {
        var plain = ansi.Replace(text, " ");
        lock (screen)
        {
            screen.Append(plain);
            File.AppendAllText(Path.Combine(scratch, "screen.txt"), plain);
        }
    },
    _ => exited.Set());
Found($"session started under a pseudo-console; claude pid known to the probe: {console.ProcessId != 0}");

var answered = 0;
var trusted = false;
var phase = "start";

List<(long Ms, string Event, string Json)> Hooks()
{
    if (!File.Exists(hooksPath)) return [];
    using var stream = new FileStream(hooksPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
    using var reader = new StreamReader(stream);
    return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(line => line.Split('\t', 3))
        .Where(parts => parts.Length == 3)
        .Select(parts => (long.Parse(parts[0]), parts[1], parts[2]))
        .ToList();
}

int Count(string name, string? holding = null) =>
    Hooks().Count(hook => hook.Event == name && (holding is null || hook.Json.Contains(holding)));

string ScreenSince(int from)
{
    lock (screen) return from < screen.Length ? screen.ToString(from, screen.Length - from) : "";
}

int ScreenAt() { lock (screen) return screen.Length; }

// A permission prompt or the folder's trust question is answered with Enter (its first choice) and recorded.
void Answer()
{
    var seen = ScreenSince(answered);
    var prompt = Regex.Match(seen, @"Do you want to proceed|Do you trust|trust this folder|Yes, I trust|Yes, proceed");
    if (!prompt.Success) return;
    answered = ScreenAt();
    if (prompt.Value.Contains("trust", StringComparison.OrdinalIgnoreCase)) return;
    Found($"{phase}: prompted ({prompt.Value})");
    Thread.Sleep(1500);
    console.Write("\r");
}

bool WaitUntil(Func<bool> done, TimeSpan limit)
{
    var end = DateTime.UtcNow + limit;
    while (DateTime.UtcNow < end)
    {
        if (done()) return true;
        Answer();
        Thread.Sleep(1000);
    }

    return done();
}

void Type(string text)
{
    console.Write(text.Replace("\n", " "));
    Thread.Sleep(700);
    console.Write("\r");
}

string Marker(string name) => Path.Combine(outDir, name);

// One turn: the instruction typed, answered by a stop.
bool Turn(string name, string text)
{
    phase = name;
    var stops = Count("Stop");
    Type(text);
    var done = WaitUntil(() => Count("Stop") > stops, TimeSpan.FromMinutes(3));
    Log($"{name}: turn {(done ? "ended" : "did not end in 3 minutes")}");
    return done;
}

string Arm(string probe) =>
    $"Run with Bash, in the foreground: {where} --probe {probe}\nIt prints one command line. Start that printed command, exactly as " +
    "printed, with Bash in the background (run_in_background: true). Then reply READY and stop.";

// The wake: a stop with nothing typed, after the background command's end marker.
void Wake(string probe, TimeSpan limit, string what)
{
    phase = probe;
    var stops = Count("Stop");
    var woke = WaitUntil(() => Count("Stop") > stops, limit);
    var ended = File.Exists(Marker(probe + ".end"));
    var stop = Hooks().Where(hook => hook.Event == "Stop").LastOrDefault();
    var late = "";
    try { late = ended && woke ? $"{stop.Ms / 1000 - long.Parse(File.ReadAllText(Marker(probe + ".end")).Trim())} s after its end" : ""; }
    catch (Exception error) when (error is IOException or FormatException) { }
    Found($"M5 {what}: the command {(ended ? "reached its end" : "did not reach its end")}; a turn {(woke ? "started" : "did not start")} {late}");
}

try
{
    // The folder's trust question, whose first choice is "No, exit" where the folder pre-approves permissions: its second is taken, again
    // every few seconds until the session starts, since a key sent before the question takes input is lost.
    for (var tries = 0; tries < 20 && Count("SessionStart") == 0; tries++)
    {
        Thread.Sleep(3000);
        if (Count("SessionStart") > 0 || !ScreenSince(0).Contains("trust this folder", StringComparison.OrdinalIgnoreCase)) continue;
        if (!trusted) Found("start: asked whether the folder is trusted");
        trusted = true;
        console.Write("\x1b[B");
        Thread.Sleep(500);
        console.Write("\r");
    }

    WaitUntil(() => Count("SessionStart") > 0, TimeSpan.FromSeconds(30));
    Thread.Sleep(4000);
    Found($"session ready: {Count("SessionStart") > 0}");
    // Nothing is typed into a session whose hooks do not run: every reading rests on them.
    if (Count("SessionStart") == 0 || args.Contains("--startup-only")) throw new OperationCanceledException("not ready, or asked to start only");

    phase = "status";
    var at = ScreenAt();
    Type("/status");
    Thread.Sleep(5000);
    var status = ScreenSince(at);
    var peer = Regex.Match(status, @"Peer address[^\n]{0,120}");
    Found(peer.Success ? $"M1 /status names a Peer address of the form: {Form(peer.Value)}" : "M1 /status shows no Peer address line");
    File.WriteAllText(Path.Combine(scratch, "status.txt"), status);
    console.Write("\x1b");
    Thread.Sleep(1500);

    Turn("hour", "Do exactly this and nothing else. 1) Run with Bash, in the foreground: bash probe/env.sh\n2) " + Arm("hour"));
    WaitUntil(() => File.Exists(Marker("hour.start")), TimeSpan.FromSeconds(30));
    if (File.Exists(Marker("m8.txt"))) foreach (var line in File.ReadAllLines(Marker("m8.txt"))) Found($"M8 {line}");
    var hourStart = File.Exists(Marker("hour.start")) ? File.ReadAllLines(Marker("hour.start")) : [];
    var ancestors = hourStart.FirstOrDefault(line => line.StartsWith("ancestors "))?.Split(' ').Skip(1) ?? [];
    Found($"M5 the background command started: {hourStart.Length > 0}; the session's process among its ancestors: {ancestors.Contains(console.ProcessId.ToString())}");
    Found($"M5 the background command's variable names: {hourStart.FirstOrDefault(line => line.StartsWith("names "))}");
    var hourStarted = DateTime.UtcNow;

    Turn("wake", Arm("wake"));
    Wake("wake", TimeSpan.FromSeconds(160), "stdout then stderr, idle session");

    Turn("silent", Arm("silent"));
    Wake("silent", TimeSpan.FromSeconds(150), "an end with nothing printed");

    Turn("kill", Arm("kill"));
    if (WaitUntil(() => File.Exists(Marker("kill.start")), TimeSpan.FromSeconds(30)))
    {
        Thread.Sleep(2000);
        var killed = File.ReadAllLines(Marker("kill.start")).First(line => line.StartsWith("winpid ")).Split(' ')[1];
        Run("taskkill", "/T", "/F", "/PID", killed);
        File.WriteAllText(Marker("kill.end"), DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
        Wake("kill", TimeSpan.FromSeconds(120), "an end by a kill");
    }
    else
    {
        Found("M5 an end by a kill: the command did not start");
    }

    // A change of build: the stand-in `--where` now prints another build's folder under the same prefix.
    Directory.CreateDirectory(Path.Combine(room, "daoris-home", "bin", "events-relay", BuildB));
    File.Copy(Path.Combine(room, "daoris-home", "bin", "events-relay", BuildA, "daoris-driver"),
        Path.Combine(room, "daoris-home", "bin", "events-relay", BuildB, "daoris-driver"));
    File.WriteAllText(Path.Combine(room, "probe", "build.txt"), BuildB);
    Turn("rebuilt", Arm("rebuilt"));
    Wake("rebuilt", TimeSpan.FromSeconds(150), "after a change of build");

    var hourLeft = TimeSpan.FromSeconds(hour) - (DateTime.UtcNow - hourStarted) + TimeSpan.FromMinutes(3);
    Log($"waiting {hourLeft.TotalMinutes:0} minutes for the hour's command");
    Wake("hour", hourLeft, $"a wait of {hour} s");
    Found($"M5 a waiter started by the SessionStart hook: started {File.Exists(Marker("hookwait.start"))}, reached its end {File.Exists(Marker("hookwait.end"))}");

    Turn("compact", Arm("compact"));
    phase = "compact";
    var compacts = Count("SessionStart", "compact");
    Type("/compact");
    var compacted = WaitUntil(() => Count("SessionStart", "compact") > compacts, TimeSpan.FromMinutes(4));
    Found($"M5 /compact {(compacted ? "ended" : "did not end in 4 minutes")} before the command's end: {!File.Exists(Marker("compact.end"))}");
    Wake("compact", TimeSpan.FromSeconds(200), "across a compaction");

    Turn("clear", Arm("clear"));
    phase = "clear";
    var clears = Count("SessionStart", "clear");
    Type("/clear");
    WaitUntil(() => Count("SessionStart", "clear") > clears, TimeSpan.FromSeconds(60));
    Wake("clear", TimeSpan.FromSeconds(170), "across /clear");

    Turn("outlive", Arm("outlive"));
    phase = "exit";
    Type("/exit");
    var gone = exited.Wait(TimeSpan.FromSeconds(60));
    Thread.Sleep(TimeSpan.FromSeconds(70));
    Found($"M5 past the session's end: the session exited {gone}; the command reached its end {File.Exists(Marker("outlive.end"))}");
}
catch (Exception error)
{
    Found($"the probe stopped at {phase}: {error.GetType().Name}");
}
finally
{
    console.Dispose();
}

// What each transcript says: the wakes' notifications, the replies, and the tokens spent.
var transcripts = Hooks().Select(hook => Regex.Match(hook.Json, @"""transcript_path"":\s*""((?:[^""\\]|\\.)*)"""))
    .Select(match => match.Success ? Regex.Unescape(match.Groups[1].Value) : null)
    .Where(path => path is not null && File.Exists(path)).Distinct().ToList();
var spend = new Dictionary<string, (string Model, long Input, long Output, long Created, long Read)>();
var notes = new List<string>();
foreach (var transcript in transcripts)
{
    foreach (var line in File.ReadLines(transcript!))
    {
        JsonDocument entry;
        try { entry = JsonDocument.Parse(line); }
        catch (JsonException) { continue; }

        using var held = entry;
        var root = entry.RootElement;
        if (!root.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object) continue;
        if (root.TryGetProperty("type", out var type) && type.GetString() == "assistant" && message.TryGetProperty("usage", out var usage))
        {
            long Of(string name) => usage.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt64() : 0;
            spend[message.GetProperty("id").GetString()!] = (message.GetProperty("model").GetString()!, Of("input_tokens"),
                Of("output_tokens"), Of("cache_creation_input_tokens"), Of("cache_read_input_tokens"));
        }

        var text = message.TryGetProperty("content", out var content) ? content.ToString() : "";
        if (Regex.IsMatch(text, @"relay (out|err)|background|task-notification|ENDED", RegexOptions.IgnoreCase))
            notes.Add($"{type.GetString()}: {Form(text)[..Math.Min(500, Form(text).Length)]}");
    }
}

var summary = new StringBuilder();
summary.AppendLine($"version: {version}");
foreach (var line in found) summary.AppendLine(line);
foreach (var model in spend.Values.GroupBy(value => value.Model))
{
    summary.AppendLine($"spend {model.Key}: {model.Count()} messages, input {model.Sum(v => v.Input)}, output {model.Sum(v => v.Output)}, " +
        $"cache written {model.Sum(v => v.Created)}, cache read {model.Sum(v => v.Read)}");
}

summary.AppendLine("the commands the session ran:");
foreach (var hook in Hooks().Where(hook => hook.Event == "PreToolUse"))
{
    JsonElement input;
    try { input = JsonDocument.Parse(hook.Json).RootElement; }
    catch (JsonException) { continue; }

    if (input.TryGetProperty("tool_input", out var tool)) summary.AppendLine($"  {input.GetProperty("tool_name").GetString()}: {Form(tool.ToString())}");
}

summary.AppendLine("transcript lines naming a background end or a relay line:");
foreach (var note in notes) summary.AppendLine("  " + note);
File.WriteAllText(Path.Combine(scratch, "summary.txt"), summary.ToString());
Console.WriteLine(summary);

try
{
    foreach (var file in Directory.EnumerateFiles(room, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
    Directory.Delete(room, recursive: true);
}
catch (Exception error) { Log($"the room was not removed: {error.GetType().Name}"); }
return 0;

// A value's form with every path, address and number replaced by a placeholder, so the record names none.
string Form(string value) => Regex.Replace(Regex.Replace(Regex.Replace(value.Replace(roomM, "<room>").Replace(room, "<room>"),
    @"[A-Za-z]:[\\/][^\s""',]*", "<path>"), @"\\\\[.?]\\pipe\\[^\s""',]*", @"\\.\pipe\<name>"), @"\b\d{3,}\b", "<n>");

static string Run(string program, params string[] arguments)
{
    var start = new ProcessStartInfo(program) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
    foreach (var argument in arguments) start.ArgumentList.Add(argument);
    using var process = Process.Start(start)!;
    var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
    process.WaitForExit();
    return output.Trim();
}
