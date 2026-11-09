using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Every way the build's banned-name error could be quieted or switched off, each red where it stands: a <c>#pragma</c> naming it or
/// naming no id, a <c>SuppressMessage</c> naming it or its category, a <c>NoWarn</c> or <c>WarningsNotAsErrors</c> naming it, the
/// analyzers switched off, a project that ships without the analyzer, a second <c>.editorconfig</c> or any <c>.globalconfig</c>, the
/// rule's category or every rule's severity set, and the one <c>.editorconfig</c> not the root or not making it an error. Files are
/// read as (path under the workspace, text), so a test plants one beside the real ones.
/// </summary>
internal static partial class BoundaryConfig
{
    internal const string Rule = "RS0030";

    /// <summary>The one <c>.editorconfig</c>, under the workspace.</summary>
    internal const string EditorConfig = "src/Daoris.Desktop/.editorconfig";

    /// <summary>The desktop tree's shared props, which give every shipping project the analyzer and the list.</summary>
    internal const string SharedProps = "src/Daoris.Desktop/Directory.Build.props";

    /// <summary>The store's folder and the home's table, under the desktop tree: the two places a banned name is the store's own.</summary>
    internal static readonly ImmutableArray<string> Places = ["Daoris.Desktop.Driver/Workplace/Declarations/**", "Daoris.Desktop.Driver/Platform/DaorisHome.cs"];

    /// <summary>Whether <paramref name="file"/>, under the desktop tree, is in one of the two places.</summary>
    internal static bool InPlace(string file) =>
        file.StartsWith("Daoris.Desktop.Driver/Workplace/Declarations/", StringComparison.Ordinal) || file == "Daoris.Desktop.Driver/Platform/DaorisHome.cs";

    /// <summary>The files the census reads: the desktop tree's sources, project files and analyzer configurations, and the workspace's above it.</summary>
    internal static IEnumerable<(string Path, string Text)> Files()
    {
        var root = WorkspaceRoot.Folder;
        var desktop = Directory.EnumerateFiles(DesktopCompilations.Desktop, "*", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(DesktopCompilations.Desktop, path).Replace('\\', '/').Split('/').Any(part => part is "bin" or "obj"));
        var above = new[] { root, Path.Combine(root, "src") }.SelectMany(folder => Directory.EnumerateFiles(folder));
        return desktop.Concat(above)
            .Where(path => Path.GetExtension(path) is ".cs" or ".csproj" or ".props" or ".targets" or ".editorconfig" or ".globalconfig")
            .Select(path => (Path.GetRelativePath(root, path).Replace('\\', '/'), File.ReadAllText(path)));
    }

    /// <summary>What quiets or switches off the error among <paramref name="files"/>, each said with where it stands.</summary>
    internal static List<string> Problems(IEnumerable<(string Path, string Text)> files)
    {
        var problems = new List<string>();
        var read = files.ToList();
        foreach (var (path, text) in read)
        {
            switch (Path.GetExtension(path))
            {
                case ".cs" when DesktopCompilations.Shipping.Any(project => path.StartsWith($"src/Daoris.Desktop/{project}/", StringComparison.Ordinal)):
                    foreach (Match pragma in Pragma().Matches(text))
                    {
                        var ids = pragma.Groups["ids"].Value.Split("//")[0].Trim();
                        if (ids.Length == 0) problems.Add($"{path} quiets every warning with a #pragma naming no id");
                        else if (ids.Contains(Rule, StringComparison.OrdinalIgnoreCase)) problems.Add($"{path} quiets {Rule} with a #pragma");
                    }

                    if (Suppression().IsMatch(text)) problems.Add($"{path} quiets {Rule} with a SuppressMessage");
                    break;
                case ".csproj" or ".props" or ".targets":
                    if (Listed("NoWarn").IsMatch(text)) problems.Add($"{path} quiets {Rule} with NoWarn");
                    if (Listed("WarningsNotAsErrors").IsMatch(text)) problems.Add($"{path} keeps {Rule} from failing the build with WarningsNotAsErrors");
                    if (Off().IsMatch(text)) problems.Add($"{path} switches the analyzers off");
                    if (ConfigItem().IsMatch(text)) problems.Add($"{path} hands the build an analyzer configuration of its own");
                    if (Shipping(path) && TestProject().IsMatch(text))
                        problems.Add($"{path} ships and is marked a test project, so it builds without the analyzer");
                    if (Shipping(path) && text.Contains("BannedApiAnalyzers", StringComparison.Ordinal))
                        problems.Add($"{path} ships and names the analyzer itself, which only the shared props gives it");
                    if (Unshared().IsMatch(text)) problems.Add($"{path} does not import the desktop tree's shared props");
                    break;
                case ".editorconfig" when path != EditorConfig:
                    problems.Add($"{path} is a second .editorconfig");
                    break;
                case ".globalconfig":
                    problems.Add($"{path} is a .globalconfig");
                    break;
            }
        }

        foreach (var project in DesktopCompilations.Shipping.Where(project => !read.Any(each => each.Path == $"src/Daoris.Desktop/{project}/{project}.csproj")))
            problems.Add($"{project} ships and its project is not found");
        var shared = read.FirstOrDefault(each => each.Path == SharedProps).Text ?? "";
        if (!SharedAnalyzer().IsMatch(shared) || !shared.Contains("Daoris.Desktop.Driver/BannedSymbols.txt", StringComparison.Ordinal))
            problems.Add($"{SharedProps} does not give every shipping project the analyzer and the list");
        var config = read.FirstOrDefault(each => each.Path == EditorConfig).Text;
        if (config is null) problems.Add($"{EditorConfig} is gone");
        else problems.AddRange(Sections(config).Problems);
        return problems;
    }

