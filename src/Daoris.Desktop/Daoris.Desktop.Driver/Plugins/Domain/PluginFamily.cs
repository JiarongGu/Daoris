namespace Daoris.Driver;

/// <summary>
/// A plugin id's shape, <c>&lt;family&gt;-&lt;name&gt;</c>: its family is the word before its first dash (<c>acme</c> in
/// <c>acme-quiet-hours</c>) and its tail the rest. Daoris's own plugins are the <see cref="Daoris"/> family; anyone else's keeps to its
/// own, so two people's <c>tickets</c> never meet. The tail is the id's own, never a second name a manifest declares.
/// </summary>
public static class PluginFamily
{
    public const string Daoris = "daoris";

    /// <summary>The id's family; null where it names none: no dash, or nothing before or after it.</summary>
    public static string? Of(string id)
    {
        var dash = id.IndexOf('-', StringComparison.Ordinal);
        return dash > 0 && dash < id.Length - 1 ? id[..dash] : null;
    }

    /// <summary>The id without its family (<c>azure-devops</c> in <c>daoris-azure-devops</c>); null where it names none.</summary>
    public static string? Tail(string id) => Of(id) is { } family ? id[(family.Length + 1)..] : null;
}
