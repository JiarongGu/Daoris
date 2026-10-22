using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// How a scope's list is used (TOOL6a; D130 §2, §3.1, §4.6, §14, as §16.6 amends them; D165 point 2, SWITCH1a):
/// <c>rotationUse</c> and <c>workspaceRotationUse</c> in <c>harnesses.json</c>, beside the lists — <i>use accounts</i>
/// (<c>use</c>: <c>goal</c> or <c>order</c>), <i>keep for conversations</i> (<c>keep</c>) and when to switch
/// (<c>switch</c>: <c>smart</c>, <c>curve</c> or <c>off</c>; and <c>curve</c>, the person's own points per window), read on
/// from the old names <c>early</c> and <c>near</c> — and the rule binding a scope's default and kept account to its list.
/// 🔴 Held, row for row, to the table <c>fixtures/rotation-use.json</c> among the driver's tests, which the page's curve rules read
/// too; the CLI's <c>rotation.ts</c>, its twin, left with <c>daoris agent</c>'s TypeScript (PUSH1f2e), and the table moved here.
/// </summary>
/// <remarks>
/// <para>A scope's settings are said in one word each, today's defaults left out, then <c>?name</c> for each value or
/// setting this build does not know (<see cref="Said"/>). A change is JSON, where <c>"keep": null</c> keeps none,
/// <c>"curve": null</c> takes the curve out, a field absent is left as it was, and <c>null</c> clears the scope's
/// settings. Nothing here counts accounts (§2 rule 6).</para>
/// <para>These tables hold the file; how a start uses what it reads is <c>AccountRotationWalkTests</c>' and
/// <c>AccountRotationGoalTests</c>' (TOOL6b), which read <see cref="RotationUse.Early"/> and <see cref="RotationUse.Near"/>
/// until SWITCH1b moves the walk to the curve.</para>
/// </remarks>
public sealed class RotationUseTwinTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-rotation-use-" + Guid.NewGuid().ToString("N")[..8]);

    public RotationUseTwinTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    public static IEnumerable<object?[]> Reads => Table("read");

    public static IEnumerable<object?[]> Writes => Table("write");

    public static IEnumerable<object?[]> Changes => Table("change");

    public static IEnumerable<object?[]> Orders => Table("order");

    public static IEnumerable<object?[]> Problems => Table("problem");

    public static IEnumerable<object?[]> CurveRefusals => Table("refusal");

    private string Wiring => DaorisHome.Entry(_home, HomeEntry.Harnesses);

    private HarnessSettings Read(string file)
    {
        File.WriteAllText(Wiring, file);
        return HarnessSettings.Load(Wiring);
    }

    private static List<string> Names(string list) => [.. JsonNode.Parse(list)!.AsArray().Select(name => name!.GetValue<string>())];

    /// <summary>The sections a row's <c>after</c> names, as written: key for key, in order.</summary>
    private string Written()
    {
        var root = JsonNode.Parse(File.ReadAllText(Wiring))!.AsObject();
        var picked = new JsonObject();
        foreach (var section in new[] { "rotationUse", "workspaceRotationUse" })
        {
            if (root[section] is { } node) picked[section] = node.DeepClone();
        }

        return picked.ToJsonString();
    }

    /// <summary>
    /// A scope's settings in one word each, today's defaults left out, then what this build does not know. The defaults
    /// are spelled here on purpose, not read from <see cref="RotationUse.Default"/>: a test below holds the record to them.
    /// </summary>
    private static string Said(RotationUse use, IReadOnlyList<string> unknown)
    {
        var parts = new[]
        {
            use.Use != "goal" ? use.Use : null,
            use.Keep is { } keep ? $"keep={keep}" : null,
            use.Switch != "smart" ? $"switch={use.Switch}" : null,
            Window("session"),
            Window("weekly"),
            use.Near != 90 ? $"near={use.Near}" : null,
        }.OfType<string>().Concat(unknown.Select(name => $"?{name}")).ToList();
        return parts.Count > 0 ? string.Join(" ", parts) : "none";

        string? Window(string window) => use.Curve[window] is { } points
            ? $"{window}={string.Join(",", points.Select(point => $"{point.Elapsed}:{point.Used}"))}"
            : null;
    }

    /// <summary>A change as the table spells it; <c>null</c> clears.</summary>
    private static UseChange? Change(string? json)
    {
        if (json is null || JsonNode.Parse(json) is not JsonObject node) return null;
        return new UseChange(
            Use: node["use"]?.GetValue<string>(),
            Keep: node["keep"]?.GetValue<string>(),
            NoKeep: node.ContainsKey("keep") && node["keep"] is null,
            Early: node["early"]?.GetValue<bool>(),
            Near: node["near"]?.GetValue<int>(),
            Switch: node["switch"]?.GetValue<string>(),
            Curve: node["curve"] is { } curve ? UseCurve.Read(JsonDocument.Parse(curve.ToJsonString()).RootElement) : null,
            NoCurve: node.ContainsKey("curve") && node["curve"] is null);
    }

    private static string Rung(ChoiceFrom from) => from switch
    {
        ChoiceFrom.Workspace => "workspace",
        ChoiceFrom.Machine => "machine",
        _ => from.ToString(),
    };

    [Fact]
    public void Todays_defaults_live_in_one_place()
    {
        Assert.Equal(new RotationUse("goal", Keep: null, Switch: "smart", Curve: UseCurve.None), RotationUse.Default);
        Assert.Equal((true, 90), (RotationUse.Default.Early, RotationUse.Default.Near));
        Assert.Equal(["goal", "order"], RotationUse.Modes);
        Assert.Equal(["smart", "curve", "off"], RotationUse.Switches);
        Assert.Equal(["session", "weekly"], UseCurve.Windows);
        Assert.Equal(12, UseCurve.MostPoints);
        Assert.Equal((50, 99), (RotationUse.NearLowest, RotationUse.NearHighest));
    }

    // ——— Read and resolved (§2 rule 1, §14, §16.6; D165 §6): the scope a start reads, its list, where it begins, and its
    // settings, the old names read on.

    [Theory]
    [MemberData(nameof(Reads))]
    public void A_scope_is_read_as_the_cli_reads_it(string why, string file, string agent, string? workspace, string from, string? list, string? begins, string use)
    {
        var scope = OlderScope.Of(Read(file), agent, workspace);

        var wanted = list is null ? [] : Names(list);
        Assert.True(from == Rung(scope.From), $"{why}: {Rung(scope.From)}");
        Assert.True(wanted.SequenceEqual(scope.List), $"{why}: [{string.Join(", ", scope.List)}]");
        Assert.True(begins == scope.Begins, $"{why}: begins {scope.Begins}");
        Assert.True(use == Said(scope.Use, scope.Unknown), $"{why}: {Said(scope.Use, scope.Unknown)}");
    }

    // ——— Written (D165 §6): the old names written as `switch` and `curve`, never again as themselves; a curve in one order.

    [Theory]
    [MemberData(nameof(Writes))]
    public void Settings_are_written_as_the_cli_writes_them(string why, string before, string after)
    {
        Read(before).Save(Wiring);

        var written = Written();
        Assert.True(JsonNode.Parse(after)!.ToJsonString() == written, $"{why}: {written}");
    }

    // ——— Edited (§2, §16.6; D165 §6): a change merged into the scope's settings, each choice written as made, so a later
    // default never overturns one; what this build does not know kept as written; null clears the scope's settings, and a
    // scope with none is dropped. The old names, which the doors still take, are mapped onto the switch and the curve.

    [Theory]
    [MemberData(nameof(Changes))]
    public void Settings_are_set_and_cleared_as_the_cli_writes_them(string why, string before, string agent, string? change, string? workspace, string after)
    {
        Read(before).WithUse(agent, Change(change), workspace).Save(Wiring);

        var written = Written();
        Assert.True(JsonNode.Parse(after)!.ToJsonString() == written, $"{why}: {written}");
    }

    // ——— A list's settings come with it (§2 rule 1): a list cleared takes its scope's settings; a list replaced keeps them.

    [Theory]
    [MemberData(nameof(Orders))]
    public void A_lists_settings_go_with_it_as_the_cli_writes_them(string why, string before, string agent, string? order, string? workspace, string after)
    {
        Read(before).WithRotation(agent, order is null ? null : Names(order), workspace).Save(Wiring);

        var written = Written();
        Assert.True(JsonNode.Parse(after)!.ToJsonString() == written, $"{why}: {written}");
    }

    // ——— The rule binding a scope (§3.1, §4.6): its default and its kept account are of its list, and driven work keeps an
    // account. The first problem is the one said; no list is refused for how many it names.

    [Theory]
    [MemberData(nameof(Problems))]
    public void A_scope_is_refused_as_the_cli_refuses_it(string why, string? scopeDefault, string list, string? keep, string? refused)
    {
        var problem = ScopeProblem.Of(scopeDefault, Names(list), keep);

        var said = problem is null ? null : $"{problem.Kind.ToString().ToLowerInvariant()} {problem.Account}";
        Assert.True(refused == said, $"{why}: {said}");
    }

    // ——— A curve's rules (D165 point 2, the switch design §2): one to twelve whole-number points per window, elapsed from 0
    // to 100 and rising, used from 0 to 100 and never falling; the first problem said, which window, which point, which rule.

    [Theory]
    [MemberData(nameof(CurveRefusals))]
    public void A_curve_is_refused_as_the_cli_refuses_it_in_the_same_sentence(string why, string curve, string? refused)
    {
        using var document = JsonDocument.Parse(curve);

        var said = UseCurve.Problem(document.RootElement);
        Assert.True(refused == said, $"{why}: {said}");
        Assert.True((said is null) == (UseCurve.Read(document.RootElement) is not null), $"{why}: a curve reads exactly where none is refused");
    }

    /// <summary>
    /// SWITCH1d: a curve's problem names its rule and the window, point and numbers it weighs, so the modules refuse it in a
    /// code the page words in the reader's language; its sentence is the twins'.
    /// </summary>
    [Theory]
    [InlineData("""{"weekly":[[0,30],[50,70],[100,60]]}""", "falling weekly 3 60 70 ")]
    [InlineData("""{"weekly":[[50,30],[40,70]]}""", "rising weekly 2 40 50 ")]
    [InlineData("""{"session":[[0,101]]}""", "used session 1 101  ")]
    [InlineData("""{"weekly":[]}""", "count weekly    0")]
    [InlineData("""{"monthly":[[0,50]]}""", "window monthly    ")]
    [InlineData("""[[0,30]]""", "shape     ")]
    public void A_curve_s_problem_names_its_rule_and_what_it_weighs(string curve, string named)
    {
        using var document = JsonDocument.Parse(curve);

        var problem = UseCurve.ProblemOf(document.RootElement)!;

        Assert.Equal(named, $"{problem.Kind} {problem.Window} {problem.Point} {problem.Value} {problem.Before} {problem.Count}");
        Assert.Equal(UseCurve.Problem(document.RootElement), problem.Sentence);
    }

    // ——— What the twin table does not hold: a kept account read only of its list, the save's refusal, and the settings as
    // the old names spelled them, which the modules' tests still build.

    [Fact]
    public void A_kept_account_outside_its_list_is_kept_as_written_and_read_as_none()
    {
        var settings = new HarnessSettings().WithRotation("claude-code", ["account-1", "account-2"])
            .WithUse("claude-code", new UseChange(Keep: "account-9"));

        Assert.Null(settings.Set("claude-code").Use.Keep);
        Assert.Equal(["account-1", "account-2"], settings.Set("claude-code").List);
        Assert.Equal("account-9", settings.Uses["claude-code"]["keep"]?.GetString());
    }

    [Fact]
    public void An_edit_over_wiring_that_could_not_be_read_is_refused_and_keeps_the_settings()
    {
        File.WriteAllText(Wiring, "{ not json");

        Assert.Throws<DriverException>(() => HarnessSettings.Load(Wiring).WithUse("claude-code", new UseChange(Use: "order")).Save(Wiring));
        Assert.Equal("{ not json", File.ReadAllText(Wiring));
    }

    [Fact]
    public void Settings_spelled_by_the_old_names_are_the_switch_and_the_curve_they_read_as()
    {
        Assert.Equal(new RotationUse("order", "account-2", "off", UseCurve.Flat("session", 85)), new RotationUse("order", "account-2", false, 85));
        Assert.Equal(new RotationUse("goal", null, "curve", UseCurve.Flat("session", 85)), new RotationUse("goal", null, true, 85));
        Assert.Equal(RotationUse.Default, new RotationUse("goal", null, true, 90));
        Assert.Equal((false, 90), (new RotationUse("goal", null, false, 85).Early, new RotationUse("goal", null, false, 85).Near));
        Assert.Equal((true, 85), (new RotationUse("goal", null, true, 85).Early, new RotationUse("goal", null, true, 85).Near));
    }

    /// <summary>One kind of the shared table's rows: a text cell as itself, null as null, and any other JSON as its text.</summary>
    private static IEnumerable<object?[]> Table(string kind)
    {
        using var table = JsonDocument.Parse(File.ReadAllText(Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "rotation-use.json")));
        return [.. table.RootElement.GetProperty(kind).EnumerateArray()
            .Select(row => row.EnumerateArray().Select(cell => cell.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.String => (object?)cell.GetString(),
                _ => cell.GetRawText(),
            }).ToArray())];
    }
}
