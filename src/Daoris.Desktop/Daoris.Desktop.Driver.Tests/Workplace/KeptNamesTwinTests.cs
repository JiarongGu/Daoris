using System.Reflection;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// SESSDEL1c (D126's SESSDEL1 note; <c>.claude/knowledge/twins.md</c>): the names a delete's <c>stayed</c> carries, which the page
/// words as <c>work.delete.kept.&lt;name&gt;</c>, are held to one table, this suite's <c>fixtures/kept-names.json</c>, which the
/// page's <c>sessionActs.test.tsx</c> holds both catalogues and its toast to as well. Here <see cref="SessionHomeFiles"/>' names
/// are the table's rows, name for name and word for word, so a name added here alone fails here, and one added to the table
/// alone fails here until the driver has it.
/// </summary>
public sealed class KeptNamesTwinTests
{
    [Fact]
    public void Each_name_a_delete_says_stayed_is_the_tables_row_for_row() => Assert.Equal(Table(), Constants());

    /// <summary><see cref="SessionHomeFiles"/>' string constants, each its name and its word, by name.</summary>
    private static List<(string Name, string Word)> Constants() =>
    [
        .. typeof(SessionHomeFiles).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (field.Name, (string)field.GetRawConstantValue()!))
            .OrderBy(row => row.Name, StringComparer.Ordinal),
    ];

    /// <summary>The shared table's names, each its name and its word, by name.</summary>
    private static List<(string Name, string Word)> Table()
    {
        using var table = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "kept-names.json")));
        return
        [
            .. table.RootElement.GetProperty("names").EnumerateObject()
                .Select(row => (row.Name, row.Value.GetString()!))
                .OrderBy(row => row.Name, StringComparer.Ordinal),
        ];
    }
}
