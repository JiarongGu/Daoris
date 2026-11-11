namespace Daoris.Driver;

/// <summary>
/// The person's own memory file, <c>CLAUDE.md</c> in their tool home, which every run of their own reads. A session's harness reads the
/// one in its account's home instead, so the person's reaches it appended to its system prompt: on the native door as the file named by
/// <c>--append-system-prompt-file</c>, read where it lies, and on the protocol door as its text on the Agent SDK's own preset
/// (<see cref="ClaudeProtocolDialect"/>). The file is read, never written; a run in the person's own home reads it by itself.
/// </summary>
/// <remarks>
/// <c>--add-dir</c> of the tool home would load the file too, but it grants the folder's files, the credentials beside it included. An
/// <c>@</c> import in the file is handed as written: the agent reads the path it names as any other.
/// </remarks>
internal static class ClaudeOwnMemory
{
    /// <summary>The flag that appends a file to the system prompt for one run.</summary>
    public const string Flag = "--append-system-prompt-file";

    private const string FileName = "CLAUDE.md";

    /// <summary>The person's memory file where they left the family on and the session runs elsewhere; null where none is handed.</summary>
    public static string? File(OwnTools tools)
    {
        if (!tools.Families.HasFlag(OwnFamilies.Memory) || ClaudeOwnHome.InOwnHome(tools)) return null;
        var file = Path.Combine(ClaudeOwnHome.Place(tools), FileName);
        return System.IO.File.Exists(file) ? file : null;
    }

    /// <summary>The memory file's text, for a door that takes it as text; null where none is handed or it cannot be read.</summary>
    public static string? Text(OwnTools tools)
    {
        try
        {
            return File(tools) is { } file && System.IO.File.ReadAllText(file) is { Length: > 0 } text ? text : null;
        }
        catch (Exception problem) when (problem is IOException or UnauthorizedAccessException)
        {
            // A file their own run cannot read appends nothing there either.
            return null;
        }
    }
}
