using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// DEDUP6 (D166's note): the one yes-or-no for a name that is joined to the disk. Ten copies of the path check disagreed on
/// <c>\</c>, <c>:</c>, <c>..</c> and control characters; a caller keeps its own refusal sentence and asks this. Containment
/// and existence stay with the caller, and a path handed to git only (<c>a:b</c> is a legal tree entry) keeps its own rule.
/// </summary>
public static partial class SafeNames
{
    private static readonly HashSet<string> DeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    /// <summary>
    /// A repository path joined to the disk: not empty; no control character, NUL or DEL; no <c>\</c> and no <c>:</c>; not
    /// starting with <c>/</c>; every <c>/</c> part not empty, not <c>.</c> or <c>..</c>, not ending in a space or a dot (Windows
    /// strips either, so <c>a.</c> opens <c>a</c>), and not a Windows device name, with or without an extension.
    /// </summary>
    public static bool IsRepositoryPath(string path) =>
        path.Length > 0 && !HasUnsafeCharacter(path, allowSlash: true) && path.Split('/').All(IsSafePart);

    /// <summary>
    /// An id used as a file or folder name under the home: not empty, one name (no <c>/</c>, <c>\</c> or <c>:</c>, which on
    /// Windows names a stream), no control character, no <c>..</c>, no trailing dot or space, and no device name. The shape
    /// each kind already has is its own; this is what any of them must also hold.
    /// </summary>
    public static bool IsFolderId(string id) =>
        id.Length > 0 && !HasUnsafeCharacter(id, allowSlash: false) && !id.Contains("..", StringComparison.Ordinal) && IsSafePart(id);

    /// <summary>
    /// A quest handed, an opinion gate's chain, a workflow run, the Ask Daoris door's suffix: <c>[A-Za-z0-9_-]{1,64}</c>, and
    /// not a Windows device name, since each names a file under the home (<c>CON.json</c> would open the console).
    /// </summary>
    public static bool IsPlainId(string id) => PlainId().IsMatch(id) && IsSafePart(id);

    /// <summary>
    /// A kebab id: lower-case words joined by single dashes, and not a device name, since a workflow's or a review's names a
    /// file. Each caller adds its own length cap.
    /// </summary>
    public static bool IsKebabId(string id) => KebabId().IsMatch(id) && IsSafePart(id);

    private static bool HasUnsafeCharacter(string text, bool allowSlash) =>
        text.Any(c => c < 0x20 || c == '\u007f' || c is '\\' or ':' || (!allowSlash && c == '/')) || (allowSlash && text[0] == '/');

    private static bool IsSafePart(string part) =>
        part is not ("" or "." or "..")
        && !part.EndsWith('.') && !part.EndsWith(' ')
        && !DeviceNames.Contains(part.Split('.')[0]);

    [GeneratedRegex(@"^[A-Za-z0-9_-]{1,64}\z")]
    private static partial Regex PlainId();

    [GeneratedRegex(@"^[a-z0-9]+(?:-[a-z0-9]+)*\z")]
    private static partial Regex KebabId();
}
