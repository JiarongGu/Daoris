namespace Daoris.Driver;

/// <summary>
/// What became of a checkout read (HELPMODE1c4), written beside it for the tool that asked, in the chosen settings' answer's fields and
/// one more: the operation, whether it was read, a code where it was not, the words the agent reads, and <see cref="Text"/>, what was
/// read. A twin: the service's <c>CheckoutBox</c> reads <c>said</c> and <c>text</c> by the same names.
/// </summary>
public sealed record CheckoutAnswer(string Operation, bool Done, string? Code, string Said)
{
    /// <summary>A file's text, at most <see cref="HelpCheckouts.Bound"/> of it; a folder's entries, one a line; git's answer. Null where nothing was read.</summary>
    public string? Text { get; init; }

    /// <summary>No service answered, so no record could be read.</summary>
    public const string NoService = "no-service";

    /// <summary>The session is not an Ask Daoris conversation of this machine's.</summary>
    public const string NotHelp = "not-help";

    /// <summary>The request named no operation of the three, no repository, or a path where its operation takes none.</summary>
    public const string Named = "named";

    /// <summary>No repository of that name is registered here.</summary>
    public const string Unknown = "unknown";

    /// <summary>The repository is registered in another workspace than the conversation's.</summary>
    public const string Outside = "outside";

    /// <summary>The repository has no checkout on this machine.</summary>
    public const string NoCheckout = "no-checkout";

    /// <summary>The repository's own reading by agents outside it is switched off (D107), so it is no checkout of the grant's.</summary>
    public const string NotReadable = "not-readable";

    /// <summary>The person turned the checkout off in configured access.</summary>
    public const string NotGranted = "not-granted";

    /// <summary>Not handed at this conversation's start: it holds from its next turn.</summary>
    public const string NotHanded = "not-handed";

    /// <summary>
    /// A path that is no path inside the checkout: <c>..</c>, an absolute path, a drive or a stream, a wildcard, git's own folder, or
    /// a link leading out of it.
    /// </summary>
    public const string Escapes = "escapes";

    /// <summary>Git ignores it, or a folder it lies in (<c>local/</c>, build output, secrets kept ignored).</summary>
    public const string Ignored = "ignored";

    /// <summary>No file or folder by that path in the checkout.</summary>
    public const string Missing = "missing";

    /// <summary>The read failed on its way: git could not say what it ignores, or the disk or git refused.</summary>
    public const string Failed = "failed";

    /// <summary>A refusal of <paramref name="operation"/> with its code, its words ending that nothing was read.</summary>
    public static CheckoutAnswer Refusal(string operation, string code, string said) => new(operation, false, code, $"{said} Nothing was read.");
}
