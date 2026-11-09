using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The build's banned-name analyzer run over a test's compilation, as every shipping project's build runs it: the package's own
/// analyzers, a list handed as <c>BannedSymbols.txt</c>, and each source's severities read from the desktop tree's one
/// <c>.editorconfig</c> by the source's path under the tree.
/// </summary>
internal static class BannedAnalyzer
{
    private static readonly Lazy<ImmutableArray<DiagnosticAnalyzer>> Loaded = new(() =>
        [.. Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "BannedApi"), "*.dll").Select(Assembly.LoadFrom).SelectMany(Types)
            .Where(type => !type.IsAbstract && typeof(DiagnosticAnalyzer).IsAssignableFrom(type)
                && type.GetCustomAttributes<DiagnosticAnalyzerAttribute>().Any(each => each.Languages.Contains(LanguageNames.CSharp)))
            .Select(type => (DiagnosticAnalyzer)Activator.CreateInstance(type)!)]);

    /// <summary>The rule's diagnostics over <paramref name="compilation"/> with <paramref name="ids"/> banned: each source's path and the severity the build gives it.</summary>
    internal static async Task<List<(string File, DiagnosticSeverity Severity)>> RunAsync(CSharpCompilation compilation, IEnumerable<string> ids)
    {
        var editorConfig = Path.Combine(DesktopCompilations.Desktop, ".editorconfig");
        var set = AnalyzerConfigSet.Create(ImmutableArray.Create(AnalyzerConfig.Parse(File.ReadAllText(editorConfig), editorConfig)));
        var severities = DesktopCompilations.Trees(compilation).ToDictionary(
            tree => tree, tree => set.GetOptionsForSourcePath(Path.GetFullPath(Path.Combine(DesktopCompilations.Desktop, tree.FilePath))).TreeOptions);
        var configured = compilation.WithOptions(compilation.Options.WithSyntaxTreeOptionsProvider(new Severities(severities)));
        var list = new Listed(Path.Combine(DesktopCompilations.Desktop, "BannedSymbols.txt"), string.Join('\n', ids));
        var found = await configured.WithAnalyzers(Loaded.Value, new AnalyzerOptions([list])).GetAnalyzerDiagnosticsAsync();
        return [.. found.Where(each => each.Id == BoundaryConfig.Rule).Select(each => (each.Location.SourceTree!.FilePath, each.Severity))];
    }

    private static IEnumerable<Type> Types(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException partial)
        {
            return partial.Types.OfType<Type>();
        }
    }

    private sealed class Listed(string path, string text) : AdditionalText
    {
        public override string Path => path;

        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(text);
    }

    private sealed class Severities(Dictionary<SyntaxTree, ImmutableDictionary<string, ReportDiagnostic>> each) : SyntaxTreeOptionsProvider
    {
        public override GeneratedKind IsGenerated(SyntaxTree tree, CancellationToken cancellationToken) => GeneratedKind.NotGenerated;

        public override bool TryGetDiagnosticValue(SyntaxTree tree, string diagnosticId, CancellationToken cancellationToken, out ReportDiagnostic severity)
        {
            severity = default;
            return each.TryGetValue(tree, out var options) && options.TryGetValue(diagnosticId, out severity);
        }

        public override bool TryGetGlobalDiagnosticValue(string diagnosticId, CancellationToken cancellationToken, out ReportDiagnostic severity)
        {
            severity = default;
            return false;
        }
    }
}
