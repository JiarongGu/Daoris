using System.Collections.Immutable;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A file outside the store that keeps a banned name: the file under the desktop tree, the banned symbols' ids it may name, and why.</summary>
internal sealed record Allowance(string File, ImmutableArray<string> Symbols, string Why);

/// <summary>
/// The allowance table (the design's §57). A file outside the store's folder and the home's table that keeps a banned name is a row,
/// and each row is one section of the desktop tree's one <c>.editorconfig</c>. A row lands in the row that bans its first symbol, and a
/// symbol banned later joins its row then, so every row builds green; until the first ban the table is empty.
/// </summary>
internal static class DeclarationAllowances
{
    /// <summary>Each row, by its file.</summary>
    internal static readonly ImmutableArray<Allowance> Rows = [];

    /// <summary>
    /// The walk's judgement: red on a banned use outside the two places no row names (a second banned symbol in an allowed file among
    /// them, since a section quiets its file whole), on a name the walk could not follow, on a row's symbol its file no longer names, on
    /// a row whose file is gone, on a row with no section, and on a section that is no row's.
    /// </summary>
    internal static List<string> Problems(IEnumerable<BannedUse> uses, IReadOnlyCollection<Allowance> rows, IReadOnlyCollection<string> sections, Func<string, bool> exists)
    {
        var problems = new List<string>();
        var found = uses.Where(use => !BoundaryConfig.InPlace(use.File)).ToList();
        foreach (var use in found.Where(use => !rows.Any(row => row.File == use.File && row.Symbols.Contains(use.Symbol))))
        {
            problems.Add(use.Symbol.StartsWith('?')
                ? $"{use.File}:{use.Line} names {use.Symbol[1..]}, which does not bind, so the walk cannot follow it"
                : $"{use.File}:{use.Line} names {use.Symbol}, which no allowance row names");
        }

        foreach (var row in rows)
        {
            if (!exists(row.File))
            {
                problems.Add($"{row.File} is an allowance row and is gone");
                continue;
            }

            foreach (var symbol in row.Symbols.Where(symbol => !found.Any(use => use.File == row.File && use.Symbol == symbol)))
                problems.Add($"{row.File} is an allowance row for {symbol} and names it no longer");
            if (!sections.Contains(row.File)) problems.Add($"{row.File} is an allowance row with no section");
        }

        foreach (var section in sections.Where(section => !rows.Any(row => row.File == section)))
            problems.Add($"[{section}] quiets {BoundaryConfig.Rule} and is no allowance row's");
        return problems;
    }
}
