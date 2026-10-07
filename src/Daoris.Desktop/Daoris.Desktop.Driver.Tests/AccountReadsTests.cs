using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// ROSTER1 (D150 §5.3): <c>reads.json</c> under the home keeps what was last read of each account's sign-in, and of the tool's
/// own, with when, so a restart starts from what was last read rather than from a probe. Nothing here starts a process.
/// </summary>
/// <remarks>
/// The driver's half of a TWIN with the CLI's <c>accountreads.ts</c> (AGENTREAD1, AGENTREAD1b): both are held, row for row, to
/// one table, the CLI's <c>test/fixtures/account-reads.json</c> (reading, keeping, forgetting), and the CLI's suite holds this
/// class's <see cref="What_does_not_read_is_nothing_known"/> rows to it, cell for cell.
/// </remarks>
public sealed class AccountReadsTests : IDisposable
{
    private static readonly DateTimeOffset Ten = new(2026, 10, 4, 10, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-account-reads-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public void Nothing_read_is_nothing_known()
    {
        var reads = AccountReads.Of(_home, "claude-code");

        Assert.Null(reads.Own);
        Assert.Empty(reads.Accounts);
        Assert.False(File.Exists(AccountReads.PathOf(_home)));
    }

    [Fact]
    public void Each_account_and_the_tools_own_sign_in_keep_their_word_and_when()
    {
        AccountReads.Keep(_home, "claude-code", "account-1", LoginState.Out, Ten);
        AccountReads.Keep(_home, "claude-code", null, LoginState.In, Ten.AddMinutes(1));
        AccountReads.Keep(_home, "codex", "work", LoginState.Unknown, Ten.AddMinutes(2));

        var claude = AccountReads.Of(_home, "claude-code");
        Assert.Equal(new AccountRead(LoginState.Out, Ten), claude.Accounts["account-1"]);
        Assert.Equal(new AccountRead(LoginState.In, Ten.AddMinutes(1)), claude.Own);
        Assert.Equal(new AccountRead(LoginState.Unknown, Ten.AddMinutes(2)), AccountReads.Of(_home, "codex").Accounts["work"]);
    }

    /// <summary>Names compare without case, as the wiring compares them.</summary>
    [Fact]
    public void An_account_is_found_by_its_name_whatever_its_case()
    {
        AccountReads.Keep(_home, "Claude-Code", "Account-1", LoginState.In, Ten);

        Assert.Equal(LoginState.In, AccountReads.Of(_home, "claude-code").Accounts["account-1"].Login);
    }

    /// <summary>Two reads finishing out of order: the later reading stands, never an older one written after it.</summary>
    [Fact]
    public void An_older_reading_never_replaces_a_newer_one()
    {
        AccountReads.Keep(_home, "claude-code", "account-1", LoginState.In, Ten.AddMinutes(5));
        AccountReads.Keep(_home, "claude-code", "account-1", LoginState.Out, Ten);

        Assert.Equal(new AccountRead(LoginState.In, Ten.AddMinutes(5)), AccountReads.Of(_home, "claude-code").Accounts["account-1"]);
    }

    /// <summary>A removed account is forgotten, so one made later under its name starts never read.</summary>
    [Fact]
    public void A_removed_account_is_forgotten_and_the_rest_stand()
    {
        AccountReads.Keep(_home, "claude-code", "account-1", LoginState.In, Ten);
        AccountReads.Keep(_home, "claude-code", "account-2", LoginState.Out, Ten);

        AccountReads.Forget(_home, "claude-code", "account-1");

        var reads = AccountReads.Of(_home, "claude-code");
        Assert.False(reads.Accounts.ContainsKey("account-1"));
        Assert.Equal(LoginState.Out, reads.Accounts["account-2"].Login);
    }

    /// <summary>
    /// The file holds a word and a time per account, never who signed in (D66 §3: read fresh, written nowhere), and keeps what
    /// it has no field for, as every file the home holds does.
    /// </summary>
    [Fact]
    public void The_file_holds_a_word_and_a_time_never_who_and_keeps_what_it_does_not_know()
    {
        Directory.CreateDirectory(_home);
        File.WriteAllText(AccountReads.PathOf(_home), """{ "claude-code": { "later": 1 } }""");

        AccountReads.Keep(_home, "claude-code", "account-1", LoginState.In, Ten);

        var text = File.ReadAllText(AccountReads.PathOf(_home));
        var root = JsonNode.Parse(text)!.AsObject();
        Assert.Equal(1, (int)root["claude-code"]!["later"]!);
        Assert.Equal("in", (string?)root["claude-code"]!["accounts"]!["account-1"]!["login"]);
        Assert.Equal("2026-10-04T10:00:00Z", (string?)root["claude-code"]!["accounts"]!["account-1"]!["read"]);
        Assert.DoesNotContain("@", text);
        Assert.DoesNotContain("\r", text);
    }

    /// <summary>An unreadable file, or an entry this build cannot read, is nothing known: never a guess of signed in or out.</summary>
    [Theory]
    [InlineData("not json")]
    [InlineData("""{ "claude-code": { "accounts": { "account-1": { "login": "maybe", "read": "2026-10-04T10:00:00Z" } } } }""")]
    [InlineData("""{ "claude-code": { "accounts": { "account-1": { "login": "in", "read": "yesterday" } } } }""")]
    [InlineData("""{ "claude-code": { "accounts": { "account-1": "in" } } }""")]
    public void What_does_not_read_is_nothing_known(string file)
    {
        Directory.CreateDirectory(_home);
        File.WriteAllText(AccountReads.PathOf(_home), file);

        Assert.Empty(AccountReads.Of(_home, "claude-code").Accounts);
    }

    // ——— The shared table (AGENTREAD1b): the CLI's twin is held to the same rows, so a rule changed on one side fails both.

    public static IEnumerable<object?[]> Reads => Table("read");

    public static IEnumerable<object?[]> Keeps => Table("keep");

    public static IEnumerable<object?[]> Forgets => Table("forget");

    [Fact]
    public void The_shared_table_holds_rows_of_each_kind()
    {
        Assert.NotEmpty(Reads);
        Assert.NotEmpty(Keeps);
        Assert.NotEmpty(Forgets);
    }

    /// <param name="said">The reading as <c>&lt;login&gt; &lt;ISO 8601 in UTC, to the millisecond&gt;</c>; null for nothing known.</param>
    [Theory]
    [MemberData(nameof(Reads))]
    public void A_reading_reads_as_the_shared_table_says(string why, string? file, string agent, string? account, string? said)
    {
        Holding(file);

        var reads = AccountReads.Of(_home, agent);
        var read = account is null ? reads.Own : reads.Accounts.GetValueOrDefault(account);

        Same(why, said, read is null ? null : $"{Word(read.Login)} {read.At.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture)}");
    }

    /// <param name="after">The file's JSON once written, or <c>unchanged</c> where nothing is written.</param>
    [Theory]
    [MemberData(nameof(Keeps))]
    public void A_reading_is_kept_as_the_shared_table_says_byte_for_byte(
        string why, string? file, string agent, string? account, string login, string at, string after)
    {
        Holding(file);

        AccountReads.Keep(_home, agent, account, Login(login), DateTimeOffset.Parse(at, CultureInfo.InvariantCulture));

        Same(why, after == "unchanged" ? file : Written(after), OnDisk());
    }

    /// <param name="after">The file's JSON once written, or <c>unchanged</c> where nothing is written.</param>
    [Theory]
    [MemberData(nameof(Forgets))]
    public void A_removed_account_is_forgotten_as_the_shared_table_says_byte_for_byte(
        string why, string? file, string agent, string account, string after)
    {
        Holding(file);

        AccountReads.Forget(_home, agent, account);

        Same(why, after == "unchanged" ? file : Written(after), OnDisk());
    }

    // ——— The driver's own rows, in the table's shape (AGENTREAD1b): a name or a word beyond plain ASCII is written as the CLI
    // writes it, as it is, where System.Text.Json's default encoder would escape it. Proposed for the shared table.

    [Theory]
    [InlineData("a name in Chinese reads", """{"claude-code":{"accounts":{"工作":{"login":"out","read":"2026-10-04T10:00:00Z"}}}}""", "claude-code", "工作", "out 2026-10-04T10:00:00.000Z")]
    [InlineData("an accented name compares without case", """{"claude-code":{"accounts":{"Café":{"login":"in","read":"2026-10-04T10:00:00Z"}}}}""", "claude-code", "CAFÉ", "in 2026-10-04T10:00:00.000Z")]
    public void A_name_beyond_plain_ascii_reads(string why, string? file, string agent, string? account, string? said) =>
        A_reading_reads_as_the_shared_table_says(why, file, agent, account, said);

    [Theory]
    [InlineData("a name in Chinese is written as it is", null, "claude-code", "工作", "in", "2026-10-04T10:00:00Z", """{"claude-code":{"accounts":{"工作":{"login":"in","read":"2026-10-04T10:00:00Z"}}}}""")]
    [InlineData("HTML's marks and a plus in a name are written as they are", null, "claude-code", "R&D+<team>'s", "out", "2026-10-04T10:00:00Z", """{"claude-code":{"accounts":{"R&D+<team>'s":{"login":"out","read":"2026-10-04T10:00:00Z"}}}}""")]
    [InlineData("an accented name found in another case is written under the name given", """{"claude-code":{"accounts":{"Café":{"login":"in","read":"2026-10-04T09:00:00Z"}}}}""", "claude-code", "CAFÉ", "out", "2026-10-04T10:00:00Z", """{"claude-code":{"accounts":{"CAFÉ":{"login":"out","read":"2026-10-04T10:00:00Z"}}}}""")]
    [InlineData("words it has no field for are written as they are, escaped on disk or not", "{\"later\":\"\\u00e9 中文 \\u0026\"}", "codex", "work", "in", "2026-10-04T10:00:00Z", """{"later":"é 中文 &","codex":{"accounts":{"work":{"login":"in","read":"2026-10-04T10:00:00Z"}}}}""")]
    public void A_name_beyond_plain_ascii_is_kept_as_the_cli_writes_it(
        string why, string? file, string agent, string? account, string login, string at, string after)
    {
        A_reading_is_kept_as_the_shared_table_says_byte_for_byte(why, file, agent, account, login, at, after);

        // Held apart from the expected bytes' own encoder: JSON.stringify escapes none of these.
        Assert.DoesNotContain("\\u", OnDisk());
    }

    [Theory]
    [InlineData("a name in Chinese is forgotten, and the rest written as they are", """{"claude-code":{"accounts":{"工作":{"login":"in","read":"2026-10-04T09:00:00Z"},"Café":{"login":"out","read":"2026-10-04T09:00:00Z"}}}}""", "claude-code", "工作", """{"claude-code":{"accounts":{"Café":{"login":"out","read":"2026-10-04T09:00:00Z"}}}}""")]
    public void A_name_beyond_plain_ascii_is_forgotten_as_the_cli_writes_it(string why, string? file, string agent, string account, string after)
    {
        A_removed_account_is_forgotten_as_the_shared_table_says_byte_for_byte(why, file, agent, account, after);

        Assert.DoesNotContain("\\u", OnDisk());
    }

    /// <summary>
    /// The file as the CLI writes it, <c>JSON.stringify(…, null, 2)</c> and a final newline: two spaces, LF, every letter of the
    /// basic plane and HTML's marks as they are. The relaxed encoder writes them so (<see cref="AccountNames"/> writes with it too).
    /// </summary>
    private static readonly JsonSerializerOptions AsTheCliWrites = new()
    {
        WriteIndented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static string Written(string after) => JsonNode.Parse(after)!.ToJsonString(AsTheCliWrites) + "\n";

    private void Holding(string? file)
    {
        if (file is null) return;
        Directory.CreateDirectory(_home);
        File.WriteAllText(AccountReads.PathOf(_home), file);
    }

    /// <summary>The file's bytes as text, a byte-order mark included; null where there is no file.</summary>
    private string? OnDisk() =>
        File.Exists(AccountReads.PathOf(_home)) ? Encoding.UTF8.GetString(File.ReadAllBytes(AccountReads.PathOf(_home))) : null;

    private static void Same(string why, string? expected, string? actual) =>
        Assert.True(expected == actual, $"{why}\nexpected: {expected ?? "(none)"}\nactual:   {actual ?? "(none)"}");

    private static LoginState Login(string word) => word switch
    {
        "in" => LoginState.In,
        "out" => LoginState.Out,
        "unknown" => LoginState.Unknown,
        _ => throw new ArgumentException($"no login word `{word}`", nameof(word)),
    };

    private static string Word(LoginState login) => login switch
    {
        LoginState.In => "in",
        LoginState.Out => "out",
        _ => "unknown",
    };

    /// <summary>One kind of the shared table's rows, each its cells as the file spells them, a JSON null as null.</summary>
    private static IEnumerable<object?[]> Table(string kind)
    {
        using var table = JsonDocument.Parse(File.ReadAllText(Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Cli", "test", "fixtures", "account-reads.json")));
        return [.. table.RootElement.GetProperty(kind).EnumerateArray()
            .Select(row => row.EnumerateArray().Select(cell => cell.ValueKind == JsonValueKind.Null ? null : (object?)cell.GetString()).ToArray())];
    }
}
