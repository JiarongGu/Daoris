namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A grid of independent cases run a few at a time, their results handed back in the grid's own order. Each case of a grid builds its
/// own scratch home, so a case is bound by the file system and a run in turn spends minutes waiting on it; the assertions read the
/// results exactly as the serial loop did.
/// </summary>
internal static class GridRun
{
    private const int Width = 8;

    public static async Task<IReadOnlyList<(TCase Case, TResult Result)>> InOrder<TCase, TResult>(
        IEnumerable<TCase> grid, Func<TCase, Task<TResult>> run)
    {
        var cases = grid.ToList();
        using var turn = new SemaphoreSlim(Width);
        var results = await Task.WhenAll(cases.Select(async each =>
        {
            await turn.WaitAsync();
            try
            {
                return await Task.Run(() => run(each));
            }
            finally
            {
                turn.Release();
            }
        }));
        return [.. cases.Zip(results, (each, result) => (each, result))];
    }
}
