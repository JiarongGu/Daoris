using System.Text.Json;

namespace Daoris.Service.Http.Tests;

/// <summary>
/// <c>POST /api/registry/{repository}/excludes</c>, the person's rewrite of a folder department's excludes once it is registered: the
/// whole list is set (blank globs dropped, each glob trimmed and kept once), an empty list clears, a repository that is not a folder or
/// not registered is refused in its own words with the row unchanged, and a call without the key is refused.
/// </summary>
public sealed class FolderExcludesHostTests(KeyedHost host) : IClassFixture<KeyedHost>
{
    private const string Key = KeyedHost.Key;

    private static JsonElement Row(Answer registry, string name) =>
        registry.Json.EnumerateArray().Single(row => row.GetProperty("repository").GetString() == name);

    private async Task<string[]> ExcludesOf(string name) =>
        [.. Row(await host.GetAsync("/api/registry"), name).GetProperty("excludes").EnumerateArray().Select(glob => glob.GetString()!)];

    /// <summary>A folder registered beside the seeded repositories, with one exclude of its own.</summary>
    private async Task Folder(string name)
    {
        var beside = Path.GetDirectoryName(Row(await host.GetAsync("/api/registry"), "Keeper").GetProperty("root").GetString())!;
        Assert.Equal(200, (await host.PostAsync("/api/registry", new
        {
            repository = name, root = Path.Combine(beside, name), kind = "folder", excludes = new[] { "build/**" },
        }, person: Key)).Status);
    }

    [Fact]
    public async Task The_whole_list_is_set_and_an_empty_one_clears()
    {
        await Folder("Scratch");

        var set = await host.PostAsync("/api/registry/Scratch/excludes", new { excludes = new[] { " *.log ", "", "tmp/**", "*.log" } }, person: Key);

        Assert.Equal(200, set.Status);
        Assert.Equal(["*.log", "tmp/**"], await ExcludesOf("Scratch"));
        Assert.Equal(200, (await host.PostAsync("/api/registry/Scratch/excludes", new { excludes = Array.Empty<string>() }, person: Key)).Status);
        Assert.Empty(await ExcludesOf("Scratch"));
    }

    [Fact]
    public async Task A_git_repository_and_an_unregistered_name_are_refused_and_nothing_changes()
    {
        var beside = Path.GetDirectoryName(Row(await host.GetAsync("/api/registry"), "Keeper").GetProperty("root").GetString())!;
        Assert.Equal(200, (await host.PostAsync("/api/registry", new
        {
            repository = "Gitted", root = Path.Combine(beside, "Gitted"), kind = "git",
        }, person: Key)).Status);

        var git = await host.PostAsync("/api/registry/Gitted/excludes", new { excludes = new[] { "*.log" } }, person: Key);
        Assert.Equal(409, git.Status);
        Assert.Contains("folder", git.Error);
        Assert.Empty(await ExcludesOf("Gitted"));

        var missing = await host.PostAsync("/api/registry/Nowhere/excludes", new { excludes = new[] { "*.log" } }, person: Key);
        Assert.Equal(404, missing.Status);
    }

    /// <summary>D284 point 2: an agent's keyless call rewrites a folder's excludes as the person's does.</summary>
    [Fact]
    public async Task A_call_without_the_key_rewrites_them_too()
    {
        await Folder("Unkeyed");

        Assert.Equal(200, (await host.PostAsync("/api/registry/Unkeyed/excludes", new { excludes = new[] { "*.log" } })).Status);
        Assert.Equal(["*.log"], await ExcludesOf("Unkeyed"));
    }
}
