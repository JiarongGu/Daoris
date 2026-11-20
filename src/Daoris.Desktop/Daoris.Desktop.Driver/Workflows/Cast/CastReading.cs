namespace Daoris.Driver;

/// <summary>
/// The place's cast read once for a run's place, role to holder (<see cref="PlaceCastBook.At"/>): what a step's wait on a role other than its
/// step's own reads for that role's holder (<see cref="StepWaiting.HolderOf"/>), as the step's own role reads its tenure.
/// </summary>
public static class CastReading
{
    public static Func<string, string> At(PlaceCastBook book, PlaceRiskBook? risk, string? workspace, string? repository)
    {
        var place = (risk ?? PlaceRiskBook.Unwritten).At(workspace, repository);
        return role => book.At(role, workspace, repository, place).Holder;
    }

    /// <summary>The cast at the place as <paramref name="home"/>'s files say it now: its cast and its machine's place risk.</summary>
    public static Func<string, string> Home(string home, string? workspace, string? repository) =>
        At(new PlaceCastFiles(home).Book(), DriverConfig.Load(DriverConfig.ResolvePath(home)).PlaceRisk, workspace, repository);

    /// <summary>Each role's cast at the place and why, the files read once at the first role asked.</summary>
    public static Func<string, CastNow> Now(string home, string? workspace, string? repository)
    {
        var read = new Lazy<(PlaceCastBook Book, PlaceRiskNow Place)>(() =>
            (new PlaceCastFiles(home).Book(), DriverConfig.Load(DriverConfig.ResolvePath(home)).PlaceRisk.At(workspace, repository)));
        return role => read.Value.Book.At(role, workspace, repository, read.Value.Place);
    }

    public static Func<string, bool> AgentHolds(string home, PluginPlace place)
    {
        var cast = new Lazy<Func<string, CastNow>>(() => Now(home, place.Workspace, place.Repository));
        return role => ServedBy(cast.Value(role));
    }

    /// <summary>Whether an agent session serves each role at the place as <paramref name="home"/>'s files say it now.</summary>
    public static Func<string, bool> ServesAt(string home, string? workspace, string? repository)
    {
        var cast = Now(home, workspace, repository);
        return role => ServedBy(cast(role));
    }

    public static Func<string, bool> Serves(PlaceCastBook book, PlaceRiskBook? risk, string? workspace, string? repository)
    {
        var place = (risk ?? PlaceRiskBook.Unwritten).At(workspace, repository);
        return role => ServedBy(book.At(role, workspace, repository, place));
    }

    private static bool ServedBy(CastNow now) => now.Why != CastWhy.Default && now.Holder != CastHolders.Person;
}
