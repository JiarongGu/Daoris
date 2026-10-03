using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The driver's note codes and the page's catalogues are two lists that must agree, in two languages and two artefacts, with
/// no compiler between them (LANG1a, D142 point 4; the language design §5). Read from this side, as the modules'
/// <c>RefusalCatalogueTests</c> reads its codes, so a new line the driver writes fails here until both languages word it.
/// </summary>
public sealed class NoteCodesTests
{
    private static string RepositoryRoot()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        return at?.FullName ?? throw new InvalidOperationException("no workspace root above the test binary");
    }

    /// <summary>A language's whole catalogue: one file per area under <c>locales/&lt;language&gt;/</c>, merged as the page merges them.</summary>
    private static JsonElement Catalogue(string language)
    {
        var folder = Path.Combine(RepositoryRoot(), "src", "Daoris.Web", "src", "locales", language);
        var merged = new JsonObject();
        foreach (var file in Directory.EnumerateFiles(folder, "*.json").Order(StringComparer.Ordinal))
        {
            foreach (var (key, value) in JsonNode.Parse(File.ReadAllText(file))!.AsObject()) merged[key] = value?.DeepClone();
        }

        return JsonDocument.Parse(merged.ToJsonString()).RootElement.Clone();
    }

    private static IReadOnlySet<string> Placeholders(string text) =>
        Regex.Matches(text, @"\{\{\s*([\w.]+)[^}]*\}\}").Select(match => match.Groups[1].Value).ToHashSet(StringComparer.Ordinal);

    private static string Entry(JsonElement catalogue, string key, string language)
    {
        Assert.True(
            catalogue.TryGetProperty(key, out var text) && !string.IsNullOrWhiteSpace(text.GetString()),
            $"`{key}` has no entry in {language}: the line would reach the person as its English.");
        return text.GetString()!;
    }

    [Theory]
    [InlineData("en")]
    [InlineData("zh")]
    public void Every_code_has_an_entry_that_says_each_of_its_values_and_no_other(string language)
    {
        var catalogue = Catalogue(language);
        Assert.NotEmpty(NoteCodes.All);
        foreach (var code in NoteCodes.All)
        {
            var said = Placeholders(Entry(catalogue, code.CatalogueKey, language));
            // A dropped value renders the fixed part and silently loses the fact (translation-parity).
            Assert.True(said.SetEquals(code.Values), $"{language}: `{code.CatalogueKey}` says {{{string.Join(", ", said)}}}, and `{code.Code}` carries {{{string.Join(", ", code.Values)}}}.");
        }
    }

    /// <summary>A <c>why</c> is a reason's code: each reason it may name has its entry, saying exactly the reason's own values.</summary>
    [Theory]
    [InlineData("en")]
    [InlineData("zh")]
    public void Every_reason_a_why_may_name_has_an_entry_that_says_its_own_values(string language)
    {
        var catalogue = Catalogue(language);
        foreach (var reasons in new[] { NoteCodes.Continue, NoteCodes.Cooling })
        {
            foreach (var reason in reasons.All)
            {
                var said = Placeholders(Entry(catalogue, reason.Key, language));
                Assert.True(said.SetEquals(reason.Values), $"{language}: `{reason.Key}` says {{{string.Join(", ", said)}}}, and `{reason.Code}` carries {{{string.Join(", ", reason.Values)}}}.");
            }
        }
    }

    /// <summary>Every reason the driver can give (MSG1f's <see cref="ContinueWhy"/>) is one a <c>why</c> declares, and only those.</summary>
    [Fact]
    public void Every_continue_reason_is_declared_once()
    {
        var reasons = typeof(ContinueWhy).GetFields()
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .Order(StringComparer.Ordinal);

        Assert.Equal(reasons, NoteCodes.Continue.All.Select(reason => reason.Code).Order(StringComparer.Ordinal));
        Assert.Equal(NoteCodes.Cooling.All.Count, NoteCodes.Cooling.All.DistinctBy(reason => reason.Code).Count());
    }

    [Fact]
    public void Every_code_is_declared_once_and_spelled_as_the_page_keys_it()
    {
        Assert.Equal(NoteCodes.All.Count, NoteCodes.All.Select(code => code.Code).Distinct(StringComparer.Ordinal).Count());
        foreach (var code in NoteCodes.All)
        {
            Assert.Matches("^[a-z]+\\.[a-z]+(-[a-z]+)*$", code.Code);
            Assert.Equal(code.Values.Count, code.Values.Distinct(StringComparer.Ordinal).Count());
        }
    }

    /// <summary>
    /// The web's twin test parses this file, one declaration per line (<c>locales/note.test.ts</c>): every code is declared in
    /// the form it reads, so a code written another way is never one the page's test misses.
    /// </summary>
    [Fact]
    public void Every_code_is_declared_in_the_form_the_page_s_test_parses()
    {
        var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "Daoris.Desktop", "Daoris.Desktop.Driver", "NoteCodes.cs"));
        var parsed = Regex.Matches(source, @"new\(""(?<code>[a-z][a-z.-]*)"", \[(?<values>[^\]]*)\](?:, Key: ""(?<key>[^""]+)"")?\)")
            .Select(match => match.Groups["code"].Value)
            .ToList();

        Assert.Equal(NoteCodes.All.Select(code => code.Code).Order(StringComparer.Ordinal), parsed.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void A_part_carries_only_the_values_its_code_declares()
    {
        var part = NoteCodes.EndedTimeout.Part("timed out after 30 minutes and was killed.", ("minutes", 30));
        Assert.Equal(30, part.Value("minutes"));
        Assert.Throws<ArgumentException>(() => NoteCodes.EndedDone.Part("the quest reached done.", ("exit", 1)));
        // A null value is not written: the page then shows the part's text.
        Assert.Empty(NoteCodes.EndedTimeout.Part("timed out.", ("minutes", null)).Values);
    }

    [Fact]
    public void A_reason_brings_its_own_values_and_elsewhere_names_the_agent_it_ran_on()
    {
        var adapter = NoteCodes.WentNewSession.Part("x", NoteCodes.Reason(ContinueWhy.AdapterChanged("claude-code", "codex-acp")));
        Assert.Equal("adapter", adapter.Value("why"));
        Assert.Equal("claude-code", adapter.Value("from"));
        Assert.Equal("codex-acp", adapter.Value("to"));

        var elsewhere = NoteCodes.WentCannot.Part("x", NoteCodes.Reason(ContinueWhy.Of(ContinueWhy.Elsewhere), "codex-acp"));
        Assert.Equal("codex-acp", elsewhere.Value("agent"));
    }

    [Fact]
    public void Parts_read_back_as_written()
    {
        IReadOnlyList<NotePart> parts =
        [
            NoteCodes.IntakePublished.Part("published `#q1`, `#q2` onto ask `#a1`", ("quests", new[] { "q1", "q2" }), ("ask", "a1")),
            NoteCodes.EndedExit.Part("exit 3", ("exit", 3)),
            NotePart.Said("Which branch?", NoteBy.Agent),
        ];

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("noteParts");
            NotePart.Write(writer, parts);
            writer.WriteEndObject();
        }

        using var document = JsonDocument.Parse(stream.ToArray());
        var read = NotePart.Read(document.RootElement)!;

        Assert.Equal(3, read.Count);
        Assert.Equal("intake.published", read[0].Code);
        Assert.Equal(["q1", "q2"], (IReadOnlyList<string>)read[0].Value("quests")!);
        Assert.Equal("a1", read[0].Value("ask"));
        Assert.Equal(3L, read[1].Value("exit"));
        Assert.Equal(("Which branch?", NoteBy.Agent), (read[2].Words, read[2].By));
        Assert.Null(NotePart.Read(JsonDocument.Parse("""{"note":"x"}""").RootElement));
    }

    /// <summary>Any serializer the modules hand a part to writes the record's shape, so the page reads it as the service answers it.</summary>
    [Fact]
    public void A_part_serializes_in_the_record_s_shape_wherever_it_goes()
    {
        var json = JsonSerializer.Serialize(new { Parts = new[] { NoteCodes.EndedTimeout.Part("t", ("minutes", 5)), NotePart.Said("w", NoteBy.Person) } });

        Assert.Equal("""{"Parts":[{"code":"ended.timeout","values":{"minutes":5},"text":"t"},{"words":"w","by":"person"}]}""", json);
    }

    [Fact]
    public void A_record_with_a_note_and_no_parts_is_carried_as_one_before_part()
    {
        var before = Noted.From("the quest reached done.", null);
        Assert.Equal("the quest reached done.", before.Note);
        Assert.Equal((NoteBy.Before, "the quest reached done."), (before.Parts.Single().By, before.Parts.Single().Words));

        Assert.Empty(Noted.From(null, null).Parts);
        var kept = Noted.From("x", [NotePart.Said("x", NoteBy.Agent)]);
        Assert.Equal(NoteBy.Agent, kept.Parts.Single().By);
    }

    [Fact]
    public void A_composed_note_is_its_lines_and_glue_and_each_coded_text_is_inside_it()
    {
        var noted = Noted.Of(NoteCodes.EndedParkedAsked, "It stopped with its quest still taken, to ask you:")
            .Then("\n\n", Noted.Said("Which branch?", NoteBy.Agent));

        Assert.Equal("It stopped with its quest still taken, to ask you:\n\nWhich branch?", noted.Note);
        Assert.Equal(["ended.parked-asked", null], noted.Parts.Select(part => part.Code));
        Assert.Empty(Noted.Said("", NoteBy.Person).Parts);

        // Where the English interleaves two lines, the second rides beside the first, its text already inside the note.
        var declined = Noted.Of(NoteCodes.EndedDeclined, "the session declined (exit 1); the reason is on the quest.")
            .Also(NoteCodes.EndedExit.Part("exit 1", ("exit", 1)));
        NoteAssert.Holds(declined);
        Assert.Equal(["ended.declined", "ended.exit"], declined.Parts.Select(part => part.Code));
    }
}

/// <summary>What every note site's test asks of what it wrote (LANG1a): each coded part's text inside the note it built.</summary>
internal static class NoteAssert
{
    public static void Holds(Noted noted) => Holds(noted.Note, noted.Parts);

    public static void Holds(string? note, IReadOnlyList<NotePart>? parts)
    {
        Assert.NotNull(parts);
        Assert.NotEmpty(parts);
        foreach (var part in parts)
        {
            var text = part.Code is null ? part.Words : part.Text;
            Assert.False(string.IsNullOrEmpty(text), $"`{part.Code ?? part.By}` has no text");
            Assert.True(note!.Contains(text, StringComparison.Ordinal), $"`{part.Code ?? part.By}`'s text \"{text}\" is not inside the note \"{note}\"");
            if (part.Code is not null) Assert.Contains(NoteCodes.All, code => code.Code == part.Code);
        }
    }

    /// <summary>The codes of the parts, in order; null for a words part.</summary>
    public static IReadOnlyList<string?> Codes(IReadOnlyList<NotePart>? parts) => [.. (parts ?? []).Select(part => part.Code)];
}
