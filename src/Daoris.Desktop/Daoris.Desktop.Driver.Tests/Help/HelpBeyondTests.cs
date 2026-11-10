using Daoris.Desktop.Driver.Tests;

namespace Daoris.Driver.Tests;

/// <summary>
/// What an Ask Daoris turn looked at beyond its focus (`docs/2026-11-11-ask-scope-ux-design.md` §4): the places its connector noted,
/// as Daoris's doors saw them, judged against the focus the conversation held when each was noted. This machine holds every place;
/// a workspace its repositories; a repository nothing else but its own workspace's shared records, which are near.
/// </summary>
public sealed class HelpBeyondTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-help-beyond-" + Guid.NewGuid().ToString("N")[..8]);

    private static readonly RepoView[] Registry =
    [
        new("engine", true, "C:/checkouts/engine", "Games"),
        new("site", true, "C:/checkouts/site", "Games"),
        new("api", true, "C:/checkouts/api", "Work"),
    ];

    private static readonly HelpFocus Games = HelpFocus.OfWorkspace("Games");
    private static readonly HelpFocus Work = HelpFocus.OfWorkspace("Work");
    private static readonly HelpFocus Engine = HelpFocus.OfRepository("Games", "engine");
    private static readonly HelpFocus Site = HelpFocus.OfRepository("Games", "site");
    private static readonly HelpFocus Api = HelpFocus.OfRepository("Work", "api");
    private static readonly DateTimeOffset Noon = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);

    public HelpBeyondTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private void Noted(params string[] lines)
    {
        var path = HelpPlaceNotes.PathOf(_home, "h1");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.AppendAllText(path, string.Join("\n", lines) + "\n");
    }

    private static string Line(DateTimeOffset at, string act, string? workspace = null, string? repository = null, string? what = null)
    {
        var fields = new List<string> { $"\"at\":\"{at.UtcDateTime:yyyy-MM-dd'T'HH:mm:ss.fff'Z'}\"", "\"tool\":\"t\"", $"\"act\":\"{act}\"" };
        if (workspace is not null) fields.Add($"\"workspace\":\"{workspace}\"");
        if (repository is not null) fields.Add($"\"repository\":\"{repository}\"");
        if (what is not null) fields.Add($"\"what\":\"{what}\"");
        return "{" + string.Join(",", fields) + "}";
    }

    [Fact]
    public void Nothing_is_beyond_this_machine()
    {
        Assert.False(HelpBeyond.Outside(HelpFocus.Machine, Work));
        Assert.False(HelpBeyond.Outside(HelpFocus.Machine, Api));
        Assert.False(HelpBeyond.Outside(HelpFocus.Machine, HelpFocus.Machine));
    }

    [Fact]
    public void A_workspace_holds_its_repositories_and_every_other_workspace_is_beyond()
    {
        Assert.False(HelpBeyond.Outside(Games, Games));
        Assert.False(HelpBeyond.Outside(Games, Engine));
        Assert.True(HelpBeyond.Outside(Games, Work));
        Assert.True(HelpBeyond.Outside(Games, Api));
        Assert.True(HelpBeyond.Outside(Games, HelpFocus.Machine));
    }

    [Fact]
    public void A_repository_s_own_workspace_is_near_and_any_other_repository_is_beyond()
    {
        Assert.False(HelpBeyond.Outside(Engine, Engine));
        Assert.False(HelpBeyond.Outside(Engine, Games));
        Assert.True(HelpBeyond.Outside(Engine, Site));
        Assert.True(HelpBeyond.Outside(Engine, Work));
        Assert.True(HelpBeyond.Outside(Engine, Api));
        Assert.True(HelpBeyond.Outside(Engine, HelpFocus.Machine));
    }

    [Fact]
    public void Each_noted_place_beyond_the_focus_is_listed_with_its_act_and_what_it_named()
    {
        new HelpConversations(_home).Change("h1", kept => kept with { Focus = Games, Foci = [Games] });
        Noted(
            Line(Noon, "search", workspace: "Work", what: "release"),
            Line(Noon.AddSeconds(1), "search", workspace: "Games", what: "release"),
            Line(Noon.AddSeconds(2), "read", repository: "api", what: "README.md"),
            Line(Noon.AddSeconds(3), "read", repository: "engine", what: "README.md"),
            Line(Noon.AddSeconds(4), "search", workspace: "all"),
            Line(Noon.AddSeconds(5), "propose", workspace: "Work", repository: "api", what: "go"),
            "not a line",
            Line(Noon.AddSeconds(6), "read", repository: "unknown"));

        var entries = HelpBeyond.Of(_home, "h1", "Games", Registry);

        Assert.Equal(
            [("search", Work, "release"), ("read", Api, "README.md"), ("search", HelpFocus.Machine, null), ("propose", Api, "go")],
            entries.Select(entry => (entry.Act, entry.Place, entry.What)));
        Assert.Equal(Noon, entries[0].At);
    }

    [Fact]
    public void Each_place_is_judged_against_the_focus_held_when_it_was_noted()
    {
        new HelpConversations(_home).Change("h1", kept => kept with
        {
            Focus = Work,
            Foci = [Games, Work],
            Marks = [new HelpFocusMark("w2", Noon.AddMinutes(1), Games, Work, false)],
        });
        Noted(
            Line(Noon.AddSeconds(5), "search", workspace: "Work"),
            Line(Noon.AddMinutes(2), "search", workspace: "Work"),
            Line(Noon.AddMinutes(3), "search", workspace: "Games"));

        var entries = HelpBeyond.Of(_home, "h1", "Games", Registry);

        Assert.Equal([(Noon.AddSeconds(5), Work), (Noon.AddMinutes(3), Games)], entries.Select(entry => (entry.At, entry.Place)));
    }

    [Fact]
    public void A_repository_named_in_two_workspaces_is_the_one_the_focus_holds()
    {
        RepoView[] twice = [.. Registry, new("engine", true, "C:/checkouts/engine-work", "Work")];
        new HelpConversations(_home).Change("h1", kept => kept with { Focus = Games, Foci = [Games] });
        Noted(Line(Noon, "read", repository: "engine"));

        Assert.Empty(HelpBeyond.Of(_home, "h1", "Games", twice));
    }

    [Fact]
    public void A_conversation_with_no_file_of_places_has_nothing_beyond()
    {
        Assert.Empty(HelpBeyond.Of(_home, "h1", "Games", Registry));
        Assert.Empty(HelpBeyond.Of(_home, "../h1", "Games", Registry));
    }

    [Fact]
    public void An_entry_is_answered_with_its_place_spelled_as_the_page_reads_it()
    {
        var wire = new HelpBeyondEntry(Noon, "read", Api, "README.md").Wire();

        Assert.Equal("read", wire["act"]);
        Assert.Equal("README.md", wire["what"]);
        Assert.Equal(Api.Wire(), wire["place"]);
        Assert.Equal(Noon, wire["at"]);
    }
}
