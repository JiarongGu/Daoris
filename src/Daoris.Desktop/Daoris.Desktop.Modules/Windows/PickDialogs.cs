using Shenora.Modules.FileDialog;

namespace Daoris.Desktop;

/// <summary>Which file the picker is asked for: a program (a tool's *Browse…*), a key (an SSH key's import) or any file.</summary>
public enum PickKind
{
    /// <summary>An executable: <c>.exe</c>, <c>.cmd</c> or <c>.bat</c> first.</summary>
    Program,

    /// <summary>A private key file: its usual names first.</summary>
    Key,

    /// <summary>Any file.</summary>
    File,
}

/// <summary>What the picker is shown with: <paramref name="Title"/> and <paramref name="Filters"/>, the first the one chosen on opening.</summary>
public sealed record PickDialog(string Title, IReadOnlyList<FileDialogFilter> Filters);

/// <summary>
/// The one place that says what each <see cref="PickKind"/> is shown with, so <see cref="PickFile"/> stays the one file dialog.
/// </summary>
/// <remarks>
/// The kit's Windows dialog writes every extension of a filter as <c>*.ext</c>, which cannot name a file with no extension, and
/// a key's usual names (<c>id_ed25519</c>, <c>id_rsa</c>) have none. A filter's extension that carries <c>;</c> is passed through
/// to the dialog's pattern list as written, so the key's filter names them in its first extension; the kit's Chromium host,
/// which Daoris does not use for dialogs, would not read it so.
/// </remarks>
public static class PickDialogs
{
    private const string AllFiles = "All files";

    /// <summary>A key's usual names beside its usual extensions, after <c>*.pem</c> as the first pattern the kit prefixes.</summary>
    private static readonly string KeyNames = "pem;*.ppk;*.key;id_ed25519;id_ecdsa;id_rsa;id_dsa";

    /// <summary>The dialog for a kind; <paramref name="title"/> is the page's, in its language, and a blank one is the kind's own.</summary>
    public static PickDialog For(PickKind kind, string? title) => kind switch
    {
        PickKind.Key => new(Titled(title, "Choose a key file"),
            [new FileDialogFilter { Name = "Keys", Extensions = [KeyNames] }, Every()]),
        PickKind.File => new(Titled(title, "Choose a file"), [Every()]),
        _ => new(Titled(title, "Choose a program"),
            [new FileDialogFilter { Name = "Programs", Extensions = ["exe", "cmd", "bat"] }, Every()]),
    };

    /// <summary>The kind a page's word names (<c>program</c>, <c>key</c>, <c>file</c>); none, or a word not known, is a program.</summary>
    public static PickKind Kind(string? word) => word?.Trim().ToLowerInvariant() switch
    {
        "key" => PickKind.Key,
        "file" => PickKind.File,
        _ => PickKind.Program,
    };

    private static string Titled(string? title, string own) => string.IsNullOrWhiteSpace(title) ? own : title;

    private static FileDialogFilter Every() => new() { Name = AllFiles, Extensions = ["*"] };
}