    /// <summary>
    /// The one <c>.editorconfig</c>'s sections that set the rule to none, each by its glob under the desktop tree, the two places among
    /// them; and what in it quiets the rule other than a section: a severity for the rule's category or for every rule, the root not
    /// set, or the rule not an error for every source.
    /// </summary>
    internal static (ImmutableArray<string> Quiet, List<string> Problems) Sections(string text)
    {
        var (root, sections) = Read(text);
        var problems = new List<string>();
        if (!root) problems.Add($"{EditorConfig} is not the root, so a file above the tree reaches its build");
        var quiet = new List<string>();
        foreach (var section in sections)
        {
            foreach (var key in section.Properties.Keys.Where(key => key.StartsWith("dotnet_analyzer_diagnostic.", StringComparison.Ordinal)
                         && (key.Contains("category-apidesign", StringComparison.OrdinalIgnoreCase) || key == "dotnet_analyzer_diagnostic.severity")))
                problems.Add($"{EditorConfig} [{section.Name}] sets {key}");
            if (!section.Properties.TryGetValue($"dotnet_diagnostic.{Rule.ToLowerInvariant()}.severity", out var severity)) continue;
            if (section.Name == "*.cs" && severity == "error") continue;
            if (severity == "none") quiet.Add(section.Name);
            else problems.Add($"{EditorConfig} [{section.Name}] sets {Rule} to {severity}");
        }

        if (!sections.Any(section => section.Name == "*.cs"
                && section.Properties.TryGetValue($"dotnet_diagnostic.{Rule.ToLowerInvariant()}.severity", out var all) && all == "error"))
            problems.Add($"{EditorConfig} does not make {Rule} an error for every source");
        foreach (var place in Places.Where(place => !quiet.Contains(place))) problems.Add($"{EditorConfig} has no section for {place}");
        return ([.. quiet.Where(section => !Places.Contains(section))], problems);
    }

    // An editorconfig as the compiler reads one: a preamble, then sections by their header, keys without regard to case.
    private static (bool Root, List<(string Name, Dictionary<string, string> Properties)> Sections) Read(string text)
    {
        var root = false;
        var sections = new List<(string Name, Dictionary<string, string> Properties)>();
        foreach (var line in text.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0 && line[0] is not ('#' or ';')))
        {
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                sections.Add((line[1..^1], new(StringComparer.Ordinal)));
                continue;
            }

            var equals = line.IndexOf('=');
            if (equals < 0) continue;
            var key = line[..equals].Trim().ToLowerInvariant();
            var value = line[(equals + 1)..].Trim().ToLowerInvariant();
            if (sections.Count == 0) root |= key == "root" && value == "true";
            else sections[^1].Properties[key] = value;
        }

        return (root, sections);
    }

    private static bool Shipping(string path) => DesktopCompilations.Shipping.Any(project => path == $"src/Daoris.Desktop/{project}/{project}.csproj");

    private static Regex Listed(string property) =>
        new($@"{property}(?:\s*>|\s*=\s*"")[^<""]*{Rule}", RegexOptions.IgnoreCase);

    [GeneratedRegex(@"^[ \t]*#[ \t]*pragma[ \t]+warning[ \t]+disable\b(?<ids>[^\r\n]*)", RegexOptions.Multiline)]
    private static partial Regex Pragma();

    [GeneratedRegex(@"SuppressMessage(?:Attribute)?\s*\([^)]*(?:RS0030|""ApiDesign"")", RegexOptions.IgnoreCase)]
    private static partial Regex Suppression();

    [GeneratedRegex(@"<RunAnalyzers(?:DuringBuild)?>\s*false\s*<", RegexOptions.IgnoreCase)]
    private static partial Regex Off();

    [GeneratedRegex(@"<(?:GlobalAnalyzerConfigFiles|EditorConfigFiles)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ConfigItem();

    [GeneratedRegex(@"<IsTestProject>\s*true\s*<", RegexOptions.IgnoreCase)]
    private static partial Regex TestProject();

    [GeneratedRegex(@"<ImportDirectoryBuildProps>\s*false\s*<", RegexOptions.IgnoreCase)]
    private static partial Regex Unshared();

    [GeneratedRegex(@"<ItemGroup\s+Condition=""'\$\(IsTestProject\)' != 'true'"">\s*<PackageReference\s+Include=""Microsoft\.CodeAnalysis\.BannedApiAnalyzers""")]
    private static partial Regex SharedAnalyzer();
}
