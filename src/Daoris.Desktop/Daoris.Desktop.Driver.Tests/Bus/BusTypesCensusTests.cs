using System.Reflection;
using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The census of the bus's types: every type is declared once, in the registry (<see cref="BusTypes"/>), and names
/// its obligations: its emitter, its obliged handler or a request's answerer, a deadline and what set it, a retry budget and a give-up from
/// the closed list, which ends at the owner's final exit and keeps no silent give-up. No desktop source builds a type elsewhere or spells
/// a kind, so a type emitted that the registry lacks fails here, naming its file and line.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed partial class BusTypesCensusTests
{
    /// <summary>The desktop's own programs, whose sources could emit a type.</summary>
    private static readonly string[] Projects =
    [
        "Daoris.Desktop.App", "Daoris.Desktop.Command", "Daoris.Desktop.Driver", "Daoris.Desktop.Driver.Host", "Daoris.Desktop.GitDoor",
        "Daoris.Desktop.Launcher", "Daoris.Desktop.Modules", "Daoris.Desktop.OwnerStandIn", "Daoris.Desktop.PieceStart",
    ];

    [Fact]
    public void Every_registered_type_names_its_obligations()
    {
        Assert.Empty(Faults(BusTypes.All));
        Assert.NotEmpty(BusTypes.All);
    }

    /// <summary>The census's own proof: a type with no obliged one, no deadline, a give-up outside the list or a field of no shape is red.</summary>
    [Fact]
    public void A_type_without_its_obligations_or_outside_the_list_fails_the_census()
    {
        var sound = BusTypes.CheckPassed;
        (BusType Type, string Says)[] cases =
        [
            (sound with { Obliged = "" }, "names no obliged handler"),
            (BusTypes.OpinionAsked with { Obliged = " " }, "a request with no answerer"),
            (sound with { Deadline = BusDeadline.Stated(TimeSpan.Zero, "none") }, "names no deadline"),
            (sound with { Deadline = BusDeadline.Stated(TimeSpan.FromMinutes(5), "") }, "says nothing of what set it"),
            (sound with { GiveUp = "gave-up" }, "give-up `gave-up` is outside the list"),
            (sound with { GiveUp = "ignored" }, "give-up `ignored` is outside the list"),
            (sound with { Deadline = BusDeadline.Measured(TimeSpan.FromMinutes(5), "n=3, the most 1 s") }, "names no commit it was read at"),
            (sound with { Retries = -1 }, "retries"),
            (sound with { Emitter = "" }, "names no emitter"),
            (sound with { Class = "event" }, "class `event`"),
            (sound with { Name = "passed", Version = 0 }, "namespace.name"),
            (sound with { Fields = [new("invocation", BusShapes.Id), new("invocation", BusShapes.Code)] }, "`invocation` twice"),
            (sound with { Fields = [new("words", "sentence")] }, "`words` has no shape"),
            (sound with { Name = "check.orphaned" }, "names nothing that ends it"),
            (BusTypes.Stalled with { GiveUp = BusGiveUps.Held }, "names nothing that ends it"),
        ];

        foreach (var (type, says) in cases)
        {
            var faults = Faults([type]);
            Assert.True(faults.Exists(fault => fault.Contains(says, StringComparison.Ordinal)), $"`{says}` was not said: {string.Join("; ", faults)}");
        }

        Assert.Contains(Faults([sound, sound]), fault => fault.Contains("declared twice", StringComparison.Ordinal));
    }

    [Fact]
    public void The_registry_lists_every_type_it_declares_once()
    {
        var declared = typeof(BusTypes).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(BusType)).Select(field => (BusType)field.GetValue(null)!).ToList();
        Assert.Equal(declared.Count, BusTypes.All.Count);
        Assert.All(declared, type => Assert.Contains(type, BusTypes.All));
        Assert.All(BusTypes.All, type => Assert.Same(type, BusTypes.Of(type.Kind)));
    }

    [Fact]
    public void No_source_emits_a_type_the_registry_lacks()
    {
        var registry = SourceFiles.Driver("BusTypes.cs");
        var found = new List<string>();
        foreach (var path in Projects.SelectMany(project => SourceFiles.All(project, "*.cs")))
        {
            var code = SourceTexts.Code(SourceTexts.Of(path));
            var named = Path.GetRelativePath(WorkspaceRoot.Folder, path).Replace('\\', '/');
            foreach (Match kind in KindLiteral().Matches(code))
            {
                var spelled = kind.Groups["kind"].Value;
                found.Add(BusTypes.Of(spelled) is null
                    ? $"{named}:{Line(code, kind.Index)} emits `{spelled}`, which the registry lacks: declare it in BusTypes.cs"
                    : $"{named}:{Line(code, kind.Index)} spells `{spelled}`: name it by its type in BusTypes, where it is declared once");
            }

            if (path == registry) continue;
            foreach (Match made in Made().Matches(code))
            {
                found.Add($"{named}:{Line(code, made.Index)} makes a bus type outside the registry (`{made.Value.Trim()}`): declare it in BusTypes.cs");
            }
        }

        Assert.Empty(found);
    }

    /// <summary>The census's rule over a source's text: a kind spelled, and a type made, each found where they stand.</summary>
    [Fact]
    public void The_scan_finds_a_spelled_kind_and_a_type_made_elsewhere()
    {
        const string source = "var draft = new BusDraft(new BusType(\"check.forged\", 1, BusClasses.Fact, e, o, d, 0, g, []), run);\nif (kind == \"check.started/1\") return;\nvar next = BusTypes.CheckStarted with { Version = 2 };\n";
        Assert.Equal(["check.started/1"], KindLiteral().Matches(source).Select(match => match.Groups["kind"].Value));
        Assert.Equal(2, Made().Matches(source).Count);
        Assert.Equal(3, Line(source, source.IndexOf("BusTypes.CheckStarted", StringComparison.Ordinal)));
    }

    /// <summary>What the census says of each type's declaration, and of a kind declared twice.</summary>
    internal static List<string> Faults(IReadOnlyList<BusType> types)
    {
        var faults = new List<string>();
        foreach (var type in types)
        {
            var at = $"`{type.Kind}`";
            if (!Named().IsMatch(type.Name) || type.Version < 1) faults.Add($"{at}: its name is no `namespace.name` of a version from 1");
            if (!BusClasses.All.Contains(type.Class)) faults.Add($"{at}: its class `{type.Class}` is no {string.Join(", ", BusClasses.All)}");
            if (string.IsNullOrWhiteSpace(type.Emitter)) faults.Add($"{at}: it names no emitter");
            if (string.IsNullOrWhiteSpace(type.Obliged))
            {
                faults.Add(type.Class == BusClasses.Request ? $"{at}: a request with no answerer" : $"{at}: it names no obliged handler");
            }

            if (type.Deadline is not { Within.Ticks: > 0 }) faults.Add($"{at}: it names no deadline");
            else if (string.IsNullOrWhiteSpace(type.Deadline.Basis)) faults.Add($"{at}: its deadline says nothing of what set it");
            else if (!type.Deadline.Provisional && !BuiltAt().IsMatch(type.Deadline.Basis)) faults.Add($"{at}: its measured deadline names no commit it was read at");
            if (type.Retries < 0) faults.Add($"{at}: its retries are under none");
            if (!BusGiveUps.All.Contains(type.GiveUp))
            {
                faults.Add($"{at}: its give-up `{type.GiveUp}` is outside the list ({string.Join(", ", BusGiveUps.All)}): a chain ends at the owner's final exit, never a silent give-up");
            }

            var ends = BusTypes.Ends(type);
            if (BusTypes.Obliges(type) && ends.Count == 0) faults.Add($"{at}: it obliges its handler and names nothing that ends it: declare its ends in BusTypes.cs");
            if (!BusTypes.Obliges(type) && ends.Count > 0) faults.Add($"{at}: it obliges no one and names ends");
            foreach (var end in ends)
            {
                if (!BusEnds.All.Contains(end.By)) faults.Add($"{at}: an end matches by `{end.By}`, no {string.Join(", ", BusEnds.All)}");
                if ((end.Type is null) != (end.By == BusEnds.Acknowledgement)) faults.Add($"{at}: an end by `{end.By}` names {(end.Type is null ? "no" : "a")} type");
                if (end.Type is { } ending && !BusTypes.Declares(ending)) faults.Add($"{at}: it is ended by `{ending.Kind}`, which the registry does not declare");
            }

            foreach (var twice in type.Fields.GroupBy(field => field.Name).Where(group => group.Count() > 1)) faults.Add($"{at}: it declares `{twice.Key}` twice");
            foreach (var field in type.Fields.Where(field => !BusShapes.All.Contains(field.Shape))) faults.Add($"{at}: `{field.Name}` has no shape of {string.Join(", ", BusShapes.All)}");
        }

        foreach (var twice in types.GroupBy(type => type.Kind).Where(group => group.Count() > 1)) faults.Add($"`{twice.Key}` is declared twice");
        return faults;
    }

    private static int Line(string code, int index) => code.AsSpan(0, index).Count('\n') + 1;

    // A string literal shaped as a kind, `namespace.name/version`.
    [GeneratedRegex("\"(?<kind>[a-z][a-z0-9-]*(?:\\.[a-z0-9-]+)+/[0-9]+)\"")]
    private static partial Regex KindLiteral();

    // A type made: built, or copied with a change.
    [GeneratedRegex(@"\bnew\s+BusType\s*\(|\bBusTypes\.\w+\s+with\b")]
    private static partial Regex Made();

    [GeneratedRegex("^[a-z][a-z0-9-]*(?:\\.[a-z0-9-]+)+$")]
    private static partial Regex Named();

    [GeneratedRegex("\\b[0-9a-f]{10}\\b")]
    private static partial Regex BuiltAt();
}
