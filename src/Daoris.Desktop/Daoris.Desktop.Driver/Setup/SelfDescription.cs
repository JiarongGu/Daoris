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

    /// <summary>
    /// A heading that says how, not what — and, as the top one, names nothing either: a generator's
    /// "Getting Started with …" and an underlined "Installation" both stood where a name goes.
    /// </summary>
    private static readonly Regex HowTo = new(
        @"^(installation|install|set ?up|getting started|usage|prerequisites|requirements|build|building|development|running|run|how to)\b",
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
        var fence = new Fence();
        var describing = true;
        var topSeen = false;

        // One rule for a marked heading and an underlined one. Only the FIRST top-level heading may
        // name the repository: a hosted template's later top-level sections (Getting Started,
        // Contribute) are its boilerplate, and "Introduction" introduces rather than names.
        bool Heading(string heading, int level)
        {
            if (Settle(paragraph, ref describing) is { } found)
            {
                said = found;
                return true;
            }

            if (level == 1 && !topSeen)
            {
                topSeen = true;
                describing = !HowTo.IsMatch(heading);
                if (describing && !Describes.IsMatch(heading)) title = heading;
            }
            else
            {
                describing = Describes.IsMatch(heading);
            }

            return false;
        }

        foreach (var raw in Head(file).Split('\n'))
        {
            var line = raw.Trim();
            if (fence.Holds(line)) continue;

            if (line.Length == 0)
            {
                if (Settle(paragraph, ref describing) is { } found) { said = found; break; }
                continue;
            }

            if (line.StartsWith('#'))
            {
                if (Heading(line.TrimStart('#').Trim(), line.StartsWith("# ", StringComparison.Ordinal) ? 1 : 2)) break;
                continue;
            }

            // An underline makes the one line above it a heading (`===` top-level, `---` below).
            if (line.Length >= 2 && (line.All(c => c == '=') || line.All(c => c == '-')) && paragraph.Count == 1)
            {
                var heading = paragraph[0];
                paragraph.Clear();
                if (Heading(heading, line[0] == '=' ? 1 : 2)) break;
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

        said ??= Settle(paragraph, ref describing);
        return (title, said) switch
        {
            (null, null) => null,
            (null, { } only) => Cut(only),
            ({ } heading, null) => Cut(heading),
            ({ } heading, { } both) => Cut($"{heading} — {both}"),
        };
    }

    /// <summary>A gathered paragraph, when it says something; cleared either way.</summary>
    /// <param name="describing">
    /// Set false by a paragraph that opens with a step: it says how, as a heading of that word would,
    /// and so does the rest of its section — the first real README of that shape was steps to the end.
    /// </param>
    private static string? Settle(List<string> paragraph, ref bool describing)
    {
        if (paragraph.Count == 0) return null;
        var text = Regex.Replace(Link.Replace(string.Join(" ", paragraph), "$1"), @"\s+", " ")
            .Replace("**", "", StringComparison.Ordinal).Replace("__", "", StringComparison.Ordinal)
            .Trim().Trim('*', '_').Trim();
        paragraph.Clear();
        if (HowTo.IsMatch(text))
        {
            describing = false;
            return null;
        }

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

    /// <summary>
    /// Which lines of a README a fenced code block holds, read a line at a time as CommonMark reads a fence (ORIENT2h6):
    /// its text is never a heading, an underline or a paragraph.
    /// </summary>
    /// <remarks>
    /// <para>A run of three or more backticks or tildes after any indent opens a fence, and only a run of the same
    /// character at least as long, with nothing after it but spaces, closes it. A backtick run with a backtick later on
    /// its line is code inline and opens nothing. A fence never closed holds the rest of the file. 🔴 The first cut
    /// toggled on any line opening with three of either, so a four-backtick fence closed on the example it quoted and
    /// the example's heading ended the description.</para>
    ///
    /// <para>The driver's half of a TWIN with the CLI's <c>markdownFence</c> (<c>document.ts</c>) and the tools'
    /// <c>fenced</c> (<c>tools/doc-duplicates.mjs</c>), sharing no code: all three are held to the CLI's
    /// <c>test/fixtures/fence-cases.json</c>, row for row. The service's <c>MarkdownFence</c> is the same rule, held to
    /// the tools' by the decisions digest's note table.</para>
    /// </remarks>
    internal sealed class Fence
    {
        private char _marker;
        private int _length;

        /// <summary>
        /// Reads the next line: whether a fence holds it, its opening and closing lines included. Called once for every
        /// line, in order, since what closes a fence depends on what opened it.
        /// </summary>
        public bool Holds(string line)
        {
            var text = line.TrimStart();
            var (marker, length) = Run(text);

            if (_marker == '\0')
            {
                if (length < 3) return false;
                if (marker == '`' && text.IndexOf('`', length) >= 0) return false;
                _marker = marker;
                _length = length;
                return true;
            }

            if (marker == _marker && length >= _length && text[length..].Trim().Length == 0) _marker = '\0';
            return true;
        }

        /// <summary>The backticks or tildes a line opens with, and how many; none for a line that opens with neither.</summary>
        private static (char Marker, int Length) Run(string text)
        {
            if (text.Length == 0 || text[0] is not ('`' or '~')) return ('\0', 0);
            var length = 1;
            while (length < text.Length && text[length] == text[0]) length++;
            return (text[0], length);
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
