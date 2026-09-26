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
