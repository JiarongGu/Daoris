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
