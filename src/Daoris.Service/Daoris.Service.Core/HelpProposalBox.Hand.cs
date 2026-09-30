namespace Daoris.Knowledge;

/// <summary>The <c>hand</c> kind's writer (WSR5b): a branch a landing made, handed to a landing plugin afterwards, with the proposal's <c>repository</c>.</summary>
public sealed partial class HelpProposalBox
{
    /// <summary>
    /// Write one hand-off proposal (WSR5b): a branch a landing made, handed to a landing plugin afterwards —
    /// the review's <i>hand it to</i> and <c>daoris-driver trees hand</c>. Whether the record holds that branch,
    /// and whether the plugin can land work there, is the driver's.
    /// </summary>
    /// <param name="target">The session that landed the branch, or the branch.</param>
    /// <param name="repository">The repository it is in, where a branch's name alone is in several; null otherwise.</param>
    /// <param name="plugin">The plugin to hand it to, where the repository's landing rule names none; null for the rule's.</param>
    public (string? Id, string Message) ProposeHand(string target, string? repository, string? plugin, string why, string? session, DateTimeOffset at)
    {
        var named = Blank(target);
        var holder = Blank(repository);
        var to = Blank(plugin);
        if (string.IsNullOrWhiteSpace(why)) return NoReason;
        var refused = named is null ? "a hand-off names the session that landed the branch, or the branch itself."
            : Word(named, "a session or a branch") ?? (holder is null ? null : Word(holder, "a repository"))
              ?? (to is null || PluginId.IsMatch(to) ? null
                  : $"`{to}` is not a plugin id — one is lowercase letters, digits, dots and dashes, as the room lists the plugins installed here.");
        if (refused is not null) return (null, $"{Capital(refused)} Nothing was proposed.");

        return Write(writer =>
        {
            writer.WriteString("kind", "hand");
            writer.WriteString("door", "hand");
            writer.WriteString("target", named);
            writer.WriteNull("workspace");
            Nullable(writer, "value", to);
            writer.WriteNull("sentence");
            Nullable(writer, "repository", holder);
        }, why, session, at);
    }

    /// <summary>What a plugin's id may be — the catalogue's own shape (the driver's <c>LandingRules</c> holds the same).</summary>
    private static readonly System.Text.RegularExpressions.Regex PluginId =
        new("^[a-z0-9][a-z0-9.-]*$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);
}
