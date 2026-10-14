using System.Text.Json;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A refusal code and its translation are two lists that must agree, in two languages and two
/// artefacts — and there is no compiler between them.
/// </summary>
/// <remarks>
/// <para>The framework's shape is a code the page translates (`errors.&lt;CODE&gt;`). A module that
/// raises a code the catalogue does not carry produces a refusal that renders as a bare identifier,
/// which is only marginally better than the generic failure this whole fix replaced — and it fails
/// SILENTLY, in the language nobody on the team reads first.</para>
///
/// <para>Checked from this side because this is the side that decides what codes exist. The web's own
/// i18n gate asserts `en` and `zh` carry the same keys; this asserts they carry the right ones.</para>
/// </remarks>
public sealed class RefusalCatalogueTests
{
    /// <summary>Walk up to the workspace root — the tests run from `bin/Debug/net10.0`.</summary>
    private static string RepositoryRoot()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        return at?.FullName ?? throw new InvalidOperationException("no workspace root above the test binary");
    }

    /// <summary>
    /// A language's whole catalogue. Since MOD2 it is one file per area under `locales/<language>/`, merged
    /// at load, and read here the same way; a single `<language>.json` no longer exists.
    /// </summary>
    private static JsonElement Catalogue(string language)
    {
        var folder = Path.Combine(RepositoryRoot(), "src", "Daoris.Web", "src", "locales", language);
        var merged = new System.Text.Json.Nodes.JsonObject();
        foreach (var file in Directory.EnumerateFiles(folder, "*.json").Order(StringComparer.Ordinal))
        {
            foreach (var (key, value) in System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(file))!.AsObject())
            {
                merged[key] = value?.DeepClone();
            }
        }

        Assert.NotEmpty(merged);
        return JsonDocument.Parse(merged.ToJsonString()).RootElement.Clone();
    }

    [Theory]
    [InlineData("en")]
    [InlineData("zh")]
    public void Every_refusal_a_module_can_raise_has_a_translation(string language)
    {
        var catalogue = Catalogue(language);

        Assert.NotEmpty(Refusals.All);
        foreach (var code in Refusals.All)
        {
            Assert.True(
                catalogue.TryGetProperty($"errors.{code}", out var text)
                && !string.IsNullOrWhiteSpace(text.GetString()),
                $"the module refusal `{code}` has no `errors.{code}` entry in {language}.json — it "
                + "would reach the person as a bare code.");
        }
    }

    /// <summary>
    /// REFUSE1: every code declared is one the catalogue test walks. `All` was a list kept by hand, so a
    /// code left out of it escaped the translation check; it is read off the declarations now.
    /// </summary>
    [Fact]
    public void Every_declared_code_is_one_the_catalogue_walks()
    {
        var declared = typeof(Refusals)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .Order(StringComparer.Ordinal);

        Assert.Equal(declared, Refusals.All.Order(StringComparer.Ordinal));
        Assert.Equal(Refusals.All.Count, Refusals.All.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// REFUSE1: a module refuses through the catalogue and nowhere else. A code typed at a throw site,
    /// or an exception built by hand, is a refusal no translation check can see.
    /// </summary>
    [Fact]
    public void Every_throw_site_names_a_declared_code()
    {
        var names = typeof(Refusals)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => field.Name)
            .ToHashSet(StringComparer.Ordinal);
        var desktop = Path.Combine(RepositoryRoot(), "src", "Daoris.Desktop");
        var sources = new[] { "Daoris.Desktop.Modules", "Daoris.Desktop.App" }
            .SelectMany(project => Directory.EnumerateFiles(Path.Combine(desktop, project), "*.cs", SearchOption.AllDirectories))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .ToList();
        Assert.NotEmpty(sources);

        var problems = new List<string>();
        var seen = 0;
        foreach (var path in sources)
        {
            var text = File.ReadAllText(path);
            var file = Path.GetFileName(path);
            foreach (System.Text.RegularExpressions.Match call in System.Text.RegularExpressions.Regex.Matches(
                text, @"Refusals\.Because\(\s*(?<code>[^,\s]+)\s*,"))
            {
                seen += 1;
                var code = call.Groups["code"].Value;
                if (!code.StartsWith("Refusals.", StringComparison.Ordinal) || !names.Contains(code["Refusals.".Length..]))
                {
                    problems.Add($"{file}: `{code}` is not a declared refusal");
                }
            }

            if (file != "Refusals.cs" && text.Contains("new ShenoraException(", StringComparison.Ordinal))
            {
                problems.Add($"{file}: builds a ShenoraException by hand, outside the catalogue");
            }
        }

        Assert.Empty(problems);
        // A scan that matched nothing would pass on a moved or renamed helper: it has to see them.
        Assert.True(seen > 10, $"the scan found {seen} refusal site(s); it should see every module's");
    }

    /// <summary>
    /// The driver's own sentences are rendered VERBATIM rather than re-authored, so that one entry
    /// must interpolate the message it is handed. A translation that dropped `{{message}}` would
    /// silently replace every driver refusal with one fixed sentence.
    /// </summary>
    [Theory]
    [InlineData("en")]
    [InlineData("zh")]
    public void The_verbatim_refusal_actually_carries_the_driver_s_words(string language)
    {
        var text = Catalogue(language).GetProperty($"errors.{Refusals.DriverRefused}").GetString();

        Assert.Contains("{{message}}", text);
    }

    /// <summary>
    /// REFAC1: a code the driver library chose (its <c>HistoryCodes</c>, which this class declares as its own) is raised only
    /// where it is declared here; any other is the driver's verbatim refusal, its sentence kept, so none reaches the page as a
    /// bare code. What REFUSE1's scan holds at a throw site naming a code, this holds at the one door that is handed one.
    /// </summary>
    [Fact]
    public void A_code_the_driver_library_chose_is_raised_only_where_it_is_declared_here()
    {
        var declared = Refusals.Declared(Daoris.Driver.HistoryCodes.Open, "it is open", ("quest", "q1"));
        var later = Refusals.Declared("HISTORY_FROM_LATER", "a newer service's sentence", ("quest", "q1"));

        Assert.Equal((Refusals.HistoryOpen, "q1", "it is open"), (declared.Code, declared.Parameters!["quest"], declared.Message));
        Assert.Equal((Refusals.DriverRefused, "a newer service's sentence"), (later.Code, later.Parameters!["message"]));
        Assert.Equal("a newer service's sentence", later.Message);
    }

    /// <summary>
    /// REFAC1 (the second-opinion review of 2026-10-07): which code a history word is said in is the driver library's
    /// <c>HistoryCodes.Of</c>, read through its projection (<c>HistoryAnswers.Reason</c>); the shell's routes add only the
    /// exception, and map no word themselves, where <c>HistoryKept</c> once repeated the mapping.
    /// </summary>
    [Fact]
    public void No_module_maps_a_history_word_to_its_code_itself()
    {
        var modules = Path.Combine(RepositoryRoot(), "src", "Daoris.Desktop", "Daoris.Desktop.Modules");
        var mapped = Directory.EnumerateFiles(modules, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(path => System.Text.RegularExpressions.Regex.IsMatch(File.ReadAllText(path), @"HistoryWords\.\w+\s*=>"))
            .Select(path => Path.GetFileName(path));

        Assert.Empty(mapped);
    }
}
