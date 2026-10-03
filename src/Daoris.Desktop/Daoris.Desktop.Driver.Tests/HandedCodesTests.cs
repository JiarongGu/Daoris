using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The codes an instruction's account carries (CONTEXT1) and the page's catalogues are two lists that must agree, in two
/// languages and two artefacts, with no compiler between them (D143 point 1, as LANG1a's note codes are). Read from this side,
/// as <see cref="NoteCodesTests"/> reads its codes: a section, a source, an absence or a cut the composer writes fails here
/// until both languages word it, and an entry no code declares fails too. The page's <c>work/handed.test.ts</c> holds the
/// same from its side.
/// </summary>
public sealed class HandedCodesTests
{
    /// <summary>The codes a class of them declares: each <c>const string</c> it holds, by its value.</summary>
    internal static IReadOnlySet<string> Declared(Type holder) =>
        holder.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field is { IsLiteral: true } && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>Each family of codes and the catalogue prefix its entries live under.</summary>
    private static readonly (string Prefix, Type Holder)[] Families =
    [
        ("work.handed.section.", typeof(HandedSections)),
        ("work.handed.source.", typeof(HandedSources)),
        ("work.handed.none.", typeof(HandedNones)),
        ("work.handed.cut.", typeof(HandedCuts)),
    ];

    private static string RepositoryRoot()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        return at?.FullName ?? throw new InvalidOperationException("no workspace root above the test binary");
    }

    /// <summary>A language's whole catalogue, merged as the page merges its areas.</summary>
    private static Dictionary<string, string> Catalogue(string language)
    {
        var folder = Path.Combine(RepositoryRoot(), "src", "Daoris.Web", "src", "locales", language);
        var merged = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(folder, "*.json").Order(StringComparer.Ordinal))
        {
            foreach (var (key, value) in JsonNode.Parse(File.ReadAllText(file))!.AsObject())
            {
                if (value?.GetValueKind() == JsonValueKind.String) merged[key] = value.GetValue<string>();
            }
        }

        return merged;
    }

    private static IReadOnlySet<string> Placeholders(string text) =>
        Regex.Matches(text, @"\{\{\s*([\w.]+)[^}]*\}\}").Select(match => match.Groups[1].Value).ToHashSet(StringComparer.Ordinal);

    [Fact]
    public void Each_family_declares_its_codes()
    {
        // A reflection that read nothing would pass every assertion under it.
        Assert.True(Declared(typeof(HandedSections)).Count >= 20);
        Assert.True(Declared(typeof(HandedSources)).Count >= 10);
        Assert.Equal(5, Declared(typeof(HandedNones)).Count);
        Assert.Equal(Declared(typeof(HandedCuts)).Order(), HandedCuts.Values.Keys.Order());
    }

    [Theory]
    [InlineData("en")]
    [InlineData("zh")]
    public void Every_code_has_an_entry_that_says_exactly_its_values(string language)
    {
        var catalogue = Catalogue(language);
        foreach (var (prefix, holder) in Families)
        {
            foreach (var code in Declared(holder))
            {
                var key = prefix + code;
                Assert.True(
                    catalogue.TryGetValue(key, out var entry) && !string.IsNullOrWhiteSpace(entry),
                    $"`{key}` has no entry in {language}: the page would show the driver's English, marked.");
                var values = holder == typeof(HandedCuts) ? HandedCuts.Values[code] : [];
                // A dropped value renders the fixed part and silently loses the fact (translation-parity).
                Assert.True(
                    Placeholders(entry!).SetEquals(values),
                    $"{language}: `{key}` says {{{string.Join(", ", Placeholders(entry!))}}}, and `{code}` carries {{{string.Join(", ", values)}}}.");
            }
        }
    }

    [Theory]
    [InlineData("en")]
    [InlineData("zh")]
    public void No_entry_names_a_code_nothing_declares(string language)
    {
        var catalogue = Catalogue(language);
        foreach (var (prefix, holder) in Families)
        {
            var declared = Declared(holder);
            var orphans = catalogue.Keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal) && !declared.Contains(key[prefix.Length..]));
            Assert.Empty(orphans);
        }
    }
}
