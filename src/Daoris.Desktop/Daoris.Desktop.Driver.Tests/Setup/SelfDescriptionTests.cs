using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What a repository that declared nothing says about ITSELF (D77): read from its own committed
/// files — never written — so an intake can decide a workspace nobody adopted. The first real one was
/// twenty-nine repositories and not one declaration, and every ask parked.
/// </summary>
public sealed class SelfDescriptionTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-self-" + Guid.NewGuid().ToString("N")[..8]);

    public SelfDescriptionTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private void File(string name, string content)
    {
        var path = Path.Combine(_root, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllText(path, content);
    }

    [Fact]
    public void A_readme_says_what_it_is_in_its_first_paragraph_under_its_title()
    {
        File("README.md", """
            # Parcel Tracking

            [![build](https://ci.example/badge.svg)](https://ci.example)
            <img src="logo.png">

            The tracking backend: [parcels](https://example.com/parcels), routes and the
            events that follow them.

            ## Setup

            Run it.
            """);

        var described = SelfDescription.Read(_root)!;

        Assert.Equal("Parcel Tracking — The tracking backend: parcels, routes and the events that follow them.",
            described.Summary);
        Assert.Equal("README.md", described.Source);
    }

    /// <summary>
    /// A generator's paragraph says which tool made the folder, not what it is for — the first real
    /// workspace had four of them. The next paragraph is read instead, and none is said as none.
    /// </summary>
    [Theory]
    [InlineData("This project was generated with [Angular CLI](https://github.com/angular/angular-cli) version 12.2.11.")]
    [InlineData("This project was bootstrapped with [Create React App](https://github.com/facebook/create-react-app).")]
    [InlineData("TODO: Give a short introduction of your project.")]
    [InlineData("https://learn.example.com/terraform/get-started")]
    public void A_paragraph_that_says_nothing_about_the_repository_is_passed_over(string boilerplate)
    {
        File("README.md", $"# LedgerUi\n\n{boilerplate}\n\n## Development server\n\nRun `ng serve`.\n");

        var described = SelfDescription.Read(_root)!;

        Assert.Equal("LedgerUi", described.Summary);
    }

    /// <summary>
    /// Three shapes the first real workspace's READMEs took, each read wrongly by the first cut: a
    /// hosted template whose later top-level sections are its own boilerplate, a heading underlined
    /// rather than marked, and a generator's title.
    /// </summary>
    [Fact]
    public void A_hosted_templates_later_sections_say_nothing_and_its_first_is_no_title()
    {
        File("README.md", """
            # Introduction
            TODO: Give a short introduction of your project.

            # Getting Started
            TODO: Guide users through getting your code up and running.

            # Contribute
            If you want to learn more about creating good readme files then refer the following guidelines.
            """);

        Assert.Null(SelfDescription.Read(_root));
    }

    [Fact]
    public void An_underlined_heading_is_a_heading()
    {
        File("README.md", "Installation\n============\n\nFollow the steps to run the project.\n");

        Assert.Null(SelfDescription.Read(_root)?.Summary);
    }

    [Fact]
    public void A_readme_that_opens_with_its_steps_says_how_not_what()
    {
        File("README.md", "Installation\n**Follow the steps to run the project**\n* Clone the repository\n* Run `npm install`\n\n"
            + "To check the versions, run `node -v`.\n");

        Assert.Null(SelfDescription.Read(_root)?.Summary);
    }

    [Fact]
    public void A_generators_title_is_no_title()
    {
        File("README.md", "# Getting Started with Create React App\n\nThis project was bootstrapped with Create React App.\n\n## Available Scripts\n\nnpm start\n");

        Assert.Null(SelfDescription.Read(_root)?.Summary);
    }

    [Fact]
    public void An_introduction_heading_introduces_rather_than_names()
    {
        File("README.md", "# Introduction\n\nThe pipelines that sort archived device messages into the database.\n");

        Assert.Equal("The pipelines that sort archived device messages into the database.", SelfDescription.Read(_root)!.Summary);
    }

    /// <summary>
    /// A README's fences, read as CommonMark reads one (ORIENT2h6): a fence's lines are skipped, and only a fence's. The
    /// first cut toggled on any line opening with three backticks or tildes, so a longer fence closed on the example it
    /// quoted, a tilde fence on a backtick run inside it, and inline code at a line's start swallowed the rest.
    /// </summary>
    [Theory]
    [InlineData("a four-backtick fence quoting a heading", "# Parcel Tracking\n\n````markdown\n```\n## Setup\n```\n````\n\nThe tracking backend.\n",
        "Parcel Tracking — The tracking backend.")]
    [InlineData("a fence quoting an underlined heading", "# Parcel Tracking\n\n````\n```\nInstallation\n---\n```\n````\n\nThe tracking backend.\n",
        "Parcel Tracking — The tracking backend.")]
    [InlineData("a tilde fence, a backtick run inside it", "# Parcel Tracking\n\n~~~\n```\n## Setup\n~~~\n\nThe tracking backend.\n",
        "Parcel Tracking — The tracking backend.")]
    [InlineData("inline code at a line's start", "# Parcel Tracking\n\n```track``` follows parcels and their routes.\n",
        "Parcel Tracking — ```track``` follows parcels and their routes.")]
    public void A_fence_holds_only_its_own_lines(string why, string readme, string summary)
    {
        File("README.md", readme);

        var said = SelfDescription.Read(_root)?.Summary;
        Assert.True(said == summary, $"{why}: {said}");
    }

    /// <summary>
    /// The driver's half of a TWIN (ORIENT2h6, <c>.claude/knowledge/twins.md</c>) with the CLI's <c>markdownFence</c> and
    /// the tools' <c>fenced</c>: all three read the CLI's <c>test/fixtures/fence-cases.json</c>, row for row, each case's
    /// lines and the lines a fence holds, from 1.
    /// </summary>
    [Fact]
    public void Each_line_is_fenced_as_the_shared_table_reads_it()
    {
        using var table = JsonDocument.Parse(System.IO.File.ReadAllText(
            Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Cli", "test", "fixtures", "fence-cases.json")));
        var cases = table.RootElement.GetProperty("cases").EnumerateArray().ToList();

        foreach (var row in cases)
        {
            var why = row.GetProperty("why").GetString();
            var fence = new SelfDescription.Fence();
            var held = row.GetProperty("lines").EnumerateArray()
                .Select((line, at) => (Held: fence.Holds(line.GetString()!), Line: at + 1))
                .Where(line => line.Held).Select(line => line.Line).ToList();
            var expected = row.GetProperty("fenced").EnumerateArray().Select(line => line.GetInt32()).ToList();
            Assert.True(expected.SequenceEqual(held), $"{why}: {string.Join(", ", held)}");
        }

        Assert.Contains(cases, row => row.GetProperty("fenced").GetArrayLength() == 0);
        Assert.Contains(cases, row => row.GetProperty("fenced").GetArrayLength() > 0);
    }

    [Fact]
    public void A_long_paragraph_is_cut_at_a_word_and_says_so()
    {
        File("README.md", "# X\n\n" + string.Join(" ", Enumerable.Repeat("tracking", 60)) + "\n");

        var summary = SelfDescription.Read(_root)!.Summary!;

        Assert.True(summary.Length <= SelfDescription.MaxSummary + 1, summary.Length.ToString());
        Assert.EndsWith("…", summary);
        Assert.DoesNotContain("trackin…", summary);
    }

    [Fact]
    public void With_no_readme_the_package_description_speaks()
    {
        File("package.json", """{ "name": "forms-fe", "description": "The inspection forms.", "dependencies": { "react": "18" } }""");

        var described = SelfDescription.Read(_root)!;

        Assert.Equal("The inspection forms.", described.Summary);
        Assert.Equal("package.json", described.Source);
        Assert.Contains("React", described.Stack);
    }

    /// <summary>What it is built with is often the deciding word — a UI change and an API change are different repositories.</summary>
    [Fact]
    public void Its_stack_is_read_from_the_files_at_its_top()
    {
        File("angular.json", "{}");
        File("package.json", """{ "dependencies": { "@angular/core": "19" } }""");
        File("Backend.sln", "");
        File("host.json", "{}");
        File("main.tf", "");

        var stack = SelfDescription.Read(_root)!.Stack;

        Assert.Equal(["Angular", ".NET", "Azure Functions", "Terraform"], stack);
    }

    [Fact]
    public void A_folder_that_says_nothing_is_null_and_a_missing_one_too()
    {
        Assert.Null(SelfDescription.Read(_root));
        Assert.Null(SelfDescription.Read(Path.Combine(_root, "no-such-folder")));
    }

    /// <summary>It reads; it never writes (D32) — the folder is exactly as it was.</summary>
    [Fact]
    public void Reading_writes_nothing()
    {
        File("README.md", "# X\n\nA thing.\n");
        var before = Directory.GetFileSystemEntries(_root, "*", SearchOption.AllDirectories).Order().ToList();

        SelfDescription.Read(_root);

        Assert.Equal(before, Directory.GetFileSystemEntries(_root, "*", SearchOption.AllDirectories).Order().ToList());
    }
}
