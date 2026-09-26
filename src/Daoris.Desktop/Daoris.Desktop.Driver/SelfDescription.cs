using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>What a repository's own files say it is, and what it is built with.</summary>
/// <param name="Summary">Its title and first paragraph that says anything, cut to a line or two; null when none does.</param>
/// <param name="Stack">What it is built with, read from the files at its top — the words a ticket's change usually decides on.</param>
/// <param name="Source">The file the summary came from, so a reader knows it is the repository's word, not Daoris's.</param>
public sealed record RepositoryDescription(string? Summary, IReadOnlyList<string> Stack, string? Source);

/// <summary>
/// What a repository that DECLARED nothing says about itself (D77) — read from its own committed
/// files, never written, so an intake can decide a workspace nobody adopted.
/// </summary>
/// <remarks>
/// <para><b>Evidence, not a declaration.</b> A declaration is a claim someone made on purpose about
/// what a repository owns (D34); a README is whatever its authors wrote for a newcomer. The room says
/// which is which, and a declaration outranks this wherever both exist.</para>
///
/// <para><b>Read, never written</b> (D32), and small: the top of a few files, no walk of the tree —
/// an intake opens against every repository in a circle, and the first real one held twenty-nine.</para>
///
/// <para>No model reads it (D24). What a paragraph says is judged by the intake session, which is
/// where a model is; this only decides which paragraph is worth handing it.</para>
/// </remarks>
public static class SelfDescription
{
    /// <summary>How long a summary may run — a line or two in the room, for each of many repositories.</summary>
    public const int MaxSummary = 280;

    /// <summary>How much of a file is read: a README's point is at its top, and a large one is not read whole.</summary>
    private const int ReadLimit = 16 * 1024;

    private static readonly string[] Readmes = ["README.md", "readme.md", "Readme.md", "README.MD", "README"];

    /// <summary>
    /// The paragraphs that say which tool made the folder, or that nothing was written yet — the first
    /// real workspace had four generator paragraphs, and a bare link is somebody's bookmark.
    /// </summary>
    private static readonly Regex SaysNothing = new(
        @"^(this project was (generated|bootstrapped|created) with|todo\b|note\b|https?://\S+$)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// A section that describes the repository rather than how to run it. Anything under another
    /// heading — setup, a development server — is how, not what, so reading stops there.
    /// </summary>
    private static readonly Regex Describes = new(
        @"^(overview|about|introduction|description|summary|purpose|what\b)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex Link = new(@"!?\[([^\]]*)\]\([^)]*\)", RegexOptions.CultureInvariant);

    /// <summary>What <paramref name="root"/> says about itself, or null when it says nothing a reader could use.</summary>
    public static RepositoryDescription? Read(string root)
    {
        if (!Directory.Exists(root)) return null;

        var stack = Stack(root);
        var (summary, source) = FromReadme(root) is { } readme
            ? (readme, Readmes.First(name => File.Exists(Path.Combine(root, name))))
            : PackageDescription(root) is { } package
                ? (package, "package.json")
                : ((string?)null, (string?)null);

        return summary is null && stack.Count == 0 ? null : new RepositoryDescription(summary, stack, source);
    }

    private static string? FromReadme(string root)
    {
        var file = Readmes.Select(name => Path.Combine(root, name)).FirstOrDefault(File.Exists);
        if (file is null) return null;

        string? title = null;
        string? said = null;
        var paragraph = new List<string>();
        var fenced = false;
        var describing = true;

        foreach (var raw in Head(file).Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("```", StringComparison.Ordinal) || line.StartsWith("~~~", StringComparison.Ordinal))
            {
                fenced = !fenced;
                continue;
            }

            if (fenced) continue;

            if (line.Length == 0)
            {
                if (Settle(paragraph) is { } found) { said = found; break; }
                continue;
            }

            if (line.StartsWith('#'))
            {
                if (Settle(paragraph) is { } found) { said = found; break; }
                var heading = line.TrimStart('#').Trim();
                if (line.StartsWith("# ", StringComparison.Ordinal))
                {
                    title ??= heading;
                    describing = true;
                }
                else
                {
                    describing = Describes.IsMatch(heading);
                }

                continue;
            }

            if (!describing) continue;

            // A badge, an image, markup, a table, a rule: decoration, not a sentence.
            if (line.StartsWith("[![", StringComparison.Ordinal) || line.StartsWith("![", StringComparison.Ordinal)
                || line.StartsWith('<') || line.StartsWith('|') || line.All(c => c is '-' or '=' or '*' or '_'))
            {
                continue;
            }

            paragraph.Add(line.TrimStart('>', ' '));
        }

