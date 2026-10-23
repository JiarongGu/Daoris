namespace Daoris.Knowledge;

/// <summary>
/// The words a session is handed for the supervisor's answer (SUPERVISOR1a-i, D208; the supervisor design §4.5): composed from the
/// decision's chosen label alone, never from a word's text, by the service at the release and by the driver at the hand. The driver
/// keeps a twin (<c>SupervisorHanded</c>), held to this one by a test on each side.
/// </summary>
/// <remarks>
/// <b>It grants nothing</b>, the chosen option's <c>does</c> included: choosing says which offered answer to follow, and every act
/// that is the owner's (a go-ahead, a sign-in, access, spending, a push, a publication, a deletion, a history rewrite) is named as
/// not granted, so a <c>does</c> of <i>I push now</i> is never read as the push's authorization. English, as every handed sentence is.
/// </remarks>
public static class SupervisorWord
{
    /// <summary>The paragraph for <paramref name="label"/>, one of the labels the session itself offered.</summary>
    public static string Handed(string label) =>
        $"The supervisor the owner appointed for this repository chose \"{label}\" among the answers you offered, on the owner's behalf. "
        + "These are not the owner's own words. Choosing tells you which of your offered answers to follow; it grants nothing: no "
        + "go-ahead, sign-in, access, spending, push, publication, deletion or history rewrite, and it overrides no refused go-ahead and "
        + "no repository document that asks the owner. If following it needs any of those, ask through go_ahead_ask or wait for the "
        + "owner. The owner may still say otherwise; their words win.";
}
