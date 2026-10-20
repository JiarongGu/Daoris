using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// HELPMODE1c4 against the real git and a real link (the stand-in half is <see cref="HelpCheckoutsTests"/>): what git itself says it
/// ignores is refused, a file in an ignored folder too; a listing leaves it out; <c>git status</c> and <c>git branch --list</c>
/// answer; and a link inside the checkout leading out of it is refused, a junction on Windows, which needs no privilege.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class HelpCheckoutsGitTests : IAsyncLifetime
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-help-checkouts-git-" + Guid.NewGuid().ToString("N")[..8]);

    private string Root => Path.Combine(_home, "checkouts", "engine");

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(Path.Combine(Root, "src"));
        Directory.CreateDirectory(Path.Combine(Root, "local"));
        Directory.CreateDirectory(Path.Combine(_home, "outside"));
        await File.WriteAllTextAsync(Path.Combine(Root, ".gitignore"), "local/\n.env\nbin/\n");
        await File.WriteAllTextAsync(Path.Combine(Root, "README.md"), "# Engine\n");
        await File.WriteAllTextAsync(Path.Combine(Root, "src", "Game.cs"), "class Game {}\n");
        await File.WriteAllTextAsync(Path.Combine(Root, "local", "notes.md"), "kept private\n");
        await File.WriteAllTextAsync(Path.Combine(Root, ".env"), "TOKEN=secret\n");
        await File.WriteAllTextAsync(Path.Combine(_home, "outside", "secret.txt"), "outside the checkout\n");
        Assert.Equal(0, (await GitFixture.RunAsync(Root, "init", "-q", "-b", "main")).ExitCode);
        await GitFixture.RunAsync(Root, "add", "-A");
        Assert.Equal(0, (await GitFixture.RunAsync(Root, "-c", "user.name=t", "-c", "user.email=t@example.invalid", "commit", "-q", "-m", "first")).ExitCode);
        await GitFixture.RunAsync(Root, "branch", "feature");
    }

    public Task DisposeAsync()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        return Task.CompletedTask;
    }

    private HousekeepingLedger Ledger()
    {
        new HelpWired(_home).Keep("h1", new HelpReceipt
        {
            At = DateTimeOffset.UtcNow, Harness = "codex-acp", Folder = HelpRoom.PathOf(_home), Checkouts = [new HelpRead("engine", "default", Root)],
        });
        return new HousekeepingLedger { Records = [("h1", HelpRoom.Repository, "default", null)], Registry = [("engine", "default", Root)] };
    }

    private Task<CheckoutAnswer> AnswerAsync(string operation, string? path = null) =>
        new HelpCheckouts(_home, Ledger().Client).AnswerAsync(
            new SessionRequest("h1", SessionMove.Read, DateTimeOffset.UtcNow)
            {
                By = RequestDoor.Help, Key = "0123456789ab", Operation = operation, Repository = "engine", Path = path,
            },
            CancellationToken.None);

    /// <summary>🔴 What git ignores is refused by git's own answer, a file in an ignored folder and an ignored file alike; a tracked file is read.</summary>
    [Fact]
    public async Task What_git_ignores_is_refused_and_a_tracked_file_is_read()
    {
        Assert.Equal(CheckoutAnswer.Ignored, (await AnswerAsync(HelpCheckouts.Read, "local/notes.md")).Code);
        Assert.Equal(CheckoutAnswer.Ignored, (await AnswerAsync(HelpCheckouts.Read, ".env")).Code);
        Assert.Equal(CheckoutAnswer.Ignored, (await AnswerAsync(HelpCheckouts.Read, "local")).Code);

        var read = await AnswerAsync(HelpCheckouts.Read, "src/Game.cs");
        Assert.Equal((true, "class Game {}\n"), (read.Done, read.Text));
    }

    /// <summary>A listing leaves out git's own folder and what git ignores.</summary>
    [Fact]
    public async Task A_listing_leaves_out_what_git_ignores()
    {
        var top = await AnswerAsync(HelpCheckouts.Read);

        Assert.True(top.Done);
        Assert.Equal(["src/", ".gitignore", "README.md"], top.Text!.Split('\n', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary><c>git status</c> and <c>git branch --list</c> answer from the checkout.</summary>
    [Fact]
    public async Task Status_and_branches_answer()
    {
        var status = await AnswerAsync(HelpCheckouts.Status);
        var branches = await AnswerAsync(HelpCheckouts.Branches);

        Assert.True(status.Done, status.Said);
        Assert.Contains("main", status.Text);
        Assert.True(branches.Done, branches.Said);
        Assert.Contains("feature", branches.Text);
    }

    /// <summary>🔴 A link inside the checkout leading out of it is refused, and nothing beyond it is read.</summary>
    [Fact]
    public async Task A_link_leading_out_of_the_checkout_is_refused()
    {
        await JunctionAsync(Path.Combine(Root, "linked"), Path.Combine(_home, "outside"));

        var through = await AnswerAsync(HelpCheckouts.Read, "linked/secret.txt");
        var at = await AnswerAsync(HelpCheckouts.Read, "linked");

        Assert.Equal((false, CheckoutAnswer.Escapes, null), (through.Done, through.Code, through.Text));
        Assert.Equal(CheckoutAnswer.Escapes, at.Code);
        Assert.DoesNotContain("outside the checkout", through.Said);
    }

    /// <summary>A link at <paramref name="link"/> to <paramref name="target"/>: a junction on Windows, which needs no privilege.</summary>
    private static async Task JunctionAsync(string link, string target)
    {
        if (!OperatingSystem.IsWindows())
        {
            Directory.CreateSymbolicLink(link, target);
            return;
        }

        var info = new System.Diagnostics.ProcessStartInfo("cmd.exe") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "/c", "mklink", "/J", link, target }) info.ArgumentList.Add(argument);
        using var process = System.Diagnostics.Process.Start(info)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, $"mklink failed: {await output} {await error}");
    }
}
