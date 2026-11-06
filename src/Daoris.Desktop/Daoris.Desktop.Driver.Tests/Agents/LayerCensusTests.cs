using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AGENTLYNTAI1a (the agent-lyntai design §6.1, D248): Lyntai's agent layer is named in the desktop by <c>Agents/Layer/</c> and the
/// providers' folders alone, a vendor's registration and options by that vendor's folder alone, and the layer's own provider ids nowhere,
/// since Daoris registers under its own. So no other subdomain gains an edge to the library, and a disclosed break reaches two places.
/// </summary>
/// <remarks>
/// Read by name in the source text of the three shipped projects, as the provider census reads them, comments and strings out (an id
/// keeps its strings). A <c>using</c>, an alias, a <c>global::</c> or qualified name all spell <c>Lyntai</c>, and a file that names a
/// layer type without one cannot compile, so the one way past the token is a <c>global using</c>: that is refused everywhere, in the
/// layer's own folder too, because it would reach every file of the driver.
/// </remarks>
[Trait(Category.Name, Category.Census)]
public sealed class LayerCensusTests
{
    private static readonly string[] Projects = ["Daoris.Desktop.Driver", "Daoris.Desktop.Driver.Host", "Daoris.Desktop.Modules"];

    private const string Layer = "Daoris.Desktop.Driver/Agents/Layer/";
    private const string Providers = "Daoris.Desktop.Driver/Agents/Providers/";

    private static readonly Regex Library = new(@"\bLyntai\b", RegexOptions.CultureInvariant);
    private static readonly Regex GlobalUsing = new(@"\bglobal\s+using\s+(?:static\s+)?(?:\w+\s*=\s*)?(?:global::)?Lyntai\b", RegexOptions.CultureInvariant);
    private static readonly Regex OwnIds = new(@"""(?:claude|codex)-cli""", RegexOptions.CultureInvariant);

    /// <summary>The generic protocol registration: the layer's folder and the providers' folders, which a plugin's or dsh's door takes.</summary>
    private static readonly Regex Generic = new(@"\bAddAcpAgentSession\b", RegexOptions.CultureInvariant);

    /// <summary>A vendor's registration, options and namespace, each named by that vendor's folder alone.</summary>
    private static readonly (string Folder, Regex Pattern)[] Vendors =
    [
        ("ClaudeCode", new(@"\b(?:AddClaudeCliAgentSession|ClaudeAgentOptions|Providers\s*\.\s*ClaudeCli)\b", RegexOptions.CultureInvariant)),
        ("Codex", new(@"\b(?:AddCodexCliAgentSession|CodexAgentOptions|Providers\s*\.\s*CodexCli)\b", RegexOptions.CultureInvariant)),
    ];

    /// <summary>Each name of the layer a source holds outside its place, as <c>file: token, why</c>; none is a passing census.</summary>
    internal static List<string> Problems(IEnumerable<(string File, string Source)> sources)
    {
        var problems = new List<string>();
        foreach (var (file, source) in sources.OrderBy(each => each.File, StringComparer.Ordinal))
        {
            var code = StructureCheck.CodeOf(source);
            var inLayer = file.StartsWith(Layer, StringComparison.Ordinal);
            var inProviders = file.StartsWith(Providers, StringComparison.Ordinal);
            var folder = inProviders ? file[Providers.Length..].Split('/')[0] : null;

            if (GlobalUsing.IsMatch(code)) problems.Add($"{file}: global using Lyntai, which reaches every file of the driver");
            if (!inLayer && !inProviders && Library.IsMatch(code)) problems.Add($"{file}: Lyntai, outside Agents/Layer/ and the providers' folders");
            if (!inLayer && !inProviders && Generic.IsMatch(code)) problems.Add($"{file}: AddAcpAgentSession, outside Agents/Layer/ and the providers' folders");
            foreach (var (vendor, pattern) in Vendors)
            {
                var found = pattern.Match(code);
                if (found.Success && folder != vendor) problems.Add($"{file}: {found.Value}, {vendor}'s, outside Agents/Providers/{vendor}/");
            }

            var ids = OwnIds.Match(ProviderCensusTests.StripComments(source));
            if (ids.Success) problems.Add($"{file}: {ids.Value}, the layer's own id, where Daoris registers under its own");
        }

        return problems;
    }

