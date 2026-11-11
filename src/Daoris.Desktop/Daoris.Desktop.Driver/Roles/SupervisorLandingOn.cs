namespace Daoris.Driver;

/// <summary>
/// The landing decision a run's step has from the supervisor, whichever session of the run's chain kept it: the newest kept on the attempt,
/// read from memory, since only memory holds a decision that is live; a restart's sweep closes the rest.
/// </summary>
internal static class SupervisorLandingOn
{
    public static SupervisorLandingView? Newest(SupervisorBook book, string run, string node, int attempt) =>
        book.Landings.Values
            .Where(each => each.Run == run && each.Node == node && each.Attempt == attempt)
            .Select(each => SupervisorLandingView.Read(book.Files, each.Session, each.Id)?.Over(each))
            .OfType<SupervisorLandingView>()
            .OrderBy(view => view.KeptAt).ThenBy(view => view.Id, StringComparer.Ordinal)
            .LastOrDefault();
}
