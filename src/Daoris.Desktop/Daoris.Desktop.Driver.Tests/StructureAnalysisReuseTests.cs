namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The structure analysis's reuse of what it bound (<see cref="StructureAnalysis"/>), over sources made in memory: a type's description
/// is read once whatever names it, and no edge is lost for it. Behaviour of the analysis, so no census: the structure census over the
/// driver's tree is <c>StructureTests</c>.
/// </summary>
public sealed class StructureAnalysisReuseTests
{
    /// <summary>A planted source: the driver's one namespace, then the body, so the body's first line is line 2.</summary>
    private static (string Path, string Text) Src(string path, string body) => (path, "namespace Daoris.Driver;\n" + body);

    [Fact]
    public void Repeated_bound_generic_types_materialize_their_declaration_once_without_losing_the_first_edge()
    {
        var repeated = string.Join('\n', Enumerable.Range(0, 256).Select(index => $"Outer<int> field{index};"));
        var analysis = StructureAnalysis.Of([
            Src("Course/Outer.cs", "public class Outer<T> { }"),
            Src("Platform/Reads.cs", "class Reads {\n" + repeated + "\n}"),
        ]);

        var edge = Assert.Single(analysis.Edges);
        Assert.Equal(("Platform/Reads.cs", 3, "Outer"), (edge.File, edge.Line, edge.Type));
        Assert.Equal(["Course/Outer.cs"], edge.DeclaredIn);
        Assert.Equal(1, analysis.SymbolQueries);
        Assert.Equal(1, analysis.TypeDescriptionsRead);
    }

    [Fact]
    public void Ambiguous_spellings_still_bind_each_occurrence_but_share_the_original_type_description()
    {
        var repeated = string.Join('\n', Enumerable.Range(0, 256).Select(index => $"Outer<int> field{index};"));
        var analysis = StructureAnalysis.Of([
            Src("Course/Outer.cs", "public class Outer<T> { }"),
            ("Course/Other.cs", "namespace Other; public class Outer<T> { }"),
            Src("Platform/Reads.cs", "class Reads {\n" + repeated + "\n}"),
        ]);

        Assert.Equal("Outer", Assert.Single(analysis.Edges).Type);
        Assert.Equal(256, analysis.SymbolQueries);
        Assert.Equal(1, analysis.TypeDescriptionsRead);
    }

    [Fact]
    public void Reuse_keeps_alias_shadowing_attribute_shorthand_partial_types_and_same_named_members()
    {
        var analysis = StructureAnalysis.Of([
            ("Course/First.cs", """
                namespace First;
                public class Same { }
                public class Unique { }
                public class MarkAttribute : System.Attribute { }
                public partial class Part { }
                public class Outer<T> { public class Inner { } }
                """),
            ("Course/Second.cs", "namespace Second { public class Same { } public class Mark { } public class Aliased { } } namespace First { public partial class Part { } }"),
            ("Platform/Aliases.cs", "global using Unique = Second.Aliased;"),
            ("Platform/OtherReads.cs", "class OtherReads { Second.Same field; }"),
            ("Platform/Reads.cs", """
                using Inner = First.Outer<int>.Inner;
                class Reads {
                    static int Same;
                    static int Before() => Same;
                    First.Same first;
                    Second.Same second;
                    First.Unique original;
                    Unique alias;
                    First.Part part;
                    Inner nested;
                    [First.Mark] void Marked() { }
                    Second.Mark plain;
                }
                """),
        ]);
        var repeated = analysis.WithoutReuse();
        static IEnumerable<string> Edges(StructureAnalysis found) => found.Edges.Select(edge =>
            $"{edge.File}:{edge.Line}:{edge.Type}:{string.Join('|', edge.DeclaredIn)}");
        Assert.Equal(Edges(repeated), Edges(analysis));
        Assert.Equal(2, analysis.Edges.Single(edge => edge.Type == "Part").DeclaredIn.Count);
        Assert.Contains(analysis.Edges, edge => edge.Type == "Outer.Inner");
        Assert.Contains(analysis.Edges, edge => edge.Type == "MarkAttribute");
        Assert.Contains(analysis.Edges, edge => edge.Type == "Mark");
        // The existing edge key is the nested type spelling within each file: its first binding stays the one recorded.
        Assert.Contains(analysis.Edges, edge => edge.File == "Platform/Reads.cs" && edge.Type == "Same" && edge.DeclaredIn.SequenceEqual(["Course/First.cs"]));
        Assert.Contains(analysis.Edges, edge => edge.File == "Platform/OtherReads.cs" && edge.Type == "Same" && edge.DeclaredIn.SequenceEqual(["Course/Second.cs"]));
        Assert.Contains(analysis.Edges, edge => edge.Type == "Unique");
        Assert.Contains(analysis.Edges, edge => edge.File == "Platform/Reads.cs" && edge.Type == "Aliased");
    }
}
