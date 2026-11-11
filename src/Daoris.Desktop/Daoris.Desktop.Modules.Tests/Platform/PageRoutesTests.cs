using Daoris.ContractsGen;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The page's window routes as the generator writes them from the marks: every route the driver answers, each press's declared
/// change by module and route with its session or subject field, and the catalogue key of each person's press. The page's
/// publishing seam and the names check read this file, so what they hold is what the marks say.
/// </summary>
public sealed class PageRoutesTests
{
    private static readonly string Written = PageRoutes.Write();

    [Fact]
    public void Every_route_the_driver_answers_is_one_line_of_its_union()
    {
        var union = Section("export type DriverRoute =", ";\n");

        Assert.Equal(DriverModule.Routes.Order(StringComparer.Ordinal).Select(route => $"  | '{route}'"), union.Split('\n'));
    }

    [Fact]
    public void A_handler_that_takes_its_payload_record_is_typed_by_it_field_by_field_as_the_record_is_read()
    {
        var payloads = Section("export type DriverPayloads = {", "\n};\n");

        Assert.Contains("  'SET_DRIVABLE': { payload: SetDrivable; answer: unknown };", payloads);
        Assert.Contains("export type SetDrivable = {\n  repository: string;\n  drivable: boolean;\n};", Written);
        Assert.Contains(
            "export type SetKeep = {\n  kind: string;\n  days?: number | null;\n  never?: boolean;\n  whole?: boolean | null;\n  default?: boolean;\n"
            + "  gb?: number | null;\n  percent?: number | null;\n  off?: boolean;\n};",
            Written);
        Assert.Contains("export type SetStanding = {\n  repository: string;\n  says?: string | null;\n};", Written);
        Assert.Equal(
            DriverModule.Shapes.Where(shape => shape.Payload is not null).Select(shape => shape.Route).Order(StringComparer.Ordinal),
            payloads.Split('\n').Select(line => line.Split('\'')[1]));
        Assert.DoesNotContain("'STATE':", payloads);
    }

    [Fact]
    public void Each_press_declares_its_scopes_in_the_wire_s_order_with_its_session_or_subject_field_and_a_read_declares_nothing()
    {
        var published = Section("export const PUBLISHES", "\n};\n");

        Assert.Contains("    'SET_DRIVABLE': { scopes: ['driver'] },", published);
        Assert.Contains("    'DISCARD_SESSION_TREE': { scopes: ['sessions', 'landing', 'diff', 'sweep', 'treesSync'], session: 'id' },", published);
        Assert.Contains("    'LAND_ENTRY': { scopes: ['sessions', 'landing', 'diff', 'sweep', 'treesSync'], subject: 'entry' },", published);
        Assert.DoesNotContain("'STATE':", published);
        foreach (var route in DriverModule.Routes)
        {
            var declared = DriverModule.Declared(new IpcRequest { Id = "x", Module = DriverModule.Name, Type = route }) is { Scopes: not StateScopes.None };
            Assert.True(declared == published.Contains($"    '{route}': {{ scopes: [", StringComparison.Ordinal), route);
        }
    }

    [Fact]
    public void The_browser_module_s_presses_are_declared_under_its_own_name()
    {
        var browser = Section($"  '{BrowserModule.Name}': {{", "\n  },");

        Assert.Equal(BrowserModule.Presses.Order(StringComparer.Ordinal).Select(route => $"    '{route}': {{ scopes: ['browser'] }},"), browser.Trim('\n').Split('\n'));
    }

    [Fact]
    public void Each_person_s_press_is_listed_by_its_act_s_catalogue_key_and_an_open_route_is_not()
    {
        var words = Section("export const PERSON_WORDS: readonly string[] = [", "\n];");

        Assert.Contains("  \"person.route.SESSION_INPUT\"", words);
        Assert.Contains("  \"person.route.SET_DRIVABLE\"", words);
        Assert.DoesNotContain("\"person.route.STATE\"", words);
    }

    /// <summary>The written text after <paramref name="start"/> and before the next <paramref name="end"/>.</summary>
    private static string Section(string start, string end)
    {
        var from = Written.IndexOf(start, StringComparison.Ordinal);
        Assert.True(from >= 0, $"the routes file writes no `{start}`");
        from += start.Length;
        var to = Written.IndexOf(end, from, StringComparison.Ordinal);
        Assert.True(to >= 0, $"`{start}` has no end");
        return Written[from..to].TrimStart('\n');
    }
}
