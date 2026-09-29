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

    private static JsonElement Catalogue(string language)
    {
        var path = Path.Combine(RepositoryRoot(), "src", "Daoris.Web", "src", "locales", $"{language}.json");
        return JsonDocument.Parse(File.ReadAllText(path)).RootElement.Clone();
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
}
