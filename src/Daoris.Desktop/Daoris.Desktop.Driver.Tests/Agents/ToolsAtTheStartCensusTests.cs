using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// CONTEXTONE1d (D230 point 3): the switch that loads a session's tools at its start is read in one place, and every process made from
/// an adapter, a start's, a go-on's or a conversation's, passes through it before it runs. A source scan, as
/// <see cref="EveryChildIsHandedTheToolsTests"/> holds the tools' environment: the next spawn written will not know to ask.
/// </summary>
/// <remarks>
/// <b>Equipped</b> is a call to <c>Equip(</c>, or the process handed on to the driver's <c>Prepare(adapter</c> or a host's
/// <c>Prepared(adapter</c>, which call it, within the statements after the process is made; either may be handed the registry the run
/// was started from first (<c>Prepared(adapters, adapter</c>).
/// </remarks>
[Trait(Category.Name, Category.Census)]
public sealed class ToolsAtTheStartCensusTests
{
    private const int Window = 1500;

    [Fact]
    public void The_switch_is_read_in_one_place_and_no_kind_of_session_asks_for_it()
    {
        var readers = new List<string>();
        foreach (var file in SourceFiles.All(SourceFiles.DriverProject, "*.cs"))
        {
            var reads = Regex.Matches(Code(File.ReadAllText(file)), @"\.ToolsUpFront\b").Count;
            readers.AddRange(Enumerable.Repeat(Path.GetFileName(file), reads));
        }

        Assert.Equal(["SpawnEquipment.cs"], readers.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Every_process_made_from_an_adapter_is_equipped_before_it_runs()
    {
        var made = 0;
        var missing = new List<string>();
        foreach (var file in SourceFiles.All(SourceFiles.DriverProject, "*.cs"))
        {
            var source = Code(File.ReadAllText(file));
            foreach (Match call in Regex.Matches(source, @"\.(Start|Chat|Resume)\((adapter|resolved)\b"))
            {
                made++;
                var after = source.Substring(call.Index, Math.Min(Window, source.Length - call.Index));
                if (!Regex.IsMatch(after, @"\bEquip\(|\.Prepared?\((adapters,\s*)?adapter\b"))
                {
                    missing.Add($"{Path.GetFileName(file)} at offset {call.Index}: {call.Value}");
                }
            }
        }

        // A start's, a conversation's, and the two go-ons on a kept conversation.
        Assert.True(made >= 4, $"expected to find the adapters' spawns, found {made}");
        Assert.True(missing.Count == 0, "a session's process that is not equipped defers its tools behind a search:\n" + string.Join('\n', missing));
    }

    /// <summary>Comments out first: a sentence naming a call is not a call.</summary>
    private static string Code(string source) => Regex.Replace(source.Replace("\r\n", "\n"), @"//[^\n]*", "");
}
