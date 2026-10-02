using System.Globalization;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>cooling.json</c> read and ended (TOOL4e, D125 §2.3, §2.4): the driver writes an account's cool-off when a limit is
/// read, and both twins read it and end one early, the screen's <i>Try now</i> and the terminal's
/// <c>daoris agent profile ready</c>. The driver's half of a TWIN with the CLI's <c>cooling.ts</c>, whose
/// <c>cooling.test.ts</c> holds the same tables, row for row and in the same order, and parses these theories to hold
/// them to its own, cell for cell.
/// </summary>
/// <remarks>
/// <para>An entry is said as JSON: whose, until when and seen when (UTC, to the second), and what said so; <c>null</c>
/// is no account cooling. An <c>after</c> is the file's JSON once the end is done, or <c>unchanged</c> where nothing was
/// written.</para>
/// <para>🔴 <b>Keep each row on one line, its cells literals</b>: the CLI's test reads them.</para>
/// </remarks>
public sealed class CoolingTwinTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-cooling-twin-" + Guid.NewGuid().ToString("N")[..8]);

    public CoolingTwinTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string File_ => AccountCooling.PathOf(_home);

    private static DateTimeOffset Moment(string text) =>
        DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

    private static JsonObject Said(CoolingEntry entry) => new()
    {
        ["agent"] = entry.Agent,
        ["account"] = entry.Account,
        ["until"] = AccountCooling.Stamp(entry.Until),
        ["stated"] = entry.Stated,
        ["window"] = entry.Window,
        ["seen"] = AccountCooling.Stamp(entry.Seen),
        ["session"] = entry.Session,
        ["assumedZone"] = entry.AssumedZone,
        ["notBelieved"] = entry.NotBelieved,
    };

    // ——— Reading (§2.3): missing or unreadable is none cooling; a passed `until` is ready; names compare without case.

    [Theory]
    [InlineData("missing is no account cooling", null, "2026-10-01T08:15:00Z", "claude-code", "account-1", null)]
    [InlineData("not JSON is none", "not json", "2026-10-01T08:15:00Z", "claude-code", "account-1", null)]
    [InlineData("JSON that is not an object is none", "[1, 2]", "2026-10-01T08:15:00Z", "claude-code", "account-1", null)]
    [InlineData("an entry with no until is none", """{"claude-code":{"account-1":{"stated":true}}}""", "2026-10-01T08:15:00Z", "claude-code", "account-1", null)]
    [InlineData("an until that is not ISO 8601 is none", """{"claude-code":{"account-1":{"until":"Oct 3"}}}""", "2026-10-01T08:15:00Z", "claude-code", "account-1", null)]
    [InlineData("a date that does not exist is none", """{"claude-code":{"account-1":{"until":"2026-11-31T10:17:00Z"}}}""", "2026-10-01T08:15:00Z", "claude-code", "account-1", null)]
    [InlineData("an agent that is not an object is none", """{"claude-code":[1]}""", "2026-10-01T08:15:00Z", "claude-code", "account-1", null)]
    [InlineData("a named account cooling, read whole", """{"claude-code":{"account-1":{"until":"2026-10-03T10:17:00Z","stated":true,"window":"weekly","seen":"2026-10-01T08:15:00Z","session":"3f9c2a71"}}}""", "2026-10-01T08:15:00Z", "claude-code", "account-1", """{"agent":"claude-code","account":"account-1","until":"2026-10-03T10:17:00Z","stated":true,"window":"weekly","seen":"2026-10-01T08:15:00Z","session":"3f9c2a71","assumedZone":false,"notBelieved":false}""")]
    [InlineData("the tool's own home is the empty name", """{"claude-code":{"":{"until":"2026-10-03T10:17:00Z","stated":false,"seen":"2026-10-01T08:15:00Z"}}}""", "2026-10-01T08:15:00Z", "claude-code", null, """{"agent":"claude-code","account":null,"until":"2026-10-03T10:17:00Z","stated":false,"window":null,"seen":"2026-10-01T08:15:00Z","session":null,"assumedZone":false,"notBelieved":false}""")]
    [InlineData("an until at the moment asked is ready", """{"claude-code":{"account-1":{"until":"2026-10-03T10:17:00Z","stated":true,"seen":"2026-10-01T08:15:00Z"}}}""", "2026-10-03T10:17:00Z", "claude-code", "account-1", null)]
    [InlineData("names compare without case, and are said as written", """{"claude-code":{"account-1":{"until":"2026-10-03T10:17:00Z","stated":true,"seen":"2026-10-01T08:15:00Z"}}}""", "2026-10-01T08:15:00Z", "Claude-Code", "ACCOUNT-1", """{"agent":"claude-code","account":"account-1","until":"2026-10-03T10:17:00Z","stated":true,"window":null,"seen":"2026-10-01T08:15:00Z","session":null,"assumedZone":false,"notBelieved":false}""")]
    [InlineData("another account is not this one", """{"claude-code":{"account-1":{"until":"2026-10-03T10:17:00Z","stated":true,"seen":"2026-10-01T08:15:00Z"}}}""", "2026-10-01T08:15:00Z", "claude-code", "account-2", null)]
    [InlineData("the own home is not a named account", """{"claude-code":{"account-1":{"until":"2026-10-03T10:17:00Z","stated":true,"seen":"2026-10-01T08:15:00Z"}}}""", "2026-10-01T08:15:00Z", "claude-code", null, null)]
    [InlineData("a seen that is missing reads as the until", """{"claude-code":{"account-1":{"until":"2026-10-03T10:17:00Z"}}}""", "2026-10-01T08:15:00Z", "claude-code", "account-1", """{"agent":"claude-code","account":"account-1","until":"2026-10-03T10:17:00Z","stated":false,"window":null,"seen":"2026-10-03T10:17:00Z","session":null,"assumedZone":false,"notBelieved":false}""")]
    [InlineData("a flag that is not true is false, and a word that is not text is none", """{"claude-code":{"account-1":{"until":"2026-10-03T10:17:00Z","stated":"yes","window":7,"seen":"2026-10-01T08:15:00Z","session":"","assumedZone":1}}}""", "2026-10-01T08:15:00Z", "claude-code", "account-1", """{"agent":"claude-code","account":"account-1","until":"2026-10-03T10:17:00Z","stated":false,"window":null,"seen":"2026-10-01T08:15:00Z","session":null,"assumedZone":false,"notBelieved":false}""")]
    [InlineData("a moment with an offset or a fraction is read in UTC, to the second", """{"claude-code":{"account-1":{"until":"2026-10-03T16:02:00+05:45","stated":true,"seen":"2026-10-01T08:15:00.5Z"}}}""", "2026-10-01T08:15:00Z", "claude-code", "account-1", """{"agent":"claude-code","account":"account-1","until":"2026-10-03T10:17:00Z","stated":true,"window":null,"seen":"2026-10-01T08:15:00Z","session":null,"assumedZone":false,"notBelieved":false}""")]
    [InlineData("the zone assumed and the date not believed are read where true", """{"claude-code":{"account-1":{"until":"2026-10-03T10:17:00Z","stated":false,"seen":"2026-10-01T08:15:00Z","assumedZone":true,"notBelieved":true}}}""", "2026-10-01T08:15:00Z", "claude-code", "account-1", """{"agent":"claude-code","account":"account-1","until":"2026-10-03T10:17:00Z","stated":false,"window":null,"seen":"2026-10-01T08:15:00Z","session":null,"assumedZone":true,"notBelieved":true}""")]
    public void An_entry_reads_as_the_cli_reads_it(string name, string? file, string now, string agent, string? account, string? entry)
    {
        if (file is not null) File.WriteAllText(File_, file);

        var read = AccountCooling.Of(_home, agent, account, Moment(now));

        if (entry is null) Assert.True(read is null, $"{name}: {(read is null ? "" : Said(read).ToJsonString())}");
        else Assert.True(read is not null && JsonNode.DeepEquals(JsonNode.Parse(entry), Said(read)), $"{name}: {(read is null ? "none" : Said(read).ToJsonString())}");
    }

    // ——— Ending one early (§2.3, §6): that account and no other; a passed or unreadable entry goes with the write; what
    // has no field is kept; nothing is written where nothing was cooling under that name.

    [Theory]
    [InlineData("ending a cooling account takes it and no other", """{"claude-code":{"account-1":{"until":"2026-10-03T10:17:00Z","stated":true,"seen":"2026-10-01T08:15:00Z"},"account-2":{"until":"2026-10-03T10:17:00Z","stated":true,"seen":"2026-10-01T08:15:00Z"}}}""", "2026-10-01T08:15:00Z", "claude-code", "account-1", true, """{"claude-code":{"account-2":{"until":"2026-10-03T10:17:00Z","stated":true,"seen":"2026-10-01T08:15:00Z"}}}""")]
    [InlineData("an account with no entry ends nothing and nothing is written", """{"claude-code":{"account-1":{"until":"2026-10-03T10:17:00Z","stated":true,"seen":"2026-10-01T08:15:00Z"}}}""", "2026-10-01T08:15:00Z", "claude-code", "account-3", false, "unchanged")]
    [InlineData("an agent with no entries ends nothing", """{"claude-code":{"account-1":{"until":"2026-10-03T10:17:00Z","stated":true,"seen":"2026-10-01T08:15:00Z"}}}""", "2026-10-01T08:15:00Z", "codex", null, false, "unchanged")]
    [InlineData("the last account of an agent takes the agent with it", """{"claude-code":{"account-1":{"until":"2026-10-03T10:17:00Z","stated":true,"seen":"2026-10-01T08:15:00Z"}}}""", "2026-10-01T08:15:00Z", "claude-code", "account-1", true, "{}")]
    [InlineData("an entry whose until has passed was not cooling, and goes", """{"claude-code":{"account-1":{"until":"2026-10-01T08:00:00Z","stated":true,"seen":"2026-09-30T08:15:00Z"}}}""", "2026-10-01T08:15:00Z", "claude-code", "account-1", false, "{}")]
    [InlineData("a passed or unreadable entry elsewhere goes with the write", """{"codex":{"":{"until":"2026-10-01T08:00:00Z"},"account-2":{"until":"Oct 3"}},"claude-code":{"account-1":{"until":"2026-10-03T10:17:00Z"},"account-2":{"until":"2026-10-03T10:17:00Z"}}}""", "2026-10-01T08:15:00Z", "claude-code", "account-1", true, """{"claude-code":{"account-2":{"until":"2026-10-03T10:17:00Z"}}}""")]
    [InlineData("what has no field is kept", """{"claude-code":{"account-1":{"until":"2026-10-03T10:17:00Z"},"account-2":{"until":"2026-10-03T10:17:00Z","later":"kept"}}}""", "2026-10-01T08:15:00Z", "claude-code", "account-1", true, """{"claude-code":{"account-2":{"until":"2026-10-03T10:17:00Z","later":"kept"}}}""")]
    [InlineData("names compare without case", """{"claude-code":{"account-1":{"until":"2026-10-03T10:17:00Z"},"account-2":{"until":"2026-10-03T10:17:00Z"}}}""", "2026-10-01T08:15:00Z", "Claude-Code", "ACCOUNT-1", true, """{"claude-code":{"account-2":{"until":"2026-10-03T10:17:00Z"}}}""")]
    [InlineData("the own home is the empty name", """{"claude-code":{"":{"until":"2026-10-03T10:17:00Z"},"account-1":{"until":"2026-10-03T10:17:00Z"}}}""", "2026-10-01T08:15:00Z", "claude-code", null, true, """{"claude-code":{"account-1":{"until":"2026-10-03T10:17:00Z"}}}""")]
    [InlineData("an agent that is not an object is kept as written", """{"codex":[1],"claude-code":{"account-1":{"until":"2026-10-03T10:17:00Z"}}}""", "2026-10-01T08:15:00Z", "claude-code", "account-1", true, """{"codex":[1]}""")]
    [InlineData("a missing file ends nothing and makes none", null, "2026-10-01T08:15:00Z", "claude-code", "account-1", false, "unchanged")]
    [InlineData("a file that does not read ends nothing and is kept", "not json", "2026-10-01T08:15:00Z", "claude-code", "account-1", false, "unchanged")]
    public void An_entry_ends_as_the_cli_ends_it(string name, string? file, string now, string agent, string? account, bool ended, string after)
    {
        if (file is not null) File.WriteAllText(File_, file);

        var said = AccountCooling.End(_home, agent, account, Moment(now));

        Assert.True(ended == said, $"{name}: ended {said}");
        if (after == "unchanged")
        {
            Assert.True(file is null ? !File.Exists(File_) : File.ReadAllText(File_) == file, $"{name}: written");
        }
        else
        {
            var written = File.ReadAllText(File_);
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(after), JsonNode.Parse(written)), $"{name}: {written}");
            Assert.True(written.EndsWith("}\n", StringComparison.Ordinal) && !written.Contains('\r'), $"{name}: LF and a final newline");
        }
    }

    // ——— What a cool-off says (§2.4): a moment in the machine's zone with the zone named, as the terminal says it too.

    [Theory]
    [InlineData("2026-10-03T10:17:00Z", "Asia/Kathmandu", "Oct 3, 16:02 (Asia/Kathmandu)")]
    [InlineData("2026-10-02T18:15:00Z", "Asia/Kathmandu", "Oct 3, 00:00 (Asia/Kathmandu)")]
    [InlineData("2026-01-01T23:30:00Z", "America/New_York", "Jan 1, 18:30 (America/New_York)")]
    [InlineData("2026-09-29T21:52:00Z", "Europe/London", "Sep 29, 22:52 (Europe/London)")]
    public void A_moment_is_said_in_the_machine_s_zone_with_the_zone_named(string moment, string zone, string said)
    {
        Assert.Equal(said, CoolingWords.When(Moment(moment), TimeZoneInfo.FindSystemTimeZoneById(zone)));
    }
}