        said ??= Settle(paragraph);
        return (title, said) switch
        {
            (null, null) => null,
            (null, { } only) => Cut(only),
            ({ } heading, null) => Cut(heading),
            ({ } heading, { } both) => Cut($"{heading} — {both}"),
        };
    }

    /// <summary>A gathered paragraph, when it says something; cleared either way.</summary>
    private static string? Settle(List<string> paragraph)
    {
        if (paragraph.Count == 0) return null;
        var text = Regex.Replace(Link.Replace(string.Join(" ", paragraph), "$1"), @"\s+", " ")
            .Replace("**", "", StringComparison.Ordinal).Replace("__", "", StringComparison.Ordinal)
            .Trim().Trim('*', '_').Trim();
        paragraph.Clear();
        return text.Length == 0 || SaysNothing.IsMatch(text) ? null : text;
    }

    private static string? PackageDescription(string root)
    {
        using var package = Package(root);
        return package?.RootElement.ValueKind == JsonValueKind.Object
               && package.RootElement.TryGetProperty("description", out var description)
               && description.ValueKind == JsonValueKind.String
               && description.GetString() is { Length: > 0 } text
            ? Cut(text.Trim())
            : null;
    }

    /// <summary>
    /// What it is built with, from the files at its top only — in a fixed order, so two readings of
    /// one folder agree. Deliberately short: a word the intake can match a ticket's change against.
    /// </summary>
    private static IReadOnlyList<string> Stack(string root)
    {
        var stack = new List<string>();
        var dependencies = Dependencies(root);

        if (File.Exists(Path.Combine(root, "angular.json")) || dependencies.Contains("@angular/core")) stack.Add("Angular");
        else if (dependencies.Contains("next")) stack.Add("Next.js");
        else if (dependencies.Contains("react")) stack.Add("React");
        else if (dependencies.Contains("vue")) stack.Add("Vue");
        else if (File.Exists(Path.Combine(root, "package.json"))) stack.Add("Node");

        if (Any(root, "*.sln") || Any(root, "*.slnx") || Any(root, "*.csproj")) stack.Add(".NET");
        if (File.Exists(Path.Combine(root, "host.json"))) stack.Add("Azure Functions");
        if (Any(root, "*.tf")) stack.Add("Terraform");
        if (File.Exists(Path.Combine(root, "build.gradle")) || File.Exists(Path.Combine(root, "build.gradle.kts"))) stack.Add("Gradle");
        if (File.Exists(Path.Combine(root, "pyproject.toml")) || File.Exists(Path.Combine(root, "requirements.txt"))) stack.Add("Python");
        if (File.Exists(Path.Combine(root, "go.mod"))) stack.Add("Go");
        if (File.Exists(Path.Combine(root, "Cargo.toml"))) stack.Add("Rust");
        return stack;
    }

    private static HashSet<string> Dependencies(string root)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        using var package = Package(root);
        if (package?.RootElement.ValueKind != JsonValueKind.Object) return names;

        foreach (var block in new[] { "dependencies", "devDependencies" })
        {
            if (package.RootElement.TryGetProperty(block, out var listed) && listed.ValueKind == JsonValueKind.Object)
            {
                foreach (var dependency in listed.EnumerateObject()) names.Add(dependency.Name);
            }
        }

        return names;
    }

    /// <summary>The package manifest, or null when there is none or it will not parse — its own tooling says why.</summary>
    private static JsonDocument? Package(string root)
    {
        var file = Path.Combine(root, "package.json");
        if (!File.Exists(file)) return null;
        try
        {
            return JsonDocument.Parse(Head(file));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool Any(string root, string pattern)
    {
        try
        {
            return Directory.EnumerateFiles(root, pattern, SearchOption.TopDirectoryOnly).Any();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>The top of a file, LF only — a README's point is at its top.</summary>
    private static string Head(string file)
    {
        try
        {
            using var stream = File.OpenRead(file);
            var buffer = new byte[Math.Min(ReadLimit, (int)Math.Min(stream.Length, ReadLimit))];
            var read = stream.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false);
            return Encoding.UTF8.GetString(buffer, 0, read).Replace("\r\n", "\n").TrimStart('﻿');
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return "";
        }
    }

    /// <summary>Cut at a word, marked — never mid-word, which reads as a typo in somebody else's text.</summary>
    private static string Cut(string text)
    {
        if (text.Length <= MaxSummary) return text;
        var cut = text.LastIndexOf(' ', MaxSummary);
        return (cut > 0 ? text[..cut] : text[..MaxSummary]).TrimEnd(' ', ',', ';', ':') + "…";
    }
}
