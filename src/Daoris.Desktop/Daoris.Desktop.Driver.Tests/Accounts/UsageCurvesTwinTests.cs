using System.Globalization;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The usage curve (D165 point 2; the switch design §2, §3 and §5; SWITCH1b-iii): how much of each window an account may have
/// used by now, and whether what its agent said is over that, which the start's line says as <c>opening.said-over</c>. The
/// driver reads the table in the CLI's <c>test/fixtures/usage-curves.json</c>, row for row, as the page's
/// <c>usageCurves.test.ts</c> does; the CLI's <c>usagecurves.ts</c>, its twin until then, left with <c>daoris agent list</c>
/// (PUSH1f2e4). <see cref="UsageCurvesTests"/> holds the curve's own cases.
/// </summary>
public sealed class UsageCurvesTwinTests
{
    public static IEnumerable<object?[]> Values => Table("value");

    public static IEnumerable<object?[]> Tightests => Table("tightest").Select(row => row[..5]);

    /// <summary>The <c>tightest</c> rows with their clause in place of the window found: what the said line adds.</summary>
    public static IEnumerable<object?[]> Clauses => Table("tightest").Select(row => (object?[])[.. row[..4], row[5]]);

    [Theory]
    [MemberData(nameof(Values))]
    public void The_curve_now_on_a_window_reads_as_the_cli_reads_it(string name, string switched, string? curve, string window, string reset, string? expected)
    {
        var value = UsageCurves.Value(Use(switched, curve), window, Moment(reset), Now);

        Assert.True(Rounded(value) == (expected is null ? null : double.Parse(expected, CultureInfo.InvariantCulture)), $"{name}: {value}");
    }

    [Theory]
    [MemberData(nameof(Tightests))]
    public void The_tightest_window_an_account_said_reads_as_the_cli_reads_it(string name, string switched, string? curve, string said, string? expected)
    {
        var found = UsageCurves.TightestSaid(Use(switched, curve), Said(said), Now);

        var read = found is { } tightest
            ? JsonSerializer.Serialize(new { window = tightest.Window.Window, curve = Rounded(tightest.Curve), room = Rounded(tightest.Room) })
            : null;
        Assert.True(Same(read, expected), $"{name}: {read}");
    }

    [Theory]
    [MemberData(nameof(Clauses))]
    public void The_start_s_line_marks_a_window_over_its_curve_as_the_cli_s_said_line_does(string name, string switched, string? curve, string said, string? clause)
    {
        var facts = new Dictionary<string, AccountFacts> { ["account-1"] = new(Said: Said(said)) };

        var line = RotationWords.AccountsSaid(["account-1"], facts, Use(switched, curve), Now)!.Note;

        Assert.True(clause is null ? !line.Contains("over its", StringComparison.Ordinal) : line.Contains(clause, StringComparison.Ordinal), $"{name}: {line}");
    }

    private static DateTimeOffset Now => Moment(Root.GetProperty("now").GetString()!);

    private static JsonElement Root
    {
        get
        {
            using var table = JsonDocument.Parse(File.ReadAllText(Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Cli", "test", "fixtures", "usage-curves.json")));
            return table.RootElement.Clone();
        }
    }

    private static DateTimeOffset Moment(string text) => DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

    /// <summary>A scope as the table writes it: its switch, and its curve's JSON as <c>harnesses.json</c> holds it, null for none.</summary>
    private static RotationUse Use(string switched, string? curve) =>
        RotationUse.Default with
        {
            Switch = switched,
            Curve = curve is null ? UseCurve.None : UseCurve.Read(JsonDocument.Parse(curve).RootElement) ?? throw new InvalidOperationException($"the table's curve does not read: {curve}"),
        };

    /// <summary>What an account's agent said, each window as <c>[name, used, reset]</c>, seen twenty minutes before the table's now.</summary>
    private static AccountSaid Said(string windows)
    {
        using var said = JsonDocument.Parse(windows);
        return new AccountSaid([.. said.RootElement.EnumerateArray().Select(window => new WindowSaid(
            window[0].GetString()!,
            window[1].ValueKind == JsonValueKind.Null ? null : window[1].GetDouble(),
            Moment(window[2].GetString()!),
            Standing: null, Credits: false, Seen: Now.AddMinutes(-20), Session: "s1"))]);
    }

    private static double? Rounded(double? value) => value is { } number ? Math.Round(number, 6) : null;

    /// <summary>Two cells' JSON, compared as values: the table's spacing and number spelling are not the answer.</summary>
    private static bool Same(string? read, string? expected)
    {
        if (read is null || expected is null) return read is null && expected is null;
        using var a = JsonDocument.Parse(read);
        using var b = JsonDocument.Parse(expected);
        return a.RootElement.GetProperty("window").GetString() == b.RootElement.GetProperty("window").GetString()
            && a.RootElement.GetProperty("curve").GetDouble() == b.RootElement.GetProperty("curve").GetDouble()
            && a.RootElement.GetProperty("room").GetDouble() == b.RootElement.GetProperty("room").GetDouble();
    }

    /// <summary>One kind of the shared table's rows: a text cell as itself, null as null, and any other JSON as its text.</summary>
    private static IEnumerable<object?[]> Table(string kind) =>
        [.. Root.GetProperty(kind).EnumerateArray()
            .Select(row => row.EnumerateArray().Select(cell => cell.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.String => (object?)cell.GetString(),
                _ => cell.GetRawText(),
            }).ToArray())];
}
