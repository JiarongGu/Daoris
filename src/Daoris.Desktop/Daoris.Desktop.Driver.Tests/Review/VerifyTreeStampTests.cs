using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A check's verification tree is prepared again whenever any input its preparation reads moves: the stamp changes with a dependency
/// key and each declared local input's presence and content, so a new input cannot be left out silently (a tree that kept a stale stamp
/// lacked the local file the files stage copies). A <c>regenerate</c> command is none: a check runs it before every gate.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class VerifyTreeStampTests : IDisposable
{
    private readonly string _scratch = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "verify-stamp", Guid.NewGuid().ToString("N")[..8]);
    private string Home => Path.Combine(_scratch, "home");
    private string Root => Path.Combine(_scratch, "engine");

    public void Dispose() => GitSeed.Discard(_scratch);

    private async Task WriteAsync(string name, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(Root, name))!);
        await File.WriteAllTextAsync(Path.Combine(Root, name), text);
    }

    private async Task CommitAsync()
    {
        await GitAsync(Root, "add", "-A", "-f");
        await GitAsync(Root, "commit", "-q", "-m", "step");
    }

    private async Task<string> StampAsync() =>
        await VerifyPreparation.Real.StampAsync(Home, Root, Root, "engine", CancellationToken.None) ?? throw new InvalidOperationException("git said no stamp");

    private static string Gates(string regenerate, string localInputs) =>
        $$"""{ "gates": [ { "name": "a", "run": "x", "regenerate": "{{regenerate}}" } ], "localInputs": [{{localInputs}}] }""";

    [Fact]
    public async Task The_stamp_moves_with_each_input_the_preparation_reads()
    {
        Directory.CreateDirectory(Root);
        await GitAsync(Root, "init", "-q", "-b", "main");
        await GitAsync(Root, "config", "user.email", "test@example.com");
        await GitAsync(Root, "config", "user.name", "Test");
        await File.WriteAllTextAsync(Path.Combine(Root, ".gitignore"), "local/\n");
        await WriteAsync("package.json", """{ "name": "x", "version": "1.0.0" }""");
        await WriteAsync("package-lock.json", """{ "name": "x", "lockfileVersion": 3, "packages": {} }""");
        await WriteAsync("daoris.gates.json", Gates("node a.cjs", ""));
        await CommitAsync();
        var seen = new List<string> { await StampAsync() };

        async Task MovedBy(string what, Func<Task> change)
        {
            await change();
            var stamp = await StampAsync();
            Assert.False(seen.Contains(stamp), $"the stamp did not move when {what}");
            seen.Add(stamp);
        }

        await WriteAsync("daoris.gates.json", Gates("node b.cjs", ""));
        await CommitAsync();
        Assert.Equal(seen[0], await StampAsync());
        await MovedBy("a local input was declared", async () => { await WriteAsync("daoris.gates.json", Gates("node b.cjs", "\"local/one.props\"")); await CommitAsync(); });
        await MovedBy("the declared local input appeared", () => WriteAsync("local/one.props", "first"));
        await MovedBy("the declared local input's content changed", () => WriteAsync("local/one.props", "second"));
        await MovedBy("a dependency key changed", async () =>
        {
            await WriteAsync("package-lock.json", """{ "name": "x", "lockfileVersion": 3, "packages": { "": { "name": "x", "dependencies": { "y": "1.0.0" } } } }""");
            await CommitAsync();
        });
    }
}
