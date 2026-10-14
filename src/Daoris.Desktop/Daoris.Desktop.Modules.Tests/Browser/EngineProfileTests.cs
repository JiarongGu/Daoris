using System.Text.Json.Nodes;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The engine's profile as <c>daoris-browser</c> edits it before the engine starts (CHR5, CHR7):
/// Daoris's favorites as a folder on the bookmarks bar, and other software's Chrome extensions
/// refused or offered again. The person's own bookmarks and their own refusals are never Daoris's.
/// </summary>
public sealed class EngineProfileTests : Bridge
{
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 9, 0, 0, TimeSpan.Zero);

    private static readonly Favorite[] Two =
    [
        new("https://site.example/board", "Board"),
        new("http://localhost:4200/", "localhost"),
    ];

    private static JsonArray Bar(string json) => JsonNode.Parse(json)!["roots"]!["bookmark_bar"]!["children"]!.AsArray();

    private static JsonObject DaorisFolder(string json) =>
        Bar(json).OfType<JsonObject>().Single(node => (string?)node["guid"] == EngineProfile.DaorisFolderGuid);

    private static string[] Ids(JsonNode? node) => node switch
    {
        JsonObject o => [.. (o["id"] is JsonValue id ? [(string)id!] : Array.Empty<string>()), .. o.SelectMany(pair => Ids(pair.Value))],
        JsonArray a => [.. a.SelectMany(Ids)],
        _ => [],
    };

    /// <summary>The person's own: a page on the bar and a folder under the other bookmarks.</summary>
    private const string Theirs = """
        { "checksum": "0f1e2d", "roots": {
            "bookmark_bar": { "children": [ { "guid": "aaaaaaaa-0000-4000-8000-000000000001", "id": "7", "name": "Mine", "type": "url", "url": "https://mine.example/" } ],
                              "guid": "0bc5d13f-2cba-5d74-951f-3f233fe6c908", "id": "1", "name": "Bookmarks bar", "type": "folder" },
            "other": { "children": [ { "children": [], "guid": "aaaaaaaa-0000-4000-8000-000000000002", "id": "12", "name": "Kept", "type": "folder" } ],
                       "guid": "82b081ec-3dd3-529c-8475-ab6c344590dd", "id": "2", "name": "Other bookmarks", "type": "folder" },
            "synced": { "children": [], "guid": "4cf2e351-0e85-532b-bb37-df045d8f8d0f", "id": "3", "name": "Mobile bookmarks", "type": "folder" } },
          "version": 1 }
        """;

    [Fact]
    public void No_bookmarks_and_no_favorites_changes_nothing() =>
        Assert.Null(EngineProfile.WithFavorites(null, [], Now));

    [Fact]
    public void A_profile_with_no_bookmarks_gets_the_engines_roots_and_Daoris_folder_on_the_bar()
    {
        var (json, created) = EngineProfile.WithFavorites(null, Two, Now)!.Value;

        Assert.True(created);
        var roots = JsonNode.Parse(json)!["roots"]!.AsObject();
        Assert.Equal(EngineProfile.BarGuid, (string?)roots["bookmark_bar"]!["guid"]);
        Assert.Equal(EngineProfile.OtherGuid, (string?)roots["other"]!["guid"]);
        Assert.Equal(EngineProfile.MobileGuid, (string?)roots["synced"]!["guid"]);

        var folder = DaorisFolder(json);
        Assert.Equal("Daoris", (string?)folder["name"]);
        Assert.Equal(["Board", "localhost"], folder["children"]!.AsArray().Select(c => (string)c!["name"]!));
        Assert.Equal(["https://site.example/board", "http://localhost:4200/"], folder["children"]!.AsArray().Select(c => (string)c!["url"]!));
        Assert.Equal(Ids(JsonNode.Parse(json)).Length, Ids(JsonNode.Parse(json)).Distinct().Count());
    }

    [Fact]
    public void The_persons_own_bookmarks_stay_and_Daoris_folder_joins_the_end_of_the_bar()
    {
        var (json, created) = EngineProfile.WithFavorites(Theirs, Two, Now)!.Value;

        Assert.True(created);
        var bar = Bar(json);
        Assert.Equal(["Mine", "Daoris"], bar.Select(node => (string)node!["name"]!));
        Assert.Equal("Kept", (string?)JsonNode.Parse(json)!["roots"]!["other"]!["children"]![0]!["name"]);
        Assert.All(Ids(DaorisFolder(json)), id => Assert.True(long.Parse(id) > 12));
        Assert.Null(JsonNode.Parse(json)!["checksum"]);
    }

    /// <summary>Found by its id, wherever the person moved it and whatever they renamed it.</summary>
    [Fact]
    public void Daoris_folder_is_refilled_where_the_person_put_it()
    {
        var first = EngineProfile.WithFavorites(Theirs, Two, Now)!.Value.Json;
        var moved = JsonNode.Parse(first)!.AsObject();
        var bar = moved["roots"]!["bookmark_bar"]!["children"]!.AsArray();
        var folder = bar.OfType<JsonObject>().Single(node => (string?)node["guid"] == EngineProfile.DaorisFolderGuid);
        bar.Remove(folder);
        folder["name"] = "Work";
        bar.Insert(0, folder);

        var (json, created) = EngineProfile.WithFavorites(moved.ToJsonString(), [new("https://site.example/new", "New")], Now)!.Value;

        Assert.False(created);
        Assert.Equal(["Work", "Mine"], Bar(json).Select(node => (string)node!["name"]!));
        Assert.Equal(["New"], DaorisFolder(json)["children"]!.AsArray().Select(c => (string)c!["name"]!));
    }

    [Fact]
    public void With_no_favorites_Daoris_folder_goes_and_nothing_else_does()
    {
        var with = EngineProfile.WithFavorites(Theirs, Two, Now)!.Value.Json;

        var (json, _) = EngineProfile.WithFavorites(with, [], Now)!.Value;

        Assert.Equal(["Mine"], Bar(json).Select(node => (string)node!["name"]!));
        Assert.Null(EngineProfile.WithFavorites(Theirs, [], Now));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{ "roots": {} }""")]
    [InlineData("""{ "roots": { "bookmark_bar": { "children": "none" } } }""")]
    public void Bookmarks_it_cannot_read_are_left_as_they_are(string bookmarks) =>
        Assert.Null(EngineProfile.WithFavorites(bookmarks, Two, Now));

    [Fact]
    public void A_favorite_has_the_same_id_every_start_in_the_form_the_engine_takes()
    {
        var id = EngineProfile.GuidFor("https://site.example/board");

        Assert.Equal(id, EngineProfile.GuidFor("https://site.example/board"));
        Assert.NotEqual(id, EngineProfile.GuidFor("https://site.example/other"));
        Assert.Matches("^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$", id);
    }

    private const string Registered = "ncennffkjdiamlpmcbajkmaiiiddgioo";
    private const string TheirOwn = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private static string[] Uninstalls(string json) =>
        [.. JsonNode.Parse(json)!["extensions"]!["external_uninstalls"]!.AsArray().Select(id => (string)id!)];

    [Fact]
    public void Refusing_seeds_the_registered_extensions_and_names_only_those_as_Daoris_own()
    {
        var prefs = """{ "extensions": { "external_uninstalls": [ "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" ] }, "theirs": 1 }""";

        var (json, refused) = EngineProfile.Refuse(prefs, [Registered, TheirOwn, "not-an-id"], [])!.Value;

        Assert.Equal([TheirOwn, Registered], Uninstalls(json));
        Assert.Equal([Registered], refused);
        Assert.Equal(1, (int)JsonNode.Parse(json)!["theirs"]!);
    }

    [Fact]
    public void Refusing_again_keeps_what_Daoris_refused_before()
    {
        var (first, refused) = EngineProfile.Refuse(null, [Registered], [])!.Value;

        var (json, again) = EngineProfile.Refuse(first, [Registered], refused)!.Value;

        Assert.Equal([Registered], Uninstalls(json));
        Assert.Equal([Registered], again);
    }

    [Fact]
    public void Offering_again_takes_back_only_what_Daoris_refused()
    {
        var prefs = $$"""{ "extensions": { "external_uninstalls": [ "{{TheirOwn}}", "{{Registered}}" ] } }""";

        var json = EngineProfile.Offer(prefs, [Registered])!;

        Assert.Equal([TheirOwn], Uninstalls(json));
    }

    [Fact]
    public void The_bar_is_shown_on_every_tab_and_the_rest_is_kept()
    {
        var json = EngineProfile.ShowBar("""{ "bookmark_bar": { "other": 2 }, "theirs": 1 }""")!;

        Assert.True((bool)JsonNode.Parse(json)!["bookmark_bar"]!["show_on_all_tabs"]!);
        Assert.Equal(2, (int)JsonNode.Parse(json)!["bookmark_bar"]!["other"]!);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    public void Preferences_it_cannot_read_are_left_as_they_are(string prefs)
    {
        Assert.Null(EngineProfile.Refuse(prefs, [Registered], []));
        Assert.Null(EngineProfile.Offer(prefs, [Registered]));
        Assert.Null(EngineProfile.ShowBar(prefs));
    }

    [Fact]
    public void Daoris_record_reads_back_what_it_wrote_and_nothing_from_what_it_cannot_read()
    {
        var record = new EngineRecord([Registered], BarShown: true);

        var read = EngineProfile.ReadRecord(EngineProfile.WriteRecord(record));
        Assert.Equal(record.Refused, read.Refused);
        Assert.True(read.BarShown);

        Assert.Equal(new EngineRecord([], false).Refused, EngineProfile.ReadRecord("not json").Refused);
        Assert.False(EngineProfile.ReadRecord(null).BarShown);
    }

    [Theory]
    [InlineData("ncennffkjdiamlpmcbajkmaiiiddgioo", true)]
    [InlineData("ncennffkjdiamlpmcbajkmaiiiddgio", false)]
    [InlineData("NCENNFFKJDIAMLPMCBAJKMAIIIDDGIOO", false)]
    [InlineData("zcennffkjdiamlpmcbajkmaiiiddgioo", false)]
    public void An_extension_id_is_32_letters_from_a_to_p(string text, bool id) =>
        Assert.Equal(id, EngineProfile.IsExtensionId(text));

    [Fact]
    public void The_home_is_found_from_the_profile_folder_the_shell_hands_over()
    {
        var home = Path.Combine(Path.GetTempPath(), "daoris-home");

        Assert.Equal(home, EngineBrowser.HomeOf(EngineBrowser.ProfileFolder(home)));
    }
}