    [Fact]
    public void No_file_names_the_agent_layer_outside_its_places()
    {
        var sources = Sources().ToList();
        var problems = Problems(sources);

        Assert.Contains(sources, each => each.File == "Daoris.Desktop.Driver/Agents/Registry/Providers.cs");
        Assert.True(problems.Count == 0, $"{problems.Count} problem(s):\n" + string.Join('\n', problems));
    }

    public static TheoryData<string, string, string> Planted => new()
    {
        { "Daoris.Desktop.Driver/Loop/Planted.cs", "using Lyntai.Agents;\nsealed class P { }", "Lyntai" },
        { "Daoris.Desktop.Driver/Events/Planted.cs", "sealed class P { global::Lyntai.Agents.AgentFailure? F; }", "Lyntai" },
        { "Daoris.Desktop.Driver/Agents/Contract/Planted.cs", "using Lyntai.Agents;\nsealed record UsagePart(IAgentUsageSource Source);", "Lyntai" },
        { "Daoris.Desktop.Driver/Agents/Registry/Planted.cs", "static class P { static void R(IServiceCollection s) => s.AddAcpAgentSession(\"dsh\"); }", "AddAcpAgentSession" },
        { "Daoris.Desktop.Driver/Agents/Providers/Codex/Planted.cs", "var options = new ClaudeAgentOptions();", "ClaudeAgentOptions" },
        { "Daoris.Desktop.Driver/Agents/Layer/Planted.cs", "var options = new CodexAgentOptions();", "CodexAgentOptions" },
        { "Daoris.Desktop.Driver/Agents/Providers/ClaudeCode/Planted.cs", "using Lyntai.Providers.CodexCli;", "Providers.CodexCli" },
        { "Daoris.Desktop.Driver/Agents/Providers/Dsh/Planted.cs", "const string Id = \"claude-cli\";", "\"claude-cli\"" },
        { "Daoris.Desktop.Driver/Agents/Layer/Planted.cs", "global using Lyntai.Agents;", "global using" },
        { "Daoris.Desktop.Modules/Platform/Planted.cs", "using Lyntai.Processes;", "Lyntai" },
        { "Daoris.Desktop.Driver.Host/Planted.cs", "using Turn = Lyntai.Agents.AgentTurn;", "Lyntai" },
    };

    [Theory]
    [MemberData(nameof(Planted))]
    public void A_name_of_the_layer_planted_outside_its_place_is_named(string file, string source, string token)
    {
        var problems = Problems([(file, source)]);

        Assert.True(problems.Count > 0 && problems.All(each => each.StartsWith(file + ":", StringComparison.Ordinal)) && problems.Any(each => each.Contains(token, StringComparison.Ordinal)),
            $"expected {token} named in {file}, got: {string.Join(" | ", problems)}");
    }

    /// <summary>Each place names what is its own, and a sentence or a literal naming the layer is no use of it.</summary>
    [Fact]
    public void Each_place_names_its_own_and_a_sentence_names_nothing()
    {
        var problems = Problems([
            ("Daoris.Desktop.Driver/Agents/Layer/Binding.cs",
                "using Lyntai.Agents;\nusing Lyntai.Providers.Acp;\nsealed class Binding { void R(IServiceCollection s) => s.AddAcpAgentSession(\"dsh\"); }"),
            ("Daoris.Desktop.Driver/Agents/Providers/ClaudeCode/Layer.cs",
                "using Lyntai.Providers.ClaudeCli;\nsealed class L { void R(IServiceCollection s) => s.AddClaudeCliAgentSession(\"claude-code\", new ClaudeAgentOptions()); }"),
            ("Daoris.Desktop.Driver/Agents/Providers/Dsh/Layer.cs", "using Lyntai.Providers.Acp;\nsealed class L { void R(IServiceCollection s) => s.AddAcpAgentSession(\"dsh\"); }"),
            ("Daoris.Desktop.Driver/Loop/Words.cs",
                "// Lyntai.Agents, ClaudeAgentOptions and \"claude-cli\"\nsealed class W { const string Text = \"Lyntai's layer: AddAcpAgentSession, claude-cli\"; }"),
        ]);

        Assert.Empty(problems);
    }

    private static IEnumerable<(string File, string Source)> Sources()
    {
        var root = EveryChildIsHandedTheToolsTests.SourceRoot();
        return Projects.SelectMany(project => SourceFiles.All(project, "*.cs"))
            .Select(file => (Path.GetRelativePath(root, file).Replace('\\', '/'), File.ReadAllText(file).Replace("\r\n", "\n")));
    }
}
