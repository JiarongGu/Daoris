namespace Daoris.Driver;

/// <summary>
/// The codes a home file's operation refuses in (ONEWAY1u, D194 point 4): the page's catalogue keys, which the shell's <c>Refusals</c>
/// declares as these, and the driver's verbatim refusal for every other. One table, read by the route that answers the window and by the
/// terminal's command, so a refusal is one line both ways. The files are the machine's remotes, the browser's favorites and its settings.
/// </summary>
public static class HomeFileCodes
{
    /// <summary>A favorite that is no web page: only `http` and `https` pages are kept.</summary>
    public const string BrowserNotAPage = "BROWSER_NOT_A_PAGE";

    /// <summary>A favorites or settings file that could not be read, which an edit will not write over.</summary>
    public const string BrowserFileUnreadable = "BROWSER_FILE_UNREADABLE";

    /// <summary>An extensions setting that is neither `offer` nor `refuse`.</summary>
    public const string BrowserSettingUnknown = "BROWSER_SETTING_UNKNOWN";

    /// <summary>A browser choice that is neither `daoris` nor `edge`.</summary>
    public const string BrowserChoiceUnknown = "BROWSER_CHOICE_UNKNOWN";

    /// <summary>A links setting that is neither `system` nor `daoris`.</summary>
    public const string BrowserLinksUnknown = "BROWSER_LINKS_UNKNOWN";

    /// <summary>A remote with an address but no key, or the reverse: no remote in any loader.</summary>
    public const string RemoteHalfDeclared = "REMOTE_HALF_DECLARED";

    /// <summary>An address that is not <c>http://</c> or <c>https://</c>; said verbatim in the window, as it has no catalogue entry.</summary>
    public const string RemoteNotAnAddress = "REMOTE_NOT_AN_ADDRESS";

    /// <summary>The shell's <c>DRIVER_REFUSED</c>: any other refusal, said in its own sentence.</summary>
    public const string Refused = "DRIVER_REFUSED";

    /// <summary>Each check an operation refuses by, and the code it is said in.</summary>
    public static IReadOnlyDictionary<string, string> Table { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [HomeFileRefusal.NotAPage] = BrowserNotAPage,
        [HomeFileRefusal.Unreadable] = BrowserFileUnreadable,
        [HomeFileRefusal.ExtensionsUnknown] = BrowserSettingUnknown,
        [HomeFileRefusal.BrowserUnknown] = BrowserChoiceUnknown,
        [HomeFileRefusal.LinksUnknown] = BrowserLinksUnknown,
        [HomeFileRefusal.HalfDeclared] = RemoteHalfDeclared,
        [HomeFileRefusal.NotAnAddress] = RemoteNotAnAddress,
    };

    /// <summary>The code a refusal is said in: its check's, else the verbatim refusal.</summary>
    public static string Of(HomeFileRefusal refused) => Table.TryGetValue(refused.Check, out var code) ? code : Refused;
}

/// <summary>What a home file's verb says: its lines on the standard output, or the refusal's on the error stream, and its exit.</summary>
public sealed record FileSaid(IReadOnlyList<string> Out, IReadOnlyList<string> Err, int Exit)
{
    public static FileSaid Said(IEnumerable<string> lines, int exit = 0) => new([.. lines], [], exit);

    /// <summary>A refusal said in a line, <c>daoris: </c> and the sentence, with the exit the verb gave it: 2, or 1 for a policy refusal (D194 point 6).</summary>
    public static FileSaid Refused(string message, int exit = 2) => new([], [$"daoris: {message}"], exit);
}

/// <summary>
/// A home file's operation refusing by a named check (ONEWAY1u, as <see cref="ChoiceRefusal"/> carries a standing choice's): its sentence,
/// and the facts the window's catalogue says it with. <see cref="HomeFileCodes.Of"/> gives its code. Nothing was written.
/// </summary>
public sealed class HomeFileRefusal(string check, string message, params (string Key, string Value)[] facts) : DriverException(message)
{
    public const string NotAPage = "not-a-page";
    public const string Unreadable = "file-unreadable";
    public const string ExtensionsUnknown = "extensions-unknown";
    public const string BrowserUnknown = "browser-unknown";
    public const string LinksUnknown = "links-unknown";
    public const string HalfDeclared = "remote-half-declared";
    public const string NotAnAddress = "remote-not-an-address";

    public string Check { get; } = check;

    public IReadOnlyList<(string Key, string Value)> Facts { get; } = facts;
}
