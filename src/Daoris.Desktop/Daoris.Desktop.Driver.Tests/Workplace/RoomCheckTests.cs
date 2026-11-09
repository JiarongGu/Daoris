using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The check of a repository's rooms over its tracked files. Rooms whose build closures
/// meet (a unit, a shared input, a generator's source, an output written into another room's paths) are one claim group; overlapping
/// rooms, a unit or a lockfile split, a generated file written from two rooms and a path no room places are refused, naming the path
/// and the rooms; a reference across rooms is listed.
/// </summary>
public sealed class RoomCheckTests
{
    private static RoomCheckReport Check(string lanes, string? files, params string[] tracked) => RoomCheck.Of(lanes, files, tracked);

    private static string Outputs(string path) => $$"""{ "rules": [ { "kind": "output", "paths": ["{{path}}"] } ] }""";

    private static string[][] Groups(RoomCheckReport report) => [.. report.Groups.Select(group => group.ToArray())];

    [Fact]
    public void A_shared_props_file_joins_the_rooms_that_read_it_into_one_claim_group()
    {
        var report = Check("""
            {
              "lanes": [
                { "id": "driver", "paths": ["driver/**"], "builds": { "units": ["driver/Driver.csproj"], "reads": ["Directory.Build.props"] } },
                { "id": "service", "paths": ["service/**"], "builds": { "units": ["service/Service.csproj"], "reads": ["Directory.Build.props"] } },
                { "id": "records", "paths": ["TASKS.md"], "builds": {} }
              ],
              "laneless": [ { "what": "Shared build settings.", "room": "docs", "paths": ["Directory.Build.props", "README.md"] } ]
            }
            """, null, "driver/Driver.csproj", "driver/A.cs", "service/Service.csproj", "service/B.cs", "TASKS.md", "Directory.Build.props", "README.md");

        Assert.Empty(report.Problems);
        Assert.Equal([["driver", "service", "docs"], ["records"]], Groups(report));
        Assert.Contains(new RoomCrossing("driver", "reads", "Directory.Build.props", "docs"), report.Crossings);
        Assert.Contains("4 rooms, in 2 claim groups: driver, service, docs; records.", report.Said(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_generators_source_and_its_output_join_its_room_with_both()
    {
        var report = Check("""
            {
              "lanes": [
                { "id": "web", "paths": ["web/**"], "builds": { "units": ["web/package.json"] } },
                { "id": "service", "paths": ["contracts/**"], "builds": { "units": ["contracts/Contracts.csproj"] } },
                { "id": "tools", "paths": ["tools/**"], "builds": { "reads": ["contracts/"], "writes": ["web/src/generated/"] } },
                { "id": "records", "paths": ["TASKS.md"], "builds": {} }
              ]
            }
            """, Outputs("web/src/generated/"),
            "web/package.json", "web/src/main.ts", "contracts/Contracts.csproj", "contracts/Quest.cs", "tools/gen.mjs", "TASKS.md");

        Assert.Empty(report.Problems);
        Assert.Equal([["web", "service", "tools"], ["records"]], Groups(report));
        Assert.Contains(new RoomCrossing("tools", "reads", "contracts/", "service"), report.Crossings);
        Assert.Contains(new RoomCrossing("tools", "writes", "web/src/generated/", "web"), report.Crossings);
    }

    [Fact]
    public void An_output_written_into_another_rooms_paths_joins_them_and_a_reader_of_it_too()
    {
        var report = Check("""
            {
              "lanes": [
                { "id": "web", "paths": ["web/**"], "builds": { "units": ["web/package.json"], "writes": ["service/wwwroot/"] } },
                { "id": "service", "paths": ["service/**"], "builds": { "units": ["service/Http.csproj"] } },
                { "id": "modules", "paths": ["modules/**"], "builds": { "units": ["modules/Modules.csproj"], "reads": ["service/wwwroot/"] } },
                { "id": "cli", "paths": ["cli/**"], "builds": { "units": ["cli/package.json"] } }
              ]
            }
            """, Outputs("service/wwwroot/"),
            "web/package.json", "service/Http.csproj", "modules/Modules.csproj", "cli/package.json");

        Assert.Empty(report.Problems);
        Assert.Equal([["web", "service", "modules"], ["cli"]], Groups(report));
        Assert.Contains(new RoomCrossing("web", "writes", "service/wwwroot/", "service"), report.Crossings);
        Assert.Contains(new RoomCrossing("modules", "reads", "service/wwwroot/", "web"), report.Crossings);
    }

    [Fact]
    public void A_laneless_input_in_no_room_joins_the_rooms_that_read_it()
    {
        var report = Check("""
            {
              "lanes": [
                { "id": "driver", "paths": ["driver/**"], "builds": { "units": ["driver/Driver.csproj"], "reads": ["canon/canon.json"] } },
                { "id": "cli", "paths": ["cli/**"], "builds": { "units": ["cli/package.json"], "reads": ["canon/"] } },
                { "id": "web", "paths": ["web/**"], "builds": { "units": ["web/package.json"] } }
              ],
              "laneless": [ { "what": "The doctrine.", "paths": ["canon/**"] } ]
            }
            """, null, "driver/Driver.csproj", "cli/package.json", "web/package.json", "canon/canon.json", "canon/core/rule.md");

        Assert.Empty(report.Problems);
        Assert.Equal([["driver", "cli"], ["web"]], Groups(report));
        Assert.Contains(new RoomCrossing("driver", "reads", "canon/canon.json", RoomCrossing.NoRoom), report.Crossings);
        Assert.Equal(["canon/**"], report.InNone);
    }

    [Fact]
    public void A_reference_to_another_rooms_unit_is_listed()
    {
        var report = Check("""
            {
              "lanes": [
                { "id": "driver", "paths": ["driver/**"], "builds": { "units": ["driver/Tests.csproj"], "reads": ["launcher/Launcher.csproj"] } },
                { "id": "modules", "paths": ["launcher/**"], "builds": { "units": ["launcher/Launcher.csproj"] } }
              ]
            }
            """, null, "driver/Tests.csproj", "launcher/Launcher.csproj", "launcher/Program.cs");

        Assert.Empty(report.Problems);
        Assert.Equal([new RoomCrossing("driver", "reads", "launcher/Launcher.csproj", "modules")], report.Crossings);
        Assert.Contains("driver reads launcher/Launcher.csproj, modules's", report.Said(), StringComparison.Ordinal);
    }

    [Fact]
    public void Overlapping_rooms_are_refused_naming_the_path_and_both_rooms()
    {
        var report = Check("""
            {
              "lanes": [
                { "id": "a", "paths": ["src/**"], "builds": {} },
                { "id": "b", "paths": ["src/b/**"], "builds": {} }
              ]
            }
            """, null, "src/a.cs", "src/b/b.cs");

        Assert.Contains("src/b/b.cs is in rooms a and b", report.Problems);
        Assert.False(report.Passes);
    }

    [Fact]
    public void A_unit_split_across_rooms_is_refused_and_one_room_of_both_lanes_passes()
    {
        const string Split = """
            {
              "lanes": [
                { "id": "web-shell", "paths": ["web/**", "!web/settings/**"], "builds": { "units": ["web/package.json"] } },
                { "id": "web-settings", "paths": ["web/settings/**"], "builds": {} }
              ]
            }
            """;
        string[] tracked = ["web/package.json", "web/src/main.ts", "web/settings/Settings.tsx"];

        Assert.Contains("the unit web/package.json of room web-shell has sources in room web-settings: web/settings/Settings.tsx", Check(Split, null, tracked).Problems);
        var joined = Check(Split.Replace("\"id\": \"web-settings\",", "\"id\": \"web-settings\", \"room\": \"web-shell\",", StringComparison.Ordinal), null, tracked);
        Assert.Empty(joined.Problems);
        Assert.Equal([["web-shell"]], Groups(joined));
    }

    [Fact]
    public void A_lockfile_in_another_room_than_the_unit_beside_it_is_refused()
    {
        var report = Check("""
            {
              "lanes": [
                { "id": "web", "paths": ["web/**", "!web/package-lock.json"], "builds": { "units": ["web/package.json"] } },
                { "id": "tools", "paths": ["tools/**", "web/package-lock.json"], "builds": {} }
              ]
            }
            """, """{ "rules": [ { "kind": "dependency", "paths": ["web/node_modules/"], "key": ["web/package-lock.json"] } ] }""",
            "web/package.json", "web/package-lock.json", "tools/a.mjs");

        Assert.Contains("the lockfile web/package-lock.json is in room tools and the unit beside it, web/package.json, in room web", report.Problems);
    }

    [Fact]
    public void A_generated_file_written_from_two_rooms_is_refused()
    {
        var report = Check("""
            {
              "lanes": [
                { "id": "tools", "paths": ["tools/**"], "builds": { "writes": ["docs/code-map.json", "out/"] } },
                { "id": "devkit", "paths": ["devkit/**"], "builds": { "writes": ["docs/code-map.json"] } },
                { "id": "web", "paths": ["web/**"], "builds": { "writes": ["out/web/"] } }
              ],
              "laneless": [ { "what": "The documents.", "paths": ["docs/**"] } ]
            }
            """, Outputs("out/"), "tools/a.mjs", "devkit/b.cs", "web/c.ts", "docs/code-map.json");

        Assert.Contains("docs/code-map.json is written from rooms tools and devkit", report.Problems);
        Assert.Contains("out/web/ is written from rooms tools and web", report.Problems);
    }

    [Fact]
    public void A_path_no_room_places_and_none_says_is_refused()
    {
        var report = Check("""{ "lanes": [ { "id": "a", "paths": ["a/**"], "builds": {} } ] }""", null, "a/x.cs", "CHANGELOG.md");

        Assert.Contains("CHANGELOG.md is in no room and no lane or laneless group says so", report.Problems);
    }

    [Fact]
    public void A_read_or_write_that_names_nothing_tracked_or_declared_is_refused()
    {
        var report = Check("""
            { "lanes": [ { "id": "a", "paths": ["a/**"], "builds": { "units": ["a/A.csproj"], "reads": ["b/B.csproj"], "writes": ["a/bin/"] } } ] }
            """, null, "a/A.csproj");

        Assert.Contains("room a reads b/B.csproj, which no tracked file or declared output is", report.Problems);
        Assert.Contains("room a writes a/bin/, which is neither tracked nor named in daoris.files.json", report.Problems);
    }

    [Fact]
    public void A_repository_that_declares_no_rooms_checks_nothing_and_says_so()
    {
        var report = Check("""{ "lanes": [ { "id": "a", "paths": ["a/**"] } ] }""", null, "a/x.cs", "elsewhere.md");

        Assert.True(report.Passes);
        Assert.Empty(report.Groups);
        Assert.Contains("declares no rooms", report.Said(), StringComparison.Ordinal);
    }

    /// <summary>A rule reaching a tracked path is git's to cover, as its ignore is: the rule is kept, and the floor keeps the path git's.</summary>
    [Fact]
    public void A_rule_reaching_a_tracked_path_is_kept_and_fails_nothing()
    {
        var report = Check("""{ "lanes": [ { "id": "a", "paths": ["a/**"], "builds": {} } ] }""", Outputs("a/"), "a/x.cs");

        Assert.True(report.Passes, string.Join("\n", report.Problems));
        Assert.Equal([FileKind.Output], report.Rules.Select(rule => rule.Kind));
    }
}
