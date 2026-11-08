namespace Daoris.Driver;

/// <summary>The codes of an account's line, appended to a failure or a resume. Part of the catalogue <see cref="NoteCodes"/> introduces.</summary>
public static class AccountNoteCodes
{
    // ——— An account's line, appended to a failure. A limit's cooling line names whose accounts they are, never which, and the window
    // its reset named. `account.cooling` is written by no code but notes on machines carry it, and the page words a line only with every
    // value its entry says, so a code that adds a value would show each of them as recorded: a limit that names no window has its own line.
    public static readonly NoteCode AccountCooling = new("account.cooling", ["until", "why"]) { Why = NoteCodes.Cooling };
    public static readonly NoteCode AccountCoolingWindow = new("account.cooling-window", ["until", "why", "owner", "window"]) { Why = NoteCodes.Cooling };
    public static readonly NoteCode AccountCoolingNoWindow = new("account.cooling-no-window", ["until", "why", "owner"]) { Why = NoteCodes.Cooling };
    public static readonly NoteCode AccountCoolingModel = new("account.cooling-model", ["until", "why", "owner", "model"]) { Why = NoteCodes.Cooling };
    public static readonly NoteCode AccountRefused = new("account.refused", ["owner"]);
    public static readonly NoteCode AccountRefusedOwn = new("account.refused-own", ["owner"]);
    public static readonly NoteCode AccountSignedOut = new("account.signed-out", ["owner"]);
    public static readonly NoteCode AccountSignedOutOwn = new("account.signed-out-own", ["owner"]);

    // The account a start ran on did not offer, or refused, the model its actor asked, before any prompt: the account's fault,
    // never the quest's, and no account is named, since the note travels.
    public static readonly NoteCode AccountRefusedModel = new("account.refused-model", ["agent", "model"]);

    /// <summary>
    /// The account's lines that say its agent or its provider refused the account a start ran on: its sign-in, its credential or
    /// the model asked. The fault is the account's, never the quest's, so a failure carrying one is no strike
    /// (<see cref="ServiceClient.ReadStrikes"/>), read by the code the driver wrote, never by the words.
    /// </summary>
    public static IReadOnlyList<NoteCode> AccountsOwn { get; } =
        [AccountRefused, AccountRefusedOwn, AccountSignedOut, AccountSignedOutOwn, AccountRefusedModel];

    // ——— Why the account a resume asked for could not carry the person's words, after a line whose reason is `account`: no account
    // is named, since the note travels.
    public static readonly NoteCode AccountResumeSignedOut = new("account.resume-signed-out", []);
    public static readonly NoteCode AccountResumeRefused = new("account.resume-refused", []);
    public static readonly NoteCode AccountResumeGone = new("account.resume-gone", []);
    public static readonly NoteCode AccountResumeOutside = new("account.resume-outside", []);
    public static readonly NoteCode AccountResumeKept = new("account.resume-kept", []);
    public static readonly NoteCode AccountResumeNewSession = new("account.resume-new-session", []);
    public static readonly NoteCode AccountResumeNotOffered = new("account.resume-not-offered", []);
}
