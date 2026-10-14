using System.Reflection;
using System.Text.Json;
using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// The tier an ask names and the words the platform says it in are two lists that must agree, in two
/// languages and two artefacts, with no compiler between them (INT4d).
/// </summary>
/// <remarks>
/// <para>Every record says which tier answered it (<c>model-decoupling</c>), and the platform says it in
/// the person's language: <c>asks.tier.&lt;tier&gt;</c> on the record, <c>asks.tierShort.&lt;tier&gt;</c>
/// on the card. A tier the catalogue does not carry is shown raw. That keeps the page honest, but it
/// is a gap all the same, and INT4b's <c>intake</c> reached the screen exactly that way.</para>
///
/// <para>Checked from this side because this is the side that decides which tiers exist. A tier is a
/// <c>By…</c> constant on <see cref="AskDesk"/>, and every one is asked for, so a new tier cannot ship
/// without its words. The web's own i18n gate asserts <c>en</c> and <c>zh</c> carry the same keys.</para>
/// </remarks>
public sealed class AskTierCatalogueTests
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

    /// <summary>Every tier the desk can write onto an ask: its <c>By…</c> constants.</summary>
    private static IReadOnlyList<string> Tiers() => typeof(AskDesk)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.IsLiteral && field.FieldType == typeof(string) && field.Name.StartsWith("By", StringComparison.Ordinal))
        .Select(field => (string)field.GetRawConstantValue()!)
        .ToList();

    /// <summary>The scan finds the tiers at all: one that found none would pass in every language.</summary>
    [Fact]
    public void The_scan_finds_every_tier_the_desk_writes() =>
        Assert.Equal(
            new[] { AskDesk.ByDeclarations, AskDesk.ByIntake, AskDesk.ByName }.Order(),
            Tiers().Order());

    [Theory]
    [InlineData("en")]
    [InlineData("zh")]
    public void Every_tier_an_ask_can_name_has_words_on_the_record_and_the_card(string language)
    {
        var catalogue = Catalogue(language);

        foreach (var tier in Tiers())
        {
            foreach (var key in new[] { $"asks.tier.{tier}", $"asks.tierShort.{tier}" })
            {
                Assert.True(
                    catalogue.TryGetProperty(key, out var text) && !string.IsNullOrWhiteSpace(text.GetString()),
                    $"the ask tier `{tier}` has no `{key}` in {language}.json — the platform would show it raw.");
            }
        }
    }
}
