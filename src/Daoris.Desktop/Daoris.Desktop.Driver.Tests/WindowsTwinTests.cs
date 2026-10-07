using System.Globalization;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>windows.json</c> read (TOOL6c, D130 §5.2): the driver writes what each account's agent said about its windows, and
/// both twins read it — the walk here, and <c>daoris agent list</c> and <c>profile use</c> in the CLI's <c>windows.ts</c>.
/// 🔴 The CLI's <c>windows.test.ts</c> holds the same table, row for row and in the same order, and parses this one to hold
/// it to its own, cell for cell.
/// </summary>
/// <remarks>
/// What an account said is written as JSON: each window still said, in the file's order, with its use, reset, standing,
/// credits, when it was seen and on which session; <c>null</c> is nothing said.
/// </remarks>
public sealed class WindowsTwinTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-windows-twin-" + Guid.NewGuid().ToString("N")[..8]);

    public WindowsTwinTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static DateTimeOffset Moment(string text) =>
        DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

    private static JsonArray Said(AccountSaid said) => new([.. said.Windows.Select(window => (JsonNode)new JsonObject
    {
        ["window"] = window.Window,
        ["used"] = window.Used,
        ["reset"] = AccountCooling.Stamp(window.Reset),
        ["standing"] = window.Standing,
        ["credits"] = window.Credits,
        ["seen"] = AccountCooling.Stamp(window.Seen),
        ["session"] = window.Session,
    })]);

    // ——— Reading (§5.2): missing or unreadable is nothing said; a reading is gone at its reset; a week a limit told says no
    // use; names compare without case; the tool's own sign-in is its own key, "" (CODEXUSE3).

    [Theory]
    [InlineData("missing is nothing said", null, "2026-10-02T12:00:00Z", "claude-code", "account-1", null)]
    [InlineData("not JSON is nothing said", "not json", "2026-10-02T12:00:00Z", "claude-code", "account-1", null)]
    [InlineData("JSON that is not an object is nothing said", "[1, 2]", "2026-10-02T12:00:00Z", "claude-code", "account-1", null)]
    [InlineData("a reading read whole, in the file's order", """{"claude-code":{"account-1":{"session":{"reset":"2026-10-02T14:00:00Z","used":0.88,"standing":"clear","seen":"2026-10-02T11:40:00Z","session":"s1"},"weekly":{"reset":"2026-10-06T21:18:00Z","used":0.14,"seen":"2026-10-02T11:40:00Z","session":"s1"}}}}""", "2026-10-02T12:00:00Z", "claude-code", "account-1", """[{"window":"session","used":0.88,"reset":"2026-10-02T14:00:00Z","standing":"clear","credits":false,"seen":"2026-10-02T11:40:00Z","session":"s1"},{"window":"weekly","used":0.14,"reset":"2026-10-06T21:18:00Z","standing":null,"credits":false,"seen":"2026-10-02T11:40:00Z","session":"s1"}]""")]
    [InlineData("a window whose reset has passed is gone", """{"claude-code":{"account-1":{"session":{"reset":"2026-10-02T12:00:00Z","used":0.88,"seen":"2026-10-02T09:00:00Z"},"weekly":{"reset":"2026-10-06T21:18:00Z","used":0.14,"seen":"2026-10-02T09:00:00Z"}}}}""", "2026-10-02T12:00:00Z", "claude-code", "account-1", """[{"window":"weekly","used":0.14,"reset":"2026-10-06T21:18:00Z","standing":null,"credits":false,"seen":"2026-10-02T09:00:00Z","session":null}]""")]
    [InlineData("every window passed is nothing said", """{"claude-code":{"account-1":{"session":{"reset":"2026-10-02T11:00:00Z","used":0.88,"seen":"2026-10-02T09:00:00Z"}}}}""", "2026-10-02T12:00:00Z", "claude-code", "account-1", null)]
    [InlineData("a week a limit told says no use", """{"claude-code":{"account-1":{"weekly":{"reset":"2026-10-03T10:17:00Z","seen":"2026-10-01T08:15:00Z","session":"s7"}}}}""", "2026-10-02T12:00:00Z", "claude-code", "account-1", null)]
    [InlineData("names compare without case, and are said as written", """{"claude-code":{"account-1":{"Session":{"reset":"2026-10-02T14:00:00Z","used":0.5,"seen":"2026-10-02T11:00:00Z"}}}}""", "2026-10-02T12:00:00Z", "CLAUDE-CODE", "Account-1", """[{"window":"Session","used":0.5,"reset":"2026-10-02T14:00:00Z","standing":null,"credits":false,"seen":"2026-10-02T11:00:00Z","session":null}]""")]
    [InlineData("another account is not this one", """{"claude-code":{"account-1":{"session":{"reset":"2026-10-02T14:00:00Z","used":0.5,"seen":"2026-10-02T11:00:00Z"}}}}""", "2026-10-02T12:00:00Z", "claude-code", "account-2", null)]
    [InlineData("a standing alone is a reading; credits are only JSON true", """{"claude-code":{"account-1":{"weekly":{"reset":"2026-10-06T21:18:00Z","standing":"near","credits":"yes","seen":"2026-10-02T11:00:00Z"},"session":{"reset":"2026-10-02T14:00:00Z","used":0.2,"credits":true,"seen":"2026-10-02T11:00:00Z"}}}}""", "2026-10-02T12:00:00Z", "claude-code", "account-1", """[{"window":"weekly","used":null,"reset":"2026-10-06T21:18:00Z","standing":"near","credits":false,"seen":"2026-10-02T11:00:00Z","session":null},{"window":"session","used":0.2,"reset":"2026-10-02T14:00:00Z","standing":null,"credits":true,"seen":"2026-10-02T11:00:00Z","session":null}]""")]
    [InlineData("a use that is not a number, a standing that is not text and a session that is empty say nothing", """{"claude-code":{"account-1":{"session":{"reset":"2026-10-02T14:00:00Z","used":"lots","standing":7,"seen":"2026-10-02T11:00:00Z","session":""},"weekly":{"reset":"2026-10-06T21:18:00Z","used":-0.2,"seen":"2026-10-02T11:00:00Z"}}}}""", "2026-10-02T12:00:00Z", "claude-code", "account-1", null)]
    [InlineData("a reset or a seen that is not ISO 8601 is nothing said", """{"claude-code":{"account-1":{"session":{"reset":"Oct 3","used":0.5,"seen":"2026-10-02T11:00:00Z"},"weekly":{"reset":"2026-10-06T21:18:00Z","used":0.5,"seen":"today"}}}}""", "2026-10-02T12:00:00Z", "claude-code", "account-1", null)]
    [InlineData("a window that is not an object, and an account that is not one, say nothing", """{"claude-code":{"account-1":{"session":"0.5"},"account-2":[1]}}""", "2026-10-02T12:00:00Z", "claude-code", "account-1", null)]
    [InlineData("a moment with an offset or a fraction is read in UTC, to the second", """{"claude-code":{"account-1":{"weekly":{"reset":"2026-10-07T03:03:00+05:45","used":0.25,"seen":"2026-10-02T11:00:00.5Z","session":"s2"}}}}""", "2026-10-02T12:00:00Z", "claude-code", "account-1", """[{"window":"weekly","used":0.25,"reset":"2026-10-06T21:18:00Z","standing":null,"credits":false,"seen":"2026-10-02T11:00:00Z","session":"s2"}]""")]
    [InlineData("a letter whose capital is two letters is not those two: straße is not STRASSE", """{"claude-code":{"straße":{"session":{"reset":"2026-10-02T14:00:00Z","used":0.5,"seen":"2026-10-02T11:00:00Z"}}}}""", "2026-10-02T12:00:00Z", "claude-code", "STRASSE", null)]
    [InlineData("a dotless i is not an I", """{"claude-code":{"ışık":{"session":{"reset":"2026-10-02T14:00:00Z","used":0.5,"seen":"2026-10-02T11:00:00Z"}}}}""", "2026-10-02T12:00:00Z", "claude-code", "IŞIK", null)]
    [InlineData("the tool's own sign-in reads under its own key, beside the accounts and none of them", """{"codex":{"account-1":{"session":{"reset":"2026-10-02T14:00:00Z","used":0.5,"seen":"2026-10-02T11:00:00Z"}},"":{"weekly":{"reset":"2026-10-06T21:18:00Z","used":0.4,"seen":"2026-10-02T11:30:00Z"}}}}""", "2026-10-02T12:00:00Z", "codex", "", """[{"window":"weekly","used":0.4,"reset":"2026-10-06T21:18:00Z","standing":null,"credits":false,"seen":"2026-10-02T11:30:00Z","session":null}]""")]
    [InlineData("an account is not the tool's own sign-in", """{"codex":{"":{"weekly":{"reset":"2026-10-06T21:18:00Z","used":0.4,"seen":"2026-10-02T11:30:00Z"}}}}""", "2026-10-02T12:00:00Z", "codex", "account-1", null)]
    public void An_account_s_windows_read_as_the_cli_reads_them(string name, string? file, string now, string agent, string account, string? said)
    {
        if (file is not null) File.WriteAllText(AccountWindows.PathOf(_home), file);

        var read = AccountWindows.SaidOf(_home, agent, account, Moment(now));

        Assert.True(
            JsonNode.DeepEquals(read is null ? null : Said(read), said is null ? null : JsonNode.Parse(said)),
            $"{name}: {(read is null ? "null" : Said(read).ToJsonString())}");
    }
}
