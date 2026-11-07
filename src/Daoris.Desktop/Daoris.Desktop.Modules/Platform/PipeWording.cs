namespace Daoris.Desktop;

/// <summary>
/// The part a value plays in the sentence a terminal's press is shown by. A wire's word,
/// told in lower case as <see cref="PipeNamed.Role"/>: added to, never renamed.
/// </summary>
public enum PipeRole
{
    /// <summary>A session, named as Sessions names it with its repository and short id beside, and its workspace where a name is in two.</summary>
    Session,

    /// <summary>Several sessions, each named as <see cref="Session"/> is.</summary>
    Sessions,

    /// <summary>Words the press carries, quoted as written, apart from Daoris's sentence.</summary>
    Words,

    /// <summary>The level, in the composer's words: when the words reach a working session.</summary>
    Level,

    /// <summary>Files, each by its path as sent, whole: two files share a name.</summary>
    Files,

    /// <summary>A quest, by its name and id.</summary>
    Quest,

    /// <summary>An ask, by its name and id.</summary>
    Ask,

    /// <summary>A repository, by its name.</summary>
    Repository,

    /// <summary>An agent, by its name.</summary>
    Agent,

    /// <summary>A value that chooses how the act runs, which the sentence says in words.</summary>
    Form,
}

/// <summary>One field of a press's payload its sentence names, by the top-level name its handler reads, and the part it plays.</summary>
public sealed record PipeWord(string Field, PipeRole Role);

/// <summary>
/// One value a press's sentence names: its role in lower case, its JSON pointer as <see cref="PipeConfirmationField.Name"/>
/// gives it, and its value as the card shows it. Every value stays in <c>fields</c> besides, so none leaves the card's view.
/// </summary>
public sealed record PipeNamed(string Role, string Name, string Value);

/// <summary>
/// Which values a terminal's press of each person's route names in the sentence the window shows it by, as its
/// <see cref="SaysAttribute"/> says them beside its mark.
/// </summary>
/// <remarks>
/// <para><b>Judged here, worded by the page</b>: the pipe's telling carries the named values by role as <c>named</c>, a field
/// added beside <c>fields</c>, never in place of it; the page words the act by <see cref="Key"/> in the reader's language and lists
/// every value no role names under the sentence, so none leaves the card's view. There is no quiet role: a value that looked quiet
/// (<c>SESSION_INPUT</c>'s <c>said</c>) chooses how the act runs, and is a <see cref="PipeRole.Form"/> the sentence says.</para>
///
/// <para>Every person's route has its act's words under <see cref="Key"/> in both catalogues, so a route's constant is never the
/// sentence. A route that says no values says the act alone, its values listed under it.</para>
/// </remarks>
public static class PipeWording
{
    /// <summary>The fields <paramref name="route"/> of <paramref name="module"/> names by role; null where it names none.</summary>
    public static IReadOnlyList<PipeWord>? For(string module, string route) =>
        PipeTargets.Daoris.Words.TryGetValue(PipeTargets.Key(module, route), out var row) ? row : null;

    /// <summary>
    /// The catalogue key of the act's words: <c>person.route.ROUTE</c> for the driver's, and <c>person.route.MODULE.ROUTE</c> for
    /// another module's, its name past <c>DAORIS.</c>, since two modules may name a route alike (<c>REMOTES</c>' <c>SET</c>). The
    /// page's twin is <c>routeKey</c> in <c>person/Confirmations.tsx</c>.
    /// </summary>
    public static string Key(string module, string route) =>
        string.Equals(module, DriverModule.Name, StringComparison.OrdinalIgnoreCase)
            ? $"person.route.{route}"
            : $"person.route.{(module.StartsWith("DAORIS.", StringComparison.OrdinalIgnoreCase) ? module["DAORIS.".Length..] : module).ToUpperInvariant()}.{route}";

    /// <summary>
    /// The values of <paramref name="fields"/> the sentence of <paramref name="route"/> names, in its row's order and each field's in
    /// the payload's: a field's own pointer, or one under it (<c>/paths/0</c> under <c>paths</c>). None where the route has no row.
    /// </summary>
    public static IReadOnlyList<PipeNamed> Named(string module, string route, IReadOnlyList<PipeConfirmationField> fields)
    {
        if (For(module, route) is not { Count: > 0 } row) return [];
        var named = new List<PipeNamed>();
        foreach (var word in row)
        {
            var at = $"/{word.Field}";
            foreach (var field in fields)
            {
                if (field.Name == at || field.Name.StartsWith(at + "/", StringComparison.Ordinal))
                {
                    named.Add(new PipeNamed(word.Role.ToString().ToLowerInvariant(), field.Name, field.Value));
                }
            }
        }

        return named;
    }
}
